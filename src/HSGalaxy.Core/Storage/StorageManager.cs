using System;
using System.IO;
using System.Threading;

namespace HSGalaxy.Core.Storage
{
    /// <summary>
    /// Manages application storage locations and ensures required directories exist.
    /// Primary data root is D:\cursor_bots\HSGalaxy; falls back to %LOCALAPPDATA%\HSGalaxy
    /// if primary is unavailable or free space is below the threshold.
    /// </summary>
    public sealed class StorageManager : IStorageEvents, IDisposable
    {
        private const long MinimumFreeBytes = 1L * 1024 * 1024 * 1024; // 1 GB
        private const string DefaultCompanyFolder = "HSGalaxy";
        private static readonly string PrimaryRoot = @"D:\\cursor_bots\\HSGalaxy";

        private string _currentRoot = PrimaryRoot;
        private bool _usingFallback;
        private Timer? _monitorTimer;

        public event EventHandler<StorageLocationChangedEventArgs>? LocationChanged;
        public event EventHandler<LowDiskSpaceEventArgs>? LowDiskSpace;

        public string RootPath => _currentRoot;
        public bool IsUsingFallback => _usingFallback;

        public StorageManager()
        {
            TryUsePrimaryDataRoot();
        }

        /// <summary>
        /// Checks if primary data root is available with sufficient space (>= 1GB).
        /// Falls back to LocalAppData if not available.
        /// </summary>
        public bool TryUsePrimaryDataRoot()
        {
            var desired = PrimaryRoot;
            var ok = DirectoryExistsAndHasSpace(desired, MinimumFreeBytes);
            return SetRoot(ok ? desired : GetFallbackRoot(), useFallback: !ok);
        }

        public void EnsureDirectories()
        {
            Directory.CreateDirectory(GetConfigPath());
            Directory.CreateDirectory(GetCalibrationPath());
            Directory.CreateDirectory(GetDictionaryPath());
            Directory.CreateDirectory(GetTiersPath());
            Directory.CreateDirectory(GetLogsPath());
            Directory.CreateDirectory(GetDumpsPath());
            Directory.CreateDirectory(GetBackupsPath());
        }

        public string GetConfigPath() => Path.Combine(_currentRoot, "config");
        public string GetCalibrationPath() => Path.Combine(_currentRoot, "calibration");
        public string GetDictionaryPath() => Path.Combine(_currentRoot, "dict");
        public string GetTiersPath() => Path.Combine(_currentRoot, "tiers");
        public string GetLogsPath() => Path.Combine(_currentRoot, "logs");
        public string GetDumpsPath() => Path.Combine(_currentRoot, "dumps");
        public string GetBackupsPath() => Path.Combine(_currentRoot, "backups");

        public void StartMonitoring(TimeSpan? interval = null)
        {
            var due = TimeSpan.FromMinutes(5);
            var period = interval ?? TimeSpan.FromMinutes(5);
            _monitorTimer ??= new Timer(CheckDiskSpace, null, due, period);
        }

        public void StopMonitoring()
        {
            _monitorTimer?.Dispose();
            _monitorTimer = null;
        }

        private void CheckDiskSpace(object? state)
        {
            try
            {
                var rootDrive = Path.GetPathRoot(_currentRoot) ?? _currentRoot;
                var di = new DriveInfo(rootDrive);
                var free = di.AvailableFreeSpace;
                if (free < MinimumFreeBytes)
                {
                    LowDiskSpace?.Invoke(this, new LowDiskSpaceEventArgs(rootDrive, free));
                }
            }
            catch
            {
                // Intentionally ignore monitoring errors for now.
            }
        }

        private static bool DirectoryExistsAndHasSpace(string path, long minBytes)
        {
            try
            {
                var root = Path.GetPathRoot(path)!;
                var di = new DriveInfo(root);
                return di.IsReady && di.AvailableFreeSpace >= minBytes;
            }
            catch
            {
                return false;
            }
        }

        private static string GetFallbackRoot()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, DefaultCompanyFolder);
        }

        private bool SetRoot(string newRoot, bool useFallback)
        {
            if (!string.Equals(_currentRoot, newRoot, StringComparison.OrdinalIgnoreCase))
            {
                _currentRoot = newRoot;
                _usingFallback = useFallback;
                LocationChanged?.Invoke(this, new StorageLocationChangedEventArgs(_currentRoot, _usingFallback));
                return true;
            }
            _usingFallback = useFallback;
            return false;
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}

