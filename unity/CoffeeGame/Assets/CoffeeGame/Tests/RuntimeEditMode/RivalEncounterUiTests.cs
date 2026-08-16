using System.Linq;
using System.Threading.Tasks;
using CoffeeGame.Domain;
using CoffeeGame.Integration;
using CoffeeGame.Run;
using CoffeeGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class RivalEncounterUiTests
    {
        [TestCase(4, 5, false)]
        [TestCase(5, 5, true)]
        [TestCase(10, 5, true)]
        [TestCase(0, 5, false)]
        [TestCase(5, 0, false)]
        public void RivalMilestoneUsesConfiguredKillInterval(int kills, int interval, bool expected)
        {
            Assert.That(
                CombatRunController.IsRivalEncounterMilestone(kills, interval),
                Is.EqualTo(expected));
        }

        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        [TestCase(false, true, false)]
        [TestCase(false, false, false)]
        public void FocusedAnswerEditorConsumesConfirmBeforeRivalAction(
            bool confirmPressed,
            bool answerFocused,
            bool expected)
        {
            Assert.That(
                CombatSliceHud.ShouldRouteRivalPrimary(confirmPressed, answerFocused),
                Is.EqualTo(expected));
        }

        [TestCase(true, true, false, false)]
        [TestCase(true, true, true, true)]
        [TestCase(true, false, false, true)]
        [TestCase(false, false, true, false)]
        public void FocusedAnswerEditorKeepsKeyboardCancelInsideTextEntry(
            bool cancelPressed,
            bool answerFocused,
            bool lastInputWasGamepad,
            bool expected)
        {
            Assert.That(
                CombatSliceHud.ShouldRouteRivalCancel(
                    cancelPressed,
                    answerFocused,
                    lastInputWasGamepad),
                Is.EqualTo(expected));
        }

        [Test]
        public void RivalOverlayUsesApprovedPortraitAndRoutesContinue()
        {
            EventSystem originalEventSystem = EventSystem.current;
            var root = new GameObject("Rival Encounter UI Test");
            try
            {
                CombatGameHudView view = root.AddComponent<CombatGameHudView>();
                view.Initialize(null);

                RawImage portrait = root.GetComponentsInChildren<RawImage>(true)
                    .Single(image => image.name == "Rival Portrait");
                Assert.That(portrait.texture, Is.Not.Null);
                Assert.That(portrait.texture.name, Is.EqualTo("rival_weakness_challenger_v1"));

                bool continued = false;
                view.RivalContinueRequested += () => continued = true;
                Button button = root.GetComponentsInChildren<Button>(true)
                    .Single(candidate => candidate.name == "Rival Continue");
                button.onClick.Invoke();
                Assert.That(continued, Is.True);

                Text message = root.GetComponentsInChildren<Text>(true)
                    .Single(text => text.name == "Rival Message");
                Assert.That(message.text, Does.Contain("5体"));
                Assert.That(message.text, Does.Contain("苦手問題"));

                InputField answer = root.GetComponentsInChildren<InputField>(true)
                    .Single(candidate => candidate.name == "Rival Answer Background");
                Assert.That(answer.characterLimit, Is.EqualTo(1000));
                Assert.That(answer.lineType, Is.EqualTo(InputField.LineType.MultiLineNewline),
                    "Enter must stay inside the editor; submission uses the explicit button.");
                Assert.That(answer.gameObject.activeSelf, Is.False,
                    "The answer field stays hidden until a real challenge has loaded.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (originalEventSystem == null && EventSystem.current != null)
                {
                    Object.DestroyImmediate(EventSystem.current.gameObject);
                }
            }
        }

        [Test]
        public async Task LoadedQuestionShowsEditableAnswerAndExplicitConfirmationAction()
        {
            EventSystem originalEventSystem = EventSystem.current;
            var root = new GameObject("Rival Question UI Test");
            using (var session = new RivalLearningQuestionSession(
                () => new MockLearningBridge(),
                () => "ui-test",
                () => 0))
            {
                try
                {
                    CombatGameHudView view = root.AddComponent<CombatGameHudView>();
                    view.Initialize(null);
                    await session.BeginNewEncounterAsync();
                    view.RefreshRivalLearning(session);

                    Text message = root.GetComponentsInChildren<Text>(true)
                        .Single(text => text.name == "Rival Message");
                    Assert.That(message.text, Does.Contain("resilient"));

                    InputField answer = root.GetComponentsInChildren<InputField>(true)
                        .Single(candidate => candidate.name == "Rival Answer Background");
                    Assert.That(answer.gameObject.activeSelf, Is.True);
                    Assert.That(answer.interactable, Is.True);
                    EventSystem.current.SetSelectedGameObject(answer.gameObject);
                    answer.ActivateInputField();
                    Assert.That(view.IsRivalAnswerInputFocused, Is.True);

                    Assert.That(view.ReleaseRivalAnswerInputFocus(), Is.True);
                    Assert.That(view.IsRivalAnswerInputFocused, Is.False,
                        "The first native Gamepad command after pointer editing must restore controller focus.");
                    Assert.That(EventSystem.current.currentSelectedGameObject?.name, Is.EqualTo("Rival Primary"));
                    Assert.That(view.ReleaseRivalAnswerInputFocus(), Is.False);

                    EventSystem.current.SetSelectedGameObject(answer.gameObject);
                    answer.ActivateInputField();
                    view.ReleaseRivalGameplayFocus();
                    Assert.That(view.IsRivalAnswerInputFocused, Is.False);
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);

                    EventSystem.current.SetSelectedGameObject(answer.gameObject);
                    answer.ActivateInputField();

                    string changed = null;
                    view.RivalAnswerChanged += value => changed = value;
                    answer.text = "編集できる回答";
                    Assert.That(changed, Is.EqualTo("編集できる回答"));

                    session.UpdateDraft(changed);
                    view.RefreshRivalLearning(session);
                    Button primary = root.GetComponentsInChildren<Button>(true)
                        .Single(candidate => candidate.name == "Rival Primary");
                    Assert.That(primary.gameObject.activeSelf, Is.True);
                    Assert.That(primary.interactable, Is.True);
                    Assert.That(primary.GetComponentInChildren<Text>().text, Is.EqualTo("回答を確認"));

                    bool requested = false;
                    view.RivalPrimaryRequested += () => requested = true;
                    primary.onClick.Invoke();
                    Assert.That(requested, Is.True);
                    Assert.That(session.State, Is.EqualTo(RivalLearningQuestionState.Editing),
                        "The view only routes the command; it cannot submit by itself.");
                }
                finally
                {
                    Object.DestroyImmediate(root);
                    if (originalEventSystem == null && EventSystem.current != null)
                    {
                        Object.DestroyImmediate(EventSystem.current.gameObject);
                    }
                }
            }
        }

        [Test]
        public async Task CompletedCorrectQuestionShowsAppliedGameRewardAndAffinity()
        {
            using (var session = new RivalLearningQuestionSession(
                () => new MockLearningBridge(),
                () => "reward-ui",
                () => 0))
            {
                await session.BeginNewEncounterAsync();
                session.UpdateDraft("しなやかで回復力がある");
                Assert.That(session.RequestConfirmation(), Is.True);
                await session.SubmitConfirmedAnswerAsync();
                await session.RecoverPendingResultAsync();

                var progression = new PlayerProgression();
                PlayerLearningRewardApplication application = progression.TryApplyLearningOutcome(
                    session.AuthoritativeOutcome.Value,
                    "rival-silver-001");
                Assert.That(session.RecordGameRewardApplication(application), Is.True);

                string note = CombatGameHudView.FormatRivalCompletionNote(session);
                Assert.That(note, Does.Contain("Gold +6"));
                Assert.That(note, Does.Contain("EXP +9"));
                Assert.That(note, Does.Contain("才能 +2"));
                Assert.That(note, Does.Contain("親密度 +6"));
                Assert.That(note, Does.Contain("6 / 100"));
                Assert.That(note, Does.Contain("AI判定:"));
                Assert.That(note, Does.Contain(session.JudgmentFeedback));
            }
        }

        [Test]
        public async Task CompletedIncorrectQuestionShowsFullAiFeedbackInExpandedResultArea()
        {
            EventSystem originalEventSystem = EventSystem.current;
            var root = new GameObject("Rival Incorrect Result UI Test");
            using (var session = new RivalLearningQuestionSession(
                () => new MockLearningBridge(),
                () => "incorrect-result-ui",
                () => 0))
            {
                try
                {
                    await session.BeginNewEncounterAsync();
                    session.UpdateDraft("問題の意味と一致しない回答");
                    Assert.That(session.RequestConfirmation(), Is.True);
                    await session.SubmitConfirmedAnswerAsync();
                    await session.RecoverPendingResultAsync();
                    Assert.That(session.IsCorrect, Is.False);
                    Assert.That(session.JudgmentFeedback, Is.Not.Empty);

                    CombatGameHudView view = root.AddComponent<CombatGameHudView>();
                    view.Initialize(null);
                    view.RefreshRivalLearning(session);

                    Text message = root.GetComponentsInChildren<Text>(true)
                        .Single(text => text.name == "Rival Message");
                    Text note = root.GetComponentsInChildren<Text>(true)
                        .Single(text => text.name == "Rival Note");
                    InputField answer = root.GetComponentsInChildren<InputField>(true)
                        .Single(candidate => candidate.name == "Rival Answer Background");

                    Assert.That(message.gameObject.activeSelf, Is.False,
                        "The result view gives the former prompt area to the AI explanation.");
                    Assert.That(answer.gameObject.activeSelf, Is.False,
                        "The completed answer editor must not consume result-reading space.");
                    Assert.That(note.text, Does.Contain("今回は不正解。"));
                    Assert.That(note.text, Does.Contain(session.PromptText));
                    Assert.That(note.text, Does.Contain("AI判定:"));
                    Assert.That(note.text, Does.Contain(session.JudgmentFeedback));
                    Assert.That(note.alignment, Is.EqualTo(TextAnchor.UpperLeft));
                    Assert.That(note.resizeTextForBestFit, Is.True);
                    Assert.That(note.rectTransform.anchorMax.y, Is.EqualTo(0.68f).Within(0.001f));
                }
                finally
                {
                    Object.DestroyImmediate(root);
                    if (originalEventSystem == null && EventSystem.current != null)
                    {
                        Object.DestroyImmediate(EventSystem.current.gameObject);
                    }
                }
            }
        }
    }
}
