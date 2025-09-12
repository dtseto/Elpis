using System;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// optional but harmless:
// using System.Net.Http.Headers;

namespace Util
{
    public static class PRequest
    {
        private static WebProxy _proxy;
        private static string _userAgent = "pianobar-2022.04.01";

        public static WebProxy Proxy { get { return _proxy; } }

        private static readonly object _httpLock = new object();
        private static HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                Proxy = _proxy,
                UseProxy = _proxy != null,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            return client;
        }

        private static void RebuildHttpClient()
        {
            lock (_httpLock)
            {
                var old = _http;
                _http = CreateHttpClient();
                if (old != null) old.Dispose();
            }
        }

        public static void SetProxy(string address, string user = "", string password = "")
        {
            var p = new WebProxy(new Uri(address));
            if (!string.IsNullOrEmpty(user))
                p.Credentials = new NetworkCredential(user, password);

            _proxy = p;
            RebuildHttpClient();
        }

        public static void SetProxy(string address, int port, string user = "", string password = "")
        {
            ServicePointManager.Expect100Continue = false; // legacy tweak
            var p = new WebProxy(address, port);
            if (!string.IsNullOrEmpty(user))
                p.Credentials = new NetworkCredential(user, password);

            _proxy = p;
            RebuildHttpClient();
        }

        public static async Task<string> StringRequest(string url, string data, CancellationToken ct = default(CancellationToken))
        {
            Exception lastError = null;

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                using (var req = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    req.Content = new StringContent(data, Encoding.UTF8, "text/plain");

                    if (!string.IsNullOrWhiteSpace(_userAgent))
                        req.Headers.UserAgent.ParseAdd(_userAgent);

                    try
                    {
                        using (var res = await _http.SendAsync(
                            req, HttpCompletionOption.ResponseHeadersRead, ct
                        ).ConfigureAwait(false))
                        {
                            res.EnsureSuccessStatusCode();

                            // No CT overload available here on your TFMs
                            ct.ThrowIfCancellationRequested();
                            var text = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                            return text;
                        }
                    }
                    catch (TaskCanceledException ex) when (!ct.IsCancellationRequested && attempt < 2)
                    {
                        Log.O("StringRequest Timeout (retrying): " + ex);
                        lastError = ex;
                        await Task.Delay(500, ct).ConfigureAwait(false);
                    }
                    catch (HttpRequestException ex) when (attempt < 2)
                    {
                        Log.O("StringRequest Error (retrying): " + ex);
                        lastError = ex;
                        await Task.Delay(500, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (attempt < 2)
                    {
                        Log.O("StringRequest Unexpected (retrying): " + ex);
                        lastError = ex;
                        await Task.Delay(500, ct).ConfigureAwait(false);
                    }
                }
            }

            throw new HttpRequestException("StringRequest failed after one retry.", lastError);
        }

        public static void ByteRequestAsync(string url, DownloadDataCompletedEventHandler dataHandler)
        {
            Log.O("Downloading Async: " + url);
            var wc = new WebClient();
            if (_proxy != null)
                wc.Proxy = _proxy;

            wc.DownloadDataCompleted += (s, e) =>
            {
                try { if (dataHandler != null) dataHandler(s, e); }
                finally { wc.Dispose(); }
            };

            wc.DownloadDataAsync(new Uri(url));
        }

        public static byte[] ByteRequest(string url)
        {
            Log.O("Downloading: " + url);
            using (var wc = new WebClient())
            {
                if (_proxy != null)
                    wc.Proxy = _proxy;

                return wc.DownloadData(new Uri(url));
            }
        }

        public static void FileRequest(string url, string outputFile)
        {
            using (var wc = new WebClient())
            {
                if (_proxy != null)
                    wc.Proxy = _proxy;

                wc.DownloadFile(url, outputFile);
            }
        }

        public static void FileRequestAsync(
            string url,
            string outputFile,
            DownloadProgressChangedEventHandler progressCallback,
            AsyncCompletedEventHandler completeCallback)
        {
            var wc = new WebClient();
            if (_proxy != null)
                wc.Proxy = _proxy;

            if (progressCallback != null)
                wc.DownloadProgressChanged += progressCallback;

            wc.DownloadFileCompleted += (s, e) =>
            {
                try { if (completeCallback != null) completeCallback(s, e); }
                finally { wc.Dispose(); }
            };

            wc.DownloadFileAsync(new Uri(url), outputFile);
        }
    }
}
