namespace MonoSearch;

/// <summary>Radii of a 4GA-8 lens, mm, all positive: front shell R1, core R2, rear shell R4, meniscus R5 (front) and R6 (back).</summary>
public readonly record struct Ga8Radii(double R1, double R2, double R4, double R5, double R6)
{
    public double this[int i] => i switch { 0 => R1, 1 => R2, 2 => R4, 3 => R5, _ => R6 };
}

/// <summary>
/// Four-glass asymmetric monocentric lens with an air gap (4GA-8), Stamenov, Agurok and Ford, Appl. Opt. 52, 8287 (2013),
/// Fig. 9: a front shell (glass 1, R1 to R2) cemented to the core ball (glass 2, radius R2, stop at the centre), a rear
/// shell (glass 3, R2 to R4) cemented to the back of the core, an air gap (R4 to R5) and a rear meniscus (glass 4, R5 to R6).
/// <para>The radii minimise the paper's criterion C (Eq. 11): lateral-aberration-like terms p_i dS(h_i, lambda_j)/lambda_j at
/// nine pupil zones and every wavelength, plus the longitudinal colour between the band's ends at zones 1, 0.88 and 0.05.
/// dS is measured from f (the image radius is the focal length), so C also holds the focal length. Wavelength weights
/// multiply the first sum; with equal weights C is the paper's.</para>
/// </summary>
public sealed class Ga8Problem
{
    /// <summary>Reduced pupil heights of Eq. (10).</summary>
    public static readonly double[] P = { 1, 0.97, 0.88, 0.8, 0.7, 0.6, 0.5, 0.4, 0.05 };
    private static readonly int[] ChromaticZones = { 8, 2, 0 };   // p = 0.05, 0.88, 1 (Eq. 11)

    // Surfaces in ray order -> radius index (R1, R2, R4, R5, R6) and direction (+1 toward the centre).
    private static readonly int[] RadiusOf = { 0, 1, 1, 2, 3, 4 };
    private static readonly int[] Sign = { 1, 1, -1, -1, -1, -1 };

    private readonly Spec _sp;
    private readonly double[][] _nb, _na;   // [wavelength][surface] index before / after
    private readonly double[] _h, _sqw, _lamUm;
    private readonly int _lo, _hi;           // shortest and longest wavelength
    private readonly double _minGlass, _minGap, _r6Max;

    public int Residuals => P.Length * _sp.NLambda + ChromaticZones.Length + 1;

    /// <param name="n">Index of glass 1..4 at each wavelength: n[g][l].</param>
    /// <param name="minGlass">Smallest glass thickness (front shell, rear shell, meniscus), mm.</param>
    /// <param name="minGap">Smallest air gap, mm.</param>
    public Ga8Problem(Spec sp, double[][] n, double minGlass, double minGap)
    {
        _sp = sp;
        int nl = sp.NLambda;
        _nb = new double[nl][]; _na = new double[nl][];
        for (int l = 0; l < nl; l++)
        {
            double n1 = n[0][l], n2 = n[1][l], n3 = n[2][l], n4 = n[3][l];
            _nb[l] = new[] { 1.0, n1, n2, n3, 1.0, n4 };
            _na[l] = new[] { n1, n2, n3, 1.0, n4, 1.0 };
        }
        _h = P.Select(p => p * sp.H1).ToArray();
        _sqw = sp.Weight.Select(Math.Sqrt).ToArray();
        _lamUm = sp.Lambda;
        _lo = Array.IndexOf(sp.Lambda, sp.Lambda.Min());
        _hi = Array.IndexOf(sp.Lambda, sp.Lambda.Max());
        _minGlass = minGlass; _minGap = minGap;
        _r6Max = sp.F - sp.MinBfl;
    }

    // ── Parameters: x0 = R2, then squared excesses over the minimum thicknesses, so the order R2 < R4 < R5 < R6
    //    and R2 < R1 holds for any x and the solver needs no bounds.
    public Ga8Radii ToRadii(ReadOnlySpan<double> x)
    {
        double r2 = x[0], r1 = r2 + _minGlass + x[1] * x[1], r4 = r2 + _minGlass + x[2] * x[2];
        double r5 = r4 + _minGap + x[3] * x[3], r6 = r5 + _minGlass + x[4] * x[4];
        return new Ga8Radii(r1, r2, r4, r5, r6);
    }

    public double[] FromRadii(Ga8Radii r) => new[]
    {
        r.R2,
        Math.Sqrt(Math.Max(1e-6, r.R1 - r.R2 - _minGlass)),
        Math.Sqrt(Math.Max(1e-6, r.R4 - r.R2 - _minGlass)),
        Math.Sqrt(Math.Max(1e-6, r.R5 - r.R4 - _minGap)),
        Math.Sqrt(Math.Max(1e-6, r.R6 - r.R5 - _minGlass)),
    };

    /// <summary>Axis crossing distance of the ray at height h, wavelength l, and its derivatives with respect to the radii.</summary>
    private double Trace(Ga8Radii r, double h, int l, Span<double> dS)
    {
        double phi = 0;
        Span<double> dphi = stackalloc double[5];
        dphi.Clear();
        var nb = _nb[l]; var na = _na[l];
        for (int k = 0; k < 6; k++)
        {
            int ri = RadiusOf[k];
            double R = r[ri], ab = h / (nb[k] * R), aa = h / (na[k] * R);
            if (!(ab < 1 && aa < 1)) return double.NaN;
            double sb = Math.Sqrt(1 - ab * ab), sa = Math.Sqrt(1 - aa * aa);
            phi += Sign[k] * (Math.Asin(ab) - Math.Asin(aa));
            dphi[ri] += Sign[k] * (-ab / (R * sb) + aa / (R * sa));
        }
        double s = Math.Sin(phi);
        if (!(s > 0)) return double.NaN;
        double og = h / s;
        if (!(og > r.R6)) return double.NaN;
        double g = -og * Math.Cos(phi) / s;     // d(og)/d(phi)
        for (int i = 0; i < 5; i++) dS[i] = g * dphi[i];
        return og;
    }

    /// <summary>Residual vector (C = sum of squares) and its Jacobian with respect to x; false if a ray fails.</summary>
    public bool Evaluate(ReadOnlySpan<double> x, Span<double> res, double[,]? jac)
    {
        var r = ToRadii(x);
        if (!(r.R2 > 0.02 * _sp.F)) return false;
        // dR/dx (rows R1, R2, R4, R5, R6).
        Span<double> dR = stackalloc double[25];
        dR.Clear();
        dR[0] = 1; dR[1] = 2 * x[1];
        dR[5] = 1;
        dR[10] = 1; dR[12] = 2 * x[2];
        dR[15] = 1; dR[17] = 2 * x[2]; dR[18] = 2 * x[3];
        dR[20] = 1; dR[22] = 2 * x[2]; dR[23] = 2 * x[3]; dR[24] = 2 * x[4];

        int nl = _sp.NLambda, m = 0;
        Span<double> dS = stackalloc double[5];
        Span<double> sLo = stackalloc double[P.Length], sHi = stackalloc double[P.Length];
        Span<double> dLo = stackalloc double[5 * P.Length], dHi = stackalloc double[5 * P.Length];
        for (int i = 0; i < P.Length; i++)
        {
            for (int l = 0; l < nl; l++)
            {
                double og = Trace(r, _h[i], l, dS);
                if (double.IsNaN(og)) return false;
                double scale = _sqw[l] * P[i] / _lamUm[l];
                res[m] = scale * (og - _sp.F);
                if (jac != null) for (int v = 0; v < 5; v++) jac[m, v] = scale * Chain(dS, dR, v);
                m++;
                if (l == _lo) { sLo[i] = og; for (int v = 0; v < 5; v++) dLo[i * 5 + v] = Chain(dS, dR, v); }
                if (l == _hi) { sHi[i] = og; for (int v = 0; v < 5; v++) dHi[i * 5 + v] = Chain(dS, dR, v); }
            }
        }
        foreach (int i in ChromaticZones)
        {
            res[m] = sLo[i] - sHi[i];
            if (jac != null) for (int v = 0; v < 5; v++) jac[m, v] = dLo[i * 5 + v] - dHi[i * 5 + v];
            m++;
        }
        // Back focus: the image (at about f) must clear the meniscus by MinBfl.
        double over = r.R6 - _r6Max;
        res[m] = over > 0 ? 10 * over : 0;
        if (jac != null) for (int v = 0; v < 5; v++) jac[m, v] = over > 0 ? 10 * dR[20 + v] : 0;
        return true;
    }

    private static double Chain(ReadOnlySpan<double> dS, ReadOnlySpan<double> dR, int v)
    {
        double acc = 0;
        for (int k = 0; k < 5; k++) acc += dS[k] * dR[k * 5 + v];
        return acc;
    }

    /// <summary>Criterion C at the given radii; +infinity if a ray fails.</summary>
    public double Criterion(Ga8Radii radii)
    {
        Span<double> res = stackalloc double[Residuals];
        if (!Evaluate(FromRadii(radii), res, null)) return double.PositiveInfinity;
        double c = 0;
        foreach (var v in res) c += v * v;
        return c;
    }

    /// <summary>Levenberg-Marquardt from <paramref name="seed"/>. Returns the radii and C, or C = +infinity if the seed fails.
    /// With <paramref name="abandonAbove"/>, gives up (returning the C reached) when C is still above it after
    /// <paramref name="abandonAfter"/> iterations: a screening run need not converge a combination that cannot make the list.</summary>
    public (Ga8Radii Radii, double C) Optimise(Ga8Radii seed, int maxIterations, double abandonAbove = double.PositiveInfinity,
        int abandonAfter = 5)
    {
        int m = Residuals;
        var x = FromRadii(seed);
        var res = new double[m];
        var jac = new double[m, 5];
        if (!Evaluate(x, res, jac)) return (seed, double.PositiveInfinity);
        double c = SumSq(res), mu = 1e-3;
        var trial = new double[5];
        var tres = new double[m];
        var A = new double[5, 5];
        var g = new double[5];
        int stalls = 0;
        for (int it = 0; it < maxIterations; it++)
        {
            for (int a = 0; a < 5; a++)
            {
                double ga = 0;
                for (int k = 0; k < m; k++) ga += jac[k, a] * res[k];
                g[a] = ga;
            }
            bool accepted = false;
            for (int tries = 0; tries < 12 && !accepted; tries++)
            {
                for (int a = 0; a < 5; a++)
                    for (int b = 0; b <= a; b++)
                    {
                        double s = 0;
                        for (int k = 0; k < m; k++) s += jac[k, a] * jac[k, b];
                        A[a, b] = A[b, a] = s;
                    }
                var step = new double[5];
                for (int a = 0; a < 5; a++) { A[a, a] *= 1 + mu; A[a, a] += 1e-15; step[a] = -g[a]; }
                if (!Linear.SolveSpd(A, step, 5)) { mu *= 10; continue; }
                for (int a = 0; a < 5; a++) trial[a] = x[a] + step[a];
                if (Evaluate(trial, tres, null))
                {
                    double ct = SumSq(tres);
                    if (ct < c)
                    {
                        stalls = ct > c * (1 - 1e-9) ? stalls + 1 : 0;
                        Array.Copy(trial, x, 5);
                        c = ct;
                        Evaluate(x, res, jac);
                        mu = Math.Max(mu / 3, 1e-12);
                        accepted = true;
                        break;
                    }
                }
                mu *= 10;
            }
            if (!accepted || stalls >= 3) break;
            if (it + 1 == abandonAfter && c > abandonAbove) break;
        }
        return (ToRadii(x), c);
    }

    private static double SumSq(double[] v) { double s = 0; foreach (var e in v) s += e * e; return s; }

    /// <summary>Paraxial focal length at the primary wavelength: a ray at small height h turns by
    /// h sum sign (1/nb - 1/na)/R over the surfaces, and crosses the axis at h over that.</summary>
    public double ParaxialFocal(Ga8Radii r)
    {
        int l = _sp.Primary;
        double p = 0;
        for (int k = 0; k < 6; k++) p += Sign[k] * (1 / _nb[l][k] - 1 / _na[l][k]) / r[RadiusOf[k]];
        return 1 / p;
    }

    /// <summary>A starting point fitted to these glasses: the element thicknesses of <paramref name="seed"/> kept, and the
    /// core radius solved (bisection) so the paraxial focal length is f. The paper's M0 suits its K-GFK68 core (n 1.59);
    /// with a denser core it focuses inside the lens and no ray is valid, and scaling every radius instead pushes the
    /// meniscus out past the image. Null if no core radius gives f with the image outside the lens.</summary>
    public Ga8Radii? Fitted(Ga8Radii seed)
    {
        double tF = seed.R1 - seed.R2, tR = seed.R4 - seed.R2, gap = seed.R5 - seed.R4, tM = seed.R6 - seed.R5;
        Ga8Radii At(double r2) => new(r2 + tF, r2, r2 + tR, r2 + tR + gap, r2 + tR + gap + tM);
        double lo = 0.05 * _sp.F, hi = _sp.F;
        double fLo = ParaxialFocal(At(lo)), fHi = ParaxialFocal(At(hi));
        if (!(fLo > 0 && fLo < _sp.F && fHi > _sp.F)) return null;     // the focal length grows with the core radius
        for (int i = 0; i < 50; i++)
        {
            double mid = 0.5 * (lo + hi);
            if (ParaxialFocal(At(mid)) < _sp.F) lo = mid; else hi = mid;
        }
        var r = At(0.5 * (lo + hi));
        return r.R6 < _sp.F - _sp.MinBfl ? r : null;
    }

    /// <summary>The first of the seeds, as given or fitted to these glasses, from which every ray traces;
    /// null if none does (the combination cannot be started).</summary>
    public Ga8Radii? Start(IEnumerable<Ga8Radii> seeds)
    {
        var list = seeds as IList<Ga8Radii> ?? seeds.ToList();
        foreach (var s in list)
            if (double.IsFinite(Criterion(s))) return s;
        foreach (var s in list.Concat(Profiles(_sp.F)))
            if (Fitted(s) is { } t && double.IsFinite(Criterion(t))) return t;
        return null;
    }

    /// <summary>Element thicknesses to fit when the paper's do not: thinner rear groups for a dense core (which needs a
    /// core radius near a ball lens's), as fractions of f: front shell, rear shell, air gap, meniscus. Only the
    /// thicknesses matter; <see cref="Fitted"/> solves the core radius.</summary>
    private static IEnumerable<Ga8Radii> Profiles(double f)
    {
        foreach (var (tF, tR, gap, tM) in new[] { (0.45, 0.06, 0.02, 0.10), (0.30, 0.05, 0.015, 0.06), (0.15, 0.04, 0.012, 0.04) })
            yield return new Ga8Radii((1 + tF) * f, f, (1 + tR) * f, (1 + tR + gap) * f, (1 + tR + gap + tM) * f);
    }

    /// <summary>Starting radii: the paper's starting point M0 (7.0, 2.9, 4.2, 4.5, 7.8 mm for f = 12 mm) scaled to f,
    /// with the front radius moved as in its search (6.5 and 7.5 mm also gave solutions).</summary>
    public static IEnumerable<Ga8Radii> Seeds(double f)
    {
        double k = f / 12.0;
        foreach (double r1 in new[] { 7.0, 6.5, 7.5 })
            yield return new Ga8Radii(r1 * k, 2.9 * k, 4.2 * k, 4.5 * k, 7.8 * k);
    }

    /// <summary>Concentric surfaces of a 4GA-8 lens for the generic trace, at wavelength l.</summary>
    public void Surfaces(Ga8Radii r, int l, Span<double> radii, Span<double> nb, Span<double> na, Span<int> sign)
    {
        for (int k = 0; k < 6; k++) { radii[k] = r[RadiusOf[k]]; nb[k] = _nb[l][k]; na[k] = _na[l][k]; sign[k] = Sign[k]; }
    }

    /// <summary>Wavefront deformation of a 4GA-8 lens with the same normalisation as the 2GS search (refocused).</summary>
    public double DPhi2(Ga8Radii r, Concentric.ZernikeFit fit, out double dS)
    {
        var self = this;
        return Concentric.DPhi2(_sp, fit, (j, l) =>
        {
            Span<double> rr = stackalloc double[6], nb = stackalloc double[6], na = stackalloc double[6];
            Span<int> sg = stackalloc int[6];
            self.Surfaces(r, l, rr, nb, na, sg);
            return Concentric.S(fit.Rho[j] * self._sp.H1, rr, nb, na, sg);
        }, out dS);
    }
}
