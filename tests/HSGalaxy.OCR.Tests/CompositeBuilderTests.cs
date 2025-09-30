using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using HSGalaxy.Core.OCR;

namespace HSGalaxy.OCR.Tests;

public class CompositeBuilderTests
{
    [Fact]
    public void Composite_Has_Gutters_And_Separators_With_Correct_Offsets()
    {
        using var r1 = MakeSolid(100, 50, Color.Red);
        using var r2 = MakeSolid(120, 60, Color.Green);
        using var r3 = MakeSolid(140, 70, Color.Blue);

        var builder = new CompositeBuilder();
        using var comp = builder.BuildComposite(new[] { r1, r2, r3 });

        // Offsets
        Assert.Equal(new Rectangle(16, 16, 100, 50), comp.Offsets[0]);
        // x1 = G + w1 + G + Sep + G
        int x1 = 16 + 100 + 16 + 1 + 16;
        Assert.Equal(new Rectangle(x1, 16, 120, 60), comp.Offsets[1]);
        int x2 = x1 + 120 + 16 + 1 + 16;
        Assert.Equal(new Rectangle(x2, 16, 140, 70), comp.Offsets[2]);
    }

    [Fact]
    public async Task Pipeline_Maps_Lines_Back_To_Correct_ROI()
    {
        // Three ROIs
        var profile = new Core.Calibration.CalibrationProfile
        {
            Regions =
            {
                new Core.Calibration.Roi{ Id="r1", X=0, Y=0, Width=100, Height=50},
                new Core.Calibration.Roi{ Id="r2", X=0, Y=0, Width=120, Height=60},
                new Core.Calibration.Roi{ Id="r3", X=0, Y=0, Width=140, Height=70},
            }
        };

        // Capture returns solid bitmaps of requested size
        Bitmap Capture(Rectangle rect) => MakeSolid(rect.Width, rect.Height, Color.Black);

        // Compute expected centers using the same layout rules
        int G = CompositeBuilder.GutterSize; int S = CompositeBuilder.SeparatorWidth;
        var off0 = new Rectangle(G, G, 100, 50);
        var off1 = new Rectangle(G + 100 + G + S + G, G, 120, 60);
        var off2 = new Rectangle(off1.Right + G + S + G, G, 140, 70);

        var stub = new StubClient(new[]
        {
            // centers inside each ROI
            new Rectangle(off0.Left + 10, off0.Top + 10, 10, 10),
            new Rectangle(off1.Left + 10, off1.Top + 10, 10, 10),
            new Rectangle(off2.Left + 10, off2.Top + 10, 10, 10),
        });

        var pipeline = new OcrPipeline(stub);
        var result = await pipeline.RunOnceAsync(profile, r => Capture(new Rectangle(0,0,r.Width,r.Height)));

        Assert.Collection(result.Lines,
            l => Assert.Equal(0, l.RoiIndex),
            l => Assert.Equal(1, l.RoiIndex),
            l => Assert.Equal(2, l.RoiIndex));
    }

    private static Bitmap MakeSolid(int w, int h, Color c)
    {
        var bmp = new Bitmap(Math.Max(w,1), Math.Max(h,1));
        using var g = Graphics.FromImage(bmp);
        using var br = new SolidBrush(c);
        g.FillRectangle(br, 0, 0, bmp.Width, bmp.Height);
        return bmp;
    }

    private sealed class StubClient : IOcrClient
    {
        private readonly Rectangle[] _rects;
        public StubClient(Rectangle[] rects) { _rects = rects; }
        public string Name => "Stub";
        public Task<OcrResult> RecognizeAsync(byte[] pngImage, CancellationToken ct = default)
        {
            var res = new OcrResult { Source = Name };
            foreach (var r in _rects)
                res.Lines.Add(new OcrLine { X = r.X, Y = r.Y, Width = r.Width, Height = r.Height, Text = "x", Confidence = 1.0f });
            return Task.FromResult(res);
        }
    }
}

