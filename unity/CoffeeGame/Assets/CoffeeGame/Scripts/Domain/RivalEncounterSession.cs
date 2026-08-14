using System;

namespace CoffeeGame.Domain
{
    public enum RivalEncounterState
    {
        Offered,
        Drafting,
        Listening,
        Transcribing,
        Editing,
        Confirming,
        Submitting,
        PendingNetwork,
        Result,
        Deferred,
        Cancelled
    }

    public enum RivalAnswerInputMode
    {
        Typed,
        SpeechTranscript
    }

    public readonly struct ConfirmedRivalAnswer
    {
        public ConfirmedRivalAnswer(
            string challengeId,
            string clientAttemptId,
            string text,
            RivalAnswerInputMode inputMode)
        {
            ChallengeId = challengeId;
            ClientAttemptId = clientAttemptId;
            Text = text;
            InputMode = inputMode;
        }

        public string ChallengeId { get; }
        public string ClientAttemptId { get; }
        public string Text { get; }
        public RivalAnswerInputMode InputMode { get; }
    }

    /// <summary>
    /// Pure encounter workflow. Audio capture/transcription and transport are injected outside
    /// this type. Accepting a transcript only enters Editing and can never submit an answer.
    /// </summary>
    public sealed class RivalEncounterSession
    {
        public RivalEncounterSession(string rivalId, string challengeId)
        {
            RivalId = RequireId(rivalId, nameof(rivalId));
            ChallengeId = RequireId(challengeId, nameof(challengeId));
            State = RivalEncounterState.Offered;
            FinalText = string.Empty;
        }

        public string RivalId { get; }
        public string ChallengeId { get; }
        public RivalEncounterState State { get; private set; }
        public string FinalText { get; private set; }
        public RivalAnswerInputMode? InputMode { get; private set; }
        public string PendingResultId { get; private set; }
        public bool? IsCorrectResult { get; private set; }

        public void StartTypedAnswer()
        {
            RequireState(RivalEncounterState.Offered);
            InputMode = RivalAnswerInputMode.Typed;
            State = RivalEncounterState.Drafting;
        }

        public void StartListening()
        {
            RequireState(RivalEncounterState.Offered);
            InputMode = RivalAnswerInputMode.SpeechTranscript;
            State = RivalEncounterState.Listening;
        }

        public void BeginTranscribing()
        {
            RequireState(RivalEncounterState.Listening);
            State = RivalEncounterState.Transcribing;
        }

        public void AcceptTranscript(string transcript)
        {
            RequireState(RivalEncounterState.Transcribing);
            FinalText = transcript ?? string.Empty;
            State = RivalEncounterState.Editing;
        }

        public void UpdateFinalText(string text)
        {
            RequireAnyState(RivalEncounterState.Drafting, RivalEncounterState.Editing);
            FinalText = text ?? string.Empty;
        }

        public void RequestConfirmation()
        {
            RequireAnyState(RivalEncounterState.Drafting, RivalEncounterState.Editing);
            if (string.IsNullOrWhiteSpace(FinalText))
            {
                throw new InvalidOperationException("A non-empty final answer is required before confirmation.");
            }

            State = RivalEncounterState.Confirming;
        }

        public void ReturnToEditing()
        {
            RequireState(RivalEncounterState.Confirming);
            State = RivalEncounterState.Editing;
        }

        public ConfirmedRivalAnswer ConfirmSubmission(string clientAttemptId)
        {
            RequireState(RivalEncounterState.Confirming);
            var validatedAttemptId = RequireId(clientAttemptId, nameof(clientAttemptId));
            if (!InputMode.HasValue || string.IsNullOrWhiteSpace(FinalText))
            {
                throw new InvalidOperationException("The encounter has no confirmed answer to submit.");
            }

            State = RivalEncounterState.Submitting;
            return new ConfirmedRivalAnswer(
                ChallengeId,
                validatedAttemptId,
                FinalText,
                InputMode.Value);
        }

        public void ApplyPendingResult(string resultId)
        {
            RequireAnyState(RivalEncounterState.Submitting, RivalEncounterState.PendingNetwork);
            PendingResultId = RequireId(resultId, nameof(resultId));
            State = RivalEncounterState.PendingNetwork;
        }

        public void ApplyCompletedResult(string resultId, bool isCorrect)
        {
            RequireAnyState(RivalEncounterState.Submitting, RivalEncounterState.PendingNetwork);
            var validatedResultId = RequireId(resultId, nameof(resultId));
            if (!string.IsNullOrEmpty(PendingResultId)
                && !string.Equals(PendingResultId, validatedResultId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The completed result does not match the pending result.");
            }

            PendingResultId = validatedResultId;
            IsCorrectResult = isCorrect;
            State = RivalEncounterState.Result;
        }

        public void ReturnToEditingAfterSubmissionFailure()
        {
            RequireAnyState(RivalEncounterState.Submitting, RivalEncounterState.PendingNetwork);
            PendingResultId = null;
            State = RivalEncounterState.Editing;
        }

        public void Defer()
        {
            if (State == RivalEncounterState.Submitting
                || State == RivalEncounterState.Result
                || State == RivalEncounterState.Cancelled
                || State == RivalEncounterState.Deferred)
            {
                throw InvalidTransition("defer");
            }

            State = RivalEncounterState.Deferred;
        }

        public void ResumeDeferred()
        {
            RequireState(RivalEncounterState.Deferred);
            if (!string.IsNullOrEmpty(PendingResultId))
            {
                State = RivalEncounterState.PendingNetwork;
            }
            else if (!string.IsNullOrEmpty(FinalText))
            {
                State = RivalEncounterState.Editing;
            }
            else
            {
                State = RivalEncounterState.Offered;
            }
        }

        public void Cancel()
        {
            if (State == RivalEncounterState.Submitting
                || State == RivalEncounterState.PendingNetwork
                || State == RivalEncounterState.Result
                || State == RivalEncounterState.Cancelled)
            {
                throw InvalidTransition("cancel");
            }

            State = RivalEncounterState.Cancelled;
        }

        public void Reset()
        {
            if (State == RivalEncounterState.Submitting
                || State == RivalEncounterState.PendingNetwork
                || (State == RivalEncounterState.Deferred && !string.IsNullOrEmpty(PendingResultId)))
            {
                throw InvalidTransition("reset while a result may still be recoverable");
            }

            State = RivalEncounterState.Offered;
            FinalText = string.Empty;
            InputMode = null;
            PendingResultId = null;
            IsCorrectResult = null;
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A stable non-whitespace ID is required.", parameterName);
            }

            return value;
        }

        private void RequireState(RivalEncounterState expected)
        {
            if (State != expected)
            {
                throw InvalidTransition("enter " + expected);
            }
        }

        private void RequireAnyState(RivalEncounterState first, RivalEncounterState second)
        {
            if (State != first && State != second)
            {
                throw InvalidTransition("enter " + first + " or " + second);
            }
        }

        private InvalidOperationException InvalidTransition(string operation)
        {
            return new InvalidOperationException($"Cannot {operation} from rival encounter state {State}.");
        }
    }
}
