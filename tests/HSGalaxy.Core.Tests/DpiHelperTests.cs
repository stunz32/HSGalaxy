using HSGalaxy.Core.Calibration;
using Xunit;

namespace HSGalaxy.Core.Tests
{
    public class DpiHelperTests
    {
        [Theory]
        [InlineData(96, 1.0)]
        [InlineData(120, 1.25)]
        [InlineData(144, 1.5)]
        public void RoundTrip_Rect_Pixels_Dips(int dpi, double scale)
        {
            // Rect in DIPs
            double x = 10.0, y = 20.0, w = 300.0, h = 80.0;
            var (px, py, pw, ph) = DpiHelper.RectDipsToPixels(x, y, w, h, scale, scale);
            var (rx, ry, rw, rh) = DpiHelper.RectPixelsToDips(px, py, pw, ph, scale, scale);

            Assert.InRange(rx, x - 0.6, x + 0.6);
            Assert.InRange(ry, y - 0.6, y + 0.6);
            Assert.InRange(rw, w - 0.6, w + 0.6);
            Assert.InRange(rh, h - 0.6, h + 0.6);
        }
    }
}

