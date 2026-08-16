using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoffeeGame.Integration
{
    public enum CoffeeLearningConnectionState
    {
        Unconnected,
        Connecting,
        Connected,
        Failed
    }

    public enum CoffeeLearningConfirmationIntent
    {
        None,
        Connect,
        Reconnect,
        Disconnect
    }

    public interface ICoffeeLearningDesktopConnectionService
    {
        Task ConnectAsync(CancellationToken cancellationToken = default);
        Task DisconnectAsync(CancellationToken cancellationToken = default);
    }

    public sealed class CoffeeLearningDesktopConnectException : Exception
    {
        public CoffeeLearningDesktopConnectException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }

    /// <summary>
    /// Pure settings presenter. It never opens a browser until a caller requests and confirms
    /// a connection action, and never exposes credential-bearing exception messages.
    /// </summary>
    public sealed class CoffeeLearningConnectionPresenter : IDisposable
    {
        private readonly ICoffeeGameAccessTokenStore tokenStore;
        private readonly ICoffeeLearningDesktopConnectionService connectionService;
        private readonly Func<ICoffeeGameAccessTokenProvider, ILearningBridge> bridgeFactory;

        private CancellationTokenSource operationCancellation;
        private CancellationTokenSource identityCancellation;
        private bool identityRefreshActive;
        private bool disposed;

        public CoffeeLearningConnectionPresenter(
            ICoffeeGameAccessTokenStore tokenStore,
            ICoffeeLearningDesktopConnectionService connectionService,
            Func<ICoffeeGameAccessTokenProvider, ILearningBridge> bridgeFactory)
        {
            this.tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
            this.connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            this.bridgeFactory = bridgeFactory ?? throw new ArgumentNullException(nameof(bridgeFactory));

            LearningBridge = new NullLearningBridge();
            RefreshFromStoredCredential();
        }

        public CoffeeLearningConnectionState State { get; private set; }
        public CoffeeLearningConfirmationIntent ConfirmationIntent { get; private set; }
        public string LastErrorCode { get; private set; } = string.Empty;
        public string AccountLabel { get; private set; } = string.Empty;
        public bool AccountLookupCompleted { get; private set; }
        public bool AccountIdentityVerified { get; private set; }
        public string AccountLookupErrorCode { get; private set; } = string.Empty;
        public ILearningBridge LearningBridge { get; private set; }

        public bool IsBridgeReady => LearningBridge != null && !(LearningBridge is NullLearningBridge);
        public bool HasStoredCredential => tokenStore.HasAccessToken;
        public bool IsConfirmationPending => ConfirmationIntent != CoffeeLearningConfirmationIntent.None;
        public bool ShouldRefreshAccountIdentity =>
            IsBridgeReady && !AccountLookupCompleted;

        public string StatusLabel
        {
            get
            {
                switch (State)
                {
                    case CoffeeLearningConnectionState.Connecting:
                        return "\u63a5\u7d9a\u4e2d";
                    case CoffeeLearningConnectionState.Connected:
                        if (!string.IsNullOrEmpty(AccountLabel))
                        {
                            return "\u63a5\u7d9a\u6e08\u307f\uff08" + AccountLabel + "\uff09";
                        }
                        return AccountLookupCompleted
                            ? "\u63a5\u7d9a\u6e08\u307f\uff08\u30a2\u30ab\u30a6\u30f3\u30c8\u78ba\u8a8d\u4e0d\u53ef\uff09"
                            : "\u63a5\u7d9a\u6e08\u307f\uff08\u30a2\u30ab\u30a6\u30f3\u30c8\u78ba\u8a8d\u4e2d\uff09";
                    case CoffeeLearningConnectionState.Failed:
                        return IsBridgeReady
                            ? "\u5931\u6557\uff08\u65e2\u5b58\u306e\u63a5\u7d9a\u306f\u5229\u7528\u53ef\u80fd\uff09"
                            : "\u5931\u6557";
                    default:
                        return "\u672a\u63a5\u7d9a";
                }
            }
        }

        public string PrimaryActionLabel
        {
            get
            {
                switch (ConfirmationIntent)
                {
                    case CoffeeLearningConfirmationIntent.Connect:
                        return "\u78ba\u8a8d: CoffeeLearning\u3068\u63a5\u7d9a\u3092\u958b\u59cb";
                    case CoffeeLearningConfirmationIntent.Reconnect:
                        return "\u78ba\u8a8d: CoffeeLearning\u3068\u518d\u63a5\u7d9a";
                    default:
                        return State == CoffeeLearningConnectionState.Connecting
                            ? "CoffeeLearning \u63a5\u7d9a\u4e2d"
                            : IsBridgeReady || HasStoredCredential
                                ? "CoffeeLearning\u3068\u518d\u63a5\u7d9a"
                                : "CoffeeLearning\u3068\u63a5\u7d9a";
                }
            }
        }

        public string DisconnectActionLabel =>
            ConfirmationIntent == CoffeeLearningConfirmationIntent.Disconnect
                ? "\u78ba\u8a8d: CoffeeLearning\u63a5\u7d9a\u3092\u89e3\u9664"
                : "CoffeeLearning\u63a5\u7d9a\u3092\u89e3\u9664";

        public string CancelActionLabel => State == CoffeeLearningConnectionState.Connecting
            ? "CoffeeLearning\u63a5\u7d9a\u3092\u4e2d\u6b62"
            : "CoffeeLearning\u64cd\u4f5c\u3092\u30ad\u30e3\u30f3\u30bb\u30eb";

        public bool CanUsePrimaryAction =>
            State != CoffeeLearningConnectionState.Connecting
            && (ConfirmationIntent == CoffeeLearningConfirmationIntent.None
                || ConfirmationIntent == CoffeeLearningConfirmationIntent.Connect
                || ConfirmationIntent == CoffeeLearningConfirmationIntent.Reconnect);

        public bool CanUseDisconnectAction =>
            State != CoffeeLearningConnectionState.Connecting
            && (IsBridgeReady || HasStoredCredential)
            && (ConfirmationIntent == CoffeeLearningConfirmationIntent.None
                || ConfirmationIntent == CoffeeLearningConfirmationIntent.Disconnect);

        public bool CanUseCancelAction =>
            State == CoffeeLearningConnectionState.Connecting || IsConfirmationPending;

        public async Task<bool> RefreshAccountIdentityAsync(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (!IsBridgeReady || identityRefreshActive)
            {
                return false;
            }

            identityRefreshActive = true;
            identityCancellation?.Dispose();
            identityCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var operation = identityCancellation;
            try
            {
                bool loaded = await LoadAccountHintAsync(operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                AccountLookupCompleted = true;
                AccountIdentityVerified = loaded;
                AccountLookupErrorCode = loaded ? string.Empty : "ACCOUNT_IDENTITY_UNAVAILABLE";
                return loaded;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch
            {
                AccountLookupCompleted = true;
                AccountIdentityVerified = false;
                AccountLookupErrorCode = "ACCOUNT_IDENTITY_UNAVAILABLE";
                AccountLabel = string.Empty;
                return false;
            }
            finally
            {
                identityRefreshActive = false;
                if (ReferenceEquals(identityCancellation, operation))
                {
                    identityCancellation.Dispose();
                    identityCancellation = null;
                }
                else
                {
                    operation.Dispose();
                }
            }
        }

        public async Task<string> TryApplyPastedAccessTokenAsync(string raw, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            try
            {
                if (!CoffeeGameAccessToken.TryExtract(raw, out string token))
                {
                    LastErrorCode = "PASTE_INVALID";
                    return "クリップボードに接続コードが見つかりませんでした。ブラウザの「接続コードをコピー」を押してから、もう一度コード貼付してください。";
                }

                await tokenStore.SaveAccessTokenAsync(token, cancellationToken).ConfigureAwait(true);
                RefreshFromStoredCredential();
                LastErrorCode = string.Empty;
                return "CoffeeLearningの接続コードを保存しました。 " + StatusLabel;
            }
            catch (Exception exception)
            {
                LastErrorCode = "PASTE_STORE_FAILED";
                return "接続コードは読めましたが保存できませんでした: " + exception.Message;
            }
        }

        public bool RequestPrimaryAction()
        {
            ThrowIfDisposed();
            if (!CanUsePrimaryAction || IsConfirmationPending)
            {
                return false;
            }

            LastErrorCode = string.Empty;
            ConfirmationIntent = IsBridgeReady || HasStoredCredential
                ? CoffeeLearningConfirmationIntent.Reconnect
                : CoffeeLearningConfirmationIntent.Connect;
            return true;
        }

        public bool RequestDisconnectAction()
        {
            ThrowIfDisposed();
            if (!CanUseDisconnectAction || IsConfirmationPending)
            {
                return false;
            }

            LastErrorCode = string.Empty;
            ConfirmationIntent = CoffeeLearningConfirmationIntent.Disconnect;
            return true;
        }

        public Task<bool> ConfirmPrimaryActionAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (ConfirmationIntent != CoffeeLearningConfirmationIntent.Connect
                && ConfirmationIntent != CoffeeLearningConfirmationIntent.Reconnect)
            {
                return Task.FromResult(false);
            }

            return RunConnectAsync(cancellationToken);
        }

        public Task<bool> ConfirmDisconnectActionAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (ConfirmationIntent != CoffeeLearningConfirmationIntent.Disconnect)
            {
                return Task.FromResult(false);
            }

            return RunDisconnectAsync(cancellationToken);
        }

        public void CancelPendingOrActiveAction()
        {
            if (disposed)
            {
                return;
            }

            ConfirmationIntent = CoffeeLearningConfirmationIntent.None;
            operationCancellation?.Cancel();
            identityCancellation?.Cancel();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            ConfirmationIntent = CoffeeLearningConfirmationIntent.None;
            operationCancellation?.Cancel();
            operationCancellation?.Dispose();
            operationCancellation = null;
            identityCancellation?.Cancel();
            identityCancellation?.Dispose();
            identityCancellation = null;
        }

        private async Task<bool> RunConnectAsync(CancellationToken cancellationToken)
        {
            if (State == CoffeeLearningConnectionState.Connecting)
            {
                return false;
            }

            ConfirmationIntent = CoffeeLearningConfirmationIntent.None;
            LastErrorCode = string.Empty;
            State = CoffeeLearningConnectionState.Connecting;
            var previousBridge = LearningBridge;
            var hadWorkingBridge = IsBridgeReady;
            var operation = CreateOperationCancellation(cancellationToken);
            try
            {
                await connectionService.ConnectAsync(operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                if (!tokenStore.HasAccessToken)
                {
                    throw new CoffeeGameCredentialException(
                        "CoffeeLearning connection completed without a stored credential.");
                }

                LearningBridge = bridgeFactory(tokenStore)
                    ?? throw new InvalidOperationException("CoffeeLearning bridge factory returned no bridge.");
                State = CoffeeLearningConnectionState.Connected;
                AccountLabel = string.Empty;
                AccountLookupCompleted = false;
                AccountIdentityVerified = false;
                AccountLookupErrorCode = string.Empty;
                await RefreshAccountIdentityAsync(operation.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                LearningBridge = hadWorkingBridge ? previousBridge : new NullLearningBridge();
                RestoreAvailableState();
                return false;
            }
            catch (Exception exception)
            {
                LearningBridge = hadWorkingBridge ? previousBridge : new NullLearningBridge();
                LastErrorCode = GetSafeErrorCode(exception);
                State = CoffeeLearningConnectionState.Failed;
                return false;
            }
            finally
            {
                ReleaseOperationCancellation(operation);
            }
        }

        private async Task<bool> RunDisconnectAsync(CancellationToken cancellationToken)
        {
            if (State == CoffeeLearningConnectionState.Connecting)
            {
                return false;
            }

            ConfirmationIntent = CoffeeLearningConfirmationIntent.None;
            LastErrorCode = string.Empty;
            State = CoffeeLearningConnectionState.Connecting;
            identityCancellation?.Cancel();
            var previousBridge = LearningBridge;
            var operation = CreateOperationCancellation(cancellationToken);
            try
            {
                await connectionService.DisconnectAsync(operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                LearningBridge = new NullLearningBridge();
                AccountLabel = string.Empty;
                AccountLookupCompleted = false;
                AccountIdentityVerified = false;
                AccountLookupErrorCode = string.Empty;
                State = CoffeeLearningConnectionState.Unconnected;
                return true;
            }
            catch (OperationCanceledException)
            {
                LearningBridge = previousBridge;
                RestoreAvailableState();
                return false;
            }
            catch (Exception exception)
            {
                LearningBridge = previousBridge;
                LastErrorCode = GetSafeErrorCode(exception);
                State = CoffeeLearningConnectionState.Failed;
                return false;
            }
            finally
            {
                ReleaseOperationCancellation(operation);
            }
        }

        private CancellationTokenSource CreateOperationCancellation(CancellationToken cancellationToken)
        {
            operationCancellation?.Dispose();
            operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return operationCancellation;
        }

        private void ReleaseOperationCancellation(CancellationTokenSource operation)
        {
            if (!ReferenceEquals(operationCancellation, operation))
            {
                operation.Dispose();
                return;
            }

            operationCancellation.Dispose();
            operationCancellation = null;
        }

        private void RefreshFromStoredCredential()
        {
            if (!tokenStore.HasAccessToken)
            {
                LearningBridge = new NullLearningBridge();
                AccountLabel = string.Empty;
                AccountLookupCompleted = false;
                AccountIdentityVerified = false;
                AccountLookupErrorCode = string.Empty;
                State = CoffeeLearningConnectionState.Unconnected;
                return;
            }

            try
            {
                LearningBridge = bridgeFactory(tokenStore)
                    ?? throw new InvalidOperationException("CoffeeLearning bridge factory returned no bridge.");
                State = CoffeeLearningConnectionState.Connected;
            }
            catch (Exception exception)
            {
                LearningBridge = new NullLearningBridge();
                LastErrorCode = GetSafeErrorCode(exception);
                State = CoffeeLearningConnectionState.Failed;
            }
        }

        private void RestoreAvailableState()
        {
            State = IsBridgeReady || tokenStore.HasAccessToken
                ? CoffeeLearningConnectionState.Connected
                : CoffeeLearningConnectionState.Unconnected;
        }

        private async Task<bool> LoadAccountHintAsync(CancellationToken cancellationToken)
        {
            try
            {
                string token = await tokenStore.LoadAccessTokenAsync(cancellationToken);
                if (CoffeeGameAccessToken.TryGetAccountEmail(token, out string email))
                {
                    AccountLabel = email;
                    return true;
                }
            }
            catch
            {
                // A local hint is optional. Authentication remains server-authoritative.
            }

            AccountLabel = string.Empty;
            return false;
        }

        private static string GetSafeErrorCode(Exception exception)
        {
            if (exception is CoffeeLearningDesktopConnectException connectException)
            {
                switch (connectException.Code)
                {
                    case "CONNECT_TIMEOUT":
                    case "CONNECT_INCOMPLETE":
                    case "CONNECT_RESPONSE_TOO_LARGE":
                    case "CONNECT_RESPONSE_INVALID":
                        return connectException.Code;
                }
            }

            if (exception is CoffeeGameCredentialException)
            {
                return "CREDENTIAL_UNAVAILABLE";
            }

            if (exception is PlatformNotSupportedException)
            {
                return "PLATFORM_UNSUPPORTED";
            }

            return "CONNECT_FAILED";
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(CoffeeLearningConnectionPresenter));
            }
        }
    }

    public sealed class UnsupportedCoffeeLearningDesktopConnectionService
        : ICoffeeLearningDesktopConnectionService
    {
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new PlatformNotSupportedException(
                "Secure CoffeeLearning browser connection is not configured for this platform.");
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    public static class CoffeeLearningConnectionComposition
    {
        public static CoffeeLearningConnectionPresenter CreateProduction()
        {
            var store = CoffeeGameAccessTokenStoreFactory.CreatePlatformDefault();
#if UNITY_ANDROID && !UNITY_EDITOR
            ICoffeeLearningDesktopConnectionService connection =
                new AndroidCoffeeLearningConnectService(
                    new CoffeeLearningDesktopConnectOptions(),
                    store,
                    new UnityCoffeeGameBrowserLauncher());
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            ICoffeeLearningDesktopConnectionService connection =
                new CoffeeLearningDesktopConnectService(
                    new CoffeeLearningDesktopConnectOptions(),
                    store,
                    new UnityCoffeeGameBrowserLauncher());
#else
            ICoffeeLearningDesktopConnectionService connection =
                new UnsupportedCoffeeLearningDesktopConnectionService();
#endif
            return new CoffeeLearningConnectionPresenter(
                store,
                connection,
                provider => new CoffeeLearningHttpBridge(
                    new CoffeeLearningHttpBridgeOptions(),
                    provider,
                    new UnityWebRequestCoffeeGameTransport()));
        }
    }
}
