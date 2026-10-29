using System.Diagnostics;
using System.Globalization;
using System.Text;
using AcGlassCatalog = AberrationCalculator.Core.Glass.GlassCatalog;

namespace MonoSearch;

/// <summary>One ranked 4GA-8 design. Family is the rank of its group of near-identical glasses.</summary>
public sealed record Ga8Candidate(int Rank, int Family, Glass Front, Glass Core, Glass Rear, Glass Meniscus,
    Ga8Radii Radii, double C, double DPhi2, double DS, Spec Spec)
{
    /// <summary>The image at the refocused distance from the centre (best focus of the wavefront fit).</summary>
    public double ImageRadius => Spec.F - DS;
    public double BackFocus => ImageRadius - Radii.R6;
    public double RmsWaves => Math.Sqrt(DPhi2 / 1.5);
    /// <summary>Largest thermal-expansion difference across the two cemented interfaces (front shell/core, core/rear shell).</summary>
    public double DeltaCte => Math.Max(Math.Abs(Front.Cte - Core.Cte), Math.Abs(Rear.Cte - Core.Cte));
    public string Glasses => $"{Front.Id} / {Core.Id} / {Rear.Id} / {Meniscus.Id}";
}

/// <summary>The best symmetric two-glass design with a core, for comparison, on the same wavefront measure.</summary>
public sealed record Ga8Baseline(Glass Core, Glass Shell, double DPhi2)
{
    public double RmsWaves => Math.Sqrt(DPhi2 / 1.5);
}

public sealed class Ga8Result
{
    public required Spec Spec { get; init; }
    public required SearchSettings Settings { get; init; }
    public required List<Ga8Candidate> Ranked { get; init; }
    public required List<Ga8Baseline> Baselines { get; init; }
    public int GlassesUsable { get; init; }
    public int GlassesWithoutCte { get; init; }
    public long Screened { get; init; }
    /// <summary>Combinations from which no starting point traces (every seed, also scaled to f, sends a ray astray).</summary>
    public long Unstartable { get; init; }
    /// <summary>Stopped by the user: the results come from the combinations screened so far (<see cref="Screened"/> of <see cref="Total"/>).</summary>
    public bool Stopped { get; init; }
    public long Total { get; init; }
    public TimeSpan Elapsed { get; init; }
    public string Tag => string.Create(CultureInfo.InvariantCulture, $"f{Spec.F}_F{Spec.FNumber}_4GA-8");
}

/// <summary>
/// Near-global search for 4GA-8 lenses after Stamenov, Agurok and Ford 2013, section 3.D: the core glass comes from the
/// best 2GS designs; every other glass is tried in the front shell, the rear shell and the meniscus, and the five radii
/// minimise the paper's criterion C (Eq. 11) for each combination.
/// <list type="number">
/// <item>Screen: every combination of representative glasses (near-identical glasses grouped), C minimised with a short
/// Levenberg-Marquardt run from the paper's starting radii.</item>
/// <item>Refine: the best combinations again, to convergence, from three starting points.</item>
/// <item>Expand: the other glasses of each group (the paper's "replacement glasses") on the best combinations.</item>
/// </list>
/// With a CTE limit, the front and rear shells must match the core within it (both are cemented to it); the meniscus is
/// air-spaced and free.
/// </summary>
public static class Ga8Search
{
    public const int ScreenIterations = 15, RefineIterations = 300, RefineCount = 400, ExpandFamilies = 40, ExpandCap = 64;
    public const double AbandonFactor = 3.0;

    /// <summary>Smallest glass thickness and air gap, as fractions of f (0.36 mm and 0.12 mm at f = 12 mm; the paper's
    /// designs have shells of 1.2 mm and more and gaps of 0.24 to 0.43 mm).</summary>
    public const double MinGlassFraction = 0.03, MinGapFraction = 0.01;

    /// <summary>Pupil zones of the wavefront fit reported for 4GA-8 (13 zones, C20..C100, as the paper's Eq. 2 adds C100).</summary>
    public static readonly Concentric.ZernikeFit Fit = new(Enumerable.Range(0, 13).Select(k => 1 - 0.05 * k).ToArray(), 5);

    private sealed record Group(Glass Rep, List<Glass> Members);

    public static Ga8Result Run(SearchSettings settings, string catDir, IProgress<double>? progress = null,
        CancellationToken cancel = default, Action<string>? log = null)
    {
        var sw = Stopwatch.StartNew();
        var sp = settings.ToSpec();
        var all = SearchEngine.LoadCatalogs(catDir, settings.Catalogs);
        var usable = SearchEngine.Usable(settings, sp, all, out int noCte);
        var cores = ResolveCores(settings, catDir, usable, cancel, log);
        if (cancel.IsCancellationRequested) throw new OperationCanceledException("Stopped while finding the core, before any 4GA-8 design.");
        double? maxDcte = settings.MaxCteDifference;
        double minGlass = MinGlassFraction * sp.F, minGap = MinGapFraction * sp.F;
        var index = usable.ToDictionary(g => g, g => sp.Lambda.Select(g.Index).ToArray());
        var (dn, dv, dp) = settings.GlassGrouping switch
        {
            "fine" => (0.005, 0.5, 0.002), "coarse" => (0.02, 1.5, 0.005), _ => (0.01, 1.0, 0.003),
        };

        // Work per core: the representative glasses for each position.
        var plans = cores.Select(core =>
        {
            bool Cemented(Glass g) => g != core && (maxDcte is not { } m || Math.Abs(g.Cte - core.Cte) <= m);
            var shells = Groups(usable.Where(Cemented), dn, dv, dp);
            var menisci = Groups(usable, dn, dv, dp);
            return (Core: core, Shells: shells, Menisci: menisci);
        }).ToList();
        long total = plans.Sum(p => (long)p.Shells.Count * p.Shells.Count * p.Menisci.Count), done = 0, screened = 0, unstartable = 0;
        log?.Invoke($"{cores.Count} core(s): {string.Join(", ", cores.Select(c => c.Id))}; "
                    + string.Join("; ", plans.Select(p => $"{p.Core.Name}: {p.Shells.Count} shell x {p.Shells.Count} rear x {p.Menisci.Count} meniscus groups"))
                    + $" = {total:N0} combinations");

        // Stop (the cancellation token) ends the screening; what has been screened is still refined and ranked.
        var screenClock = Stopwatch.StartNew();
        long lastReport = 0;
        double bestSoFar = double.PositiveInfinity;
        object bestGate = new();
        bool stopped = false;

        var designs = new List<(Glass F, Glass K, Glass R, Glass M, Ga8Radii Radii, double C, Group G1, Group G3, Group G4)>();
        for (int pi = 0; pi < plans.Count && !stopped; pi++)
        {
            var plan = plans[pi];
            var core = plan.Core;
            var shells = plan.Shells; var menisci = plan.Menisci;
            Ga8Problem Problem(Glass f, Glass r, Glass m) => new(sp, new[] { index[f], index[core], index[r], index[m] }, minGlass, minGap);
            var seeds = Ga8Problem.Seeds(sp.F).ToArray();
            string coreLabel = cores.Count > 1 ? $"core {core.Name} ({pi + 1} of {cores.Count})" : $"core {core.Name}";

            void ScreenStatus()
            {
                long now = screenClock.ElapsedMilliseconds, last = Interlocked.Read(ref lastReport);
                if (now - last < 1000 || Interlocked.CompareExchange(ref lastReport, now, last) != last) return;
                long d = Interlocked.Read(ref done);
                double frac = (double)d / total;
                string left = frac > 0.01 ? $", about {Clock(TimeSpan.FromSeconds(now / 1000.0 * (1 - frac) / frac))} left" : "";
                string best = double.IsFinite(bestSoFar) ? $" Best C so far {bestSoFar:F5}." : "";
                log?.Invoke($"Screening {coreLabel}: {d:N0} of {total:N0} combinations ({frac:P0}), "
                            + $"{Clock(screenClock.Elapsed)} elapsed{left}.{best} Stop keeps what has been found.");
            }

            // 1. Screen every combination of representatives.
            var best = new List<(int A, int B, int M, double C)>();
            object gate = new();
            int nPairs = shells.Count * shells.Count;
            long doneBefore = Interlocked.Read(ref done);
            Parallel.For(0, nPairs, new ParallelOptions(),
                () => new List<(int A, int B, int M, double C)>(),
                (pair, state, local) =>
                {
                    if (cancel.IsCancellationRequested) { state.Stop(); return local; }
                    int a = pair / shells.Count, b = pair % shells.Count;
                    // Abandon a combination still AbandonFactor x above this thread's RefineCount-th best after 5 iterations.
                    double cut = local.Count >= RefineCount ? AbandonFactor * local[RefineCount - 1].C : double.PositiveInfinity;
                    int noStart = 0;
                    double lowest = double.PositiveInfinity;
                    for (int m = 0; m < menisci.Count; m++)
                    {
                        var prob = Problem(shells[a].Rep, shells[b].Rep, menisci[m].Rep);
                        if (prob.Start(seeds) is not { } start) { noStart++; continue; }
                        var (_, c) = prob.Optimise(start, ScreenIterations, cut);
                        if (double.IsFinite(c)) { local.Add((a, b, m, c)); lowest = Math.Min(lowest, c); }
                    }
                    if (noStart > 0) Interlocked.Add(ref unstartable, noStart);
                    if (lowest < bestSoFar) lock (bestGate) bestSoFar = Math.Min(bestSoFar, lowest);
                    if (local.Count > 2 * RefineCount) Trim(local, RefineCount);
                    long d = Interlocked.Add(ref done, menisci.Count);
                    if (progress != null && pair % 16 == 0) progress.Report(0.9 * d / total);
                    ScreenStatus();
                    return local;
                },
                local => { lock (gate) { best.AddRange(local); Trim(best, RefineCount); } });
            screened += Interlocked.Read(ref done) - doneBefore;
            stopped = cancel.IsCancellationRequested;
            if (best.Count == 0) continue;

            // 2. Refine the best to convergence from every starting point and from the screening result.
            log?.Invoke($"Refining the {best.Count} best combinations for {coreLabel}{(stopped ? " (stopped: those screened so far)" : "")}...");
            var refined = new (int A, int B, int M, Ga8Radii R, double C)[best.Count];
            Parallel.For(0, best.Count, k =>
            {
                var (a, b, m, _) = best[k];
                var prob = Problem(shells[a].Rep, shells[b].Rep, menisci[m].Rep);
                var top = (R: seeds[0], C: double.PositiveInfinity);
                foreach (var s in seeds)
                {
                    if (prob.Start(new[] { s }) is not { } start) continue;
                    var r = prob.Optimise(start, RefineIterations);
                    if (r.C < top.C) top = r;
                }
                refined[k] = (a, b, m, top.R, top.C);
            });
            var families = refined.Where(r => double.IsFinite(r.C)).OrderBy(r => r.C).Take(ExpandFamilies).ToList();
            progress?.Report(0.9 * done / total);

            // 3. Expand the best families to their replacement glasses, starting from the family's radii.
            log?.Invoke($"Trying replacement glasses on the {families.Count} best families for {coreLabel}...");
            var expanded = new List<(Glass, Glass, Glass, Glass, Ga8Radii, double, Group, Group, Group)>();
            Parallel.ForEach(families, fam =>
            {
                Group g1 = shells[fam.A], g3 = shells[fam.B], g4 = menisci[fam.M];
                var combos = (from f in g1.Members from r in g3.Members from m in g4.Members select (f, r, m))
                    .Take(ExpandCap).ToList();
                var rows = new List<(Glass, Glass, Glass, Glass, Ga8Radii, double, Group, Group, Group)>();
                foreach (var (f, r, m) in combos)
                {
                    if (f == g1.Rep && r == g3.Rep && m == g4.Rep) { rows.Add((f, core, r, m, fam.R, fam.C, g1, g3, g4)); continue; }
                    var prob = Problem(f, r, m);
                    var res = prob.Optimise(fam.R, RefineIterations);
                    if (!double.IsFinite(res.C) && prob.Start(seeds) is { } start) res = prob.Optimise(start, RefineIterations);
                    if (double.IsFinite(res.C)) rows.Add((f, core, r, m, res.Radii, res.C, g1, g3, g4));
                }
                lock (expanded) expanded.AddRange(rows);
            });
            designs.AddRange(expanded.Select(e => (e.Item1, e.Item2, e.Item3, e.Item4, e.Item5, e.Item6, e.Item7, e.Item8, e.Item9)));
        }
        if (designs.Count == 0 && stopped) throw new OperationCanceledException("Stopped before any combination was screened.");

        // Rank: refocused wavefront and back focus for each design, ordered by C; families numbered by their best member.
        log?.Invoke("Ranking...");
        var scored = designs.AsParallel().Select(d =>
        {
            var prob = new Ga8Problem(sp, new[] { index[d.F], index[d.K], index[d.R], index[d.M] }, minGlass, minGap);
            double dphi2 = prob.DPhi2(d.Radii, Fit, out double dS);
            return (d, dphi2, dS);
        }).Where(x => double.IsFinite(x.dphi2) && sp.F - x.dS - x.d.Radii.R6 >= sp.MinBfl)
          .OrderBy(x => x.d.C).ToList();
        var familyOf = new Dictionary<(Group, Group, Group, Glass), int>();
        var ranked = new List<Ga8Candidate>();
        foreach (var (d, dphi2, dS) in scored)
        {
            var key = (d.G1, d.G3, d.G4, d.K);
            if (!familyOf.TryGetValue(key, out int fam)) familyOf[key] = fam = familyOf.Count + 1;
            ranked.Add(new Ga8Candidate(ranked.Count + 1, fam, d.F, d.K, d.R, d.M, d.Radii, d.C, dphi2, dS, sp));
            if (ranked.Count >= Math.Max(1, settings.KeepTop)) break;
        }
        progress?.Report(1.0);

        return new Ga8Result
        {
            Spec = sp, Settings = settings.Clone(), Ranked = ranked, Baselines = cores.Select(c => Baseline(sp, c, usable)).ToList(),
            GlassesUsable = usable.Length, GlassesWithoutCte = noCte, Screened = screened, Unstartable = unstartable, Elapsed = sw.Elapsed,
            Stopped = stopped, Total = total,
        };
    }

    private static string Clock(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

    private static void Trim(List<(int A, int B, int M, double C)> list, int keep)
    {
        list.Sort((x, y) => x.C.CompareTo(y.C));
        if (list.Count > keep) list.RemoveRange(keep, list.Count - keep);
    }

    /// <summary>The core glasses: those named in the settings, or the first distinct ball glasses of a 2GS search.</summary>
    private static List<Glass> ResolveCores(SearchSettings settings, string catDir, Glass[] usable, CancellationToken cancel,
        Action<string>? log)
    {
        if (settings.CoreGlasses.Count > 0)
            return settings.CoreGlasses.Select(id => Find(usable, id)
                ?? throw new ArgumentException($"Core glass {id} is not among the usable glasses (catalogs, band, obsolete, CTE).")).ToList();
        log?.Invoke("Finding the core: 2GS search with these settings...");
        var s2 = settings.Clone();
        s2.Architecture = "2GS";
        s2.KeepTop = Math.Max(200, settings.KeepTop);
        var r2 = SearchEngine.Run(s2, catDir, null, cancel);
        // The 2GS search loads its own copy of the catalogs: map its ball glasses back to this search's glasses by id.
        var cores = r2.Ranked.Select(c => c.Inner.Id).Distinct().Select(id => usable.FirstOrDefault(g => g.Id == id))
            .OfType<Glass>().Take(settings.CoreCount).ToList();
        if (cores.Count == 0) throw new ArgumentException("The 2GS search found no core glass; give core glasses explicitly.");
        log?.Invoke($"2GS search for the core: {string.Join(", ", cores.Select(c => c.Id))}");
        return cores;
    }

    /// <summary>A glass by CATALOG:NAME or NAME, matching aliases, ignoring case.</summary>
    public static Glass? Find(IEnumerable<Glass> glasses, string id)
    {
        string cat = id.Contains(':') ? id[..id.IndexOf(':')] : "", name = id.Contains(':') ? id[(id.IndexOf(':') + 1)..] : id;
        return glasses.FirstOrDefault(g => (cat == "" || string.Equals(g.Catalog, cat, StringComparison.OrdinalIgnoreCase))
            && (string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)
                || g.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase)));
    }

    /// <summary>Groups glasses whose nd, Vd and PgF all agree within the tolerances; preferred glasses (status 1)
    /// become representatives first.</summary>
    private static List<Group> Groups(IEnumerable<Glass> glasses, double dn, double dv, double dp)
    {
        static double Pg(Glass g)
        {
            double v = (g.Index(0.4358343) - g.Index(0.4861327)) / (g.Index(0.4861327) - g.Index(0.6562725));
            return double.IsFinite(v) ? v : 0;
        }
        var groups = new List<Group>();
        var pg = new List<double>();
        foreach (var g in glasses.OrderBy(g => g.Status == 1 ? 0 : 1).ThenBy(g => g.Nd))
        {
            double p = Pg(g);
            int k = groups.FindIndex(x => Math.Abs(x.Rep.Nd - g.Nd) <= dn && Math.Abs(x.Rep.Vd - g.Vd) <= dv);
            while (k >= 0 && Math.Abs(pg[k] - p) > dp)
            {
                int next = groups.FindIndex(k + 1, x => Math.Abs(x.Rep.Nd - g.Nd) <= dn && Math.Abs(x.Rep.Vd - g.Vd) <= dv);
                k = next;
            }
            if (k >= 0) groups[k].Members.Add(g);
            else { groups.Add(new Group(g, new List<Glass> { g })); pg.Add(p); }
        }
        return groups;
    }

    /// <summary>The best 2GS design with this core (any shell glass), on the 4GA-8 wavefront measure.</summary>
    private static Ga8Baseline Baseline(Spec sp, Glass core, Glass[] usable)
    {
        var n3 = sp.Lambda.Select(core.Index).ToArray();
        double n3d = core.Index(sp.DesignLambda);
        var best = new Ga8Baseline(core, core, double.PositiveInfinity);
        foreach (var shell in usable)
        {
            if (shell == core) continue;
            var px = new PairIndex(sp.Lambda.Select(shell.Index).ToArray(), n3, shell.Index(sp.DesignLambda), n3d);
            var e = Mono.Minimise(sp, px, false, 200, 0.15 * sp.F, sp.R1Max);
            if (e is null) continue;
            var ev = e.Value;
            double d = Concentric.DPhi2(sp, Fit, (j, l) => Mono.S(Fit.Rho[j] * sp.H1, ev.R1, ev.R2, px.N2[l], px.N3[l]), out _);
            if (d < best.DPhi2) best = new Ga8Baseline(core, shell, d);
        }
        return best;
    }

    public static string WriteCsv(Ga8Result r, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder("rank,family,front,core,rear,meniscus,R1,R2,R4,R5,R6,image_radius,bfl,C,dPhi2,rms_waves,dcte\n");
        foreach (var c in r.Ranked)
            sb.AppendLine(string.Join(",", c.Rank, c.Family, c.Front.Label, c.Core.Label, c.Rear.Label, c.Meniscus.Label,
                c.Radii.R1.ToString("F4", ci), c.Radii.R2.ToString("F4", ci), c.Radii.R4.ToString("F4", ci),
                c.Radii.R5.ToString("F4", ci), c.Radii.R6.ToString("F4", ci), c.ImageRadius.ToString("F4", ci),
                c.BackFocus.ToString("F4", ci), c.C.ToString("F6", ci), c.DPhi2.ToString("F6", ci),
                c.RmsWaves.ToString("F4", ci), c.DeltaCte.ToString("F2", ci)));
        File.WriteAllText(path, sb.ToString());
        return Path.GetFullPath(path);
    }

    public static List<string> Export(Ga8Result r, IEnumerable<Ga8Candidate> candidates, string catDir, string outDir,
        IEnumerable<string> formats, bool optilandInstall, string? zemaxGlasscat)
    {
        var warnings = new List<string>();
        var zc = ZemaxCatalogs.Open(zemaxGlasscat);
        var acGlass = new AcGlassCatalog();
        acGlass.LoadFolder(catDir);
        var fmts = formats.ToList();
        foreach (var c in candidates)
        {
            string name = $"rank{c.Rank:D3}_{SearchEngine.Safe(c.Front.Name)}_{SearchEngine.Safe(c.Core.Name)}_"
                          + $"{SearchEngine.Safe(c.Rear.Name)}_{SearchEngine.Safe(c.Meniscus.Name)}";
            foreach (var w in Exporters.Write(Path.Combine(outDir, name), $"{r.Tag}_{name}", fmts, MonoLens.From(c),
                         zc, acGlass, optilandInstall))
                warnings.Add($"rank {c.Rank}: {w}");
        }
        return warnings;
    }
}
