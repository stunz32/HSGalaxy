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

        private const int SPI_GETWORKAREA = 0x0030;
        private const int WM_DPICHANGED = 0x02E0;

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

            // Nearly-opaque so it’s visible if needed; click-through because of WS_EX_TRANSPARENT.
            SetLayeredWindowAttributes(_hwnd, 0, 255, LWA_ALPHA);

            // Exclude from screen capture.
            SetWindowDisplayAffinity(_hwnd, WDA_EXCLUDEFROMCAPTURE);

            PositionOverlayWindow();
            ShowWindow(_hwnd, SW_SHOW);
            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            return _hwnd;
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

        private IntPtr WndProcImpl(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_DPICHANGED:
                    PositionOverlayWindow();
                    break;
            }
            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
            if (_classAtom != 0)
            {
                UnregisterClassW("HSGalaxyOverlayWindow", GetModuleHandle(null));
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
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(int uiAction, int uiParam, out RECT pvParam, int fWinIni);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool UnregisterClassW(string lpClassName, IntPtr hInstance);
        #endregion
    }
}

