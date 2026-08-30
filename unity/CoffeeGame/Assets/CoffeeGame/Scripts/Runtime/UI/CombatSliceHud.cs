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

        private enum PauseMenuTab
        {
            Status,
            Inventory,
            System,
            Companions
        }


        private const string HeroStatusSpriteResource = "Art/UI/Hero/hero_fullbody_ui";

        private static readonly InputMode[] SelectableInputModes =
        {
            InputMode.KeyboardMouse,
            InputMode.ControllerGamepad,
            InputMode.SteamDesktopCompatibility,
            InputMode.TouchOnScreen
        };

        private static readonly string[] PauseMenuTabLabels =
        {
            "ステータス",
            "持ち物",
            "システム",
            "仲間"
        };


        private CombatRunController run;

        private GameInputReader input;

        private GUIStyle titleStyle;

        private GUIStyle labelStyle;

        private GUIStyle smallStyle;

        private GUIStyle centeredStyle;

        private GUIStyle buttonStyle;

        private GUIStyle selectedButtonStyle;

        private GUIStyle pauseTitleStyle;

        private GUIStyle pauseSubtitleStyle;

        private GUIStyle pauseTabStyle;

        private GUIStyle selectedPauseTabStyle;

        private GUIStyle statusNameStyle;

        private GUIStyle statusValueStyle;

        private GUIStyle mutedCenteredStyle;

        private Sprite heroStatusSprite;

        private bool showInputSettings;

        private bool pauseMenuOpen;

        private PauseMenuTab selectedPauseMenuTab;

        private int pauseTabNavigationLatch;

        private int selectedSettingsRow;

        private int navigationLatch;

        private int selectedInputModeRow;

        private int inputModeNavigationLatch;

        private string inputModeSelectionMessage = string.Empty;

        private CombatGameHudView modernView;

        private OnScreenTouchControls touchControls;

        private int pauseOpenedFrame = -1;

        private Func<string> saveProfileCommand;

        private Func<string> exportProfileCommand;

        private Func<string> importProfileCommand;

        private Func<string> cloudDriveCommand;

        private Func<string> cloudFolderCommand;

        private Func<string> cloudLocalCommand;

        private CoffeeLearningConnectionPresenter coffeeLearningConnection;

        private RivalLearningQuestionSession rivalLearningQuestion;

        private bool rivalEncounterActive;


        public void Initialize(
            CombatRunController runController,
            GameInputReader inputReader,
            Func<string> manualSaveCommand = null,
            CoffeeLearningConnectionPresenter learningConnection = null,
            Func<string> exportProfile = null,
            Func<string> importProfile = null,
            Func<string> cloudDrive = null,
            Func<string> cloudFolder = null,
            Func<string> cloudLocal = null)
        {
            run = runController;
            input = inputReader;
            saveProfileCommand = manualSaveCommand;
            exportProfileCommand = exportProfile;
            importProfileCommand = importProfile;
            cloudDriveCommand = cloudDrive;
            cloudFolderCommand = cloudFolder;
            cloudLocalCommand = cloudLocal;
            coffeeLearningConnection = learningConnection;
            rivalLearningQuestion = new RivalLearningQuestionSession(
                () => coffeeLearningConnection?.LearningBridge ?? new NullLearningBridge());
            GamePerformanceSettings.ApplySavedPreset();
            selectedInputModeRow = GetInputModeRow(input.PreferredInputModeForSelection);
            heroStatusSprite = Resources.Load<Sprite>(HeroStatusSpriteResource);
            modernView = gameObject.AddComponent<CombatGameHudView>();
            modernView.Initialize(input, coffeeLearningConnection);
            touchControls = gameObject.AddComponent<OnScreenTouchControls>();
            touchControls.Initialize(input);
            modernView.PauseRequested += HandlePointerPause;
            modernView.ResumeRequested += ResumeFromPauseMenu;
            modernView.StartRequested += run.StartNewRun;
            modernView.InputSettingsRequested += OpenPreBattleInputSettings;
            modernView.TabRequested += HandlePointerTab;
            modernView.RebindRequested += BeginRebind;
            modernView.InputModeSelectionRequested += BeginInputModeSelectionFromPause;
            modernView.PerformancePresetRequested += HandlePerformancePreset;
            modernView.FrameStatsToggleRequested += HandleFrameStatsToggle;
            modernView.SaveRequested += HandleManualSave;
            modernView.ExportProfileRequested += HandleExportProfile;
            modernView.ImportProfileRequested += HandleImportProfile;
            modernView.CloudDriveRequested += HandleCloudDrive;
            modernView.CloudFolderRequested += HandleCloudFolder;
            modernView.CloudLocalRequested += HandleCloudLocal;
            modernView.ResetBindingsRequested += HandleResetBindings;
            modernView.CancelRebindRequested += input.CancelInteractiveRebind;
            modernView.CoffeeLearningPrimaryRequested += HandleCoffeeLearningPrimary;
            modernView.PasteConnectionRequested += HandlePasteConnection;
            modernView.CoffeeLearningDisconnectRequested += HandleCoffeeLearningDisconnect;
            modernView.CoffeeLearningCancelRequested += HandleCoffeeLearningCancel;
            modernView.RivalAnswerChanged += HandleRivalAnswerChanged;
            modernView.RivalPrimaryRequested += HandleRivalPrimary;
            modernView.RivalSecondaryRequested += HandleRivalSecondary;
            modernView.RivalContinueRequested += HandleRivalContinue;
            run.StateChanged += HandleRunStateChanged;
            run.Progression.Changed += HandleProgressionChanged;
            HandleRunStateChanged();
        }


        private void Update()
        {
            if (run == null || input == null)
            {
                return;
            }

            RefreshTouchOverlay();
            _ = ConsumePendingCoffeeLearningBearer();

            if (modernView != null)
            {
                UpdateModernHud();
                return;
            }

            if (run.Mode == CombatRunMode.InputModeSelection)
            {
                HandleInputModeSelection();
                return;
            }

            if (run.Mode == CombatRunMode.Paused)
            {
                pauseMenuOpen = true;
            }
            else if (run.Mode == CombatRunMode.Playing && pauseMenuOpen)
            {
                pauseMenuOpen = false;
                pauseTabNavigationLatch = 0;
            }

            if (input.IsRebinding)
            {
                return;
            }

            if (input.SettingsPressed)
            {
                if (run.Mode == CombatRunMode.Playing)
                {
                    run.Pause();
                    pauseMenuOpen = true;
                    SelectPauseMenuTab(PauseMenuTab.System);
                    return;
                }

                if (run.Mode == CombatRunMode.Paused)
                {
                    pauseMenuOpen = true;
                    SelectPauseMenuTab(PauseMenuTab.System);
                    return;
                }

                ToggleInputSettings();
                return;
            }

            if (IsPauseMenuVisible)
            {
                HandlePauseMenuNavigation();
                if (selectedPauseMenuTab == PauseMenuTab.System)
                {
                    HandleSettingsNavigation();
                    if (input.ConfirmPressed)
                    {
                        ActivateSelectedSettingsRow();
                    }
                }
                return;
            }

            if (!showInputSettings)
            {
                return;
            }

            HandleSettingsNavigation();
            if (input.CancelPressed)
            {
                CloseInputSettings();
                return;
            }

            if (input.ConfirmPressed)
            {
                ActivateSelectedSettingsRow();
            }
        }


        private void OnGUI()
        {
            if (run == null || input == null)
            {
                return;
            }

            if (modernView != null && run.Mode != CombatRunMode.InputModeSelection)
            {
                DrawPinnedSystemActions();
                return;
            }

            EnsureStyles();
            if (run.Mode == CombatRunMode.Paused)
            {
                pauseMenuOpen = true;
            }
            if (run.Mode == CombatRunMode.InputModeSelection)
            {
                DrawInputModeSelection();
                return;
            }

            if (IsPauseMenuVisible)
            {
                DrawPauseMenu();
                return;
            }

            if (showInputSettings)
            {
                DrawInputSettings();
                return;
            }

            DrawPlayerHud();
            DrawRunOverlay();
        }


        private bool IsPauseMenuVisible =>
            pauseMenuOpen &&
            (run.Mode == CombatRunMode.Paused
                || run.Mode == CombatRunMode.InputRebinding
                || run.Mode == CombatRunMode.InputSettings);


        private void UpdateModernHud()
        {
            if (run.Mode == CombatRunMode.InputModeSelection)
            {
                HandleInputModeSelection();
                modernView.Refresh(run, pauseMenuOpen && run.Mode == CombatRunMode.Paused);
                return;
            }

            if (run.Mode == CombatRunMode.RivalEncounter)
            {
                UpdateRivalEncounter();
                return;
            }

            bool pauseCapableMode = run.Mode == CombatRunMode.Paused
                || run.Mode == CombatRunMode.InputRebinding
                || run.Mode == CombatRunMode.InputSettings;
            if (pauseCapableMode && !pauseMenuOpen)
            {
                OpenPauseMenu(run.Mode == CombatRunMode.InputSettings
                    ? CharacterMenuTab.System
                    : CharacterMenuTab.Status);
            }
            else if (!pauseCapableMode && pauseMenuOpen)
            {
                pauseMenuOpen = false;
                pauseTabNavigationLatch = 0;
                navigationLatch = 0;
            }

            if (!input.IsRebinding)
            {
                if (run.Mode == CombatRunMode.Playing && input.SettingsPressed)
                {
                    run.Pause();
                    OpenPauseMenu(CharacterMenuTab.System);
                }
                else if (run.Mode == CombatRunMode.Paused || run.Mode == CombatRunMode.InputSettings)
                {
                    if (input.SettingsPressed)
                    {
                        SelectModernTab(CharacterMenuTab.System);
                    }
                    else if (Time.frameCount > pauseOpenedFrame && (input.PausePressed || input.CancelPressed))
                    {
                        ResumeFromPauseMenu();
                    }
                    else
                    {
                        HandleModernMenuNavigation();
                    }
                }
            }

            modernView.Refresh(run, pauseMenuOpen && pauseCapableMode);
        }


        private void RefreshTouchOverlay()
        {
            if (touchControls == null || input == null || run == null)
            {
                return;
            }

            bool showPad = input.UsesTouchOverlay
                && run.Mode == CombatRunMode.Playing
                && !pauseMenuOpen;
            touchControls.SetVisible(showPad);
        }


        private void OpenPauseMenu(CharacterMenuTab tab)
        {
            pauseMenuOpen = true;
            pauseOpenedFrame = Time.frameCount;
            pauseTabNavigationLatch = 0;
            navigationLatch = 0;
            SelectModernTab(tab);
        }


        private void SelectModernTab(CharacterMenuTab tab)
        {
            if ((CharacterMenuTab)selectedPauseMenuTab == CharacterMenuTab.System
                && tab != CharacterMenuTab.System)
            {
                coffeeLearningConnection?.CancelPendingOrActiveAction();
            }
            selectedPauseMenuTab = (PauseMenuTab)tab;
            navigationLatch = 0;
            if (tab == CharacterMenuTab.System)
            {
                selectedSettingsRow = SupportsButtonRebind ? 0 : CombatHudSettingsRows.InputMode;
                if (coffeeLearningConnection != null
                    && coffeeLearningConnection.ShouldRefreshAccountIdentity)
                {
                    _ = coffeeLearningConnection.RefreshAccountIdentityAsync();
                }
            }
            modernView.SetSelectedTab(tab);
            modernView.RebuildMenuContent(run);
            modernView.SetSelectedControlRow(selectedSettingsRow);
        }


        private void HandlePointerPause()
        {
            if (run.Mode != CombatRunMode.Playing)
            {
                return;
            }
            run.Pause();
            OpenPauseMenu(CharacterMenuTab.Status);
        }


        private void HandlePointerTab(CharacterMenuTab tab)
        {
            if ((run.Mode == CombatRunMode.Paused || run.Mode == CombatRunMode.InputSettings)
                && !input.IsRebinding)
            {
                SelectModernTab(tab);
            }
        }


        private void ResumeFromPauseMenu()
        {
            coffeeLearningConnection?.CancelPendingOrActiveAction();
            if (input.IsRebinding)
            {
                input.CancelInteractiveRebind();
                return;
            }
            if (run.Mode == CombatRunMode.Paused)
            {
                run.Resume();
            }
            else if (run.Mode == CombatRunMode.InputSettings)
            {
                run.EndInputSettings();
            }
            pauseMenuOpen = false;
        }


        private void OpenPreBattleInputSettings()
        {
            if ((run.Mode != CombatRunMode.Ready && run.Mode != CombatRunMode.GameOver)
                || input.IsRebinding
                || !run.BeginInputSettings())
            {
                return;
            }

            OpenPauseMenu(CharacterMenuTab.System);
        }


        private void BeginInputModeSelectionFromPause()
        {
            if ((run.Mode != CombatRunMode.Paused && run.Mode != CombatRunMode.InputSettings)
                || input.IsRebinding)
            {
                return;
            }
            selectedInputModeRow = GetInputModeRow(input.SelectedInputMode);
            inputModeSelectionMessage = "入力方式を変更すると、受け付けるデバイスもその方式だけに切り替わります。";
            run.BeginInputModeSelection();
        }


        private void HandleResetBindings()
        {
            if (!SupportsButtonRebind || input.IsRebinding)
            {
                return;
            }
            input.ResetBindingOverrides();
            modernView.RebuildMenuContent(run);
            modernView.SetSelectedControlRow(selectedSettingsRow);
        }


        private void HandleManualSave()
        {
            string profileMessage = saveProfileCommand != null
                ? saveProfileCommand()
                : "プロフィールの保存先を初期化できていません。";
            bool bindingsSaved = input.SaveBindingOverridesToPlayerPrefs();
            string bindingMessage = bindingsSaved
                ? "ボタン設定も保存しました。"
                : "ボタン設定を保存できませんでした。";
            SetSystemNotice($"{profileMessage} {bindingMessage}");
        }


        private void HandleExportProfile()
        {
            SetSystemNotice(exportProfileCommand != null
                ? exportProfileCommand()
                : "セーブの書き出し先を初期化できていません。");
        }


        private void HandleImportProfile()
        {
            SetSystemNotice(importProfileCommand != null
                ? importProfileCommand()
                : "セーブの取り込み先を初期化できていません。");
        }


        private void HandleCloudDrive()
        {
            SetSystemNotice(cloudDriveCommand != null ? cloudDriveCommand() : "クラウド設定を初期化できていません。");
        }


        private void HandleCloudFolder()
        {
            SetSystemNotice(cloudFolderCommand != null ? cloudFolderCommand() : "クラウド設定を初期化できていません。");
        }


        private void HandleCloudLocal()
        {
            SetSystemNotice(cloudLocalCommand != null ? cloudLocalCommand() : "クラウド設定を初期化できていません。");
        }


        private void SetSystemNotice(string message)
        {
            if (modernView != null)
            {
                modernView.SetSystemNotice(message);
                modernView.SetSelectedControlRow(selectedSettingsRow);
            }
        }


        private void HandlePerformancePreset()
        {
            GamePerformanceSettings.SelectNextPreset();
            if (modernView != null)
            {
                modernView.SetSystemNotice($"描画設定を「{GamePerformanceSettings.CurrentPresetLabel}」に変更しました。");
                modernView.RebuildMenuContent(run);
                modernView.SetSelectedControlRow(selectedSettingsRow);
            }
        }


        private void HandleFrameStatsToggle()
        {
            bool visible = GamePerformanceSettings.ToggleFrameStats();
            if (modernView != null)
            {
                modernView.SetSystemNotice($"FPS表示を{(visible ? "ON" : "OFF")}にしました。");
                modernView.RebuildMenuContent(run);
                modernView.SetSelectedControlRow(selectedSettingsRow);
            }
        }


        private void HandleProgressionChanged()
        {
            if (modernView != null && pauseMenuOpen)
            {
                modernView.RebuildMenuContent(run);
            }
        }


        private void SelectPauseMenuTab(PauseMenuTab tab)
        {
            if (modernView != null)
            {
                SelectModernTab((CharacterMenuTab)tab);
            }
            else
            {
                selectedPauseMenuTab = tab;
            }
        }


        private void HandlePauseMenuNavigation()
        {
            if (modernView != null)
            {
                HandleModernMenuNavigation();
            }
        }


        private void DrawPauseMenu()
        {
            // The active pause menu is rendered by CombatGameHudView (uGUI).
        }


        private void HandleInputModeSelection()
        {
            float vertical = input.Navigate.y;
            if (Mathf.Abs(vertical) < 0.28f)
            {
                inputModeNavigationLatch = 0;
            }
            else
            {
                int direction = vertical > 0.55f ? -1 : vertical < -0.55f ? 1 : 0;
                if (direction != 0 && inputModeNavigationLatch != direction)
                {
                    inputModeNavigationLatch = direction;
                    selectedInputModeRow =
                        (selectedInputModeRow + direction + SelectableInputModes.Length) % SelectableInputModes.Length;
                }
            }

            if (input.CancelPressed)
            {
                run.CancelInputModeSelection(out inputModeSelectionMessage);
                return;
            }

            if (input.ConfirmPressed)
            {
                InputMode selectedMode = SelectableInputModes[selectedInputModeRow];
                if (input.LastUsedInputIsGamepad
                    && input.HasConnectedGamepad
                    && selectedMode == InputMode.KeyboardMouse)
                {
                    selectedInputModeRow = GetInputModeRow(InputMode.ControllerGamepad);
                    selectedMode = InputMode.ControllerGamepad;
                }
                SelectInputMode(selectedMode);
            }
        }


        private void DrawPinnedSystemActions()
        {
            if (!pauseMenuOpen || selectedPauseMenuTab != PauseMenuTab.System)
            {
                return;
            }

            EnsureStyles();
            float height = Mathf.Max(72f, Screen.height * 0.11f);
            Rect bar = new Rect(12f, Screen.height - height - 8f, Screen.width - 24f, height);
            Color previous = GUI.color;
            GUI.color = new Color(0.02f, 0.04f, 0.06f, 0.92f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = previous;

            float buttonWidth = (bar.width - 36f) / 5f;
            float buttonHeight = bar.height * 0.55f;
            float y = bar.y + bar.height * 0.35f;
            if (GUI.Button(new Rect(bar.x + 8f, y, buttonWidth, buttonHeight), "セーブする"))
            {
                HandleManualSave();
            }

            if (GUI.Button(new Rect(bar.x + 12f + buttonWidth, y, buttonWidth, buttonHeight), "書き出す"))
            {
                HandleExportProfile();
            }

            if (GUI.Button(new Rect(bar.x + 16f + buttonWidth * 2f, y, buttonWidth, buttonHeight), "取り込む"))
            {
                HandleImportProfile();
            }

            if (GUI.Button(new Rect(bar.x + 20f + buttonWidth * 3f, y, buttonWidth, buttonHeight), "接続"))
            {
                HandleCoffeeLearningPrimary();
            }

            if (GUI.Button(new Rect(bar.x + 24f + buttonWidth * 4f, y, buttonWidth, buttonHeight), "コード貼付"))
            {
                HandlePasteConnection();
            }
        }


        private void DrawInputModeSelection()
        {
            float width = Mathf.Min(660f, Screen.width - 32f);
            const float height = 560f;
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                Mathf.Max(16f, (Screen.height - height) * 0.5f),
                width,
                Mathf.Min(height, Screen.height - 32f));

            Color previousColor = GUI.color;
            GUI.color = new Color(0.025f, 0.035f, 0.045f, 0.98f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.Box(panel, GUIContent.none);

            GUI.Label(new Rect(panel.x + 20f, panel.y + 16f, panel.width - 40f, 30f),
                "操作方法を選んでください",
                centeredStyle);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 50f, panel.width - 40f, 40f),
                "自動判定は行いません。起動するたびに、実際に使う入力だけを選択します。",
                centeredStyle);

            DrawInputModeButton(
                panel,
                98f,
                0,
                "キーボード／マウス",
                "WASDで移動、Spaceでジャンプ、マウス左で刀。メニューはマウスでも操作できます。",
                true);
            DrawInputModeButton(
                panel,
                178f,
                1,
                "コントローラー（Gamepad）",
                input.HasConnectedGamepad
                    ? "Gamepadを検出しました。Steam ControllerもGamepadとして届いていればこちらを選びます。"
                    : "Gamepad未検出。Steamへ非Steamゲームとして登録し、GamepadテンプレートでSteamから起動してください。",
                input.HasConnectedGamepad);
            DrawInputModeButton(
                panel,
                258f,
                2,
                "Steam Desktop互換で続ける",
                "SteamがA/X/Y/RTなどをEnter/PageUp/PageDown/Mouseへ変換している場合だけ使います。",
                true);
            DrawInputModeButton(
                panel,
                338f,
                3,
                "タッチ（画面操作）",
                "横画面。左半分を少しスワイプして押しっぱなしで移動、右半分でカメラ、右下で跳／刀／居合／氷。",
                true);

            string status = string.IsNullOrEmpty(inputModeSelectionMessage)
                ? (Application.isMobilePlatform
                    ? "この端末では『タッチ（画面操作）』を選ぶと画面の仮想パッドで遊べます。"
                    : "おすすめ: Steamへ登録してライブラリから起動し、『コントローラー（Gamepad）』を選択。")
                : inputModeSelectionMessage;
            GUI.Label(new Rect(panel.x + 20f, panel.y + 428f, panel.width - 40f, 52f), status, smallStyle);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 490f, panel.width - 40f, 42f),
                "操作: ↑↓／左スティックで選択、Enter／Southで決定。タップやマウスでも選べます。",
                centeredStyle);
        }


        private void DrawInputModeButton(
            Rect panel,
            float yOffset,
            int row,
            string title,
            string description,
            bool available)
        {
            GUIStyle style = selectedInputModeRow == row ? selectedButtonStyle : buttonStyle;
            string availability = available ? string.Empty : "  ［現在は選択不可］";
            if (GUI.Button(
                    new Rect(panel.x + 20f, panel.y + yOffset, panel.width - 40f, 34f),
                    title + availability,
                    style))
            {
                selectedInputModeRow = row;
                SelectInputMode(SelectableInputModes[row]);
            }
            GUI.Label(new Rect(panel.x + 30f, panel.y + yOffset + 38f, panel.width - 60f, 36f), description, smallStyle);
        }


        private void SelectInputMode(InputMode mode)
        {
            if (run.TrySelectInputMode(mode, out string message))
            {
                inputModeSelectionMessage = message;
                selectedSettingsRow = SupportsButtonRebind ? 0 : CombatHudSettingsRows.InputMode;
                navigationLatch = 0;
                inputModeNavigationLatch = 0;
                if (run.Mode == CombatRunMode.Ready
                    && mode != InputMode.KeyboardMouse)
                {
                    OpenPreBattleInputSettings();
                }
                return;
            }

            inputModeSelectionMessage = message;
        }


        private static int GetInputModeRow(InputMode mode)
        {
            for (int i = 0; i < SelectableInputModes.Length; i++)
            {
                if (SelectableInputModes[i] == mode)
                {
                    return i;
                }
            }
            return 0;
        }


        private bool SupportsButtonRebind =>
            input.SelectedInputMode == InputMode.ControllerGamepad ||
            input.SelectedInputMode == InputMode.SteamDesktopCompatibility;


        private void DrawPlayerHud()
        {
            Health health = run.PlayerHealth;
            PlayerResources resources = run.PlayerResources;
            PlayerProgression progression = run.Progression;

            GUI.Box(new Rect(16f, 16f, 350f, 151f), GUIContent.none);
            GUI.Label(new Rect(28f, 24f, 320f, 24f), $"Lv.{progression.Level}   討伐 {run.Kills}", titleStyle);
            DrawBar(new Rect(28f, 56f, 220f, 15f), health.Normalized, new Color(0.82f, 0.18f, 0.26f), $"HP {health.Current}/{health.Maximum}");
            DrawBar(new Rect(28f, 80f, 220f, 15f), resources.MaxStamina <= 0f ? 0f : resources.Stamina / resources.MaxStamina, new Color(0.95f, 0.68f, 0.18f), $"ST {Mathf.FloorToInt(resources.Stamina)}/{Mathf.FloorToInt(resources.MaxStamina)}");
            DrawBar(new Rect(28f, 104f, 220f, 15f), resources.MaxMagicPoints <= 0f ? 0f : resources.MagicPoints / resources.MaxMagicPoints, new Color(0.2f, 0.65f, 0.95f), $"MP {resources.MagicPoints:0.0}/{resources.MaxMagicPoints:0}");
            DrawBar(new Rect(28f, 128f, 220f, 15f), progression.ExperienceRequiredForNextLevel <= 0 ? 0f : (float)progression.Experience / progression.ExperienceRequiredForNextLevel, new Color(0.42f, 0.82f, 0.45f), $"EXP {progression.Experience}/{progression.ExperienceRequiredForNextLevel}");
            GUI.Label(new Rect(260f, 57f, 95f, 86f), $"Gold {progression.Gold}\nゼリー {progression.SlimeJelly}\n{run.LastEvent}", smallStyle);

            if (run.PlayerCombat.IsCharging)
            {
                Rect chargeRect = new Rect(Screen.width * 0.5f - 150f, Screen.height - 90f, 300f, 22f);
                DrawBar(chargeRect, run.PlayerCombat.ChargeNormalized, new Color(0.52f, 0.84f, 1f), $"{run.PlayerCombat.ChargeLabel} CHARGE");
            }
        }


        private void DrawInputDiagnostic()
        {
            bool stackBelowPlayerHud = Screen.width < 800f;
            float width = stackBelowPlayerHud
                ? Mathf.Min(650f, Screen.width - 32f)
                : Mathf.Min(650f, Screen.width - 398f);
            float x = stackBelowPlayerHud ? 16f : Screen.width - width - 16f;
            float y = stackBelowPlayerHud ? 176f : 16f;
            Rect panel = new Rect(x, y, width, 154f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 152f, 22f), "入力診断", titleStyle);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = !input.IsRebinding;
            if (AcceptSettingsPointerCommand(GUI.Button(
                    new Rect(panel.xMax - 138f, panel.y + 8f, 126f, 25f),
                    showInputSettings ? "設定を閉じる" : "ボタン設定",
                    buttonStyle)))
            {
                ToggleInputSettings();
            }
            GUI.enabled = previousEnabled;

            GUI.Label(new Rect(panel.x + 12f, panel.y + 36f, panel.width - 24f, 34f), $"選択中: {input.ActiveControllerProfileName} / {input.ConnectedControllersSummary}", smallStyle);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 70f, panel.width - 24f, 34f), $"直近の実ボタン: {input.LastRawButtonDiagnostic}", smallStyle);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 105f, panel.width - 24f, 42f),
                $"Jump {input.GetActiveControllerBindingDescription(GameInputSemantic.Jump)}\nSword {input.GetActiveControllerBindingDescription(GameInputSemantic.Sword)}  |  Iai {input.GetActiveControllerBindingDescription(GameInputSemantic.Special)}  |  Ice {input.GetActiveControllerBindingDescription(GameInputSemantic.Magic)}  |  Dodge {input.GetActiveControllerBindingDescription(GameInputSemantic.Dodge)}",
                smallStyle);
        }


        private void DrawRunOverlay()
        {
            if (run.Mode == CombatRunMode.Playing ||
                run.Mode == CombatRunMode.InputSettings ||
                run.Mode == CombatRunMode.InputRebinding)
            {
                return;
            }

            float width = Mathf.Min(510f, Screen.width - 40f);
            const float height = 220f;
            Rect rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(rect, GUIContent.none);
            string heading = run.Mode == CombatRunMode.Ready ? "COFFEEGAME — UNITY COMBAT SLICE" :
                run.Mode == CombatRunMode.Paused ? "一時停止" :
                "GAME OVER";
            GUI.Label(new Rect(rect.x + 18f, rect.y + 24f, rect.width - 36f, 36f), heading, centeredStyle);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 70f, rect.width - 36f, 28f), run.LastEvent, centeredStyle);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 108f, rect.width - 36f, 46f),
                "移動: Left Stick / WASD   ジャンプ: South / Space\n刀: Right Trigger / F   居合斬り: West / Q   氷魔法: North / E",
                centeredStyle);

            string buttonLabel = run.Mode == CombatRunMode.Paused ? "再開" : "開始";
            if (AcceptPointerCommand(GUI.Button(
                    new Rect(rect.x + 90f, rect.y + 166f, rect.width - 180f, 34f),
                    buttonLabel,
                    buttonStyle)))
            {
                if (run.Mode == CombatRunMode.Paused)
                {
                    run.Resume();
                }
                else
                {
                    run.StartNewRun();
                }
            }
        }


        private void BeginRebind(GameInputSemantic semantic)
        {
            if (!SupportsButtonRebind)
            {
                return;
            }

            int bindingIndex = input.GetBindingIndexForGroup(semantic, input.PreferredRebindBindingGroup);
            if (bindingIndex < 0 || !run.BeginInputRebind())
            {
                return;
            }

            if (!input.TryStartInteractiveRebind(semantic, bindingIndex))
            {
                run.CancelInputRebindMode();
            }
        }


        private bool AcceptPointerCommand(bool wasClicked)
        {
            if (!wasClicked)
            {
                return false;
            }

            // Gameplay bindings remain profile-exclusive. Pointer interaction is
            // allowed in menu contexts so a controller profile can still be
            // configured or recovered from the on-screen settings UI.
            return input.SelectedInputMode == InputMode.KeyboardMouse ||
                   input.SelectedInputMode == InputMode.Unselected ||
                   input.Context == GameInputContext.UI ||
                   input.Context == GameInputContext.InputSelection;
        }


        private static bool AcceptSettingsPointerCommand(bool wasClicked)
        {
            // The settings entry is an explicit recovery path even while Battle
            // uses an exclusive controller binding profile.
            return wasClicked;
        }


        private static void DrawBar(Rect rect, float normalized, Color fillColor, string label)
        {
            GUI.Box(rect, GUIContent.none);
            Color previous = GUI.color;
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * Mathf.Clamp01(normalized), rect.height - 4f), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(rect, label, GUI.skin.label);
        }


        private void OnDestroy()
        {
            if (modernView != null)
            {
                modernView.PauseRequested -= HandlePointerPause;
                modernView.ResumeRequested -= ResumeFromPauseMenu;
                modernView.StartRequested -= run.StartNewRun;
                modernView.TabRequested -= HandlePointerTab;
                modernView.RebindRequested -= BeginRebind;
                modernView.InputModeSelectionRequested -= BeginInputModeSelectionFromPause;
                modernView.PerformancePresetRequested -= HandlePerformancePreset;
                modernView.FrameStatsToggleRequested -= HandleFrameStatsToggle;
                modernView.SaveRequested -= HandleManualSave;
                modernView.ResetBindingsRequested -= HandleResetBindings;
                modernView.CancelRebindRequested -= input.CancelInteractiveRebind;
                modernView.CoffeeLearningPrimaryRequested -= HandleCoffeeLearningPrimary;
                modernView.PasteConnectionRequested -= HandlePasteConnection;
                modernView.CoffeeLearningDisconnectRequested -= HandleCoffeeLearningDisconnect;
                modernView.CoffeeLearningCancelRequested -= HandleCoffeeLearningCancel;
                modernView.RivalAnswerChanged -= HandleRivalAnswerChanged;
                modernView.RivalPrimaryRequested -= HandleRivalPrimary;
                modernView.RivalSecondaryRequested -= HandleRivalSecondary;
                modernView.RivalContinueRequested -= HandleRivalContinue;
            }
            coffeeLearningConnection?.CancelPendingOrActiveAction();
            rivalLearningQuestion?.Dispose();
            rivalLearningQuestion = null;
            if (run != null && run.Progression != null)
            {
                run.StateChanged -= HandleRunStateChanged;
                run.Progression.Changed -= HandleProgressionChanged;
            }
        }


        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            centeredStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            selectedButtonStyle = new GUIStyle(buttonStyle) { fontStyle = FontStyle.Bold };
            selectedButtonStyle.normal.textColor = new Color(1f, 0.82f, 0.28f);
            selectedButtonStyle.focused.textColor = new Color(1f, 0.82f, 0.28f);
        }
    }
}
