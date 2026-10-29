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

    private static readonly Dictionary<string, string> FormatDescriptions = new()
    {
        ["zmx"] = "ZEMAX / OpticStudio (.zmx)", ["len"] = "OSLO (.len)", ["otx"] = "Optalix (.otx)",
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
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanExport))] private SearchResult? _lastResult;

    // ── Drawing of the selected design ──────────────────────────────────────────
    [ObservableProperty] private ResultRow? _selectedResult;
    [ObservableProperty] private LensLayout? _selectedLayout;
    [ObservableProperty] private string _selectedText = "";

    partial void OnSelectedResultChanged(ResultRow? value) => UpdateDrawing();

    // The field does not enter the search, so the drawing follows it without a new run.
    partial void OnHalfFieldAngleChanged(double value) => UpdateDrawing();

    private void UpdateDrawing()
    {
        var c = SelectedResult?.Candidate;
        if (c is null || !(HalfFieldAngle >= 0 && HalfFieldAngle < 90))
        {
            SelectedLayout = null;
            SelectedText = c is null ? "" : "Half field of view must be from 0 to below 90 degrees.";
            return;
        }
        var L = LensLayout.Build(c, HalfFieldAngle);
        SelectedLayout = L;
        SelectedText = $"Rank {c.Rank}: shell {c.Outer.Id}, ball {c.Inner.Id}.  r1 {c.E.R1:F3}, r2 {c.E.R2:F3}, "
                       + $"image radius {c.ImageRadius:F3}, back focus {c.BackFocus:F3}, stop diameter {2 * L.StopSemi:F3} mm.  "
                       + $"Rays at {L.Wavelength} um, fields {string.Join(", ", L.FieldAngles.Select(a => $"{a:0.##}"))} deg"
                       + (L.FullShell ? "; the shells close into a complete spherical shell at this field" : "")
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
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MonoSearch");
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
        StatusText = "Searching...";
        _cts = new CancellationTokenSource();
        var progress = new Progress<double>(p => Progress = Math.Max(Progress, p * 100));
        try
        {
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
            StatusText = r.Ranked.Count == 0 ? "No valid design. Relax the back focus or widen the catalogs."
                : $"Done: best {r.Ranked[0].Outer.Label} / {r.Ranked[0].Inner.Label}, RMS {r.Ranked[0].RmsWaves:F4} waves.";
        }
        catch (OperationCanceledException) { StatusText = "Cancelled."; Progress = 0; }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or IOException) { StatusText = ex.Message; }
        finally
        {
            IsRunning = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private bool CanCancel() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    public bool CanExport => !IsRunning && LastResult is { Ranked.Count: > 0 };

    /// <summary>Writes the ranked CSV and the top N (Export count) in the chosen formats; returns the warnings.</summary>
    public Task<List<string>> ExportTop() => ExportCandidates(LastResult!.Ranked.Take(Math.Max(0, ExportCount)).ToList());

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
