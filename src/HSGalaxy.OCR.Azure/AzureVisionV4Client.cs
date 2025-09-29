using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HSGalaxy.Core.OCR;
using Newtonsoft.Json.Linq;

namespace HSGalaxy.OCR.Azure
{
    /// <summary>
    /// Azure Image Analysis v4 client (Read feature) — env driven.
    /// Expects HSGALAXY_AZURE_VISION_ENDPOINT and HSGALAXY_AZURE_VISION_KEY.
    /// If env vars are missing, throws InvalidOperationException so caller can fallback.
    /// </summary>
    public sealed class AzureVisionV4Client : IOcrClient
    {
        public string Name => "AzureVisionV4";

        public async Task<OcrResult> RecognizeAsync(byte[] pngImage, CancellationToken ct = default)
        {
            var endpoint = Environment.GetEnvironmentVariable("HSGALAXY_AZURE_VISION_ENDPOINT");
            var apiKey = Environment.GetEnvironmentVariable("HSGALAXY_AZURE_VISION_KEY");
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Azure Vision env vars not set.");

            var url = $"{endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=read";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
            req.Content = new ByteArrayContent(pngImage);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");

            var sw = Stopwatch.StartNew();
            using var resp = await HSGalaxy.Core.Net.HttpClientManager.SendWithRetryAsync(req, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            sw.Stop();

            var parsed = JObject.Parse(json);
            var result = new OcrResult { Source = Name, ElapsedMs = sw.Elapsed.TotalMilliseconds };
            // Minimal extraction: iterate lines if present
            var lines = parsed.SelectTokens("$.readResult.blocks[*].lines[*]");
            foreach (var line in lines)
            {
                var text = line["text"]?.Value<string>() ?? string.Empty;
                var conf = line["confidence"]?.Value<float?>() ?? 0.0f;
                result.Lines.Add(new OcrLine { RoiIndex = 0, Text = text, Confidence = conf, X = 0, Y = 0, Width = 0, Height = 0 });
            }
            return result;
        }
    }
}

