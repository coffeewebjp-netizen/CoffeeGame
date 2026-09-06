using System;
using NUnit.Framework;

namespace CoffeeGame.Domain.Tests
{
    public sealed class PlayerPartyTests
    {
        private static readonly DateTime Anchor =
            new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void RuntimeMaximums_InitializeUnconfiguredResourcesOnlyOnce()
        {
            var member = new PartyMember(
                PartyMemberIds.Hero,
                1,
                0,
                new PlayerStatus(),
                recoveryAnchorUtc: Anchor);

            Assert.That(member.ResourcesInitialized, Is.False);
            member.SetMaximumResources(80d, 150d, 100d, Anchor);

            Assert.That(member.ResourcesInitialized, Is.True);
            Assert.That(member.Resources.HitPoints, Is.EqualTo(80d));
            Assert.That(member.Resources.MagicPoints, Is.EqualTo(150d));
            Assert.That(member.Resources.Special, Is.Zero);

            member.SetResources(20d, 30d, 40d, Anchor);
            member.SetMaximumResources(100d, 200d, 120d, Anchor);

            Assert.That(member.Resources.HitPoints, Is.EqualTo(20d));
            Assert.That(member.Resources.MagicPoints, Is.EqualTo(30d));
            Assert.That(member.Resources.Special, Is.EqualTo(40d));
        }

        [Test]
        public void Knockout_RecoversAtSixHundredSecondsThenUsesLeftoverRestTime()
        {
            PartyMember member = InitializedMember(maximumHitPoints: 80d, maximumMagicPoints: 150d);
            var party = new PlayerParty(new[] { member });
            party.KnockOut(member.Id, Anchor);

            party.Settle(Anchor.AddSeconds(599.9d));
            Assert.That(member.RecoveryState, Is.EqualTo(PartyRecoveryState.KnockedOut));
            Assert.That(member.Resources.HitPoints, Is.Zero);

            party.Settle(Anchor.AddSeconds(600d));
            Assert.That(member.RecoveryState, Is.EqualTo(PartyRecoveryState.Resting));
            Assert.That(member.Resources.HitPoints, Is.EqualTo(1d));
            Assert.That(member.Resources.MagicPoints, Is.EqualTo(30d));
            Assert.That(member.Resources.Special, Is.Zero);

            party.Settle(Anchor.AddSeconds(1500d));
            Assert.That(member.Resources.HitPoints, Is.EqualTo(80d));
            Assert.That(member.Resources.MagicPoints, Is.EqualTo(150d));
        }

        [Test]
        public void Knockout_WithMaximumBelowOneHundredStillRecoversOneHitPoint()
        {
            PartyMember member = InitializedMember(maximumHitPoints: 7d, maximumMagicPoints: 0d);
            var party = new PlayerParty(new[] { member });

            party.KnockOut(member.Id, Anchor);
            party.Settle(Anchor.AddSeconds(600d));

            Assert.That(member.Resources.HitPoints, Is.EqualTo(1d));
            Assert.That(member.Resources.MagicPoints, Is.Zero);
            Assert.That(member.RecoveryState, Is.EqualTo(PartyRecoveryState.Resting));
        }

        [Test]
        public void Rest_RecoversHpAndMpConcurrentlyWithFractionalPrecisionButNotSpecial()
        {
            PartyMember member = InitializedMember(maximumHitPoints: 3d, maximumMagicPoints: 1d);
            member.SetResources(0.5d, 0.25d, 50d, Anchor);
            var party = new PlayerParty(new[] { member });

            party.Settle(Anchor.AddSeconds(150d));

            Assert.That(member.Resources.HitPoints, Is.EqualTo(1d).Within(0.0000001d));
            Assert.That(member.Resources.MagicPoints, Is.EqualTo(5d / 12d).Within(0.0000001d));
            Assert.That(member.Resources.Special, Is.EqualTo(50d));

            party.Settle(Anchor.AddSeconds(450d));
            Assert.That(member.Resources.HitPoints, Is.EqualTo(2d).Within(0.0000001d));
            Assert.That(member.Resources.MagicPoints, Is.EqualTo(0.75d).Within(0.0000001d));
        }

        [Test]
        public void Settlement_ClampsBackwardClockAndDoesNotApplySameIntervalTwice()
        {
            PartyMember member = InitializedMember();
            member.SetResources(10d, 10d, 25d, Anchor);
            var party = new PlayerParty(new[] { member });

            party.Settle(Anchor.AddSeconds(90d));
            double afterFirst = member.Resources.HitPoints;
            party.Settle(Anchor.AddSeconds(90d));
            party.Settle(Anchor.AddSeconds(60d));

            Assert.That(member.Resources.HitPoints, Is.EqualTo(afterFirst));
            Assert.That(member.Resources.MagicPoints, Is.EqualTo(20d).Within(0.0000001d));
        }

        [Test]
        public void KnockedOutControlledHero_DeploysLivingReserveAndPartySurvives()
        {
            var progression = RecruitedProgression();
            PartyMember hero = progression.Party.Find(PartyMemberIds.Hero);
            PartyMember cat = progression.Party.Find(PartyMemberIds.CatMage);
            hero.SetMaximumResources(100d, 100d, 100d, Anchor);
            cat.SetMaximumResources(80d, 150d, 100d, Anchor);
            progression.Party.TryDeploy(hero.Id, Anchor);
            progression.Party.TryRest(cat.Id, Anchor);
            progression.Party.TrySetPreferredControlledMember(hero.Id);

            progression.Party.KnockOut(hero.Id, Anchor);

            Assert.That(progression.Party.IsDefeated, Is.False);
            Assert.That(cat.RecoveryState, Is.EqualTo(PartyRecoveryState.Deployed));
            Assert.That(progression.Party.PreferredControlledMemberId, Is.EqualTo(cat.Id));
            Assert.That(progression.Party.TryDeploy(hero.Id, Anchor.AddSeconds(599d)), Is.False);
        }

        [Test]
        public void NormalExit_RestsLivingMembersAndRetainsKnockoutDeadline()
        {
            var progression = RecruitedProgression();
            PartyMember hero = progression.Party.Find(PartyMemberIds.Hero);
            PartyMember cat = progression.Party.Find(PartyMemberIds.CatMage);
            hero.SetMaximumResources(100d, 100d, 100d, Anchor);
            cat.SetMaximumResources(80d, 150d, 100d, Anchor);
            progression.Party.TryDeploy(hero.Id, Anchor);
            progression.Party.TryDeploy(cat.Id, Anchor);
            progression.Party.KnockOut(cat.Id, Anchor);
            DateTime? deadline = cat.RecoverableAtUtc;

            progression.Party.RestAllLiving(Anchor.AddSeconds(30d));

            Assert.That(hero.RecoveryState, Is.EqualTo(PartyRecoveryState.Resting));
            Assert.That(cat.RecoveryState, Is.EqualTo(PartyRecoveryState.KnockedOut));
            Assert.That(cat.RecoverableAtUtc, Is.EqualTo(deadline));
        }

        [Test]
        public void RewardExperience_IsIndependentWhileGoldAndClaimsRemainShared()
        {
            var progression = RecruitedProgression();
            PartyMember hero = progression.Party.Find(PartyMemberIds.Hero);
            PartyMember cat = progression.Party.Find(PartyMemberIds.CatMage);
            int goldBefore = progression.Gold;

            Assert.That(progression.TryApplyReward("party-reward", new RewardBundle(3, 7, 2)), Is.True);
            Assert.That(progression.TryApplyReward("party-reward", new RewardBundle(3, 7, 2)), Is.False);

            Assert.That(hero.Level, Is.EqualTo(cat.Level));
            Assert.That(hero.Experience, Is.EqualTo(cat.Experience));
            Assert.That(progression.Gold, Is.EqualTo(goldBefore + 7));
            Assert.That(progression.SlimeJelly, Is.EqualTo(2));

            cat.AddExperience(2);
            Assert.That(cat.Experience, Is.Not.EqualTo(hero.Experience));
        }

        [Test]
        public void ExistingSilverRecruitment_CreatesCatExactlyOnce()
        {
            var progression = new PlayerProgression(
                2,
                1,
                0,
                0,
                rivalAffinities: new[] { new RivalAffinityEntry(RivalCharacterIds.WeaknessChallenger, 100) });

            Assert.That(progression.IsRivalRecruited(RivalCharacterIds.WeaknessChallenger), Is.True);
            Assert.That(progression.Party.Members.Count, Is.EqualTo(2));
            Assert.That(progression.Party.Find(PartyMemberIds.CatMage), Is.Not.Null);
        }

        [Test]
        public void RecruitmentGrant_AddsDeployedCatOnceAndSharesThatGrantExperience()
        {
            var progression = new PlayerProgression(
                1,
                0,
                0,
                0,
                rivalAffinities: new[] { new RivalAffinityEntry(RivalCharacterIds.WeaknessChallenger, 97) });
            var outcome = new AuthoritativeLearningOutcome(
                "result-recruit",
                AuthoritativeLearningResultStatus.Completed,
                true,
                true,
                true,
                "grant-recruit",
                LearningDifficultyBand.Foundation,
                1);

            PlayerLearningRewardApplication first = progression.TryApplyLearningOutcome(
                outcome,
                RivalCharacterIds.WeaknessChallenger);
            PlayerLearningRewardApplication duplicate = progression.TryApplyLearningOutcome(
                outcome,
                RivalCharacterIds.WeaknessChallenger);
            PartyMember hero = progression.Party.Find(PartyMemberIds.Hero);
            PartyMember cat = progression.Party.Find(PartyMemberIds.CatMage);

            Assert.That(first.RivalRecruited, Is.True);
            Assert.That(duplicate.Status, Is.EqualTo(LearningRewardApplyStatus.DuplicateGrant));
            Assert.That(progression.Party.Members.Count, Is.EqualTo(2));
            Assert.That(cat.RecoveryState, Is.EqualTo(PartyRecoveryState.Deployed));
            Assert.That(cat.Level, Is.EqualTo(hero.Level));
            Assert.That(cat.Experience, Is.EqualTo(hero.Experience));
        }

        [Test]
        public void PartyMutationEvent_RemainsSeparateFromProgressionChanged()
        {
            var progression = new PlayerProgression();
            int partyChanges = 0;
            int progressionChanges = 0;
            progression.Party.Changed += _ => partyChanges++;
            progression.Changed += () => progressionChanges++;

            progression.Party.Find(PartyMemberIds.Hero)
                .SetMaximumResources(100d, 40d, 100d, DateTime.UtcNow);

            Assert.That(partyChanges, Is.EqualTo(1));
            Assert.That(progressionChanges, Is.Zero);
        }

        private static PartyMember InitializedMember(
            double maximumHitPoints = 100d,
            double maximumMagicPoints = 100d)
        {
            return new PartyMember(
                PartyMemberIds.Hero,
                1,
                0,
                new PlayerStatus(),
                hitPoints: Math.Min(10d, maximumHitPoints),
                maximumHitPoints: maximumHitPoints,
                magicPoints: Math.Min(30d, maximumMagicPoints),
                maximumMagicPoints: maximumMagicPoints,
                special: 50d,
                maximumSpecial: 100d,
                recoveryState: PartyRecoveryState.Resting,
                recoveryAnchorUtc: Anchor,
                resourcesInitialized: true);
        }

        private static PlayerProgression RecruitedProgression()
        {
            return new PlayerProgression(
                1,
                0,
                0,
                0,
                rivalAffinities: new[] { new RivalAffinityEntry(RivalCharacterIds.WeaknessChallenger, 100) },
                previouslyRecruitedRivalIds: new[] { RivalCharacterIds.WeaknessChallenger });
        }
    }
}
