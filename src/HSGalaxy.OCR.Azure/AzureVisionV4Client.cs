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
            // Extract lines; compute rectangle from bounding polygon; compute confidence from words
            var lines = parsed.SelectTokens("$.readResult.blocks[*].lines[*]");
            foreach (var line in lines)
            {
                var text = line["text"]?.Value<string>() ?? string.Empty;
                float confidence = 0f;
                int wcount = 0;
                var words = line["words"] as JArray;
                if (words != null)
                {
                    foreach (var w in words)
                    {
                        var c = w["confidence"]?.Value<float?>();
                        if (c.HasValue)
                        {
                            confidence += c.Value;
                            wcount++;
                        }
                    }
                }
                if (wcount > 0)
                {
                    confidence /= wcount;
                }
                else
                {
                    // Line-level confidence if present, else a neutral default
                    confidence = line["confidence"]?.Value<float?>() ?? 0.85f;
                }

                // boundingPolygon: [x1,y1,x2,y2,...]
                int x=0, y=0, width=0, height=0;
                var poly = line["boundingPolygon"] as JArray ?? line["boundingBox"] as JArray; // support older shapes
                if (poly != null && poly.Count >= 8)
                {
                    int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                    for (int i = 0; i+1 < poly.Count; i += 2)
                    {
                        int px = (int)Math.Round(poly[i]!.Value<double>());
                        int py = (int)Math.Round(poly[i+1]!.Value<double>());
                        if (px < minX) minX = px; if (px > maxX) maxX = px;
                        if (py < minY) minY = py; if (py > maxY) maxY = py;
                    }
                    x = Math.Max(minX, 0);
                    y = Math.Max(minY, 0);
                    width = Math.Max(maxX - minX, 0);
                    height = Math.Max(maxY - minY, 0);
                }

                result.Lines.Add(new OcrLine { RoiIndex = 0, Text = text, Confidence = confidence, X = x, Y = y, Width = width, Height = height });
            }
            return result;
        }
    }
}
