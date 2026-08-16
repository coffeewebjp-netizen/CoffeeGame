using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace CoffeeGame.Integration.Tests
{
    public sealed class CoffeeLearningHttpBridgeTests
    {
        private const string TestToken = "cgt_test-id.test-secret";

        [Test]
        public async Task AccountIdentityUsesSelfOnlyV1Endpoint()
        {
            var transport = new FakeTransport(
                new CoffeeGameHttpResponse(
                    200,
                    "{\"contractVersion\":\"1.0\",\"account\":{\"email\":\"player@example.com\"}}"));
            var bridge = CreateBridge(transport);

            var response = await bridge.GetAccountIdentityAsync();

            Assert.That(response.error, Is.Null);
            Assert.That(response.account.email, Is.EqualTo("player@example.com"));
            Assert.That(
                transport.LastRequest.Uri.AbsoluteUri,
                Is.EqualTo("https://www.coffeewebjp.com/api/integrations/coffee-game/v1/account"));
            Assert.That(transport.LastRequest.JsonBody, Is.Null);
            Assert.That(transport.LastRequest.Uri.AbsoluteUri, Does.Not.Contain(TestToken));
        }

        [Test]
        public async Task WeakSync_UsesProductionV1PathAndDefaultLookback()
        {
            var transport = new FakeTransport(
                new CoffeeGameHttpResponse(
                    200,
                    "{\"contractVersion\":\"1.0\",\"items\":[],\"hasMore\":false,\"syncAfterSeconds\":900}"));
            var bridge = CreateBridge(transport);

            var response = await bridge.SyncWeakItemsAsync(new WeakSyncRequestDto());

            Assert.That(response.error, Is.Null);
            Assert.That(
                transport.LastRequest.Uri.AbsoluteUri,
                Is.EqualTo(
                    "https://www.coffeewebjp.com/api/integrations/coffee-game/v1/weak-items"
                    + "?limit=50&lookbackDays=14"));
            Assert.That(transport.LastAccessToken, Is.EqualTo(TestToken));
            Assert.That(transport.LastRequest.ToString(), Does.Not.Contain(TestToken));
        }

        [Test]
        public async Task WeakSync_UsesCustomLookbackAndEscapesCursor()
        {
            var transport = new FakeTransport(
                new CoffeeGameHttpResponse(200, "{\"contractVersion\":\"1.0\",\"items\":[]}"));
            var bridge = CreateBridge(transport, "https://learning.example.test/custom/v1");

            await bridge.SyncWeakItemsAsync(new WeakSyncRequestDto
            {
                cursor = "next cursor/+",
                limit = 25,
                lookbackDays = 7
            });

            Assert.That(
                transport.LastRequest.Uri.AbsoluteUri,
                Is.EqualTo(
                    "https://learning.example.test/custom/v1/weak-items"
                    + "?cursor=next%20cursor%2F%2B&limit=25&lookbackDays=7"));
        }

        [TestCase(0)]
        [TestCase(31)]
        public async Task WeakSync_RejectsOutOfRangeLookbackBeforeTransport(int lookbackDays)
        {
            var transport = new FakeTransport(
                new CoffeeGameHttpResponse(500, string.Empty));
            var bridge = CreateBridge(transport);

            var response = await bridge.SyncWeakItemsAsync(new WeakSyncRequestDto
            {
                lookbackDays = lookbackDays
            });

            Assert.That(response.error.code, Is.EqualTo("INVALID_REQUEST"));
            Assert.That(transport.CallCount, Is.Zero);
        }

        [Test]
        public async Task PostBodiesPreserveCallerIdsAndRecoveryEscapesResultId()
        {
            var transport = new FakeTransport(
                new CoffeeGameHttpResponse(
                    201,
                    "{\"contractVersion\":\"1.0\",\"challenge\":{\"challengeId\":\"ch_1\"}}"));
            var bridge = CreateBridge(transport);

            await bridge.IssueChallengeAsync(new ChallengeIssueRequestDto
            {
                weakItemId = "wi_1",
                clientRequestId = "caller-request-001"
            });
            Assert.That(transport.LastRequest.JsonBody, Does.Contain("\"clientRequestId\":\"caller-request-001\""));

            transport.Response = new CoffeeGameHttpResponse(
                202,
                "{\"contractVersion\":\"1.0\",\"result\":{\"resultId\":\"rs_1\",\"status\":\"pending\"}}" );
            await bridge.SubmitAnswerAsync(new AnswerSubmitRequestDto
            {
                challengeId = "ch_1",
                clientAttemptId = "caller-attempt-001",
                answer = new CoffeeGameAnswerDto
                {
                    text = "answer",
                    inputMode = CoffeeGameContractV1.TypedInputMode
                }
            });
            Assert.That(transport.LastRequest.JsonBody, Does.Contain("\"clientAttemptId\":\"caller-attempt-001\""));

            transport.Response = new CoffeeGameHttpResponse(
                200,
                "{\"contractVersion\":\"1.0\",\"result\":{\"resultId\":\"rs_1\",\"status\":\"completed\"}}" );
            var recovered = await bridge.RecoverResultAsync("rs/one");
            Assert.That(recovered.result.status, Is.EqualTo(CoffeeGameContractV1.CompletedStatus));
            Assert.That(transport.LastRequest.Uri.AbsolutePath, Does.EndWith("/results/rs%2Fone"));
        }

        [Test]
        public async Task ProviderErrorIsMappedAndCredentialIsRedacted()
        {
            var body = "{\"contractVersion\":\"1.0\",\"error\":{"
                + "\"code\":\"RATE_LIMITED\","
                + "\"message\":\"Do not echo " + TestToken + "\","
                + "\"retryable\":true,\"fields\":[]}}";
            var bridge = CreateBridge(new FakeTransport(new CoffeeGameHttpResponse(429, body)));

            var response = await bridge.SyncWeakItemsAsync(new WeakSyncRequestDto());

            Assert.That(response.error.code, Is.EqualTo("RATE_LIMITED"));
            Assert.That(response.error.retryable, Is.True);
            Assert.That(response.error.message, Does.Contain("[REDACTED]"));
            Assert.That(response.error.message, Does.Not.Contain(TestToken));
        }

        [TestCase(401, "AUTHENTICATION_REQUIRED", false)]
        [TestCase(408, "REQUEST_TIMEOUT", true)]
        [TestCase(409, "REQUEST_CONFLICT", false)]
        [TestCase(500, "SERVICE_UNAVAILABLE", true)]
        public async Task NonProviderHttpFailuresUseSafeStatusMapping(
            int statusCode,
            string expectedCode,
            bool retryable)
        {
            var bridge = CreateBridge(new FakeTransport(
                new CoffeeGameHttpResponse(statusCode, "not-json " + TestToken)));

            var response = await bridge.SyncWeakItemsAsync(new WeakSyncRequestDto());

            Assert.That(response.error.code, Is.EqualTo(expectedCode));
            Assert.That(response.error.retryable, Is.EqualTo(retryable));
            Assert.That(response.error.message, Does.Not.Contain(TestToken));
        }

        [Test]
        public async Task TransportTimeoutBecomesRetryableSafeError()
        {
            var transport = new FakeTransport(null)
            {
                Exception = new CoffeeGameHttpTransportException("REQUEST_TIMEOUT", true)
            };
            var bridge = CreateBridge(transport);

            var response = await bridge.SyncWeakItemsAsync(new WeakSyncRequestDto());

            Assert.That(response.error.code, Is.EqualTo("REQUEST_TIMEOUT"));
            Assert.That(response.error.retryable, Is.True);
            Assert.That(response.error.message, Does.Not.Contain(TestToken));
        }

        [Test]
        public void CallerCancellationIsNotConvertedToProviderError()
        {
            var bridge = CreateBridge(new FakeTransport(
                new CoffeeGameHttpResponse(200, "{\"contractVersion\":\"1.0\"}")));
            var cancellation = new CancellationToken(true);

            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await bridge.SyncWeakItemsAsync(new WeakSyncRequestDto(), cancellation));
        }

        [Test]
        public void UnsupportedContractVersionIsRejected()
        {
            var bridge = CreateBridge(new FakeTransport(
                new CoffeeGameHttpResponse(200, "{\"contractVersion\":\"2.0\",\"items\":[]}")));

            Assert.ThrowsAsync<UnsupportedContractVersionException>(async () =>
                await bridge.SyncWeakItemsAsync(new WeakSyncRequestDto()));
        }

        [Test]
        public async Task MissingCredentialFailsClosedWithoutTransport()
        {
            var transport = new FakeTransport(new CoffeeGameHttpResponse(200, string.Empty));
            var bridge = new CoffeeLearningHttpBridge(
                new CoffeeLearningHttpBridgeOptions(),
                new FakeTokenProvider(null),
                transport);

            var response = await bridge.SyncWeakItemsAsync(new WeakSyncRequestDto());

            Assert.That(bridge.IsSignedIn, Is.False);
            Assert.That(response.error.code, Is.EqualTo("AUTHENTICATION_REQUIRED"));
            Assert.That(transport.CallCount, Is.Zero);
        }

        [Test]
        public async Task LegacyDailyClaimDoesNotInventProviderEndpoint()
        {
            var transport = new FakeTransport(new CoffeeGameHttpResponse(500, string.Empty));
            var bridge = CreateBridge(transport);

            var result = await bridge.ClaimTodayAsync();

            Assert.That(result.ClaimId, Is.Empty);
            Assert.That(result.Currency, Is.Zero);
            Assert.That(transport.CallCount, Is.Zero);
        }

        private static CoffeeLearningHttpBridge CreateBridge(
            FakeTransport transport,
            string baseUrl = CoffeeLearningHttpBridgeOptions.ProductionIntegrationBaseUrl)
        {
            return new CoffeeLearningHttpBridge(
                new CoffeeLearningHttpBridgeOptions(baseUrl),
                new FakeTokenProvider(TestToken),
                transport);
        }

        private sealed class FakeTokenProvider : ICoffeeGameAccessTokenProvider
        {
            private readonly string token;

            public FakeTokenProvider(string token)
            {
                this.token = token;
            }

            public bool HasAccessToken => !string.IsNullOrEmpty(token);

            public Task<string> LoadAccessTokenAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(token);
            }
        }

        private sealed class FakeTransport : ICoffeeGameHttpTransport
        {
            public FakeTransport(CoffeeGameHttpResponse response)
            {
                Response = response;
            }

            public CoffeeGameHttpResponse Response { get; set; }
            public CoffeeGameHttpTransportException Exception { get; set; }
            public CoffeeGameHttpRequest LastRequest { get; private set; }
            public string LastAccessToken { get; private set; }
            public int CallCount { get; private set; }

            public Task<CoffeeGameHttpResponse> SendAsync(
                CoffeeGameHttpRequest request,
                string accessToken,
                int timeoutSeconds,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                LastRequest = request;
                LastAccessToken = accessToken;
                if (Exception != null)
                {
                    throw Exception;
                }

                return Task.FromResult(Response);
            }
        }
    }
}
