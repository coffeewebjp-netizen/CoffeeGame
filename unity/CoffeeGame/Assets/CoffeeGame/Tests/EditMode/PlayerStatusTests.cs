using System.Linq;
using NUnit.Framework;

namespace CoffeeGame.Domain.Tests
{
    public sealed class PlayerStatusTests
    {
        [Test]
        public void DefaultStatus_StartsAsNamelessSwordsmanWithNeutralAttributes()
        {
            var status = new PlayerStatus();

            Assert.That(status.ClassName, Is.EqualTo("名もなき剣士"));
            Assert.That(status.Strength, Is.EqualTo(10));
            Assert.That(status.Agility, Is.EqualTo(10));
            Assert.That(status.Technique, Is.EqualTo(10));
            Assert.That(status.Luck, Is.EqualTo(10));
            Assert.That(status.Vitality, Is.EqualTo(10));
            Assert.That(status.Talent, Is.EqualTo("なし"));
        }

        [Test]
        public void Constructor_RejectsEmptyLabelsAndOutOfRangeAttributes()
        {
            Assert.That(
                () => new PlayerStatus(" ", 10, 10, 10, 10, 10, "なし"),
                Throws.ArgumentException);
            Assert.That(
                () => new PlayerStatus("剣士", 0, 10, 10, 10, 10, "なし"),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(
                () => new PlayerStatus("剣士", 10, 10, 10, 10, 10, null),
                Throws.ArgumentException);
        }

        [Test]
        public void PlayerProgression_PreservesProvidedStatus()
        {
            var status = new PlayerStatus("流浪の剣士", 12, 14, 13, 8, 11, "剣閃");
            var progression = new PlayerProgression(1, 0, 0, 0, status: status);

            Assert.That(progression.Status, Is.SameAs(status));
        }

        [Test]
        public void ExtensibleAttributes_PreserveUnknownIdsAndMergeNewDefaults()
        {
            var status = new PlayerStatus(
                PlayerStatus.DefaultArchetypeId,
                PlayerStatus.DefaultClassName,
                TalentGrowthCatalog.NoneTalentId,
                PlayerStatus.DefaultTalentName,
                new[] { new PlayerAttributeValue("spirit", 17) },
                null);

            Assert.That(status.Attributes.GetValue("spirit"), Is.EqualTo(17));
            Assert.That(status.Strength, Is.EqualTo(10));
            Assert.That(status.Attributes.CreateSnapshot().Any(value => value.Id == "spirit" && value.Value == 17), Is.True);
        }

        [Test]
        public void FractionalTalentGrowth_AccumulatesDeterministicallyAcrossLevels()
        {
            var profile = new TalentGrowthProfile(
                "late-bloomer",
                new[] { new PlayerGrowthRule(PlayerAttributeIds.Strength, 1500) });

            PlayerStatus afterOneLevel = new PlayerStatus().ApplyLevelGrowth(1, profile);
            PlayerStatus afterTwoLevels = afterOneLevel.ApplyLevelGrowth(1, profile);

            Assert.That(afterOneLevel.Strength, Is.EqualTo(11));
            Assert.That(afterOneLevel.CreateGrowthRemainderSnapshot().Single().GrowthUnits, Is.EqualTo(500));
            Assert.That(afterTwoLevels.Strength, Is.EqualTo(13));
            Assert.That(afterTwoLevels.CreateGrowthRemainderSnapshot().Single().GrowthUnits, Is.Zero);
        }

        [Test]
        public void DerivedStats_AreNeutralAtTenAndRespondToTheirDocumentedAttributes()
        {
            PlayerDerivedStats neutral = PlayerDerivedStatCalculator.Calculate(new PlayerStatus());
            PlayerDerivedStats trained = PlayerDerivedStatCalculator.Calculate(
                new PlayerStatus("名もなき剣士", 20, 20, 20, 20, 20, "なし"));

            Assert.That(neutral.AttackMultiplier, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(neutral.MovementSpeedMultiplier, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(neutral.CriticalChance, Is.Zero);
            Assert.That(neutral.EvasionChance, Is.Zero);
            Assert.That(neutral.SpecialChargeSpeedMultiplier, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(neutral.MaxStaminaMultiplier, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(neutral.IncomingDamageMultiplier, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(trained.AttackMultiplier, Is.GreaterThan(1f));
            Assert.That(trained.MovementSpeedMultiplier, Is.GreaterThan(1f));
            Assert.That(trained.CriticalChance, Is.GreaterThan(0f));
            Assert.That(trained.EvasionChance, Is.GreaterThan(0f));
            Assert.That(trained.SpecialChargeSpeedMultiplier, Is.GreaterThan(1f));
            Assert.That(trained.MaxStaminaMultiplier, Is.GreaterThan(1f));
            Assert.That(trained.IncomingDamageMultiplier, Is.LessThan(1f));
        }

        [Test]
        public void TalentGrowth_CapsAttributesAtTheSupportedMaximum()
        {
            var status = new PlayerStatus(
                PlayerStatus.DefaultClassName,
                PlayerAttributeSet.MaximumValue,
                10,
                10,
                10,
                10,
                PlayerStatus.DefaultTalentName);
            var profile = new TalentGrowthProfile(
                TalentGrowthCatalog.NoneTalentId,
                new[] { new PlayerGrowthRule(PlayerAttributeIds.Strength, 5000) });

            PlayerStatus grown = status.ApplyLevelGrowth(2, profile);

            Assert.That(grown.Strength, Is.EqualTo(PlayerAttributeSet.MaximumValue));
        }
    }
}
