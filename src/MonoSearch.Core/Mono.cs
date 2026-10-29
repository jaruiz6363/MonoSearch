namespace MonoSearch;

/// <summary>Design specification. Wavelengths in micrometres, any number from 1 to 24, each with a weight;
/// Primary (0-based) is the reference wavelength, the one Eq. 36 refocuses at.
/// DesignLambda sets the focal length (Eq. 9) and Q (Eq. 31); the paper uses the d line there.</summary>
public sealed record Spec(double F, double FNumber, double[] Lambda, double[] Weight, int Primary, double DesignLambda,
    bool PolyFocus = false, double MinBfl = 0.0, double HalfFov = 60.0)
{
    public double NA => 1.0 / (2.0 * FNumber);      // back NA, "A" in the paper
    public double H1 => F * NA;                     // marginal input height

    /// <summary>Largest outer radius searched. The image sits about F from the centre, so the back focus
    /// (rear shell vertex to image) is about F - r1; capping r1 at F - MinBfl searches only designs that can meet it.</summary>
    public double R1Max => Math.Min(0.995 * F, F - MinBfl);
    public int NLambda => Lambda.Length;

    /// <summary>Field angles of the exported files, degrees: on axis, half and full half-field. Every field sees the
    /// same lens about its own chief ray (the flat stop only foreshortens the pupil), so the field is not in the ranking.</summary>
    public double[] FieldAngles => HalfFov > 0 ? new[] { 0.0, HalfFov / 2, HalfFov } : new[] { 0.0 };
}

/// <summary>Indices of one glass pair (outer n2, inner n3) at each wavelength, and at the design wavelength.</summary>
public readonly record struct PairIndex(double[] N2, double[] N3, double N2d, double N3d);

/// <summary>Result for one r1 of one glass pair.</summary>
public readonly record struct Eval(double R1, double R2, double Q, double DPhi2, double DS, double C20, double C40, double C60, double C80);

/// <summary>
/// Two-glass symmetric monocentric lens, Stamenov, Agurok and Ford, Appl. Opt. 51, 7648 (2012).
/// Outer shell glass n2 (radius r1), inner ball glass n3 (radius r2), stop at the common centre.
/// </summary>
public static class Mono
{
    // Reduced heights and least-squares fit of Eq. (35).
    public static readonly double[] Rho = { 1, 0.95, 0.9, 0.85, 0.8, 0.75, 0.7, 0.6, 0.5 };
    private static readonly double[,] Pinv = BuildPseudoInverse();

    /// <summary>Eq. (9): inner radius that gives focal length f at the centre wavelength. NaN if none.</summary>
    public static double R2FromR1(double r1, double f, double n2, double n3)
    {
        double denom = 1.0 / f - 2.0 * (1.0 - 1.0 / n2) / r1;
        if (denom == 0) return double.NaN;
        double r2 = 2.0 * (1.0 / n2 - 1.0 / n3) / denom;
        return r2 > 0 && r2 < r1 ? r2 : double.NaN;
    }

    /// <summary>Eq. (29): exact distance from the centre to the axis crossing of a ray entering parallel at height h.
    /// NaN if the ray misses the ball, is totally reflected, or does not cross the axis outside the lens.</summary>
    public static double S(double h, double r1, double r2, double n2, double n3)
    {
        double a = h / r1, b = h / (r1 * n2), c = h / (r2 * n2), d = h / (r2 * n3);
        if (a >= 1 || c >= 1 || d >= 1) return double.NaN;
        double ang = 2.0 * (Math.Asin(a) - Math.Asin(b) + Math.Asin(c) - Math.Asin(d));
        double s = Math.Sin(ang);
        if (!(s > 0)) return double.NaN;
        double S = h / s;
        return S > r1 ? S : double.NaN;
    }

    /// <summary>Eq. (31): monochromatic criterion at the centre wavelength, heights h1, 0.7 h1, 0.4 h1.</summary>
    public static double Q(Spec sp, double r1, double r2, double n2, double n3)
    {
        Span<double> ds = stackalloc double[3];
        ReadOnlySpan<double> k = [1.0, 0.7, 0.4];
        double q = 0;
        for (int i = 0; i < 3; i++)
        {
            double s = S(k[i] * sp.H1, r1, r2, n2, n3);
            if (double.IsNaN(s)) return double.NaN;
            ds[i] = s - sp.F;
            q += Math.Abs(ds[i]);
        }
        for (int j = 0; j < 3; j++)
            for (int m = 0; m < 3; m++)
                if (m != j) q += Math.Abs(ds[j] - ds[m]);
        return q;
    }

    /// <summary>Full evaluation at one r1: Q, Zernike fit (Eq. 35), refocus (Eqs. 36-37) and polychromatic
    /// mean-square wavefront deformation (Eq. 38, in waves^2).
    /// <para>With weights, Eq. 38's (1/2) sum over three wavelengths becomes 1.5 x the weighted mean of the
    /// per-wavelength mean squares: the same number for three equal weights, and comparable for any count.</para></summary>
    public static Eval Evaluate(Spec sp, in PairIndex px, double r1)
    {
        var nan = new Eval(r1, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        int c = sp.Primary, nl = sp.NLambda;
        double r2 = R2FromR1(r1, sp.F, px.N2d, px.N3d);
        if (double.IsNaN(r2)) return nan;
        double q = Q(sp, r1, r2, px.N2d, px.N3d);
        if (double.IsNaN(q)) return nan;

        double A2 = sp.NA * sp.NA;
        Span<double> coef = stackalloc double[4 * nl];   // [lambda][C20, C40, C60, C80]
        Span<double> y = stackalloc double[9];
        for (int l = 0; l < nl; l++)
        {
            double lamMm = sp.Lambda[l] * 1e-3;
            for (int j = 0; j < 9; j++)
            {
                double s = S(Rho[j] * sp.H1, r1, r2, px.N2[l], px.N3[l]);
                if (double.IsNaN(s)) return nan;
                y[j] = -(s - sp.F) * A2 / lamMm;
            }
            for (int t = 0; t < 4; t++)
            {
                double acc = 0;
                for (int j = 0; j < 9; j++) acc += Pinv[t, j] * y[j];
                coef[l * 4 + t] = acc;
            }
        }

        double lamC = sp.Lambda[c] * 1e-3;
        double dS = 4.0 * coef[c * 4] * lamC / A2;                       // Eq. (36)
        if (sp.PolyFocus)
        {
            // Weighted least-squares focus: minimise sum_l w_l (C20_l - dS * k_l)^2, k_l = A^2 / (4 lambda_l).
            double num = 0, den = 0;
            for (int l = 0; l < nl; l++)
            {
                double k = A2 / (4.0 * sp.Lambda[l] * 1e-3);
                num += sp.Weight[l] * coef[l * 4] * k; den += sp.Weight[l] * k * k;
            }
            dS = num / den;
        }
        double sum = 0, wsum = 0;
        for (int l = 0; l < nl; l++)
        {
            double c20 = coef[l * 4] - dS * A2 / (4.0 * sp.Lambda[l] * 1e-3);  // Eq. (37)
            double c40 = coef[l * 4 + 1], c60 = coef[l * 4 + 2], c80 = coef[l * 4 + 3];
            sum += sp.Weight[l] * (c20 * c20 / 3.0 + c40 * c40 / 5.0 + c60 * c60 / 7.0 + c80 * c80 / 9.0);
            wsum += sp.Weight[l];
        }
        // Back focus after refocusing: image at F - dS from the centre, rear vertex at r1.
        if (sp.F - dS - r1 < sp.MinBfl) return nan;
        return new Eval(r1, r2, q, 1.5 * sum / wsum, dS,
            coef[c * 4] - dS * A2 / (4.0 * lamC), coef[c * 4 + 1], coef[c * 4 + 2], coef[c * 4 + 3]);
    }

    /// <summary>Global minimum over r1 of either Q (paper mode) or DPhi2 (direct mode): coarse scan, then golden section.</summary>
    public static Eval? Minimise(Spec sp, in PairIndex px, bool byQ, int scan, double r1Lo, double r1Hi)
    {
        double step = (r1Hi - r1Lo) / (scan - 1);
        double best = double.PositiveInfinity;
        int bestK = -1;
        for (int k = 0; k < scan; k++)
        {
            double r1 = r1Lo + k * step;
            double v = byQ ? QOnly(sp, px, r1) : Evaluate(sp, px, r1).DPhi2;
            if (v < best) { best = v; bestK = k; }
        }
        if (bestK < 0) return null;

        // Golden-section refinement within one step either side.
        var pxc = px;
        double Fn(double r1)
        {
            double v = byQ ? QOnly(sp, pxc, r1) : Evaluate(sp, pxc, r1).DPhi2;
            return double.IsNaN(v) ? double.PositiveInfinity : v;
        }
        double a = Math.Max(r1Lo, r1Lo + (bestK - 1) * step), b = Math.Min(r1Hi, r1Lo + (bestK + 1) * step);
        const double g = 0.6180339887498949;
        double x1 = b - g * (b - a), x2 = a + g * (b - a), f1 = Fn(x1), f2 = Fn(x2);
        for (int it = 0; it < 40; it++)
        {
            if (f1 < f2) { b = x2; x2 = x1; f2 = f1; x1 = b - g * (b - a); f1 = Fn(x1); }
            else { a = x1; x1 = x2; f1 = f2; x2 = a + g * (b - a); f2 = Fn(x2); }
        }
        double rBest = f1 < f2 ? x1 : x2;
        if (Fn(rBest) > best) rBest = r1Lo + bestK * step;
        var e = Evaluate(sp, px, rBest);
        return double.IsNaN(e.DPhi2) ? null : e;
    }

    /// <summary>RMS wavefront (waves) per wavelength for a given prescription and image distance from the centre,
    /// with no refocus: the Zernike fit of Eq. (35) with dS measured from sImage. Used to cross-check against OpticStudio.</summary>
    public static double[] RmsFixed(Spec sp, in PairIndex px, double r1, double r2, double sImage)
    {
        double A2 = sp.NA * sp.NA;
        var rms = new double[sp.NLambda];
        Span<double> y = stackalloc double[9];
        for (int l = 0; l < sp.NLambda; l++)
        {
            double lamMm = sp.Lambda[l] * 1e-3;
            for (int j = 0; j < 9; j++) y[j] = -(S(Rho[j] * sp.H1, r1, r2, px.N2[l], px.N3[l]) - sImage) * A2 / lamMm;
            double ms = 0;
            ReadOnlySpan<double> div = [3.0, 5.0, 7.0, 9.0];
            for (int t = 0; t < 4; t++)
            {
                double ct = 0;
                for (int j = 0; j < 9; j++) ct += Pinv[t, j] * y[j];
                ms += ct * ct / div[t];
            }
            rms[l] = Math.Sqrt(ms);
        }
        return rms;
    }

    private static double QOnly(Spec sp, in PairIndex px, double r1)
    {
        double r2 = R2FromR1(r1, sp.F, px.N2d, px.N3d);
        return double.IsNaN(r2) ? double.NaN : Q(sp, r1, r2, px.N2d, px.N3d);
    }

    private static double[,] BuildPseudoInverse()
    {
        // Rows: 4 C20 + C40 (24p^2-12) + C60 (120p^4-120p^2+24) + C80 (560p^6-840p^4+360p^2-40)
        var M = new double[9, 4];
        for (int j = 0; j < 9; j++)
        {
            double p2 = Rho[j] * Rho[j], p4 = p2 * p2, p6 = p4 * p2;
            M[j, 0] = 4;
            M[j, 1] = 24 * p2 - 12;
            M[j, 2] = 120 * p4 - 120 * p2 + 24;
            M[j, 3] = 560 * p6 - 840 * p4 + 360 * p2 - 40;
        }
        var N = new double[4, 4];
        for (int a = 0; a < 4; a++)
            for (int b = 0; b < 4; b++)
                for (int j = 0; j < 9; j++) N[a, b] += M[j, a] * M[j, b];
        var Ni = Invert4(N);
        var P = new double[4, 9];
        for (int a = 0; a < 4; a++)
            for (int j = 0; j < 9; j++)
                for (int b = 0; b < 4; b++) P[a, j] += Ni[a, b] * M[j, b];
        return P;
    }

    private static double[,] Invert4(double[,] m)
    {
        int n = 4;
        var a = new double[n, 2 * n];
        for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) a[i, j] = m[i, j]; a[i, n + i] = 1; }
        for (int col = 0; col < n; col++)
        {
            int piv = col;
            for (int r = col + 1; r < n; r++) if (Math.Abs(a[r, col]) > Math.Abs(a[piv, col])) piv = r;
            for (int j = 0; j < 2 * n; j++) (a[col, j], a[piv, j]) = (a[piv, j], a[col, j]);
            double d = a[col, col];
            for (int j = 0; j < 2 * n; j++) a[col, j] /= d;
            for (int r = 0; r < n; r++)
            {
                if (r == col) continue;
                double fct = a[r, col];
                for (int j = 0; j < 2 * n; j++) a[r, j] -= fct * a[col, j];
            }
        }
        var inv = new double[n, n];
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) inv[i, j] = a[i, n + j];
        return inv;
    }
}
