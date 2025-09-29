using System;
using System.Runtime.InteropServices;

namespace HSGalaxy.UI.Native
{
    /// <summary>
    /// Native Win32 overlay window with click-through and screenshot exclusion.
    /// Styles: WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST, WS_POPUP.
    /// </summary>
    public sealed class NativeWindow : IDisposable
    {
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        private const int SW_SHOW = 5;
        private const uint LWA_ALPHA = 0x2;
        private const int ULW_ALPHA = 0x00000002;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        private const int SPI_GETWORKAREA = 0x0030;
        private const int WM_DPICHANGED = 0x02E0;
        private const int WM_ACTIVATEAPP = 0x001C;
        private const int WM_HOTKEY = 0x0312;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        // WDA_EXCLUDEFROMCAPTURE (0x11) hides window content from screenshots.
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        private WndProc? _wndProc;
        private ushort _classAtom;
        private IntPtr _hwnd;

        public IntPtr Handle => _hwnd;

        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int VK_T = 0x54;
        private const int VK_P = 0x50;
        private const int VK_C = 0x43;
        private const int HOTKEY_ID_THEME = 0xA11E; // arbitrary unique id
        private const int HOTKEY_ID_CAPTURE = 0xA11F;
        private const int HOTKEY_ID_WIZARD = 0xA120;

        public event EventHandler? ThemeHotkeyPressed;
        public event EventHandler? CaptureHotkeyPressed;
        public event EventHandler? WizardHotkeyPressed;

        /// <summary>
        /// Creates and shows the overlay window.
        /// </summary>
        public IntPtr CreateOverlayWindow()
        {
            if (_hwnd != IntPtr.Zero)
                return _hwnd;

            _wndProc = WndProcImpl;
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = _wndProc,
                hInstance = GetModuleHandle(null),
                lpszClassName = "HSGalaxyOverlayWindow"
            };
            _classAtom = RegisterClassExW(ref wc);
            if (_classAtom == 0)
                throw new InvalidOperationException("RegisterClassExW failed.");

            var exStyle = WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST;
            _hwnd = CreateWindowExW(exStyle, wc.lpszClassName, string.Empty, WS_POPUP,
                0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
                throw new InvalidOperationException("CreateWindowExW failed.");

            // Keep layered style; constant alpha not required when using UpdateLayeredWindow with per-pixel alpha.
            // SetLayeredWindowAttributes(_hwnd, 0, 255, LWA_ALPHA);

            // Exclude from screen capture.
            SetWindowDisplayAffinity(_hwnd, WDA_EXCLUDEFROMCAPTURE);

            PositionOverlayWindow();
            ShowWindow(_hwnd, SW_SHOW);
            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

            // Debug: draw a faint per-pixel alpha tint so users can see the overlay
            TryApplyDebugTint(alpha: 90); // ~35% opacity
            return _hwnd;
        }

        public bool RegisterThemeHotKey()
        {
            if (_hwnd == IntPtr.Zero) return false;
            // Ctrl + Alt + T
            return RegisterHotKey(_hwnd, HOTKEY_ID_THEME, MOD_CONTROL | MOD_ALT, VK_T);
        }

        public bool RegisterCaptureHotKey()
        {
            if (_hwnd == IntPtr.Zero) return false;
            // Ctrl + Alt + P
            return RegisterHotKey(_hwnd, HOTKEY_ID_CAPTURE, MOD_CONTROL | MOD_ALT, VK_P);
        }

        public bool RegisterWizardHotKey()
        {
            if (_hwnd == IntPtr.Zero) return false;
            // Ctrl + Alt + C
            return RegisterHotKey(_hwnd, HOTKEY_ID_WIZARD, MOD_CONTROL | MOD_ALT, VK_C);
        }

        /// <summary>
        /// Positions the overlay to the primary monitor work area with DPI awareness.
        /// </summary>
        public void PositionOverlayWindow()
        {
            if (_hwnd == IntPtr.Zero) return;
            if (!SystemParametersInfo(SPI_GETWORKAREA, 0, out RECT work, 0))
                return;

            var width = work.Right - work.Left;
            var height = work.Bottom - work.Top;
            SetWindowPos(_hwnd, HWND_TOPMOST, work.Left, work.Top, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public event EventHandler? DpiChanged;

        private IntPtr WndProcImpl(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_DPICHANGED:
                    PositionOverlayWindow();
                    // TryApplyDebugTint(alpha: 90); // disabled in normal runs
                    DpiChanged?.Invoke(this, EventArgs.Empty);
                    break;
                case WM_ACTIVATEAPP:
                    // Reassert topmost after task switching and reapply tint
                    SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                    // TryApplyDebugTint(alpha: 200); // disabled in normal runs
                    break;
                case WM_HOTKEY:
                    if (wParam == (IntPtr)HOTKEY_ID_THEME) { ThemeHotkeyPressed?.Invoke(this, EventArgs.Empty); return IntPtr.Zero; }
                    if (wParam == (IntPtr)HOTKEY_ID_CAPTURE) { CaptureHotkeyPressed?.Invoke(this, EventArgs.Empty); return IntPtr.Zero; }
                    if (wParam == (IntPtr)HOTKEY_ID_WIZARD) { WizardHotkeyPressed?.Invoke(this, EventArgs.Empty); return IntPtr.Zero; }
                    break;
            }
            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        public void ClearTint() => TryApplyDebugTint(0);

        private void TryApplyDebugTint(byte alpha)
        {
            if (_hwnd == IntPtr.Zero) return;
            // Use actual window rectangle to match the overlay size/position
            if (!GetWindowRect(_hwnd, out RECT wndRect)) return;
            int width = wndRect.Right - wndRect.Left;
            int height = wndRect.Bottom - wndRect.Top;
            if (width <= 0 || height <= 0) return;

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            try
            {
                BITMAPINFO bmi = new BITMAPINFO();
                bmi.biSize = (uint)Marshal.SizeOf<BITMAPINFO>();
                bmi.biWidth = width;
                bmi.biHeight = -height; // top-down
                bmi.biPlanes = 1;
                bmi.biBitCount = 32;
                bmi.biCompression = 0; // BI_RGB

                IntPtr dib;
                IntPtr bits = IntPtr.Zero;
                dib = CreateDIBSection(memDc, ref bmi, 0, out bits, IntPtr.Zero, 0);
                if (dib == IntPtr.Zero || bits == IntPtr.Zero) return;

                IntPtr old = SelectObject(memDc, dib);

                // Fill pixels with a bright magenta premultiplied color so it's clearly visible
                // Premultiplied: colorChannel = desiredChannel * alpha / 255
                byte a = alpha;
                byte r = a; // 255 * a / 255
                byte g = 0;
                byte b = a; // magenta
                int stride = width * 4;
                int total = stride * height;
                byte[] buffer = new byte[total];
                for (int i = 0; i < total; i += 4)
                {
                    buffer[i + 0] = b; // B
                    buffer[i + 1] = g; // G
                    buffer[i + 2] = r; // R
                    buffer[i + 3] = a; // A
                }
                Marshal.Copy(buffer, 0, bits, total);

                POINT dstPt = new POINT { x = wndRect.Left, y = wndRect.Top };
                SIZE size = new SIZE { cx = width, cy = height };
                POINT srcPt = new POINT { x = 0, y = 0 };
                BLENDFUNCTION blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA
                };
                UpdateLayeredWindow(_hwnd, screenDc, ref dstPt, ref size, memDc, ref srcPt, 0, ref blend, ULW_ALPHA);

                // Cleanup
                SelectObject(memDc, old);
                DeleteObject(dib);
            }
            finally
            {
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        public void Dispose()
        {
            if (_hwnd != IntPtr.Zero)
            {
                try { UnregisterHotKey(_hwnd, HOTKEY_ID_THEME); } catch { }
                try { UnregisterHotKey(_hwnd, HOTKEY_ID_CAPTURE); } catch { }
                try { UnregisterHotKey(_hwnd, HOTKEY_ID_WIZARD); } catch { }
                try { DestroyWindow(_hwnd); } catch { }
                _hwnd = IntPtr.Zero;
            }
            if (_classAtom != 0)
            {
                try { UnregisterClassW("HSGalaxyOverlayWindow", GetModuleHandle(null)); } catch { }
                _classAtom = 0;
            }
            _wndProc = null;
        }

        #region P/Invoke
        private delegate IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEXW
        {
            public uint cbSize;
            public uint style;
            public WndProc? lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowExW(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
            int X, int Y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowDisplayAffinity(IntPtr hWnd, out uint pdwAffinity);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(int uiAction, int uiParam, out RECT pvParam, int fWinIni);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool UnregisterClassW(string lpClassName, IntPtr hInstance);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx; public int cy; }
        [StructLayout(LayoutKind.Sequential)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        #endregion

        public uint QueryDisplayAffinity()
        {
            if (_hwnd == IntPtr.Zero) return 0;
            if (GetWindowDisplayAffinity(_hwnd, out uint a)) return a;
            return 0;
        }

        
    }
}
