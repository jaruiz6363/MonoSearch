using System.Globalization;
using MonoSearch;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
// A run in a background terminal is never the foreground app; without this Windows 11 throttles it throughout.
WindowsPerformance.DisablePowerThrottling();

var opt = Options.Parse(args);
if (opt.Command is null || opt.Has("help"))
{
    Console.WriteLine("""
        MonoSearch - global glass search for two-glass monocentric lenses
        (Stamenov, Agurok, Ford, Appl. Opt. 51, 7648 (2012)).

          search  [--settings file.monosearch] --f 12 --fno 1.71 --lambda 0.47,0.55,0.65
                  [--catalogs SCHOTT,OHARA,HOYA,SUMITA,CDGM,MISC] [--mode paper|direct] [--top 200] [--export 10]
                  [--formats zmx,seq,len,otx,json,lhlt] [--scan 400] [--include-obsolete] [--min-bfl 0] [--fov 60] [--max-dcte 1.5] [--out results]
          eval    --f 12 --fno 1.71 --lambda 0.47,0.55,0.65 --outer OHARA:S-LAH79 --inner SUMITA:LASFN9 [--r1 9.049]
          check   [--catalogs ...]      compare computed nd with the catalog nd for every glass
          readback --dir <folder>       read exported lens files back and compare their prescriptions
          save-settings --file x.monosearch [options]   write the settings the options describe

        mode paper : r1 = global minimum of Q (Eq. 31), ranked by polychromatic wavefront (Eq. 38) - reproduces Table 3
        mode direct: r1 = global minimum of the polychromatic wavefront itself
        Catalogs are read from the catalogs folder next to the executable (vendor AGF files).
        Obsolete glasses are excluded unless --include-obsolete is given.

        Wavelengths: --lambda takes 1 to 24 values in um; --weights the same count (default all 1);
        --primary the 1-based reference wavelength (default the middle one). --design-lambda (Eq. 9 and Q)
        defaults to the d line when it lies inside the band, else to the primary wavelength.
        --settings loads a file saved by the GUI or save-settings; any option given as well overrides it.

        Four-glass asymmetric lens with an air gap (Stamenov, Agurok, Ford, Appl. Opt. 52, 8287 (2013)):
          search --arch 4GA-8 [--core SUMITA:K-GFK68,...] [--cores 1] [--grouping fine|normal|coarse] [other options]
        Without --core the core is the best ball glass of a 2GS search (--cores N: the best N). Each core takes
        minutes (coarse) to about an hour (fine) with 9 wavelengths; fewer wavelengths are proportionally faster.
        """);
    return 0;
}

string catDir = opt.Get("catdir", SearchEngine.DefaultCatalogFolder);
try
{
    var settings = ReadSettings(opt);
    switch (opt.Command)
    {
        case "check": return Check(SearchEngine.LoadCatalogs(catDir, settings.Catalogs));
        case "eval": return EvalOne(opt, settings, SearchEngine.LoadCatalogs(catDir, settings.Catalogs));
        case "search": return Search(settings, catDir);
        case "readback": return Readback(opt, catDir);
        case "save-settings":
            settings.ToSpec();
            settings.Save(opt.Get("file", "settings" + SearchSettings.FileExtension));
            Console.WriteLine($"Settings written to {Path.GetFullPath(opt.Get("file", "settings" + SearchSettings.FileExtension))}");
            return 0;
        default: Console.Error.WriteLine($"Unknown command {opt.Command}"); return 1;
    }
}
catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or InvalidDataException or System.Text.Json.JsonException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

// Settings from --settings (or the defaults), then every option given on the command line on top.
static SearchSettings ReadSettings(Options opt)
{
    var s = opt.Has("settings") ? SearchSettings.Load(opt.Get("settings", "")) : new SearchSettings();
    static double[] List(string v) =>
        v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();

    if (opt.Has("f")) s.FocalLength = opt.GetD("f", s.FocalLength);
    if (opt.Has("fno")) s.FNumber = opt.GetD("fno", s.FNumber);
    if (opt.Has("lambda"))
    {
        s.Wavelengths = List(opt.Get("lambda", "")).Select(l => new WavelengthSetting(l)).ToList();
        if (!opt.Has("primary")) s.PrimaryWavelength = (s.Wavelengths.Count + 1) / 2;
    }
    if (opt.Has("weights"))
    {
        var w = List(opt.Get("weights", ""));
        if (w.Length != s.Wavelengths.Count) throw new ArgumentException($"--weights needs {s.Wavelengths.Count} values, one per wavelength");
        for (int k = 0; k < w.Length; k++) s.Wavelengths[k].Weight = w[k];
    }
    if (opt.Has("primary")) s.PrimaryWavelength = opt.GetI("primary", s.PrimaryWavelength);
    if (opt.Has("design-lambda")) s.DesignWavelength = opt.GetD("design-lambda", SearchSettings.DLine);
    if (opt.Has("min-bfl")) s.MinBackFocus = opt.GetD("min-bfl", 0);
    if (opt.Has("fov")) s.HalfFieldAngle = opt.GetD("fov", s.HalfFieldAngle);
    if (opt.Has("max-dcte")) s.MaxCteDifference = opt.GetD("max-dcte", 1.5);
    if (opt.Has("arch")) s.Architecture = opt.Get("arch", s.Architecture).ToUpperInvariant();
    if (opt.Has("core")) s.CoreGlasses = opt.Get("core", "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    if (opt.Has("cores")) s.CoreCount = opt.GetI("cores", s.CoreCount);
    if (opt.Has("grouping")) s.GlassGrouping = opt.Get("grouping", s.GlassGrouping).ToLowerInvariant();
    if (opt.Has("catalogs")) s.Catalogs = opt.Get("catalogs", "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    if (opt.Has("include-obsolete")) s.IncludeObsolete = true;
    if (opt.Has("mode")) s.Mode = opt.Get("mode", s.Mode);
    if (opt.Has("focus")) s.Focus = opt.Get("focus", s.Focus);
    if (opt.Has("scan")) s.ScanPoints = opt.GetI("scan", s.ScanPoints);
    if (opt.Has("top")) s.KeepTop = opt.GetI("top", s.KeepTop);
    if (opt.Has("export")) s.ExportCount = opt.GetI("export", s.ExportCount);
    if (opt.Has("formats")) s.ExportFormats = opt.Get("formats", "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    if (opt.Has("out")) s.OutputFolder = opt.Get("out", s.OutputFolder);
    if (opt.Has("no-optiland-install")) s.OptilandInstall = false;
    if (opt.Has("zemax-glasscat")) s.ZemaxGlasscat = opt.Get("zemax-glasscat", "");
    return s;
}

static int Search4(SearchSettings settings, string catDir)
{
    var sp = settings.ToSpec();
    Console.WriteLine($"4GA-8: f={sp.F}, F/{sp.FNumber}, {sp.NLambda} wavelengths {sp.Lambda.Min()}-{sp.Lambda.Max()} um, "
        + $"grouping {settings.GlassGrouping}, {Environment.ProcessorCount} threads"
        + (settings.MaxCteDifference is { } dc ? $"; cemented CTE difference <= {dc}e-6/K" : ""));
    var r = Ga8Search.Run(settings, catDir, new ConsoleProgress(), default, m => Console.WriteLine("\r  " + m));
    Console.WriteLine($"\r  {r.GlassesUsable} usable glasses, {r.Screened:N0} combinations screened"
        + (r.Unstartable > 0 ? $" ({r.Unstartable:N0} could not be started)" : "") + $", {r.Elapsed.TotalMinutes:F1} min");
    foreach (var b in r.Baselines)
        Console.WriteLine($"  best 2GS with core {b.Core.Id}: shell {b.Shell.Id}, RMS {b.RmsWaves:F4} waves");

    string csv = Ga8Search.WriteCsv(r, Path.Combine(settings.OutputFolder, $"ranked_{r.Tag}.csv"));
    Console.WriteLine($"Top {r.Ranked.Count} written to {csv}");
    foreach (var c in r.Ranked.Take(20))
        Console.WriteLine($"  {c.Rank,3} fam {c.Family,3}  {c.Glasses,-64} C={c.C:F5} RMS={c.RmsWaves:F4} "
            + $"R={c.Radii.R1:F3},{c.Radii.R2:F3},{c.Radii.R4:F3},{c.Radii.R5:F3},{c.Radii.R6:F3} bfl={c.BackFocus:F2} dCTE={c.DeltaCte:F1}");

    if (settings.ExportCount > 0)
    {
        foreach (var w in Ga8Search.Export(r, r.Ranked.Take(settings.ExportCount), catDir, settings.OutputFolder,
                     settings.ExportFormats, settings.OptilandInstall, settings.ResolvedZemaxGlasscat))
            Console.WriteLine("  " + w);
        Console.WriteLine($"Top {Math.Min(settings.ExportCount, r.Ranked.Count)} written as {string.Join(", ", settings.ExportFormats)}, "
            + $"one folder each, in {Path.GetFullPath(settings.OutputFolder)}");
    }
    return 0;
}

static int Search(SearchSettings settings, string catDir)
{
    if (settings.Architecture == "4GA-8") return Search4(settings, catDir);
    var sp = settings.ToSpec();
    Console.WriteLine($"f={sp.F}, F/{sp.FNumber}, mode={settings.Mode}, {Environment.ProcessorCount} threads");
    Console.WriteLine("  wavelengths " + string.Join(", ", sp.Lambda.Select((l, k) =>
        $"{l} um (w {sp.Weight[k]}){(k == sp.Primary ? " primary" : "")}")) + $"; design wavelength {sp.DesignLambda} um"
        + (sp.MinBfl > 0 ? $"; back focus >= {sp.MinBfl} mm (r1 <= {sp.R1Max:F3})" : "")
        + (settings.MaxCteDifference is { } dc ? $"; shell/ball CTE difference <= {dc}e-6/K" : ""));

    var r = SearchEngine.Run(settings, catDir, new ConsoleProgress());
    Console.WriteLine($"\r  {r.GlassesRead} glasses read, {r.GlassesUsable} usable"
        + (r.GlassesWithoutCte > 0 ? $" ({r.GlassesWithoutCte} more without a CTE left out)" : "")
        + $", {r.Pairs:N0} pairs, {r.ValidDesigns:N0} valid designs, {r.Elapsed.TotalSeconds:F1} s");

    string csv = SearchEngine.WriteCsv(r, Path.Combine(settings.OutputFolder, $"ranked_{r.Tag}.csv"));
    Console.WriteLine($"Top {r.Ranked.Count} written to {csv}");
    foreach (var c in r.Ranked.Take(20))
        Console.WriteLine($"  {c.Rank,3}  {c.Outer.Label,-26} / {c.Inner.Label,-30} r1={c.E.R1:F3} r2={c.E.R2:F3} "
            + $"bfl={c.BackFocus:F3} dPhi2={c.E.DPhi2:F4} dCTE={c.DeltaCte:F1}");

    if (settings.ExportCount > 0)
    {
        foreach (var w in SearchEngine.Export(r, r.Ranked.Take(settings.ExportCount), catDir, settings.OutputFolder,
                     settings.ExportFormats, settings.OptilandInstall, settings.ResolvedZemaxGlasscat))
            Console.WriteLine("  " + w);
        Console.WriteLine($"Top {Math.Min(settings.ExportCount, r.Ranked.Count)} written as {string.Join(", ", settings.ExportFormats)}, "
            + $"one folder each, in {Path.GetFullPath(settings.OutputFolder)}");
    }
    return 0;
}

static int Check(List<Glass> glasses)
{
    int bad = 0, unsupported = 0;
    double worst = 0; string worstId = "";
    foreach (var g in glasses)
    {
        double n = g.Index(0.5875618);
        if (double.IsNaN(n)) { unsupported++; Console.WriteLine($"  unsupported formula {g.Formula}: {g.Id}"); continue; }
        double d = Math.Abs(n - g.Nd);
        if (d > worst) { worst = d; worstId = g.Id; }
        if (d > 5e-5) { bad++; Console.WriteLine($"  {g.Id,-28} nd catalog {g.Nd:F6} computed {n:F6} diff {d:E2}"); }
    }
    Console.WriteLine($"{glasses.Count} glasses, {unsupported} unsupported formula, {bad} with |nd diff| > 5e-5; worst {worst:E2} ({worstId})");
    return 0;
}

// Reads every exported lens file in a folder back with AberrationCalculator's readers and prints its
// prescription and paraxial focal length, so the formats can be compared with each other.
static int Readback(Options opt, string catDir)
{
    var acGlass = new AberrationCalculator.Core.Glass.GlassCatalog();
    acGlass.LoadFolder(catDir);
    foreach (var file in Directory.EnumerateFiles(opt.Get("dir", ".")).OrderBy(f => f))
    {
        string ext = Path.GetExtension(file).ToLowerInvariant();
        AberrationCalculator.Core.Models.OpticalSystem sys;
        try
        {
            sys = ext switch
            {
                ".zmx" => AberrationCalculator.Core.IO.ZmxReader.Read(file, acGlass),
                ".len" => AberrationCalculator.Core.IO.OsloReader.Read(file),
                ".otx" => AberrationCalculator.Core.IO.OptalixReader.Read(file, acGlass),
                ".json" => AberrationCalculator.Core.IO.OptilandReader.Read(file, acGlass),
                ".lhlt" => AberrationCalculator.Core.IO.LhltReader.Read(file).System,
                ".seq" => AberrationCalculator.Core.IO.CodeVReader.Read(file, acGlass),
                _ => null!,
            };
            if (sys is null) continue;
        }
        catch (Exception ex) { Console.WriteLine($"{ext,-6} read failed: {ex.Message}"); continue; }

        var unresolved = new List<string>();
        double lam = sys.Wavelengths.Count > 0 ? sys.Wavelengths[Math.Max(0, sys.PrimaryWavelengthIndex)].Value : 0.55;
        var n = AberrationCalculator.Core.Glass.IndexResolver.Build(sys, acGlass, lam, unresolved);
        double efl = AberrationCalculator.Core.RayTrace.ParaxialTrace.Trace(sys, n, 0.0).Efl;
        string pres = string.Join(" | ", sys.Surfaces.Skip(1).Select(s =>
            $"{(double.IsInfinity(s.Radius) ? "inf" : s.Radius.ToString("F4"))},{s.Thickness:F4},{s.Material}"));
        Console.WriteLine($"{ext,-6} EFL {efl:F5} at {lam} um  EPD {sys.Aperture.Type} {sys.Aperture.Value:F4}  "
            + $"stop {sys.StopSurfaceIndex}  fields {string.Join("/", sys.Fields.Select(f => f.Y))}"
            + (unresolved.Count > 0 ? $"  UNRESOLVED {string.Join(",", unresolved.Distinct())}" : ""));
        Console.WriteLine($"       {pres}");
    }
    return 0;
}

static Glass Find(List<Glass> glasses, string id)
{
    var parts = id.Split(':');
    var hits = glasses.Where(g => parts.Length == 2
        ? g.Catalog.Equals(parts[0], StringComparison.OrdinalIgnoreCase) && HasName(g, parts[1])
        : HasName(g, id)).ToList();
    if (hits.Count != 1) throw new ArgumentException($"{id}: {hits.Count} matches; use CATALOG:NAME");
    return hits[0];

    static bool HasName(Glass g, string n) =>
        g.Name.Equals(n, StringComparison.OrdinalIgnoreCase) || g.Aliases.Any(a => a.Equals(n, StringComparison.OrdinalIgnoreCase));
}

static int EvalOne(Options opt, SearchSettings settings, List<Glass> glasses)
{
    var sp = settings.ToSpec();
    var o = Find(glasses, opt.Get("outer", "")); var i = Find(glasses, opt.Get("inner", ""));
    var px = new PairIndex(sp.Lambda.Select(o.Index).ToArray(), sp.Lambda.Select(i.Index).ToArray(),
        o.Index(sp.DesignLambda), i.Index(sp.DesignLambda));
    Console.WriteLine($"{o.Id} (n={px.N2[sp.Primary]:F5}) / {i.Id} (n={px.N3[sp.Primary]:F5}), f={sp.F}, F/{sp.FNumber}, NA={sp.NA:F4}");
    if (opt.Has("r1") && opt.Has("r2") && opt.Has("image"))
    {
        var rms = Mono.RmsFixed(sp, px, opt.GetD("r1", 0), opt.GetD("r2", 0), opt.GetD("image", 0));
        Console.WriteLine($"  fixed prescription, image at {opt.GetD("image", 0)} from centre: RMS wavefront "
            + string.Join(", ", rms.Select((r, k) => $"{sp.Lambda[k]} um {r:F4}")) + " waves");
        return 0;
    }
    if (opt.Has("r1")) Print("given r1", Mono.Evaluate(sp, px, opt.GetD("r1", 0)), sp);
    Print("paper  (min Q)", Mono.Minimise(sp, px, true, opt.GetI("scan", 2000), 0.15 * sp.F, sp.R1Max), sp);
    Print("direct (min wavefront)", Mono.Minimise(sp, px, false, opt.GetI("scan", 2000), 0.15 * sp.F, sp.R1Max), sp);
    return 0;

    static void Print(string label, Eval? e, Spec sp)
    {
        if (e is null) { Console.WriteLine($"  {label}: no valid solution"); return; }
        var v = e.Value;
        Console.WriteLine($"  {label,-24} r1={v.R1:F4} r2={v.R2:F4} bfl={sp.F - v.DS - v.R1:F4} Q={v.Q:E3} dPhi2={v.DPhi2:F5} rms~{Math.Sqrt(v.DPhi2 / 1.5):F4} waves"
            + $"  C20={v.C20:F4} C40={v.C40:F4} C60={v.C60:F4} C80={v.C80:F4} image R={sp.F - v.DS:F4}");
    }
}

// Reported from worker threads; prints only forward, in whole per cent.
sealed class ConsoleProgress : IProgress<double>
{
    private int _shown = -1;
    public void Report(double value)
    {
        int pct = (int)(value * 100);
        lock (this)
        {
            if (pct <= _shown) return;
            _shown = pct;
            Console.Write($"\r  {pct,3}%");
        }
    }
}

sealed class Options
{
    public string? Command;
    private readonly Dictionary<string, string> _kv = new(StringComparer.OrdinalIgnoreCase);
    public static Options Parse(string[] a)
    {
        var o = new Options();
        for (int k = 0; k < a.Length; k++)
        {
            if (a[k].StartsWith("--"))
            {
                string key = a[k][2..];
                if (k + 1 < a.Length && !a[k + 1].StartsWith("--")) o._kv[key] = a[++k];
                else o._kv[key] = "true";
            }
            else o.Command ??= a[k];
        }
        return o;
    }
    public bool Has(string k) => _kv.ContainsKey(k);
    public string Get(string k, string d) => _kv.TryGetValue(k, out var v) ? v : d;
    public double GetD(string k, double d) => _kv.TryGetValue(k, out var v) ? double.Parse(v, CultureInfo.InvariantCulture) : d;
    public int GetI(string k, int d) => _kv.TryGetValue(k, out var v) ? int.Parse(v, CultureInfo.InvariantCulture) : d;
}
