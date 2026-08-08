using System;
using NUnit.Framework;

namespace CoffeeGame.Domain.Tests
{
    public sealed class RewardBundleTests
    {
        [Test]
        public void Constructor_RejectsNegativeValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardBundle(-1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardBundle(0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardBundle(0, 0, -1));
        }

        [Test]
        public void Addition_ProducesANewCombinedBundle()
        {
            var first = new RewardBundle(1, 2, 3);
            var second = new RewardBundle(4, 5, 6);

            Assert.That(first + second, Is.EqualTo(new RewardBundle(5, 7, 9)));
            Assert.That(first, Is.EqualTo(new RewardBundle(1, 2, 3)));
        }
    }
}
