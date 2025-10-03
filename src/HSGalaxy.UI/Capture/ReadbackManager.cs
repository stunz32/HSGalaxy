using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using HSGalaxy.Diagnostics;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace HSGalaxy.UI.Capture
{
    /// <summary>
    /// Triple-buffer GPU->CPU readback manager using D3D11 staging textures.
    /// - Uses a 3-texture ring to defer CPU Map by ≥2 frames, minimizing GPU stalls.
    /// - Copies source subrect into staging at (0,0) so row-aligned copy is straightforward.
    /// </summary>
    public sealed class ReadbackManager : IDisposable
    {
        private readonly ID3D11Device _device;
        private readonly ID3D11DeviceContext _context;
        private ID3D11Texture2D[] _staging = new ID3D11Texture2D[3];
        private int _index;
        private int _stagingW;
        private int _stagingH;
        private long _frame;

        public ReadbackManager(ID3D11Device device, ID3D11DeviceContext context)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Ensure the staging textures are allocated at or above the requested size.
        /// </summary>
        public void EnsureStaging(int width, int height)
        {
            width = Math.Max(width, 1);
            height = Math.Max(height, 1);
            if (_stagingW >= width && _stagingH >= height && _staging[0] != null)
                return;

            DisposeStaging();
            _stagingW = width;
            _stagingH = height;
            var desc = new Texture2DDescription
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CPUAccessFlags = CpuAccessFlags.Read,
                MiscFlags = ResourceOptionFlags.None
            };
            for (int i = 0; i < 3; i++)
            {
                _staging[i] = _device.CreateTexture2D(desc);
            }
            _index = 0;
            OverlayLogger.Log("Readback.Staging", $"Allocated 3x {_stagingW}x{_stagingH}");
        }

        /// <summary>
        /// Queue a GPU->CPU readback by copying source region into the next staging texture.
        /// Returns a handle that becomes readable once FrameAge &gt;= 2.
        /// </summary>
        public PendingReadback QueueReadback(ID3D11Texture2D source, Rectangle region)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (region.Width <= 0 || region.Height <= 0) throw new ArgumentException("Invalid region", nameof(region));
            EnsureStaging(region.Width, region.Height);

            int idx = _index;
            _index = (idx + 1) % 3;
            _frame++;

            var srcBox = new Box(region.Left, region.Top, 0, region.Right, region.Bottom, 1);
            _context.CopySubresourceRegion(_staging[idx], 0, 0, 0, 0, source, 0, srcBox);

            return new PendingReadback(idx, region.Width, region.Height, _frame);
        }

        /// <summary>
        /// Attempt to map and copy the CPU data for a pending readback into a tightly-packed byte[] (BGRA8).
        /// Returns false if the request is not yet old enough (needs ≥2 frames of delay).
        /// </summary>
        public bool TryGetReadbackData(PendingReadback pending, out byte[]? data, out int rowPitch)
        {
            data = null;
            rowPitch = 0;
            long age = _frame - pending.IssuedFrame;
            if (age < 2) return false;

            var tex = _staging[pending.Index];
            if (tex is null) return false;

            MappedSubresource box;
            try
            {
                box = _context.Map(tex, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            }
            catch (Exception ex)
            {
                OverlayLogger.Log("Readback.Map.Error", ex.Message);
                return false;
            }

            try
            {
                int srcPitch = (int)box.RowPitch;
                rowPitch = srcPitch;
                int copyPitch = pending.Width * 4; // BGRA8
                data = new byte[pending.Width * pending.Height * 4];
                unsafe
                {
                    byte* src = (byte*)box.DataPointer;
                    fixed (byte* dstBase = data)
                    {
                        byte* dst = dstBase;
                        for (int y = 0; y < pending.Height; y++)
                        {
                            Buffer.MemoryCopy(src + y * srcPitch, dst + y * copyPitch, copyPitch, copyPitch);
                        }
                    }
                }
                return true;
            }
            finally
            {
                _context.Unmap(tex, 0);
            }
        }

        public void AdvanceFrame() => _frame++;

        private void DisposeStaging()
        {
            for (int i = 0; i < _staging.Length; i++)
            {
                _staging[i]?.Dispose();
                _staging[i] = null!;
            }
        }

        public void Dispose()
        {
            DisposeStaging();
        }

        public readonly struct PendingReadback
        {
            public int Index { get; }
            public int Width { get; }
            public int Height { get; }
            public long IssuedFrame { get; }
            public PendingReadback(int index, int width, int height, long frame)
            {
                Index = index; Width = width; Height = height; IssuedFrame = frame;
            }
        }

        // Self-test helper to validate performance characteristics without external capture sources.
        public static ReadbackMetrics SelfTest(int frames = 100, int width = 640, int height = 360)
        {
            var metrics = new ReadbackMetrics();
            ID3D11Device? device = null;
            ID3D11DeviceContext? context = null;
            try
            {
                var fl = new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 };
                var hr = D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport, fl, out device, out context);
                if (hr.Failure || device is null || context is null)
                {
                    hr = D3D11.D3D11CreateDevice(null, DriverType.Warp, DeviceCreationFlags.BgraSupport, fl, out device, out context);
                }
                if (device is null || context is null)
                    throw new InvalidOperationException("Failed to create D3D11 device");

                using var src = device.CreateTexture2D(new Texture2DDescription
                {
                    Width = (uint)width,
                    Height = (uint)height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.RenderTarget,
                    CPUAccessFlags = CpuAccessFlags.None,
                    MiscFlags = ResourceOptionFlags.None
                });
                using var rtv = device.CreateRenderTargetView(src);

                var mgr = new ReadbackManager(device, context);
                var rnd = new Random(1234);
                var pending = new Queue<(PendingReadback pr, long t0)>();
                var latencies = new List<double>(frames);
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < frames; i++)
                {
                    // Clear RT with a varying color to simulate frame updates
                    float t = (float)(i % 60) / 60.0f;
                    var color = new Color4(t, 0.25f + 0.5f * (float)rnd.NextDouble(), 0.1f, 1.0f);
                    context.ClearRenderTargetView(rtv, color);

                    var pr = mgr.QueueReadback(src, new Rectangle(0, 0, width, height));
                    pending.Enqueue((pr, sw.ElapsedTicks));

                    // Try to flush oldest if ready
                    while (pending.Count > 0)
                    {
                        var (old, t0) = pending.Peek();
                        if (mgr.TryGetReadbackData(old, out var data, out _))
                        {
                            pending.Dequeue();
                            double ms = (sw.ElapsedTicks - t0) * 1000.0 / Stopwatch.Frequency;
                            latencies.Add(ms);
                        }
                        else break;
                    }

                    mgr.AdvanceFrame();
                }

                // Drain remaining
                while (pending.Count > 0)
                {
                    var (old, t0) = pending.Peek();
                    if (mgr.TryGetReadbackData(old, out var data, out _))
                    {
                        pending.Dequeue();
                        double ms = (sw.ElapsedTicks - t0) * 1000.0 / Stopwatch.Frequency;
                        latencies.Add(ms);
                    }
                    mgr.AdvanceFrame();
                }

                metrics.Frames = frames;
                metrics.AvgLatencyMs = latencies.Count > 0 ? Average(latencies) : 0;
                metrics.P95LatencyMs = latencies.Count > 0 ? Quantile(latencies, 0.95) : 0;
                metrics.MaxLatencyMs = latencies.Count > 0 ? Max(latencies) : 0;
                OverlayLogger.Log("Readback.SelfTest", $"Frames={frames}; Avg={metrics.AvgLatencyMs:F2}ms P95={metrics.P95LatencyMs:F2} Max={metrics.MaxLatencyMs:F2}");
                return metrics;
            }
            catch (Exception ex)
            {
                OverlayLogger.Log("Readback.SelfTest.Error", ex.Message);
                metrics.Error = ex.Message;
                return metrics;
            }
            finally
            {
                context?.Dispose();
                device?.Dispose();
            }
        }

        private static double Average(List<double> v)
        {
            double s = 0; for (int i = 0; i < v.Count; i++) s += v[i]; return s / Math.Max(1, v.Count);
        }
        private static double Quantile(List<double> v, double q)
        {
            var arr = v.ToArray(); Array.Sort(arr);
            double pos = (arr.Length - 1) * q; int lo = (int)Math.Floor(pos); int hi = (int)Math.Ceiling(pos);
            if (lo == hi) return arr[lo]; double w = pos - lo; return arr[lo] * (1 - w) + arr[hi] * w;
        }
        private static double Max(List<double> v)
        {
            double m = double.MinValue; foreach (var x in v) if (x > m) m = x; return m;
        }
    }

    public sealed class ReadbackMetrics
    {
        public int Frames { get; set; }
        public double AvgLatencyMs { get; set; }
        public double P95LatencyMs { get; set; }
        public double MaxLatencyMs { get; set; }
        public string? Error { get; set; }
        public override string ToString() => $"Frames={Frames} Avg={AvgLatencyMs:F2}ms P95={P95LatencyMs:F2}ms Max={MaxLatencyMs:F2}ms" + (string.IsNullOrEmpty(Error) ? string.Empty : $" Error={Error}");
    }
}
