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
            var rois = profile.Regions;
            // Capture ROI bitmaps
            var roiBitmaps = new System.Collections.Generic.List<Bitmap>(rois.Count);
            try
            {
                foreach (var r in rois)
                {
                    roiBitmaps.Add(capture(new Rectangle(r.X, r.Y, r.Width, r.Height)));
                }
                // Build composite horizontally with gutters/separators
                var builder = new CompositeBuilder();
                using var comp = builder.BuildComposite(roiBitmaps);
                var bytes = CompositeBuilder.EncodeComposite(comp.Composite);
                var result = await _client.RecognizeAsync(bytes, ct).ConfigureAwait(false);
                swTotal.Stop();
                result.Source = _client.Name;
                result.ElapsedMs = swTotal.Elapsed.TotalMilliseconds;

                // Map each OCR line back to the ROI index using center point against offsets
                foreach (var line in result.Lines)
                {
                    int cx = line.X + (line.Width > 0 ? line.Width / 2 : 0);
                    int cy = line.Y + (line.Height > 0 ? line.Height / 2 : 0);
                    foreach (var kv in comp.Offsets)
                    {
                        var rect = kv.Value;
                        if (cx >= rect.Left && cx < rect.Right && cy >= rect.Top && cy < rect.Bottom)
                        {
                            line.RoiIndex = kv.Key;
                            break;
                        }
                    }
                }
                return result;
            }
            finally
            {
                foreach (var b in roiBitmaps) b.Dispose();
            }
        }
    }
}
