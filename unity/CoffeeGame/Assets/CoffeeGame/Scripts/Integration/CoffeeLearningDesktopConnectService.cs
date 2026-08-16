#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_ANDROID
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CoffeeGame.Integration
{
    public sealed class CoffeeLearningDesktopConnectOptions
    {
        public const string ProductionConnectUrl =
            CoffeeLearningHttpBridgeOptions.ProductionProviderBaseUrl + "/api/coffee-game/connect";
        public const int DefaultTimeoutSeconds = 180;
        public const int MinimumTimeoutSeconds = 1;
        public const int MaximumTimeoutSeconds = 300;

        public CoffeeLearningDesktopConnectOptions(
            string providerConnectUrl = ProductionConnectUrl,
            int timeoutSeconds = DefaultTimeoutSeconds)
        {
            if (!Uri.TryCreate(providerConnectUrl, UriKind.Absolute, out var connectUri)
                || !string.Equals(connectUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrEmpty(connectUri.Query)
                || !string.IsNullOrEmpty(connectUri.Fragment))
            {
                throw new ArgumentException(
                    "CoffeeLearning connect URL must be an absolute HTTPS URL without a query or fragment.",
                    nameof(providerConnectUrl));
            }

            if (timeoutSeconds < MinimumTimeoutSeconds || timeoutSeconds > MaximumTimeoutSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutSeconds),
                    $"Connection timeout must be between {MinimumTimeoutSeconds} and {MaximumTimeoutSeconds} seconds.");
            }

            ProviderConnectUri = connectUri;
            TimeoutSeconds = timeoutSeconds;
        }

        public Uri ProviderConnectUri { get; }
        public int TimeoutSeconds { get; }
    }

    public interface ICoffeeGameBrowserLauncher
    {
        void Open(Uri uri);
    }

    public sealed class UnityCoffeeGameBrowserLauncher : ICoffeeGameBrowserLauncher
    {
        public void Open(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            Application.OpenURL(uri.AbsoluteUri);
        }
    }

    /// <summary>
    /// Windows desktop browser handoff. The bearer arrives in a browser fragment, is relayed
    /// over 127.0.0.1 only, state-checked, then persisted through the injected secure store.
    /// </summary>
    public sealed class CoffeeLearningDesktopConnectService : ICoffeeLearningDesktopConnectionService
    {
        private const string CallbackPath = "/coffee-game-callback";
        private const int MaximumCallbackRequests = 8;
        private const int MaximumRequestBytes = 128 * 1024;

        private readonly CoffeeLearningDesktopConnectOptions options;
        private readonly ICoffeeGameAccessTokenStore tokenStore;
        private readonly ICoffeeGameBrowserLauncher browserLauncher;

        public CoffeeLearningDesktopConnectService(
            CoffeeLearningDesktopConnectOptions options,
            ICoffeeGameAccessTokenStore tokenStore,
            ICoffeeGameBrowserLauncher browserLauncher)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
            this.browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = CreateState();

            var listener = new TcpListener(IPAddress.Loopback, 0);
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                listener.Start();
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var redirectUri = new Uri(
                    "http://127.0.0.1:"
                    + endpoint.Port.ToString(CultureInfo.InvariantCulture)
                    + CallbackPath,
                    UriKind.Absolute);
                var connectUri = BuildConnectUri(redirectUri, state);
                timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

                try
                {
                    browserLauncher.Open(connectUri);
                    for (var requestCount = 0; requestCount < MaximumCallbackRequests; requestCount++)
                    {
                        using (var client = await AcceptTcpClientAsync(listener, timeout.Token))
                        using (var stream = client.GetStream())
                        {
                            var request = await ReadHttpRequestAsync(stream, timeout.Token);
                            if (!IsCallbackRequest(request.Target))
                            {
                                await WriteHtmlResponseAsync(
                                    stream,
                                    "CoffeeGAME",
                                    "Not found.",
                                    404,
                                    timeout.Token);
                                continue;
                            }

                            var values = string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase)
                                ? ParseUrlEncoded(request.Body)
                                : ParseQuery(request.Target);
                            if (!values.TryGetValue("bearer", out var bearer)
                                || string.IsNullOrWhiteSpace(bearer))
                            {
                                await WriteFragmentRelayResponseAsync(stream, timeout.Token);
                                continue;
                            }

                            if (!values.TryGetValue("state", out var receivedState)
                                || !FixedTimeEquals(state, receivedState))
                            {
                                await WriteHtmlResponseAsync(
                                    stream,
                                    "CoffeeGAME",
                                    "The connection state did not match. Return to CoffeeGAME and try again.",
                                    400,
                                    timeout.Token);
                                continue;
                            }

                            string token;
                            try
                            {
                                token = CoffeeGameAccessToken.Normalize(bearer);
                            }
                            catch (ArgumentException)
                            {
                                await WriteHtmlResponseAsync(
                                    stream,
                                    "CoffeeGAME",
                                    "CoffeeLearning did not return a valid CoffeeGAME credential.",
                                    400,
                                    timeout.Token);
                                continue;
                            }

                            await tokenStore.SaveAccessTokenAsync(token, timeout.Token);
                            await WriteConnectionCompleteResponseBestEffortAsync(stream, timeout.Token);
                            return;
                        }
                    }

                    throw new CoffeeLearningDesktopConnectException(
                        "CONNECT_INCOMPLETE",
                        "CoffeeLearning connection did not complete.");
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new CoffeeLearningDesktopConnectException(
                        "CONNECT_TIMEOUT",
                        "CoffeeLearning connection timed out.");
                }
                catch (SocketException) when (timeout.IsCancellationRequested)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    throw new CoffeeLearningDesktopConnectException(
                        "CONNECT_TIMEOUT",
                        "CoffeeLearning connection timed out.");
                }
                catch (ObjectDisposedException) when (timeout.IsCancellationRequested)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    throw new CoffeeLearningDesktopConnectException(
                        "CONNECT_TIMEOUT",
                        "CoffeeLearning connection timed out.");
                }
                finally
                {
                    listener.Stop();
                }
            }
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            return tokenStore.DeleteAccessTokenAsync(cancellationToken);
        }

        private Uri BuildConnectUri(Uri redirectUri, string state)
        {
            return new Uri(
                options.ProviderConnectUri.AbsoluteUri
                + "?redirect_uri=" + Uri.EscapeDataString(redirectUri.AbsoluteUri)
                + "&state=" + Uri.EscapeDataString(state),
                UriKind.Absolute);
        }

        private static async Task<TcpClient> AcceptTcpClientAsync(
            TcpListener listener,
            CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(listener.Stop))
            {
                try
                {
                    return await listener.AcceptTcpClientAsync();
                }
                catch (SocketException) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
        }

        private static string CreateState()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static bool FixedTimeEquals(string expected, string actual)
        {
            if (expected == null || actual == null)
            {
                return false;
            }

            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var actualBytes = Encoding.UTF8.GetBytes(actual);
            var difference = expectedBytes.Length ^ actualBytes.Length;
            var length = Math.Max(expectedBytes.Length, actualBytes.Length);
            for (var index = 0; index < length; index++)
            {
                var expectedByte = index < expectedBytes.Length ? expectedBytes[index] : (byte)0;
                var actualByte = index < actualBytes.Length ? actualBytes[index] : (byte)0;
                difference |= expectedByte ^ actualByte;
            }

            Array.Clear(expectedBytes, 0, expectedBytes.Length);
            Array.Clear(actualBytes, 0, actualBytes.Length);
            return difference == 0;
        }

        private static bool IsCallbackRequest(string target)
        {
            var queryIndex = target.IndexOf('?');
            var path = queryIndex >= 0 ? target.Substring(0, queryIndex) : target;
            return string.Equals(path, CallbackPath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(CallbackPath, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<HttpRequestData> ReadHttpRequestAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            using (var requestBytes = new MemoryStream())
            {
                var headerEnd = -1;
                var contentLength = 0;
                while (true)
                {
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    requestBytes.Write(buffer, 0, read);
                    var data = requestBytes.ToArray();
                    if (headerEnd < 0)
                    {
                        headerEnd = FindHeaderEnd(data);
                        if (headerEnd >= 0)
                        {
                            contentLength = ParseContentLength(
                                Encoding.ASCII.GetString(data, 0, headerEnd));
                            if (contentLength < 0 || contentLength > MaximumRequestBytes)
                            {
                                throw new CoffeeLearningDesktopConnectException(
                                    "CONNECT_RESPONSE_TOO_LARGE",
                                    "CoffeeLearning connection response was too large.");
                            }
                        }
                    }

                    if (data.Length > MaximumRequestBytes)
                    {
                        throw new CoffeeLearningDesktopConnectException(
                            "CONNECT_RESPONSE_TOO_LARGE",
                            "CoffeeLearning connection response was too large.");
                    }

                    if (headerEnd >= 0 && data.Length >= headerEnd + 4 + contentLength)
                    {
                        return ParseRequest(data, headerEnd, contentLength);
                    }
                }
            }

            throw new CoffeeLearningDesktopConnectException(
                "CONNECT_RESPONSE_INVALID",
                "CoffeeLearning connection response was invalid.");
        }

        private static int FindHeaderEnd(byte[] data)
        {
            for (var index = 0; index <= data.Length - 4; index++)
            {
                if (data[index] == '\r' && data[index + 1] == '\n'
                    && data[index + 2] == '\r' && data[index + 3] == '\n')
                {
                    return index;
                }
            }

            return -1;
        }

        private static int ParseContentLength(string headers)
        {
            var lines = headers.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var separator = line.IndexOf(':');
                if (separator <= 0
                    || !string.Equals(
                        line.Substring(0, separator).Trim(),
                        "Content-Length",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return int.TryParse(
                    line.Substring(separator + 1).Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var value)
                    ? value
                    : -1;
            }

            return 0;
        }

        private static HttpRequestData ParseRequest(byte[] data, int headerEnd, int contentLength)
        {
            var headers = Encoding.ASCII.GetString(data, 0, headerEnd);
            var lines = headers.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var requestParts = lines.Length == 0
                ? Array.Empty<string>()
                : lines[0].Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (requestParts.Length < 2
                || (!string.Equals(requestParts[0], "GET", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(requestParts[0], "POST", StringComparison.OrdinalIgnoreCase)))
            {
                throw new CoffeeLearningDesktopConnectException(
                    "CONNECT_RESPONSE_INVALID",
                    "CoffeeLearning connection response was invalid.");
            }

            var body = contentLength == 0
                ? string.Empty
                : Encoding.UTF8.GetString(data, headerEnd + 4, contentLength);
            return new HttpRequestData(requestParts[0], requestParts[1], body);
        }

        private static Dictionary<string, string> ParseQuery(string target)
        {
            var questionIndex = target.IndexOf('?');
            return questionIndex < 0
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : ParseUrlEncoded(target.Substring(questionIndex + 1));
        }

        private static Dictionary<string, string> ParseUrlEncoded(string value)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in value.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=');
                var key = DecodeUrlPart(separator >= 0 ? pair.Substring(0, separator) : pair);
                var itemValue = separator >= 0 ? DecodeUrlPart(pair.Substring(separator + 1)) : string.Empty;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    result[key] = itemValue;
                }
            }

            return result;
        }

        private static string DecodeUrlPart(string value)
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }

        private static Task WriteFragmentRelayResponseAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            const string html = "<!doctype html><meta charset=\"utf-8\"><title>CoffeeGAME</title>"
                + "<body><p id=\"status\">Connecting CoffeeGAME...</p><script>"
                + "(async function(){const s=document.getElementById('status');"
                + "const b=location.hash.length>1?location.hash.substring(1):'';"
                + "if(!b){s.textContent='Connection data was not found.';return;}"
                + "try{const r=await fetch('/coffee-game-callback/receive',{method:'POST',"
                + "headers:{'Content-Type':'application/x-www-form-urlencoded'},body:b});"
                + "history.replaceState(null,'','/coffee-game-callback/done');"
                + "s.textContent=r.ok?'CoffeeLearning is connected. You can close this tab.':"
                + "'CoffeeLearning could not be connected. Return to CoffeeGAME and try again.';"
                + "if(r.ok){setTimeout(function(){window.close();},500);}}catch(e){"
                + "s.textContent='CoffeeLearning could not be connected. Return to CoffeeGAME and try again.';}"
                + "})();</script></body>";
            return WriteRawHtmlResponseAsync(stream, html, 200, cancellationToken);
        }

        private static Task WriteHtmlResponseAsync(
            NetworkStream stream,
            string title,
            string message,
            int statusCode,
            CancellationToken cancellationToken)
        {
            var html = "<!doctype html><meta charset=\"utf-8\"><title>"
                + EscapeHtml(title)
                + "</title><body><h2>" + EscapeHtml(title) + "</h2><p>"
                + EscapeHtml(message) + "</p></body>";
            return WriteRawHtmlResponseAsync(stream, html, statusCode, cancellationToken);
        }

        private static async Task WriteConnectionCompleteResponseBestEffortAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            try
            {
                await WriteHtmlResponseAsync(
                    stream,
                    "CoffeeGAME",
                    "CoffeeLearning is connected. You can close this tab.",
                    200,
                    cancellationToken);
            }
            catch (IOException)
            {
                // The credential is already stored. A browser closing the local page must not
                // keep CoffeeGAME in Connecting or turn a completed handoff into a failure.
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static Task WriteRawHtmlResponseAsync(
            NetworkStream stream,
            string html,
            int statusCode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var body = Encoding.UTF8.GetBytes(html);
            var statusText = statusCode == 404 ? "Not Found" : statusCode == 400 ? "Bad Request" : "OK";
            var headers = Encoding.ASCII.GetBytes(
                "HTTP/1.1 " + statusCode.ToString(CultureInfo.InvariantCulture) + " " + statusText
                + "\r\nContent-Type: text/html; charset=utf-8"
                + "\r\nCache-Control: no-store"
                + "\r\nReferrer-Policy: no-referrer"
                + "\r\nContent-Security-Policy: default-src 'none'; script-src 'unsafe-inline'; connect-src 'self'"
                + "\r\nContent-Length: " + body.Length.ToString(CultureInfo.InvariantCulture)
                + "\r\nConnection: close\r\n\r\n");
            stream.Write(headers, 0, headers.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        private static string EscapeHtml(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private sealed class HttpRequestData
        {
            public HttpRequestData(string method, string target, string body)
            {
                Method = method;
                Target = target;
                Body = body;
            }

            public string Method { get; }
            public string Target { get; }
            public string Body { get; }
        }
    }
}
#endif
