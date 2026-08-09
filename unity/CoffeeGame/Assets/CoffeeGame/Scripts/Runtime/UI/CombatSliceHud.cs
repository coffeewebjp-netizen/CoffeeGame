using CoffeeGame.Actors;
using CoffeeGame.Domain;
using CoffeeGame.Input;
using CoffeeGame.Run;
using UnityEngine;

namespace CoffeeGame.UI
{
    [DisallowMultipleComponent]
    public sealed class CombatSliceHud : MonoBehaviour
    {
        private const int InputModeRow = 4;
        private const int ResetRow = 5;
        private const int CloseRow = 6;
        private const int SettingsRowCount = 7;
        private static readonly InputMode[] SelectableInputModes =
        {
            InputMode.KeyboardMouse,
            InputMode.ControllerGamepad,
            InputMode.SteamDesktopCompatibility
        };

        private CombatRunController run;
        private GameInputReader input;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle centeredStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private bool showInputSettings;
        private int selectedSettingsRow;
        private int navigationLatch;
        private int selectedInputModeRow;
        private int inputModeNavigationLatch;
        private string inputModeSelectionMessage = string.Empty;

        public void Initialize(CombatRunController runController, GameInputReader inputReader)
        {
            run = runController;
            input = inputReader;
            selectedInputModeRow = GetInputModeRow(input.PreferredInputModeForSelection);
        }

        private void Update()
        {
            if (run == null || input == null)
            {
                return;
            }

            if (run.Mode == CombatRunMode.InputModeSelection)
            {
                HandleInputModeSelection();
                return;
            }

            if (input.IsRebinding)
            {
                return;
            }

            if (input.SettingsPressed)
            {
                ToggleInputSettings();
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

            EnsureStyles();
            if (run.Mode == CombatRunMode.InputModeSelection)
            {
                DrawInputModeSelection();
                return;
            }

            if (showInputSettings)
            {
                DrawInputSettings();
                return;
            }

            DrawPlayerHud();
            DrawInputDiagnostic();
            DrawRunOverlay();
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
                SelectInputMode(SelectableInputModes[selectedInputModeRow]);
            }
        }

        private void DrawInputModeSelection()
        {
            float width = Mathf.Min(660f, Screen.width - 32f);
            const float height = 472f;
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

            string status = string.IsNullOrEmpty(inputModeSelectionMessage)
                ? "おすすめ: Steamへ登録してライブラリから起動し、『コントローラー（Gamepad）』を選択。"
                : inputModeSelectionMessage;
            GUI.Label(new Rect(panel.x + 20f, panel.y + 348f, panel.width - 40f, 52f), status, smallStyle);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 410f, panel.width - 40f, 42f),
                "操作: ↑↓／左スティックで選択、Enter／Southで決定。マウスで直接クリックもできます。",
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
                selectedSettingsRow = SupportsButtonRebind ? 0 : InputModeRow;
                navigationLatch = 0;
                inputModeNavigationLatch = 0;
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
            GUI.Label(new Rect(28f, 24f, 320f, 24f), $"Lv.{progression.Level}   討伐 {run.Kills}/{run.GoalKills}", titleStyle);
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
                $"Jump {input.GetActiveControllerBindingDescription(GameInputSemantic.Jump)}\nSword {input.GetActiveControllerBindingDescription(GameInputSemantic.Sword)}  |  Spin {input.GetActiveControllerBindingDescription(GameInputSemantic.Special)}  |  Ice {input.GetActiveControllerBindingDescription(GameInputSemantic.Magic)}",
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
                run.Mode == CombatRunMode.Cleared ? "CLEAR" : "GAME OVER";
            GUI.Label(new Rect(rect.x + 18f, rect.y + 24f, rect.width - 36f, 36f), heading, centeredStyle);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 70f, rect.width - 36f, 28f), run.LastEvent, centeredStyle);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 108f, rect.width - 36f, 46f),
                "移動: Left Stick / WASD   ジャンプ: South / Space\n刀: Right Trigger / F   回転斬り: West / Q   氷魔法: North / E",
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

        private void DrawInputSettings()
        {
            float width = Mathf.Min(590f, Screen.width - 32f);
            float height = Screen.height - 32f;
            Rect panel = new Rect((Screen.width - width) * 0.5f, 16f, width, height);
            Color previousColor = GUI.color;
            GUI.color = new Color(0.025f, 0.035f, 0.045f, 0.97f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.Box(panel, GUIContent.none);

            string title = input.IsWaitingForRebindButtonRelease
                ? "決定に使ったボタンを離してください"
                : input.IsRebinding
                    ? input.UsesSteamDesktopFallback
                        ? $"新しく使うSteam Controllerボタンを押してください（残り {Mathf.CeilToInt(input.RebindSecondsRemaining)}秒）"
                        : $"新しく使うGamepadボタンを押してください（残り {Mathf.CeilToInt(input.RebindSecondsRemaining)}秒）"
                    : SupportsButtonRebind
                        ? $"{input.ActiveControllerProfileName} ボタン割当"
                        : "キーボード／マウス設定";
            GUI.Label(new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, 24f), title, titleStyle);

            string status = string.IsNullOrEmpty(input.LastRebindMessage)
                ? "View/Select または Tab で開閉できます。"
                : input.LastRebindMessage;
            GUI.Label(new Rect(panel.x + 14f, panel.y + 36f, panel.width - 28f, 43f), status, smallStyle);

            DrawRebindRow(panel, 84f, 0, "ジャンプ", GameInputSemantic.Jump);
            DrawRebindRow(panel, 124f, 1, "刀攻撃", GameInputSemantic.Sword);
            DrawRebindRow(panel, 164f, 2, "回転斬り", GameInputSemantic.Special);
            DrawRebindRow(panel, 204f, 3, "氷魔法", GameInputSemantic.Magic);

            DrawSettingsCommandButton(
                panel,
                250f,
                InputModeRow,
                $"入力方式を選び直す（現在: {input.ActiveControllerProfileName}）",
                BeginInputModeSelectionFromSettings);
            DrawSettingsCommandButton(panel, 290f, ResetRow, "初期配置へ戻す", () => input.ResetBindingOverrides(), SupportsButtonRebind);
            DrawSettingsCommandButton(panel, 330f, CloseRow, "設定を保存して閉じる", CloseInputSettings);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = input.IsRebinding;
            if (AcceptPointerCommand(GUI.Button(
                    new Rect(panel.x + 14f, panel.y + 370f, panel.width - 28f, 27f),
                    "再割当を取り消す（B/Space・East・Esc）",
                    buttonStyle)))
            {
                input.CancelInteractiveRebind();
            }
            GUI.enabled = previousEnabled;

            string operationHelp = input.SelectedInputMode switch
            {
                InputMode.SteamDesktopCompatibility =>
                    "Steam Desktop互換: Stick/矢印で選択、A/Enterで決定、B/Spaceで取消。A=Enter、X=PageUp、Y=PageDown、RT=Mouse Leftとして届く実際のcontrol pathを保存します。",
                InputMode.ControllerGamepad =>
                    "操作: ↑↓で選択 / South位置で決定 / East位置で取消。物理ABXYの文字ではなく、Unityへ届いた実際のcontrol pathを表示・保存します。",
                _ =>
                    "キーボード／マウスの基本配置は現在固定です。変更したい場合は『入力方式を選び直す』からGamepadまたはSteam Desktop互換を選択してください。"
            };
            GUI.Label(new Rect(panel.x + 14f, panel.y + 406f, panel.width - 28f, 48f),
                operationHelp,
                smallStyle);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 458f, panel.width - 28f, Mathf.Max(40f, panel.height - 470f)),
                input.ControllerCompatibilityHint,
                smallStyle);
        }

        private void DrawRebindRow(Rect panel, float yOffset, int row, string label, GameInputSemantic semantic)
        {
            GUI.Label(new Rect(panel.x + 16f, panel.y + yOffset, 92f, 30f), label, labelStyle);
            string current = input.GetActiveControllerBindingDescription(semantic);
            if (!SupportsButtonRebind)
            {
                current += "（固定）";
            }
            GUIStyle style = selectedSettingsRow == row ? selectedButtonStyle : buttonStyle;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = SupportsButtonRebind && !input.IsRebinding;
            if (AcceptPointerCommand(GUI.Button(
                    new Rect(panel.x + 112f, panel.y + yOffset, panel.width - 128f, 30f),
                    current,
                    style)))
            {
                selectedSettingsRow = row;
                BeginRebind(semantic);
            }
            GUI.enabled = previousEnabled;
        }

        private void DrawSettingsCommandButton(
            Rect panel,
            float yOffset,
            int row,
            string text,
            System.Action command,
            bool enabled = true)
        {
            GUIStyle style = selectedSettingsRow == row ? selectedButtonStyle : buttonStyle;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = enabled && !input.IsRebinding;
            if (AcceptPointerCommand(GUI.Button(
                    new Rect(panel.x + 14f, panel.y + yOffset, panel.width - 28f, 30f),
                    text,
                    style)))
            {
                selectedSettingsRow = row;
                command?.Invoke();
            }
            GUI.enabled = previousEnabled;
        }

        private void HandleSettingsNavigation()
        {
            float vertical = input.Navigate.y;
            if (Mathf.Abs(vertical) < 0.28f)
            {
                navigationLatch = 0;
                return;
            }

            int direction = vertical > 0.55f ? -1 : vertical < -0.55f ? 1 : 0;
            if (direction == 0 || navigationLatch == direction)
            {
                return;
            }

            navigationLatch = direction;
            do
            {
                selectedSettingsRow = (selectedSettingsRow + direction + SettingsRowCount) % SettingsRowCount;
            }
            while (!IsSettingsRowSelectable(selectedSettingsRow));
        }

        private void ActivateSelectedSettingsRow()
        {
            switch (selectedSettingsRow)
            {
                case 0:
                    BeginRebind(GameInputSemantic.Jump);
                    break;
                case 1:
                    BeginRebind(GameInputSemantic.Sword);
                    break;
                case 2:
                    BeginRebind(GameInputSemantic.Special);
                    break;
                case 3:
                    BeginRebind(GameInputSemantic.Magic);
                    break;
                case InputModeRow:
                    BeginInputModeSelectionFromSettings();
                    break;
                case ResetRow:
                    if (SupportsButtonRebind)
                    {
                        input.ResetBindingOverrides();
                    }
                    break;
                case CloseRow:
                    CloseInputSettings();
                    break;
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

        private void ToggleInputSettings()
        {
            if (input.IsRebinding)
            {
                return;
            }

            if (showInputSettings)
            {
                CloseInputSettings();
                return;
            }

            if (run.BeginInputSettings())
            {
                showInputSettings = true;
                selectedSettingsRow = SupportsButtonRebind ? 0 : InputModeRow;
                navigationLatch = 0;
            }
        }

        private bool IsSettingsRowSelectable(int row)
        {
            return SupportsButtonRebind || row == InputModeRow || row == CloseRow;
        }

        private void BeginInputModeSelectionFromSettings()
        {
            InputMode currentMode = input.SelectedInputMode;
            selectedInputModeRow = GetInputModeRow(
                currentMode == InputMode.Unselected ? input.PreferredInputModeForSelection : currentMode);
            inputModeSelectionMessage = "入力方式を変更すると、受け付けるデバイスもその方式だけに切り替わります。";
            inputModeNavigationLatch = 0;
            run.BeginInputModeSelection();
        }

        private void CloseInputSettings()
        {
            if (input.IsRebinding)
            {
                input.CancelInteractiveRebind();
                return;
            }

            if (run.EndInputSettings())
            {
                showInputSettings = false;
                navigationLatch = 0;
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
