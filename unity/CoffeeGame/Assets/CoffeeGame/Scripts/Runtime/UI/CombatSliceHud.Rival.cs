using System;
using CoffeeGame.Actors;
using CoffeeGame.Domain;
using CoffeeGame.Input;
using CoffeeGame.Integration;
using CoffeeGame.Run;
using UnityEngine;

namespace CoffeeGame.UI
{
    public sealed partial class CombatSliceHud : MonoBehaviour
    {

        private void UpdateRivalEncounter()
        {
            if (!rivalEncounterActive)
            {
                HandleRunStateChanged();
            }

            Vector2 rivalNavigation = input.Navigate;
            bool rivalConfirmPressed = input.ConfirmPressed;
            bool rivalAnswerFocused = modernView.IsRivalAnswerInputFocused;
            bool rivalCancelPressed = ShouldRouteRivalCancel(
                input.CancelPressed,
                rivalAnswerFocused,
                input.LastUsedInputIsGamepad);
            bool gamepadNavigationIntent = input.LastUsedInputIsGamepad
                && (rivalConfirmPressed
                    || rivalCancelPressed
                    || rivalNavigation.sqrMagnitude >= 0.25f);
            bool releasedPointerFocus = gamepadNavigationIntent
                && modernView.ReleaseRivalAnswerInputFocus();
            if (releasedPointerFocus && !rivalConfirmPressed)
            {
                modernView.Refresh(run, false);
                modernView.RefreshRivalLearning(rivalLearningQuestion);
                return;
            }

            if (rivalCancelPressed)
            {
                if (rivalLearningQuestion.State == RivalLearningQuestionState.Confirming)
                {
                    rivalLearningQuestion.ReturnToEditing();
                }
                else
                {
                    HandleRivalContinue();
                }
            }
            else if (ShouldRouteRivalPrimary(
                rivalConfirmPressed,
                modernView.IsRivalAnswerInputFocused))
            {
                HandleRivalPrimary();
            }

            TryApplyCompletedRivalReward();
            modernView.Refresh(run, false);
            modernView.RefreshRivalLearning(rivalLearningQuestion);
        }

        public static bool ShouldRouteRivalPrimary(bool confirmPressed, bool answerInputFocused)
        {
            return confirmPressed && !answerInputFocused;
        }


        public static bool ShouldRouteRivalCancel(
            bool cancelPressed,
            bool answerInputFocused,
            bool lastInputWasGamepad)
        {
            return cancelPressed && (!answerInputFocused || lastInputWasGamepad);
        }


        private void HandleRunStateChanged()
        {
            if (run == null || rivalLearningQuestion == null)
            {
                return;
            }

            if (run.Mode == CombatRunMode.RivalEncounter)
            {
                if (rivalEncounterActive)
                {
                    return;
                }

                rivalEncounterActive = true;
                _ = rivalLearningQuestion.BeginNewEncounterAsync();
                return;
            }

            if (rivalEncounterActive)
            {
                rivalEncounterActive = false;
                rivalLearningQuestion.CancelPendingOperation();
            }
        }


        private void HandleRivalAnswerChanged(string answer)
        {
            rivalLearningQuestion?.UpdateDraft(answer);
        }


        private async void HandleRivalPrimary()
        {
            if (!rivalEncounterActive || rivalLearningQuestion == null)
            {
                return;
            }

            switch (rivalLearningQuestion.State)
            {
                case RivalLearningQuestionState.Editing:
                    rivalLearningQuestion.RequestConfirmation();
                    break;
                case RivalLearningQuestionState.Confirming:
                    await rivalLearningQuestion.SubmitConfirmedAnswerAsync();
                    break;
                case RivalLearningQuestionState.Pending:
                    await rivalLearningQuestion.RecoverPendingResultAsync();
                    break;
                case RivalLearningQuestionState.Error:
                    await rivalLearningQuestion.RetryPreparationAsync();
                    break;
            }
        }


        private void HandleRivalSecondary()
        {
            rivalLearningQuestion?.ReturnToEditing();
        }


        private void TryApplyCompletedRivalReward()
        {
            if (run?.Progression == null
                || rivalLearningQuestion == null
                || rivalLearningQuestion.State != RivalLearningQuestionState.Completed
                || !rivalLearningQuestion.AuthoritativeOutcome.HasValue
                || rivalLearningQuestion.GameRewardApplication.HasValue)
            {
                return;
            }

            PlayerLearningRewardApplication application = run.Progression.TryApplyLearningOutcome(
                rivalLearningQuestion.AuthoritativeOutcome.Value,
                string.IsNullOrWhiteSpace(run.CurrentRivalId)
                    ? RivalCharacterIds.WeaknessChallenger
                    : run.CurrentRivalId);
            rivalLearningQuestion.RecordGameRewardApplication(application);
        }


        private void HandleRivalContinue()
        {
            if (!rivalEncounterActive || run == null)
            {
                return;
            }

            rivalEncounterActive = false;
            rivalLearningQuestion?.CancelPendingOperation();
            modernView?.ReleaseRivalGameplayFocus();
            run.ContinueAfterRivalEncounter();
        }
    }
}
