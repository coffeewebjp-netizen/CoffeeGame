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

        private void HandleModernMenuNavigation()
        {
            Vector2 navigation = input.Navigate;
            int tabDirection = MenuNavigationAxisLatch.Read(navigation.x, ref pauseTabNavigationLatch);
            if (tabDirection != 0)
            {
                int count = System.Enum.GetValues(typeof(CharacterMenuTab)).Length;
                int next = ((int)selectedPauseMenuTab + tabDirection + count) % count;
                SelectModernTab((CharacterMenuTab)next);
            }

            if ((CharacterMenuTab)selectedPauseMenuTab != CharacterMenuTab.System)
            {
                navigationLatch = 0;
                if (tabDirection == 0)
                {
                    modernView.ScrollMenu(navigation.y, Time.unscaledDeltaTime);
                }
                return;
            }

            int verticalDirection = MenuNavigationAxisLatch.Read(navigation.y, ref navigationLatch);
            if (verticalDirection != 0)
            {
                int rowDirection = -verticalDirection;
                selectedSettingsRow = FindNextModernControlRow(selectedSettingsRow, rowDirection);
                modernView.SetSelectedControlRow(selectedSettingsRow);
            }

            if (input.ConfirmPressed)
            {
                ActivateModernControlRow();
            }
        }


        private int FindNextModernControlRow(int current, int direction)
        {
            int candidate = current;
            do
            {
                candidate = (candidate + direction + CombatHudSettingsRows.Count) % CombatHudSettingsRows.Count;
            }
            while (!IsSettingsRowSelectable(candidate));
            return candidate;
        }


        private void ActivateModernControlRow()
        {
            switch (selectedSettingsRow)
            {
                case CombatHudSettingsRows.Jump:
                    BeginRebind(GameInputSemantic.Jump);
                    break;
                case CombatHudSettingsRows.Sword:
                    BeginRebind(GameInputSemantic.Sword);
                    break;
                case CombatHudSettingsRows.Special:
                    BeginRebind(GameInputSemantic.Special);
                    break;
                case CombatHudSettingsRows.Magic:
                    BeginRebind(GameInputSemantic.Magic);
                    break;
                case CombatHudSettingsRows.Dodge:
                    BeginRebind(GameInputSemantic.Dodge);
                    break;
                case CombatHudSettingsRows.InputMode:
                    BeginInputModeSelectionFromPause();
                    break;
                case CombatHudSettingsRows.Save:
                    HandleManualSave();
                    break;
                case CombatHudSettingsRows.ExportProfile:
                    HandleExportProfile();
                    break;
                case CombatHudSettingsRows.ImportProfile:
                    HandleImportProfile();
                    break;
                case CombatHudSettingsRows.CloudDrive:
                    HandleCloudDrive();
                    break;
                case CombatHudSettingsRows.CloudFolder:
                    HandleCloudFolder();
                    break;
                case CombatHudSettingsRows.CloudLocal:
                    HandleCloudLocal();
                    break;
                case CombatHudSettingsRows.ResetBindings:
                    HandleResetBindings();
                    break;
                case CombatHudSettingsRows.Resume:
                    ResumeFromPauseMenu();
                    break;
                case CombatHudSettingsRows.Performance:
                    HandlePerformancePreset();
                    break;
                case CombatHudSettingsRows.FrameStats:
                    HandleFrameStatsToggle();
                    break;
                case CombatHudSettingsRows.CoffeeLearningPrimary:
                    HandleCoffeeLearningPrimary();
                    break;
                case CombatHudSettingsRows.CoffeeLearningDisconnect:
                    HandleCoffeeLearningDisconnect();
                    break;
                case CombatHudSettingsRows.CoffeeLearningCancel:
                    HandleCoffeeLearningCancel();
                    break;
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

            DrawRebindRow(panel, 84f, CombatHudSettingsRows.Jump, "ジャンプ", GameInputSemantic.Jump);
            DrawRebindRow(panel, 124f, CombatHudSettingsRows.Sword, "刀攻撃", GameInputSemantic.Sword);
            DrawRebindRow(panel, 164f, CombatHudSettingsRows.Special, "居合斬り", GameInputSemantic.Special);
            DrawRebindRow(panel, 204f, CombatHudSettingsRows.Magic, "氷魔法", GameInputSemantic.Magic);
            DrawRebindRow(panel, 244f, CombatHudSettingsRows.Dodge, "回避", GameInputSemantic.Dodge);

            DrawSettingsCommandButton(
                panel,
                290f,
                CombatHudSettingsRows.InputMode,
                $"入力方式を選び直す（現在: {input.ActiveControllerProfileName}）",
                BeginInputModeSelectionFromSettings);
            DrawSettingsCommandButton(panel, 330f, CombatHudSettingsRows.Save, "プロフィールとボタン設定を保存", HandleManualSave);
            DrawSettingsCommandButton(panel, 370f, CombatHudSettingsRows.ResetBindings, "初期配置へ戻す", () => input.ResetBindingOverrides(), SupportsButtonRebind);
            DrawSettingsCommandButton(panel, 410f, CombatHudSettingsRows.Resume, "設定を保存して閉じる", CloseInputSettings);

            DrawSettingsCommandButton(
                panel,
                450f,
                CombatHudSettingsRows.Performance,
                $"描画プリセット: {GamePerformanceSettings.CurrentPresetLabel}",
                HandlePerformancePreset);
            DrawSettingsCommandButton(
                panel,
                490f,
                CombatHudSettingsRows.FrameStats,
                $"FPS表示: {(GamePerformanceSettings.ShowFrameStats ? "ON" : "OFF")}",
                HandleFrameStatsToggle);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = input.IsRebinding;
            if (AcceptPointerCommand(GUI.Button(
                    new Rect(panel.x + 14f, panel.y + 530f, panel.width - 28f, 27f),
                    "再割当を取り消す（Start/View・Esc）",
                    buttonStyle)))
            {
                input.CancelInteractiveRebind();
            }
            GUI.enabled = previousEnabled;

            string operationHelp = input.SelectedInputMode switch
            {
                InputMode.SteamDesktopCompatibility =>
                    "Steam Desktop互換: B/Spaceは通常の取消、再割当待機中は戦闘ボタンとして選択可。Escで再割当を取り消します。変換後の実control pathを保存します。",
                InputMode.ControllerGamepad =>
                    "操作: ↑↓で選択 / South位置で決定 / East位置で通常取消。再割当待機中はEastも戦闘ボタンに選択可。Start/View・Escで再割当を取り消します。",
                _ =>
                    "キーボード／マウスの基本配置は現在固定です。変更したい場合は『入力方式を選び直す』からGamepadまたはSteam Desktop互換を選択してください。"
            };
            GUI.Label(new Rect(panel.x + 14f, panel.y + 566f, panel.width - 28f, 48f),
                operationHelp,
                smallStyle);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 618f, panel.width - 28f, Mathf.Max(40f, panel.height - 630f)),
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
                selectedSettingsRow = (selectedSettingsRow + direction + CombatHudSettingsRows.Count) % CombatHudSettingsRows.Count;
            }
            while (!IsSettingsRowSelectable(selectedSettingsRow));
        }


        private void ActivateSelectedSettingsRow()
        {
            switch (selectedSettingsRow)
            {
                case CombatHudSettingsRows.Jump:
                    BeginRebind(GameInputSemantic.Jump);
                    break;
                case CombatHudSettingsRows.Sword:
                    BeginRebind(GameInputSemantic.Sword);
                    break;
                case CombatHudSettingsRows.Special:
                    BeginRebind(GameInputSemantic.Special);
                    break;
                case CombatHudSettingsRows.Magic:
                    BeginRebind(GameInputSemantic.Magic);
                    break;
                case CombatHudSettingsRows.Dodge:
                    BeginRebind(GameInputSemantic.Dodge);
                    break;
                case CombatHudSettingsRows.InputMode:
                    BeginInputModeSelectionFromSettings();
                    break;
                case CombatHudSettingsRows.Save:
                    HandleManualSave();
                    break;
                case CombatHudSettingsRows.ExportProfile:
                    HandleExportProfile();
                    break;
                case CombatHudSettingsRows.ImportProfile:
                    HandleImportProfile();
                    break;
                case CombatHudSettingsRows.CloudDrive:
                    HandleCloudDrive();
                    break;
                case CombatHudSettingsRows.CloudFolder:
                    HandleCloudFolder();
                    break;
                case CombatHudSettingsRows.CloudLocal:
                    HandleCloudLocal();
                    break;
                case CombatHudSettingsRows.ResetBindings:
                    if (SupportsButtonRebind)
                    {
                        input.ResetBindingOverrides();
                    }
                    break;
                case CombatHudSettingsRows.Resume:
                    CloseInputSettings();
                    break;
                case CombatHudSettingsRows.Performance:
                    HandlePerformancePreset();
                    break;
                case CombatHudSettingsRows.FrameStats:
                    HandleFrameStatsToggle();
                    break;
                case CombatHudSettingsRows.CoffeeLearningPrimary:
                    HandleCoffeeLearningPrimary();
                    break;
                case CombatHudSettingsRows.CoffeeLearningDisconnect:
                    HandleCoffeeLearningDisconnect();
                    break;
                case CombatHudSettingsRows.CoffeeLearningCancel:
                    HandleCoffeeLearningCancel();
                    break;
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
                selectedSettingsRow = SupportsButtonRebind ? 0 : CombatHudSettingsRows.InputMode;
                navigationLatch = 0;
            }
        }


        private bool IsSettingsRowSelectable(int row)
        {
            if (row == CombatHudSettingsRows.CoffeeLearningPrimary)
            {
                return coffeeLearningConnection != null
                    && coffeeLearningConnection.CanUsePrimaryAction;
            }

            if (row == CombatHudSettingsRows.CoffeeLearningDisconnect)
            {
                return coffeeLearningConnection != null
                    && coffeeLearningConnection.CanUseDisconnectAction;
            }

            if (row == CombatHudSettingsRows.CoffeeLearningCancel)
            {
                return coffeeLearningConnection != null
                    && coffeeLearningConnection.CanUseCancelAction;
            }

            return SupportsButtonRebind ||
                   row == CombatHudSettingsRows.InputMode ||
                   row == CombatHudSettingsRows.Save ||
                   row == CombatHudSettingsRows.ExportProfile ||
                   row == CombatHudSettingsRows.ImportProfile ||
                   row == CombatHudSettingsRows.CloudDrive ||
                   row == CombatHudSettingsRows.CloudFolder ||
                   row == CombatHudSettingsRows.CloudLocal ||
                   row == CombatHudSettingsRows.Resume ||
                   row == CombatHudSettingsRows.Performance ||
                   row == CombatHudSettingsRows.FrameStats;
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
    }
}
