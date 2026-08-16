using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace CoffeeGame.Integration.Tests
{
    public sealed class LearningBridgeTests
    {
        [Test]
        public async Task WeakSyncRequest_DefaultsToFourteenDaysAndAcceptsCustomLookback()
        {
            var bridge = new MockLearningBridge();
            var defaultRequest = new WeakSyncRequestDto();

            Assert.That(
                defaultRequest.lookbackDays,
                Is.EqualTo(CoffeeGameContractV1.DefaultWeakLookbackDays));
            Assert.That(CoffeeGameContractV1.DefaultWeakLookbackDays, Is.EqualTo(14));

            var customRequest = new WeakSyncRequestDto { limit = 25, lookbackDays = 7 };
            CoffeeGameContractV1.RequireSupportedWeakLookbackDays(customRequest.lookbackDays);
            var response = await bridge.SyncWeakItemsAsync(customRequest);

            Assert.That(response.error, Is.Null);
            Assert.That(response.items, Is.Not.Empty);
        }

        [TestCase(0)]
        [TestCase(31)]
        public async Task WeakSyncRequest_RejectsOutOfRangeLookback(int lookbackDays)
        {
            var bridge = new MockLearningBridge();
            var request = new WeakSyncRequestDto { lookbackDays = lookbackDays };

            Assert.Throws<ArgumentOutOfRangeException>(
                () => CoffeeGameContractV1.RequireSupportedWeakLookbackDays(lookbackDays));

            var response = await bridge.SyncWeakItemsAsync(request);

            Assert.That(response.items, Is.Empty);
            Assert.That(response.error.code, Is.EqualTo("INVALID_REQUEST"));
            Assert.That(response.error.fields[0].field, Is.EqualTo("query.lookbackDays"));
            Assert.That(
                response.error.fields[0].issue,
                Is.EqualTo("must be between 1 and 30"));
        }

        [Test]
        public async Task MockBridge_IsIdempotentAndRecoversPendingAsCompleted()
        {
            var bridge = new MockLearningBridge();
            var weak = await bridge.SyncWeakItemsAsync(new WeakSyncRequestDto { limit = 50 });
            var issueRequest = new ChallengeIssueRequestDto
            {
                weakItemId = weak.items[0].weakItemId,
                clientRequestId = "cr_test_encounter_001"
            };

            var issued = await bridge.IssueChallengeAsync(issueRequest);
            var issuedAgain = await bridge.IssueChallengeAsync(issueRequest);
            Assert.That(issuedAgain.challenge.challengeId, Is.EqualTo(issued.challenge.challengeId));

            var submitRequest = new AnswerSubmitRequestDto
            {
                challengeId = issued.challenge.challengeId,
                clientAttemptId = "ca_test_encounter_001",
                answer = new CoffeeGameAnswerDto
                {
                    text = "しなやかで回復力がある",
                    inputMode = CoffeeGameContractV1.SpeechTranscriptInputMode
                }
            };

            var pending = await bridge.SubmitAnswerAsync(submitRequest);
            var pendingRetry = await bridge.SubmitAnswerAsync(submitRequest);
            Assert.That(pending.result.status, Is.EqualTo("pending"));
            Assert.That(pendingRetry.result.resultId, Is.EqualTo(pending.result.resultId));

            var completed = await bridge.RecoverResultAsync(pending.result.resultId);
            var completedAgain = await bridge.RecoverResultAsync(pending.result.resultId);
            Assert.That(completed.result.status, Is.EqualTo("completed"));
            Assert.That(completed.result.judgment.isCorrect, Is.True);
            Assert.That(completed.result.learning.mutationApplied, Is.True);
            Assert.That(completed.result.rewardEligibility.eligible, Is.True);
            Assert.That(completedAgain.result.resultId, Is.EqualTo(completed.result.resultId));
            Assert.That(
                completedAgain.result.rewardEligibility.grantId,
                Is.EqualTo(completed.result.rewardEligibility.grantId));
        }

        [Test]
        public async Task NullBridge_RemainsPlayableAndFailSafe()
        {
            var bridge = new NullLearningBridge();

            var weak = await bridge.SyncWeakItemsAsync(new WeakSyncRequestDto { limit = 50 });
            var issue = await bridge.IssueChallengeAsync(new ChallengeIssueRequestDto());

            Assert.That(bridge.IsSignedIn, Is.False);
            Assert.That(weak.items, Is.Empty);
            Assert.That(issue.error.code, Is.EqualTo("INTEGRATION_DISABLED"));
        }

        [Test]
        public void Bridges_ObserveCancellationBeforeDoingWork()
        {
            var cancellation = new CancellationToken(true);

            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await new MockLearningBridge().SyncWeakItemsAsync(
                    new WeakSyncRequestDto { limit = 50 },
                    cancellation));
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await new NullLearningBridge().ClaimTodayAsync(cancellation));
        }
    }
}
