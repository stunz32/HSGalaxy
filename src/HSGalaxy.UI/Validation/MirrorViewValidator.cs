using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using HSGalaxy.Diagnostics;

namespace HSGalaxy.UI.Validation
{
    /// <summary>
    /// Mirror view validator (approximate):
    /// Captures a screen region under the overlay via GDI BitBlt and searches for the overlay strip color.
    /// If no matching pixels are found across RequiredCleanFrames consecutive captures, it passes.
    /// Note: This uses GDI screen capture and serves as a practical approximation for Windows Graphics Capture.
    /// </summary>
    public static class MirrorViewValidator
    {
        private const int SRCCOPY = 0x00CC0020;

        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")]  private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]  private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")]  private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")]  private static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")]  private static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, int rop);

        public static bool ValidateNoOverlayInCapture(Rectangle captureRect, Color expectedOverlayColor, int requiredCleanFrames = 3, int sampleStep = 4)
        {
            try
            {
                int clean = 0;
                for (int i = 0; i < requiredCleanFrames * 2 && clean < requiredCleanFrames; i++)
                {
                    using var bmp = CaptureRegion(captureRect);
                    bool hasOverlay = ContainsColorApprox(bmp, expectedOverlayColor, tolerance: 24, step: sampleStep);
                    if (!hasOverlay) clean++; else clean = 0;
                }
                return clean >= requiredCleanFrames;
            }
            catch (Exception ex)
            {
                OverlayLogger.Log("MirrorView.Error", ex.Message);
                return false;
            }
        }

        private static Bitmap CaptureRegion(Rectangle rect)
        {
            var screenDc = GetDC(IntPtr.Zero);
            var memDc = CreateCompatibleDC(screenDc);
            var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            var hBmp = bmp.GetHbitmap();
            var old = SelectObject(memDc, hBmp);
            BitBlt(memDc, 0, 0, rect.Width, rect.Height, screenDc, rect.Left, rect.Top, SRCCOPY);
            SelectObject(memDc, old);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
            return Image.FromHbitmap(hBmp);
        }

        private static bool ContainsColorApprox(Bitmap bmp, Color color, int tolerance, int step)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    byte* p = (byte*)data.Scan0;
                    for (int y = 0; y < data.Height; y += step)
                    {
                        byte* row = p + y * data.Stride;
                        for (int x = 0; x < data.Width; x += step)
                        {
                            byte b = row[x * 4 + 0];
                            byte g = row[x * 4 + 1];
                            byte r = row[x * 4 + 2];
                            if (Math.Abs(r - color.R) <= tolerance && Math.Abs(g - color.G) <= tolerance && Math.Abs(b - color.B) <= tolerance)
                                return true;
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return false;
        }
    }
}

