using System;

namespace HSGalaxy.Core.Calibration
{
    /// <summary>
    /// Simple helpers for converting between device independent pixels (DIPs) and physical pixels
    /// given Per‑Monitor‑V2 scale factors. Kept in Core for testability without WPF dependencies.
    /// </summary>
    public static class DpiHelper
    {
        public static int ToPixels(double dip, double scale) => (int)Math.Round(dip * scale);
        public static double ToDips(int px, double scale) => px / Math.Max(scale, 0.0001);

        public static (int x, int y, int w, int h) RectDipsToPixels(double xDip, double yDip, double wDip, double hDip, double scaleX, double scaleY)
        {
            return (
                ToPixels(xDip, scaleX),
                ToPixels(yDip, scaleY),
                Math.Max(1, ToPixels(wDip, scaleX)),
                Math.Max(1, ToPixels(hDip, scaleY))
            );
        }

        public static (double x, double y, double w, double h) RectPixelsToDips(int xPx, int yPx, int wPx, int hPx, double scaleX, double scaleY)
        {
            return (
                ToDips(xPx, scaleX),
                ToDips(yPx, scaleY),
                Math.Max(1.0, ToDips(wPx, scaleX)),
                Math.Max(1.0, ToDips(hPx, scaleY))
            );
        }
    }
}

