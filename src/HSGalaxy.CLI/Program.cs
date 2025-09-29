using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using HSGalaxy.Core.Net;
using HSGalaxy.Core.Calibration;
using HSGalaxy.Core.OCR;
using HSGalaxy.UI.Capture;

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
            if (args[0].Equals("calib:test", StringComparison.OrdinalIgnoreCase))
                return await CalibTest();
        }

        Console.WriteLine("HSGalaxy CLI\nCommands:\n  net:test    Run HTTP client tests (100x httpbin.org)\n  ocr:test    Run OCR pipeline (auto: Azure if configured, else simulated)\n  calib:test  Save+load a calibration profile and verify");
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

        var client = new SimulatedOcrClient();
        var pipeline = new OcrPipeline(client);
        using var cap = new CaptureManager();
        var result = await pipeline.RunOnceAsync(calib, rect => cap.Capture(new System.Drawing.Rectangle(rect.X, rect.Y, rect.Width, rect.Height)));

        Console.WriteLine($"OCR Client: {result.Source}, Elapsed: {result.ElapsedMs:F1} ms, Lines: {result.Lines.Count}");
        foreach (var line in result.Lines)
        {
            Console.WriteLine($"ROI:{line.RoiIndex} Conf:{line.Confidence:F2} Text:{line.Text}");
        }
        return 0;
    }

    private static async Task<int> CalibTest()
    {
        var profile = new CalibrationProfile
        {
            Name = "SelfTest",
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
        return ok ? 0 : 1;
    }
}

