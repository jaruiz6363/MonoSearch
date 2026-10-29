namespace MonoSearch;

/// <summary>A point of the meridional section, mm: z along the axis (light travels +z), y up, origin at the centre.</summary>
public readonly record struct Pt(double Z, double Y);

/// <summary>One traced ray from in front of the lens to the image, and the field it belongs to (0-based).</summary>
public sealed record RayPath(int Field, Pt[] Points);

/// <summary>Glass between two refracting spheres of the lens (the stop plane inside the core is skipped), by surface
/// index into <see cref="MonoLens.Surfaces"/>.</summary>
public sealed record GlassRegion(int First, int Second, Glass Glass);

/// <summary>
/// Meridional section of a monocentric lens for drawing: real rays traced at the primary wavelength with the stop filled
/// as OpticStudio's real ray aiming fills it, and the angular extent each surface needs to pass them.
/// Angles are half-angles about the surface's own vertex direction (-z in front of the stop, +z behind), radians,
/// capped at pi/2 (a hemisphere).
/// </summary>
public sealed class LensLayout
{
    public required MonoLens Lens { get; init; }
    public required double[] Angle { get; init; }          // per surface of Lens.Surfaces (0 for the stop and the image)
    public required List<GlassRegion> Regions { get; init; }
    public required double StopSemi { get; init; }
    public required double ImageAngle { get; init; }
    /// <summary>The angles the rays reach, before the drawing's edge is added: clear apertures, as OpticStudio's
    /// automatic semi-diameters (per surface, then the image).</summary>
    public required double[] ClearAngle { get; init; }
    public required double ClearImageAngle { get; init; }
    public required double[] FieldAngles { get; init; }
    public required double Wavelength { get; init; }
    public required List<RayPath> Rays { get; init; }
    public int Failed { get; init; }

    public double ImageRadius => Lens.ImageRadius;

    /// <summary>Traces <paramref name="raysPerField"/> rays across the pupil at each field angle (degrees).</summary>
    public static LensLayout Build(MonoLens lens, double halfFovDeg, int raysPerField = 7)
    {
        var sp = lens.Spec;
        double lam = sp.Lambda[sp.Primary];
        var surf = lens.Surfaces;
        int ns = surf.Count;
        var nAfter = surf.Select(s => s.Glass?.Index(lam) ?? 1.0).ToArray();
        var fields = (sp with { HalfFov = halfFovDeg }).FieldAngles;
        double start = 1.4 * surf.Where(s => s.Radius > 0).Select(s => s.Radius).DefaultIfEmpty(sp.F).Max();
        int stopIdx = surf.FindIndex(s => s.Stop);

        // Stop semi-diameter: where the on-axis marginal ray crosses the stop plane, as OpticStudio sizes it.
        double stop = Math.Abs(Trace(surf, nAfter, 0, sp.H1, start)?[stopIdx + 1].Y ?? sp.H1);

        var rays = new List<RayPath>();
        int failed = 0;
        var angle = new double[ns];
        double image = 0;
        for (int f = 0; f < fields.Length; f++)
        {
            double th = fields[f] * Math.PI / 180;
            for (int k = 0; k < raysPerField; k++)
            {
                double rho = raysPerField == 1 ? 0 : -1 + 2.0 * k / (raysPerField - 1);
                double? h = Aim(surf, nAfter, stopIdx, th, rho * stop, sp.H1, start);
                var pts = h is null ? null : Trace(surf, nAfter, th, h.Value, start);
                if (pts is null) { failed++; continue; }
                rays.Add(new RayPath(f, pts));
                // pts[0] is the start, pts[k + 1] the hit on surface k.
                for (int s = 0; s < ns - 1; s++)
                {
                    if (surf[s].Radius > 0) angle[s] = Math.Max(angle[s], FromFront(pts[s + 1]));
                    else if (surf[s].Radius < 0) angle[s] = Math.Max(angle[s], FromRear(pts[s + 1]));
                }
                image = Math.Max(image, FromRear(pts[ns]));
            }
        }
        var clear = (double[])angle.Clone();
        // A little edge beyond the last ray, as a mounted element would have.
        const double edge = 2.0 * Math.PI / 180;
        for (int s = 0; s < ns - 1; s++) if (surf[s].Radius != 0) angle[s] = Math.Min(Math.PI / 2, angle[s] + edge);

        var regions = new List<GlassRegion>();
        var refracting = Enumerable.Range(0, ns - 1).Where(s => surf[s].Radius != 0).ToList();
        for (int k = 0; k + 1 < refracting.Count; k++)
            if (surf[refracting[k]].Glass is { } g) regions.Add(new GlassRegion(refracting[k], refracting[k + 1], g));

        return new LensLayout
        {
            Lens = lens, Angle = angle, Regions = regions, StopSemi = stop, ImageAngle = image + edge, ClearAngle = clear, ClearImageAngle = image,
            FieldAngles = fields, Wavelength = lam, Rays = rays, Failed = failed,
        };
    }

    private static double FromFront(Pt p) => Math.Atan2(Math.Abs(p.Y), -p.Z);
    private static double FromRear(Pt p) => Math.Atan2(Math.Abs(p.Y), p.Z);

    /// <summary>Offset of a ray from the chief ray (which passes the centre undeviated) that crosses the stop plane
    /// at <paramref name="yStop"/>: bisection, the crossing height rises with the offset.</summary>
    private static double? Aim(List<LensSurface> surf, double[] nAfter, int stopIdx, double th, double yStop, double h1, double start)
    {
        if (yStop == 0) return 0;
        double lo = -1.5 * h1, hi = 1.5 * h1;
        for (int i = 0; i < 60; i++)
        {
            double mid = 0.5 * (lo + hi);
            var pts = Trace(surf, nAfter, th, mid, start);
            if (pts is null) return null;
            if (pts[stopIdx + 1].Y < yStop) lo = mid; else hi = mid;
        }
        return 0.5 * (lo + hi);
    }

    /// <summary>Real meridional trace through the concentric surfaces (the last is the image, not refracting);
    /// null if the ray misses a surface or is totally reflected.</summary>
    private static Pt[]? Trace(List<LensSurface> surf, double[] nAfter, double th, double h, double start)
    {
        double uz = Math.Cos(th), uy = Math.Sin(th);
        double pz = -start * uz - h * uy, py = -start * uy + h * uz;
        var pts = new Pt[surf.Count + 1];
        pts[0] = new Pt(pz, py);
        double n = 1.0;
        for (int k = 0; k < surf.Count; k++)
        {
            double R = surf[k].Radius;
            if (R == 0)
            {
                // Plane (the stop) at the centre: no refraction.
                if (uz <= 0) return null;
                double t0 = -pz / uz;
                pz += t0 * uz; py += t0 * uy;
                pts[k + 1] = new Pt(pz, py);
                continue;
            }
            double rr = Math.Abs(R);
            bool leaving = R < 0;   // behind the stop the ray meets each sphere on its way out
            double b = pz * uz + py * uy, cc = pz * pz + py * py - rr * rr, disc = b * b - cc;
            if (disc < 0) return null;
            double t = leaving ? -b + Math.Sqrt(disc) : -b - Math.Sqrt(disc);
            if (t <= 1e-12) return null;
            pz += t * uz; py += t * uy;
            pts[k + 1] = new Pt(pz, py);
            if (k == surf.Count - 1) break;                     // the image
            double n2 = nAfter[k];
            if (n2 == n) continue;
            double nz = pz / rr, ny = py / rr;                   // outward normal
            double cosI = -(nz * uz + ny * uy);
            if (cosI < 0) { nz = -nz; ny = -ny; cosI = -cosI; }
            double mu = n / n2, kk = 1 - mu * mu * (1 - cosI * cosI);
            if (kk < 0) return null;
            double g = mu * cosI - Math.Sqrt(kk);
            uz = mu * uz + g * nz; uy = mu * uy + g * ny;
            double norm = Math.Sqrt(uz * uz + uy * uy);
            uz /= norm; uy /= norm;
            n = n2;
        }
        return pts;
    }
}
