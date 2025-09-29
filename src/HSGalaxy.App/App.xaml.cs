using System.Configuration;
using System.Data;
using System.Windows;
using HSGalaxy.UI.Native;

namespace HSGalaxy.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private NativeWindow? _overlay;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Ensure app stays alive without a visible WPF window
        this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        // Create click-through overlay window at startup for validation (Task 2.1)
        _overlay = new NativeWindow();
        _overlay.CreateOverlayWindow();
        _overlay.PositionOverlayWindow();

    }

    protected override void OnExit(ExitEventArgs e)
    {
        _overlay?.Dispose();
        base.OnExit(e);
    }
}

