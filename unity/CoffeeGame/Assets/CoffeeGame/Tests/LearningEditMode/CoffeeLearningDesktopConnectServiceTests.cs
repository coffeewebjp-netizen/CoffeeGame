#if UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace CoffeeGame.Integration.Tests
{
    public sealed class CoffeeLearningDesktopConnectServiceTests
    {
        private const string TestToken = "cgt_loopback-id.loopback-secret";

        [Test]
        public async Task ConnectUsesFrozenProviderUrlVerifiesStateAndStoresBearer()
        {
            var store = new MemoryTokenStore();
            var launcher = new LoopbackBrowserLauncher(TestToken, sendWrongStateFirst: true);
            var service = new CoffeeLearningDesktopConnectService(
                new CoffeeLearningDesktopConnectOptions(),
                store,
                launcher);

            await service.ConnectAsync();
            await launcher.CallbackTask;

            Assert.That(
                launcher.LaunchedUri.GetLeftPart(UriPartial.Path),
                Is.EqualTo("https://www.coffeewebjp.com/api/coffee-game/connect"));
            var query = ParseUrlEncoded(launcher.LaunchedUri.Query.TrimStart('?'));
            Assert.That(query["redirect_uri"], Does.Match("^http://127\\.0\\.0\\.1:[0-9]+/coffee-game-callback$"));
            Assert.That(query["state"].Length, Is.InRange(32, 200));
            Assert.That(query["state"], Does.Match("^[A-Za-z0-9_-]+$"));
            CollectionAssert.AreEquivalent(new[] { "redirect_uri", "state" }, query.Keys);
            Assert.That(launcher.LaunchedUri.AbsoluteUri, Does.Not.Contain(TestToken));
            Assert.That(store.Token, Is.EqualTo(TestToken));

            await service.DisconnectAsync();
            Assert.That(store.Token, Is.Null);
        }

        [Test]
        public void ConnectObservesCallerCancellationAndDoesNotSaveToken()
        {
            var store = new MemoryTokenStore();
            var launcher = new NoCallbackBrowserLauncher();
            var service = new CoffeeLearningDesktopConnectService(
                new CoffeeLearningDesktopConnectOptions(timeoutSeconds: 30),
                store,
                launcher);
            var cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await service.ConnectAsync(cancellation.Token));
            Assert.That(store.Token, Is.Null);
        }

        private static Dictionary<string, string> ParseUrlEncoded(string value)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in value.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=');
                var key = separator >= 0 ? pair.Substring(0, separator) : pair;
                var itemValue = separator >= 0 ? pair.Substring(separator + 1) : string.Empty;
                result[Uri.UnescapeDataString(key.Replace('+', ' '))] =
                    Uri.UnescapeDataString(itemValue.Replace('+', ' '));
            }

            return result;
        }

        private sealed class MemoryTokenStore : ICoffeeGameAccessTokenStore
        {
            public string Token { get; private set; }
            public bool HasAccessToken => Token != null;

            public Task<string> LoadAccessTokenAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Token);
            }

            public Task SaveAccessTokenAsync(
                string accessToken,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Token = CoffeeGameAccessToken.Normalize(accessToken);
                return Task.CompletedTask;
            }

            public Task DeleteAccessTokenAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Token = null;
                return Task.CompletedTask;
            }
        }

        private sealed class NoCallbackBrowserLauncher : ICoffeeGameBrowserLauncher
        {
            public void Open(Uri uri)
            {
            }
        }

        private sealed class LoopbackBrowserLauncher : ICoffeeGameBrowserLauncher
        {
            private readonly string token;
            private readonly bool sendWrongStateFirst;

            public LoopbackBrowserLauncher(string token, bool sendWrongStateFirst)
            {
                this.token = token;
                this.sendWrongStateFirst = sendWrongStateFirst;
            }

            public Uri LaunchedUri { get; private set; }
            public Task CallbackTask { get; private set; }

            public void Open(Uri uri)
            {
                LaunchedUri = uri;
                CallbackTask = Task.Run(async () =>
                {
                    var query = ParseUrlEncoded(uri.Query.TrimStart('?'));
                    var redirectUri = new Uri(query["redirect_uri"]);
                    if (sendWrongStateFirst)
                    {
                        await PostCallbackAsync(redirectUri, "wrong-state", token);
                    }

                    await PostCallbackAsync(redirectUri, query["state"], token);
                });
            }

            private static async Task PostCallbackAsync(Uri redirectUri, string state, string token)
            {
                var body = "state=" + Uri.EscapeDataString(state)
                    + "&bearer=" + Uri.EscapeDataString("Bearer " + token);
                var bodyBytes = Encoding.UTF8.GetBytes(body);
                var requestBytes = Encoding.ASCII.GetBytes(
                    "POST /coffee-game-callback/receive HTTP/1.1\r\n"
                    + "Host: 127.0.0.1\r\n"
                    + "Content-Type: application/x-www-form-urlencoded\r\n"
                    + "Content-Length: " + bodyBytes.Length + "\r\n"
                    + "Connection: close\r\n\r\n");

                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(redirectUri.Host, redirectUri.Port);
                    using (var stream = client.GetStream())
                    {
                        await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
                        await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
                        await stream.FlushAsync();
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            await reader.ReadToEndAsync();
                        }
                    }
                }
            }
        }
    }
}
#endif
