namespace MonoSearch;

/// <summary>
/// Exact trace of a ray entering parallel to the axis through concentric spheres, and the polychromatic wavefront
/// deformation from it, for any number of shells (Stamenov, Agurok and Ford 2012 Eq. 29 and 2013 Eq. 7 are cases of it).
/// <para>A ray at height h keeps n·d = h, d its distance from the centre, so at a sphere of radius R between indices
/// nb and na the angles are asin(h/(nb R)) and asin(h/(na R)). Toward the centre the ray turns by their difference,
/// away from it by the opposite difference, and it crosses the axis at OG = h / sin(total turn).</para>
/// </summary>
public static class Concentric
{
    /// <summary>Axis crossing distance from the centre. Surfaces in ray order: radius, index before and after, and
    /// +1 for a surface met toward the centre (front half), -1 away from it. NaN if the ray misses a sphere, is
    /// totally reflected, or does not cross the axis beyond the last surface.</summary>
    public static double S(double h, ReadOnlySpan<double> r, ReadOnlySpan<double> nBefore, ReadOnlySpan<double> nAfter,
        ReadOnlySpan<int> sign)
    {
        double phi = 0;
        for (int k = 0; k < r.Length; k++)
        {
            double ab = h / (nBefore[k] * r[k]), aa = h / (nAfter[k] * r[k]);
            if (!(ab < 1 && aa < 1)) return double.NaN;
            phi += sign[k] * (Math.Asin(ab) - Math.Asin(aa));
        }
        double s = Math.Sin(phi);
        if (!(s > 0)) return double.NaN;
        double og = h / s;
        return og > r[^1] ? og : double.NaN;
    }

    /// <summary>Least-squares fit of the longitudinal aberration to Zernike C20..C(2K) (2012 Eq. 35, extended):
    /// rows are (1/rho) dZ/drho of the radial polynomials, so y = -(S - F) A^2 / lambda fits sum_k C_k row_k.</summary>
    public sealed class ZernikeFit
    {
        public double[] Rho { get; }
        public int Terms { get; }
        private readonly double[,] _pinv;

        public ZernikeFit(double[] rho, int terms)
        {
            Rho = rho; Terms = terms;
            var M = new double[rho.Length, terms];
            for (int j = 0; j < rho.Length; j++)
                for (int t = 0; t < terms; t++) M[j, t] = RadialDerivOverRho(2 * (t + 1), rho[j]);
            _pinv = PseudoInverse(M);
        }

        public double Coef(int t, ReadOnlySpan<double> y)
        {
            double acc = 0;
            for (int j = 0; j < y.Length; j++) acc += _pinv[t, j] * y[j];
            return acc;
        }

        /// <summary>(1/rho) d/drho of the Zernike radial polynomial R_n^0, n even.</summary>
        private static double RadialDerivOverRho(int n, double rho)
        {
            double acc = 0;
            for (int s = 0; s <= n / 2; s++)
            {
                int p = n - 2 * s;                     // power of rho
                if (p == 0) continue;
                double c = (s % 2 == 0 ? 1 : -1) * Fact(n - s) / (Fact(s) * Fact(n / 2 - s) * Fact(n / 2 - s));
                acc += c * p * Math.Pow(rho, p - 2);
            }
            return acc;
        }

        private static double Fact(int k) { double f = 1; for (int i = 2; i <= k; i++) f *= i; return f; }

        private static double[,] PseudoInverse(double[,] M)
        {
            int m = M.GetLength(0), n = M.GetLength(1);
            var N = new double[n, n];
            for (int a = 0; a < n; a++)
                for (int b = 0; b < n; b++)
                    for (int j = 0; j < m; j++) N[a, b] += M[j, a] * M[j, b];
            var Ni = Linear.Invert(N);
            var P = new double[n, m];
            for (int a = 0; a < n; a++)
                for (int j = 0; j < m; j++)
                    for (int b = 0; b < n; b++) P[a, j] += Ni[a, b] * M[j, b];
            return P;
        }
    }

    /// <summary>Polychromatic mean-square wavefront deformation (waves^2), as Mono.Evaluate but for any lens:
    /// <paramref name="s"/>(rho index, wavelength index) is the axis crossing of the ray at height rho*H1, NaN if it fails.
    /// Refocuses by weighted least squares (or at the primary, Eq. 36) and returns 1.5 x the weighted mean of
    /// sum C_k^2/(2k+1), the paper's normalisation; <paramref name="dS"/> is the refocus from F.</summary>
    public static double DPhi2(Spec sp, ZernikeFit fit, Func<int, int, double> s, out double dS)
    {
        dS = double.NaN;
        int nl = sp.NLambda, nt = fit.Terms, nr = fit.Rho.Length;
        double A2 = sp.NA * sp.NA;
        var coef = new double[nl, nt];
        Span<double> y = stackalloc double[nr];
        for (int l = 0; l < nl; l++)
        {
            double lamMm = sp.Lambda[l] * 1e-3;
            for (int j = 0; j < nr; j++)
            {
                double v = s(j, l);
                if (double.IsNaN(v)) return double.NaN;
                y[j] = -(v - sp.F) * A2 / lamMm;
            }
            for (int t = 0; t < nt; t++) coef[l, t] = fit.Coef(t, y);
        }
        int c = sp.Primary;
        double focus = 4.0 * coef[c, 0] * sp.Lambda[c] * 1e-3 / A2;
        if (sp.PolyFocus)
        {
            double num = 0, den = 0;
            for (int l = 0; l < nl; l++)
            {
                double k = A2 / (4.0 * sp.Lambda[l] * 1e-3);
                num += sp.Weight[l] * coef[l, 0] * k; den += sp.Weight[l] * k * k;
            }
            focus = num / den;
        }
        double sum = 0, wsum = 0;
        for (int l = 0; l < nl; l++)
        {
            double ms = 0;
            for (int t = 0; t < nt; t++)
            {
                double ct = t == 0 ? coef[l, 0] - focus * A2 / (4.0 * sp.Lambda[l] * 1e-3) : coef[l, t];
                ms += ct * ct / (2 * t + 3);
            }
            sum += sp.Weight[l] * ms; wsum += sp.Weight[l];
        }
        dS = focus;
        return 1.5 * sum / wsum;
    }
}

/// <summary>Small dense linear algebra.</summary>
public static class Linear
{
    public static double[,] Invert(double[,] m)
    {
        int n = m.GetLength(0);
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
                double f = a[r, col];
                for (int j = 0; j < 2 * n; j++) a[r, j] -= f * a[col, j];
            }
        }
        var inv = new double[n, n];
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) inv[i, j] = a[i, n + j];
        return inv;
    }

    /// <summary>Solves A x = b in place for a small symmetric positive definite A (Cholesky); false if not positive definite.</summary>
    public static bool SolveSpd(double[,] A, double[] b, int n)
    {
        for (int j = 0; j < n; j++)
        {
            double d = A[j, j];
            for (int k = 0; k < j; k++) d -= A[j, k] * A[j, k];
            if (!(d > 0)) return false;
            A[j, j] = Math.Sqrt(d);
            for (int i = j + 1; i < n; i++)
            {
                double v = A[i, j];
                for (int k = 0; k < j; k++) v -= A[i, k] * A[j, k];
                A[i, j] = v / A[j, j];
            }
        }
        for (int i = 0; i < n; i++) { double v = b[i]; for (int k = 0; k < i; k++) v -= A[i, k] * b[k]; b[i] = v / A[i, i]; }
        for (int i = n - 1; i >= 0; i--) { double v = b[i]; for (int k = i + 1; k < n; k++) v -= A[k, i] * b[k]; b[i] = v / A[i, i]; }
        return true;
    }
}
