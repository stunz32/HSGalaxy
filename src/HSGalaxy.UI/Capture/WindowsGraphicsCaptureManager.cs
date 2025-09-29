using System;
using HSGalaxy.Diagnostics;

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
    }
}

