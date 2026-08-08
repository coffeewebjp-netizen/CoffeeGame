using System;
using System.Collections.Generic;

namespace CoffeeGame.Domain
{
    /// <summary>
    /// Remembers stable reward claim IDs so the same reward cannot be granted twice.
    /// The snapshot is suitable for persistence alongside player progression.
    /// </summary>
    public sealed class RewardLedger
    {
        private readonly HashSet<string> claimedIds;

        public RewardLedger()
            : this(null)
        {
        }

        public RewardLedger(IEnumerable<string> previouslyClaimedIds)
        {
            claimedIds = new HashSet<string>(StringComparer.Ordinal);

            if (previouslyClaimedIds == null)
            {
                return;
            }

            foreach (var claimId in previouslyClaimedIds)
            {
                claimedIds.Add(ValidateClaimId(claimId));
            }
        }

        public int Count => claimedIds.Count;

        public bool HasClaimed(string claimId)
        {
            return claimedIds.Contains(ValidateClaimId(claimId));
        }

        public bool TryClaim(string claimId)
        {
            return claimedIds.Add(ValidateClaimId(claimId));
        }

        public IReadOnlyList<string> CreateSnapshot()
        {
            var snapshot = new string[claimedIds.Count];
            claimedIds.CopyTo(snapshot);
            Array.Sort(snapshot, StringComparer.Ordinal);
            return Array.AsReadOnly(snapshot);
        }

        private static string ValidateClaimId(string claimId)
        {
            if (string.IsNullOrWhiteSpace(claimId))
            {
                throw new ArgumentException("A reward claim ID must contain a stable, non-whitespace value.", nameof(claimId));
            }

            return claimId;
        }
    }
}
