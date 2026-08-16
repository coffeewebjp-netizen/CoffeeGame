using System;

namespace CoffeeGame.Domain
{
    public readonly struct SafeCheckpointSnapshot
    {
        public SafeCheckpointSnapshot(
            bool isSafeCheckpoint,
            bool isCombatActive,
            bool hasBlockingInteraction,
            bool encounterInProgress,
            int safeCheckpointsSinceLastOffer)
        {
            if (safeCheckpointsSinceLastOffer < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(safeCheckpointsSinceLastOffer),
                    "Safe checkpoint cadence cannot be negative.");
            }

            IsSafeCheckpoint = isSafeCheckpoint;
            IsCombatActive = isCombatActive;
            HasBlockingInteraction = hasBlockingInteraction;
            EncounterInProgress = encounterInProgress;
            SafeCheckpointsSinceLastOffer = safeCheckpointsSinceLastOffer;
        }

        public bool IsSafeCheckpoint { get; }
        public bool IsCombatActive { get; }
        public bool HasBlockingInteraction { get; }
        public bool EncounterInProgress { get; }
        public int SafeCheckpointsSinceLastOffer { get; }
    }

    /// <summary>
    /// Pure eligibility rule. An encounter is never eligible during combat, loading-like
    /// blocking interactions, or outside an explicitly declared safe checkpoint.
    /// </summary>
    public sealed class SafeCheckpointCadencePolicy
    {
        public SafeCheckpointCadencePolicy(int minimumSafeCheckpointsBetweenOffers)
        {
            if (minimumSafeCheckpointsBetweenOffers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumSafeCheckpointsBetweenOffers));
            }

            MinimumSafeCheckpointsBetweenOffers = minimumSafeCheckpointsBetweenOffers;
        }

        public int MinimumSafeCheckpointsBetweenOffers { get; }

        public bool IsEligible(SafeCheckpointSnapshot snapshot)
        {
            return snapshot.IsSafeCheckpoint
                && !snapshot.IsCombatActive
                && !snapshot.HasBlockingInteraction
                && !snapshot.EncounterInProgress
                && snapshot.SafeCheckpointsSinceLastOffer >= MinimumSafeCheckpointsBetweenOffers;
        }
    }
}
