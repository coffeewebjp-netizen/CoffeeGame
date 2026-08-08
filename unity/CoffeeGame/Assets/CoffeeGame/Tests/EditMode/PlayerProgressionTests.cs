using NUnit.Framework;

namespace CoffeeGame.Domain.Tests
{
    public sealed class PlayerProgressionTests
    {
        [Test]
        public void NewPlayer_StartsAtLevelOneWithThreeXpRequirement()
        {
            var progression = new PlayerProgression();

            Assert.That(progression.Level, Is.EqualTo(1));
            Assert.That(progression.Experience, Is.Zero);
            Assert.That(progression.ExperienceRequiredForNextLevel, Is.EqualTo(3));
            Assert.That(progression.Gold, Is.Zero);
            Assert.That(progression.SlimeJelly, Is.Zero);
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
    }
}
