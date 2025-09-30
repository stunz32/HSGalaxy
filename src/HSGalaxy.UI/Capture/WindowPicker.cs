using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace HSGalaxy.UI.Capture
{
    public sealed class WindowPicker
    {
        public IEnumerable<WindowInfo> Enumerate()
        {
            var list = new List<WindowInfo>();
            EnumWindows((hwnd, l) =>
            {
                if (!IsWindowVisible(hwnd)) return true;
                if (IsCloaked(hwnd)) return true;
                var title = GetWindowText(hwnd);
                if (string.IsNullOrWhiteSpace(title)) return true;
                var cls = GetClassName(hwnd);
                GetWindowRect(hwnd, out RECT rc);
                if (rc.Right <= rc.Left || rc.Bottom <= rc.Top) return true;
                list.Add(new WindowInfo
                {
                    Hwnd = hwnd,
                    Title = title,
                    Class = cls,
                    Left = rc.Left,
                    Top = rc.Top,
                    Right = rc.Right,
                    Bottom = rc.Bottom
                });
                return true;
            }, IntPtr.Zero);
            return list;
        }

        public bool TryPickFirst(string query, out WindowInfo info)
        {
            foreach (var w in Enumerate())
            {
                if (w.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    w.Class.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    info = w; return true;
                }
            }
            info = default; return false;
        }

        public sealed class WindowInfo
        {
            public IntPtr Hwnd { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Class { get; set; } = string.Empty;
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }

        #region PInvoke
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        private static string GetWindowText(IntPtr hWnd)
        {
            var sb = new System.Text.StringBuilder(512);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
        private static string GetClassName(IntPtr hWnd)
        {
            var sb = new System.Text.StringBuilder(256);
            GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
        private static bool IsCloaked(IntPtr hWnd)
        {
            const int DWMWA_CLOAKED = 14;
            int cloaked = 0;
            try { DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out cloaked, sizeof(int)); } catch { }
            return cloaked != 0;
        }
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
        #endregion
    }
}
