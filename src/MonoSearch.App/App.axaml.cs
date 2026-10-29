using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using MonoSearch.App.ViewModels;
using MonoSearch.App.Views;

namespace MonoSearch.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow { DataContext = new MainViewModel() };
            desktop.MainWindow = window;

            // A .monosearch file to open: on Windows and Linux it is the first argument (a file double-clicked in
            // Explorer or a file manager arrives that way); on macOS, Finder sends it as an activation instead.
            if (desktop.Args is [var path, ..] && File.Exists(path))
                window.Opened += async (_, _) => await window.OpenPath(path);
            if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
                activatable.Activated += async (_, e) =>
                {
                    if (e is FileActivatedEventArgs { Files: var files } && files.FirstOrDefault()?.TryGetLocalPath() is { } opened)
                        await window.OpenPath(opened);
                };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
