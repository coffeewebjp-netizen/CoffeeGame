using System.Threading;
using System.Threading.Tasks;
using CoffeeGame.Domain;
using CoffeeGame.Integration;
using NUnit.Framework;

namespace CoffeeGame.Learning.Tests
{
    public sealed class RivalLearningQuestionSessionTests
    {
        [Test]
        public async Task EncounterLoadsWeakQuestionAndRequiresEditableConfirmationBeforeSubmit()
        {
            var bridge = new CountingBridge(new MockLearningBridge());
            int id = 0;
            using (var session = new RivalLearningQuestionSession(
                () => bridge,
                () => "test" + (++id)))
            {
                await session.BeginNewEncounterAsync();

                Assert.That(session.State, Is.EqualTo(RivalLearningQuestionState.Editing));
                Assert.That(session.PromptText, Is.EqualTo("resilient"));
                Assert.That(bridge.SyncCalls, Is.EqualTo(1));
                Assert.That(bridge.IssueCalls, Is.EqualTo(1));
                Assert.That(bridge.SubmitCalls, Is.Zero);

                session.UpdateDraft("最初の回答");
                Assert.That(session.RequestConfirmation(), Is.True);
                Assert.That(session.State, Is.EqualTo(RivalLearningQuestionState.Confirming));
                Assert.That(bridge.SubmitCalls, Is.Zero,
                    "Moving to confirmation must never submit an answer.");

                Assert.That(session.ReturnToEditing(), Is.True);
                session.UpdateDraft("しなやかで回復力がある");
                Assert.That(session.RequestConfirmation(), Is.True);
                await session.SubmitConfirmedAnswerAsync();

                Assert.That(bridge.SubmitCalls, Is.EqualTo(1));
                Assert.That(session.State, Is.EqualTo(RivalLearningQuestionState.Pending));
                Assert.That(session.ResultId, Is.Not.Empty);

                await session.RecoverPendingResultAsync();

                Assert.That(bridge.RecoverCalls, Is.EqualTo(1));
                Assert.That(session.State, Is.EqualTo(RivalLearningQuestionState.Completed));
                Assert.That(session.IsCorrect, Is.True);
                Assert.That(session.RewardEligible, Is.True);
                Assert.That(session.JudgmentFeedback, Is.Not.Empty);
                Assert.That(session.AuthoritativeOutcome.HasValue, Is.True);
                Assert.That(session.AuthoritativeOutcome.Value.LearningMutationApplied, Is.True);
                var application = new PlayerProgression().TryApplyLearningOutcome(
                    session.AuthoritativeOutcome.Value,
                    "rival-silver-001");
                Assert.That(session.RecordGameRewardApplication(application), Is.True);
                Assert.That(session.RecordGameRewardApplication(application), Is.False);
                Assert.That(session.GameRewardApplication.Value.Status,
                    Is.EqualTo(LearningRewardApplyStatus.Granted));
            }
        }

        [Test]
        public async Task MissingConnectionFailsClosedAndCanReturnWithoutSubmitting()
        {
            using (var session = new RivalLearningQuestionSession(
                () => new NullLearningBridge(),
                () => "offline"))
            {
                await session.BeginNewEncounterAsync();

                Assert.That(session.State, Is.EqualTo(RivalLearningQuestionState.Error));
                Assert.That(session.ErrorCode, Is.EqualTo("NOT_CONNECTED"));
                Assert.That(session.IsBusy, Is.False);

                session.CancelPendingOperation();
                Assert.That(session.State, Is.EqualTo(RivalLearningQuestionState.Idle));
            }
        }

        [Test]
        public async Task EmptyWeakSetShowsNoItemsInsteadOfRemainingInLoading()
        {
            var bridge = new EmptyWeakBridge();
            using (var session = new RivalLearningQuestionSession(() => bridge, () => "empty"))
            {
                await session.BeginNewEncounterAsync();

                Assert.That(session.State, Is.EqualTo(RivalLearningQuestionState.NoItems));
                Assert.That(session.ErrorCode, Is.Empty);
                Assert.That(bridge.IssueCalls, Is.Zero);
            }
        }

        [Test]
        public void WeakSelectionUsesRequestedIndexAndAvoidsImmediateRepeat()
        {
            var items = new[]
            {
                CreateWeakItem("weak-a", "alpha"),
                CreateWeakItem("weak-b", "beta"),
                CreateWeakItem("weak-c", "gamma")
            };

            Assert.That(
                RivalLearningQuestionSession.SelectUsableItem(items, 1).weakItemId,
                Is.EqualTo("weak-b"));
            Assert.That(
                RivalLearningQuestionSession.SelectUsableItem(items, 0, "weak-a").weakItemId,
                Is.EqualTo("weak-b"),
                "The previous item is removed before choosing when alternatives exist.");
            Assert.That(
                RivalLearningQuestionSession.SelectUsableItem(items, -1).weakItemId,
                Is.EqualTo("weak-c"));
        }

        [Test]
        public async Task ConsecutiveEncountersAvoidThePreviousWeakItemWhenAlternativesExist()
        {
            var bridge = new MultipleWeakBridge();
            int id = 0;
            using (var session = new RivalLearningQuestionSession(
                () => bridge,
                () => "rotation-" + (++id),
                () => 0))
            {
                await session.BeginNewEncounterAsync();
                Assert.That(session.PromptText, Is.EqualTo("alpha"));

                await session.BeginNewEncounterAsync();
                Assert.That(session.PromptText, Is.EqualTo("beta"),
                    "A new encounter must not reuse the previous first item while another item exists.");
            }
        }

        private static WeakItemDto CreateWeakItem(string id, string prompt)
        {
            return new WeakItemDto
            {
                weakItemId = id,
                prompt = new CoffeeGamePromptDto { text = prompt, answerLocale = "ja-JP" },
                difficulty = new CoffeeGameDifficultyDto { band = "foundation", level = 1 }
            };
        }

        private sealed class CountingBridge : ILearningBridge
        {
            private readonly ILearningBridge inner;

            public CountingBridge(ILearningBridge inner)
            {
                this.inner = inner;
            }

            public int SyncCalls { get; private set; }
            public int IssueCalls { get; private set; }
            public int SubmitCalls { get; private set; }
            public int RecoverCalls { get; private set; }
            public bool IsSignedIn => inner.IsSignedIn;

            public Task<LearningClaimResult> ClaimTodayAsync(CancellationToken cancellationToken = default)
            {
                return inner.ClaimTodayAsync(cancellationToken);
            }

            public Task<AccountIdentityResponseDto> GetAccountIdentityAsync(
                CancellationToken cancellationToken = default)
            {
                return inner.GetAccountIdentityAsync(cancellationToken);
            }

            public Task<WeakSyncResponseDto> SyncWeakItemsAsync(
                WeakSyncRequestDto request,
                CancellationToken cancellationToken = default)
            {
                SyncCalls++;
                return inner.SyncWeakItemsAsync(request, cancellationToken);
            }

            public Task<ChallengeIssueResponseDto> IssueChallengeAsync(
                ChallengeIssueRequestDto request,
                CancellationToken cancellationToken = default)
            {
                IssueCalls++;
                return inner.IssueChallengeAsync(request, cancellationToken);
            }

            public Task<AnswerResultResponseDto> SubmitAnswerAsync(
                AnswerSubmitRequestDto request,
                CancellationToken cancellationToken = default)
            {
                SubmitCalls++;
                return inner.SubmitAnswerAsync(request, cancellationToken);
            }

            public Task<AnswerResultResponseDto> RecoverResultAsync(
                string resultId,
                CancellationToken cancellationToken = default)
            {
                RecoverCalls++;
                return inner.RecoverResultAsync(resultId, cancellationToken);
            }
        }

        private sealed class EmptyWeakBridge : ILearningBridge
        {
            public int IssueCalls { get; private set; }
            public bool IsSignedIn => true;

            public Task<LearningClaimResult> ClaimTodayAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new LearningClaimResult(string.Empty, 0, false));
            }

            public Task<AccountIdentityResponseDto> GetAccountIdentityAsync(
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new AccountIdentityResponseDto());
            }

            public Task<WeakSyncResponseDto> SyncWeakItemsAsync(
                WeakSyncRequestDto request,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new WeakSyncResponseDto
                {
                    contractVersion = CoffeeGameContractV1.Version,
                    items = new WeakItemDto[0]
                });
            }

            public Task<ChallengeIssueResponseDto> IssueChallengeAsync(
                ChallengeIssueRequestDto request,
                CancellationToken cancellationToken = default)
            {
                IssueCalls++;
                return Task.FromResult(new ChallengeIssueResponseDto());
            }

            public Task<AnswerResultResponseDto> SubmitAnswerAsync(
                AnswerSubmitRequestDto request,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new AnswerResultResponseDto());
            }

            public Task<AnswerResultResponseDto> RecoverResultAsync(
                string resultId,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new AnswerResultResponseDto());
            }
        }

        private sealed class MultipleWeakBridge : ILearningBridge
        {
            public bool IsSignedIn => true;

            public Task<LearningClaimResult> ClaimTodayAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new LearningClaimResult(string.Empty, 0, false));
            }

            public Task<AccountIdentityResponseDto> GetAccountIdentityAsync(
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new AccountIdentityResponseDto());
            }

            public Task<WeakSyncResponseDto> SyncWeakItemsAsync(
                WeakSyncRequestDto request,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new WeakSyncResponseDto
                {
                    contractVersion = CoffeeGameContractV1.Version,
                    items = new[]
                    {
                        CreateWeakItem("weak-a", "alpha"),
                        CreateWeakItem("weak-b", "beta")
                    }
                });
            }

            public Task<ChallengeIssueResponseDto> IssueChallengeAsync(
                ChallengeIssueRequestDto request,
                CancellationToken cancellationToken = default)
            {
                string prompt = request.weakItemId == "weak-a" ? "alpha" : "beta";
                return Task.FromResult(new ChallengeIssueResponseDto
                {
                    contractVersion = CoffeeGameContractV1.Version,
                    challenge = new CoffeeGameChallengeDto
                    {
                        challengeId = "challenge-" + request.clientRequestId,
                        weakItemId = request.weakItemId,
                        prompt = new CoffeeGamePromptDto { text = prompt, answerLocale = "ja-JP" },
                        difficulty = new CoffeeGameDifficultyDto { band = "foundation", level = 1 },
                        acceptedInputModes = new[] { CoffeeGameContractV1.TypedInputMode }
                    }
                });
            }

            public Task<AnswerResultResponseDto> SubmitAnswerAsync(
                AnswerSubmitRequestDto request,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new AnswerResultResponseDto());
            }

            public Task<AnswerResultResponseDto> RecoverResultAsync(
                string resultId,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new AnswerResultResponseDto());
            }
        }
    }
}
