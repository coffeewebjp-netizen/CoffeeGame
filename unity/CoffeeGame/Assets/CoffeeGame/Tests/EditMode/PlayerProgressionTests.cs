using NUnit.Framework;

namespace CoffeeGame.Domain.Tests
{
    public sealed class PlayerProgressionTests
    {
        [Test]
        public void ReplaceFrom_CopiesLevelGoldAndStatusIntoTheLiveObject()
        {
            var live = new PlayerProgression();
            var imported = new PlayerProgression(13, 17, 180, 175);
            live.ReplaceFrom(imported);
            Assert.That(live.Level, Is.EqualTo(13));
            Assert.That(live.Experience, Is.EqualTo(17));
            Assert.That(live.Gold, Is.EqualTo(180));
            Assert.That(live.SlimeJelly, Is.EqualTo(175));
        }

        [Test]
        public void RivalCatalog_ListsSilverAndSplitInkPortraits()
        {
            Assert.That(
                RivalCharacterIds.All,
                Is.EqualTo(new[]
                {
                    RivalCharacterIds.WeaknessChallenger,
                    RivalCharacterIds.SplitInk
                }));
            Assert.That(
                RivalCharacterIds.DisplayName(RivalCharacterIds.WeaknessChallenger),
                Is.EqualTo("白銀のライバル"));
            Assert.That(
                RivalCharacterIds.DisplayName(RivalCharacterIds.SplitInk),
                Is.EqualTo("白黒のライバル"));
        }

        [Test]
        public void NewPlayer_StartsAtLevelOneWithThreeXpRequirement()
        {
            var progression = new PlayerProgression();

            Assert.That(progression.Level, Is.EqualTo(1));
            Assert.That(progression.Experience, Is.Zero);
            Assert.That(progression.ExperienceRequiredForNextLevel, Is.EqualTo(3));
            Assert.That(progression.Gold, Is.Zero);
            Assert.That(progression.SlimeJelly, Is.Zero);
            Assert.That(progression.TalentPoints, Is.Zero);
        }

        [Test]
        public void TryApplyReward_AppliesEachClaimExactlyOnce()
        {
            var progression = new PlayerProgression();
            var slimeReward = new RewardBundle(1, 1, 1);

            Assert.That(progression.TryApplyReward("battle-01/slime-01", slimeReward), Is.True);
            Assert.That(progression.TryApplyReward("battle-01/slime-01", slimeReward), Is.False);
            Assert.That(progression.Experience, Is.EqualTo(1));
            Assert.That(progression.Gold, Is.EqualTo(1));
            Assert.That(progression.SlimeJelly, Is.EqualTo(1));
            Assert.That(progression.ClaimedRewardCount, Is.EqualTo(1));
        }

        [Test]
        public void Experience_CanCrossMultipleLevelsAndRequirementGrowsByTwo()
        {
            var progression = new PlayerProgression();

            progression.TryApplyReward("large-reward", new RewardBundle(8, 0, 0));

            Assert.That(progression.Level, Is.EqualTo(3));
            Assert.That(progression.Experience, Is.Zero);
            Assert.That(progression.ExperienceRequiredForNextLevel, Is.EqualTo(7));
        }

        [Test]
        public void RestoredClaim_CannotBeAppliedAgain()
        {
            var progression = new PlayerProgression(
                level: 2,
                experience: 1,
                gold: 4,
                slimeJelly: 2,
                previouslyClaimedRewardIds: new[] { "learning/daily/2026-08-08" });

            var applied = progression.TryApplyReward(
                "learning/daily/2026-08-08",
                new RewardBundle(100, 100, 100));

            Assert.That(applied, Is.False);
            Assert.That(progression.Level, Is.EqualTo(2));
            Assert.That(progression.Experience, Is.EqualTo(1));
            Assert.That(progression.Gold, Is.EqualTo(4));
            Assert.That(progression.SlimeJelly, Is.EqualTo(2));
        }

        [Test]
        public void LevelUp_UsesTheGrowthProfileSelectedByTalentId()
        {
            var status = new PlayerStatus(
                PlayerStatus.DefaultArchetypeId,
                PlayerStatus.DefaultClassName,
                "power-focused",
                "豪腕",
                null,
                null);
            var progression = new PlayerProgression(
                1,
                0,
                0,
                0,
                status: status,
                talentGrowthProfileResolver: talentId => new TalentGrowthProfile(
                    talentId,
                    new[] { new PlayerGrowthRule(PlayerAttributeIds.Strength, 2000) }));

            progression.TryApplyReward("level-up", new RewardBundle(3, 0, 0));

            Assert.That(progression.Level, Is.EqualTo(2));
            Assert.That(progression.Status.Strength, Is.EqualTo(12));
            Assert.That(progression.Status.Agility, Is.EqualTo(10));
        }

        [Test]
        public void CorrectLearningOutcome_AppliesApprovedRewardExactlyOnce()
        {
            var progression = new PlayerProgression();
            var outcome = Completed("grant-001", LearningDifficultyBand.Intermediate, 3);
            int changedCount = 0;
            progression.Changed += () => changedCount++;

            PlayerLearningRewardApplication first = progression.TryApplyLearningOutcome(
                outcome,
                "rival-silver-001");
            PlayerLearningRewardApplication duplicate = progression.TryApplyLearningOutcome(
                outcome,
                "rival-silver-001");

            Assert.That(first.Status, Is.EqualTo(LearningRewardApplyStatus.Granted));
            Assert.That(first.Reward, Is.EqualTo(new LearningRewardBundle(2, 9, 6, 6)));
            Assert.That(first.CurrentAffinity, Is.EqualTo(6));
            Assert.That(first.RecruitmentThreshold, Is.EqualTo(100));
            Assert.That(first.RivalRecruited, Is.False);
            Assert.That(duplicate.Status, Is.EqualTo(LearningRewardApplyStatus.DuplicateGrant));
            Assert.That(progression.Level, Is.EqualTo(3));
            Assert.That(progression.Experience, Is.EqualTo(1));
            Assert.That(progression.Gold, Is.EqualTo(6));
            Assert.That(progression.TalentPoints, Is.EqualTo(2));
            Assert.That(progression.GetRivalAffinity("rival-silver-001"), Is.EqualTo(6));
            Assert.That(progression.ClaimedRewardCount, Is.EqualTo(1));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void LearningAffinity_CrossesOneHundredAndRecruitsExactlyOnce()
        {
            var progression = new PlayerProgression(
                1,
                0,
                0,
                0,
                talentPoints: 0,
                rivalAffinities: new[] { new RivalAffinityEntry("rival-silver-001", 97) });

            PlayerLearningRewardApplication crossing = progression.TryApplyLearningOutcome(
                Completed("grant-crossing", LearningDifficultyBand.Foundation, 1),
                "rival-silver-001");
            PlayerLearningRewardApplication later = progression.TryApplyLearningOutcome(
                Completed("grant-later", LearningDifficultyBand.Foundation, 1),
                "rival-silver-001");

            Assert.That(crossing.RivalRecruited, Is.True);
            Assert.That(later.RivalRecruited, Is.False);
            Assert.That(progression.IsRivalRecruited("rival-silver-001"), Is.True);
            Assert.That(progression.GetRivalAffinity("rival-silver-001"), Is.EqualTo(103));
        }

        private static AuthoritativeLearningOutcome Completed(
            string grantId,
            LearningDifficultyBand band,
            int level)
        {
            return new AuthoritativeLearningOutcome(
                "result-" + grantId,
                AuthoritativeLearningResultStatus.Completed,
                true,
                true,
                true,
                grantId,
                band,
                level);
        }
    }
}
