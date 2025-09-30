using System;

namespace HSGalaxy.Core.OCR
{
    /// <summary>
    /// Chooses the best available OCR client at runtime.
    /// Prefers Azure (via reflection) when HSGALAXY_AZURE_VISION_* env vars are present,
    /// otherwise falls back to the deterministic simulated client.
    /// </summary>
    public static class OcrClientSelector
    {
        public static IOcrClient Create()
        {
            var endpoint = Environment.GetEnvironmentVariable("HSGALAXY_AZURE_VISION_ENDPOINT");
            var apiKey = Environment.GetEnvironmentVariable("HSGALAXY_AZURE_VISION_KEY");
            if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
            {
                return CreateAzureIfConfigured() ?? new SimulatedOcrClient();
            }
            return new SimulatedOcrClient();
        }

        private static IOcrClient? CreateAzureIfConfigured()
        {
            try
            {
                // Late-bind to avoid Core -> Azure project reference
                var t4 = Type.GetType("HSGalaxy.OCR.Azure.AzureVisionV4Client, HSGalaxy.OCR.Azure", throwOnError: false);
                if (t4 != null) return (IOcrClient?)Activator.CreateInstance(t4);
                var t32 = Type.GetType("HSGalaxy.OCR.Azure.AzureVisionV32Client, HSGalaxy.OCR.Azure", throwOnError: false);
                if (t32 != null) return (IOcrClient?)Activator.CreateInstance(t32);
            }
            catch { /* ignore and fall back */ }
            return null;
        }
    }
}
