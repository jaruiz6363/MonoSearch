using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MonoSearch.App.ViewModels;

public partial class WavelengthRow : ObservableObject
{
    [ObservableProperty] private double _micrometres;
    [ObservableProperty] private double _weight = 1.0;
}

public partial class CheckItem : ObservableObject
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    [ObservableProperty] private bool _isSelected;
}

/// <summary>One row of the results table.</summary>
public sealed class ResultRow
{
    public required Candidate Candidate { get; init; }
    public int Rank => Candidate.Rank;
    public string Outer => Candidate.Outer.Label;
    public double OuterNd => Candidate.Outer.Nd;
    public double OuterVd => Candidate.Outer.Vd;
    public string Inner => Candidate.Inner.Label;
    public double InnerNd => Candidate.Inner.Nd;
    public double InnerVd => Candidate.Inner.Vd;
    public double R1 => Candidate.E.R1;
    public double R2 => Candidate.E.R2;
    public double BackFocus => Candidate.BackFocus;
    public double ImageRadius => Candidate.ImageRadius;
    public double DPhi2 => Candidate.E.DPhi2;
    public double Rms => Candidate.RmsWaves;
    public double OuterCte => Candidate.Outer.Cte;
    public double InnerCte => Candidate.Inner.Cte;
    public double DeltaCte => Candidate.DeltaCte;
}

/// <summary>One row of the 4GA-8 results table.</summary>
public sealed class Ga8Row
{
    public required Ga8Candidate Candidate { get; init; }
    public int Rank => Candidate.Rank;
    public int Family => Candidate.Family;
    public string Front => Candidate.Front.Label;
    public string Core => Candidate.Core.Label;
    public string Rear => Candidate.Rear.Label;
    public string Meniscus => Candidate.Meniscus.Label;
    public double R1 => Candidate.Radii.R1;
    public double R2 => Candidate.Radii.R2;
    public double R4 => Candidate.Radii.R4;
    public double R5 => Candidate.Radii.R5;
    public double R6 => Candidate.Radii.R6;
    public double BackFocus => Candidate.BackFocus;
    public double C => Candidate.C;
    public double Rms => Candidate.RmsWaves;
    public double DeltaCte => Candidate.DeltaCte;
}

/// <summary>
/// The search settings as the window edits them, the settings file they belong to, and the last result.
/// Every settings property marks the document changed, so New, Open and closing can ask before discarding.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    public static readonly string[] ModeNames =
        { "paper: r1 at the minimum of Q (Eq. 31), as the paper", "direct: r1 at the minimum of the wavefront" };
    public static readonly string[] FocusNames =
        { "poly: weighted least-squares focus over the band", "eq36: the paper's Eq. 36 (zero defocus at the primary)" };

    public static readonly string[] ArchitectureNames =
        { "2GS: two-glass symmetric (2012 paper)", "4GA-8: four glasses, asymmetric, air gap (2013 paper)" };
    public static readonly string[] GroupingNames =
        { "fine (slowest)", "normal", "coarse (fastest)" };
    private static readonly string[] GroupingKeys = { "fine", "normal", "coarse" };

    private static readonly Dictionary<string, string> FormatDescriptions = new()
    {
        ["zmx"] = "ZEMAX / OpticStudio (.zmx)", ["seq"] = "Code V (.seq)", ["len"] = "OSLO (.len)", ["otx"] = "Optalix (.otx)",
        ["json"] = "Optiland (.json)", ["lhlt"] = "LensHH-LT (.lhlt)",
    };

    public string CatalogFolder { get; } = SearchEngine.DefaultCatalogFolder;

    // ── Settings ────────────────────────────────────────────────────────────────
    [ObservableProperty] private double _focalLength;
    [ObservableProperty] private double _fNumber;
    [ObservableProperty] private double _minBackFocus;
    [ObservableProperty] private double _halfFieldAngle;
    [ObservableProperty] private bool _limitCte;
    [ObservableProperty] private double _maxCteDifference;
    [ObservableProperty] private int _primaryWavelength;
    [ObservableProperty] private bool _autoDesignWavelength;
    [ObservableProperty] private double _designWavelength;
    [ObservableProperty] private bool _includeObsolete;
    [ObservableProperty] private int _modeIndex;
    [ObservableProperty] private int _focusIndex;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Is4GA8))] private int _architectureIndex;
    [ObservableProperty] private string _coreGlassesText = "";
    [ObservableProperty] private int _coreCount;
    [ObservableProperty] private int _groupingIndex;
    public bool Is4GA8 => ArchitectureIndex == 1;
    [ObservableProperty] private int _scanPoints;
    [ObservableProperty] private int _keepTop;
    [ObservableProperty] private int _exportCount;
    [ObservableProperty] private string _outputFolder = "";
    [ObservableProperty] private bool _optilandInstall;
    [ObservableProperty] private string _zemaxGlasscat = "";

    public ObservableCollection<WavelengthRow> Wavelengths { get; } = new();
    public ObservableCollection<CheckItem> Catalogs { get; } = new();
    public ObservableCollection<CheckItem> Formats { get; } = new();
    [ObservableProperty] private WavelengthRow? _selectedWavelength;

    private static readonly HashSet<string> SettingProperties = new()
    {
        nameof(FocalLength), nameof(FNumber), nameof(MinBackFocus), nameof(HalfFieldAngle), nameof(LimitCte), nameof(MaxCteDifference), nameof(PrimaryWavelength), nameof(AutoDesignWavelength),
        nameof(DesignWavelength), nameof(IncludeObsolete), nameof(ModeIndex), nameof(FocusIndex), nameof(ScanPoints),
        nameof(KeepTop), nameof(ExportCount), nameof(OutputFolder), nameof(OptilandInstall), nameof(ZemaxGlasscat),
        nameof(ArchitectureIndex), nameof(CoreGlassesText), nameof(CoreCount), nameof(GroupingIndex),
    };

    // ── Document ────────────────────────────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Title))] private string? _currentFile;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Title))] private bool _isDirty;
    public string Title => $"MonoSearch - {(CurrentFile is null ? "Untitled" : Path.GetFileName(CurrentFile))}{(IsDirty ? " *" : "")}";

    // ── Run state and results ───────────────────────────────────────────────────
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(RunCommand), nameof(CancelCommand))]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    private bool _isRunning;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _statusText = "Set the lens and band, then Run.";
    [ObservableProperty] private string _summaryText = "";
    public ObservableCollection<ResultRow> Results { get; } = new();
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanExport), nameof(Shows4GA8), nameof(CanUseCores))]
    private SearchResult? _lastResult;
    public ObservableCollection<Ga8Row> Results4 { get; } = new();
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanExport), nameof(Shows4GA8))] private Ga8Result? _lastResult4;
    /// <summary>The 4GA-8 table is shown (the last run was 4GA-8); otherwise the 2GS table.</summary>
    public bool Shows4GA8 => LastResult4 != null;
    public bool CanUseCores => LastResult is { Ranked.Count: > 0 };

    // ── Drawing of the selected design ──────────────────────────────────────────
    [ObservableProperty] private ResultRow? _selectedResult;
    [ObservableProperty] private Ga8Row? _selectedResult4;
    [ObservableProperty] private LensLayout? _selectedLayout;
    [ObservableProperty] private string _selectedText = "";

    partial void OnSelectedResultChanged(ResultRow? value) => UpdateDrawing();
    partial void OnSelectedResult4Changed(Ga8Row? value) => UpdateDrawing();

    // The field does not enter the search, so the drawing follows it without a new run.
    partial void OnHalfFieldAngleChanged(double value) => UpdateDrawing();

    private void UpdateDrawing()
    {
        MonoLens? lens = Shows4GA8
            ? (SelectedResult4?.Candidate is { } c4 ? MonoLens.From(c4) : null)
            : (SelectedResult?.Candidate is { } c2 ? MonoLens.From(c2) : null);
        if (lens is null || !(HalfFieldAngle >= 0 && HalfFieldAngle < 90))
        {
            SelectedLayout = null;
            SelectedText = lens is null ? "" : "Half field of view must be from 0 to below 90 degrees.";
            return;
        }
        var L = LensLayout.Build(lens, HalfFieldAngle);
        SelectedLayout = L;
        string design = Shows4GA8 && SelectedResult4?.Candidate is { } c
            ? $"Rank {c.Rank} (family {c.Family}): {c.Front.Id} / {c.Core.Id} / {c.Rear.Id} / {c.Meniscus.Id}.  "
              + $"R1 {c.Radii.R1:F3}, R2 {c.Radii.R2:F3}, R4 {c.Radii.R4:F3}, R5 {c.Radii.R5:F3}, R6 {c.Radii.R6:F3}, "
              + $"image radius {c.ImageRadius:F3}, back focus {c.BackFocus:F3}"
            : SelectedResult?.Candidate is { } d
                ? $"Rank {d.Rank}: shell {d.Outer.Id}, ball {d.Inner.Id}.  r1 {d.E.R1:F3}, r2 {d.E.R2:F3}, "
                  + $"image radius {d.ImageRadius:F3}, back focus {d.BackFocus:F3}"
                : "";
        SelectedText = design + $", stop diameter {2 * L.StopSemi:F3} mm.  "
                       + $"Rays at {L.Wavelength} um, fields {string.Join(", ", L.FieldAngles.Select(a => $"{a:0.##}"))} deg"
                       + (L.Failed > 0 ? $"; {L.Failed} ray(s) not traced" : "") + ".";
    }

    private bool _loading;
    private CancellationTokenSource? _cts;

    public MainViewModel()
    {
        Wavelengths.CollectionChanged += OnCollectionChanged;
        Catalogs.CollectionChanged += OnCollectionChanged;
        Formats.CollectionChanged += OnCollectionChanged;
        LoadFrom(new SearchSettings());
        IsDirty = false;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_loading && e.PropertyName != null && SettingProperties.Contains(e.PropertyName)) IsDirty = true;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (INotifyPropertyChanged item in e.NewItems ?? Array.Empty<object>()) item.PropertyChanged += OnItemChanged;
        foreach (INotifyPropertyChanged item in e.OldItems ?? Array.Empty<object>()) item.PropertyChanged -= OnItemChanged;
        if (!_loading) IsDirty = true;
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_loading) IsDirty = true;
    }

    // ── Settings <-> window ─────────────────────────────────────────────────────
    public void LoadFrom(SearchSettings s)
    {
        _loading = true;
        try
        {
            FocalLength = s.FocalLength;
            FNumber = s.FNumber;
            MinBackFocus = s.MinBackFocus;
            HalfFieldAngle = s.HalfFieldAngle;
            Wavelengths.Clear();
            foreach (var w in s.Wavelengths) Wavelengths.Add(new WavelengthRow { Micrometres = w.Micrometres, Weight = w.Weight });
            PrimaryWavelength = s.PrimaryWavelength;
            AutoDesignWavelength = s.DesignWavelength is null;
            DesignWavelength = s.DesignWavelength ?? SearchSettings.DLine;

            Catalogs.Clear();
            var available = SearchEngine.AvailableCatalogs(CatalogFolder);
            foreach (var c in available.Union(s.Catalogs, StringComparer.OrdinalIgnoreCase))
                Catalogs.Add(new CheckItem
                {
                    Name = c, Description = available.Contains(c, StringComparer.OrdinalIgnoreCase) ? c : $"{c} (not found)",
                    IsSelected = s.Catalogs.Contains(c, StringComparer.OrdinalIgnoreCase),
                });
            IncludeObsolete = s.IncludeObsolete;
            LimitCte = s.MaxCteDifference is not null;
            MaxCteDifference = s.MaxCteDifference ?? 1.5;
            ModeIndex = s.Mode == "direct" ? 1 : 0;
            FocusIndex = s.Focus == "eq36" ? 1 : 0;
            ArchitectureIndex = s.Architecture == "4GA-8" ? 1 : 0;
            CoreGlassesText = string.Join(", ", s.CoreGlasses);
            CoreCount = s.CoreCount;
            GroupingIndex = Math.Max(0, Array.IndexOf(GroupingKeys, s.GlassGrouping));
            ScanPoints = s.ScanPoints;
            KeepTop = s.KeepTop;

            ExportCount = s.ExportCount;
            Formats.Clear();
            foreach (var f in Exporters.AllFormats)
                Formats.Add(new CheckItem { Name = f, Description = FormatDescriptions[f], IsSelected = s.ExportFormats.Contains(f) });
            OutputFolder = s.OutputFolder;
            OptilandInstall = s.OptilandInstall;
            ZemaxGlasscat = s.ZemaxGlasscat ?? "";
        }
        finally { _loading = false; }
    }

    public SearchSettings ToSettings() => new()
    {
        FocalLength = FocalLength,
        FNumber = FNumber,
        MinBackFocus = MinBackFocus,
        HalfFieldAngle = HalfFieldAngle,
        Wavelengths = Wavelengths.Select(w => new WavelengthSetting(w.Micrometres, w.Weight)).ToList(),
        PrimaryWavelength = PrimaryWavelength,
        DesignWavelength = AutoDesignWavelength ? null : DesignWavelength,
        Catalogs = Catalogs.Where(c => c.IsSelected).Select(c => c.Name).ToList(),
        IncludeObsolete = IncludeObsolete,
        MaxCteDifference = LimitCte ? MaxCteDifference : null,
        Mode = ModeIndex == 1 ? "direct" : "paper",
        Focus = FocusIndex == 1 ? "eq36" : "poly",
        Architecture = ArchitectureIndex == 1 ? "4GA-8" : "2GS",
        CoreGlasses = CoreGlassesText.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
        CoreCount = CoreCount,
        GlassGrouping = GroupingKeys[Math.Clamp(GroupingIndex, 0, 2)],
        ScanPoints = ScanPoints,
        KeepTop = KeepTop,
        ExportCount = ExportCount,
        ExportFormats = Formats.Where(f => f.IsSelected).Select(f => f.Name).ToList(),
        OutputFolder = OutputFolder,
        OptilandInstall = OptilandInstall,
        ZemaxGlasscat = string.IsNullOrWhiteSpace(ZemaxGlasscat) ? null : ZemaxGlasscat,
    };

    // ── File commands (the window asks for paths and confirmations) ─────────────
    public void New()
    {
        LoadFrom(new SearchSettings());
        CurrentFile = null;
        IsDirty = false;
        ClearResults();
        StatusText = "New settings.";
    }

    public void Open(string path)
    {
        LoadFrom(SearchSettings.Load(path));
        CurrentFile = path;
        IsDirty = false;
        ClearResults();
        StatusText = $"Opened {path}";
    }

    public void SaveAs(string path)
    {
        ToSettings().Save(path);
        CurrentFile = path;
        IsDirty = false;
        StatusText = $"Saved {path}";
    }

    private void ClearResults()
    {
        Results.Clear();
        SelectedResult = null;
        LastResult = null;
        Results4.Clear();
        SelectedResult4 = null;
        LastResult4 = null;
        SummaryText = "";
        Progress = 0;
    }

    /// <summary>The output folder, a relative one taken from the settings file's folder (or Documents\MonoSearch).</summary>
    public string ResolvedOutputFolder()
    {
        string folder = string.IsNullOrWhiteSpace(OutputFolder) ? "results" : OutputFolder;
        if (Path.IsPathRooted(folder)) return folder;
        string baseDir = CurrentFile != null
            ? Path.GetDirectoryName(Path.GetFullPath(CurrentFile))!
            : Path.Combine(UserFolders.Documents, "MonoSearch");
        return Path.GetFullPath(Path.Combine(baseDir, folder));
    }

    // ── Wavelength table ────────────────────────────────────────────────────────
    [RelayCommand]
    private void AddWavelength()
    {
        if (Wavelengths.Count >= 24) { StatusText = "At most 24 wavelengths."; return; }
        double next = Wavelengths.Count == 0 ? 0.55 : Math.Round(Wavelengths.Max(w => w.Micrometres) + 0.05, 4);
        Wavelengths.Add(new WavelengthRow { Micrometres = next, Weight = 1.0 });
    }

    [RelayCommand]
    private void RemoveWavelength()
    {
        if (Wavelengths.Count <= 1) { StatusText = "At least one wavelength is needed."; return; }
        var row = SelectedWavelength ?? Wavelengths[^1];
        Wavelengths.Remove(row);
        if (PrimaryWavelength > Wavelengths.Count) PrimaryWavelength = Wavelengths.Count;
    }

    [RelayCommand]
    private void SortWavelengths()
    {
        var primary = PrimaryWavelength >= 1 && PrimaryWavelength <= Wavelengths.Count ? Wavelengths[PrimaryWavelength - 1] : null;
        var sorted = Wavelengths.OrderBy(w => w.Micrometres).ToList();
        Wavelengths.Clear();
        foreach (var w in sorted) Wavelengths.Add(w);
        if (primary != null) PrimaryWavelength = sorted.IndexOf(primary) + 1;
    }

    // ── Run, cancel, export ─────────────────────────────────────────────────────
    private bool CanRun() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task Run()
    {
        SearchSettings settings;
        try { settings = ToSettings(); settings.ToSpec(); }
        catch (ArgumentException ex) { StatusText = ex.Message; return; }

        ClearResults();
        IsRunning = true;
        StatusText = SummaryText = settings.Architecture == "4GA-8" ? "4GA-8: starting..." : "2GS: searching every glass pair (a few seconds)...";
        _cts = new CancellationTokenSource();
        var progress = new Progress<double>(p => Progress = Math.Max(Progress, p * 100));
        try
        {
            if (settings.Architecture == "4GA-8") { await Run4(settings, progress, _cts.Token); return; }
            var r = await Task.Run(() => SearchEngine.Run(settings, CatalogFolder, progress, _cts.Token));
            LastResult = r;
            foreach (var c in r.Ranked) Results.Add(new ResultRow { Candidate = c });
            SelectedResult = Results.FirstOrDefault();
            Progress = 100;
            SummaryText = $"{r.GlassesUsable} usable glasses of {r.GlassesRead}"
                          + (r.GlassesWithoutCte > 0 ? $" ({r.GlassesWithoutCte} without a CTE left out)" : "")
                          + (r.Settings.MaxCteDifference is { } dc ? $", shell/ball CTE difference <= {dc}" : "")
                          + $", {r.Pairs:N0} pairs, {r.ValidDesigns:N0} valid designs, "
                          + $"{r.Elapsed.TotalSeconds:F1} s. Design wavelength {r.Spec.DesignLambda} um"
                          + (r.Spec.MinBfl > 0 ? $"; r1 <= {r.Spec.R1Max:F3} mm for back focus >= {r.Spec.MinBfl} mm." : ".");
            if (r.Stopped)
                SummaryText = $"Stopped after {r.Fraction:P0} of the outer glasses: the table ranks the pairs searched so far. " + SummaryText;
            StatusText = r.Ranked.Count == 0 ? "No valid design. Relax the back focus or widen the catalogs."
                : $"{(r.Stopped ? "Stopped" : "Done")}: best {r.Ranked[0].Outer.Label} / {r.Ranked[0].Inner.Label}, RMS {r.Ranked[0].RmsWaves:F4} waves.";
        }
        catch (OperationCanceledException ex)
        {
            StatusText = SummaryText = ex.Message.StartsWith("Stopped") ? ex.Message + " Nothing to show." : "Stopped before any result. Nothing to show.";
            Progress = 0;
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or IOException) { StatusText = ex.Message; }
        catch (Exception ex)
        {
            // Anything else is a bug: report it in the window rather than let it close the program.
            var inner = ex is AggregateException ae ? ae.Flatten().InnerExceptions[0] : ex;
            StatusText = $"Search failed ({inner.GetType().Name}): {inner.Message}";
            Progress = 0;
        }
        finally
        {
            IsRunning = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private async Task Run4(SearchSettings settings, IProgress<double> progress, CancellationToken cancel)
    {
        StatusText = SummaryText = "4GA-8: " + (settings.CoreGlasses.Count > 0 ? "preparing..." : "finding the core with a 2GS search...");
        // What the search is doing, shown above the table and in the status bar while it runs.
        IProgress<string> log = new Progress<string>(m => { if (IsRunning) StatusText = SummaryText = "4GA-8: " + m; });
        var r = await Task.Run(() => Ga8Search.Run(settings, CatalogFolder, progress, cancel, log.Report));
        LastResult4 = r;
        foreach (var c in r.Ranked) Results4.Add(new Ga8Row { Candidate = c });
        SelectedResult4 = Results4.FirstOrDefault();
        Progress = 100;
        SummaryText = $"4GA-8: {r.GlassesUsable} usable glasses"
                      + (r.GlassesWithoutCte > 0 ? $" ({r.GlassesWithoutCte} without a CTE left out)" : "")
                      + (r.Settings.MaxCteDifference is { } dc ? $", cemented CTE difference <= {dc}" : "")
                      + $", {r.Screened:N0} combinations screened"
                      + (r.Unstartable > 0 ? $" ({r.Unstartable:N0} could not be started)" : "")
                      + $", {r.Elapsed.TotalMinutes:F1} min. "
                      + string.Join(" ", r.Baselines.Select(b => $"Best 2GS with core {b.Core.Id}: {b.Shell.Id}, RMS {b.RmsWaves:F4} waves."));
        if (r.Stopped)
            SummaryText = $"Stopped after {(double)r.Screened / r.Total:P0} of the screening ({r.Screened:N0} of {r.Total:N0} combinations): "
                          + "the table holds the best of those, refined and ranked. Run again for the full search. " + SummaryText;
        StatusText = r.Ranked.Count == 0 ? "No valid 4GA-8 design. Relax the back focus, the CTE limit or widen the catalogs."
            : $"{(r.Stopped ? "Stopped" : "Done")} after {r.Elapsed.TotalMinutes:F1} min: best {r.Ranked[0].Glasses}, RMS {r.Ranked[0].RmsWaves:F4} waves.";
    }

    /// <summary>Sets up a 4GA-8 search seeded with the ball glasses of the given 2GS designs (the paper's procedure).</summary>
    public void UseCores(IEnumerable<Candidate> designs)
    {
        var ids = designs.Select(c => c.Inner.Id).Distinct().ToList();
        if (ids.Count == 0) return;
        ArchitectureIndex = 1;
        CoreGlassesText = string.Join(", ", ids);
        StatusText = $"4GA-8 set up with core {CoreGlassesText}. Press Run.";
    }

    private bool CanCancel() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    public bool CanExport => !IsRunning && (LastResult is { Ranked.Count: > 0 } || LastResult4 is { Ranked.Count: > 0 });

    /// <summary>Writes the ranked CSV and the top N (Export count) in the chosen formats; returns the warnings.</summary>
    public Task<List<string>> ExportTop() => Shows4GA8
        ? ExportCandidates4(LastResult4!.Ranked.Take(Math.Max(0, ExportCount)).ToList())
        : ExportCandidates(LastResult!.Ranked.Take(Math.Max(0, ExportCount)).ToList());

    /// <summary>A file name for the results table: ranked_&lt;tag&gt;.csv.</summary>
    public string TableFileName => $"ranked_{(Shows4GA8 ? LastResult4?.Tag : LastResult?.Tag) ?? "results"}.csv";

    /// <summary>Writes the whole results table (every ranked design, in rank order) as CSV.</summary>
    public void SaveTableCsv(string path)
    {
        if (Shows4GA8 && LastResult4 is { } r4) Ga8Search.WriteCsv(r4, path);
        else if (LastResult is { } r2) SearchEngine.WriteCsv(r2, path);
        else return;
        int rows = Shows4GA8 ? Results4.Count : Results.Count;
        StatusText = $"Saved the table ({rows} designs) to {path}";
    }

    public async Task<List<string>> ExportCandidates4(List<Ga8Candidate> candidates)
    {
        var r = LastResult4;
        if (r is null) return new List<string>();
        var formats = Formats.Where(f => f.IsSelected).Select(f => f.Name).ToList();
        string outDir = ResolvedOutputFolder();
        string glasscat = string.IsNullOrWhiteSpace(ZemaxGlasscat) ? r.Settings.ResolvedZemaxGlasscat : ZemaxGlasscat;
        bool install = OptilandInstall;
        IsRunning = true;
        StatusText = "Exporting...";
        try
        {
            var warnings = await Task.Run(() =>
            {
                Ga8Search.WriteCsv(r, Path.Combine(outDir, $"ranked_{r.Tag}.csv"));
                return candidates.Count > 0 && formats.Count > 0
                    ? Ga8Search.Export(r, candidates, CatalogFolder, outDir, formats, install, glasscat)
                    : new List<string>();
            });
            StatusText = $"Wrote the ranked CSV and {candidates.Count} design(s) as {string.Join(", ", formats)} to {outDir}"
                         + (warnings.Count > 0 ? $" ({warnings.Count} warning(s))" : "");
            return warnings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusText = "Export failed: " + ex.Message;
            return new List<string> { ex.Message };
        }
        finally { IsRunning = false; }
    }

    public async Task<List<string>> ExportCandidates(List<Candidate> candidates)
    {
        var r = LastResult;
        if (r is null) return new List<string>();
        var formats = Formats.Where(f => f.IsSelected).Select(f => f.Name).ToList();
        string outDir = ResolvedOutputFolder();
        string glasscat = string.IsNullOrWhiteSpace(ZemaxGlasscat) ? r.Settings.ResolvedZemaxGlasscat : ZemaxGlasscat;
        bool install = OptilandInstall;
        IsRunning = true;
        StatusText = "Exporting...";
        try
        {
            var warnings = await Task.Run(() =>
            {
                string csv = SearchEngine.WriteCsv(r, Path.Combine(outDir, $"ranked_{r.Tag}.csv"));
                var w = candidates.Count > 0 && formats.Count > 0
                    ? SearchEngine.Export(r, candidates, CatalogFolder, outDir, formats, install, glasscat)
                    : new List<string>();
                return w;
            });
            StatusText = $"Wrote the ranked CSV and {candidates.Count} design(s) as {string.Join(", ", formats)} to {outDir}"
                         + (warnings.Count > 0 ? $" ({warnings.Count} warning(s))" : "");
            return warnings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusText = "Export failed: " + ex.Message;
            return new List<string> { ex.Message };
        }
        finally { IsRunning = false; }
    }
}
