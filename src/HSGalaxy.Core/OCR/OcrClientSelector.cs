using System;


namespace HSGalaxy.Core.OCR
{
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
    }
}






        private static IOcrClient? CreateAzureIfConfigured()
        {
            try
            {
                var endpoint = Environment.GetEnvironmentVariable("HSGALAXY_AZURE_VISION_ENDPOINT");
                var apiKey = Environment.GetEnvironmentVariable("HSGALAXY_AZURE_VISION_KEY");
                if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
                {
                    // Late-bind to avoid project circular reference
                    var t = Type.GetType("HSGalaxy.OCR.Azure.AzureVisionV4Client, HSGalaxy.OCR.Azure", throwOnError: false);
                    if (t != null)
                    {
                        return (IOcrClient?)Activator.CreateInstance(t);
                    }
                }
            }
            catch { }
            return null;
        }
