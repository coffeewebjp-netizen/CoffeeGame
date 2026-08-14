using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoffeeGame.Integration
{
    public interface ILearningBridge
    {
        bool IsSignedIn { get; }
        Task<LearningClaimResult> ClaimTodayAsync(CancellationToken cancellationToken = default);
        Task<WeakSyncResponseDto> SyncWeakItemsAsync(
            WeakSyncRequestDto request,
            CancellationToken cancellationToken = default);
        Task<ChallengeIssueResponseDto> IssueChallengeAsync(
            ChallengeIssueRequestDto request,
            CancellationToken cancellationToken = default);
        Task<AnswerResultResponseDto> SubmitAnswerAsync(
            AnswerSubmitRequestDto request,
            CancellationToken cancellationToken = default);
        Task<AnswerResultResponseDto> RecoverResultAsync(
            string resultId,
            CancellationToken cancellationToken = default);
    }

    [Serializable]
    public readonly struct LearningClaimResult
    {
        public LearningClaimResult(string claimId, int currency, bool wasAlreadyClaimed)
        {
            ClaimId = claimId ?? string.Empty;
            Currency = Math.Max(0, currency);
            WasAlreadyClaimed = wasAlreadyClaimed;
        }

        public string ClaimId { get; }
        public int Currency { get; }
        public bool WasAlreadyClaimed { get; }
    }

    public sealed class NullLearningBridge : ILearningBridge
    {
        public bool IsSignedIn => false;

        public Task<LearningClaimResult> ClaimTodayAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new LearningClaimResult(string.Empty, 0, false));
        }

        public Task<WeakSyncResponseDto> SyncWeakItemsAsync(
            WeakSyncRequestDto request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new WeakSyncResponseDto
            {
                contractVersion = CoffeeGameContractV1.Version,
                items = Array.Empty<WeakItemDto>(),
                hasMore = false,
                syncAfterSeconds = 900
            });
        }

        public Task<ChallengeIssueResponseDto> IssueChallengeAsync(
            ChallengeIssueRequestDto request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ChallengeIssueResponseDto
            {
                contractVersion = CoffeeGameContractV1.Version,
                error = CreateUnavailableError()
            });
        }

        public Task<AnswerResultResponseDto> SubmitAnswerAsync(
            AnswerSubmitRequestDto request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateUnavailableResult());
        }

        public Task<AnswerResultResponseDto> RecoverResultAsync(
            string resultId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateUnavailableResult());
        }

        private static AnswerResultResponseDto CreateUnavailableResult()
        {
            return new AnswerResultResponseDto
            {
                contractVersion = CoffeeGameContractV1.Version,
                error = CreateUnavailableError()
            };
        }

        private static CoffeeGameErrorDto CreateUnavailableError()
        {
            return new CoffeeGameErrorDto
            {
                code = "INTEGRATION_DISABLED",
                message = "CoffeeGAME learning integration is unavailable; gameplay remains enabled.",
                retryable = false,
                fields = Array.Empty<CoffeeGameErrorFieldDto>()
            };
        }
    }
}
