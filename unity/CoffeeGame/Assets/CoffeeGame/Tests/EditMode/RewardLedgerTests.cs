using System;
using NUnit.Framework;

namespace CoffeeGame.Domain.Tests
{
    public sealed class RewardLedgerTests
    {
        [Test]
        public void TryClaim_OnlyAcceptsAClaimIdOnce()
        {
            var ledger = new RewardLedger();

            Assert.That(ledger.TryClaim("encounter-01/slime-01"), Is.True);
            Assert.That(ledger.TryClaim("encounter-01/slime-01"), Is.False);
            Assert.That(ledger.Count, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_RestoresClaimsAndSnapshotIsStable()
        {
            var ledger = new RewardLedger(new[] { "claim-b", "claim-a" });

            Assert.That(ledger.HasClaimed("claim-a"), Is.True);
            Assert.That(ledger.CreateSnapshot(), Is.EqualTo(new[] { "claim-a", "claim-b" }));
        }

        [Test]
        public void ClaimOperations_RejectMissingIds()
        {
            var ledger = new RewardLedger();

            Assert.Throws<ArgumentException>(() => ledger.TryClaim("  "));
            Assert.Throws<ArgumentException>(() => ledger.HasClaimed(null));
        }
    }
}
