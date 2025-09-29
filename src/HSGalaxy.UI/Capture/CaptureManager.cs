using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using HSGalaxy.Diagnostics;

namespace HSGalaxy.UI.Capture
{
    /// <summary>
    /// GDI-based capture manager (approximate). Provides screen-region capture.
    /// Suitable for automated tests; may be upgraded to Windows.Graphics.Capture later.
    /// </summary>
    public sealed class CaptureManager : IDisposable
    {
        private const int SRCCOPY = 0x00CC0020;

        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")]  private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]  private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")]  private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")]  private static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")]  private static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, int rop);

        public Bitmap Capture(Rectangle rect)
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
            OverlayLogger.Log("Capture.Frame", $"{rect.Width}x{rect.Height} at {rect.Left},{rect.Top}");
            return Image.FromHbitmap(hBmp);
        }

        public void Dispose() { }
    }
}

