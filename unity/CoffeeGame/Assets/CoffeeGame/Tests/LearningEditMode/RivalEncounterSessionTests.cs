using System;
using NUnit.Framework;

namespace CoffeeGame.Domain.Tests
{
    public sealed class RivalEncounterSessionTests
    {
        [Test]
        public void SpeechFlow_RequiresEditingAndExplicitConfirmationBeforeSubmission()
        {
            var session = new RivalEncounterSession("rival-fox-001", "ch_test_001");

            session.StartListening();
            Assert.That(session.State, Is.EqualTo(RivalEncounterState.Listening));
            session.BeginTranscribing();
            session.AcceptTranscript("最初の文字起こし");

            Assert.That(session.State, Is.EqualTo(RivalEncounterState.Editing));
            Assert.That(session.FinalText, Is.EqualTo("最初の文字起こし"));

            session.UpdateFinalText("編集した最終回答");
            session.RequestConfirmation();
            session.ReturnToEditing();
            session.RequestConfirmation();
            var confirmed = session.ConfirmSubmission("ca_test_001");

            Assert.That(session.State, Is.EqualTo(RivalEncounterState.Submitting));
            Assert.That(confirmed.Text, Is.EqualTo("編集した最終回答"));
            Assert.That(confirmed.InputMode, Is.EqualTo(RivalAnswerInputMode.SpeechTranscript));

            session.ApplyPendingResult("rs_test_001");
            Assert.That(session.State, Is.EqualTo(RivalEncounterState.PendingNetwork));
            session.ApplyCompletedResult("rs_test_001", true);
            Assert.That(session.State, Is.EqualTo(RivalEncounterState.Result));
            Assert.That(session.IsCorrectResult, Is.True);
        }

        [Test]
        public void TypedFlow_CanDeferResumeCancelAndResetWithoutLosingDraftUnexpectedly()
        {
            var session = new RivalEncounterSession("rival-fox-001", "ch_test_001");

            session.StartTypedAnswer();
            session.UpdateFinalText("draft");
            session.Defer();
            session.ResumeDeferred();

            Assert.That(session.State, Is.EqualTo(RivalEncounterState.Editing));
            Assert.That(session.FinalText, Is.EqualTo("draft"));

            session.Cancel();
            Assert.That(session.State, Is.EqualTo(RivalEncounterState.Cancelled));
            session.Reset();
            Assert.That(session.State, Is.EqualTo(RivalEncounterState.Offered));
            Assert.That(session.FinalText, Is.Empty);
        }

        [Test]
        public void InvalidTransitions_AreRejectedDeterministically()
        {
            var session = new RivalEncounterSession("rival-fox-001", "ch_test_001");

            Assert.Throws<InvalidOperationException>(() => session.RequestConfirmation());
            Assert.Throws<InvalidOperationException>(() => session.ApplyPendingResult("rs_test_001"));

            session.StartListening();
            Assert.Throws<InvalidOperationException>(() => session.AcceptTranscript("too early"));
        }

        [Test]
        public void SafeCheckpointPolicy_NeverOffersDuringCombatOrOutsideCheckpoint()
        {
            var policy = new SafeCheckpointCadencePolicy(2);

            Assert.That(policy.IsEligible(new SafeCheckpointSnapshot(true, false, false, false, 2)), Is.True);
            Assert.That(policy.IsEligible(new SafeCheckpointSnapshot(true, true, false, false, 2)), Is.False);
            Assert.That(policy.IsEligible(new SafeCheckpointSnapshot(false, false, false, false, 2)), Is.False);
            Assert.That(policy.IsEligible(new SafeCheckpointSnapshot(true, false, false, false, 1)), Is.False);
            Assert.That(policy.IsEligible(new SafeCheckpointSnapshot(true, false, true, false, 2)), Is.False);
        }
    }
}
