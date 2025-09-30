using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.DXGI;
using Vortice.Mathematics;
using Vortice.DCommon;
using Vortice.Direct2D1;
#if HSGALAXY_HAS_DWRITE
using Vortice.DirectWrite;
#endif
using HSGalaxy.Diagnostics;

namespace HSGalaxy.UI.Rendering
{
    /// <summary>
    /// Vortice-based D3D11 + DirectComposition renderer with present-on-change policy.
    /// </summary>
    public sealed class D3D11Renderer : IDisposable
    {
        private ID3D11Device? _device;
        private ID3D11DeviceContext? _context;
        private ID3D11DeviceContext1? _context1;
        private IDXGIFactory2? _factory2;
        private IDXGISwapChain1? _swapChain;
        private IDXGIDevice? _dxgiDevice;

        private IDCompositionDevice? _dcompDevice;
        private IDCompositionTarget? _dcompTarget;
        private IDCompositionVisual? _rootVisual;

        // (Optional) D2D removed for now; using D3D11 ClearView for status strip

        private bool _isDirty;
        private IntPtr _hwnd;
        private int _presentCount;
        private int _bufferCount = 2;
        private readonly Stopwatch _presentSw = new Stopwatch();
        private ID3D11Texture2D? _textTexture;
        private int _textTexW, _textTexH;

        // Optional D2D/DirectWrite path (HSGALAXY_USE_DWRITE=1)
        private bool _useDWrite;
        private ID2D1Factory1? _d2dFactory;
        private ID2D1Device? _d2dDevice;
        private ID2D1DeviceContext? _d2dContext;
        #if HSGALAXY_HAS_DWRITE
        private IDWriteFactory? _dwFactory;
        private IDWriteTextFormat? _dwFormat;
        #endif
        private ID2D1SolidColorBrush? _d2dBrush;

        public int PresentCount => _presentCount;
        public double LastPresentMs { get; private set; }

        public void Initialize(IntPtr hwnd)
        {
            _hwnd = hwnd;
            CreateDevice();
            CreateSwapChainForComposition();
            SetupDirectComposition();
            TryInitD2DAndDWrite();
            _isDirty = true;
        }

        public void ClearAndPresent(Color4? color = null)
        {
            if (_device is null || _context is null || _swapChain is null) return;

            var c = color ?? new Color4(0, 0, 0, 0);
            using (var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0))
            using (var rtv = _device.CreateRenderTargetView(backBuffer))
            {
                _context.OMSetRenderTargets(rtv);
                _context.ClearRenderTargetView(rtv, c);
            }

            _isDirty = true;
            PresentIfDirty();
        }

        /// <summary>
        /// Mark the renderer dirty without rendering (triggers a present on next tick).
        /// </summary>
        public void MarkDirty() => _isDirty = true;

        public void PresentIfDirty()
        {
            if (!_isDirty || _swapChain is null) return;
            _presentSw.Restart();
            var hr = _swapChain.Present(1, PresentFlags.None);
            _presentSw.Stop();
            LastPresentMs = _presentSw.Elapsed.TotalMilliseconds;
            _presentCount++;
            OverlayLogger.Log("OverlayRender.Present", $"hr={hr.Code}; dtMs={_presentSw.Elapsed.TotalMilliseconds:F3}");
            MaybeEscalateBuffers();
            _isDirty = false;
        }

        public void ResizeToClient()
        {
            if (_swapChain is null) return;
            GetClientSize(_hwnd, out int w, out int h);
            if (w <= 0 || h <= 0) return;
            _swapChain.ResizeBuffers(0, (uint)w, (uint)h, Format.B8G8R8A8_UNorm, SwapChainFlags.None);
            _isDirty = true;
        }

        private void CreateDevice()
        {
            var flags = DeviceCreationFlags.BgraSupport;
#if DEBUG
            flags |= DeviceCreationFlags.Debug;
#endif
            D3D11.D3D11CreateDevice(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.None,
                new[] { Vortice.Direct3D.FeatureLevel.Level_11_1, Vortice.Direct3D.FeatureLevel.Level_11_0 },
                out _device,
                out _context).CheckError();

            _dxgiDevice = _device!.QueryInterface<IDXGIDevice>();
            using var adapter = _dxgiDevice.GetAdapter();
            _factory2 = adapter.GetParent<IDXGIFactory2>();
            _context1 = _context!.QueryInterfaceOrNull<ID3D11DeviceContext1>();
        }

        private void CreateSwapChainForComposition()
        {
            if (_factory2 is null || _device is null) throw new InvalidOperationException("DXGI factory/device not ready");
            GetClientSize(_hwnd, out int w, out int h);
            if (w <= 0 || h <= 0) { w = 1280; h = 720; }

            var desc = new SwapChainDescription1
            {
                Width = (uint)w,
                Height = (uint)h,
                Format = Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = (uint)_bufferCount,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipDiscard,
                AlphaMode = Vortice.DXGI.AlphaMode.Premultiplied,
                Flags = SwapChainFlags.None
            };

            _swapChain = _factory2.CreateSwapChainForComposition(_device, desc);
            OverlayLogger.Log("Swapchain.Created", $"BufferCount={(int)desc.BufferCount}; AlphaMode={desc.AlphaMode}; Format={desc.Format}");
        }

        private void MaybeEscalateBuffers()
        {
            if (_swapChain is null) return;
            // Simple heuristic: if present took longer than 20ms twice in a row and we are double-buffered, escalate to triple.
            if (_bufferCount >= 3) return;
            if (_presentSw.Elapsed.TotalMilliseconds > 20)
            {
                _bufferCount = 3;
                // Query current size and format to resize correctly (fall back to current size if not available)
                GetClientSize(_hwnd, out int w, out int h);
                if (w <= 0 || h <= 0) { w = 1280; h = 720; }
                _swapChain.ResizeBuffers((uint)_bufferCount, (uint)w, (uint)h, Format.B8G8R8A8_UNorm, _swapChain.Description1.Flags);
                OverlayLogger.Log("OverlayRender.BufferEscalated", "count=3");
            }
        }

        private void SetupDirectComposition()
        {
            if (_swapChain is null || _dxgiDevice is null) throw new InvalidOperationException("Swapchain/DXGI missing");

            DComp.DCompositionCreateDevice(_dxgiDevice, out _dcompDevice).CheckError();
            _dcompDevice!.CreateTargetForHwnd(_hwnd, true, out _dcompTarget).CheckError();
            _dcompDevice.CreateVisual(out _rootVisual).CheckError();
            _rootVisual!.SetContent(_swapChain).CheckError();
            _dcompTarget!.SetRoot(_rootVisual).CheckError();
            _dcompDevice.Commit().CheckError();
        }

        public void Dispose()
        {
            _textTexture?.Dispose();
            _d2dBrush?.Dispose();
            #if HSGALAXY_HAS_DWRITE
            _dwFormat?.Dispose();
            #endif
            _d2dContext?.Dispose();
            _d2dDevice?.Dispose();
            _d2dFactory?.Dispose();
            #if HSGALAXY_HAS_DWRITE
            _dwFactory?.Dispose();
            #endif
            _rootVisual?.Dispose();
            _dcompTarget?.Dispose();
            _dcompDevice?.Dispose();
            _swapChain?.Dispose();
            _context?.Dispose();
            _factory2?.Dispose();
            _dxgiDevice?.Dispose();
            _device?.Dispose();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        private static void GetClientSize(IntPtr hwnd, out int w, out int h)
        {
            if (GetClientRect(hwnd, out RECT rc)) { w = rc.Right - rc.Left; h = rc.Bottom - rc.Top; }
            else { w = h = 0; }
        }

        public void DrawStatusStrip(Color4 color, float heightDip = 32f)
        {
            if (_context1 is null || _swapChain is null || _device is null) return;
            GetClientSize(_hwnd, out int w, out int h);
            if (w <= 0 || h <= 0) return;
            int dpi = GetWindowDpi(_hwnd);
            float scale = Math.Max(dpi / 96.0f, 1.0f);
            int heightPx = (int)MathF.Round(heightDip * scale);
            // Clear the bottom band using ClearView on the current render target
            using var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
            using var rtv = _device.CreateRenderTargetView(backBuffer);
            var rect = new Vortice.RawRect(0, h - heightPx, w, h);
            _context1.ClearView(rtv, color, new[] { rect });

            // Compose a small metrics text line and blit it via a CPU-updated texture
            string az = Environment.GetEnvironmentVariable("HSGALAXY_AZURE_VISION_ENDPOINT") is string ep && ep.Length > 0 ? "Azure:On" : "Azure:Off";
            string text = $"{az}  |  Presents:{_presentCount}  |  dt(ms):{_presentSw.Elapsed.TotalMilliseconds:F1}  |  DPI:{dpi}";
            DrawTextOverlayEx(backBuffer, text, w, heightPx, destX: 0, destY: h - heightPx, scale);
            _isDirty = true;
        }

        /// <summary>
        /// Overload that accepts a StatusStrip model with theme and fields.
        /// </summary>
        public void DrawStatusStrip(StatusStrip strip)
        {
            if (_context1 is null || _swapChain is null || _device is null) return;
            GetClientSize(_hwnd, out int w, out int h);
            if (w <= 0 || h <= 0) return;
            int dpi = GetWindowDpi(_hwnd);
            float scale = Math.Max(dpi / 96.0f, 1.0f);
            int heightPx = (int)MathF.Round(strip.HeightDip * scale);
            using var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
            using var rtv = _device.CreateRenderTargetView(backBuffer);
            var rect = new Vortice.RawRect(0, h - heightPx, w, h);
            _context1.ClearView(rtv, strip.Background, new[] { rect });

            string az = string.IsNullOrEmpty(strip.Endpoint)
                ? (Environment.GetEnvironmentVariable("HSGALAXY_AZURE_VISION_ENDPOINT") is string ep && ep.Length > 0 ? "Azure:On" : "Azure:Off")
                : strip.Endpoint;
            string status = string.IsNullOrEmpty(strip.Status) ? "Connected" : strip.Status;
            string profile = string.IsNullOrEmpty(strip.Profile) ? "Profile:--" : $"Profile:{strip.Profile}";
            string p50 = string.IsNullOrEmpty(strip.P50) ? "P50:N/A" : $"P50:{strip.P50}";
            string p95 = string.IsNullOrEmpty(strip.P95) ? "P95:N/A" : $"P95:{strip.P95}";
            string lat = string.IsNullOrEmpty(strip.Latency) ? "Latency:N/A" : $"Latency:{strip.Latency}";
            string region = string.IsNullOrEmpty(strip.Region) ? "Region:Auto" : $"Region:{strip.Region}";
            string mode = string.IsNullOrEmpty(strip.Mode) ? "Mode:--" : $"Mode:{strip.Mode}";
            string line = $"{status}  |  {profile}  |  {az}  |  {region}  |  {lat}  |  {p50}  |  {p95}  |  {mode}  |  Presents:{_presentCount}  |  dt(ms):{_presentSw.Elapsed.TotalMilliseconds:F1}  |  DPI:{dpi}";
            DrawTextOverlayEx(backBuffer, line, w, heightPx, destX: 0, destY: h - heightPx, scale, ToGdiColor(strip.Foreground));
            _isDirty = true;
        }

        // TODO: Text rendering will be added with a D2D/DirectWrite path to avoid complexities
        // of subresource updates. Placeholder removed to keep builds clean.

        private void DrawTextOverlayEx(ID3D11Texture2D backBuffer, string text, int width, int height, int destX, int destY, float scale, System.Drawing.Color? overrideColor = null)
        {
            if (_device is null || _context is null) return;
            #if HSGALAXY_HAS_DWRITE
            if (_useDWrite && _d2dContext != null)
            {
                EnsureTextTexture(width, height);
                if (_textTexture is null) return;
                using var surf = _textTexture.QueryInterfaceOrNull<IDXGISurface>();
                if (surf is null) return;
                var dpi = GetWindowDpi(_hwnd);
                var props = new BitmapProperties1(new Vortice.Direct2D1.PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied), dpi, dpi, BitmapOptions.Target | BitmapOptions.CannotDraw);
                using var targetBmp = _d2dContext.CreateBitmapFromDxgiSurface(surf, props);
                _d2dContext.Target = targetBmp;
                _d2dContext.BeginDraw();
                _d2dContext.Clear(new Color4(0, 0, 0, 0));
                var c = overrideColor ?? System.Drawing.Color.FromArgb(245, 247, 249);
                _d2dBrush ??= _d2dContext.CreateSolidColorBrush(new Color4(c.R/255f, c.G/255f, c.B/255f, c.A/255f));
                _d2dBrush.Color = new Color4(c.R/255f, c.G/255f, c.B/255f, c.A/255f);
                _dwFormat ??= _dwFactory?.CreateTextFormat("Segoe UI", null, FontWeight.Regular, FontStyle.Normal, FontStretch.Normal, 12.0f * scale, "en-us");
                float lx = (float)Math.Floor(8f * scale);
                float ly = (float)Math.Floor(6f * scale);
                float lw = (float)Math.Floor(width - 16f * scale);
                float lh = (float)Math.Floor(height - 8f * scale);
                var rect = new Vortice.Mathematics.RawVector4(lx, ly, lx + lw, ly + lh);
                if (_dwFormat != null)
                {
                    _d2dContext.DrawText(text, _dwFormat, rect, _d2dBrush);
                }
                _d2dContext.EndDraw();
                var box = new Box(0, 0, 0, width, height, 1);
                _context.CopySubresourceRegion(backBuffer, 0, (uint)destX, (uint)destY, 0, _textTexture, 0, box);
            }
            else
            #endif
            {
                EnsureTextTexture(width, height);
                if (_textTexture is null) return;
                using var bmp = new Bitmap(Math.Max(width,1), Math.Max(height,1), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bmp))
                using (var brush = new SolidBrush(overrideColor ?? System.Drawing.Color.FromArgb(245, 247, 249)))
                {
                    g.Clear(System.Drawing.Color.FromArgb(0, 0, 0, 0));
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    float fontSize = 12.0f * scale;
                    using var font = new Font("Segoe UI", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
                    float lx = (float)Math.Floor(8f * scale);
                    float ly = (float)Math.Floor(6f * scale);
                    float lw = (float)Math.Floor(width - 16f * scale);
                    float lh = (float)Math.Floor(height - 8f * scale);
                    var layout = new RectangleF(lx, ly, lw, lh);
                    g.DrawString(text, font, brush, layout);
                }
                var data = bmp.LockBits(new Rectangle(0,0,bmp.Width,bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                try
                {
                    var db = _context.Map(_textTexture, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
                    try
                    {
                        int srcStride = data.Stride;
                        int dstStride = (int)db.RowPitch;
                        unsafe
                        {
                            byte* src = (byte*)data.Scan0;
                            byte* dst = (byte*)db.DataPointer;
                            int rowBytes = Math.Min(srcStride, dstStride);
                            for (int y = 0; y < height; y++)
                            {
                                Buffer.MemoryCopy(src + y * srcStride, dst + y * dstStride, dstStride, rowBytes);
                            }
                        }
                    }
                    finally
                    {
                        _context.Unmap(_textTexture, 0);
                    }
                    var box = new Box(0, 0, 0, width, height, 1);
                    _context.CopySubresourceRegion(backBuffer, 0, (uint)destX, (uint)destY, 0, _textTexture, 0, box);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
        }

        private void EnsureTextTexture(int width, int height)
        {
            if (_device is null) return;
            if (_textTexture != null && width == _textTexW && height == _textTexH) return;
            _textTexture?.Dispose();
            _textTexW = Math.Max(width, 1);
            _textTexH = Math.Max(height, 1);
            var desc = new Texture2DDescription
            {
                Width = (uint)_textTexW,
                Height = (uint)_textTexH,
                Format = Format.B8G8R8A8_UNorm,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = _useDWrite ? ResourceUsage.Default : ResourceUsage.Dynamic,
                BindFlags = _useDWrite ? (BindFlags.RenderTarget | BindFlags.ShaderResource) : BindFlags.ShaderResource,
                CPUAccessFlags = _useDWrite ? CpuAccessFlags.None : CpuAccessFlags.Write,
                MiscFlags = ResourceOptionFlags.None
            };
            _textTexture = _device.CreateTexture2D(desc);
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        private static int GetWindowDpi(IntPtr hwnd)
        {
            try
            {
                var overrideStr = Environment.GetEnvironmentVariable("HSGALAXY_DPI_OVERRIDE");
                if (!string.IsNullOrEmpty(overrideStr) && int.TryParse(overrideStr, out var dpiOv))
                {
                    dpiOv = Math.Clamp(dpiOv, 48, 480);
                    return dpiOv;
                }
                if (hwnd != IntPtr.Zero)
                {
                    uint dpi = GetDpiForWindow(hwnd);
                    if (dpi >= 48 && dpi <= 480) return (int)dpi;
                }
            }
            catch { }
            return 96;
        }

        private static System.Drawing.Color ToGdiColor(Color4 c)
        {
            int r = (int)Math.Round(c.R * 255.0f);
            int g = (int)Math.Round(c.G * 255.0f);
            int b = (int)Math.Round(c.B * 255.0f);
            int a = (int)Math.Round(c.A * 255.0f);
            return System.Drawing.Color.FromArgb(a, r, g, b);
        }

        private void TryInitD2DAndDWrite()
        {
            _useDWrite = string.Equals(Environment.GetEnvironmentVariable("HSGALAXY_USE_DWRITE"), "1", StringComparison.OrdinalIgnoreCase);
            #if HSGALAXY_HAS_DWRITE
            if (!_useDWrite) return;
            try
            {
                if (_dxgiDevice is null) return;
                _d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory1>(FactoryType.SingleThreaded);
                _d2dDevice = _d2dFactory.CreateDevice(_dxgiDevice);
                _d2dContext = _d2dDevice.CreateDeviceContext(DeviceContextOptions.None);
                _dwFactory = DWrite.DWriteCreateFactory<IDWriteFactory>(DWrite.FactoryType.Shared);
            }
            catch (Exception ex)
            {
                OverlayLogger.Log("DWrite.Init.Error", ex.Message);
                _useDWrite = false;
            }
            #else
            _useDWrite = false;
            #endif
        }
    }
}
