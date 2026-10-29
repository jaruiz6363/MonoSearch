using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MonoSearch.App.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using MsgIcon = MsBox.Avalonia.Enums.Icon;

namespace MonoSearch.App.Views;

public partial class MainWindow : Window
{
    private bool _forceClose;

    private static readonly FilePickerFileType SettingsType =
        new("MonoSearch settings") { Patterns = new[] { "*" + SearchSettings.FileExtension } };
    private static readonly FilePickerFileType AllFiles = new("All files") { Patterns = new[] { "*" } };

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnWindowClosing;
        KeyDown += OnKeyDown;
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Cmd on macOS, as Mac users expect; Ctrl everywhere.
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || (OperatingSystem.IsMacOS() && e.KeyModifiers.HasFlag(KeyModifiers.Meta)),
             shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (e.Key == Key.F5 && Vm?.RunCommand.CanExecute(null) == true) { e.Handled = true; await Vm.RunCommand.ExecuteAsync(null); }
        else if (ctrl && e.Key == Key.N) { e.Handled = true; NewClick(this, new RoutedEventArgs()); }
        else if (ctrl && e.Key == Key.O) { e.Handled = true; OpenClick(this, new RoutedEventArgs()); }
        else if (ctrl && shift && e.Key == Key.S) { e.Handled = true; await SaveAs(); }
        else if (ctrl && e.Key == Key.S) { e.Handled = true; await Save(); }
    }

    // ── File ────────────────────────────────────────────────────────────────────
    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose || Vm is null) return;
        if (Vm.IsRunning) Vm.CancelCommand.Execute(null);
        if (!Vm.IsDirty) return;
        e.Cancel = true;
        if (!await ConfirmDiscard()) return;
        _forceClose = true;
        Close();
    }

    /// <summary>True when it is fine to replace the current settings: saved, or the user chose not to save.</summary>
    private async Task<bool> ConfirmDiscard()
    {
        if (Vm is null || !Vm.IsDirty) return true;
        var box = MessageBoxManager.GetMessageBoxStandard("Unsaved settings",
            "The settings have changed.\n\nSave them first?", ButtonEnum.YesNoCancel, MsgIcon.Warning);
        var result = await box.ShowWindowDialogAsync(this);
        if (result == ButtonResult.Cancel) return false;
        if (result == ButtonResult.Yes) return await Save();
        return true;
    }

    private async void NewClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || !await ConfirmDiscard()) return;
        Vm.New();
    }

    private async void OpenClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || !await ConfirmDiscard()) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open MonoSearch Settings",
            AllowMultiple = false,
            FileTypeFilter = new[] { SettingsType, AllFiles },
        });
        if (files.Count == 0) return;
        await OpenFile(files[0].Path.LocalPath);
    }

    /// <summary>Opens a settings file handed to the program: the command-line argument (a file double-clicked in
    /// Explorer or a Linux file manager), or a file macOS's Finder sends while the program runs.</summary>
    public async Task OpenPath(string path)
    {
        if (Vm is null || !await ConfirmDiscard()) return;
        await OpenFile(path);
    }

    private async Task OpenFile(string path)
    {
        try { Vm!.Open(path); }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            await Message("Open failed", $"{path} could not be read:\n\n{ex.Message}", MsgIcon.Error);
        }
    }

    private async void SaveClick(object? sender, RoutedEventArgs e) => await Save();

    private async void SaveAsClick(object? sender, RoutedEventArgs e) => await SaveAs();

    private async Task<bool> Save() =>
        Vm?.CurrentFile is { } path ? await SaveTo(path) : await SaveAs();

    private async Task<bool> SaveAs()
    {
        if (Vm is null) return false;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save MonoSearch Settings As",
            DefaultExtension = SearchSettings.FileExtension.TrimStart('.'),
            SuggestedFileName = Vm.CurrentFile is null ? "settings" + SearchSettings.FileExtension : Path.GetFileName(Vm.CurrentFile),
            FileTypeChoices = new[] { SettingsType, AllFiles },
        });
        return file != null && await SaveTo(file.Path.LocalPath);
    }

    private async Task<bool> SaveTo(string path)
    {
        try { Vm!.SaveAs(path); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Message("Save failed", $"{path} could not be written:\n\n{ex.Message}", MsgIcon.Error);
            return false;
        }
    }

    private void ExitClick(object? sender, RoutedEventArgs e) => Close();

    // ── Folders ─────────────────────────────────────────────────────────────────
    private async Task<string?> PickFolder(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    private async void BrowseOutputClick(object? sender, RoutedEventArgs e)
    {
        if (Vm != null && await PickFolder("Output Folder") is { } f) Vm.OutputFolder = f;
    }

    private async void BrowseGlasscatClick(object? sender, RoutedEventArgs e)
    {
        if (Vm != null && await PickFolder("OpticStudio Glasscat Folder") is { } f) Vm.ZemaxGlasscat = f;
    }

    private void OpenFolderClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        string dir = Vm.ResolvedOutputFolder();
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    // ── Export ──────────────────────────────────────────────────────────────────
    private async void ExportTopClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || !Vm.CanExport) return;
        await ShowWarnings(await Vm.ExportTop());
    }

    private async void ExportSelectedClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || !Vm.CanExport) return;
        if (Vm.Shows4GA8)
        {
            var rows4 = Results4Grid.SelectedItems.OfType<Ga8Row>().Select(r => r.Candidate).OrderBy(c => c.Rank).ToList();
            if (rows4.Count == 0) { await NothingSelected(); return; }
            await ShowWarnings(await Vm.ExportCandidates4(rows4));
            return;
        }
        var rows = ResultsGrid.SelectedItems.OfType<ResultRow>().Select(r => r.Candidate).OrderBy(c => c.Rank).ToList();
        if (rows.Count == 0) { await NothingSelected(); return; }
        await ShowWarnings(await Vm.ExportCandidates(rows));
    }

    private async void SaveTableClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || !Vm.CanExport) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Table as CSV",
            DefaultExtension = "csv",
            SuggestedFileName = Vm.TableFileName,
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(Vm.ResolvedOutputFolder()),
            FileTypeChoices = new[] { new FilePickerFileType("CSV (comma separated)") { Patterns = new[] { "*.csv" } }, AllFiles },
        });
        if (file is null) return;
        string path = file.Path.LocalPath;
        try { Vm.SaveTableCsv(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Message("Save failed", $"{path} could not be written:\n\n{ex.Message}\n\nIs it open in Excel?", MsgIcon.Error);
        }
    }

    private Task NothingSelected() =>
        Message("Export selected", "Select one or more designs in the table first (Ctrl or Shift to select several).", MsgIcon.Info);

    private async void UseCoresClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        var rows = ResultsGrid.SelectedItems.OfType<ResultRow>().Select(r => r.Candidate).OrderBy(c => c.Rank).ToList();
        if (rows.Count == 0)
        {
            await Message("Use cores for 4GA-8", "Select one or more 2GS designs first; their ball glasses become the 4GA-8 cores.", MsgIcon.Info);
            return;
        }
        Vm.UseCores(rows);
    }

    private async Task ShowWarnings(List<string> warnings)
    {
        if (warnings.Count == 0) return;
        string text = string.Join("\n\n", warnings.Take(15)) + (warnings.Count > 15 ? $"\n\n... and {warnings.Count - 15} more" : "");
        await Message("Export warnings", text, MsgIcon.Warning);
    }

    private async void AboutClick(object? sender, RoutedEventArgs e) =>
        await Message("About MonoSearch",
            $"MonoSearch {typeof(MainWindow).Assembly.GetName().Version?.ToString(3)}\n\n" +
            "Global glass search for two-glass monocentric lenses, after Stamenov, Agurok and Ford, " +
            "Appl. Opt. 51, 7648 (2012).\n\nGlass data: vendor catalogs (SCHOTT, OHARA, HOYA, SUMITA, CDGM) and MISC.AGF.",
            MsgIcon.Info);

    private Task Message(string title, string text, MsgIcon icon) =>
        MessageBoxManager.GetMessageBoxStandard(title, text, ButtonEnum.Ok, icon).ShowWindowDialogAsync(this);
}
