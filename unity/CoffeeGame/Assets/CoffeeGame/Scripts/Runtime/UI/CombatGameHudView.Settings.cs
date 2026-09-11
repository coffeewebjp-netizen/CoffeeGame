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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private PlayerProgression debugProgression;
        public event Action<int> DebugAffinityRequested;
#endif
        public event Action<int> RivalIntervalRequested;

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
            AddControlButton(menuScrollContent, GameInputSemantic.Sword, "通常攻撃");
            AddControlButton(menuScrollContent, GameInputSemantic.Special, "必殺技");
            AddControlButton(menuScrollContent, GameInputSemantic.Magic, "魔法");
            AddControlButton(menuScrollContent, GameInputSemantic.Dodge, "回避");
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
            AddControlButton(menuScrollContent, GameInputSemantic.SwitchCharacter, "操作切替");
            AddControlButton(menuScrollContent, GameInputSemantic.Guard, "防御");
            AddControlButton(menuScrollContent, GameInputSemantic.LockOn, "ターゲット固定");
            AddSectionHeading(menuScrollContent, "進行設定", 27, Accent, 42f);
            AddCommandButton(menuScrollContent, "Rival Interval Decrease", () => RivalIntervalRequested?.Invoke(-1));
            AddCommandButton(menuScrollContent, "Rival Interval Increase", () => RivalIntervalRequested?.Invoke(1));
            AddCommandButton(menuScrollContent, "Rival Interval Reset", () => RivalIntervalRequested?.Invoke(0));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddSectionHeading(menuScrollContent, "デバッグ：ライバル親密度", 27, Accent, 42f);
            AddSectionHeading(menuScrollContent, "100%で加入。数値を下げても加入済みの仲間は残ります。", 20, MutedInk, 42f);
            for (int index = 0; index < 8; index++)
            {
                int captured = index;
                AddCommandButton(menuScrollContent, "Debug Affinity " + index, () => DebugAffinityRequested?.Invoke(captured));
            }
#endif
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


        private void RefreshControls(bool rebinding)
        {
            if (controlsStatusText == null || controlButtons.Count < CombatHudSettingsRows.Count)
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
                GameInputSemantic.Magic,
                GameInputSemantic.Dodge,
                GameInputSemantic.Guard
            };
            string[] labels = { "ジャンプ", "通常攻撃", "必殺技", "魔法", "回避", "防御" };
            bool supportsRebind = input.SelectedInputMode == InputMode.ControllerGamepad ||
                                  input.SelectedInputMode == InputMode.SteamDesktopCompatibility;
            controlButtons[CombatHudSettingsRows.SwitchCharacter].GetComponentInChildren<Text>().text =
                "操作切替　" + input.GetActiveControllerBindingDescription(GameInputSemantic.SwitchCharacter);
            controlButtons[CombatHudSettingsRows.SwitchCharacter].interactable = supportsRebind && !rebinding;
            controlButtons[CombatHudSettingsRows.LockOn].GetComponentInChildren<Text>().text =
                "ターゲット固定　" + input.GetActiveControllerBindingDescription(GameInputSemantic.LockOn);
            controlButtons[CombatHudSettingsRows.LockOn].interactable = supportsRebind && !rebinding;
            int rivalInterval = renderedPartyRun != null ? renderedPartyRun.RivalEncounterIntervalKills : 5;
            controlButtons[CombatHudSettingsRows.RivalIntervalDecrease].GetComponentInChildren<Text>().text =
                $"ライバル出現まで　{rivalInterval}体　　−1";
            controlButtons[CombatHudSettingsRows.RivalIntervalIncrease].GetComponentInChildren<Text>().text =
                $"ライバル出現まで　{rivalInterval}体　　＋1";
            controlButtons[CombatHudSettingsRows.RivalIntervalReset].GetComponentInChildren<Text>().text =
                $"標準の{(renderedPartyRun != null ? renderedPartyRun.DefaultRivalEncounterIntervalKills : 5)}体に戻す";
            controlButtons[CombatHudSettingsRows.RivalIntervalDecrease].interactable = !rebinding && rivalInterval > RivalEncounterSettings.Minimum;
            controlButtons[CombatHudSettingsRows.RivalIntervalIncrease].interactable = !rebinding && rivalInterval < RivalEncounterSettings.Maximum;
            controlButtons[CombatHudSettingsRows.RivalIntervalReset].interactable = !rebinding;
            for (int index = 0; index < semantics.Length; index++)
            {
                int row = index == semantics.Length - 1 ? CombatHudSettingsRows.Guard : index;
                controlButtons[row].GetComponentInChildren<Text>().text =
                    $"{labels[index]}　　{input.GetActiveControllerBindingDescription(semantics[index])}";
                controlButtons[row].interactable = supportsRebind && !rebinding;
            }
            controlButtons[CombatHudSettingsRows.InputMode].GetComponentInChildren<Text>().text =
                $"入力方式を選び直す　（現在: {input.ActiveControllerProfileName}）";
            controlButtons[CombatHudSettingsRows.InputMode].interactable = !rebinding;
            controlButtons[CombatHudSettingsRows.Save].interactable = !rebinding;
            controlButtons[CombatHudSettingsRows.ExportProfile].interactable = !rebinding;
            controlButtons[CombatHudSettingsRows.ImportProfile].interactable = !rebinding;
            controlButtons[CombatHudSettingsRows.CloudDrive].interactable = !rebinding;
            controlButtons[CombatHudSettingsRows.CloudFolder].interactable = !rebinding;
            controlButtons[CombatHudSettingsRows.CloudLocal].interactable = !rebinding;
            controlButtons[CombatHudSettingsRows.ResetBindings].interactable = supportsRebind && !rebinding;
            controlButtons[CombatHudSettingsRows.Resume].interactable = !rebinding;
            controlButtons[CombatHudSettingsRows.Performance].GetComponentInChildren<Text>().text =
                $"描画プリセット　{GamePerformanceSettings.CurrentPresetLabel}";
            controlButtons[CombatHudSettingsRows.FrameStats].GetComponentInChildren<Text>().text =
                $"FPS表示　{(GamePerformanceSettings.ShowFrameStats ? "ON" : "OFF")}";
            controlButtons[CombatHudSettingsRows.Performance].interactable = !rebinding;
            controlButtons[CombatHudSettingsRows.FrameStats].interactable = !rebinding;
            RefreshCoffeeLearningControls(rebinding);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string[] changes = { "−10%", "＋10%", "0%にする", "100%にする" };
            for (int index = 0; index < 8; index++)
            {
                string id = index < 4 ? RivalCharacterIds.WeaknessChallenger : RivalCharacterIds.SplitInk;
                string name = index < 4 ? "猫少女" : "龍少女";
                Button button = controlButtons[CombatHudSettingsRows.DebugAffinityFirst + index];
                button.GetComponentInChildren<Text>().text = $"{name}  {debugProgression?.GetRivalAffinity(id) ?? 0}%　　{changes[index % 4]}";
                button.interactable = !rebinding && debugProgression != null;
            }
#endif
            SetSelectedControlRow(selectedControlRow);

            Transform cancelTransform = controlsStatusText.transform.parent.Find("Cancel Rebind");
            if (cancelTransform != null)
            {
                cancelTransform.gameObject.SetActive(rebinding);
            }
        }


        private void RefreshCoffeeLearningControls(bool rebinding)
        {
            if (coffeeLearningStatusText == null || controlButtons.Count < CombatHudSettingsRows.Count)
            {
                return;
            }

            if (coffeeLearningConnection == null)
            {
                coffeeLearningStatusText.text = "CoffeeLearning: \u672a\u63a5\u7d9a";
                controlButtons[CombatHudSettingsRows.CoffeeLearningPrimary].GetComponentInChildren<Text>().text = "CoffeeLearning\u3068\u63a5\u7d9a";
                controlButtons[CombatHudSettingsRows.CoffeeLearningPrimary].interactable = false;
                controlButtons[CombatHudSettingsRows.CoffeeLearningDisconnect].GetComponentInChildren<Text>().text = "CoffeeLearning\u63a5\u7d9a\u3092\u89e3\u9664";
                controlButtons[CombatHudSettingsRows.CoffeeLearningDisconnect].interactable = false;
                controlButtons[CombatHudSettingsRows.CoffeeLearningCancel].GetComponentInChildren<Text>().text = "CoffeeLearning\u64cd\u4f5c\u3092\u30ad\u30e3\u30f3\u30bb\u30eb";
                controlButtons[CombatHudSettingsRows.CoffeeLearningCancel].interactable = false;
                return;
            }

            coffeeLearningStatusText.text = "CoffeeLearning: " + coffeeLearningConnection.StatusLabel;
            controlButtons[CombatHudSettingsRows.CoffeeLearningPrimary].GetComponentInChildren<Text>().text = coffeeLearningConnection.PrimaryActionLabel;
            controlButtons[CombatHudSettingsRows.CoffeeLearningDisconnect].GetComponentInChildren<Text>().text = coffeeLearningConnection.DisconnectActionLabel;
            controlButtons[CombatHudSettingsRows.CoffeeLearningCancel].GetComponentInChildren<Text>().text = coffeeLearningConnection.CancelActionLabel;
            controlButtons[CombatHudSettingsRows.CoffeeLearningPrimary].interactable = !rebinding && coffeeLearningConnection.CanUsePrimaryAction;
            controlButtons[CombatHudSettingsRows.CoffeeLearningDisconnect].interactable = !rebinding && coffeeLearningConnection.CanUseDisconnectAction;
            controlButtons[CombatHudSettingsRows.CoffeeLearningCancel].interactable = !rebinding && coffeeLearningConnection.CanUseCancelAction;
        }


        private void BuildSystemActionDock(RectTransform panel)
        {
            Image dock = CreateImage("System Action Dock", panel, new Color(0.04f, 0.06f, 0.09f, 0.98f));
            Anchor(dock.rectTransform, new Vector2(0.36f, 0.095f), new Vector2(0.975f, 0.185f));
            systemActionDock = dock.gameObject;

            actionNoticeText = CreateText("Action Notice", dock.transform, 18, FontStyle.Bold, TextAnchor.MiddleLeft, Accent);
            Anchor(actionNoticeText.rectTransform, new Vector2(0.02f, 0.58f), new Vector2(0.98f, 0.95f));
            actionNoticeText.text = "セーブとCoffeeLearningは下の大きなボタンから操作します。";

            string[] labels = { "セーブする", "書き出す", "取り込む", "接続", "コード貼付" };
            Action[] commands =
            {
                () => SaveRequested?.Invoke(),
                () => ExportProfileRequested?.Invoke(),
                () => ImportProfileRequested?.Invoke(),
                () => CoffeeLearningPrimaryRequested?.Invoke(),
                () => PasteConnectionRequested?.Invoke()
            };
            for (int index = 0; index < labels.Length; index++)
            {
                int captured = index;
                Button button = CreateButton($"Dock {labels[index]}", dock.transform, labels[index], 20, commands[captured]);
                float left = 0.015f + index * 0.195f;
                Anchor(button.GetComponent<RectTransform>(), new Vector2(left, 0.08f), new Vector2(left + 0.185f, 0.52f));
            }

            systemActionDock.SetActive(false);
        }
    }
}
