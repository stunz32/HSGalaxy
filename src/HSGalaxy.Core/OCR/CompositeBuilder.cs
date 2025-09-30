using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace HSGalaxy.Core.OCR
{
    /// <summary>
    /// Combines ROI bitmaps into a single composite image with gutters and separators.
    /// Layout: [Gutter] ROI0 [Sep] [Gutter] ROI1 [Sep] [Gutter] ROI2 [Gutter]
    /// Returns the composite bitmap and an offset table for mapping OCR results back to ROIs.
    /// </summary>
    public sealed class CompositeBuilder
    {
        public const int GutterSize = 16;
        public const int SeparatorWidth = 1;

        public sealed class CompositeResult : IDisposable
        {
            public Bitmap Composite { get; }
            public Dictionary<int, Rectangle> Offsets { get; }
            public CompositeResult(Bitmap bmp, Dictionary<int, Rectangle> offsets)
            {
                Composite = bmp; Offsets = offsets;
            }
            public void Dispose() => Composite.Dispose();
        }

        public CompositeResult BuildComposite(IReadOnlyList<Bitmap> rois)
        {
            if (rois.Count == 0) throw new ArgumentException("No ROI images.");
            // Calculate size
            int totalWidth = GutterSize; // left gutter
            for (int i = 0; i < rois.Count; i++)
            {
                totalWidth += rois[i].Width + GutterSize; // right gutter for each
                if (i < rois.Count - 1) totalWidth += SeparatorWidth; // separator between rois
            }
            int maxHeight = 0;
            for (int i = 0; i < rois.Count; i++) maxHeight = Math.Max(maxHeight, rois[i].Height);
            int totalHeight = Math.Max(maxHeight + GutterSize * 2, 1);

            var bmp = new Bitmap(Math.Max(totalWidth, 1), totalHeight, PixelFormat.Format32bppPArgb);
            var offsets = new Dictionary<int, Rectangle>(rois.Count);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                int x = GutterSize;
                for (int i = 0; i < rois.Count; i++)
                {
                    var src = rois[i];
                    var rect = new Rectangle(x, GutterSize, src.Width, src.Height);
                    offsets[i] = rect;
                    g.DrawImageUnscaled(src, rect);
                    x += src.Width + GutterSize;
                    if (i < rois.Count - 1)
                    {
                        g.FillRectangle(Brushes.Black, new Rectangle(x, GutterSize, SeparatorWidth, src.Height));
                        x += SeparatorWidth + GutterSize;
                    }
                }
            }

            return new CompositeResult(bmp, offsets);
        }

        public static byte[] EncodeComposite(Bitmap composite)
        {
            // Try PNG first
            var png = SavePng(composite);
            if (png.Length <= 150_000) return png;
            // JPEG 90
            var jpg90 = SaveJpeg(composite, 90L);
            if (jpg90.Length <= 300_000) return jpg90;
            // JPEG 80
            return SaveJpeg(composite, 80L);
        }

        private static ImageCodecInfo GetJpeg()
        {
            foreach (var c in ImageCodecInfo.GetImageEncoders())
                if (c.FormatID == ImageFormat.Jpeg.Guid) return c;
            throw new NotSupportedException("JPEG encoder not found.");
        }

        private static byte[] SavePng(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        private static byte[] SaveJpeg(Bitmap bmp, long quality)
        {
            using var ms = new MemoryStream();
            var encoder = GetJpeg();
            using var ep = new EncoderParameters(1);
            ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            bmp.Save(ms, encoder, ep);
            return ms.ToArray();
        }
    }
}
