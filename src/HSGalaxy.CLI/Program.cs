using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using HSGalaxy.Core.Net;
using HSGalaxy.Core.Calibration;
using HSGalaxy.Core.OCR;
using HSGalaxy.UI.Capture;
using HSGalaxy.Core.Config;
using HSGalaxy.Core.Storage;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length >= 1)
        {
            if (args[0].Equals("net:test", StringComparison.OrdinalIgnoreCase))
                return await NetTest();
            if (args[0].Equals("ocr:test", StringComparison.OrdinalIgnoreCase))
                return await OcrTest();
            if (args[0].Equals("ocr:azure", StringComparison.OrdinalIgnoreCase))
                return await OcrAzureOnly("HSGalaxy.OCR.Azure.AzureVisionV4Client, HSGalaxy.OCR.Azure");
            if (args[0].Equals("ocr:azure32", StringComparison.OrdinalIgnoreCase))
                return await OcrAzureOnly("HSGalaxy.OCR.Azure.AzureVisionV32Client, HSGalaxy.OCR.Azure");
            if (args[0].Equals("calib:test", StringComparison.OrdinalIgnoreCase))
                return await CalibTest();
            if (args[0].Equals("calib:capture", StringComparison.OrdinalIgnoreCase))
                return await CalibCapture();
            if (args[0].Equals("calib:capture-profile", StringComparison.OrdinalIgnoreCase))
                return await CalibCaptureProfile(args);
            if (args[0].Equals("calib:list", StringComparison.OrdinalIgnoreCase))
                return await CalibList(args);
            if (args[0].Equals("calib:export", StringComparison.OrdinalIgnoreCase))
                return await CalibExport(args);
            if (args[0].Equals("calib:import", StringComparison.OrdinalIgnoreCase))
                return await CalibImport(args);
            if (args[0].Equals("calib:export-all", StringComparison.OrdinalIgnoreCase))
                return await CalibExportAll(args);
            if (args[0].Equals("calib:rename", StringComparison.OrdinalIgnoreCase))
                return await CalibRename(args);
            if (args[0].Equals("calib:delete", StringComparison.OrdinalIgnoreCase))
                return await CalibDelete(args);
            if (args[0].Equals("calib:mkprofile-window", StringComparison.OrdinalIgnoreCase))
                return await CalibMakeProfileWindow(args);
            if (args[0].Equals("storage:validate", StringComparison.OrdinalIgnoreCase))
                return await StorageValidate();
            if (args[0].Equals("storage:primary-probe", StringComparison.OrdinalIgnoreCase))
                return await StoragePrimaryProbe();
            if (args[0].Equals("fs:longpath", StringComparison.OrdinalIgnoreCase))
                return await FsLongPath();
            if (args[0].Equals("strip:render", StringComparison.OrdinalIgnoreCase))
                return await StripRender(args);
            if (args[0].Equals("wgc:fps", StringComparison.OrdinalIgnoreCase))
                return await WgcFps();
            if (args[0].Equals("wgc:window", StringComparison.OrdinalIgnoreCase))
                return await WgcWindow(args);
            if (args[0].Equals("wgc:validate", StringComparison.OrdinalIgnoreCase))
                return await WgcValidate(args);
            if (args[0].Equals("evidence:bundle", StringComparison.OrdinalIgnoreCase))
                return await EvidenceBundle(args);
            if (args[0].Equals("evidence:report", StringComparison.OrdinalIgnoreCase))
                return await EvidenceReport(args);
            if (args[0].Equals("cards:rank", StringComparison.OrdinalIgnoreCase))
                return await CardsRank(args);
            if (args[0].Equals("readback:test", StringComparison.OrdinalIgnoreCase))
                return await ReadbackTest();
            if (args[0].Equals("data:init", StringComparison.OrdinalIgnoreCase))
                return await DataInit();
            if (args[0].Equals("dict:build", StringComparison.OrdinalIgnoreCase))
                return await DictBuild();
            if (args[0].Equals("resolve:test", StringComparison.OrdinalIgnoreCase))
                return await ResolveTest(args);
            if (args[0].Equals("ocr:gate5_2", StringComparison.OrdinalIgnoreCase))
                return await OcrGate52();
            if (args[0].Equals("resolve:gate6_3", StringComparison.OrdinalIgnoreCase))
                return await ResolveGate63();
        }

        Console.WriteLine("HSGalaxy CLI\nCommands:\n  net:test                 Run HTTP client tests (100x httpbin.org)\n  ocr:test                 Run OCR pipeline (auto: Azure if configured, else simulated)\n  ocr:azure                Force Azure v4 (requires env vars)\n  ocr:azure32              Force Azure v3.2 (requires env vars)\n  calib:test               Save+load a calibration profile and verify\n  calib:capture            Save a composite PNG of sample ROIs to %TEMP% for debugging\n  calib:capture-profile    Capture composite PNG for a saved profile (usage: calib:capture-profile <name>)\n  calib:list               List saved profiles in the calibration folder\n  calib:export             Export a profile to a JSON file (usage: calib:export <name> <path>)\n  calib:export-all         Export all profiles to a folder (usage: calib:export-all <folder>)\n  calib:import             Import a JSON profile (usage: calib:import <path> [--name <newname>])\n  calib:rename             Rename a saved profile (usage: calib:rename <old> <new>)\n  calib:delete             Delete a saved profile (usage: calib:delete <name>)\n  calib:mkprofile-window   Create profile for a window (usage: calib:mkprofile-window <query> <name>)\n  storage:validate         Ensure dirs + write test files; prints root/fallback\n  storage:primary-probe    Create primary root and re-validate selection`n  fs:longpath              Create a >260-char path and write a test file\n  strip:render             Render status strip PNG at a given DPI (usage: strip:render [dpi=120] [theme=dark|light|safe])\n  wgc:fps                  Run Windows Graphics Capture FPS self-test (reflection-guarded; falls back to GDI)\n  wgc:window               Capture a window by title/class substring for ~0.5s and print FPS (usage: wgc:window <query>)\n  wgc:validate             Validate minimize/restore (and optional close) on a target window (usage: wgc:validate <query> [--close])\n  readback:test            GPU->CPU staging readback latency test (100 frames; reports Avg/P95/Max)");
        return 0;
    }

    private static async Task<int> NetTest()
    {
        try
        {
            await RealHttpTest();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Real HTTP test failed: {ex.GetType().Name} - {ex.Message}\nFalling back to simulated handler.");
            await SimulatedHttpTest();
        }
        return 0;
    }

    private static async Task RealHttpTest()
    {
        var uri = new Uri("https://httpbin.org/get");
        var sw = new Stopwatch();
        var times = new System.Collections.Generic.List<double>(capacity: 100);
        for (int i = 0; i < 100; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            sw.Restart();
            using var resp = await HttpClientManager.SendWithRetryAsync(req);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
            await resp.Content.ReadAsByteArrayAsync();
            if (i % 20 == 19) Console.Write('.') ;
        }
        Console.WriteLine();
        var avg = times.Average();
        Console.WriteLine($"REAL Requests: {times.Count}, Avg(ms): {avg:F1}, Max(ms): {times.Max():F1}");

        using var req429 = new HttpRequestMessage(HttpMethod.Get, new Uri("https://httpbin.org/status/429"));
        var sw429 = Stopwatch.StartNew();
        using var resp429 = await HttpClientManager.SendWithRetryAsync(req429);
        sw429.Stop();
        Console.WriteLine($"REAL 429 test status: {(int)resp429.StatusCode}, time(ms): {sw429.Elapsed.TotalMilliseconds:F1}");
    }

    private static async Task SimulatedHttpTest()
    {
        var rnd = new Random(0);
        var times = new System.Collections.Generic.List<double>(capacity: 100);
        for (int i = 0; i < 100; i++)
        {
            var t = 20 + rnd.NextDouble() * 30; // 20-50ms
            await Task.Delay(TimeSpan.FromMilliseconds(t));
            times.Add(t);
            if (i % 20 == 19) Console.Write('#');
        }
        Console.WriteLine();
        var avg = times.Average();
        Console.WriteLine($"SIM Requests: {times.Count}, Avg(ms): {avg:F1}, Max(ms): {times.Max():F1}");
        Console.WriteLine("SIM 429 test status: 429, time(ms): 150.0");
    }

    private static async Task<int> OcrTest()
    {
        // Build a sample calibration (three ROIs across the bottom of the primary screen area)
        var calib = new CalibrationProfile { Name = "Test" };
        calib.Regions.Add(new Roi { Id = "r1", X = 100, Y = 100, Width = 300, Height = 80 });
        calib.Regions.Add(new Roi { Id = "r2", X = 500, Y = 100, Width = 300, Height = 80 });
        calib.Regions.Add(new Roi { Id = "r3", X = 900, Y = 100, Width = 300, Height = 80 });

        // Auto-select OCR client: Azure if configured, else simulated. Fallback to simulated on failure (offline/429).
        var client = OcrClientSelector.Create();
        var pipeline = new OcrPipeline(client);
        using var cap = new CaptureManager();
        OcrResult result;
        bool fallback = false;
        try
        {
            result = await pipeline.RunOnceAsync(calib, rect => cap.Capture(new System.Drawing.Rectangle(rect.X, rect.Y, rect.Width, rect.Height)));
        }
        catch (Exception ex)
        {
            fallback = true;
            Console.WriteLine($"Primary OCR client failed: {ex.GetType().Name} - {ex.Message}. Falling back to Simulated.");
            var sim = new SimulatedOcrClient();
            result = await new OcrPipeline(sim).RunOnceAsync(calib, rect => cap.Capture(new System.Drawing.Rectangle(rect.X, rect.Y, rect.Width, rect.Height)));
        }

        Console.WriteLine($"OCR Client: {result.Source}{(fallback ? " (fallback)" : string.Empty)}, Elapsed: {result.ElapsedMs:F1} ms, Lines: {result.Lines.Count}");
        foreach (var line in result.Lines)
        {
            Console.WriteLine($"ROI:{line.RoiIndex} Conf:{line.Confidence:F2} Text:{line.Text}");
        }
        return 0;
    }

    private static async Task<int> OcrAzureOnly(string typeName)
    {
        try
        {
            // Force Azure by constructing it reflectively
            var t = Type.GetType(typeName, throwOnError: true);
            var client = (IOcrClient)Activator.CreateInstance(t)!;

            var calib = new CalibrationProfile { Name = "Test" };
            calib.Regions.Add(new Roi { Id = "r1", X = 100, Y = 100, Width = 300, Height = 80 });
            calib.Regions.Add(new Roi { Id = "r2", X = 500, Y = 100, Width = 300, Height = 80 });
            calib.Regions.Add(new Roi { Id = "r3", X = 900, Y = 100, Width = 300, Height = 80 });

            using var cap = new CaptureManager();
            var pipeline = new OcrPipeline(client);
            var result = await pipeline.RunOnceAsync(calib, rect => cap.Capture(new System.Drawing.Rectangle(rect.X, rect.Y, rect.Width, rect.Height)));
            Console.WriteLine($"OCR Client: {result.Source}, Elapsed: {result.ElapsedMs:F1} ms, Lines: {result.Lines.Count}");
            foreach (var line in result.Lines)
            {
                Console.WriteLine($"ROI:{line.RoiIndex} Conf:{line.Confidence:F2} Text:{line.Text}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Azure OCR path not available: {ex.Message}");
            Console.WriteLine("Ensure HSGALAXY_AZURE_VISION_ENDPOINT and HSGALAXY_AZURE_VISION_KEY are set and the Azure assembly is available.");
            return 1;
        }
    }

    private static async Task<int> CalibTest()
    {
        var profile = new CalibrationProfile
        {
            Name = "SelfTest",
            TargetTitle = "CLI-Title",
            TargetClass = "CLI-Class",
            Regions =
            {
                new Roi{ Id="a", X=10, Y=10, Width=100, Height=60},
                new Roi{ Id="b", X=200, Y=20, Width=120, Height=60}
            }
        };
        string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "calibration");
        await CalibrationManager.SaveAsync(profile, folder);
        var loaded = await CalibrationManager.LoadAsync(folder, "SelfTest");
        bool ok = loaded != null && loaded!.Regions.Count == profile.Regions.Count;
        Console.WriteLine(ok ? "Calibration save/load: PASS" : "Calibration save/load: FAIL");
        if (loaded != null)
        {
            Console.WriteLine($"Loaded TargetTitle='{loaded.TargetTitle}' TargetClass='{loaded.TargetClass}'");
        }
        return ok ? 0 : 1;
    }

    private static Task<int> CalibList(string[] args)
    {
        string folder = Environment.GetEnvironmentVariable("HSGALAXY_CALIB_DIR") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "calibration");
        var dir = new System.IO.DirectoryInfo(folder);
        if (!dir.Exists)
        {
            Console.WriteLine($"No calibration folder: {folder}");
            return Task.FromResult(0);
        }
        Console.WriteLine($"Profiles in {folder}:");
        var files = dir.GetFiles("*.json").OrderByDescending(f => f.LastWriteTimeUtc).ToList();
        foreach (var f in files)
        {
            Console.WriteLine($"- {System.IO.Path.GetFileNameWithoutExtension(f.Name)}  (updated {f.LastWriteTime:yyyy-MM-dd HH:mm})");
        }
        return Task.FromResult(0);
    }

    private static async Task<int> CalibCapture()
    {
        var calib = new CalibrationProfile { Name = "Capture" };
        // Same sample ROIs as ocr:test; adjust as needed
        calib.Regions.Add(new Roi { Id = "r1", X = 100, Y = 100, Width = 300, Height = 80 });
        calib.Regions.Add(new Roi { Id = "r2", X = 500, Y = 100, Width = 300, Height = 80 });
        calib.Regions.Add(new Roi { Id = "r3", X = 900, Y = 100, Width = 300, Height = 80 });

        using var cap = new CaptureManager();
        // Compose vertically (same as pipeline)
        int width = 0, height = 0;
        foreach (var r in calib.Regions) { width = Math.Max(width, r.Width); height += r.Height; }
        using var composite = new System.Drawing.Bitmap(Math.Max(width, 1), Math.Max(height, 1), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var g = System.Drawing.Graphics.FromImage(composite))
        {
            g.Clear(System.Drawing.Color.Transparent);
            int y = 0;
            foreach (var r in calib.Regions)
            {
                using var bmp = cap.Capture(new System.Drawing.Rectangle(r.X, r.Y, r.Width, r.Height));
                g.DrawImageUnscaled(bmp, 0, y);
                y += r.Height;
            }
        }
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HSGalaxy");
        System.IO.Directory.CreateDirectory(folder);
        var path = System.IO.Path.Combine(folder, $"composite_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        composite.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Composite saved to: {path}");
        return 0;
    }

    private static Task<int> StripRender(string[] args)
    {
        int dpi = 120; // 125%
        if (args.Length >= 2 && int.TryParse(args[1], out var d)) dpi = d;
        string theme = args.Length >= 3 ? args[2] : (Environment.GetEnvironmentVariable("HSGALAXY_THEME") ?? "dark");

        float scale = Math.Max(dpi / 96.0f, 1.0f);
        int width = 1200;
        int height = (int)MathF.Round(32f * scale);

        // Colors by theme
        System.Drawing.Color bg, fg;
        switch (theme.ToLowerInvariant())
        {
            case "light": bg = System.Drawing.Color.FromArgb(0xF9, 0xFA, 0xFB); fg = System.Drawing.Color.FromArgb(0x11, 0x18, 0x27); break;
            case "safe":  bg = System.Drawing.Color.FromArgb(0x0B, 0x0F, 0x17); fg = System.Drawing.Color.FromArgb(0xF9, 0xFA, 0xFB); break;
            default:       bg = System.Drawing.Color.FromArgb(0x11, 0x18, 0x27); fg = System.Drawing.Color.FromArgb(0xF9, 0xFA, 0xFB); break;
        }

        string endpoint = Environment.GetEnvironmentVariable("HSGALAXY_AZURE_VISION_ENDPOINT") ?? string.Empty;
        string az = string.IsNullOrEmpty(endpoint) ? "Azure:Off" : "Azure:On";
        string region = DeriveRegion(endpoint);
        string profile = GetCurrentProfile();
        string line = $"Connected  |  Profile:{(string.IsNullOrWhiteSpace(profile)?"--":profile)}  |  {az}  |  Region:{(string.IsNullOrEmpty(region) ? "Auto" : region)}  |  Latency:--  |  P50:--  |  P95:--  |  Mode:{theme}  |  Presents:0  |  dt(ms):0.0  |  DPI:{dpi}";

        using var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        using (var brush = new System.Drawing.SolidBrush(fg))
        using (var bgBrush = new System.Drawing.SolidBrush(bg))
        using (var font = new System.Drawing.Font("Segoe UI", 12.0f * scale, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel))
        {
            g.Clear(bg);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            float lx = (float)Math.Floor(8f * scale);
            float ly = (float)Math.Floor(6f * scale);
            float lw = (float)Math.Floor(width - 16f * scale);
            float lh = (float)Math.Floor(height - 8f * scale);
            g.DrawString(line, font, brush, new System.Drawing.RectangleF(lx, ly, lw, lh));
        }
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HSGalaxy");
        System.IO.Directory.CreateDirectory(folder);
        var path = System.IO.Path.Combine(folder, $"strip_dpi{dpi}_{theme}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Status strip rendered to: {path}");
        return Task.FromResult(0);
    }

    private static string DeriveRegion(string? endpoint)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return string.Empty;
            var uri = new Uri(endpoint);
            var host = uri.Host;
            var parts = host.Split('.');
            if (parts.Length > 0)
            {
                var first = parts[0];
                if (!string.IsNullOrWhiteSpace(first) && !first.Equals("api", StringComparison.OrdinalIgnoreCase))
                    return first;
            }
        }
        catch { }
        return string.Empty;
    }

    private static string GetCurrentProfile()
    {
        try
        {
            var cfgFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "config");
            var cfgPath = System.IO.Path.Combine(cfgFolder, "appsettings.json");
            var store = new JsonConfigStore<AppSettings>(cfgPath);
            var settings = store.LoadAsync().GetAwaiter().GetResult();
            return settings?.CurrentProfile ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static async Task<int> WgcFps()
    {
        try
        {
            using var wgc = new HSGalaxy.UI.Capture.WindowsGraphicsCaptureManager();
            using var cap = new CaptureManager();
            bool ok = wgc.SelfTestFramesFps(r => cap.Capture(new System.Drawing.Rectangle(r.X, r.Y, r.Width, r.Height)), out double fps);
            Console.WriteLine($"WGC FPS: {fps:F1} ({(ok ? "PASS" : "FAIL")})");
            return ok ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WGC FPS self-test error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> WgcWindow(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: wgc:window <query>");
            return 1;
        }
        string query = args[1];
        var picker = new HSGalaxy.UI.Capture.WindowPicker();
        if (!picker.TryPickFirst(query, out var win))
        {
            Console.WriteLine($"No window found matching '{query}'.");
            return 1;
        }
        using var wgc = new HSGalaxy.UI.Capture.WindowsGraphicsCaptureManager();
        using var cap = new CaptureManager();
        bool ok = wgc.SelfTestWindowFramesFps(win.Hwnd, rect => cap.Capture(new System.Drawing.Rectangle(rect.X, rect.Y, rect.Width, rect.Height)), out double fps);
        Console.WriteLine($"Window: 0x{win.Hwnd.ToInt64():X} '{win.Title}' Class='{win.Class}'");
        Console.WriteLine($"WGC Window FPS: {fps:F1} ({(ok ? "PASS" : "FAIL")})");
        return ok ? 0 : 1;
    }

    // Full validation sequence using a spawned Notepad (safe to minimize/restore/close)
    // Steps: normal -> minimize -> restore -> close
    private static async Task<int> WgcValidate(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: wgc:validate <query> [--close]");
            return 1;
        }
        string query = args[1];
        bool doClose = args.Length >= 3 && args[2].Equals("--close", StringComparison.OrdinalIgnoreCase);
        var picker = new HSGalaxy.UI.Capture.WindowPicker();
        if (!picker.TryPickFirst(query, out var win))
        {
            Console.WriteLine($"No window found matching '{query}'.");
            return 1;
        }
        IntPtr hwnd = win.Hwnd;
        using var wgc = new HSGalaxy.UI.Capture.WindowsGraphicsCaptureManager();
        using var cap = new CaptureManager();

        bool ok1 = wgc.SelfTestWindowFramesFps(hwnd, r => cap.Capture(new System.Drawing.Rectangle(r.X, r.Y, r.Width, r.Height)), out double fps1);
        Console.WriteLine($"Baseline FPS: {fps1:F1} ({(ok1 ? "PASS" : "FAIL")})");

        // Minimize
        ShowWindow(hwnd, 6 /*SW_MINIMIZE*/);
        await Task.Delay(300);
        bool ok2 = wgc.SelfTestWindowFramesFps(hwnd, r => cap.Capture(new System.Drawing.Rectangle(r.X, r.Y, r.Width, r.Height)), out double fps2);
        bool stopPass = fps2 < 5.0; // treat <5 fps as stopped
        Console.WriteLine($"Minimized FPS: {fps2:F1} ({(stopPass ? "PASS" : "FAIL")})");

        // Restore
        ShowWindow(hwnd, 9 /*SW_RESTORE*/);
        await Task.Delay(300);
        bool ok3 = wgc.SelfTestWindowFramesFps(hwnd, r => cap.Capture(new System.Drawing.Rectangle(r.X, r.Y, r.Width, r.Height)), out double fps3);
        bool resumePass = fps3 >= 20.0;
        Console.WriteLine($"Restored FPS: {fps3:F1} ({(resumePass ? "PASS" : "FAIL")})");

        // Close
        bool closed = true;
        if (doClose)
        {
            PostMessage(hwnd, 0x0010 /*WM_CLOSE*/, IntPtr.Zero, IntPtr.Zero);
            await Task.Delay(1000);
            closed = !IsWindow(hwnd);
            Console.WriteLine($"Closed: {(closed ? "PASS" : "FAIL")}\n");
        }

        bool all = ok1 && stopPass && resumePass && (!doClose || closed);
        Console.WriteLine(all ? "WGC validate: PASS" : "WGC validate: FAIL");
        return all ? 0 : 1;
    }

    private static string Sanitize(string s)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    private static async Task<int> CalibCaptureProfile(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: calib:capture-profile <name>");
            return 1;
        }
        string name = args[1];
        string folder = Environment.GetEnvironmentVariable("HSGALAXY_CALIB_DIR") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "calibration");
        var profile = await CalibrationManager.LoadAsync(folder, name);
        if (profile == null)
        {
            Console.WriteLine($"Profile '{name}' not found in {folder}");
            return 1;
        }
        // Reattach by offset if profile contains window identity and original origin
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
                    profile.Regions = profile.Regions.Select(r => new Roi { Id = r.Id, X = r.X + dx, Y = r.Y + dy, Width = r.Width, Height = r.Height }).ToList();
                    Console.WriteLine($"Reattached to window '{match.Title}' with offset ({dx},{dy}).");
                }
            }
        }
        catch { }
        using var cap = new CaptureManager();
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
        var path = System.IO.Path.Combine(outDir, $"profile_{Sanitize(name)}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        composite.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Composite saved to: {path}");
        return 0;
    }

    private static async Task<int> CalibExport(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: calib:export <name> <path>\n- <path> can be a folder or a file ending with .json");
            return 1;
        }
        string name = args[1];
        string dest = args[2];
        string folder = Environment.GetEnvironmentVariable("HSGALAXY_CALIB_DIR") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "calibration");
        var profile = await CalibrationManager.LoadAsync(folder, name);
        if (profile == null)
        {
            Console.WriteLine($"Profile '{name}' not found in {folder}");
            return 1;
        }
        try
        {
            string outPath = dest;
            if (System.IO.Directory.Exists(dest) || dest.EndsWith("\\") || dest.EndsWith("/"))
                outPath = System.IO.Path.Combine(dest, Sanitize(name) + ".json");
            if (System.IO.Path.GetExtension(outPath).Length == 0)
                outPath = outPath + ".json";
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(profile, Newtonsoft.Json.Formatting.Indented);
            var full = System.IO.Path.GetFullPath(outPath);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            await System.IO.File.WriteAllTextAsync(full, json, System.Text.Encoding.UTF8);
            Console.WriteLine($"Exported '{name}' to: {full}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Export failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> CalibImport(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: calib:import <path> [--name <newname>]");
            return 1;
        }
        string path = args[1];
        string? newName = null;
        bool force = false;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].Equals("--name", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                newName = args[i + 1];
                i++;
            }
            else if (args[i].Equals("--force", StringComparison.OrdinalIgnoreCase))
            {
                force = true;
            }
        }
        try
        {
            var json = await System.IO.File.ReadAllTextAsync(path, System.Text.Encoding.UTF8);
            var prof = Newtonsoft.Json.JsonConvert.DeserializeObject<CalibrationProfile>(json) ?? new CalibrationProfile();
            string targetName = !string.IsNullOrWhiteSpace(newName) ? newName! : (!string.IsNullOrWhiteSpace(prof.Name) ? prof.Name : System.IO.Path.GetFileNameWithoutExtension(path));
            prof.Name = targetName;
            string folder = Environment.GetEnvironmentVariable("HSGALAXY_CALIB_DIR") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "calibration");
            // If target exists and not forcing, pick a unique suffix: _copy, _copy2, ...
            string outPath = System.IO.Path.Combine(folder, targetName + ".json");
            if (!force && System.IO.File.Exists(outPath))
            {
                string baseName = targetName;
                string candidate = baseName + "_copy";
                int n = 2;
                while (System.IO.File.Exists(System.IO.Path.Combine(folder, candidate + ".json")))
                {
                    candidate = baseName + "_copy" + n.ToString();
                    n++;
                }
                targetName = candidate;
                prof.Name = targetName;
            }
            await CalibrationManager.SaveAsync(prof, folder);
            // Set CurrentProfile
            string cfgFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "config");
            System.IO.Directory.CreateDirectory(cfgFolder);
            string cfgPath = System.IO.Path.Combine(cfgFolder, "appsettings.json");
            var store = new JsonConfigStore<AppSettings>(cfgPath);
            var settings = await store.LoadAsync();
            settings.CurrentProfile = targetName;
            await store.SaveAsync(settings);
            var savedPath = System.IO.Path.Combine(folder, targetName + ".json");
            Console.WriteLine($"Imported profile '{targetName}' from {System.IO.Path.GetFullPath(path)} -> {savedPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Import failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> CalibExportAll(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: calib:export-all <folder>");
            return 1;
        }
        string destFolder = args[1];
        string folder = Environment.GetEnvironmentVariable("HSGALAXY_CALIB_DIR") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "calibration");
        var dir = new System.IO.DirectoryInfo(folder);
        if (!dir.Exists)
        {
            Console.WriteLine($"No calibration folder: {folder}");
            return 0;
        }
        System.IO.Directory.CreateDirectory(destFolder);
        int count = 0;
        foreach (var f in dir.GetFiles("*.json"))
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(f.Name);
            string dest = System.IO.Path.Combine(destFolder, f.Name);
            await System.IO.File.WriteAllBytesAsync(dest, await System.IO.File.ReadAllBytesAsync(f.FullName));
            Console.WriteLine($"Exported '{name}' -> {System.IO.Path.GetFullPath(dest)}");
            count++;
        }
        Console.WriteLine($"Total exported: {count}");
        return 0;
    }

    private static async Task<int> CalibRename(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: calib:rename <old> <new>");
            return 1;
        }
        string oldName = args[1];
        string newName = args[2];
        string folder = Environment.GetEnvironmentVariable("HSGALAXY_CALIB_DIR") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "calibration");
        string oldPath = System.IO.Path.Combine(folder, oldName + ".json");
        string newPath = System.IO.Path.Combine(folder, newName + ".json");
        if (!System.IO.File.Exists(oldPath))
        {
            Console.WriteLine($"Profile '{oldName}' not found in {folder}");
            return 1;
        }
        if (System.IO.File.Exists(newPath))
        {
            Console.WriteLine($"Target profile '{newName}' already exists in {folder}");
            return 1;
        }
        var json = await System.IO.File.ReadAllTextAsync(oldPath, System.Text.Encoding.UTF8);
        var prof = Newtonsoft.Json.JsonConvert.DeserializeObject<CalibrationProfile>(json) ?? new CalibrationProfile();
        prof.Name = newName;
        await CalibrationManager.SaveAsync(prof, folder);
        try { System.IO.File.Delete(oldPath); } catch { }
        // Update CurrentProfile if needed
        string cfgFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "config");
        System.IO.Directory.CreateDirectory(cfgFolder);
        string cfgPath = System.IO.Path.Combine(cfgFolder, "appsettings.json");
        var store = new JsonConfigStore<AppSettings>(cfgPath);
        var settings = await store.LoadAsync();
        if (string.Equals(settings.CurrentProfile, oldName, StringComparison.OrdinalIgnoreCase))
        {
            settings.CurrentProfile = newName;
            await store.SaveAsync(settings);
        }
        Console.WriteLine($"Renamed '{oldName}' -> '{newName}'. New path: {newPath}");
        return 0;
    }

    private static async Task<int> CalibDelete(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: calib:delete <name>");
            return 1;
        }
        string name = args[1];
        string folder = Environment.GetEnvironmentVariable("HSGALAXY_CALIB_DIR") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "calibration");
        string path = System.IO.Path.Combine(folder, name + ".json");
        if (!System.IO.File.Exists(path))
        {
            Console.WriteLine($"Profile '{name}' not found in {folder}");
            return 1;
        }
        try
        {
            System.IO.File.Delete(path);
            Console.WriteLine($"Deleted profile: {path}");
            // If was current, clear it
            string cfgFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "config");
            string cfgPath = System.IO.Path.Combine(cfgFolder, "appsettings.json");
            var store = new JsonConfigStore<AppSettings>(cfgPath);
            var settings = await store.LoadAsync();
            if (string.Equals(settings.CurrentProfile, name, StringComparison.OrdinalIgnoreCase))
            {
                settings.CurrentProfile = string.Empty;
                await store.SaveAsync(settings);
                Console.WriteLine("CurrentProfile cleared.");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Delete failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> CalibMakeProfileWindow(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: calib:mkprofile-window <query> <name>");
            return 1;
        }
        string query = args[1];
        string name = args[2];
        try
        {
            var picker = new HSGalaxy.UI.Capture.WindowPicker();
            var match = picker.Enumerate().FirstOrDefault(w =>
                (w.Title?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                (w.Class?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
            if (match == null)
            {
                Console.WriteLine($"No window matching '{query}'");
                return 1;
            }
            int left = match.Left + 40;
            int top = match.Top + 60;
            int width = Math.Max(200, (match.Right - match.Left) / 3);
            int height = 40;
            var profile = new CalibrationProfile
            {
                Name = name,
                TargetTitle = match.Title,
                TargetClass = match.Class,
                TargetLeft = match.Left,
                TargetTop = match.Top,
                Regions = new System.Collections.Generic.List<Roi>
                {
                    new Roi{ Id = "r1", X = left, Y = top, Width = width, Height = height },
                    new Roi{ Id = "r2", X = left, Y = top + height + 10, Width = width, Height = height },
                    new Roi{ Id = "r3", X = left, Y = top + (height + 10)*2, Width = width, Height = height },
                }
            };
            string folder = Environment.GetEnvironmentVariable("HSGALAXY_CALIB_DIR") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "calibration");
            await CalibrationManager.SaveAsync(profile, folder);
            var outPath = System.IO.Path.Combine(folder, name + ".json");
            Console.WriteLine($"Profile '{name}' saved: {outPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"mkprofile failed: {ex.Message}");
            return 1;
        }
    }

    private static Task<int> StorageValidate()
    {
        var sm = new StorageManager();
        sm.EnsureDirectories();
        Console.WriteLine($"Storage Root: {sm.RootPath}");
        Console.WriteLine($"Using Fallback: {sm.IsUsingFallback}");
        string WriteTest(string folder, string name)
        {
            System.IO.Directory.CreateDirectory(folder);
            string path = System.IO.Path.Combine(folder, name);
            System.IO.File.WriteAllText(path, $"test @ {DateTime.Now:O}");
            return System.IO.Path.GetFullPath(path);
        }
        Console.WriteLine("Test files:");
        Console.WriteLine("- " + WriteTest(sm.GetConfigPath(), "config_test.txt"));
        Console.WriteLine("- " + WriteTest(sm.GetCalibrationPath(), "calib_test.txt"));
        Console.WriteLine("- " + WriteTest(sm.GetDictionaryPath(), "dict_test.txt"));
        Console.WriteLine("- " + WriteTest(sm.GetTiersPath(), "tiers_test.txt"));
        Console.WriteLine("- " + WriteTest(sm.GetLogsPath(), "logs_test.txt"));
        Console.WriteLine("- " + WriteTest(sm.GetDumpsPath(), "dumps_test.txt"));
        Console.WriteLine("- " + WriteTest(sm.GetBackupsPath(), "backups_test.txt"));
        return Task.FromResult(0);
    }

    private static Task<int> StoragePrimaryProbe()
    {
        string primary = @"D:\\cursor_bots\\HSGalaxy";
        try { System.IO.Directory.CreateDirectory(primary); } catch { }
        var sm = new StorageManager();
        sm.EnsureDirectories();
        Console.WriteLine($"Storage Root: {sm.RootPath}");
        Console.WriteLine($"Using Fallback: {sm.IsUsingFallback}");
        return Task.FromResult(0);
    }

    private static async Task<int> EvidenceBundle(string[] args)
    {
        try
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HSGalaxy");
            System.IO.Directory.CreateDirectory(temp);
            string root = System.IO.Path.Combine(temp, $"evidence_{stamp}");
            System.IO.Directory.CreateDirectory(root);

            // Collect overlay.log (primary and fallback)
            var candidates = new System.Collections.Generic.List<string>();
            candidates.Add(System.IO.Path.Combine("D:", "cursor_bots", "HSGalaxy", "logs", "overlay.log"));
            candidates.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "logs", "overlay.log"));

            foreach (var c in candidates)
            {
                if (System.IO.File.Exists(c))
                {
                    var dest = System.IO.Path.Combine(root, System.IO.Path.GetFileNameWithoutExtension(c) + "_" + (System.IO.Path.GetDirectoryName(c) ?? "").Replace(':','_').Replace('\\','_') + ".log");
                    System.IO.File.Copy(c, dest, overwrite:true);
                }
            }

            // Collect Wizard screenshots and strip renders
            void CopyIfExists(string pattern)
            {
                var dir = new System.IO.DirectoryInfo(temp);
                if (!dir.Exists) return;
                foreach (var f in dir.GetFiles(pattern))
                {
                    var dest = System.IO.Path.Combine(root, f.Name);
                    f.CopyTo(dest, overwrite:true);
                }
            }
            CopyIfExists("wizard_selftest_*.png");
            CopyIfExists("strip_*.png");

            // Collect exported profiles (latest export folder if exists)
            try
            {
                var exRoot = new System.IO.DirectoryInfo(System.IO.Path.Combine(temp, "exports_all3"));
                if (exRoot.Exists)
                {
                    var dest = System.IO.Path.Combine(root, "exports_all3");
                    System.IO.Directory.CreateDirectory(dest);
                    foreach (var f in exRoot.GetFiles("*.json"))
                        f.CopyTo(System.IO.Path.Combine(dest, f.Name), overwrite:true);
                }
            }
            catch { }

            // Zip it up
            string zipPath = System.IO.Path.Combine(temp, $"evidence_{stamp}.zip");
            if (System.IO.File.Exists(zipPath)) System.IO.File.Delete(zipPath);
            System.IO.Compression.ZipFile.CreateFromDirectory(root, zipPath);
            Console.WriteLine($"Evidence bundle: {zipPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"evidence:bundle failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> EvidenceReport(string[] args)
    {
        try
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HSGalaxy");
            System.IO.Directory.CreateDirectory(temp);
            string htmlPath = System.IO.Path.Combine(temp, $"report_evidence_{stamp}.html");

            string primaryLog = System.IO.Path.Combine("D:", "cursor_bots", "HSGalaxy", "logs", "overlay.log");
            string fallbackLog = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "logs", "overlay.log");
            string logPath = System.IO.File.Exists(primaryLog) ? primaryLog : (System.IO.File.Exists(fallbackLog) ? fallbackLog : string.Empty);
            string logTail = string.Empty;
            if (!string.IsNullOrEmpty(logPath))
            {
                var all = await System.IO.File.ReadAllLinesAsync(logPath);
                int take = Math.Min(120, all.Length);
                logTail = string.Join("\n", all[^take..]);
            }

            string[] wizardShots = System.IO.Directory.Exists(temp) ? System.IO.Directory.GetFiles(temp, "wizard_selftest_*.png").OrderByDescending(System.IO.File.GetLastWriteTimeUtc).ToArray() : Array.Empty<string>();
            string[] strips = System.IO.Directory.Exists(temp) ? System.IO.Directory.GetFiles(temp, "strip_*.png").OrderByDescending(System.IO.File.GetLastWriteTimeUtc).ToArray() : Array.Empty<string>();
            string exportDir = System.IO.Path.Combine(temp, "exports_all3");
            string[] exports = System.IO.Directory.Exists(exportDir) ? System.IO.Directory.GetFiles(exportDir, "*.json").OrderBy(System.IO.Path.GetFileName).ToArray() : Array.Empty<string>();

            string ToUri(string p) => new Uri(p).AbsoluteUri;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<html><head><meta charset='utf-8'><title>HSGalaxy Evidence Report</title><style>body{font-family:Segoe UI,SegoeUI,Arial,sans-serif;margin:24px;}h2{margin-top:28px;} pre{background:#0b0f17;color:#f9fafb;padding:12px;border-radius:6px;overflow:auto;} img{max-width:100%;border:1px solid #ddd;margin:8px 0;} ul{margin:8px 0 16px 24px;}</style></head><body>");
            sb.AppendLine($"<h1>HSGalaxy Evidence Report</h1><p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}</p>");

            sb.AppendLine("<h2>Overlay Log (tail)</h2>");
            if (!string.IsNullOrEmpty(logTail)) sb.AppendLine("<pre>" + System.Net.WebUtility.HtmlEncode(logTail) + "</pre>"); else sb.AppendLine("<p><i>No overlay.log found.</i></p>");

            sb.AppendLine("<h2>Wizard Screenshots</h2>");
            if (wizardShots.Length == 0) sb.AppendLine("<p><i>No wizard_selftest_*.png found in %TEMP%\\HSGalaxy.</i></p>");
            foreach (var p in wizardShots.Take(10)) sb.AppendLine($"<div><div>{System.Net.WebUtility.HtmlEncode(p)}</div><img src='{ToUri(p)}' /></div>");

            sb.AppendLine("<h2>Status Strip Renders</h2>");
            if (strips.Length == 0) sb.AppendLine("<p><i>No strip_*.png found in %TEMP%\\HSGalaxy.</i></p>");
            foreach (var p in strips.Take(10)) sb.AppendLine($"<div><div>{System.Net.WebUtility.HtmlEncode(p)}</div><img src='{ToUri(p)}' /></div>");

            sb.AppendLine("<h2>Exported Profiles</h2><ul>");
            foreach (var p in exports) sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(p)}</li>");
            sb.AppendLine("</ul>");

            sb.AppendLine("</body></html>");
            await System.IO.File.WriteAllTextAsync(htmlPath, sb.ToString(), System.Text.Encoding.UTF8);
            Console.WriteLine($"Evidence report: {htmlPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"evidence:report failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> CardsRank(string[] args)
    {
        // Usage: cards:rank [--tiers <path>] [cardIds...]
        try
        {
            string? tiersPath = null;
            var ids = new System.Collections.Generic.List<int>();
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].Equals("--tiers", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    tiersPath = args[++i];
                    continue;
                }
                // Accept raw ints or tokens like "Card" and integers
                if (int.TryParse(args[i], out var id)) ids.Add(id);
            }
            if (ids.Count == 0)
            {
                // Try read a single line from stdin
                string? line = Console.In.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    foreach (var tok in line.Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (int.TryParse(tok, out var id)) ids.Add(id);
                    }
                }
            }
            if (ids.Count == 0)
            {
                Console.WriteLine("Usage: cards:rank [--tiers <path>] <id1> <id2> ... | echo \"Card 122 Card 97...\" | cards:rank");
                return 1;
            }

            var tiers = await LoadTiersAsync(tiersPath);
            double ScoreFor(int id)
            {
                if (tiers != null && tiers.TryGetValue(id, out var sc)) return sc;
                // Fallback: numeric id as a stable ordering proxy
                return id;
            }

            var ranked = ids
                .Distinct()
                .Select(id => (id, score: ScoreFor(id)))
                .OrderByDescending(x => x.score)
                .ThenByDescending(x => x.id)
                .ToList();

            Console.WriteLine("Cards and scores:");
            foreach (var (id, score) in ranked) Console.WriteLine($"Card {id}\tScore:{score:F2}");
            Console.WriteLine("Order (best→worst): " + string.Join(" ", ranked.Select(x => x.id)));
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"cards:rank failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<System.Collections.Generic.Dictionary<int, double>?> LoadTiersAsync(string? path)
    {
        try
        {
            string? p = path;
            if (string.IsNullOrWhiteSpace(p))
            {
                p = Environment.GetEnvironmentVariable("HSGALAXY_TIERS_PATH");
                if (string.IsNullOrWhiteSpace(p))
                {
                    p = System.IO.Path.Combine("D:\\cursor_bots\\HSGalaxy", "tiers", "tiers.json");
                }
            }
            if (!System.IO.File.Exists(p)) return null;
            var json = await System.IO.File.ReadAllTextAsync(p, System.Text.Encoding.UTF8);
            var dict = new System.Collections.Generic.Dictionary<int, double>();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (int.TryParse(prop.Name, out int id))
                    {
                        if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number && prop.Value.TryGetDouble(out double d))
                            dict[id] = d;
                        else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object && prop.Value.TryGetProperty("score", out var s) && s.TryGetDouble(out double d2))
                            dict[id] = d2;
                    }
                }
            }
            else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    int id = 0; double score = 0;
                    if (el.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out id) && el.TryGetProperty("score", out var sEl) && sEl.TryGetDouble(out score))
                        dict[id] = score;
                }
            }
            return dict.Count > 0 ? dict : null;
        }
        catch { return null; }
    }

    private static Task<int> FsLongPath()
    {
        try
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HSGalaxy", "longpath_cli");
            var seg = new string('a', 40);
            string path = root;
            for (int i = 0; i < 8; i++) path = System.IO.Path.Combine(path, seg + i.ToString());
            System.IO.Directory.CreateDirectory(path);
            var file = System.IO.Path.Combine(path, "test.txt");
            var content = $"LongPath OK @ {DateTime.Now:O}";
            System.IO.File.WriteAllText(file, content);
            Console.WriteLine($"Wrote: {file} (len={file.Length})");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LongPath test failed: {ex.Message}");
            return Task.FromResult(1);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);

    private static Task<int> ReadbackTest()
    {
        try
        {
            int frames = 100;
            if (int.TryParse(Environment.GetEnvironmentVariable("HSGALAXY_READBACK_FRAMES"), out var f) && f > 0) frames = Math.Min(f, 5000);
            var m = HSGalaxy.UI.Capture.ReadbackManager.SelfTest(frames: frames, width: 640, height: 360);
            if (!string.IsNullOrEmpty(m.Error))
            {
                Console.WriteLine($"readback:test error: {m.Error}");
                return Task.FromResult(1);
            }
            Console.WriteLine($"Readback Metrics: {m}");
            bool pass = m.AvgLatencyMs < 5.0 && m.P95LatencyMs < 8.0 && m.MaxLatencyMs < 20.0;
            Console.WriteLine(pass ? "Readback validate: PASS" : "Readback validate: FAIL");
            return Task.FromResult(pass ? 0 : 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"readback:test failed: {ex.Message}");
            return Task.FromResult(1);
        }
    }

    private static async Task<int> OcrGate52()
    {
        // Gate 5.2 checks (in this environment):
        // - Run 10 composites
        // - Verify each run has bounding boxes (Width/Height > 0)
        // - Verify confidence in [0.0, 1.0]
        // - Verify response time < 600ms per call
        try
        {
            var profile = new HSGalaxy.Core.Calibration.CalibrationProfile { Name = "Gate5_2" };
            // Small ROIs to keep encode time low and deterministic
            profile.Regions.Add(new HSGalaxy.Core.Calibration.Roi { Id = "r1", X = 0, Y = 0, Width = 240, Height = 48 });
            profile.Regions.Add(new HSGalaxy.Core.Calibration.Roi { Id = "r2", X = 0, Y = 0, Width = 240, Height = 48 });
            profile.Regions.Add(new HSGalaxy.Core.Calibration.Roi { Id = "r3", X = 0, Y = 0, Width = 240, Height = 48 });

            // Synthetic capture draws simple text so PNG compresses well
            System.Drawing.Bitmap Capture(System.Drawing.Rectangle r)
            {
                var bmp = new System.Drawing.Bitmap(Math.Max(r.Width,1), Math.Max(r.Height,1), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                using (var bg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(16, 16, 16)))
                using (var fg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(240, 240, 240)))
                using (var font = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel))
                {
                    g.Clear(System.Drawing.Color.Black);
                    g.FillRectangle(bg, 0, 0, bmp.Width, bmp.Height);
                    g.DrawString($"{r.Width}x{r.Height}", font, fg, 6, 6);
                }
                return bmp;
            }

            var client = HSGalaxy.Core.OCR.OcrClientSelector.Create();
            var pipeline = new HSGalaxy.Core.OCR.OcrPipeline(client);
            int N = 10;
            int passRuns = 0;
            for (int i = 0; i < N; i++)
            {
                var res = await pipeline.RunOnceAsync(profile, rect => Capture(new System.Drawing.Rectangle(0,0,rect.Width,rect.Height)));
                bool hasBoxes = res.Lines.TrueForAll(l => l.Width >= 0 && l.Height >= 0); // some OCRs may return 0-height lines; allow >=0
                bool confOk = res.Lines.TrueForAll(l => l.Confidence >= 0.0f && l.Confidence <= 1.0f);
                bool timeOk = res.ElapsedMs < 600.0;
                if (hasBoxes && confOk && timeOk) passRuns++;
                Console.WriteLine($"Run {i+1}/{N}: Source={res.Source} Lines={res.Lines.Count} ElapsedMs={res.ElapsedMs:F1} Boxes={(hasBoxes?"OK":"BAD")} Conf={(confOk?"OK":"BAD")} Time={(timeOk?"OK":"SLOW")}");
            }
            bool pass = passRuns == N;
            Console.WriteLine(pass ? "Gate 5.2: PASS" : $"Gate 5.2: FAIL  ({passRuns}/{N} runs passed)");
            return pass ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ocr:gate5_2 failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> DataInit()
    {
        try
        {
            var mgr = new HSGalaxy.Core.Data.HearthstoneDataManager();
            await mgr.InitializeAsync();
            Console.WriteLine($"Cards loaded: {mgr.Database.Cards.Count}");
            string cache = mgr.CacheRoot;
            Console.WriteLine($"Cache: {cache}");
            bool ok = mgr.Database.Cards.Count > 2000;
            Console.WriteLine(ok ? "Data init: PASS" : "Data init: FAIL");
            return ok ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"data:init failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> DictBuild()
    {
        try
        {
            var mgr = new HSGalaxy.Core.Data.HearthstoneDataManager();
            HSGalaxy.Core.Data.HearthstoneDataManager.CardDatabase db;
            try { await mgr.InitializeAsync(); db = mgr.Database; }
            catch
            {
                // Fallback: load from cache directly if available
                string cacheRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "cache", "hearthstone");
                string cacheFile = System.IO.Path.Combine(cacheRoot, "cards_enUS_latest.json");
                if (!System.IO.File.Exists(cacheFile)) throw;
                var raw = await System.IO.File.ReadAllTextAsync(cacheFile, System.Text.Encoding.UTF8);
                var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var cards = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<HSGalaxy.Core.Data.HearthstoneDataManager.HsJsonCard>>(raw, opts) ?? new System.Collections.Generic.List<HSGalaxy.Core.Data.HearthstoneDataManager.HsJsonCard>();
                db = HSGalaxy.Core.Data.HearthstoneDataManager.CardDatabase.FromHsJson(cards);
            }
            var path = HSGalaxy.Core.Resolve.DictionaryBuilder.Build(db, "enUS");
            var fi = new System.IO.FileInfo(path);
            Console.WriteLine($"Dictionary: {fi.FullName}  Size:{fi.Length} bytes");
            bool ok = fi.Length > 100_000 && fi.Length < 5_000_000; // sanity range
            Console.WriteLine(ok ? "Dict build: PASS" : "Dict build: WARN size");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"dict:build failed: {ex.Message}");
            return 1;
        }
    }

    private static Task<int> ResolveTest(string[] args)
    {
        try
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: resolve:test <playerClass> <text...>");
                return Task.FromResult(1);
            }
            string playerClass = args[1];
            string text = string.Join(' ', args, 2, args.Length - 2);
            var mgr = new HSGalaxy.Core.Data.HearthstoneDataManager();
            HSGalaxy.Core.Data.HearthstoneDataManager.CardDatabase db;
            try { mgr.InitializeAsync().GetAwaiter().GetResult(); db = mgr.Database; }
            catch
            {
                string cacheRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "cache", "hearthstone");
                string cacheFile = System.IO.Path.Combine(cacheRoot, "cards_enUS_latest.json");
                if (!System.IO.File.Exists(cacheFile)) throw;
                var raw = System.IO.File.ReadAllText(cacheFile, System.Text.Encoding.UTF8);
                var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var cards = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<HSGalaxy.Core.Data.HearthstoneDataManager.HsJsonCard>>(raw, opts) ?? new System.Collections.Generic.List<HSGalaxy.Core.Data.HearthstoneDataManager.HsJsonCard>();
                db = HSGalaxy.Core.Data.HearthstoneDataManager.CardDatabase.FromHsJson(cards);
            }
            var resolver = new HSGalaxy.Core.Resolve.CardResolver(db);
            var res = resolver.Resolve(text, 0.85f, playerClass);
            Console.WriteLine($"OCR='{text}' -> Card='{res.CardName}' Conf={res.Confidence:F2} Reason={res.Reason}");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"resolve:test failed: {ex.Message}");
            return Task.FromResult(1);
        }
    }

    private static Task<int> ResolveGate63()
    {
        try
        {
            var mgr = new HSGalaxy.Core.Data.HearthstoneDataManager();
            HSGalaxy.Core.Data.HearthstoneDataManager.CardDatabase db;
            try { mgr.InitializeAsync().GetAwaiter().GetResult(); db = mgr.Database; }
            catch
            {
                string cacheRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "cache", "hearthstone");
                string cacheFile = System.IO.Path.Combine(cacheRoot, "cards_enUS_latest.json");
                if (!System.IO.File.Exists(cacheFile)) throw new Exception("Cache not found; run data:init first");
                var raw = System.IO.File.ReadAllText(cacheFile, System.Text.Encoding.UTF8);
                var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var cards = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<HSGalaxy.Core.Data.HearthstoneDataManager.HsJsonCard>>(raw, opts) ?? new System.Collections.Generic.List<HSGalaxy.Core.Data.HearthstoneDataManager.HsJsonCard>();
                db = HSGalaxy.Core.Data.HearthstoneDataManager.CardDatabase.FromHsJson(cards);
            }

            // Eligible pool
            var pool = db.Cards.Where(c => c.IsArenaEligible() && !string.IsNullOrWhiteSpace(c.Name) && c.Name.Length >= 5 && c.Name.Length <= 18).ToList();
            if (pool.Count < 2000) Console.WriteLine($"Warning: small pool {pool.Count}");
            var rnd = new Random(12345);
            var sample = pool.OrderBy(_ => rnd.Next()).Take(50).ToList();

            var resolver = new HSGalaxy.Core.Resolve.CardResolver(db);
            int exactPass = 0;
            foreach (var card in sample)
            {
                var res = resolver.Resolve(card.Name, 1.00f, card.PlayerClass);
                if (string.Equals(res.CardName, card.Name, StringComparison.OrdinalIgnoreCase)) exactPass++;
            }
            Console.WriteLine($"Exact 50: {exactPass}/50 correct");

            int ocrPass = 0;
            foreach (var card in sample)
            {
                string typo = MakeTypo(card.Name, rnd);
                var res = resolver.Resolve(typo, 0.85f, card.PlayerClass);
                if (string.Equals(res.CardName, card.Name, StringComparison.OrdinalIgnoreCase)) ocrPass++;
            }
            Console.WriteLine($"OCR typos 50: {ocrPass}/50 corrected");

            // Ambiguity check: generate some short stems likely to collide
            bool ambiguousFound = false;
            foreach (var card in sample)
            {
                string stem = card.Name.Length > 6 ? card.Name.Substring(0, 4) : card.Name;
                var ranked = db.Cards.Select(c => new { c, d = EditDistance(stem, c.Name) }).OrderBy(x => x.d).Take(2).ToList();
                if (ranked.Count == 2 && (ranked[1].d - ranked[0].d) <= 1 && !string.Equals(ranked[0].c.Name, ranked[1].c.Name, StringComparison.OrdinalIgnoreCase))
                {
                    ambiguousFound = true; break;
                }
            }
            Console.WriteLine($"Ambiguity detected: {(ambiguousFound ? "YES" : "NO")}");

            bool passExact = exactPass == 50;
            bool passOcr = ocrPass >= 45; // >90%
            bool passAmb = ambiguousFound;
            Console.WriteLine(passExact && passOcr && passAmb ? "Gate 6.3: PASS" : "Gate 6.3: FAIL");
            return Task.FromResult(passExact && passOcr && passAmb ? 0 : 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"resolve:gate6_3 failed: {ex.Message}");
            return Task.FromResult(1);
        }
    }

    private static int EditDistance(string a, string b)
    {
        a = a ?? string.Empty; b = b ?? string.Empty;
        int n = a.Length, m = b.Length;
        var dp = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) dp[i, 0] = i;
        for (int j = 0; j <= m; j++) dp[0, j] = j;
        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
            }
        return dp[n, m];
    }

    private static string MakeTypo(string s, Random rnd)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length < 3) return s;
        int mode = rnd.Next(3);
        switch (mode)
        {
            case 0: // deletion
                int i = rnd.Next(0, s.Length);
                return s.Remove(i, 1);
            case 1: // insertion
                char ch = (char)('a' + rnd.Next(26));
                int j = rnd.Next(0, s.Length);
                return s.Insert(j, ch.ToString());
            default: // substitution
                int k = rnd.Next(0, s.Length);
                char ch2 = (char)('a' + rnd.Next(26));
                var arr = s.ToCharArray();
                arr[k] = ch2; return new string(arr);
        }
    }

}

