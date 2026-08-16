using System;
using System.Collections.Generic;

namespace CoffeeGame.Domain
{
    public readonly struct RivalAffinityEntry
    {
        public RivalAffinityEntry(string rivalId, int affinity)
        {
            if (string.IsNullOrWhiteSpace(rivalId))
            {
                throw new ArgumentException("A stable rival ID is required.", nameof(rivalId));
            }

            if (affinity < 0 || affinity > LearningRewardAggregate.MaximumAffinity)
            {
                throw new ArgumentOutOfRangeException(nameof(affinity));
            }

            RivalId = rivalId;
            Affinity = affinity;
        }

        public string RivalId { get; }
        public int Affinity { get; }
    }

    public readonly struct PlayerLearningRewardApplication
    {
        public PlayerLearningRewardApplication(
            LearningRewardApplyStatus status,
            LearningRewardBundle reward,
            int currentAffinity,
            int recruitmentThreshold,
            bool rivalRecruited)
        {
            Status = status;
            Reward = reward;
            CurrentAffinity = currentAffinity;
            RecruitmentThreshold = recruitmentThreshold;
            RivalRecruited = rivalRecruited;
        }

        public LearningRewardApplyStatus Status { get; }
        public LearningRewardBundle Reward { get; }
        public int CurrentAffinity { get; }
        public int RecruitmentThreshold { get; }
        public bool RivalRecruited { get; }
    }

    /// <summary>
    /// Owns the player's level and inventory progression independently of presentation.
    /// </summary>
    public sealed class PlayerProgression
    {
        private readonly RewardLedger rewardLedger;
        private readonly Func<string, TalentGrowthProfile> growthProfileResolver;
        private readonly Dictionary<string, int> rivalAffinityById =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> recruitedRivalIds =
            new HashSet<string>(StringComparer.Ordinal);

        public event Action Changed;

        public PlayerProgression()
            : this(1, 0, 0, 0, null, null)
        {
        }

        public PlayerProgression(
            int level,
            int experience,
            int gold,
            int slimeJelly,
            IEnumerable<string> previouslyClaimedRewardIds = null,
            PlayerStatus status = null,
            Func<string, TalentGrowthProfile> talentGrowthProfileResolver = null,
            int talentPoints = 0,
            IEnumerable<RivalAffinityEntry> rivalAffinities = null,
            IEnumerable<string> previouslyRecruitedRivalIds = null)
        {
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), "Level must be at least one.");
            }

            if (experience < 0 || experience >= GetExperienceRequiredForNextLevel(level))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(experience),
                    "Experience must be non-negative and less than the requirement for the next level.");
            }

            if (gold < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gold), "Gold cannot be negative.");
            }

            if (slimeJelly < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slimeJelly), "Slime jelly cannot be negative.");
            }

            if (talentPoints < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(talentPoints), "Talent points cannot be negative.");
            }

            Level = level;
            Experience = experience;
            Gold = gold;
            SlimeJelly = slimeJelly;
            TalentPoints = talentPoints;
            Status = status ?? new PlayerStatus();
            growthProfileResolver = talentGrowthProfileResolver ?? TalentGrowthCatalog.Resolve;
            rewardLedger = new RewardLedger(previouslyClaimedRewardIds);

            if (rivalAffinities != null)
            {
                foreach (RivalAffinityEntry entry in rivalAffinities)
                {
                    string rivalId = RequireRivalId(entry.RivalId);
                    if (rivalAffinityById.ContainsKey(rivalId))
                    {
                        throw new ArgumentException("Rival affinity IDs must be unique.", nameof(rivalAffinities));
                    }

                    rivalAffinityById.Add(rivalId, entry.Affinity);
                }
            }

            if (previouslyRecruitedRivalIds != null)
            {
                foreach (string rivalId in previouslyRecruitedRivalIds)
                {
                    recruitedRivalIds.Add(RequireRivalId(rivalId));
                }
            }
        }

        public int Level { get; private set; }

        /// <summary>
        /// Experience accumulated within the current level.
        /// </summary>
        public int Experience { get; private set; }

        public int ExperienceRequiredForNextLevel => GetExperienceRequiredForNextLevel(Level);

        public int Gold { get; private set; }

        public int SlimeJelly { get; private set; }

        public int TalentPoints { get; private set; }

        public PlayerStatus Status { get; private set; }

        public int ClaimedRewardCount => rewardLedger.Count;

        public int GetRivalAffinity(string rivalId)
        {
            string id = RequireRivalId(rivalId);
            return rivalAffinityById.TryGetValue(id, out int affinity) ? affinity : 0;
        }

        public bool IsRivalRecruited(string rivalId)
        {
            return recruitedRivalIds.Contains(RequireRivalId(rivalId));
        }

        public static int GetExperienceRequiredForNextLevel(int level)
        {
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), "Level must be at least one.");
            }

            var requirement = 3L + ((level - 1L) * 2L);
            if (requirement > int.MaxValue)
            {
                throw new OverflowException("The experience requirement is outside the supported range.");
            }

            return (int)requirement;
        }

        /// <summary>
        /// Applies a reward atomically when its stable claim ID has not been seen before.
        /// </summary>
        /// <returns>True only when the reward was newly applied.</returns>
        public bool TryApplyReward(string claimId, RewardBundle reward)
        {
            if (rewardLedger.HasClaimed(claimId))
            {
                return false;
            }

            var nextGold = checked(Gold + reward.Gold);
            var nextSlimeJelly = checked(SlimeJelly + reward.SlimeJelly);
            var nextLevel = Level;
            var nextExperience = checked(Experience + reward.Experience);

            while (nextExperience >= GetExperienceRequiredForNextLevel(nextLevel))
            {
                nextExperience -= GetExperienceRequiredForNextLevel(nextLevel);
                nextLevel = checked(nextLevel + 1);
            }

            int levelsGained = nextLevel - Level;
            PlayerStatus nextStatus = levelsGained > 0
                ? Status.ApplyLevelGrowth(levelsGained, growthProfileResolver(Status.TalentId))
                : Status;

            // TryClaim remains the commit gate if callers ever race on the same ledger.
            if (!rewardLedger.TryClaim(claimId))
            {
                return false;
            }

            Gold = nextGold;
            SlimeJelly = nextSlimeJelly;
            Level = nextLevel;
            Experience = nextExperience;
            Status = nextStatus;
            Changed?.Invoke();
            return true;
        }

        public PlayerLearningRewardApplication TryApplyLearningOutcome(
            AuthoritativeLearningOutcome outcome,
            string rivalId,
            LearningRewardPolicyV1 policy = null)
        {
            string id = RequireRivalId(rivalId);
            var rewardPolicy = policy ?? new LearningRewardPolicyV1();
            int currentAffinity = GetRivalAffinity(id);
            int threshold = LearningRewardPolicyV1.RecruitmentThreshold;
            if (outcome.Status != AuthoritativeLearningResultStatus.Completed
                || !outcome.IsCorrect
                || !outcome.LearningMutationApplied
                || !outcome.RewardEligible)
            {
                return new PlayerLearningRewardApplication(
                    LearningRewardApplyStatus.NotEligible,
                    default,
                    currentAffinity,
                    threshold,
                    false);
            }

            string claimId = "learning:" + outcome.GrantId;
            if (rewardLedger.HasClaimed(claimId))
            {
                return new PlayerLearningRewardApplication(
                    LearningRewardApplyStatus.DuplicateGrant,
                    default,
                    currentAffinity,
                    threshold,
                    false);
            }

            LearningRewardBundle reward = rewardPolicy.Map(
                outcome.DifficultyBand,
                outcome.DifficultyLevel);
            int nextGold = checked(Gold + reward.Gold);
            int nextTalentPoints = checked(TalentPoints + reward.TalentPoints);
            int nextAffinity = checked(currentAffinity + reward.AffinityDelta);
            if (nextAffinity > LearningRewardAggregate.MaximumAffinity)
            {
                throw new OverflowException("Rival affinity exceeded its supported bound.");
            }

            int nextLevel = Level;
            int nextExperience = checked(Experience + reward.Experience);
            while (nextExperience >= GetExperienceRequiredForNextLevel(nextLevel))
            {
                nextExperience -= GetExperienceRequiredForNextLevel(nextLevel);
                nextLevel = checked(nextLevel + 1);
            }

            int levelsGained = nextLevel - Level;
            PlayerStatus nextStatus = levelsGained > 0
                ? Status.ApplyLevelGrowth(levelsGained, growthProfileResolver(Status.TalentId))
                : Status;
            bool recruitsRival = currentAffinity < threshold
                && nextAffinity >= threshold
                && !recruitedRivalIds.Contains(id);

            if (!rewardLedger.TryClaim(claimId))
            {
                return new PlayerLearningRewardApplication(
                    LearningRewardApplyStatus.DuplicateGrant,
                    default,
                    currentAffinity,
                    threshold,
                    false);
            }

            Gold = nextGold;
            TalentPoints = nextTalentPoints;
            rivalAffinityById[id] = nextAffinity;
            Level = nextLevel;
            Experience = nextExperience;
            Status = nextStatus;
            if (recruitsRival)
            {
                recruitedRivalIds.Add(id);
            }

            Changed?.Invoke();
            return new PlayerLearningRewardApplication(
                LearningRewardApplyStatus.Granted,
                reward,
                nextAffinity,
                threshold,
                recruitsRival);
        }

        public IReadOnlyList<string> CreateClaimedRewardSnapshot()
        {
            return rewardLedger.CreateSnapshot();
        }

        public IReadOnlyList<RivalAffinityEntry> CreateRivalAffinitySnapshot()
        {
            var ids = new List<string>(rivalAffinityById.Keys);
            ids.Sort(StringComparer.Ordinal);
            var snapshot = new List<RivalAffinityEntry>(ids.Count);
            foreach (string id in ids)
            {
                snapshot.Add(new RivalAffinityEntry(id, rivalAffinityById[id]));
            }

            return snapshot.AsReadOnly();
        }

        public IReadOnlyList<string> CreateRecruitedRivalSnapshot()
        {
            var snapshot = new List<string>(recruitedRivalIds);
            snapshot.Sort(StringComparer.Ordinal);
            return snapshot.AsReadOnly();
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
