using System;

namespace CoffeeGame.Domain
{
    /// <summary>
    /// Immutable weak-sync cadence policy. Callers supply deterministic Unix-second time and
    /// decide how to execute sync; this type starts no timer, task, or network operation.
    /// </summary>
    public sealed class LearningSyncSchedulePolicy
    {
        public const int MinimumSyncAfterSeconds = 60;
        public const int MaximumSyncAfterSeconds = 86_400;

        public LearningSyncSchedulePolicy()
        {
        }

        private LearningSyncSchedulePolicy(long nextDueUnixSeconds)
        {
            HasSuccessfulSync = true;
            NextDueUnixSeconds = nextDueUnixSeconds;
        }

        public bool HasSuccessfulSync { get; }
        public long NextDueUnixSeconds { get; }

        public bool IsDue(long nowUnixSeconds)
        {
            RequireValidTime(nowUnixSeconds, nameof(nowUnixSeconds));
            return !HasSuccessfulSync || nowUnixSeconds >= NextDueUnixSeconds;
        }

        public LearningSyncSchedulePolicy AfterSuccessfulSync(
            long completedAtUnixSeconds,
            int syncAfterSeconds)
        {
            RequireValidTime(completedAtUnixSeconds, nameof(completedAtUnixSeconds));
            if (syncAfterSeconds < MinimumSyncAfterSeconds
                || syncAfterSeconds > MaximumSyncAfterSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(syncAfterSeconds),
                    "CoffeeGAME contract v1 syncAfterSeconds must be between 60 and 86400.");
            }

            return new LearningSyncSchedulePolicy(
                checked(completedAtUnixSeconds + syncAfterSeconds));
        }

        private static void RequireValidTime(long unixSeconds, string parameterName)
        {
            if (unixSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Unix-second time cannot be negative.");
            }
        }
    }
}
