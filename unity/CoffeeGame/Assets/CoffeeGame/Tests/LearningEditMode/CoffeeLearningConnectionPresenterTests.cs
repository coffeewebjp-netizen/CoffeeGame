using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace CoffeeGame.Integration.Tests
{
    public sealed class CoffeeLearningConnectionPresenterTests
    {
        [Test]
        public void StoredCredentialCreatesReadyBridgeWithoutOpeningBrowser()
        {
            var store = new MemoryTokenStore("cgt_stored-id.stored-secret");
            var connection = new FakeConnectionService(store);
            var bridge = new FakeLearningBridge();

            using (var presenter = new CoffeeLearningConnectionPresenter(
                store,
                connection,
                _ => bridge))
            {
                Assert.That(presenter.State, Is.EqualTo(CoffeeLearningConnectionState.Connected));
                Assert.That(
                    presenter.StatusLabel,
                    Is.EqualTo("\u63a5\u7d9a\u6e08\u307f\uff08\u30a2\u30ab\u30a6\u30f3\u30c8\u78ba\u8a8d\u4e2d\uff09"));
                Assert.That(presenter.IsBridgeReady, Is.True);
                Assert.That(presenter.LearningBridge, Is.SameAs(bridge));
                Assert.That(connection.ConnectCalls, Is.Zero);
            }
        }

        [Test]
        public async Task AccountIdentityIsDisplayedFromDedicatedTokenWithoutNetworkLookup()
        {
            const string token =
                "cgt_cGxheWVyQGV4YW1wbGUuY29t.cgtok_test.abcdefghijklmnopqrstuvwxyz0123456789";
            var store = new MemoryTokenStore(token);
            var connection = new FakeConnectionService(store);
            var bridge = new FakeLearningBridge();
            using (var presenter = new CoffeeLearningConnectionPresenter(
                store,
                connection,
                _ => bridge))
            {
                Assert.That(await presenter.RefreshAccountIdentityAsync(), Is.True);
                Assert.That(presenter.AccountLabel, Is.EqualTo("player@example.com"));
                Assert.That(presenter.AccountIdentityVerified, Is.True);
                Assert.That(bridge.AccountIdentityCalls, Is.Zero);
                Assert.That(
                    presenter.StatusLabel,
                    Is.EqualTo("\u63a5\u7d9a\u6e08\u307f\uff08player@example.com\uff09"));
            }
        }

        [Test]
        public async Task InvalidTokenSubjectFailsClosedWithoutNetworkLookup()
        {
            var store = new MemoryTokenStore("cgt_invalid-subject.token.secret");
            var bridge = new FakeLearningBridge();
            using (var presenter = new CoffeeLearningConnectionPresenter(
                store,
                new FakeConnectionService(store),
                _ => bridge))
            {
                Assert.That(await presenter.RefreshAccountIdentityAsync(), Is.False);
                Assert.That(presenter.AccountLabel, Is.Empty);
                Assert.That(presenter.AccountIdentityVerified, Is.False);
                Assert.That(presenter.AccountLookupErrorCode, Is.EqualTo("ACCOUNT_IDENTITY_UNAVAILABLE"));
                Assert.That(presenter.ShouldRefreshAccountIdentity, Is.False);
                Assert.That(bridge.AccountIdentityCalls, Is.Zero);
            }
        }

        [Test]
        public async Task ConnectRequiresConfirmationAndSuppressesDoubleConfirmation()
        {
            var store = new MemoryTokenStore();
            var connection = new FakeConnectionService(store) { DeferConnect = true };
            using (var presenter = CreatePresenter(store, connection))
            {
                Assert.That(presenter.RequestPrimaryAction(), Is.True);
                Assert.That(presenter.ConfirmationIntent, Is.EqualTo(CoffeeLearningConfirmationIntent.Connect));
                Assert.That(connection.ConnectCalls, Is.Zero);

                Task<bool> first = presenter.ConfirmPrimaryActionAsync();
                Task<bool> duplicate = presenter.ConfirmPrimaryActionAsync();
                Assert.That(presenter.State, Is.EqualTo(CoffeeLearningConnectionState.Connecting));
                Assert.That(connection.ConnectCalls, Is.EqualTo(1));
                Assert.That(await duplicate, Is.False);

                connection.CompleteConnect("cgt_new-id.new-secret");
                Assert.That(await first, Is.True);
                Assert.That(presenter.State, Is.EqualTo(CoffeeLearningConnectionState.Connected));
                Assert.That(presenter.IsBridgeReady, Is.True);
            }
        }

        [Test]
        public async Task CancelStopsActiveConnectAndReturnsToUnconnected()
        {
            var store = new MemoryTokenStore();
            var connection = new FakeConnectionService(store) { DeferConnect = true };
            using (var presenter = CreatePresenter(store, connection))
            {
                presenter.RequestPrimaryAction();
                Task<bool> connect = presenter.ConfirmPrimaryActionAsync();

                presenter.CancelPendingOrActiveAction();

                Assert.That(await connect, Is.False);
                Assert.That(connection.SawConnectCancellation, Is.True);
                Assert.That(presenter.State, Is.EqualTo(CoffeeLearningConnectionState.Unconnected));
                Assert.That(presenter.IsBridgeReady, Is.False);
            }
        }

        [Test]
        public async Task FailureUsesAllowlistedCodeAndNeverExposesExceptionMessage()
        {
            const string secret = "cgt_private-id.private-secret";
            var store = new MemoryTokenStore();
            var connection = new FakeConnectionService(store)
            {
                ConnectException = new InvalidOperationException("provider echoed " + secret)
            };
            using (var presenter = CreatePresenter(store, connection))
            {
                presenter.RequestPrimaryAction();
                Assert.That(await presenter.ConfirmPrimaryActionAsync(), Is.False);

                Assert.That(presenter.State, Is.EqualTo(CoffeeLearningConnectionState.Failed));
                Assert.That(presenter.LastErrorCode, Is.EqualTo("CONNECT_FAILED"));
                Assert.That(presenter.StatusLabel, Does.Not.Contain(secret));
                Assert.That(presenter.LastErrorCode, Does.Not.Contain(secret));
            }
        }

        [Test]
        public async Task FailedReconnectRetainsPriorBridgeAndStoredCredential()
        {
            const string original = "cgt_original-id.original-secret";
            var store = new MemoryTokenStore(original);
            var connection = new FakeConnectionService(store)
            {
                ConnectException = new CoffeeLearningDesktopConnectException(
                    "CONNECT_TIMEOUT",
                    "unsafe " + original)
            };
            var originalBridge = new FakeLearningBridge();
            using (var presenter = new CoffeeLearningConnectionPresenter(
                store,
                connection,
                _ => originalBridge))
            {
                Assert.That(presenter.RequestPrimaryAction(), Is.True);
                Assert.That(presenter.ConfirmationIntent, Is.EqualTo(CoffeeLearningConfirmationIntent.Reconnect));
                Assert.That(await presenter.ConfirmPrimaryActionAsync(), Is.False);

                Assert.That(presenter.State, Is.EqualTo(CoffeeLearningConnectionState.Failed));
                Assert.That(presenter.StatusLabel, Is.EqualTo("失敗（既存の接続は利用可能）"));
                Assert.That(presenter.LastErrorCode, Is.EqualTo("CONNECT_TIMEOUT"));
                Assert.That(presenter.LearningBridge, Is.SameAs(originalBridge));
                Assert.That(store.Token, Is.EqualTo(original));
            }
        }

        [Test]
        public async Task ReconnectThenDeliberateDisconnectRefreshesBridgeReadiness()
        {
            var store = new MemoryTokenStore("cgt_old-id.old-secret");
            var connection = new FakeConnectionService(store);
            int bridgeGeneration = 0;
            using (var presenter = new CoffeeLearningConnectionPresenter(
                store,
                connection,
                _ => new FakeLearningBridge(++bridgeGeneration)))
            {
                ILearningBridge initial = presenter.LearningBridge;
                Assert.That(presenter.RequestPrimaryAction(), Is.True);
                Assert.That(connection.ConnectCalls, Is.Zero);
                Assert.That(await presenter.ConfirmPrimaryActionAsync(), Is.True);
                Assert.That(presenter.LearningBridge, Is.Not.SameAs(initial));
                Assert.That(connection.ConnectCalls, Is.EqualTo(1));

                Assert.That(presenter.RequestDisconnectAction(), Is.True);
                Assert.That(connection.DisconnectCalls, Is.Zero);
                Assert.That(presenter.ConfirmationIntent, Is.EqualTo(CoffeeLearningConfirmationIntent.Disconnect));
                Assert.That(await presenter.ConfirmDisconnectActionAsync(), Is.True);
                Assert.That(connection.DisconnectCalls, Is.EqualTo(1));
                Assert.That(presenter.State, Is.EqualTo(CoffeeLearningConnectionState.Unconnected));
                Assert.That(presenter.IsBridgeReady, Is.False);
                Assert.That(store.HasAccessToken, Is.False);
            }
        }

        [Test]
        public void CancelingConfirmationDoesNotDeleteCompletedCredential()
        {
            const string token = "cgt_existing-id.existing-secret";
            var store = new MemoryTokenStore(token);
            var connection = new FakeConnectionService(store);
            using (var presenter = CreatePresenter(store, connection))
            {
                presenter.RequestDisconnectAction();
                presenter.CancelPendingOrActiveAction();

                Assert.That(presenter.State, Is.EqualTo(CoffeeLearningConnectionState.Connected));
                Assert.That(presenter.ConfirmationIntent, Is.EqualTo(CoffeeLearningConfirmationIntent.None));
                Assert.That(store.Token, Is.EqualTo(token));
                Assert.That(connection.DisconnectCalls, Is.Zero);
            }
        }

        private static CoffeeLearningConnectionPresenter CreatePresenter(
            MemoryTokenStore store,
            FakeConnectionService connection)
        {
            return new CoffeeLearningConnectionPresenter(
                store,
                connection,
                _ => new FakeLearningBridge());
        }

        private sealed class MemoryTokenStore : ICoffeeGameAccessTokenStore
        {
            public MemoryTokenStore(string token = null)
            {
                Token = token;
            }

            public string Token { get; set; }
            public bool HasAccessToken => !string.IsNullOrEmpty(Token);

            public Task<string> LoadAccessTokenAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Token);
            }

            public Task SaveAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
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

        private sealed class FakeConnectionService : ICoffeeLearningDesktopConnectionService
        {
            private readonly MemoryTokenStore store;
            private TaskCompletionSource<string> connectCompletion;

            public FakeConnectionService(MemoryTokenStore store)
            {
                this.store = store;
            }

            public bool DeferConnect { get; set; }
            public Exception ConnectException { get; set; }
            public int ConnectCalls { get; private set; }
            public int DisconnectCalls { get; private set; }
            public bool SawConnectCancellation { get; private set; }

            public async Task ConnectAsync(CancellationToken cancellationToken = default)
            {
                ConnectCalls++;
                if (ConnectException != null)
                {
                    throw ConnectException;
                }

                if (!DeferConnect)
                {
                    store.Token = "cgt_connected-id.connected-secret";
                    return;
                }

                connectCompletion = new TaskCompletionSource<string>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellationToken.Register(() =>
                {
                    SawConnectCancellation = true;
                    connectCompletion.TrySetCanceled();
                }))
                {
                    store.Token = await connectCompletion.Task;
                }
            }

            public Task DisconnectAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DisconnectCalls++;
                store.Token = null;
                return Task.CompletedTask;
            }

            public void CompleteConnect(string token)
            {
                connectCompletion.TrySetResult(token);
            }
        }

        private sealed class FakeLearningBridge : ILearningBridge
        {
            public FakeLearningBridge(int generation = 0)
            {
                Generation = generation;
                AccountResponse = new AccountIdentityResponseDto
                {
                    contractVersion = CoffeeGameContractV1.Version,
                    account = new CoffeeGameAccountDto { email = "player@example.com" }
                };
            }

            public int Generation { get; }
            public AccountIdentityResponseDto AccountResponse { get; set; }
            public int AccountIdentityCalls { get; private set; }
            public bool IsSignedIn => true;
            public Task<LearningClaimResult> ClaimTodayAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(new LearningClaimResult(string.Empty, 0, false));
            public Task<AccountIdentityResponseDto> GetAccountIdentityAsync(CancellationToken cancellationToken = default)
            {
                AccountIdentityCalls++;
                return Task.FromResult(AccountResponse);
            }
            public Task<WeakSyncResponseDto> SyncWeakItemsAsync(WeakSyncRequestDto request, CancellationToken cancellationToken = default) =>
                Task.FromResult(new WeakSyncResponseDto { contractVersion = CoffeeGameContractV1.Version });
            public Task<ChallengeIssueResponseDto> IssueChallengeAsync(ChallengeIssueRequestDto request, CancellationToken cancellationToken = default) =>
                Task.FromResult(new ChallengeIssueResponseDto { contractVersion = CoffeeGameContractV1.Version });
            public Task<AnswerResultResponseDto> SubmitAnswerAsync(AnswerSubmitRequestDto request, CancellationToken cancellationToken = default) =>
                Task.FromResult(new AnswerResultResponseDto { contractVersion = CoffeeGameContractV1.Version });
            public Task<AnswerResultResponseDto> RecoverResultAsync(string resultId, CancellationToken cancellationToken = default) =>
                Task.FromResult(new AnswerResultResponseDto { contractVersion = CoffeeGameContractV1.Version });
        }
    }
}
