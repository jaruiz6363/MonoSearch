namespace MonoSearch;

/// <summary>A point of the meridional section, mm: z along the axis (light travels +z), y up, origin at the centre.</summary>
public readonly record struct Pt(double Z, double Y);

/// <summary>One traced ray from in front of the lens to the image, and the field it belongs to (0-based).</summary>
public sealed record RayPath(int Field, Pt[] Points);

/// <summary>
/// Meridional section of a candidate for drawing: real rays traced at the primary wavelength with the stop filled
/// as OpticStudio's real ray aiming fills it, and the angular extent each surface needs to pass them.
/// Angles are half-angles about the axis, radians; pi/2 or more means a complete sphere.
/// </summary>
public sealed class LensLayout
{
    public required double R1 { get; init; }
    public required double R2 { get; init; }
    public required double ImageRadius { get; init; }
    public required double StopSemi { get; init; }
    public required double ShellAngle { get; init; }
    public required double BallAngle { get; init; }
    public required double ImageAngle { get; init; }
    public required double[] FieldAngles { get; init; }
    public required double Wavelength { get; init; }
    public required List<RayPath> Rays { get; init; }
    public int Failed { get; init; }

    /// <summary>The shells' outer surfaces reach their equator: front and rear shells close into one spherical shell.</summary>
    public bool FullShell => ShellAngle >= Math.PI / 2 - 1e-3;
    public bool FullBall => BallAngle >= Math.PI / 2 - 1e-3;

    /// <summary>Traces <paramref name="raysPerField"/> rays across the pupil at each field angle (degrees).</summary>
    public static LensLayout Build(Candidate c, double halfFovDeg, int raysPerField = 7)
    {
        var sp = c.Spec;
        double lam = sp.Lambda[sp.Primary];
        double n2 = c.Outer.Index(lam), n3 = c.Inner.Index(lam);
        double r1 = c.E.R1, r2 = c.E.R2, sImg = c.ImageRadius;
        var fields = (sp with { HalfFov = halfFovDeg }).FieldAngles;
        double start = 1.4 * r1;

        // Stop semi-diameter: where the on-axis marginal ray crosses the stop plane, as OpticStudio sizes it.
        double stop = Math.Abs(Trace(0, sp.H1, r1, r2, n2, n3, sImg, start)?[3].Y ?? sp.H1);

        var rays = new List<RayPath>();
        int failed = 0;
        double shell = 0, ball = 0, image = 0;
        for (int f = 0; f < fields.Length; f++)
        {
            double th = fields[f] * Math.PI / 180;
            for (int k = 0; k < raysPerField; k++)
            {
                double rho = raysPerField == 1 ? 0 : -1 + 2.0 * k / (raysPerField - 1);
                double? h = Aim(th, rho * stop, sp.H1, r1, r2, n2, n3, sImg, start);
                var pts = h is null ? null : Trace(th, h.Value, r1, r2, n2, n3, sImg, start);
                if (pts is null) { failed++; continue; }
                rays.Add(new RayPath(f, pts));
                // pts: 0 start, 1 shell front, 2 ball front, 3 stop, 4 ball back, 5 shell back, 6 image
                shell = Math.Max(shell, Math.Max(FromFront(pts[1]), FromRear(pts[5])));
                ball = Math.Max(ball, Math.Max(FromFront(pts[2]), FromRear(pts[4])));
                image = Math.Max(image, FromRear(pts[6]));
            }
        }
        // A little edge beyond the last ray, as a mounted element would have.
        const double edge = 2.0 * Math.PI / 180;
        return new LensLayout
        {
            R1 = r1, R2 = r2, ImageRadius = sImg, StopSemi = stop,
            ShellAngle = Math.Min(Math.PI / 2, shell + edge),
            BallAngle = Math.Min(Math.PI / 2, ball + edge),
            ImageAngle = image + edge,
            FieldAngles = fields, Wavelength = lam, Rays = rays, Failed = failed,
        };
    }

    private static double FromFront(Pt p) => Math.Atan2(Math.Abs(p.Y), -p.Z);
    private static double FromRear(Pt p) => Math.Atan2(Math.Abs(p.Y), p.Z);

    /// <summary>Offset of a ray from the chief ray (which passes the centre undeviated) that crosses the stop plane
    /// at <paramref name="yStop"/>: bisection, the crossing height rises with the offset.</summary>
    private static double? Aim(double th, double yStop, double h1, double r1, double r2, double n2, double n3, double sImg, double start)
    {
        if (yStop == 0) return 0;
        double lo = -1.5 * h1, hi = 1.5 * h1;
        double? Y(double h) => Trace(th, h, r1, r2, n2, n3, sImg, start)?[3].Y;
        for (int i = 0; i < 60; i++)
        {
            double mid = 0.5 * (lo + hi);
            double? y = Y(mid);
            if (y is null) return null;
            if (y.Value < yStop) lo = mid; else hi = mid;
        }
        return 0.5 * (lo + hi);
    }

    /// <summary>Real meridional trace through the concentric surfaces; null if the ray misses or is totally reflected.</summary>
    private static Pt[]? Trace(double th, double h, double r1, double r2, double n2, double n3, double sImg, double start)
    {
        double uz = Math.Cos(th), uy = Math.Sin(th);
        double pz = -start * uz - h * uy, py = -start * uy + h * uz;
        var pts = new Pt[7];
        pts[0] = new Pt(pz, py);

        bool Sphere(double R, bool leaving, double nIn, double nOut, int slot)
        {
            double b = pz * uz + py * uy, cc = pz * pz + py * py - R * R, disc = b * b - cc;
            if (disc < 0) return false;
            double t = leaving ? -b + Math.Sqrt(disc) : -b - Math.Sqrt(disc);
            if (t <= 1e-12) return false;
            pz += t * uz; py += t * uy;
            pts[slot] = new Pt(pz, py);
            if (nIn == nOut) return true;
            double nz = pz / R, ny = py / R;                  // outward normal
            double cosI = -(nz * uz + ny * uy);
            if (cosI < 0) { nz = -nz; ny = -ny; cosI = -cosI; }
            double mu = nIn / nOut, k = 1 - mu * mu * (1 - cosI * cosI);
            if (k < 0) return false;
            double g = mu * cosI - Math.Sqrt(k);
            uz = mu * uz + g * nz; uy = mu * uy + g * ny;
            double norm = Math.Sqrt(uz * uz + uy * uy);
            uz /= norm; uy /= norm;
            return true;
        }

        if (!Sphere(r1, false, 1.0, n2, 1) || !Sphere(r2, false, n2, n3, 2)) return null;
        if (uz <= 0) return null;
        double ts = -pz / uz;                                  // stop plane z = 0, no refraction
        pts[3] = new Pt(0, py + ts * uy);
        if (!Sphere(r2, true, n3, n2, 4) || !Sphere(r1, true, n2, 1.0, 5) || !Sphere(sImg, true, 1.0, 1.0, 6)) return null;
        return pts;
    }
}
