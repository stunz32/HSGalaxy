using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.DXGI;
using Vortice.Mathematics;
using Vortice.DCommon;
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

        public int PresentCount => _presentCount;

        public void Initialize(IntPtr hwnd)
        {
            _hwnd = hwnd;
            CreateDevice();
            CreateSwapChainForComposition();
            SetupDirectComposition();
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
            float heightPx = heightDip; // simple DIP->px for now (96 DPI assumption)
            // Clear the bottom band using ClearView on the current render target
            using var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
            using var rtv = _device.CreateRenderTargetView(backBuffer);
            var rect = new Vortice.RawRect(0, (int)(h - heightPx), w, h);
            _context1.ClearView(rtv, color, new[] { rect });
            _isDirty = true;
        }

        // TODO: Text rendering will be added with a D2D/DirectWrite path to avoid complexities
        // of subresource updates. Placeholder removed to keep builds clean.
    }
}
