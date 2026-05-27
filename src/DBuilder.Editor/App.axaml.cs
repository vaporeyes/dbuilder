// ABOUTME: Avalonia Application subclass - wires the desktop lifetime to the main window.
// ABOUTME: Passes any command-line file argument through so the editor can open a WAD on launch.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace DBuilder.Editor;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string? openPath = desktop.Args is { Length: > 0 } a ? a[0] : null;
            desktop.MainWindow = new MainWindow(openPath);
            if (desktop is IActivatableLifetime activatable)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    activatable.TryLeaveBackground();
                    MacApplicationActivator.Activate();
                }, DispatcherPriority.Loaded);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
