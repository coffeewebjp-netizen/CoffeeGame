using System;
using System.Collections.Generic;

namespace CoffeeGame.Domain
{
    public interface IBoundedIntegerSource
    {
        int Next(int exclusiveUpperBound);
    }

    /// <summary>
    /// Pure candidate selection with caller-injected bounded randomness. When alternatives
    /// exist, the last-seen rival is removed before selecting an index.
    /// </summary>
    public sealed class DeterministicRivalSelector
    {
        private readonly IBoundedIntegerSource integerSource;

        public DeterministicRivalSelector(IBoundedIntegerSource integerSource)
        {
            this.integerSource = integerSource ?? throw new ArgumentNullException(nameof(integerSource));
        }

        public string Select(IReadOnlyList<string> candidateIds, string lastSeenRivalId = null)
        {
            if (candidateIds == null)
            {
                throw new ArgumentNullException(nameof(candidateIds));
            }

            if (candidateIds.Count == 0)
            {
                throw new ArgumentException("At least one rival candidate is required.", nameof(candidateIds));
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < candidateIds.Count; index++)
            {
                var candidateId = candidateIds[index];
                if (string.IsNullOrWhiteSpace(candidateId))
                {
                    throw new ArgumentException("Rival candidate IDs must be non-empty.", nameof(candidateIds));
                }

                if (!seenIds.Add(candidateId))
                {
                    throw new ArgumentException("Rival candidate IDs must be unique.", nameof(candidateIds));
                }
            }

            if (candidateIds.Count == 1)
            {
                return candidateIds[0];
            }

            var suppressLastSeen = !string.IsNullOrEmpty(lastSeenRivalId)
                && seenIds.Contains(lastSeenRivalId);
            var selectableCount = suppressLastSeen ? candidateIds.Count - 1 : candidateIds.Count;
            var selectedIndex = integerSource.Next(selectableCount);
            if (selectedIndex < 0 || selectedIndex >= selectableCount)
            {
                throw new InvalidOperationException(
                    "The bounded integer source returned a value outside its requested range.");
            }

            for (var index = 0; index < candidateIds.Count; index++)
            {
                var candidateId = candidateIds[index];
                if (suppressLastSeen
                    && string.Equals(candidateId, lastSeenRivalId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (selectedIndex == 0)
                {
                    return candidateId;
                }

                selectedIndex--;
            }

            throw new InvalidOperationException("Rival selection exhausted a validated candidate set.");
        }
    }
}
