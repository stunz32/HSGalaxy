using System;

namespace HSGalaxy.Core.Storage
{
    public interface IStorageEvents
    {
        event EventHandler<StorageLocationChangedEventArgs>? LocationChanged;
        event EventHandler<LowDiskSpaceEventArgs>? LowDiskSpace;
    }

    public sealed class StorageLocationChangedEventArgs : EventArgs
    {
        public string NewRoot { get; }
        public bool IsFallback { get; }

        public StorageLocationChangedEventArgs(string newRoot, bool isFallback)
        {
            NewRoot = newRoot;
            IsFallback = isFallback;
        }
    }

    public sealed class LowDiskSpaceEventArgs : EventArgs
    {
        public string DriveRoot { get; }
        public long FreeBytes { get; }

        public LowDiskSpaceEventArgs(string driveRoot, long freeBytes)
        {
            DriveRoot = driveRoot;
            FreeBytes = freeBytes;
        }
    }
}

