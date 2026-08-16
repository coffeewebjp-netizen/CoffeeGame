using NUnit.Framework;

namespace CoffeeGame.Domain.Tests
{
    public sealed class LearningRewardDomainTests
    {
        [Test]
        public void CorrectAuthoritativeResult_GrantsOnceAndDuplicateReplayIsNoOp()
        {
            var aggregate = new LearningRewardAggregate(recruitmentThreshold: 6);
            var outcome = Completed("gr_test_001", LearningDifficultyBand.Intermediate, 3);

            var first = aggregate.TryApply(outcome, "rival-fox-001");
            var duplicate = aggregate.TryApply(outcome, "rival-fox-001");

            Assert.That(first.Status, Is.EqualTo(LearningRewardApplyStatus.Granted));
            Assert.That(first.Reward, Is.EqualTo(new LearningRewardBundle(2, 9, 6, 6)));
            Assert.That(first.RivalRecruited, Is.True);
            Assert.That(duplicate.Status, Is.EqualTo(LearningRewardApplyStatus.DuplicateGrant));
            Assert.That(aggregate.TalentPoints, Is.EqualTo(2));
            Assert.That(aggregate.Experience, Is.EqualTo(9));
            Assert.That(aggregate.Gold, Is.EqualTo(6));
            Assert.That(aggregate.GetAffinity("rival-fox-001"), Is.EqualTo(6));
            Assert.That(aggregate.ConsumedGrantCount, Is.EqualTo(1));
        }

        [Test]
        public void AffinityThresholdCrossing_RecruitsRivalExactlyOnce()
        {
            var aggregate = new LearningRewardAggregate(recruitmentThreshold: 6);

            var first = aggregate.TryApply(
                Completed("gr_test_001", LearningDifficultyBand.Foundation, 1),
                "rival-fox-001");
            var crossing = aggregate.TryApply(
                Completed("gr_test_002", LearningDifficultyBand.Foundation, 1),
                "rival-fox-001");
            var later = aggregate.TryApply(
                Completed("gr_test_003", LearningDifficultyBand.Foundation, 1),
                "rival-fox-001");

            Assert.That(first.RivalRecruited, Is.False);
            Assert.That(crossing.RivalRecruited, Is.True);
            Assert.That(later.RivalRecruited, Is.False);
            Assert.That(aggregate.IsRecruited("rival-fox-001"), Is.True);
        }

        [Test]
        public void PendingIncorrectIneligibleOrUnappliedMutation_GrantNothing()
        {
            var aggregate = new LearningRewardAggregate(recruitmentThreshold: 5);
            var pending = new AuthoritativeLearningOutcome(
                "rs_pending_001",
                AuthoritativeLearningResultStatus.Pending,
                false,
                false,
                false,
                null,
                LearningDifficultyBand.Foundation,
                0);
            var incorrect = new AuthoritativeLearningOutcome(
                "rs_wrong_001",
                AuthoritativeLearningResultStatus.Completed,
                false,
                true,
                false,
                null,
                LearningDifficultyBand.Intermediate,
                3);
            var mutationNotApplied = new AuthoritativeLearningOutcome(
                "rs_unapplied_001",
                AuthoritativeLearningResultStatus.Completed,
                true,
                false,
                true,
                "gr_unapplied_001",
                LearningDifficultyBand.Advanced,
                5);

            Assert.That(aggregate.TryApply(pending, "rival-fox-001").Status, Is.EqualTo(LearningRewardApplyStatus.NotEligible));
            Assert.That(aggregate.TryApply(incorrect, "rival-fox-001").Status, Is.EqualTo(LearningRewardApplyStatus.NotEligible));
            Assert.That(aggregate.TryApply(mutationNotApplied, "rival-fox-001").Status, Is.EqualTo(LearningRewardApplyStatus.NotEligible));
            Assert.That(aggregate.ConsumedGrantCount, Is.Zero);
            Assert.That(aggregate.TalentPoints, Is.Zero);
            Assert.That(aggregate.GetAffinity("rival-fox-001"), Is.Zero);
        }

        [Test]
        public void ApprovedPolicy_MapsEverySemanticBandAndLevelDeterministically()
        {
            var policy = new LearningRewardPolicyV1();

            Assert.That(policy.Map(LearningDifficultyBand.Foundation, 1),
                Is.EqualTo(new LearningRewardBundle(1, 2, 1, 3)));
            Assert.That(policy.Map(LearningDifficultyBand.Intermediate, 3),
                Is.EqualTo(new LearningRewardBundle(2, 9, 6, 6)));
            Assert.That(policy.Map(LearningDifficultyBand.Advanced, 5),
                Is.EqualTo(new LearningRewardBundle(3, 20, 15, 9)));
            Assert.That(LearningRewardPolicyV1.PolicyId,
                Is.EqualTo("coffee-game-rival-reward-v1"));
            Assert.That(LearningRewardPolicyV1.RecruitmentThreshold, Is.EqualTo(100));
        }

        private static AuthoritativeLearningOutcome Completed(
            string grantId,
            LearningDifficultyBand band,
            int level)
        {
            return new AuthoritativeLearningOutcome(
                "rs_" + grantId,
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
