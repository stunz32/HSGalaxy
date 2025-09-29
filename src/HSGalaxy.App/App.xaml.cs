using System.Configuration;
using System.Data;
using System.Windows;
using HSGalaxy.UI.Native;
using HSGalaxy.UI.Rendering;
using System.Windows.Threading;
using HSGalaxy.Diagnostics;

namespace HSGalaxy.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private NativeWindow? _overlay;
    private D3D11Renderer? _renderer;
    private DispatcherTimer? _presentTimer;
    private DispatcherTimer? _stressTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Ensure app stays alive without a visible WPF window
        this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        // Create click-through overlay window
        _overlay = new NativeWindow();
        _overlay.CreateOverlayWindow();
        _overlay.PositionOverlayWindow();
        OverlayLogger.Log("Overlay.WindowCreated", $"Affinity={_overlay.QueryDisplayAffinity()}");

        // Initialize Vortice D3D11 + DirectComposition renderer
        _renderer = new D3D11Renderer();
        _renderer.Initialize(_overlay.Handle);
        _renderer.ResizeToClient();
        _renderer.ClearAndPresent();

        _overlay.DpiChanged += (_, __) => _renderer?.ResizeToClient();

        // Ensure any prior debug tint is cleared (fully transparent overlay)
        _overlay.ClearTint();

        // Present scheduler: checks for dirty state ~60 FPS without busy waiting
        _presentTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _presentTimer.Tick += (_, __) => _renderer?.PresentIfDirty();
        _presentTimer.Start();

        // Optional: stress test via env var (HSGALAXY_STRESS_PRESENTS=1)
        var stress = Environment.GetEnvironmentVariable("HSGALAXY_STRESS_PRESENTS");
        if (string.Equals(stress, "1", StringComparison.OrdinalIgnoreCase))
        {
            OverlayLogger.Log("SelfTest.Stress", "begin");
            var started = DateTime.UtcNow;
            bool flip = false;
            _stressTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _stressTimer.Tick += (_, __) =>
            {
                // Flip clear color each tick for 3 seconds to drive presents
                if ((DateTime.UtcNow - started) > TimeSpan.FromSeconds(3))
                {
                    _stressTimer!.Stop();
                    OverlayLogger.Log("SelfTest.Stress", "end");
                    return;
                }
                flip = !flip;
                _renderer?.ClearAndPresent(new Vortice.Mathematics.Color4(flip ? 0.0f : 0.2f, 0.0f, 0.0f, 0.0f));
            };
            _stressTimer.Start();
        }

        // Optional: status strip stress (HSGALAXY_STRESS_STRIP=1)
        var stressStrip = Environment.GetEnvironmentVariable("HSGALAXY_STRESS_STRIP");
        if (string.Equals(stressStrip, "1", StringComparison.OrdinalIgnoreCase))
        {
            OverlayLogger.Log("SelfTest.Strip", "begin");
            var started2 = DateTime.UtcNow;
            bool flip2 = false;
            var stripTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            stripTimer.Tick += (_, __) =>
            {
                if ((DateTime.UtcNow - started2) > TimeSpan.FromSeconds(2))
                {
                    stripTimer.Stop();
                    OverlayLogger.Log("SelfTest.Strip", "end");
                    return;
                }
                flip2 = !flip2;
                _renderer?.DrawStatusStrip(new Vortice.Mathematics.Color4(0f, flip2 ? 0.6f : 0.2f, 0f, 1f));
                _renderer?.PresentIfDirty();
            };
            stripTimer.Start();
        }

    }

    protected override void OnExit(ExitEventArgs e)
    {
        _presentTimer?.Stop();
        _stressTimer?.Stop();
        _renderer?.Dispose();
        _overlay?.Dispose();
        base.OnExit(e);
    }
}

