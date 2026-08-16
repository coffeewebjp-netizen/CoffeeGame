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
    public enum CharacterMenuTab
    {
        Status,
        Inventory,
        System,
        Companions
    }

    /// <summary>
    /// Runtime-built uGUI view for the combat HUD and pause menu. The view owns
    /// layout/pointer interaction only; CombatSliceHud remains the input/state controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatGameHudView : MonoBehaviour
    {
        private const string PortraitResource = "Art/UI/Hero/hero_portrait_ui";
        private const string FullBodyResource = "Art/UI/Hero/hero_fullbody_ui";
        private const string RivalPortraitResource = "Art/UI/Rivals/rival_weakness_challenger_v1";

        private static readonly Color Ink = new Color(0.94f, 0.96f, 1f, 1f);
        private static readonly Color MutedInk = new Color(0.69f, 0.75f, 0.82f, 1f);
        private static readonly Color Panel = new Color(0.025f, 0.043f, 0.066f, 0.93f);
        private static readonly Color PanelLight = new Color(0.075f, 0.105f, 0.14f, 0.97f);
        private static readonly Color Accent = new Color(0.99f, 0.66f, 0.24f, 1f);
        private static readonly Color Selected = new Color(0.18f, 0.38f, 0.48f, 1f);

        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<Button> controlButtons = new List<Button>();

        private Font font;
        private Canvas canvas;
        private GameObject gameplayHud;
        private GameObject pauseOverlay;
        private GameObject runOverlay;
        private GameObject rivalOverlay;
        private GameObject chargePanel;
        private Button pauseButton;
        private Text identityText;
        private Text objectiveText;
        private Text performanceText;
        private Text runHeadingText;
        private Text runMessageText;
        private Text chargeText;
        private Image healthFill;
        private Image staminaFill;
        private Image magicFill;
        private Image experienceFill;
        private Image chargeFill;
        private Text healthText;
        private Text staminaText;
        private Text magicText;
        private Text experienceText;
        private RectTransform menuContentHost;
        private ScrollRect menuScrollRect;
        private Scrollbar menuScrollbar;
        private RectTransform menuScrollContent;
        private Image fullBodyImage;
        private Text menuHeadingText;
        private Text menuFooterText;
        private Text controlsStatusText;
        private Text coffeeLearningStatusText;
        private Text rivalMessageText;
        private Text rivalNoteText;
        private InputField rivalAnswerInput;
        private Button rivalPrimaryButton;
        private Button rivalSecondaryButton;
        private Button rivalContinueButton;
        private RivalLearningQuestionState renderedRivalState = RivalLearningQuestionState.Idle;
        private CharacterMenuTab selectedTab;
        private int selectedControlRow;
        private GameInputReader input;
        private CoffeeLearningConnectionPresenter coffeeLearningConnection;
        private string systemNotice = string.Empty;
        private float smoothedFrameSeconds = 1f / 60f;
        private float frameStatsClock;

        public event Action PauseRequested;
        public event Action ResumeRequested;
        public event Action StartRequested;
        public event Action InputSettingsRequested;
        public event Action<CharacterMenuTab> TabRequested;
        public event Action<GameInputSemantic> RebindRequested;
        public event Action InputModeSelectionRequested;
        public event Action PerformancePresetRequested;
        public event Action FrameStatsToggleRequested;
        public event Action SaveRequested;
        public event Action ExportProfileRequested;
        public event Action ImportProfileRequested;
        public event Action CloudDriveRequested;
        public event Action CloudFolderRequested;
        public event Action CloudLocalRequested;
        public event Action ResetBindingsRequested;
        public event Action CancelRebindRequested;
        public event Action CoffeeLearningPrimaryRequested;
        public event Action CoffeeLearningDisconnectRequested;
        public event Action CoffeeLearningCancelRequested;
        public event Action<string> RivalAnswerChanged;
        public event Action RivalPrimaryRequested;
        public event Action RivalSecondaryRequested;
        public event Action RivalContinueRequested;

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

        public void Initialize(
            GameInputReader inputReader,
            CoffeeLearningConnectionPresenter learningConnection = null)
        {
            input = inputReader;
            coffeeLearningConnection = learningConnection;
            font = CreateJapaneseFont();
            EnsureEventSystem();
            BuildCanvas();
            SetSelectedTab(CharacterMenuTab.Status);
        }

        public void Refresh(CombatRunController run, bool pauseMenuOpen)
        {
            if (run == null || canvas == null)
            {
                return;
            }

            bool choosingInput = run.Mode == CombatRunMode.InputModeSelection;
            canvas.enabled = !choosingInput;
            if (choosingInput)
            {
                return;
            }

            bool gameplayVisible = run.Mode != CombatRunMode.InputModeSelection;
            gameplayHud.SetActive(gameplayVisible && !pauseMenuOpen);
            pauseOverlay.SetActive(pauseMenuOpen);
            rivalOverlay.SetActive(!pauseMenuOpen && run.Mode == CombatRunMode.RivalEncounter);
            RefreshFrameStats(gameplayVisible && !pauseMenuOpen);

            bool showRunOverlay = !pauseMenuOpen &&
                                  (run.Mode == CombatRunMode.Ready ||
                                   run.Mode == CombatRunMode.GameOver);
            runOverlay.SetActive(showRunOverlay);
            pauseButton.gameObject.SetActive(run.Mode == CombatRunMode.Playing);

            PlayerProgression progression = run.Progression;
            identityText.text = $"Lv.{progression.Level}  {progression.Status.ClassName}\n討伐 {run.Kills}    Gold {progression.Gold}";
            objectiveText.text = run.LastEvent;
            SetBar(healthFill, healthText, run.PlayerHealth.Normalized,
                $"HP  {run.PlayerHealth.Current} / {run.PlayerHealth.Maximum}");
            SetBar(staminaFill, staminaText,
                SafeRatio(run.PlayerResources.Stamina, run.PlayerResources.MaxStamina),
                $"ST  {Mathf.FloorToInt(run.PlayerResources.Stamina)} / {Mathf.FloorToInt(run.PlayerResources.MaxStamina)}");
            SetBar(magicFill, magicText,
                SafeRatio(run.PlayerResources.MagicPoints, run.PlayerResources.MaxMagicPoints),
                $"MP  {run.PlayerResources.MagicPoints:0.0} / {run.PlayerResources.MaxMagicPoints:0}");
            SetBar(experienceFill, experienceText,
                SafeRatio(progression.Experience, progression.ExperienceRequiredForNextLevel),
                $"EXP  {progression.Experience} / {progression.ExperienceRequiredForNextLevel}");

            bool charging = !pauseMenuOpen && run.PlayerCombat.IsCharging;
            chargePanel.SetActive(charging);
            if (charging)
            {
                chargeFill.fillAmount = Mathf.Clamp01(run.PlayerCombat.ChargeNormalized);
                chargeText.text = $"{run.PlayerCombat.ChargeLabel}  CHARGE";
            }

            if (showRunOverlay)
            {
                runHeadingText.text = run.Mode == CombatRunMode.Ready
                    ? "COFFEEGAME"
                    : "GAME OVER";
                runMessageText.text = run.LastEvent;
            }

            if (pauseMenuOpen && selectedTab == CharacterMenuTab.System)
            {
                RefreshControls(run.Mode == CombatRunMode.InputRebinding || input.IsRebinding);
            }
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

        public void SetSelectedTab(CharacterMenuTab tab)
        {
            selectedTab = tab;
            for (int index = 0; index < tabButtons.Count; index++)
            {
                SetButtonColor(tabButtons[index], index == (int)tab ? Selected : PanelLight);
            }

            bool showIllustration = tab == CharacterMenuTab.Status;
            fullBodyImage.gameObject.SetActive(showIllustration);
            menuContentHost.anchorMin = new Vector2(showIllustration ? 0.36f : 0.035f, 0.105f);
            menuContentHost.offsetMin = Vector2.zero;
            menuContentHost.offsetMax = Vector2.zero;
        }

        public void RebuildMenuContent(CombatRunController run)
        {
            if (run == null || menuScrollContent == null)
            {
                return;
            }

            ClearChildren(menuScrollContent);
            controlsStatusText = null;
            coffeeLearningStatusText = null;
            controlButtons.Clear();
            switch (selectedTab)
            {
                case CharacterMenuTab.Status:
                    BuildStatusContent(run.Progression);
                    break;
                case CharacterMenuTab.Inventory:
                    BuildTextContent(
                        "持ち物",
                        $"<size=29><b>通貨・成長</b></size>\nGold　{run.Progression.Gold}\n才能ポイント　{run.Progression.TalentPoints}\n\n" +
                        $"<size=29><b>素材</b></size>\nスライムゼリー　× {run.Progression.SlimeJelly}\n\n" +
                        "<size=29><b>消耗品</b></size>\nまだ持っていません。\n\n" +
                        "<size=29><b>装備品</b></size>\n装備システムの追加後、ここに表示されます。",
                        420f);
                    break;
                case CharacterMenuTab.System:
                    BuildControlsContent();
                    RefreshControls(run.Mode == CombatRunMode.InputRebinding || input.IsRebinding);
                    break;
                case CharacterMenuTab.Companions:
                {
                    int affinity = run.Progression.GetRivalAffinity(RivalCharacterIds.WeaknessChallenger);
                    bool recruited = run.Progression.IsRivalRecruited(RivalCharacterIds.WeaknessChallenger);
                    BuildTextContent(
                        "仲間",
                        recruited
                            ? $"<size=29><b>銀髪のライバル</b></size>\n仲間になりました。\n親密度　{affinity} / {LearningRewardPolicyV1.RecruitmentThreshold}"
                            : $"<size=29><b>銀髪のライバル</b></size>\n親密度　{affinity} / {LearningRewardPolicyV1.RecruitmentThreshold}\n\n正解を重ねて親密度が100になると仲間になります。",
                        420f);
                    break;
                }
            }
            FinalizeMenuLayout();
        }

        public void SetSystemNotice(string notice)
        {
            systemNotice = notice ?? string.Empty;
            if (selectedTab == CharacterMenuTab.System && controlsStatusText != null && input != null)
            {
                RefreshControls(input.IsRebinding);
            }
        }

        public void SetSelectedControlRow(int row)
        {
            int clampedRow = Mathf.Clamp(row, 0, Mathf.Max(0, controlButtons.Count - 1));
            bool changed = clampedRow != selectedControlRow;
            selectedControlRow = clampedRow;
            for (int index = 0; index < controlButtons.Count; index++)
            {
                SetButtonColor(controlButtons[index], index == selectedControlRow ? Selected : PanelLight);
            }
            if (changed)
            {
                ScrollToSelectedControl();
            }
        }

        public void ScrollMenu(float verticalInput, float unscaledDeltaTime)
        {
            if (menuScrollRect == null || menuScrollRect.content == null || menuScrollRect.viewport == null)
            {
                return;
            }

            const float deadZone = 0.2f;
            float absoluteInput = Mathf.Abs(verticalInput);
            if (absoluteInput <= deadZone || unscaledDeltaTime <= 0f)
            {
                return;
            }

            float overflow = menuScrollRect.content.rect.height - menuScrollRect.viewport.rect.height;
            if (overflow <= 0.5f)
            {
                menuScrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            float inputStrength = Mathf.InverseLerp(deadZone, 1f, Mathf.Min(1f, absoluteInput));
            float normalizedDelta = Mathf.Sign(verticalInput) * 620f * inputStrength * unscaledDeltaTime / overflow;
            menuScrollRect.verticalNormalizedPosition =
                Mathf.Clamp01(menuScrollRect.verticalNormalizedPosition + normalizedDelta);
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("CoffeeGAME UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform safeArea = CreateRect("Safe Area", canvasObject.transform, Vector2.zero, Vector2.one);
            safeArea.gameObject.AddComponent<SafeAreaRectTransform>();
            BuildGameplayHud(safeArea);
            BuildRunOverlay(safeArea);
            BuildRivalOverlay(safeArea);
            BuildPauseMenu(safeArea);
        }

        private void BuildGameplayHud(RectTransform parent)
        {
            gameplayHud = CreateRect("Gameplay HUD", parent, Vector2.zero, Vector2.one).gameObject;

            Image playerPanel = CreateImage("Player Status", gameplayHud.transform, Panel);
            SetTopLeft(playerPanel.rectTransform, new Vector2(28f, -28f), new Vector2(560f, 196f));

            Image portrait = CreateImage("Hero Portrait", playerPanel.transform, Color.white);
            SetTopLeft(portrait.rectTransform, new Vector2(14f, -14f), new Vector2(154f, 154f));
            portrait.sprite = Resources.Load<Sprite>(PortraitResource);
            portrait.preserveAspect = true;

            identityText = CreateText("Identity", playerPanel.transform, 26, FontStyle.Bold, TextAnchor.UpperLeft, Ink);
            SetTopLeft(identityText.rectTransform, new Vector2(180f, -14f), new Vector2(360f, 63f));

            CreateBar(playerPanel.transform, "Health", new Vector2(180f, -78f), new Color(0.88f, 0.2f, 0.3f), out healthFill, out healthText);
            CreateBar(playerPanel.transform, "Stamina", new Vector2(180f, -107f), new Color(0.96f, 0.66f, 0.18f), out staminaFill, out staminaText);
            CreateBar(playerPanel.transform, "Magic", new Vector2(180f, -136f), new Color(0.18f, 0.62f, 0.96f), out magicFill, out magicText);
            CreateBar(playerPanel.transform, "Experience", new Vector2(180f, -165f), new Color(0.3f, 0.8f, 0.46f), out experienceFill, out experienceText);

            objectiveText = CreateText("Objective", gameplayHud.transform, 27, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            SetTopCenter(objectiveText.rectTransform, new Vector2(0f, -34f), new Vector2(680f, 58f));

            pauseButton = CreateButton("Pause", gameplayHud.transform, "Ⅱ  ポーズ", 25, () => PauseRequested?.Invoke());
            SetTopRight(pauseButton.GetComponent<RectTransform>(), new Vector2(-28f, -28f), new Vector2(174f, 58f));

            performanceText = CreateText(
                "Frame Stats",
                gameplayHud.transform,
                19,
                FontStyle.Normal,
                TextAnchor.UpperRight,
                MutedInk);
            SetTopRight(performanceText.rectTransform, new Vector2(-30f, -94f), new Vector2(250f, 32f));
            performanceText.text = "FPS --  |  -- ms";

            Image charge = CreateImage("Charge", gameplayHud.transform, Panel);
            SetBottomCenter(charge.rectTransform, new Vector2(0f, 54f), new Vector2(520f, 56f));
            chargePanel = charge.gameObject;
            chargeFill = CreateImage("Fill", charge.transform, new Color(0.38f, 0.82f, 1f, 0.9f));
            Stretch(chargeFill.rectTransform, 5f);
            chargeFill.type = Image.Type.Filled;
            chargeFill.fillMethod = Image.FillMethod.Horizontal;
            chargeFill.fillOrigin = 0;
            chargeText = CreateText("Label", charge.transform, 24, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            Stretch(chargeText.rectTransform, 2f);
            chargePanel.SetActive(false);
        }

        private void BuildRunOverlay(RectTransform parent)
        {
            Image dimmer = CreateImage("Run Overlay", parent, new Color(0.01f, 0.018f, 0.026f, 0.78f));
            Stretch(dimmer.rectTransform, 0f);
            runOverlay = dimmer.gameObject;

            Image panel = CreateImage("Prompt", dimmer.transform, Panel);
            SetCenter(panel.rectTransform, Vector2.zero, new Vector2(660f, 340f));
            runHeadingText = CreateText("Heading", panel.transform, 48, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            Anchor(runHeadingText.rectTransform, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.94f));
            runMessageText = CreateText("Message", panel.transform, 27, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            Anchor(runMessageText.rectTransform, new Vector2(0.06f, 0.49f), new Vector2(0.94f, 0.72f));
            Button startButton = CreateButton("Start", panel.transform, "開始／再挑戦", 28, () => StartRequested?.Invoke());
            Anchor(startButton.GetComponent<RectTransform>(), new Vector2(0.2f, 0.27f), new Vector2(0.8f, 0.44f));
            Button inputSettingsButton = CreateButton(
                "Pre-Battle Input Settings",
                panel.transform,
                "コントローラー設定",
                24,
                () => InputSettingsRequested?.Invoke());
            Anchor(inputSettingsButton.GetComponent<RectTransform>(), new Vector2(0.2f, 0.07f), new Vector2(0.8f, 0.22f));
            runOverlay.SetActive(false);
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

        private void BuildPauseMenu(RectTransform parent)
        {
            Image dimmer = CreateImage("Pause Overlay", parent, new Color(0.008f, 0.014f, 0.024f, 0.9f));
            Stretch(dimmer.rectTransform, 0f);
            pauseOverlay = dimmer.gameObject;

            Image panel = CreateImage("Pause Panel", dimmer.transform, Panel);
            Anchor(panel.rectTransform, new Vector2(0.035f, 0.045f), new Vector2(0.965f, 0.955f));

            menuHeadingText = CreateText("Pause Heading", panel.transform, 34, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            Anchor(menuHeadingText.rectTransform, new Vector2(0.025f, 0.91f), new Vector2(0.52f, 0.985f));
            menuHeadingText.text = "PAUSE  —  キャラクター";

            Button resume = CreateButton("Resume", panel.transform, "戦闘へ戻る", 24, () => ResumeRequested?.Invoke());
            Anchor(resume.GetComponent<RectTransform>(), new Vector2(0.80f, 0.925f), new Vector2(0.975f, 0.98f));

            string[] labels = { "ステータス", "持ち物", "システム", "仲間" };
            for (int index = 0; index < labels.Length; index++)
            {
                int captured = index;
                Button tab = CreateButton($"Tab {labels[index]}", panel.transform, labels[index], 25,
                    () => TabRequested?.Invoke((CharacterMenuTab)captured));
                tabButtons.Add(tab);
                float left = 0.025f + index * 0.195f;
                Anchor(tab.GetComponent<RectTransform>(), new Vector2(left, 0.835f), new Vector2(left + 0.18f, 0.905f));
            }

            fullBodyImage = CreateImage("Hero Full Body", panel.transform, Color.white);
            Anchor(fullBodyImage.rectTransform, new Vector2(0.035f, 0.11f), new Vector2(0.335f, 0.825f));
            fullBodyImage.sprite = Resources.Load<Sprite>(FullBodyResource);
            fullBodyImage.preserveAspect = true;

            menuContentHost = CreateRect("Menu Content Host", panel.transform, new Vector2(0.36f, 0.105f), new Vector2(0.975f, 0.825f));
            BuildMenuScrollArea(menuContentHost);
            menuFooterText = CreateText("Footer", panel.transform, 20, FontStyle.Normal, TextAnchor.MiddleLeft, MutedInk);
            Anchor(menuFooterText.rectTransform, new Vector2(0.035f, 0.02f), new Vector2(0.78f, 0.09f));
            menuFooterText.text = "←→: タブ　↑↓: 選択　決定: 実行　Start / Esc / 取消: 戻る";

            pauseOverlay.SetActive(false);
        }

        private void BuildStatusContent(PlayerProgression progression)
        {
            var builder = new StringBuilder();
            builder.Append("<size=36><b>ステータス</b></size>\n\n");
            builder.Append($"<color=#B5C4D7>クラス</color>　{progression.Status.ClassName}\n");
            builder.Append($"<color=#B5C4D7>才能</color>　　{progression.Status.Talent}\n");
            builder.Append($"<color=#B5C4D7>レベル</color>　Lv.{progression.Level}\n");
            builder.Append($"<color=#B5C4D7>経験値</color>　{progression.Experience} / {progression.ExperienceRequiredForNextLevel}\n");
            builder.Append($"<color=#B5C4D7>お金</color>　　{progression.Gold} Gold\n\n");
            builder.Append("<size=29><b>能力</b></size>\n");
            foreach (PlayerAttributeValue attribute in progression.Status.Attributes.CreateSnapshot())
            {
                PlayerAttributeDefinition definition = PlayerAttributeCatalog.Find(attribute.Id);
                string label = definition != null ? definition.DisplayName : attribute.Id;
                string effect = definition != null ? $"　<color=#AEBECB>{definition.EffectDescription}</color>" : string.Empty;
                builder.Append($"<b>{label,-7}</b> {attribute.Value}{effect}\n");
            }

            PlayerDerivedStats derived = PlayerDerivedStatCalculator.Calculate(progression.Status);
            builder.Append("\n<size=29><b>現在の補正</b></size>\n");
            builder.Append($"攻撃 ×{derived.AttackMultiplier:0.00}　移動 ×{derived.MovementSpeedMultiplier:0.00}\n");
            builder.Append($"クリティカル {derived.CriticalChance * 100f:0.0}%　回避 {derived.EvasionChance * 100f:0.0}%\n");
            builder.Append($"必殺技速度 ×{derived.SpecialChargeSpeedMultiplier:0.00}　防御 ×{derived.IncomingDamageMultiplier:0.00}");
            BuildTextContent(null, builder.ToString(), 850f);
        }

        private void BuildTextContent(string heading, string body, float preferredHeight)
        {
            string text = string.IsNullOrEmpty(heading)
                ? body
                : $"<size=36><b>{heading}</b></size>\n\n{body}";
            Text label = CreateText("Content", menuScrollContent, 27, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
            label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = text;
            LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            layout.flexibleWidth = 1f;
        }

        private void BuildControlsContent()
        {
            AddSectionHeading(menuScrollContent, "システム", 36, Ink, 58f);
            AddSectionHeading(menuScrollContent, "コントローラー設定", 27, Accent, 42f);
            controlsStatusText = CreateText("Controls Status", menuScrollContent, 21, FontStyle.Normal, TextAnchor.UpperLeft, MutedInk);
            controlsStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            controlsStatusText.verticalOverflow = VerticalWrapMode.Overflow;
            LayoutElement statusLayout = controlsStatusText.gameObject.AddComponent<LayoutElement>();
            statusLayout.preferredHeight = 148f;
            statusLayout.flexibleWidth = 1f;

            AddControlButton(menuScrollContent, GameInputSemantic.Jump, "ジャンプ");
            AddControlButton(menuScrollContent, GameInputSemantic.Sword, "刀攻撃");
            AddControlButton(menuScrollContent, GameInputSemantic.Special, "居合斬り");
            AddControlButton(menuScrollContent, GameInputSemantic.Magic, "氷魔法");
            AddCommandButton(menuScrollContent, "入力方式を選び直す", () => InputModeSelectionRequested?.Invoke());
            AddSectionHeading(menuScrollContent, "セーブ", 27, Accent, 42f);
            AddCommandButton(menuScrollContent, "セーブする", () => SaveRequested?.Invoke());
            AddCommandButton(menuScrollContent, "セーブを書き出す", () => ExportProfileRequested?.Invoke());
            AddCommandButton(menuScrollContent, "セーブを取り込む", () => ImportProfileRequested?.Invoke());
            AddCommandButton(menuScrollContent, "Google Driveを使う", () => CloudDriveRequested?.Invoke());
            AddCommandButton(menuScrollContent, "セーブ先を指定", () => CloudFolderRequested?.Invoke());
            AddCommandButton(menuScrollContent, "ローカル保存に戻す", () => CloudLocalRequested?.Invoke());
            AddCommandButton(menuScrollContent, "初期配置へ戻す", () => ResetBindingsRequested?.Invoke());
            AddCommandButton(menuScrollContent, "戦闘へ戻る", () => ResumeRequested?.Invoke());

            Button cancel = CreateButton("Cancel Rebind", menuScrollContent, "再割当を取り消す", 23, () => CancelRebindRequested?.Invoke());
            LayoutElement cancelLayout = cancel.gameObject.AddComponent<LayoutElement>();
            cancelLayout.preferredHeight = 52f;
            cancelLayout.flexibleWidth = 1f;

            AddSectionHeading(menuScrollContent, "描画設定", 27, Accent, 42f);
            AddCommandButton(menuScrollContent, "描画プリセット", () => PerformancePresetRequested?.Invoke());
            AddCommandButton(menuScrollContent, "FPS表示", () => FrameStatsToggleRequested?.Invoke());

            AddSectionHeading(menuScrollContent, "CoffeeLearning", 27, Accent, 42f);
            coffeeLearningStatusText = CreateText(
                "CoffeeLearning Status",
                menuScrollContent,
                22,
                FontStyle.Normal,
                TextAnchor.MiddleLeft,
                MutedInk);
            LayoutElement learningStatusLayout = coffeeLearningStatusText.gameObject.AddComponent<LayoutElement>();
            learningStatusLayout.preferredHeight = 48f;
            learningStatusLayout.flexibleWidth = 1f;
            AddCommandButton(menuScrollContent, "CoffeeLearning Primary", () => CoffeeLearningPrimaryRequested?.Invoke());
            AddCommandButton(menuScrollContent, "CoffeeLearning Disconnect", () => CoffeeLearningDisconnectRequested?.Invoke());
            AddCommandButton(menuScrollContent, "CoffeeLearning Cancel", () => CoffeeLearningCancelRequested?.Invoke());
        }

        private void AddControlButton(RectTransform content, GameInputSemantic semantic, string label)
        {
            Button button = CreateButton(label, content, label, 23, () => RebindRequested?.Invoke(semantic));
            button.gameObject.name = semantic.ToString();
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 58f;
            layout.flexibleWidth = 1f;
            controlButtons.Add(button);
        }

        private void AddSectionHeading(
            RectTransform content,
            string label,
            int fontSize,
            Color color,
            float preferredHeight)
        {
            Text heading = CreateText($"{label} Heading", content, fontSize, FontStyle.Bold, TextAnchor.MiddleLeft, color);
            heading.text = label;
            LayoutElement layout = heading.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            layout.flexibleWidth = 1f;
        }

        private void AddCommandButton(RectTransform content, string label, Action command)
        {
            Button button = CreateButton(label, content, label, 23, command);
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 58f;
            layout.flexibleWidth = 1f;
            controlButtons.Add(button);
        }

        private const int InputModeButton = 4;
        private const int SaveButton = 5;
        private const int ExportButton = 6;
        private const int ImportButton = 7;
        private const int CloudDriveButton = 8;
        private const int CloudFolderButton = 9;
        private const int CloudLocalButton = 10;
        private const int ResetBindingsButton = 11;
        private const int ResumeButton = 12;
        private const int PerformanceButton = 13;
        private const int FrameStatsButton = 14;
        private const int CoffeeLearningPrimaryButton = 15;
        private const int CoffeeLearningDisconnectButton = 16;
        private const int CoffeeLearningCancelButton = 17;
        private const int SystemCommandButtonCount = 18;

        private void RefreshControls(bool rebinding)
        {
            if (controlsStatusText == null || controlButtons.Count < SystemCommandButtonCount)
            {
                return;
            }

            controlsStatusText.text = rebinding
                ? input.IsWaitingForRebindButtonRelease
                    ? "決定に使ったボタンを離してください。"
                    : $"新しく使うボタンを押してください（残り {Mathf.CeilToInt(input.RebindSecondsRemaining)} 秒）。Start / View / Esc で取り消せます。"
                : $"{input.ActiveControllerProfileName}\n{CoffeeGame.Persistence.CloudSaveSettings.StatusLabel}\n{input.LastRebindMessage}\n{input.ControllerCompatibilityHint}" +
                  (string.IsNullOrWhiteSpace(systemNotice) ? string.Empty : $"\n<color=#FCA83D>{systemNotice}</color>");

            GameInputSemantic[] semantics =
            {
                GameInputSemantic.Jump,
                GameInputSemantic.Sword,
                GameInputSemantic.Special,
                GameInputSemantic.Magic
            };
            string[] labels = { "ジャンプ", "刀攻撃", "居合斬り", "氷魔法" };
            bool supportsRebind = input.SelectedInputMode == InputMode.ControllerGamepad ||
                                  input.SelectedInputMode == InputMode.SteamDesktopCompatibility;
            for (int index = 0; index < semantics.Length; index++)
            {
                controlButtons[index].GetComponentInChildren<Text>().text =
                    $"{labels[index]}　　{input.GetActiveControllerBindingDescription(semantics[index])}";
                controlButtons[index].interactable = supportsRebind && !rebinding;
            }
            controlButtons[InputModeButton].GetComponentInChildren<Text>().text =
                $"入力方式を選び直す　（現在: {input.ActiveControllerProfileName}）";
            controlButtons[InputModeButton].interactable = !rebinding;
            controlButtons[SaveButton].interactable = !rebinding;
            controlButtons[ExportButton].interactable = !rebinding;
            controlButtons[ImportButton].interactable = !rebinding;
            controlButtons[CloudDriveButton].interactable = !rebinding;
            controlButtons[CloudFolderButton].interactable = !rebinding;
            controlButtons[CloudLocalButton].interactable = !rebinding;
            controlButtons[ResetBindingsButton].interactable = supportsRebind && !rebinding;
            controlButtons[ResumeButton].interactable = !rebinding;
            controlButtons[PerformanceButton].GetComponentInChildren<Text>().text =
                $"描画プリセット　{GamePerformanceSettings.CurrentPresetLabel}";
            controlButtons[FrameStatsButton].GetComponentInChildren<Text>().text =
                $"FPS表示　{(GamePerformanceSettings.ShowFrameStats ? "ON" : "OFF")}";
            controlButtons[PerformanceButton].interactable = !rebinding;
            controlButtons[FrameStatsButton].interactable = !rebinding;
            RefreshCoffeeLearningControls(rebinding);
            SetSelectedControlRow(selectedControlRow);

            Transform cancelTransform = controlsStatusText.transform.parent.Find("Cancel Rebind");
            if (cancelTransform != null)
            {
                cancelTransform.gameObject.SetActive(rebinding);
            }
        }

        private void RefreshCoffeeLearningControls(bool rebinding)
        {
            if (coffeeLearningStatusText == null || controlButtons.Count < SystemCommandButtonCount)
            {
                return;
            }

            if (coffeeLearningConnection == null)
            {
                coffeeLearningStatusText.text = "CoffeeLearning: \u672a\u63a5\u7d9a";
                controlButtons[CoffeeLearningPrimaryButton].GetComponentInChildren<Text>().text = "CoffeeLearning\u3068\u63a5\u7d9a";
                controlButtons[CoffeeLearningPrimaryButton].interactable = false;
                controlButtons[CoffeeLearningDisconnectButton].GetComponentInChildren<Text>().text = "CoffeeLearning\u63a5\u7d9a\u3092\u89e3\u9664";
                controlButtons[CoffeeLearningDisconnectButton].interactable = false;
                controlButtons[CoffeeLearningCancelButton].GetComponentInChildren<Text>().text = "CoffeeLearning\u64cd\u4f5c\u3092\u30ad\u30e3\u30f3\u30bb\u30eb";
                controlButtons[CoffeeLearningCancelButton].interactable = false;
                return;
            }

            coffeeLearningStatusText.text = "CoffeeLearning: " + coffeeLearningConnection.StatusLabel;
            controlButtons[CoffeeLearningPrimaryButton].GetComponentInChildren<Text>().text = coffeeLearningConnection.PrimaryActionLabel;
            controlButtons[CoffeeLearningDisconnectButton].GetComponentInChildren<Text>().text = coffeeLearningConnection.DisconnectActionLabel;
            controlButtons[CoffeeLearningCancelButton].GetComponentInChildren<Text>().text = coffeeLearningConnection.CancelActionLabel;
            controlButtons[CoffeeLearningPrimaryButton].interactable = !rebinding && coffeeLearningConnection.CanUsePrimaryAction;
            controlButtons[CoffeeLearningDisconnectButton].interactable = !rebinding && coffeeLearningConnection.CanUseDisconnectAction;
            controlButtons[CoffeeLearningCancelButton].interactable = !rebinding && coffeeLearningConnection.CanUseCancelAction;
        }

        private void BuildMenuScrollArea(RectTransform host)
        {
            Image viewportImage = CreateImage("Viewport", host, new Color(0.035f, 0.055f, 0.078f, 0.62f));
            Anchor(viewportImage.rectTransform, Vector2.zero, new Vector2(0.955f, 1f));
            viewportImage.gameObject.AddComponent<RectMask2D>();

            menuScrollContent = CreateRect("Content", viewportImage.transform, new Vector2(0f, 1f), new Vector2(1f, 1f));
            menuScrollContent.pivot = new Vector2(0.5f, 1f);
            menuScrollContent.anchoredPosition = Vector2.zero;
            menuScrollContent.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = menuScrollContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(26, 26, 24, 24);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = menuScrollContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image scrollbarTrack = CreateImage(
                "Vertical Scrollbar",
                host,
                new Color(0.12f, 0.17f, 0.22f, 0.9f));
            Anchor(scrollbarTrack.rectTransform, new Vector2(0.97f, 0f), Vector2.one);
            RectTransform slidingArea = CreateRect(
                "Sliding Area",
                scrollbarTrack.transform,
                Vector2.zero,
                Vector2.one);
            Stretch(slidingArea, 4f);
            Image handle = CreateImage("Handle", slidingArea, Accent);
            Anchor(handle.rectTransform, Vector2.zero, new Vector2(1f, 0.22f));
            menuScrollbar = scrollbarTrack.gameObject.AddComponent<Scrollbar>();
            menuScrollbar.handleRect = handle.rectTransform;
            menuScrollbar.targetGraphic = handle;
            menuScrollbar.direction = Scrollbar.Direction.BottomToTop;
            menuScrollbar.numberOfSteps = 0;

            menuScrollRect = host.gameObject.AddComponent<ScrollRect>();
            menuScrollRect.viewport = viewportImage.rectTransform;
            menuScrollRect.content = menuScrollContent;
            menuScrollRect.horizontal = false;
            menuScrollRect.vertical = true;
            menuScrollRect.scrollSensitivity = 42f;
            menuScrollRect.movementType = ScrollRect.MovementType.Clamped;
            menuScrollRect.verticalScrollbar = menuScrollbar;
            menuScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            menuScrollRect.verticalScrollbarSpacing = 8f;
            menuScrollRect.verticalNormalizedPosition = 1f;
        }

        private void FinalizeMenuLayout()
        {
            if (menuScrollContent == null || menuScrollRect == null)
            {
                return;
            }
            bool restoreHiddenState = pauseOverlay != null && !pauseOverlay.activeSelf;
            if (restoreHiddenState)
            {
                pauseOverlay.SetActive(true);
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuScrollContent);
            Canvas.ForceUpdateCanvases();
            menuScrollRect.verticalNormalizedPosition = 1f;
            if (selectedTab == CharacterMenuTab.System)
            {
                ScrollToSelectedControl();
            }
            if (restoreHiddenState)
            {
                pauseOverlay.SetActive(false);
            }
        }

        private void ScrollToSelectedControl()
        {
            if (menuScrollRect == null || controlButtons.Count == 0)
            {
                return;
            }
            float normalized = controlButtons.Count == 1
                ? 1f
                : 1f - selectedControlRow / (float)(controlButtons.Count - 1);
            menuScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);
        }

        private void RefreshFrameStats(bool gameplayVisible)
        {
            if (performanceText == null)
            {
                return;
            }

            bool visible = gameplayVisible && GamePerformanceSettings.ShowFrameStats;
            performanceText.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            float delta = Time.unscaledDeltaTime;
            if (delta <= 0f || delta > 1f)
            {
                return;
            }

            float blend = 1f - Mathf.Exp(-delta * 5f);
            smoothedFrameSeconds = Mathf.Lerp(smoothedFrameSeconds, delta, blend);
            frameStatsClock += delta;
            if (frameStatsClock < 0.25f)
            {
                return;
            }

            frameStatsClock = 0f;
            float milliseconds = smoothedFrameSeconds * 1000f;
            float fps = smoothedFrameSeconds > 0.0001f ? 1f / smoothedFrameSeconds : 0f;
            performanceText.text = $"FPS {fps:0}  |  {milliseconds:0.0} ms";
        }

        private void CreateBar(
            Transform parent,
            string name,
            Vector2 topLeft,
            Color color,
            out Image fill,
            out Text label)
        {
            Image background = CreateImage(name, parent, new Color(0.01f, 0.015f, 0.025f, 0.94f));
            SetTopLeft(background.rectTransform, topLeft, new Vector2(352f, 23f));
            fill = CreateImage("Fill", background.transform, color);
            Stretch(fill.rectTransform, 3f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            label = CreateText("Label", background.transform, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            Stretch(label.rectTransform, 0f);
        }

        private Button CreateButton(string name, Transform parent, string label, int fontSize, Action command)
        {
            Image background = CreateImage(name, parent, PanelLight);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.72f, 0.84f, 0.9f, 1f);
            colors.disabledColor = new Color(0.42f, 0.44f, 0.47f, 0.72f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            if (command != null)
            {
                button.onClick.AddListener(() => command());
            }
            Text text = CreateText("Label", button.transform, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            text.text = label;
            Stretch(text.rectTransform, 10f);
            return button;
        }

        private Image CreateImage(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.one);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private Text CreateText(
            string name,
            Transform parent,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Color color)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.one);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Font CreateJapaneseFont()
        {
            string[] candidates = { "Yu Gothic UI", "Yu Gothic", "Meiryo", "Noto Sans CJK JP", "Arial" };
            try
            {
                return Font.CreateDynamicFontFromOSFont(candidates, 28);
            }
            catch
            {
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }
            eventSystem.sendNavigationEvents = false;
            if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
                InputSystemUIInputModule module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
        }

        private static void SetBar(Image fill, Text text, float normalized, string label)
        {
            fill.fillAmount = Mathf.Clamp01(normalized);
            text.text = label;
        }

        private static float SafeRatio(float value, float maximum)
        {
            return maximum <= 0f ? 0f : value / maximum;
        }

        private static void SetButtonColor(Button button, Color color)
        {
            if (button != null && button.targetGraphic is Image image)
            {
                image.color = color;
            }
        }

        private static void ClearChildren(RectTransform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                if (Application.isPlaying)
                {
                    child.SetActive(false);
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetTopRight(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetTopCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetBottomCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }

    [DisallowMultipleComponent]
    public sealed class SafeAreaRectTransform : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            Apply();
        }

        private void LateUpdate()
        {
            if (lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
            {
                Apply();
            }
        }

        private void Apply()
        {
            Rect safe = Screen.safeArea;
            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);
            rectTransform.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
            rectTransform.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            lastSafeArea = safe;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
