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
    /// Azure Computer Vision Read v3.2 client (REST) — env driven.
    /// Expects HSGALAXY_AZURE_VISION_ENDPOINT and HSGALAXY_AZURE_VISION_KEY.
    /// </summary>
    public sealed class AzureVisionV32Client : IOcrClient
    {
        public string Name => "AzureVisionV3.2";

        public async Task<OcrResult> RecognizeAsync(byte[] pngImage, CancellationToken ct = default)
        {
            var endpoint = Environment.GetEnvironmentVariable("HSGALAXY_AZURE_VISION_ENDPOINT");
            var apiKey = Environment.GetEnvironmentVariable("HSGALAXY_AZURE_VISION_KEY");
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Azure Vision env vars not set.");

            // Typical path for Read API v3.2
            var url = $"{endpoint.TrimEnd('/')}/vision/v3.2/read/analyze";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
            req.Content = new ByteArrayContent(pngImage);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");

            var sw = Stopwatch.StartNew();
            using var resp = await HSGalaxy.Core.Net.HttpClientManager.SendWithRetryAsync(req, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            // Read API is typically async-operation via Operation-Location; however some regions/resources may inline.
            // Try inline first; if not present, follow Operation-Location once.
            string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                if (resp.Headers.TryGetValues("Operation-Location", out var values))
                {
                    var op = values is null ? null : System.Linq.Enumerable.FirstOrDefault(values);
                    if (!string.IsNullOrWhiteSpace(op))
                    {
                        // Poll once or twice with small delay
                        for (int i = 0; i < 3; i++)
                        {
                            await Task.Delay(150, ct).ConfigureAwait(false);
                            using var getReq = new HttpRequestMessage(HttpMethod.Get, op);
                            getReq.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
                            using var getResp = await HSGalaxy.Core.Net.HttpClientManager.SendWithRetryAsync(getReq, ct).ConfigureAwait(false);
                            getResp.EnsureSuccessStatusCode();
                            var body = await getResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                            if (!string.IsNullOrWhiteSpace(body)) { json = body; break; }
                        }
                    }
                }
            }

            sw.Stop();
            var result = new OcrResult { Source = Name, ElapsedMs = sw.Elapsed.TotalMilliseconds };
            if (string.IsNullOrWhiteSpace(json)) return result;

            var parsed = JObject.Parse(json);
            // Expected shape: { "analyzeResult": { "readResults": [ { "lines": [ { "text": "..", "words": [..], "boundingBox": [x1,y1,...] } ] } ] } }
            var lines = parsed.SelectTokens("$.analyzeResult.readResults[*].lines[*]");
            foreach (var line in lines)
            {
                var text = line["text"]?.Value<string>() ?? string.Empty;
                float confidence = 0.85f;
                int wc = 0;
                if (line["words"] is JArray warr)
                {
                    foreach (var w in warr)
                    {
                        var c = w["confidence"]?.Value<float?>();
                        if (c.HasValue) { confidence += c.Value; wc++; }
                    }
                }
                if (wc > 0) confidence /= (wc + 1); // average with default prior

                int x = 0, y = 0, width = 0, height = 0;
                if (line["boundingBox"] is JArray bb && bb.Count >= 8)
                {
                    int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                    for (int i = 0; i + 1 < bb.Count; i += 2)
                    {
                        int px = (int)Math.Round(bb[i]!.Value<double>());
                        int py = (int)Math.Round(bb[i + 1]!.Value<double>());
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

