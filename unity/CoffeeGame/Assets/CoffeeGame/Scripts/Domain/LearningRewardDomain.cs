using System;
using System.Collections.Generic;

namespace CoffeeGame.Domain
{
    public static class RivalCharacterIds
    {
        public const string WeaknessChallenger = "rival-silver-001";
        public const string SplitInk = "rival-split-001";

        public static readonly string[] All =
        {
            WeaknessChallenger,
            SplitInk
        };

        public static string DisplayName(string rivalId)
        {
            switch (rivalId)
            {
                case WeaknessChallenger:
                    return "白銀のライバル";
                case SplitInk:
                    return "白黒のライバル";
                default:
                    return "ライバル";
            }
        }

        /// <summary>
        /// Encounter order is sequential: the first unrecruited rival appears.
        /// After every rival is recruited, the last rival remains the learning encounter.
        /// </summary>
        public static string[] EncounterCandidates(Func<string, bool> isRecruited)
        {
            if (isRecruited == null)
            {
                throw new ArgumentNullException(nameof(isRecruited));
            }

            for (int index = 0; index < All.Length; index++)
            {
                string rivalId = All[index];
                if (!isRecruited(rivalId))
                {
                    return new[] { rivalId };
                }
            }

            return new[] { All[All.Length - 1] };
        }

        /// <summary>
        /// The first rival is always listed. Later rivals appear in the companions
        /// roster only after the previous rival has been recruited.
        /// </summary>
        public static string[] VisibleCompanionIds(Func<string, bool> isRecruited)
        {
            if (isRecruited == null)
            {
                throw new ArgumentNullException(nameof(isRecruited));
            }

            var visible = new List<string>(All.Length) { All[0] };
            for (int index = 1; index < All.Length; index++)
            {
                if (isRecruited(All[index - 1]))
                {
                    visible.Add(All[index]);
                }
            }

            return visible.ToArray();
        }
    }

    public enum AuthoritativeLearningResultStatus
    {
        Pending,
        Completed
    }

    public enum LearningDifficultyBand
    {
        Foundation,
        Intermediate,
        Advanced
    }

    /// <summary>
    /// Provider-neutral, immutable projection of the provider-owned completed result.
    /// Provider adapters must populate this only from authoritative response fields.
    /// </summary>
    public readonly struct AuthoritativeLearningOutcome
    {
        public AuthoritativeLearningOutcome(
            string resultId,
            AuthoritativeLearningResultStatus status,
            bool isCorrect,
            bool learningMutationApplied,
            bool rewardEligible,
            string grantId,
            LearningDifficultyBand difficultyBand,
            int difficultyLevel)
        {
            if (string.IsNullOrWhiteSpace(resultId))
            {
                throw new ArgumentException("A stable provider result ID is required.", nameof(resultId));
            }

            if ((status == AuthoritativeLearningResultStatus.Completed
                    && (difficultyLevel < 1 || difficultyLevel > 5))
                || (status == AuthoritativeLearningResultStatus.Pending
                    && (difficultyLevel < 0 || difficultyLevel > 5)))
            {
                throw new ArgumentOutOfRangeException(nameof(difficultyLevel));
            }

            if (rewardEligible && string.IsNullOrWhiteSpace(grantId))
            {
                throw new ArgumentException("An eligible provider result requires a stable grant ID.", nameof(grantId));
            }

            ResultId = resultId;
            Status = status;
            IsCorrect = isCorrect;
            LearningMutationApplied = learningMutationApplied;
            RewardEligible = rewardEligible;
            GrantId = grantId;
            DifficultyBand = difficultyBand;
            DifficultyLevel = difficultyLevel;
        }

        public string ResultId { get; }
        public AuthoritativeLearningResultStatus Status { get; }
        public bool IsCorrect { get; }
        public bool LearningMutationApplied { get; }
        public bool RewardEligible { get; }
        public string GrantId { get; }
        public LearningDifficultyBand DifficultyBand { get; }
        public int DifficultyLevel { get; }
    }

    public readonly struct LearningRewardBundle : IEquatable<LearningRewardBundle>
    {
        public LearningRewardBundle(int talentPoints, int experience, int gold, int affinityDelta)
        {
            if (talentPoints < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(talentPoints));
            }

            if (experience < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(experience));
            }

            if (gold < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gold));
            }

            if (affinityDelta < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(affinityDelta));
            }

            TalentPoints = talentPoints;
            Experience = experience;
            Gold = gold;
            AffinityDelta = affinityDelta;
        }

        public int TalentPoints { get; }
        public int Experience { get; }
        public int Gold { get; }
        public int AffinityDelta { get; }

        public bool Equals(LearningRewardBundle other)
        {
            return TalentPoints == other.TalentPoints
                && Experience == other.Experience
                && Gold == other.Gold
                && AffinityDelta == other.AffinityDelta;
        }

        public override bool Equals(object obj)
        {
            return obj is LearningRewardBundle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = TalentPoints;
                hashCode = (hashCode * 397) ^ Experience;
                hashCode = (hashCode * 397) ^ Gold;
                hashCode = (hashCode * 397) ^ AffinityDelta;
                return hashCode;
            }
        }
    }

    /// <summary>
    /// Owner-approved initial balance for the first rival-learning loop.
    /// </summary>
    public sealed class LearningRewardPolicyV1
    {
        public const string PolicyId = "coffee-game-rival-reward-v1";
        public const int RecruitmentThreshold = 100;

        public LearningRewardBundle Map(LearningDifficultyBand band, int level)
        {
            if (level < 1 || level > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            int talentPoints;
            int experiencePerLevel;
            int goldPerLevel;
            int affinityBase;
            switch (band)
            {
                case LearningDifficultyBand.Foundation:
                    talentPoints = 1;
                    experiencePerLevel = 2;
                    goldPerLevel = 1;
                    affinityBase = 2;
                    break;
                case LearningDifficultyBand.Intermediate:
                    talentPoints = 2;
                    experiencePerLevel = 3;
                    goldPerLevel = 2;
                    affinityBase = 3;
                    break;
                case LearningDifficultyBand.Advanced:
                    talentPoints = 3;
                    experiencePerLevel = 4;
                    goldPerLevel = 3;
                    affinityBase = 4;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(band));
            }

            return new LearningRewardBundle(
                talentPoints,
                checked(experiencePerLevel * level),
                checked(goldPerLevel * level),
                checked(affinityBase + level));
        }
    }

    public sealed class LearningGrantLedger
    {
        private readonly HashSet<string> consumedGrantIds;

        public LearningGrantLedger(IEnumerable<string> previouslyConsumedGrantIds = null)
        {
            consumedGrantIds = new HashSet<string>(StringComparer.Ordinal);
            if (previouslyConsumedGrantIds == null)
            {
                return;
            }

            foreach (var grantId in previouslyConsumedGrantIds)
            {
                consumedGrantIds.Add(RequireGrantId(grantId));
            }
        }

        public int Count => consumedGrantIds.Count;

        public bool HasConsumed(string grantId)
        {
            return consumedGrantIds.Contains(RequireGrantId(grantId));
        }

        public bool TryConsume(string grantId)
        {
            return consumedGrantIds.Add(RequireGrantId(grantId));
        }

        public IReadOnlyList<string> CreateSnapshot()
        {
            var snapshot = new string[consumedGrantIds.Count];
            consumedGrantIds.CopyTo(snapshot);
            Array.Sort(snapshot, StringComparer.Ordinal);
            return Array.AsReadOnly(snapshot);
        }

        private static string RequireGrantId(string grantId)
        {
            if (string.IsNullOrWhiteSpace(grantId))
            {
                throw new ArgumentException("A stable non-whitespace grant ID is required.", nameof(grantId));
            }

            return grantId;
        }
    }

    public enum LearningRewardApplyStatus
    {
        NotEligible,
        DuplicateGrant,
        Granted
    }

    public readonly struct LearningRewardApplication
    {
        public LearningRewardApplication(
            LearningRewardApplyStatus status,
            LearningRewardBundle reward,
            bool rivalRecruited)
        {
            Status = status;
            Reward = reward;
            RivalRecruited = rivalRecruited;
        }

        public LearningRewardApplyStatus Status { get; }
        public LearningRewardBundle Reward { get; }
        public bool RivalRecruited { get; }
    }

    /// <summary>
    /// Game-owned, persistence-ready skeleton for one atomic learning reward transaction.
    /// It is intentionally independent from PlayerProgression and the combat RewardBundle.
    /// </summary>
    public sealed class LearningRewardAggregate
    {
        public const int MaximumBalance = 1_000_000_000;
        public const int MaximumAffinity = 1_000_000;

        private readonly object gate = new object();
        private readonly LearningRewardPolicyV1 policy;
        private readonly LearningGrantLedger grantLedger;
        private readonly Dictionary<string, int> affinityByRival =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> recruitedRivals =
            new HashSet<string>(StringComparer.Ordinal);

        public LearningRewardAggregate(
            int recruitmentThreshold,
            LearningRewardPolicyV1 policy = null,
            IEnumerable<string> previouslyConsumedGrantIds = null)
        {
            if (recruitmentThreshold < 1 || recruitmentThreshold > MaximumAffinity)
            {
                throw new ArgumentOutOfRangeException(nameof(recruitmentThreshold));
            }

            RecruitmentThreshold = recruitmentThreshold;
            this.policy = policy ?? new LearningRewardPolicyV1();
            grantLedger = new LearningGrantLedger(previouslyConsumedGrantIds);
        }

        public int RecruitmentThreshold { get; }
        public int TalentPoints { get; private set; }
        public int Experience { get; private set; }
        public int Gold { get; private set; }
        public int ConsumedGrantCount => grantLedger.Count;

        public int GetAffinity(string rivalId)
        {
            var validatedRivalId = RequireRivalId(rivalId);
            lock (gate)
            {
                return affinityByRival.TryGetValue(validatedRivalId, out var value) ? value : 0;
            }
        }

        public bool IsRecruited(string rivalId)
        {
            var validatedRivalId = RequireRivalId(rivalId);
            lock (gate)
            {
                return recruitedRivals.Contains(validatedRivalId);
            }
        }

        public LearningRewardApplication TryApply(
            AuthoritativeLearningOutcome outcome,
            string rivalId)
        {
            var validatedRivalId = RequireRivalId(rivalId);
            if (outcome.Status != AuthoritativeLearningResultStatus.Completed
                || !outcome.IsCorrect
                || !outcome.LearningMutationApplied
                || !outcome.RewardEligible)
            {
                return new LearningRewardApplication(
                    LearningRewardApplyStatus.NotEligible,
                    default,
                    false);
            }

            lock (gate)
            {
                if (grantLedger.HasConsumed(outcome.GrantId))
                {
                    return new LearningRewardApplication(
                        LearningRewardApplyStatus.DuplicateGrant,
                        default,
                        false);
                }

                var reward = policy.Map(outcome.DifficultyBand, outcome.DifficultyLevel);
                var currentAffinity = affinityByRival.TryGetValue(validatedRivalId, out var affinity)
                    ? affinity
                    : 0;

                // Compute every bounded/checked value before the grant ID becomes the commit gate.
                var nextTalentPoints = AddBounded(TalentPoints, reward.TalentPoints, MaximumBalance);
                var nextExperience = AddBounded(Experience, reward.Experience, MaximumBalance);
                var nextGold = AddBounded(Gold, reward.Gold, MaximumBalance);
                var nextAffinity = AddBounded(currentAffinity, reward.AffinityDelta, MaximumAffinity);
                var recruitsRival = currentAffinity < RecruitmentThreshold
                    && nextAffinity >= RecruitmentThreshold
                    && !recruitedRivals.Contains(validatedRivalId);

                if (!grantLedger.TryConsume(outcome.GrantId))
                {
                    return new LearningRewardApplication(
                        LearningRewardApplyStatus.DuplicateGrant,
                        default,
                        false);
                }

                TalentPoints = nextTalentPoints;
                Experience = nextExperience;
                Gold = nextGold;
                affinityByRival[validatedRivalId] = nextAffinity;
                if (recruitsRival)
                {
                    recruitedRivals.Add(validatedRivalId);
                }

                return new LearningRewardApplication(
                    LearningRewardApplyStatus.Granted,
                    reward,
                    recruitsRival);
            }
        }

        public IReadOnlyList<string> CreateConsumedGrantSnapshot()
        {
            lock (gate)
            {
                return grantLedger.CreateSnapshot();
            }
        }

        private static int AddBounded(int current, int delta, int maximum)
        {
            if (current < 0 || delta < 0)
            {
                throw new InvalidOperationException("Learning reward aggregate values cannot be negative.");
            }

            var result = checked(current + delta);
            if (result > maximum)
            {
                throw new OverflowException("Learning reward aggregate exceeded its supported bound.");
            }

            return result;
        }

        private static string RequireRivalId(string rivalId)
        {
            if (string.IsNullOrWhiteSpace(rivalId))
            {
                throw new ArgumentException("A stable rival ID is required.", nameof(rivalId));
            }

            return rivalId;
        }
    }
}
