using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HSGalaxy.Core.OCR
{
    /// <summary>
    /// Simulated OCR for automated tests without network/engine dependency.
    /// Generates deterministic results from input size and seed.
    /// </summary>
    public sealed class SimulatedOcrClient : IOcrClient
    {
        public string Name => "SimulatedOCR";

        public Task<OcrResult> RecognizeAsync(byte[] pngImage, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            // Simulate work
            Thread.SpinWait(100_000);
            var rnd = new Random(pngImage.Length);
            var result = new OcrResult { Source = Name };
            int count = 3 + rnd.Next(0, 3);
            for (int i = 0; i < count; i++)
            {
                result.Lines.Add(new OcrLine
                {
                    RoiIndex = rnd.Next(0, 3),
                    Text = $"Card {rnd.Next(1, 200)}",
                    Confidence = (float)(0.8 + rnd.NextDouble() * 0.2),
                    X = rnd.Next(0, 200),
                    Y = rnd.Next(0, 50),
                    Width = rnd.Next(50, 200),
                    Height = 20
                });
            }
            sw.Stop();
            result.ElapsedMs = sw.Elapsed.TotalMilliseconds;
            return Task.FromResult(result);
        }
    }
}

