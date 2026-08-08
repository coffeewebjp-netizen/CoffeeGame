using System;
using System.Collections.Generic;

namespace CoffeeGame.Domain
{
    /// <summary>
    /// Owns the player's level and inventory progression independently of presentation.
    /// </summary>
    public sealed class PlayerProgression
    {
        private readonly RewardLedger rewardLedger;

        public PlayerProgression()
            : this(1, 0, 0, 0, null)
        {
        }

        public PlayerProgression(
            int level,
            int experience,
            int gold,
            int slimeJelly,
            IEnumerable<string> previouslyClaimedRewardIds = null)
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

            Level = level;
            Experience = experience;
            Gold = gold;
            SlimeJelly = slimeJelly;
            rewardLedger = new RewardLedger(previouslyClaimedRewardIds);
        }

        public int Level { get; private set; }

        /// <summary>
        /// Experience accumulated within the current level.
        /// </summary>
        public int Experience { get; private set; }

        public int ExperienceRequiredForNextLevel => GetExperienceRequiredForNextLevel(Level);

        public int Gold { get; private set; }

        public int SlimeJelly { get; private set; }

        public int ClaimedRewardCount => rewardLedger.Count;

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

            // TryClaim remains the commit gate if callers ever race on the same ledger.
            if (!rewardLedger.TryClaim(claimId))
            {
                return false;
            }

            Gold = nextGold;
            SlimeJelly = nextSlimeJelly;
            Level = nextLevel;
            Experience = nextExperience;
            return true;
        }

        public IReadOnlyList<string> CreateClaimedRewardSnapshot()
        {
            return rewardLedger.CreateSnapshot();
        }
    }
}
