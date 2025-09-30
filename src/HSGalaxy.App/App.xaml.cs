using System.Configuration;
using System.Data;
using System.Windows;
using HSGalaxy.UI.Native;
using HSGalaxy.UI.Rendering;
using System.Windows.Threading;
using HSGalaxy.Diagnostics;
using HSGalaxy.UI.Validation;
using HSGalaxy.App.UI;
using HSGalaxy.Core.Config;
using HSGalaxy.Core.Calibration;
using HSGalaxy.App.Calibration;
using Forms = System.Windows.Forms;

namespace HSGalaxy.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private HSGalaxy.UI.Native.NativeWindow? _overlay;
    private D3D11Renderer? _renderer;
    private DispatcherTimer? _presentTimer;
    private DispatcherTimer? _stressTimer;
    private DispatcherTimer? _stripTimer;
    private readonly System.Collections.Generic.List<double> _presentDurations = new();
    private JsonConfigStore<AppSettings>? _settingsStore;
    private AppSettings _settings = new AppSettings();
    private CalibrationWizardWindow? _wizard;
    private Forms.NotifyIcon? _tray;
    private Forms.ToolStripMenuItem? _profilesMenu;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Ensure app stays alive without a visible WPF window
        this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Safe Wizard mode: skip overlay entirely when explicitly requested
        var disableOverlay = string.Equals(Environment.GetEnvironmentVariable("HSGALAXY_DISABLE_OVERLAY"), "1", StringComparison.OrdinalIgnoreCase);
        if (disableOverlay)
        {
            OverlayLogger.Log("Startup", "SafeWizardMode: DISABLE_OVERLAY=1");
            TryInitSettings();
            OpenWizard();
            TryScheduleWizardSelfTest();
            // In this mode, keep the app alive by binding shutdown to wizard window
            if (_wizard != null)
            {
                this.ShutdownMode = ShutdownMode.OnMainWindowClose;
                this.MainWindow = _wizard;
            }
            return;
        }
        // Create click-through overlay window
        _overlay = new HSGalaxy.UI.Native.NativeWindow();
        _overlay.CreateOverlayWindow();
        _overlay.PositionOverlayWindow();
        OverlayLogger.Log("Overlay.WindowCreated", $"Affinity={_overlay.QueryDisplayAffinity()}");
        // Load settings before registering hotkeys
        TryInitSettings();
        _overlay.ThemeHotkeyPressed += (_, __) => ThemeManager.Cycle();
        _overlay.CaptureHotkeyPressed += (_, __) => { OverlayLogger.Log("Hotkey", "Capture pressed"); Dispatcher.InvokeAsync(async () => await CaptureCurrentProfileAsync()); };
        _overlay.WizardHotkeyPressed += (_, __) => { OverlayLogger.Log("Hotkey", "Wizard pressed"); Dispatcher.InvokeAsync(() => OpenWizard()); };
        ApplyHotkeysFromSettings();

        InitTrayIcon();
        OverlayLogger.Log("Tray", "Initialized");

        // Initialize Vortice D3D11 + DirectComposition renderer
        _renderer = new D3D11Renderer();
        _renderer.Initialize(_overlay.Handle);
        _renderer.ResizeToClient();
        _renderer.ClearAndPresent();

        _overlay.DpiChanged += (_, __) => _renderer?.ResizeToClient();

        // Ensure any prior debug tint is cleared (fully transparent overlay)
        _overlay.ClearTint();

        // Theme
        ThemeManager.InitializeFromEnv();
        ThemeManager.Changed += (_, __) =>
        {
            TryRenderStatusStrip();
        };

        // Settings already initialized above for hotkeys

        // Present scheduler: checks for dirty state ~60 FPS without busy waiting
        _presentTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _presentTimer.Tick += (_, __) => _renderer?.PresentIfDirty();
        _presentTimer.Start();

        // Optionally auto-open wizard (first run, or env flag)
        TryAutoOpenWizard();
        TryScheduleWizardSelfTest();
        TryScheduleAutoCaptureForTest();

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
                    // Mirror-view gate (approximate): capture the strip area and ensure no overlay pixels appear
                    try
                    {
                        // Assume 32 DIP height ~ 32 px at 96 DPI
                        var expected = new Vortice.Mathematics.Color4(0f, 0.6f, 0f, 1f);
                        var expectedRgb = System.Drawing.Color.FromArgb((int)(expected.R * 255), (int)(expected.G * 255), (int)(expected.B * 255));
                        GetOverlayClient(out int w, out int h);
                        var rect = new System.Drawing.Rectangle(0, h - 32, w, 32);
                        var passed = MirrorViewValidator.ValidateNoOverlayInCapture(rect, expectedRgb, requiredCleanFrames: 3, sampleStep: 6);
                        OverlayLogger.Log("SelfTest.MirrorView", passed ? "Passed" : "Failed");
                    }
                    catch (System.Exception ex)
                    {
                        OverlayLogger.Log("SelfTest.MirrorView.Error", ex.Message);
                    }
                    return;
                }
                flip2 = !flip2;
                var strip = new HSGalaxy.UI.Rendering.StatusStrip
                {
                    Background = new Vortice.Mathematics.Color4(0f, flip2 ? 0.6f : 0.2f, 0f, 1f),
                    HeightDip = 32f,
                    Status = "SelfTest",
                    Endpoint = "Auto",
                    Latency = "--",
                    P50 = "--",
                    P95 = "--",
                    Mode = ""
                };
                _renderer?.DrawStatusStrip(strip);
                _renderer?.PresentIfDirty();
            };
            stripTimer.Start();
        }

        // Status strip periodic update (instant apply + live metrics)
        _stripTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _stripTimer.Tick += (_, __) => TryRenderStatusStrip();
        _stripTimer.Start();

        // Optional: WGC frame-arrival self-test (HSGALAXY_WGC_TEST=1)
        var wgcTest = Environment.GetEnvironmentVariable("HSGALAXY_WGC_TEST");
        if (string.Equals(wgcTest, "1", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var wgc = new HSGalaxy.UI.Capture.WindowsGraphicsCaptureManager();
                using var cap = new HSGalaxy.UI.Capture.CaptureManager();
                bool ok = wgc.SelfTestFrames(r => cap.Capture(new System.Drawing.Rectangle(r.X, r.Y, r.Width, r.Height)));
                OverlayLogger.Log("SelfTest.WGC", ok ? "Passed" : "Failed");
            }
            catch (System.Exception ex)
            {
                OverlayLogger.Log("SelfTest.WGC.Error", ex.Message);
            }
        }

        // Optional: idle guard self-test (HSGALAXY_IDLE_TEST=1)
        var idleTest = Environment.GetEnvironmentVariable("HSGALAXY_IDLE_TEST");
        if (string.Equals(idleTest, "1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(stress, "1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(stressStrip, "1", StringComparison.OrdinalIgnoreCase))
        {
            var startCount = _renderer!.PresentCount;
            var idleTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            idleTimer.Tick += (_, __) =>
            {
                idleTimer.Stop();
                var endCount = _renderer!.PresentCount;
                var delta = endCount - startCount;
                OverlayLogger.Log("SelfTest.IdleNoPresent", delta == 0 ? "pass" : $"fail delta={delta}");
            };
            idleTimer.Start();
        }

        // Optional: auto-exit to enable non-interactive validation (HSGALAXY_EXIT_AFTER_TEST=1)
        var autoExit = Environment.GetEnvironmentVariable("HSGALAXY_EXIT_AFTER_TEST");
        if (string.Equals(autoExit, "1", StringComparison.OrdinalIgnoreCase))
        {
            var exitTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(6)
            };
            exitTimer.Tick += (_, __) =>
            {
                exitTimer.Stop();
                Shutdown();
            };
            exitTimer.Start();
        }
    }

    private void TryScheduleWizardSelfTest()
    {
        try
        {
            var selftest = Environment.GetEnvironmentVariable("HSGALAXY_WIZARD_SELFTEST");
            if (!string.Equals(selftest, "1", StringComparison.OrdinalIgnoreCase)) return;
            var t = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(1200) };
            t.Tick += (_, __) =>
            {
                t.Stop();
                bool found = false;
                try
                {
                    var picker = new HSGalaxy.UI.Capture.WindowPicker();
                    foreach (var w in picker.Enumerate())
                    {
                        if (w.Title?.IndexOf("Calibration Wizard", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            found = true; break;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    OverlayLogger.Log("Wizard.SelfTest.Error", ex.Message);
                }
                OverlayLogger.Log("Wizard.SelfTest", found ? "PASS" : "FAIL");
                var autoExit = Environment.GetEnvironmentVariable("HSGALAXY_EXIT_AFTER_TEST");
                if (string.Equals(autoExit, "1", StringComparison.OrdinalIgnoreCase))
                {
                    try { Shutdown(); } catch { }
                }
            };
            t.Start();
        }
        catch (System.Exception ex)
        {
            OverlayLogger.Log("Wizard.SelfTest.InitError", ex.Message);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray != null) { try { _tray.Visible = false; _tray.Dispose(); } catch { } _tray = null; }
        _presentTimer?.Stop();
        _stressTimer?.Stop();
        _renderer?.Dispose();
        _overlay?.Dispose();
        base.OnExit(e);
    }

    private void GetOverlayClient(out int w, out int h)
    {
        w = h = 0;
        if (_overlay is null) return;
        // Use the same helper as renderer (GetClientRect)
        var hwnd = _overlay.Handle;
        GetClientRect(hwnd, out RECT rc);
        w = rc.Right - rc.Left; h = rc.Bottom - rc.Top;
    }

    private void InitTrayIcon()
    {
        try
        {
            _tray = new Forms.NotifyIcon();
            _tray.Icon = System.Drawing.SystemIcons.Application;
            _tray.Text = "HSGalaxy";
            var menu = new Forms.ContextMenuStrip();
            _profilesMenu = new Forms.ToolStripMenuItem("Profiles");
            menu.Items.Add(_profilesMenu);
            menu.Items.Add("Open Calibration Wizard (Ctrl+Alt+C)", null, (_, __) => OpenWizard());
            menu.Items.Add("Capture Current Profile (Ctrl+Alt+P)", null, async (_, __) => await CaptureCurrentProfileAsync());
            menu.Items.Add("Hotkey Settings...", null, (_, __) => OpenHotkeySettings());
            menu.Items.Add("Exit", null, (_, __) => Shutdown());
            _tray.ContextMenuStrip = menu;
            menu.Opening += (_, __) => RebuildProfilesMenu();
            _tray.Visible = true;
        }
        catch (System.Exception ex)
        {
            OverlayLogger.Log("TrayIcon.Error", ex.Message);
        }
    }

    private void RebuildProfilesMenu()
    {
        try
        {
            if (_profilesMenu is null) return;
            _profilesMenu.DropDownItems.Clear();
            string folder = Environment.GetEnvironmentVariable("HSGALAXY_CALIB_DIR") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "calibration");
            var dir = new System.IO.DirectoryInfo(folder);
            if (!dir.Exists)
            {
                _profilesMenu.DropDownItems.Add("(no profiles)").Enabled = false;
                return;
            }
            foreach (var f in dir.GetFiles("*.json").OrderByDescending(x => x.LastWriteTimeUtc))
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(f.Name);
                var item = new Forms.ToolStripMenuItem(name) { Checked = string.Equals(_settings?.CurrentProfile, name, StringComparison.OrdinalIgnoreCase) };
                item.Click += async (_, __) => await SetCurrentProfileAsync(name);
                _profilesMenu.DropDownItems.Add(item);
            }
        }
        catch (System.Exception ex)
        {
            OverlayLogger.Log("Tray.Profiles.Error", ex.Message);
        }
    }

    private async System.Threading.Tasks.Task SetCurrentProfileAsync(string name)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            _settings.CurrentProfile = name;
            if (_settingsStore != null) await _settingsStore.SaveAsync(_settings);
            OverlayLogger.Log("Settings.Save", $"CurrentProfile='{name}'");
            RebuildProfilesMenu();
        }
        catch (System.Exception ex)
        {
            OverlayLogger.Log("Settings.Save.Error", ex.Message);
        }
    }

    private void TryRenderStatusStrip()
    {
        if (_renderer is null || _overlay is null) return;
        // Resolve theme colors
        var (bg, fg) = ThemeManager.GetColors(ThemeManager.Current);

        // Pull Azure endpoint and derive a region token if possible
        string? ep = Environment.GetEnvironmentVariable("HSGALAXY_AZURE_VISION_ENDPOINT");
        string endpoint = string.IsNullOrEmpty(ep) ? "Auto" : ep;
        string region = DeriveRegionFromEndpoint(ep);

        // Compute simple p50/p95 from last N present durations
        // Push the latest observed present duration if non-zero
        if (_renderer.LastPresentMs > 0)
        {
            _presentDurations.Add(_renderer.LastPresentMs);
            const int MaxSamples = 120;
            if (_presentDurations.Count > MaxSamples)
                _presentDurations.RemoveRange(0, _presentDurations.Count - MaxSamples);
        }
        string p50 = Quantile(_presentDurations, 0.5).ToString("F1");
        string p95 = Quantile(_presentDurations, 0.95).ToString("F1");
        string latency = _renderer.LastPresentMs > 0 ? _renderer.LastPresentMs.ToString("F1") : "--";

        var strip = new HSGalaxy.UI.Rendering.StatusStrip
        {
            Background = bg,
            Foreground = fg,
            HeightDip = 32f,
            Status = "Connected",
            Endpoint = string.IsNullOrEmpty(ep) ? "Azure:Off" : "Azure:On",
            Region = string.IsNullOrEmpty(region) ? "Auto" : region,
            Latency = latency,
            P50 = p50,
            P95 = p95,
            Mode = ThemeManager.Current.ToString()
        };
        _renderer.DrawStatusStrip(strip);
        _renderer.PresentIfDirty();
    }

    private void OpenWizard()
    {
        try
        {
            if (_wizard == null || !_wizard.IsVisible)
            {
                _overlay?.SetVisible(false);
                _wizard = new CalibrationWizardWindow();
                _wizard.Owner = null; // modeless
                _wizard.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                _wizard.Loaded += (_, __) =>
                {
                    try
                    {
                        OverlayLogger.Log("Wizard", $"Loaded @ ({_wizard.Left:F0},{_wizard.Top:F0}) Size=({_wizard.Width:F0}x{_wizard.Height:F0})");
                    }
                    catch { }
                };
                _wizard.Closed += (_, __) => { _wizard = null; _overlay?.SetVisible(true); };
                _wizard.Show();
                OverlayLogger.Log("Wizard", "Opened");
            }
            else
            {
                if (_wizard.WindowState == WindowState.Minimized) _wizard.WindowState = WindowState.Normal;
                _wizard.Activate();
                OverlayLogger.Log("Wizard", "Activated");
            }
        }
        catch (System.Exception ex)
        {
            OverlayLogger.Log("Wizard.Error", ex.Message);
        }
    }

    private void TryInitSettings()
    {
        // Load minimal app settings (current profile)
        try
        {
            var cfgFolder = GetConfigFolder();
            var cfgPath = System.IO.Path.Combine(cfgFolder, "appsettings.json");
            _settingsStore = new JsonConfigStore<AppSettings>(cfgPath);
            _settings = _settingsStore.LoadAsync().GetAwaiter().GetResult();
            OverlayLogger.Log("Settings.Load", $"CurrentProfile='{_settings.CurrentProfile}'");
        }
        catch (System.Exception ex)
        {
            OverlayLogger.Log("Settings.Load.Error", ex.Message);
        }
    }

    private void ApplyHotkeysFromSettings()
    {
        if (_overlay == null) return;
        try
        {
            bool okTheme = _overlay.RegisterThemeChord(_settings.ThemeHotkey);
            bool okCapture = _overlay.RegisterCaptureChord(_settings.CaptureHotkey);
            bool okWizard = _overlay.RegisterWizardChord(_settings.WizardHotkey);
            OverlayLogger.Log("Hotkey.Register", $"Theme='{_settings.ThemeHotkey}' => {(okTheme?"OK":"FAIL")}; Capture='{_settings.CaptureHotkey}' => {(okCapture?"OK":"FAIL")}; Wizard='{_settings.WizardHotkey}' => {(okWizard?"OK":"FAIL")}");
        }
        catch (System.Exception ex)
        {
            OverlayLogger.Log("Hotkey.Register.Error", ex.Message);
        }
    }

    private async void OpenHotkeySettings()
    {
        try
        {
            var wnd = new Settings.HotkeySettingsWindow(_settings);
            wnd.Owner = null;
            wnd.Saved += async (_, args) =>
            {
                // Update settings and persist
                _settings.ThemeHotkey = args.Theme;
                _settings.CaptureHotkey = args.Capture;
                _settings.WizardHotkey = args.Wizard;
                if (_settingsStore != null) await _settingsStore.SaveAsync(_settings);
                ApplyHotkeysFromSettings();
            };
            wnd.Show();
        }
        catch (System.Exception ex)
        {
            OverlayLogger.Log("Hotkey.UI.Error", ex.Message);
        }
    }

    private void TryAutoOpenWizard()
    {
        try
        {
            var env = Environment.GetEnvironmentVariable("HSGALAXY_SHOW_WIZARD");
            if (string.Equals(env, "1", StringComparison.OrdinalIgnoreCase))
            {
                OverlayLogger.Log("Wizard", "AutoOpen via env");
                OpenWizard();
                return;
            }
            // First-run heuristic: if no calibration profiles exist, open wizard
            string calibDir = Environment.GetEnvironmentVariable("HSGALAXY_CALIB_DIR") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "calibration");
            var dir = new System.IO.DirectoryInfo(calibDir);
            if (!dir.Exists || dir.GetFiles("*.json").Length == 0)
            {
                OverlayLogger.Log("Wizard", "AutoOpen (no profiles found)");
                OpenWizard();
            }
        }
        catch (System.Exception ex)
        {
            OverlayLogger.Log("Wizard.AutoOpen.Error", ex.Message);
        }
    }

    private void TryScheduleAutoCaptureForTest()
    {
        try
        {
            var env = Environment.GetEnvironmentVariable("HSGALAXY_TEST_CAPTURE_ON_START");
            if (string.Equals(env, "1", StringComparison.OrdinalIgnoreCase))
            {
                // Delay slightly to allow settings to load
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
                t.Tick += async (_, __) => { t.Stop(); await CaptureCurrentProfileAsync(); };
                t.Start();
            }
        }
        catch { }
    }

    private async System.Threading.Tasks.Task CaptureCurrentProfileAsync()
    {
        try
        {
            string name = _settings?.CurrentProfile ?? string.Empty;
            string folder = Environment.GetEnvironmentVariable("HSGALAXY_CALIB_DIR") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "calibration");
            if (string.IsNullOrWhiteSpace(name))
            {
                // Fallback to most-recent .json in calibration folder
                try
                {
                    var dir = new System.IO.DirectoryInfo(folder);
                    var mostRecent = dir.Exists ? dir.GetFiles("*.json").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault() : null;
                    if (mostRecent != null) name = System.IO.Path.GetFileNameWithoutExtension(mostRecent.Name);
                }
                catch { }
                if (string.IsNullOrWhiteSpace(name))
                {
                    OverlayLogger.Log("Calib.Capture", "No current profile set or found.");
                    return;
                }
            }
            var profile = await CalibrationManager.LoadAsync(folder, name);
            if (profile is null || profile.Regions.Count == 0)
            {
                OverlayLogger.Log("Calib.Capture", $"Profile '{name}' missing or empty.");
                return;
            }
            // Try reattach: if profile has identity + saved origin, offset ROIs by current window delta
            try
            {
                if ((!string.IsNullOrWhiteSpace(profile.TargetTitle) || !string.IsNullOrWhiteSpace(profile.TargetClass)) && profile.TargetLeft.HasValue && profile.TargetTop.HasValue)
                {
                    var picker = new HSGalaxy.UI.Capture.WindowPicker();
                    var match = picker.Enumerate().FirstOrDefault(w =>
                        (!string.IsNullOrWhiteSpace(profile.TargetTitle) && w.Title.IndexOf(profile.TargetTitle, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!string.IsNullOrWhiteSpace(profile.TargetClass) && w.Class.IndexOf(profile.TargetClass, StringComparison.OrdinalIgnoreCase) >= 0));
                    if (match != null)
                    {
                        int dx = match.Left - profile.TargetLeft.Value;
                        int dy = match.Top - profile.TargetTop.Value;
                        profile = new CalibrationProfile
                        {
                            Name = profile.Name,
                            TargetTitle = profile.TargetTitle,
                            TargetClass = profile.TargetClass,
                            TargetLeft = match.Left,
                            TargetTop = match.Top,
                            Regions = profile.Regions.Select(r => new Roi { Id = r.Id, X = r.X + dx, Y = r.Y + dy, Width = r.Width, Height = r.Height }).ToList()
                        };
                        OverlayLogger.Log("Calib.Attach", $"Reattached to '{match.Title}' offset=({dx},{dy}).");
                    }
                }
            }
            catch { }
            using var cap = new HSGalaxy.UI.Capture.CaptureManager();
            int width = 0, height = 0;
            foreach (var r in profile.Regions) { width = Math.Max(width, r.Width); height += r.Height; }
            using var composite = new System.Drawing.Bitmap(Math.Max(width, 1), Math.Max(height, 1), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = System.Drawing.Graphics.FromImage(composite))
            {
                g.Clear(System.Drawing.Color.Transparent);
                int y = 0;
                foreach (var r in profile.Regions)
                {
                    using var bmp = cap.Capture(new System.Drawing.Rectangle(r.X, r.Y, r.Width, r.Height));
                    g.DrawImageUnscaled(bmp, 0, y);
                    y += r.Height;
                }
            }
            var outDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HSGalaxy");
            System.IO.Directory.CreateDirectory(outDir);
            var path = System.IO.Path.Combine(outDir, $"hotkey_{Sanitize(name)}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            composite.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            OverlayLogger.Log("Calib.Capture", path);
            ToastService.Show($"Capture saved: {path}");
            // Optional auto-exit for deterministic testing
            var exit = Environment.GetEnvironmentVariable("HSGALAXY_EXIT_AFTER_TEST");
            if (string.Equals(exit, "1", StringComparison.OrdinalIgnoreCase))
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                t.Tick += (_, __) => { t.Stop(); try { this.Shutdown(); } catch { } };
                t.Start();
            }
        }
        catch (System.Exception ex)
        {
            OverlayLogger.Log("Calib.Capture.Error", ex.Message);
        }
    }

    private static string GetConfigFolder()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = System.IO.Path.Combine(local, "HSGalaxy", "config");
        System.IO.Directory.CreateDirectory(folder);
        return folder;
    }

    private static string Sanitize(string s)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    private static string DeriveRegionFromEndpoint(string? endpoint)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return string.Empty;
            var uri = new Uri(endpoint);
            var host = uri.Host; // e.g., eastus.api.cognitive.microsoft.com or myres.cognitiveservices.azure.com
            var parts = host.Split('.');
            if (parts.Length > 0)
            {
                // Heuristic: if host starts with region token like eastus, westeurope, etc., use it
                var first = parts[0];
                if (!string.IsNullOrWhiteSpace(first) && !first.Equals("api", StringComparison.OrdinalIgnoreCase))
                    return first;
            }
        }
        catch { }
        return string.Empty;
    }

    private static double Quantile(System.Collections.Generic.List<double> data, double q)
    {
        if (data.Count == 0) return 0;
        var arr = data.ToArray();
        Array.Sort(arr);
        double pos = (arr.Length - 1) * q;
        int lo = (int)Math.Floor(pos);
        int hi = (int)Math.Ceiling(pos);
        if (lo == hi) return arr[lo];
        double w = pos - lo;
        return arr[lo] * (1 - w) + arr[hi] * w;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetClientRect(nint hWnd, out RECT lpRect);
}



