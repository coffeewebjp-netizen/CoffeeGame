using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoffeeGame.Integration
{
    public interface ILearningBridge
    {
        bool IsSignedIn { get; }
        Task<LearningClaimResult> ClaimTodayAsync(CancellationToken cancellationToken = default);
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
            return Task.FromResult(new LearningClaimResult(string.Empty, 0, false));
        }
    }
}

