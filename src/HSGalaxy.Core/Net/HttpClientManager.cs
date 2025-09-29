using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace HSGalaxy.Core.Net
{
    /// <summary>
    /// Centralized HttpClient with connection pooling, HTTP/2, and retry policy.
    /// </summary>
    public static class HttpClientManager
    {
        private static readonly SocketsHttpHandler Handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            EnableMultipleHttp2Connections = true,
            ConnectTimeout = TimeSpan.FromMilliseconds(250),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };

        public static readonly HttpClient Client;

        static HttpClientManager()
        {
            Client = new HttpClient(Handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            Client.DefaultRequestHeaders.UserAgent.ParseAdd("HSGalaxy/0.1");
            Client.DefaultRequestVersion = HttpVersion.Version20;
            Client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        }

        public static async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage request, CancellationToken ct = default)
        {
            int attempt = 0;
            var sw = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                    if (IsRetriableStatus(response.StatusCode) && attempt < 3)
                    {
                        attempt++;
                        await Task.Delay(Backoff(attempt), ct).ConfigureAwait(false);
                        continue;
                    }
                    return response;
                }
                catch (HttpRequestException) when (attempt < 3)
                {
                    attempt++;
                    await Task.Delay(Backoff(attempt), ct).ConfigureAwait(false);
                    continue;
                }
            }
        }

        private static bool IsRetriableStatus(HttpStatusCode code)
            => code == HttpStatusCode.TooManyRequests || (int)code >= 500;

        private static TimeSpan Backoff(int attempt)
        {
            // 100ms, 200ms, 400ms capped
            var ms = 100 * (int)Math.Pow(2, attempt - 1);
            return TimeSpan.FromMilliseconds(Math.Min(ms, 800));
        }
    }
}


