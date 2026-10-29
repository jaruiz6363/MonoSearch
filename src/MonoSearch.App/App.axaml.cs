using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            var vm = new MainViewModel();
            // A .monosearch file given on the command line (or by double-clicking one) opens at start.
            if (desktop.Args is { Length: > 0 } a && File.Exists(a[0])) vm.Open(a[0]);
            desktop.MainWindow = new MainWindow { DataContext = vm };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
