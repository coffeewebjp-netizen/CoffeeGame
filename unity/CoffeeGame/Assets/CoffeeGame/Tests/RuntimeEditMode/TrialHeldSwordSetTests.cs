using NUnit.Framework;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class TrialHeldSwordSetTests
    {
        [TestCase(CharacterAction.Sword, true)]
        [TestCase(CharacterAction.AirSlash, true)]
        [TestCase(CharacterAction.SpinRelease, true)]
        [TestCase(CharacterAction.Plunge, true)]
        [TestCase(CharacterAction.Idle, false)]
        [TestCase(CharacterAction.Walk, false)]
        [TestCase(CharacterAction.Run, false)]
        [TestCase(CharacterAction.Jump, false)]
        [TestCase(CharacterAction.Dodge, false)]
        [TestCase(CharacterAction.MagicCharge, false)]
        [TestCase(CharacterAction.MagicRelease, false)]
        [TestCase(CharacterAction.Hurt, false)]
        public void UsesHeldSwordSet_OnlyDrawnSwordCombat(CharacterAction action, bool expected)
        {
            Assert.That(ModelCharacterVisual.UsesHeldSwordSet(action), Is.EqualTo(expected));
        }
    }
}
