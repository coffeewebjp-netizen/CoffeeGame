using System;
using NUnit.Framework;

namespace CoffeeGame.Domain.Tests
{
    public sealed class LearningScheduleAndRivalSelectorTests
    {
        [Test]
        public void SyncSchedule_IsDueAtStartup()
        {
            var schedule = new LearningSyncSchedulePolicy();

            Assert.That(schedule.HasSuccessfulSync, Is.False);
            Assert.That(schedule.IsDue(0), Is.True);
            Assert.That(schedule.IsDue(5_000), Is.True);
        }

        [Test]
        public void SyncSchedule_IsNotDueBeforeProviderDelayAndIsDueAtBoundary()
        {
            var schedule = new LearningSyncSchedulePolicy()
                .AfterSuccessfulSync(completedAtUnixSeconds: 1_000, syncAfterSeconds: 900);

            Assert.That(schedule.NextDueUnixSeconds, Is.EqualTo(1_900));
            Assert.That(schedule.IsDue(1_899), Is.False);
            Assert.That(schedule.IsDue(1_900), Is.True);
        }

        [Test]
        public void SyncSchedule_RefreshesFromLatestSuccessfulSync()
        {
            var initial = new LearningSyncSchedulePolicy()
                .AfterSuccessfulSync(completedAtUnixSeconds: 1_000, syncAfterSeconds: 900);
            var refreshed = initial.AfterSuccessfulSync(
                completedAtUnixSeconds: 1_500,
                syncAfterSeconds: 60);

            Assert.That(initial.NextDueUnixSeconds, Is.EqualTo(1_900));
            Assert.That(refreshed.NextDueUnixSeconds, Is.EqualTo(1_560));
            Assert.That(refreshed.IsDue(1_559), Is.False);
            Assert.That(refreshed.IsDue(1_560), Is.True);
        }

        [TestCase(59)]
        [TestCase(86401)]
        public void SyncSchedule_RejectsProviderDelayOutsideV1Bounds(int syncAfterSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LearningSyncSchedulePolicy().AfterSuccessfulSync(1_000, syncAfterSeconds));
        }

        [Test]
        public void RivalSelector_SuppressesLastSeenWhenAlternativesExist()
        {
            var source = new StubBoundedIntegerSource(0);
            var selector = new DeterministicRivalSelector(source);

            var selected = selector.Select(
                new[] { "rival-a", "rival-b", "rival-c" },
                lastSeenRivalId: "rival-a");

            Assert.That(selected, Is.EqualTo("rival-b"));
            Assert.That(source.LastExclusiveUpperBound, Is.EqualTo(2));
        }

        [Test]
        public void RivalSelector_ReturnsSingleCandidateWithoutRequestingRandomness()
        {
            var source = new StubBoundedIntegerSource(0);
            var selector = new DeterministicRivalSelector(source);

            var selected = selector.Select(new[] { "rival-only" }, "rival-only");

            Assert.That(selected, Is.EqualTo("rival-only"));
            Assert.That(source.CallCount, Is.Zero);
        }

        [Test]
        public void RivalSelector_RejectsEmptyInvalidOrDuplicateCandidates()
        {
            var selector = new DeterministicRivalSelector(new StubBoundedIntegerSource(0));

            Assert.Throws<ArgumentException>(() => selector.Select(Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => selector.Select(new[] { "rival-a", " " }));
            Assert.Throws<ArgumentException>(() => selector.Select(new[] { "rival-a", "rival-a" }));
        }

        private sealed class StubBoundedIntegerSource : IBoundedIntegerSource
        {
            private readonly int nextValue;

            public StubBoundedIntegerSource(int nextValue)
            {
                this.nextValue = nextValue;
            }

            public int CallCount { get; private set; }
            public int LastExclusiveUpperBound { get; private set; }

            public int Next(int exclusiveUpperBound)
            {
                CallCount++;
                LastExclusiveUpperBound = exclusiveUpperBound;
                return nextValue;
            }
        }
    }
}
