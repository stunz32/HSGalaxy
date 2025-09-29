using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HSGalaxy.Core.Calibration;

namespace HSGalaxy.Core.OCR
{
    /// <summary>
    /// Orchestrates ROI capture and OCR call; composes composite PNG.
    /// </summary>
    public sealed class OcrPipeline
    {
        private readonly IOcrClient _client;

        public OcrPipeline(IOcrClient client)
        {
            _client = client;
        }

        public async Task<OcrResult> RunOnceAsync(CalibrationProfile profile, Func<Rectangle, Bitmap> capture, CancellationToken ct = default)
        {
            var swTotal = Stopwatch.StartNew();
            // Compose composite bitmap vertically
            var rois = profile.Regions;
            int width = 0, height = 0;
            foreach (var r in rois)
            {
                width = Math.Max(width, r.Width);
                height += r.Height;
            }
            using var composite = new Bitmap(Math.Max(width, 1), Math.Max(height, 1), PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(composite))
            {
                g.Clear(Color.Transparent);
                int y = 0; int idx = 0;
                foreach (var r in rois)
                {
                    using var roiBmp = capture(new Rectangle(r.X, r.Y, r.Width, r.Height));
                    g.DrawImageUnscaled(roiBmp, 0, y);
                    y += r.Height;
                    idx++;
                }
            }

            using var ms = new MemoryStream();
            composite.Save(ms, ImageFormat.Png);
            var bytes = ms.ToArray();
            var result = await _client.RecognizeAsync(bytes, ct).ConfigureAwait(false);
            swTotal.Stop();
            result.Source = _client.Name;
            result.ElapsedMs = swTotal.Elapsed.TotalMilliseconds;
            return result;
        }
    }
}

