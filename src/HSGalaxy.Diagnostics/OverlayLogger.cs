using System;
using System.Diagnostics;
using System.IO;

namespace HSGalaxy.Diagnostics
{
    public static class OverlayLogger
    {
        private static readonly object Gate = new object();
        private static string? _logPath;

        private static string ResolveLogPath()
        {
            if (_logPath != null) return _logPath;
            try
            {
                var primary = @"D:\\cursor_bots\\HSGalaxy\\logs";
                if (Directory.Exists(@"D:\\cursor_bots\\HSGalaxy"))
                {
                    Directory.CreateDirectory(primary);
                    _logPath = Path.Combine(primary, "overlay.log");
                    return _logPath;
                }
            }
            catch { /* ignore */ }

            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "logs");
            Directory.CreateDirectory(local);
            _logPath = Path.Combine(local, "overlay.log");
            return _logPath;
        }

        public static void Log(string evt, string message = "")
        {
            try
            {
                var path = ResolveLogPath();
                var line = $"{DateTime.Now:O}\t{evt}\t{message}";
                lock (Gate)
                {
                    File.AppendAllText(path, line + Environment.NewLine);
                }
                Debug.WriteLine(line);
            }
            catch { /* ignore logging errors */ }
        }
    }
}

