using CoffeeGame.Combat;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class IaiCinematicTimingTests
    {
        [Test]
        public void Cinematic_UsesBlackoutBeforeTheStrikeAndWhiteFlashAtImpact()
        {
            IaiCinematicFrame anticipation = IaiCinematicTiming.Sample(0.1f);
            IaiCinematicFrame impact = IaiCinematicTiming.Sample(0.205f);

            Assert.That(anticipation.BlackoutAlpha, Is.GreaterThan(0.9f));
            Assert.That(anticipation.SlashAlpha, Is.GreaterThan(0.9f));
            Assert.That(anticipation.SlashProgress, Is.InRange(0f, 1f));
            Assert.That(impact.FlashAlpha, Is.GreaterThan(0.9f));
        }

        [Test]
        public void Cinematic_FadesCompletelyByItsDeclaredDuration()
        {
            IaiCinematicFrame finished = IaiCinematicTiming.Sample(IaiCinematicTiming.Duration);

            Assert.That(finished.BlackoutAlpha, Is.EqualTo(0f).Within(0.001f));
            Assert.That(finished.FlashAlpha, Is.EqualTo(0f).Within(0.001f));
            Assert.That(finished.SlashAlpha, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void SpawnIaiCinematic_CreatesAVisualOnlyScreenEffect()
        {
            GameObject effect = CombatVfxFactory.SpawnIaiCinematic(
                Vector3.zero,
                Vector3.forward,
                1.42f);
            try
            {
                Assert.That(effect.GetComponent<IaiCinematicEffect>(), Is.Not.Null);
                Assert.That(effect.GetComponent<Collider>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(effect);
            }
        }
    }
}
