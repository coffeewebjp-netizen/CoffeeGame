using System;
using System.Collections.Generic;
using System.Text;
using CoffeeGame.Domain;
using CoffeeGame.Input;
using CoffeeGame.Integration;
using CoffeeGame.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CoffeeGame.UI
{
    public sealed partial class CombatGameHudView : MonoBehaviour
    {

        public bool IsRivalAnswerInputFocused =>
            rivalAnswerInput != null
            && rivalAnswerInput.gameObject.activeInHierarchy
            && rivalAnswerInput.interactable
            && (rivalAnswerInput.isFocused
                || EventSystem.current?.currentSelectedGameObject == rivalAnswerInput.gameObject);


        public bool ReleaseRivalAnswerInputFocus()
        {
            if (!IsRivalAnswerInputFocused)
            {
                return false;
            }

            DeactivateRivalAnswerEditor();
            Button next = rivalPrimaryButton != null
                && rivalPrimaryButton.gameObject.activeInHierarchy
                && rivalPrimaryButton.interactable
                    ? rivalPrimaryButton
                    : rivalContinueButton;
            EventSystem.current?.SetSelectedGameObject(next?.gameObject);
            return true;
        }


        public void ReleaseRivalGameplayFocus()
        {
            DeactivateRivalAnswerEditor();
            EventSystem current = EventSystem.current;
            if (current != null)
            {
                current.SetSelectedGameObject(null);
            }
        }


        private void DeactivateRivalAnswerEditor()
        {
            if (rivalAnswerInput != null && rivalAnswerInput.isFocused)
            {
                rivalAnswerInput.DeactivateInputField();
            }

            Keyboard.current?.SetIMEEnabled(false);
        }


        public void RefreshRivalLearning(RivalLearningQuestionSession session)
        {
            if (session == null || rivalMessageText == null || rivalAnswerInput == null)
            {
                return;
            }

            RivalLearningQuestionState previousState = renderedRivalState;
            renderedRivalState = session.State;
            string difficulty = FormatRivalDifficulty(session.Difficulty);

            switch (session.State)
            {
                case RivalLearningQuestionState.Loading:
                    rivalMessageText.text = "スライムを5体倒したね。\n\n直近14日の苦手問題を読み込んでいます…";
                    rivalNoteText.text = "読み込み中でも「戦闘へ戻る」で中断できます。";
                    SetRivalButton(rivalPrimaryButton, "読み込み中…", false, false);
                    SetRivalButton(rivalSecondaryButton, string.Empty, false, false);
                    break;
                case RivalLearningQuestionState.Editing:
                    rivalMessageText.text = "問題\n\n" + session.PromptText;
                    rivalNoteText.text = string.IsNullOrEmpty(difficulty)
                        ? "回答を入力し、内容を確認してください。まだ送信されません。"
                        : difficulty + "　回答を入力し、内容を確認してください。";
                    SetRivalButton(rivalPrimaryButton, "回答を確認", true, session.HasDraft);
                    SetRivalButton(rivalSecondaryButton, string.Empty, false, false);
                    break;
                case RivalLearningQuestionState.Confirming:
                    rivalMessageText.text = "問題\n\n" + session.PromptText;
                    rivalNoteText.text = string.IsNullOrEmpty(session.ErrorCode)
                        ? "この回答で送信しますか？"
                        : "送信できませんでした（" + session.ErrorCode + "）。内容を保ったまま再送できます。";
                    SetRivalButton(rivalPrimaryButton, "この内容で送信", true, true);
                    SetRivalButton(rivalSecondaryButton, "編集に戻る", true, true);
                    break;
                case RivalLearningQuestionState.Submitting:
                    rivalMessageText.text = "問題\n\n" + session.PromptText;
                    rivalNoteText.text = "CoffeeLearningへ回答を送信しています…";
                    SetRivalButton(rivalPrimaryButton, "送信中…", true, false);
                    SetRivalButton(rivalSecondaryButton, string.Empty, false, false);
                    break;
                case RivalLearningQuestionState.Pending:
                    rivalMessageText.text = "問題\n\n" + session.PromptText;
                    rivalNoteText.text = string.IsNullOrEmpty(session.ErrorCode)
                        ? "AI判定を待っています。少し待ってから確認してください。"
                        : "判定を確認できませんでした（" + session.ErrorCode + "）。再確認できます。";
                    SetRivalButton(rivalPrimaryButton, "判定を確認", true, true);
                    SetRivalButton(rivalSecondaryButton, string.Empty, false, false);
                    break;
                case RivalLearningQuestionState.CheckingResult:
                    rivalMessageText.text = "問題\n\n" + session.PromptText;
                    rivalNoteText.text = "AI判定を確認しています…";
                    SetRivalButton(rivalPrimaryButton, "確認中…", true, false);
                    SetRivalButton(rivalSecondaryButton, string.Empty, false, false);
                    break;
                case RivalLearningQuestionState.Completed:
                    rivalMessageText.text = session.IsCorrect == true
                        ? "正解！\n\n" + session.PromptText
                        : "今回は不正解。\n\n" + session.PromptText;
                    rivalNoteText.text = FormatRivalCompletionResult(session);
                    SetRivalButton(rivalPrimaryButton, string.Empty, false, false);
                    SetRivalButton(rivalSecondaryButton, string.Empty, false, false);
                    break;
                case RivalLearningQuestionState.NoItems:
                    rivalMessageText.text = "直近14日には、出題できる苦手問題がありません。";
                    rivalNoteText.text = "戦闘へ戻って続けられます。";
                    SetRivalButton(rivalPrimaryButton, string.Empty, false, false);
                    SetRivalButton(rivalSecondaryButton, string.Empty, false, false);
                    break;
                case RivalLearningQuestionState.Error:
                    rivalMessageText.text = session.ErrorCode == "NOT_CONNECTED"
                        ? "CoffeeLearningに接続されていません。"
                        : "苦手問題を取得できませんでした。";
                    rivalNoteText.text = "エラー: " + session.ErrorCode + "　再試行するか、戦闘へ戻れます。";
                    SetRivalButton(rivalPrimaryButton, "再試行", true, true);
                    SetRivalButton(rivalSecondaryButton, string.Empty, false, false);
                    break;
                default:
                    rivalMessageText.text = "苦手問題を準備しています。";
                    rivalNoteText.text = "戦闘へ戻ることもできます。";
                    SetRivalButton(rivalPrimaryButton, string.Empty, false, false);
                    SetRivalButton(rivalSecondaryButton, string.Empty, false, false);
                    break;
            }

            ConfigureRivalResultLayout(session.State == RivalLearningQuestionState.Completed);

            bool showInput = session.State == RivalLearningQuestionState.Editing
                || session.State == RivalLearningQuestionState.Confirming
                || session.State == RivalLearningQuestionState.Submitting
                || session.State == RivalLearningQuestionState.Pending
                || session.State == RivalLearningQuestionState.CheckingResult;
            rivalAnswerInput.gameObject.SetActive(showInput);
            rivalAnswerInput.interactable = session.State == RivalLearningQuestionState.Editing;
            if (!string.Equals(rivalAnswerInput.text, session.DraftAnswer, StringComparison.Ordinal))
            {
                rivalAnswerInput.SetTextWithoutNotify(session.DraftAnswer);
            }

            SetRivalButton(
                rivalContinueButton,
                session.State == RivalLearningQuestionState.Completed ? "つづける" : "戦闘へ戻る",
                true,
                true);

            if (session.State == RivalLearningQuestionState.Editing
                && previousState != RivalLearningQuestionState.Editing
                && rivalAnswerInput.gameObject.activeInHierarchy)
            {
                EventSystem.current?.SetSelectedGameObject(rivalAnswerInput.gameObject);
                rivalAnswerInput.ActivateInputField();
            }
        }


        public static string FormatRivalCompletionNote(RivalLearningQuestionSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (session.IsCorrect != true)
            {
                return AppendJudgmentFeedback(
                    "苦手問題として記録されました。また挑戦できます。",
                    session.JudgmentFeedback);
            }

            if (!session.GameRewardApplication.HasValue)
            {
                string pendingNote = session.RewardEligible
                    ? "CoffeeLearningでOKになりました。報酬を反映しています…"
                    : "CoffeeLearningでOKになりました。ゲーム報酬は対象外です。";
                return AppendJudgmentFeedback(pendingNote, session.JudgmentFeedback);
            }

            PlayerLearningRewardApplication application = session.GameRewardApplication.Value;
            if (application.Status == LearningRewardApplyStatus.DuplicateGrant)
            {
                return AppendJudgmentFeedback(
                    $"CoffeeLearningでOKになりました。報酬は受取済みです。\n" +
                    $"親密度 {application.CurrentAffinity} / {application.RecruitmentThreshold}",
                    session.JudgmentFeedback);
            }

            if (application.Status != LearningRewardApplyStatus.Granted)
            {
                return AppendJudgmentFeedback(
                    "CoffeeLearningでOKになりました。ゲーム報酬は対象外です。",
                    session.JudgmentFeedback);
            }

            LearningRewardBundle reward = application.Reward;
            string note =
                $"報酬　Gold +{reward.Gold} / EXP +{reward.Experience} / 才能 +{reward.TalentPoints}\n" +
                $"親密度 +{reward.AffinityDelta}（{application.CurrentAffinity} / {application.RecruitmentThreshold}）";
            note = application.RivalRecruited
                ? note + "\n親密度が最大になり、仲間になりました！"
                : note;
            return AppendJudgmentFeedback(note, session.JudgmentFeedback);
        }


        private static string FormatRivalCompletionResult(RivalLearningQuestionSession session)
        {
            string heading = session.IsCorrect == true ? "正解！" : "今回は不正解。";
            string prompt = string.IsNullOrWhiteSpace(session.PromptText)
                ? string.Empty
                : "\n\n問題\n" + session.PromptText;
            return heading + prompt + "\n\n" + FormatRivalCompletionNote(session);
        }


        private static string AppendJudgmentFeedback(string note, string feedback)
        {
            return string.IsNullOrWhiteSpace(feedback)
                ? note
                : note + "\n\nAI判定: " + feedback;
        }


        private void ConfigureRivalResultLayout(bool showResult)
        {
            rivalMessageText.gameObject.SetActive(!showResult);
            rivalNoteText.alignment = showResult ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            rivalNoteText.resizeTextForBestFit = showResult;
            rivalNoteText.resizeTextMinSize = 11;
            rivalNoteText.resizeTextMaxSize = 19;
            rivalNoteText.verticalOverflow = VerticalWrapMode.Truncate;
            Anchor(
                rivalNoteText.rectTransform,
                new Vector2(0.535f, 0.245f),
                new Vector2(0.955f, showResult ? 0.68f : 0.335f));
        }


        private void BuildRivalOverlay(RectTransform parent)
        {
            Image dimmer = CreateImage(
                "Rival Encounter Overlay",
                parent,
                new Color(0.008f, 0.012f, 0.024f, 0.94f));
            Stretch(dimmer.rectTransform, 0f);
            rivalOverlay = dimmer.gameObject;

            Image panel = CreateImage("Rival Encounter Panel", dimmer.transform, Panel);
            Anchor(panel.rectTransform, new Vector2(0.055f, 0.065f), new Vector2(0.945f, 0.935f));

            Image portraitFrame = CreateImage(
                "Rival Portrait Frame",
                panel.transform,
                new Color(0.91f, 0.91f, 0.89f, 1f));
            Anchor(portraitFrame.rectTransform, new Vector2(0.025f, 0.055f), new Vector2(0.49f, 0.945f));

            RectTransform portraitRect = CreateRect(
                "Rival Portrait",
                portraitFrame.transform,
                new Vector2(0.02f, 0.02f),
                new Vector2(0.98f, 0.98f));
            RawImage portrait = portraitRect.gameObject.AddComponent<RawImage>();
            Texture2D texture = Resources.Load<Texture2D>(RivalPortraitResource);
            portrait.texture = texture;
            portrait.color = Color.white;
            portrait.raycastTarget = false;
            if (texture != null && texture.height > 0)
            {
                AspectRatioFitter fitter = portraitRect.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = (float)texture.width / texture.height;
            }

            Text encounter = CreateText(
                "Rival Encounter Heading",
                panel.transform,
                28,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                Accent);
            Anchor(encounter.rectTransform, new Vector2(0.535f, 0.82f), new Vector2(0.955f, 0.92f));
            encounter.text = "RIVAL ENCOUNTER";

            Text name = CreateText(
                "Rival Name",
                panel.transform,
                48,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                Ink);
            Anchor(name.rectTransform, new Vector2(0.535f, 0.69f), new Vector2(0.955f, 0.81f));
            name.text = "白銀のライバル";

            rivalMessageText = CreateText(
                "Rival Message",
                panel.transform,
                27,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                Ink);
            rivalMessageText.supportRichText = false;
            rivalMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            rivalMessageText.verticalOverflow = VerticalWrapMode.Truncate;
            Anchor(rivalMessageText.rectTransform, new Vector2(0.535f, 0.49f), new Vector2(0.955f, 0.68f));
            rivalMessageText.text = "スライムを5体倒したね。\n\n直近14日の苦手問題を読み込んでいます…";

            Image answerBackground = CreateImage(
                "Rival Answer Background",
                panel.transform,
                new Color(0.04f, 0.065f, 0.09f, 1f));
            Anchor(answerBackground.rectTransform, new Vector2(0.535f, 0.34f), new Vector2(0.955f, 0.47f));
            rivalAnswerInput = answerBackground.gameObject.AddComponent<InputField>();
            rivalAnswerInput.targetGraphic = answerBackground;
            rivalAnswerInput.characterLimit = 1000;
            // Submission is an explicit button action. A multiline editor keeps
            // IME conversion-confirm Enter inside the field instead of treating it
            // as an implicit UI submit/deactivation command.
            rivalAnswerInput.lineType = InputField.LineType.MultiLineNewline;
            rivalAnswerInput.contentType = InputField.ContentType.Standard;
            Text answerText = CreateText(
                "Rival Answer Text",
                answerBackground.transform,
                25,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                Ink);
            answerText.supportRichText = false;
            Anchor(answerText.rectTransform, new Vector2(0.035f, 0.06f), new Vector2(0.965f, 0.94f));
            Text answerPlaceholder = CreateText(
                "Rival Answer Placeholder",
                answerBackground.transform,
                23,
                FontStyle.Italic,
                TextAnchor.MiddleLeft,
                MutedInk);
            answerPlaceholder.text = "ここに回答を入力";
            Anchor(answerPlaceholder.rectTransform, new Vector2(0.035f, 0.06f), new Vector2(0.965f, 0.94f));
            rivalAnswerInput.textComponent = answerText;
            rivalAnswerInput.placeholder = answerPlaceholder;
            rivalAnswerInput.onValueChanged.AddListener(value => RivalAnswerChanged?.Invoke(value));

            rivalNoteText = CreateText(
                "Rival Note",
                panel.transform,
                19,
                FontStyle.Normal,
                TextAnchor.MiddleLeft,
                MutedInk);
            rivalNoteText.supportRichText = false;
            rivalNoteText.horizontalOverflow = HorizontalWrapMode.Wrap;
            Anchor(rivalNoteText.rectTransform, new Vector2(0.535f, 0.245f), new Vector2(0.955f, 0.335f));
            rivalNoteText.text = "回答は送信前に確認できます。";

            rivalPrimaryButton = CreateButton(
                "Rival Primary",
                panel.transform,
                "回答を確認",
                23,
                () => RivalPrimaryRequested?.Invoke());
            Anchor(rivalPrimaryButton.GetComponent<RectTransform>(), new Vector2(0.535f, 0.105f), new Vector2(0.68f, 0.22f));

            rivalSecondaryButton = CreateButton(
                "Rival Secondary",
                panel.transform,
                "編集に戻る",
                22,
                () => RivalSecondaryRequested?.Invoke());
            Anchor(rivalSecondaryButton.GetComponent<RectTransform>(), new Vector2(0.69f, 0.105f), new Vector2(0.81f, 0.22f));

            rivalContinueButton = CreateButton(
                "Rival Continue",
                panel.transform,
                "戦闘へ戻る",
                22,
                () => RivalContinueRequested?.Invoke());
            Anchor(rivalContinueButton.GetComponent<RectTransform>(), new Vector2(0.82f, 0.105f), new Vector2(0.955f, 0.22f));

            SetRivalButton(rivalPrimaryButton, "読み込み中…", false, false);
            SetRivalButton(rivalSecondaryButton, string.Empty, false, false);
            rivalAnswerInput.gameObject.SetActive(false);

            rivalOverlay.SetActive(false);
        }


        private static void SetRivalButton(Button button, string label, bool visible, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(visible);
            button.interactable = interactable;
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label ?? string.Empty;
            }
        }


        private static string FormatRivalDifficulty(CoffeeGameDifficultyDto difficulty)
        {
            if (difficulty == null || string.IsNullOrWhiteSpace(difficulty.band))
            {
                return string.Empty;
            }

            return $"難易度 {difficulty.band} / Lv.{difficulty.level}";
        }
    }
}
