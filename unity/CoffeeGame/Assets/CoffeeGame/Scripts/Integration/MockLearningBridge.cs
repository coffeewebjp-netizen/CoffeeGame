using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace CoffeeGame.Integration
{
    /// <summary>
    /// Deterministic in-process contract v1 provider for tests and offline development.
    /// It performs no HTTP, authentication, AI grading, microphone access, or audio persistence.
    /// </summary>
    public sealed class MockLearningBridge : ILearningBridge
    {
        private const string DefaultWeakItemId = "wi_mock_resilient_001";
        private const string DefaultPrompt = "resilient";
        private const string DefaultAnswer = "しなやかで回復力がある";

        private readonly object gate = new object();
        private readonly Dictionary<string, CoffeeGameChallengeDto> challengesByRequestId =
            new Dictionary<string, CoffeeGameChallengeDto>(StringComparer.Ordinal);
        private readonly Dictionary<string, CoffeeGameChallengeDto> challengesById =
            new Dictionary<string, CoffeeGameChallengeDto>(StringComparer.Ordinal);
        private readonly Dictionary<string, AttemptRecord> attemptsByClientId =
            new Dictionary<string, AttemptRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, AttemptRecord> attemptsByResultId =
            new Dictionary<string, AttemptRecord>(StringComparer.Ordinal);

        public bool IsSignedIn => true;

        public Task<LearningClaimResult> ClaimTodayAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new LearningClaimResult("mock-claim-v1", 0, false));
        }

        public Task<AccountIdentityResponseDto> GetAccountIdentityAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AccountIdentityResponseDto
            {
                contractVersion = CoffeeGameContractV1.Version,
                account = new CoffeeGameAccountDto { email = "mock-coffee-game@example.com" }
            });
        }

        public Task<WeakSyncResponseDto> SyncWeakItemsAsync(
            WeakSyncRequestDto request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                return Task.FromResult(new WeakSyncResponseDto
                {
                    contractVersion = CoffeeGameContractV1.Version,
                    items = Array.Empty<WeakItemDto>(),
                    error = CreateInvalidRequest("query", "is required")
                });
            }

            if (request.limit < 1 || request.limit > 100)
            {
                return Task.FromResult(new WeakSyncResponseDto
                {
                    contractVersion = CoffeeGameContractV1.Version,
                    items = Array.Empty<WeakItemDto>(),
                    error = CreateInvalidRequest("query.limit", "must be between 1 and 100")
                });
            }

            if (!CoffeeGameContractV1.IsSupportedWeakLookbackDays(request.lookbackDays))
            {
                return Task.FromResult(new WeakSyncResponseDto
                {
                    contractVersion = CoffeeGameContractV1.Version,
                    items = Array.Empty<WeakItemDto>(),
                    error = CreateInvalidRequest(
                        "query.lookbackDays",
                        $"must be between {CoffeeGameContractV1.MinimumWeakLookbackDays} and {CoffeeGameContractV1.MaximumWeakLookbackDays}")
                });
            }

            return Task.FromResult(new WeakSyncResponseDto
            {
                contractVersion = CoffeeGameContractV1.Version,
                items = new[] { CreateWeakItem() },
                nextCursor = null,
                hasMore = false,
                syncAfterSeconds = 900
            });
        }

        public Task<ChallengeIssueResponseDto> IssueChallengeAsync(
            ChallengeIssueRequestDto request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null || string.IsNullOrWhiteSpace(request.weakItemId)
                || string.IsNullOrWhiteSpace(request.clientRequestId))
            {
                return Task.FromResult(new ChallengeIssueResponseDto
                {
                    contractVersion = CoffeeGameContractV1.Version,
                    error = CreateInvalidRequest("body", "weakItemId and clientRequestId are required")
                });
            }

            lock (gate)
            {
                if (!challengesByRequestId.TryGetValue(request.clientRequestId, out var challenge))
                {
                    challenge = CreateChallenge(request.weakItemId, request.clientRequestId);
                    challengesByRequestId.Add(request.clientRequestId, challenge);
                    challengesById.Add(challenge.challengeId, challenge);
                }

                return Task.FromResult(new ChallengeIssueResponseDto
                {
                    contractVersion = CoffeeGameContractV1.Version,
                    challenge = CopyChallenge(challenge)
                });
            }
        }

        public Task<AnswerResultResponseDto> SubmitAnswerAsync(
            AnswerSubmitRequestDto request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsValidAnswerRequest(request, out var invalidField, out var invalidIssue))
            {
                return Task.FromResult(CreateAnswerError(invalidField, invalidIssue));
            }

            lock (gate)
            {
                if (!challengesById.ContainsKey(request.challengeId))
                {
                    return Task.FromResult(CreateAnswerError("body.challengeId", "does not identify an issued mock challenge"));
                }

                if (!attemptsByClientId.TryGetValue(request.clientAttemptId, out var attempt))
                {
                    var isCorrect = string.Equals(
                        request.answer.text.Trim(),
                        DefaultAnswer,
                        StringComparison.Ordinal);
                    attempt = new AttemptRecord(
                        request.challengeId,
                        request.clientAttemptId,
                        CreateStableId("rs_mock_", request.clientAttemptId),
                        CreateStableId("gr_mock_", request.clientAttemptId),
                        isCorrect);
                    attemptsByClientId.Add(request.clientAttemptId, attempt);
                    attemptsByResultId.Add(attempt.ResultId, attempt);
                }

                return Task.FromResult(CreatePendingResponse(attempt));
            }
        }

        public Task<AnswerResultResponseDto> RecoverResultAsync(
            string resultId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(resultId))
            {
                return Task.FromResult(CreateAnswerError("path.resultId", "is required"));
            }

            lock (gate)
            {
                if (!attemptsByResultId.TryGetValue(resultId, out var attempt))
                {
                    return Task.FromResult(CreateAnswerError("path.resultId", "does not identify a mock result"));
                }

                return Task.FromResult(CreateCompletedResponse(attempt));
            }
        }

        private static bool IsValidAnswerRequest(
            AnswerSubmitRequestDto request,
            out string invalidField,
            out string invalidIssue)
        {
            if (request == null)
            {
                invalidField = "body";
                invalidIssue = "is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.challengeId))
            {
                invalidField = "body.challengeId";
                invalidIssue = "is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.clientAttemptId))
            {
                invalidField = "body.clientAttemptId";
                invalidIssue = "is required";
                return false;
            }

            if (request.answer == null || string.IsNullOrWhiteSpace(request.answer.text))
            {
                invalidField = "body.answer.text";
                invalidIssue = "must contain at least 1 character(s)";
                return false;
            }

            try
            {
                CoffeeGameContractV1.RequireSupportedInputMode(request.answer.inputMode);
            }
            catch (ArgumentException)
            {
                invalidField = "body.answer.inputMode";
                invalidIssue = "must be typed or speechTranscript";
                return false;
            }

            invalidField = null;
            invalidIssue = null;
            return true;
        }

        private static WeakItemDto CreateWeakItem()
        {
            return new WeakItemDto
            {
                weakItemId = DefaultWeakItemId,
                prompt = CreatePrompt(),
                difficulty = CreateDifficulty()
            };
        }

        private static CoffeeGameChallengeDto CreateChallenge(string weakItemId, string clientRequestId)
        {
            return new CoffeeGameChallengeDto
            {
                challengeId = CreateStableId("ch_mock_", clientRequestId),
                weakItemId = weakItemId,
                prompt = CreatePrompt(),
                difficulty = CreateDifficulty(),
                acceptedInputModes = new[]
                {
                    CoffeeGameContractV1.TypedInputMode,
                    CoffeeGameContractV1.SpeechTranscriptInputMode
                },
                expiresAt = "2030-01-02T03:04:05.000Z"
            };
        }

        private static CoffeeGamePromptDto CreatePrompt()
        {
            return new CoffeeGamePromptDto { text = DefaultPrompt, answerLocale = "ja-JP" };
        }

        private static CoffeeGameDifficultyDto CreateDifficulty()
        {
            return new CoffeeGameDifficultyDto
            {
                band = CoffeeGameContractV1.IntermediateBand,
                level = 3
            };
        }

        private static CoffeeGameChallengeDto CopyChallenge(CoffeeGameChallengeDto source)
        {
            return new CoffeeGameChallengeDto
            {
                challengeId = source.challengeId,
                weakItemId = source.weakItemId,
                prompt = CreatePrompt(),
                difficulty = CreateDifficulty(),
                acceptedInputModes = (string[])source.acceptedInputModes.Clone(),
                expiresAt = source.expiresAt
            };
        }

        private static AnswerResultResponseDto CreatePendingResponse(AttemptRecord attempt)
        {
            return new AnswerResultResponseDto
            {
                contractVersion = CoffeeGameContractV1.Version,
                result = new CoffeeGameResultDto
                {
                    resultId = attempt.ResultId,
                    challengeId = attempt.ChallengeId,
                    clientAttemptId = attempt.ClientAttemptId,
                    status = CoffeeGameContractV1.PendingStatus,
                    retryAfterSeconds = 3,
                    recovery = new ResultRecoveryDto
                    {
                        method = "GET",
                        path = "/api/integrations/coffee-game/v1/results/" + attempt.ResultId
                    }
                }
            };
        }

        private static AnswerResultResponseDto CreateCompletedResponse(AttemptRecord attempt)
        {
            return new AnswerResultResponseDto
            {
                contractVersion = CoffeeGameContractV1.Version,
                result = new CoffeeGameResultDto
                {
                    resultId = attempt.ResultId,
                    challengeId = attempt.ChallengeId,
                    clientAttemptId = attempt.ClientAttemptId,
                    status = CoffeeGameContractV1.CompletedStatus,
                    judgment = new ResultJudgmentDto
                    {
                        isCorrect = attempt.IsCorrect,
                        feedback = attempt.IsCorrect
                            ? "回答の意味が問題の意図と一致しています。"
                            : "回答の意味が問題の意図とは一致しませんでした。"
                    },
                    learning = new LearningMutationDto
                    {
                        state = attempt.IsCorrect
                            ? CoffeeGameContractV1.OkLearningState
                            : CoffeeGameContractV1.MistakeLearningState,
                        mutationApplied = true
                    },
                    rewardEligibility = new RewardEligibilityDto
                    {
                        eligible = attempt.IsCorrect,
                        grantId = attempt.IsCorrect ? attempt.GrantId : null,
                        difficulty = CreateDifficulty()
                    },
                    completedAt = "2030-01-02T03:04:06.000Z"
                }
            };
        }

        private static AnswerResultResponseDto CreateAnswerError(string field, string issue)
        {
            return new AnswerResultResponseDto
            {
                contractVersion = CoffeeGameContractV1.Version,
                error = CreateInvalidRequest(field, issue)
            };
        }

        private static CoffeeGameErrorDto CreateInvalidRequest(string field, string issue)
        {
            return new CoffeeGameErrorDto
            {
                code = "INVALID_REQUEST",
                message = "The CoffeeGAME request does not match contract v1.",
                retryable = false,
                fields = new[] { new CoffeeGameErrorFieldDto { field = field, issue = issue } }
            };
        }

        private static string CreateStableId(string prefix, string source)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offsetBasis;
            foreach (var character in source)
            {
                hash ^= character;
                hash *= prime;
            }

            return prefix + hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private sealed class AttemptRecord
        {
            public AttemptRecord(
                string challengeId,
                string clientAttemptId,
                string resultId,
                string grantId,
                bool isCorrect)
            {
                ChallengeId = challengeId;
                ClientAttemptId = clientAttemptId;
                ResultId = resultId;
                GrantId = grantId;
                IsCorrect = isCorrect;
            }

            public string ChallengeId { get; }
            public string ClientAttemptId { get; }
            public string ResultId { get; }
            public string GrantId { get; }
            public bool IsCorrect { get; }
        }
    }
}
