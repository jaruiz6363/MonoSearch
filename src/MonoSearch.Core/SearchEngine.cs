using System.Diagnostics;
using System.Globalization;
using System.Text;
using AcGlassCatalog = AberrationCalculator.Core.Glass.GlassCatalog;

namespace MonoSearch;

/// <summary>One ranked design.</summary>
public sealed record Candidate(int Rank, Glass Outer, Glass Inner, Eval E, Spec Spec)
{
    public double BackFocus => Spec.F - E.DS - E.R1;
    public double ImageRadius => Spec.F - E.DS;
    /// <summary>Weighted RMS wavefront over the band, waves (dPhi2 is 1.5 x the weighted mean square).</summary>
    public double RmsWaves => Math.Sqrt(E.DPhi2 / 1.5);
    /// <summary>Thermal-expansion difference across the cemented shell/ball interface, 1e-6/K (NaN if a CTE is unknown).</summary>
    public double DeltaCte => Math.Abs(Outer.Cte - Inner.Cte);
}

public sealed class SearchResult
{
    public required Spec Spec { get; init; }
    public required SearchSettings Settings { get; init; }
    public required List<Candidate> Ranked { get; init; }
    public int GlassesRead { get; init; }
    public int GlassesUsable { get; init; }
    /// <summary>Glasses left out because the CTE limit is on and their catalog gives no CTE.</summary>
    public int GlassesWithoutCte { get; init; }
    public long Pairs { get; init; }
    public long ValidDesigns { get; init; }
    public TimeSpan Elapsed { get; init; }
    public string Tag => $"f{Spec.F.ToString(CultureInfo.InvariantCulture)}_F{Spec.FNumber.ToString(CultureInfo.InvariantCulture)}_{Settings.Mode}";
}

/// <summary>The glass search, shared by the command line and the GUI.</summary>
public static class SearchEngine
{
    public static string DefaultCatalogFolder => Path.Combine(AppContext.BaseDirectory, "catalogs");

    /// <summary>The catalog names available in a folder (file names of its .agf files).</summary>
    public static List<string> AvailableCatalogs(string catDir) =>
        Directory.Exists(catDir)
            ? Directory.EnumerateFiles(catDir).Where(p => p.EndsWith(".agf", StringComparison.OrdinalIgnoreCase))
                .Select(p => Path.GetFileNameWithoutExtension(p).ToUpperInvariant()).OrderBy(n => n).ToList()
            : new List<string>();

    public static List<Glass> LoadCatalogs(string catDir, IEnumerable<string> names)
    {
        var glasses = new List<Glass>();
        foreach (var c in names)
        {
            var file = Directory.EnumerateFiles(catDir).FirstOrDefault(p =>
                string.Equals(Path.GetFileNameWithoutExtension(p), c, StringComparison.OrdinalIgnoreCase)
                && p.EndsWith(".agf", StringComparison.OrdinalIgnoreCase))
                ?? throw new FileNotFoundException($"Catalog {c} not found in {catDir}");
            glasses.AddRange(AgfReader.Load(file));
        }
        return AgfReader.CollapseAliases(glasses);
    }

    /// <summary>Runs the search. Progress reports the fraction of outer glasses done, 0..1.</summary>
    public static SearchResult Run(SearchSettings settings, string catDir, IProgress<double>? progress = null,
        CancellationToken cancel = default)
    {
        var sp = settings.ToSpec();
        var all = LoadCatalogs(catDir, settings.Catalogs);
        bool byQ = settings.Mode != "direct";
        double lmin = Math.Min(sp.Lambda.Min(), sp.DesignLambda), lmax = Math.Max(sp.Lambda.Max(), sp.DesignLambda);

        var usable = all.Where(g =>
                (settings.IncludeObsolete || g.Status != 2)
                && g.LambdaMin <= lmin + 1e-6 && g.LambdaMax >= lmax - 1e-6
                && (g.Nd <= 1.0 || Math.Abs(g.Index(0.5875618) - g.Nd) < 1e-3)   // formula must agree with its own nd
                && sp.Lambda.All(l => double.IsFinite(g.Index(l)) && g.Index(l) > 1))
            .ToArray();
        // The CTE limit applies to the cemented shell/ball interface; a glass without a CTE cannot be checked.
        double? maxDcte = settings.MaxCteDifference;
        int noCte = maxDcte is null ? 0 : usable.Count(g => double.IsNaN(g.Cte));
        if (maxDcte is not null) usable = usable.Where(g => !double.IsNaN(g.Cte)).ToArray();
        var cte = usable.Select(g => g.Cte).ToArray();
        // Indices once, before the parallel loop; the loop only reads these arrays.
        var n = usable.Select(g => sp.Lambda.Select(g.Index).ToArray()).ToArray();
        var nd = usable.Select(g => g.Index(sp.DesignLambda)).ToArray();
        int N = usable.Length;

        var perOuter = new List<(int J, Eval E)>[N];
        long done = 0, pairs = 0;
        var sw = Stopwatch.StartNew();
        Parallel.For(0, N, new ParallelOptions { CancellationToken = cancel }, i =>
        {
            var rows = new List<(int, Eval)>();
            long tried = 0;
            for (int j = 0; j < N; j++)
            {
                if (j == i) continue;
                if (maxDcte is { } m && Math.Abs(cte[i] - cte[j]) > m) continue;
                tried++;
                var px = new PairIndex(n[i], n[j], nd[i], nd[j]);
                var e = Mono.Minimise(sp, px, byQ, settings.ScanPoints, 0.15 * sp.F, sp.R1Max);
                if (e is not null) rows.Add((j, e.Value));
            }
            perOuter[i] = rows;
            Interlocked.Add(ref pairs, tried);
            long d = Interlocked.Increment(ref done);
            if (progress != null && (d % 10 == 0 || d == N)) progress.Report((double)d / N);
        });

        var all3 = new List<(int I, int J, Eval E)>();
        for (int i = 0; i < N; i++) foreach (var (j, e) in perOuter[i]) all3.Add((i, j, e));
        var top = all3.OrderBy(r => r.E.DPhi2).Take(Math.Max(1, settings.KeepTop))
            .Select((r, k) => new Candidate(k + 1, usable[r.I], usable[r.J], r.E, sp)).ToList();

        return new SearchResult
        {
            Spec = sp, Settings = settings.Clone(), Ranked = top, GlassesRead = all.Count, GlassesUsable = N,
            GlassesWithoutCte = noCte, Pairs = pairs, ValidDesigns = all3.Count, Elapsed = sw.Elapsed,
        };
    }

    public static string WriteCsv(SearchResult r, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var sb = new StringBuilder("rank,outer,outer_nd,outer_vd,outer_status,inner,inner_nd,inner_vd,inner_status,r1,r2,bfl,Q,dPhi2,rms_waves,C20,C40,C60,C80,image_radius,outer_cte,inner_cte,dcte\n");
        foreach (var c in r.Ranked)
        {
            Glass o = c.Outer, i = c.Inner;
            sb.AppendLine(string.Join(",", c.Rank, o.Label, o.Nd, o.Vd, Glass.StatusName(o.Status), i.Label, i.Nd, i.Vd,
                Glass.StatusName(i.Status), c.E.R1.ToString("F4"), c.E.R2.ToString("F4"), c.BackFocus.ToString("F4"),
                c.E.Q.ToString("E3"), c.E.DPhi2.ToString("F5"), c.RmsWaves.ToString("F4"), c.E.C20.ToString("F4"),
                c.E.C40.ToString("F4"), c.E.C60.ToString("F4"), c.E.C80.ToString("F4"), c.ImageRadius.ToString("F4"), o.Cte, i.Cte, c.DeltaCte.ToString("F2")));
        }
        File.WriteAllText(path, sb.ToString());
        return Path.GetFullPath(path);
    }

    /// <summary>Writes the first <paramref name="count"/> candidates in the settings' formats, one folder each; returns warnings.</summary>
    public static List<string> Export(SearchResult r, IEnumerable<Candidate> candidates, string catDir, string outDir,
        IEnumerable<string> formats, bool optilandInstall, string? zemaxGlasscat)
    {
        var warnings = new List<string>();
        var zc = ZemaxCatalogs.Open(zemaxGlasscat);
        var acGlass = new AcGlassCatalog();
        acGlass.LoadFolder(catDir);
        var fmts = formats.ToList();
        foreach (var c in candidates)
        {
            string name = $"rank{c.Rank:D3}_{Safe(c.Outer.Name)}_{Safe(c.Inner.Name)}";
            foreach (var w in Exporters.Write(Path.Combine(outDir, name), $"{r.Tag}_{name}", fmts, r.Spec, c.Outer, c.Inner,
                         c.E, c.Rank, zc, acGlass, optilandInstall))
                warnings.Add($"rank {c.Rank}: {w}");
        }
        return warnings;
    }

    public static string Safe(string s) => string.Concat(s.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' ? ch : '_'));
}
