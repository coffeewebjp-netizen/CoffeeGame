using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Domain.Tests
{
    public sealed class CombatTuningTests
    {
        private CombatTuning tuning;

        [SetUp]
        public void SetUp()
        {
            tuning = CombatTuning.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void DefaultFactory_UsesBrowserValuesAtOneHundredPixelsPerMeter()
        {
            Assert.That(tuning.PlayerMaxHealth, Is.EqualTo(24));
            Assert.That(tuning.PlayerMaxMp, Is.EqualTo(12));
            Assert.That(tuning.WalkSpeed, Is.EqualTo(1.55f).Within(0.0001f));
            Assert.That(tuning.RunSpeed, Is.EqualTo(2.45f).Within(0.0001f));
            Assert.That(tuning.JumpVelocity, Is.EqualTo(4.8f).Within(0.0001f));
            Assert.That(tuning.Gravity, Is.EqualTo(11.8f).Within(0.0001f));
            Assert.That(tuning.AirControl, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(tuning.SwordRange, Is.EqualTo(0.78f).Within(0.0001f));
            Assert.That(tuning.AirSlashRange, Is.EqualTo(0.94f).Within(0.0001f));
            Assert.That(tuning.PlungeRadius, Is.EqualTo(1.18f).Within(0.0001f));
            Assert.That(tuning.SpecialRange, Is.EqualTo(1.42f).Within(0.0001f));
            Assert.That(tuning.SpecialStaminaCost, Is.EqualTo(100));
            Assert.That(tuning.MagicProjectileSpeed, Is.EqualTo(4.4f).Within(0.0001f));
            Assert.That(tuning.MagicMpRegenPerSecond, Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(tuning.SlimeSpeed, Is.EqualTo(0.68f).Within(0.0001f));
            Assert.That(tuning.SlimeAttackRange, Is.EqualTo(1.6f).Within(0.0001f));
            Assert.That(tuning.RivalEncounterIntervalKills, Is.EqualTo(5));
            Assert.That(tuning.SlimeReward, Is.EqualTo(new RewardBundle(1, 1, 1)));
            Assert.That(tuning.IsValid, Is.True);
        }

        [Test]
        public void Validation_ReportsBrokenCrossFieldRules()
        {
            SetPrivateField("runSpeed", 1f);
            SetPrivateField("magicCost", 13);

            var errors = tuning.GetValidationErrors();

            Assert.That(errors, Has.Some.Contains("runSpeed"));
            Assert.That(errors, Has.Some.Contains("magicCost"));
            Assert.Throws<System.InvalidOperationException>(() => tuning.ValidateOrThrow());
        }

        private void SetPrivateField(string fieldName, object value)
        {
            var field = typeof(CombatTuning).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing test field: {fieldName}");
            field.SetValue(tuning, value);
        }
    }
}
