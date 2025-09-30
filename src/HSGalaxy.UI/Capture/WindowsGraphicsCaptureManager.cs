using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using HSGalaxy.Diagnostics;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace HSGalaxy.UI.Capture
{
    /// <summary>
    /// Placeholder for Windows.Graphics.Capture-based manager using reflection to avoid hard build deps.
    /// Currently reports IsSupported=false in this environment; falls back to GDI capture.
    /// </summary>
    public sealed class WindowsGraphicsCaptureManager : IDisposable
    {
        public bool IsSupported { get; }

        public WindowsGraphicsCaptureManager()
        {
            try
            {
                // Try resolve a WGC type via reflection
                var t = Type.GetType("Windows.Graphics.Capture.GraphicsCaptureItem, Windows.Foundation.UniversalApiContract", throwOnError: false);
                IsSupported = t != null;
            }
            catch
            {
                IsSupported = false;
            }
            OverlayLogger.Log("Capture.WGC.Supported", IsSupported.ToString());
        }

        public void Dispose() { }

        /// <summary>
        /// Frame-arrival self-test. If WGC is unavailable, falls back to GDI capture of a small region
        /// for ~0.5s and reports FPS. Returns true if FPS >= 20.
        /// </summary>
        public bool SelfTestFrames(Func<Rectangle, Bitmap> fallbackCapture) => SelfTestFramesFps(fallbackCapture, out _);

        public bool SelfTestFramesFps(Func<Rectangle, Bitmap> fallbackCapture, out double measuredFps)
        {
            measuredFps = 0;
            // Try real WGC path first (reflection-guarded). If anything fails, fall back to GDI.
            if (IsSupported)
            {
                try
                {
                    if (TryRunWgcFps(out double realFps))
                    {
                        measuredFps = realFps;
                        OverlayLogger.Log("Capture.WGC.RealFPS", realFps.ToString("F1"));
                        return realFps >= 20.0;
                    }
                }
                catch (Exception ex)
                {
                    OverlayLogger.Log("Capture.WGC.RealError", ex.GetType().Name + ": " + ex.Message);
                }
            }

            // Fallback to GDI loop
            try
            {
                var rect = new Rectangle(0, 0, 320, 180);
                int frames = 0;
                var stopAt = DateTime.UtcNow + TimeSpan.FromMilliseconds(500);
                while (DateTime.UtcNow < stopAt)
                {
                    using var bmp = fallbackCapture(rect);
                    frames++;
                    Thread.Sleep(16);
                }
                double fps = frames / 0.5;
                measuredFps = fps;
                OverlayLogger.Log("Capture.WGC.FallbackFPS", fps.ToString("F1"));
                return fps >= 20.0;
            }
            catch (Exception ex)
            {
                OverlayLogger.Log("Capture.WGC.SelfTestError", ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Measure FPS capturing a specific HWND for ~0.5s. Reflection-guarded WGC path; falls back to GDI capture
        /// of that window's rectangle.
        /// </summary>
        public bool SelfTestWindowFramesFps(IntPtr hwnd, Func<Rectangle, Bitmap> fallbackCapture, out double measuredFps)
        {
            measuredFps = 0;
            Rectangle bounds = GetWindowBounds(hwnd);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                OverlayLogger.Log("Capture.WGC.WindowError", "Invalid window bounds");
                return false;
            }

            if (IsSupported)
            {
                try
                {
                    if (TryRunWgcWindowFps(hwnd, out double realFps))
                    {
                        measuredFps = realFps;
                        OverlayLogger.Log("Capture.WGC.WindowRealFPS", realFps.ToString("F1"));
                        return realFps >= 20.0;
                    }
                }
                catch (Exception ex)
                {
                    OverlayLogger.Log("Capture.WGC.WindowRealError", ex.GetType().Name + ": " + ex.Message);
                }
            }

            // Fallback: GDI capture loop of a cropped window rect for consistency
            try
            {
                // Crop to at most 640x360 anchored at top-left to avoid huge blits
                var crop = new Rectangle(bounds.X, bounds.Y, Math.Min(bounds.Width, 640), Math.Min(bounds.Height, 360));
                int frames = 0;
                var stopAt = DateTime.UtcNow + TimeSpan.FromMilliseconds(500);
                while (DateTime.UtcNow < stopAt)
                {
                    if (!IsWindow(hwnd))
                    {
                        OverlayLogger.Log("Capture.WGC.WindowClosed", "true");
                        break; // graceful exit
                    }
                    if (IsIconic(hwnd))
                    {
                        // Minimized: treat as stopped capture
                        Thread.Sleep(16);
                    }
                    else
                    {
                        using var bmp = fallbackCapture(crop);
                        frames++;
                    }
                    Thread.Sleep(16);
                }
                measuredFps = frames / 0.5;
                OverlayLogger.Log("Capture.WGC.WindowFallbackFPS", measuredFps.ToString("F1"));
                return measuredFps >= 20.0;
            }
            catch (Exception ex)
            {
                OverlayLogger.Log("Capture.WGC.WindowSelfTestError", ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        private bool TryRunWgcFps(out double fps)
        {
            fps = 0;
            // Resolve required WinRT types via reflection
            var captureItemType = Type.GetType("Windows.Graphics.Capture.GraphicsCaptureItem, Windows.Foundation.UniversalApiContract", throwOnError: false);
            var framePoolType   = Type.GetType("Windows.Graphics.Capture.Direct3D11CaptureFramePool, Windows.Foundation.UniversalApiContract", throwOnError: false);
            var sessionType     = Type.GetType("Windows.Graphics.Capture.GraphicsCaptureSession, Windows.Foundation.UniversalApiContract", throwOnError: false);
            var dxpfType        = Type.GetType("Windows.Graphics.DirectX.DirectXPixelFormat, Windows.Foundation.UniversalApiContract", throwOnError: false);
            if (captureItemType is null || framePoolType is null || sessionType is null || dxpfType is null)
                return false;

            // Create D3D11 device with BGRA support
            using var device = CreateDevice();
            using var dxgi = device?.QueryInterface<IDXGIDevice>();
            if (device is null || dxgi is null) return false;

            // Bridge to IDirect3DDevice (WinRT)
            IntPtr winrtDevicePtr = IntPtr.Zero;
            int hr = CreateDirect3D11DeviceFromDXGIDevice(dxgi.NativePointer, out winrtDevicePtr);
            if (hr != 0 || winrtDevicePtr == IntPtr.Zero) return false;
            object winrtDevice = Marshal.GetObjectForIUnknown(winrtDevicePtr);

            // Create GraphicsCaptureItem for primary monitor via IGraphicsCaptureItemInterop
            object? factory = RoGetActivationFactoryForInterop("Windows.Graphics.Capture.GraphicsCaptureItem");
            if (factory is null) return false;

            var interop = (IGraphicsCaptureItemInterop)factory;
            Guid iidInspectable = new Guid("AF86E2E0-B12D-4C6A-9C5A-D7AA65101E90"); // IInspectable
            IntPtr hmon = MonitorFromPoint(new POINT(0, 0), 1 /*MONITOR_DEFAULTTOPRIMARY*/);
            IntPtr itemPtr = IntPtr.Zero;
            int hrCreate = interop.CreateForMonitor(hmon, ref iidInspectable, out itemPtr);
            if (hrCreate != 0 || itemPtr == IntPtr.Zero) return false;
            object item = Marshal.GetObjectForIUnknown(itemPtr);

            // Get item.Size (SizeInt32)
            var sizeProp = captureItemType.GetProperty("Size");
            object itemSize = sizeProp!.GetValue(item)!;

            // Get pixel format value: B8G8R8A8UIntNormalized
            object pixelFormat = Enum.Parse(dxpfType, "B8G8R8A8UIntNormalized");

            // Create frame pool
            object? framePool = TryInvokeStatic(framePoolType, "Create", new object?[] { winrtDevice, pixelFormat, 1, itemSize });
            if (framePool is null)
            {
                // Try CreateFreeThreaded if Create is unavailable
                framePool = TryInvokeStatic(framePoolType, "CreateFreeThreaded", new object?[] { winrtDevice, pixelFormat, 1, itemSize });
                if (framePool is null) return false;
            }

            // Create session and start
            var createSession = framePoolType.GetMethod("CreateCaptureSession");
            object session = createSession!.Invoke(framePool, new object[] { item })!;
            sessionType.GetMethod("StartCapture")!.Invoke(session, Array.Empty<object>());
            OverlayLogger.Log("Capture.WGC.RealBegin", "started");

            // Poll frames for ~0.5 sec
            int frames = 0;
            var stopAt = DateTime.UtcNow + TimeSpan.FromMilliseconds(500);
            var tryGetNextFrame = framePoolType.GetMethod("TryGetNextFrame");
            while (DateTime.UtcNow < stopAt)
            {
                object? frame = tryGetNextFrame!.Invoke(framePool, Array.Empty<object>());
                if (frame != null)
                {
                    frames++;
                    (frame as IDisposable)?.Dispose();
                }
                Thread.Sleep(1);
            }

            // Cleanup
            (session as IDisposable)?.Dispose();
            (framePool as IDisposable)?.Dispose();

            fps = frames / 0.5;
            return true;
        }

        private bool TryRunWgcWindowFps(IntPtr hwnd, out double fps)
        {
            fps = 0;
            var captureItemType = Type.GetType("Windows.Graphics.Capture.GraphicsCaptureItem, Windows.Foundation.UniversalApiContract", throwOnError: false);
            var framePoolType   = Type.GetType("Windows.Graphics.Capture.Direct3D11CaptureFramePool, Windows.Foundation.UniversalApiContract", throwOnError: false);
            var sessionType     = Type.GetType("Windows.Graphics.Capture.GraphicsCaptureSession, Windows.Foundation.UniversalApiContract", throwOnError: false);
            var dxpfType        = Type.GetType("Windows.Graphics.DirectX.DirectXPixelFormat, Windows.Foundation.UniversalApiContract", throwOnError: false);
            if (captureItemType is null || framePoolType is null || sessionType is null || dxpfType is null)
                return false;

            using var device = CreateDevice();
            using var dxgi = device?.QueryInterface<IDXGIDevice>();
            if (device is null || dxgi is null) return false;

            IntPtr winrtDevicePtr = IntPtr.Zero;
            int hr = CreateDirect3D11DeviceFromDXGIDevice(dxgi.NativePointer, out winrtDevicePtr);
            if (hr != 0 || winrtDevicePtr == IntPtr.Zero) return false;
            object winrtDevice = Marshal.GetObjectForIUnknown(winrtDevicePtr);

            object? factory = RoGetActivationFactoryForInterop("Windows.Graphics.Capture.GraphicsCaptureItem");
            if (factory is null) return false;
            var interop = (IGraphicsCaptureItemInterop)factory;
            Guid iidInspectable = new Guid("AF86E2E0-B12D-4C6A-9C5A-D7AA65101E90");
            IntPtr itemPtr = IntPtr.Zero;
            int hrCreate = interop.CreateForWindow(hwnd, ref iidInspectable, out itemPtr);
            if (hrCreate != 0 || itemPtr == IntPtr.Zero) return false;
            object item = Marshal.GetObjectForIUnknown(itemPtr);

            // Obtain size
            var sizeProp = captureItemType.GetProperty("Size");
            object itemSize = sizeProp!.GetValue(item)!;
            object pixelFormat = Enum.Parse(dxpfType, "B8G8R8A8UIntNormalized");
            object? framePool = TryInvokeStatic(framePoolType, "Create", new object?[] { winrtDevice, pixelFormat, 1, itemSize })
                               ?? TryInvokeStatic(framePoolType, "CreateFreeThreaded", new object?[] { winrtDevice, pixelFormat, 1, itemSize });
            if (framePool is null) return false;

            var createSession = framePoolType.GetMethod("CreateCaptureSession");
            object session = createSession!.Invoke(framePool, new object[] { item })!;
            sessionType.GetMethod("StartCapture")!.Invoke(session, Array.Empty<object>());
            OverlayLogger.Log("Capture.WGC.WindowRealBegin", $"hwnd=0x{hwnd.ToInt64():X}");

            int frames = 0;
            var stopAt = DateTime.UtcNow + TimeSpan.FromMilliseconds(500);
            var tryGetNextFrame = framePoolType.GetMethod("TryGetNextFrame");
            while (DateTime.UtcNow < stopAt)
            {
                object? frame = tryGetNextFrame!.Invoke(framePool, Array.Empty<object>());
                if (frame != null)
                {
                    frames++;
                    (frame as IDisposable)?.Dispose();
                }
                Thread.Sleep(1);
            }
            (session as IDisposable)?.Dispose();
            (framePool as IDisposable)?.Dispose();
            fps = frames / 0.5;
            return true;
        }

        private static Rectangle GetWindowBounds(IntPtr hwnd)
        {
            GetWindowRect(hwnd, out RECT rc);
            return new Rectangle(rc.Left, rc.Top, Math.Max(0, rc.Right - rc.Left), Math.Max(0, rc.Bottom - rc.Top));
        }

        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

        private static ID3D11Device? CreateDevice()
        {
            Vortice.Direct3D11.ID3D11Device? device = null;
            try
            {
                var fl = new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 };
                var res = D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport, fl, out device);
                if (res.Failure || device is null)
                {
                    res = D3D11.D3D11CreateDevice(null, DriverType.Warp, DeviceCreationFlags.BgraSupport, fl, out device);
                }
            }
            catch { }
            return device;
        }

        // Reflection helpers / interop
        private static object? TryInvokeStatic(Type type, string method, object?[] args)
        {
            try { return type.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!.Invoke(null, args); }
            catch { return null; }
        }

        private static object? RoGetActivationFactoryForInterop(string classId)
        {
            try
            {
                Guid iid = typeof(IGraphicsCaptureItemInterop).GUID;
                RoGetActivationFactory(classId, ref iid, out object factory);
                return factory;
            }
            catch { return null; }
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
        private interface IGraphicsCaptureItemInterop
        {
            int CreateForWindow(IntPtr hwnd, ref Guid iid, out IntPtr result);
            int CreateForMonitor(IntPtr monitor, ref Guid iid, out IntPtr result);
        }

        [DllImport("combase.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int RoGetActivationFactory(string classId, ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object factory);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; public POINT(int x, int y){X=x;Y=y;} }
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice")]
        private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);
    }
}
