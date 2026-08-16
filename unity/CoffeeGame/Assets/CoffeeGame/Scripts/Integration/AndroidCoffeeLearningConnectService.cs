#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CoffeeGame.Integration
{
    public sealed class AndroidCoffeeLearningConnectService : ICoffeeLearningDesktopConnectionService
    {
        public const string AppCallbackUri = CoffeeGameDeepLink.AppCallback;

        private readonly CoffeeLearningDesktopConnectOptions options;
        private readonly ICoffeeGameAccessTokenStore tokenStore;
        private readonly ICoffeeGameBrowserLauncher browserLauncher;

        public AndroidCoffeeLearningConnectService(
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
            string state = CreateState();
            var redirectUri = new Uri(AppCallbackUri, UriKind.Absolute);
            Uri connectUri = BuildConnectUri(redirectUri, state);
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnDeepLink(string url)
            {
                if (CoffeeGameDeepLink.TryParseCallback(url, state, out string token, out string error)
                    && !string.IsNullOrEmpty(token))
                {
                    completion.TrySetResult(token);
                    return;
                }

                if (!string.IsNullOrEmpty(error) && url != null && url.IndexOf(state, StringComparison.Ordinal) >= 0)
                {
                    completion.TrySetException(new CoffeeLearningDesktopConnectException("CONNECT_REJECTED", error));
                }
            }

            CoffeeGameDeepLink.Clear();
            CoffeeGameDeepLink.Received += OnDeepLink;
            try
            {
                browserLauncher.Open(connectUri);
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
                    using (timeout.Token.Register(() => completion.TrySetCanceled(timeout.Token)))
                    {
                        while (!completion.Task.IsCompleted)
                        {
                            CoffeeGameDeepLinkListener.PollNativeIntent();
                            if (!string.IsNullOrEmpty(Application.absoluteURL))
                            {
                                OnDeepLink(Application.absoluteURL);
                            }

                            if (completion.Task.IsCompleted)
                            {
                                break;
                            }

                            await Task.Yield();
                            timeout.Token.ThrowIfCancellationRequested();
                        }

                        string token = await completion.Task.ConfigureAwait(true);
                        await tokenStore.SaveAccessTokenAsync(token, cancellationToken).ConfigureAwait(true);
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new CoffeeLearningDesktopConnectException(
                    "CONNECT_TIMEOUT",
                    "ブラウザからCoffeeGAMEへ戻れませんでした。CoffeeLearningの本番がスマホ用の戻り先を許可しているか確認してください。");
            }
            finally
            {
                CoffeeGameDeepLink.Received -= OnDeepLink;
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

        private static string CreateState()
        {
            var bytes = new byte[32];
            using (var random = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

    }
}
#endif
