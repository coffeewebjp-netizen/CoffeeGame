# Design Index

CoffeeGAME の設計情報を読むための入口です。
毎回すべての docs を読まず、触る面だけに進みます。

## 読む順番

1. `docs/DESIGN_INDEX.md`
2. `docs/current-status.md`
3. 触る機能の `docs/*.md` または `unity/CoffeeGame/README.md`
4. 関連する C# だけ

Do not search or read `archive/` unless the task is historical archaeology.
The live game is `unity/CoffeeGame`.

| 作業 | 先に読む文書 | 主な実装入口 |
| --- | --- | --- |
| 猫少女の専用モーションと遷移 | `docs/cat-motion-v14.md` | `SilverCatMotionSetup`, `ModelCharacterVisual`, `CatMotionEvidenceCapture` |
| いまの実装範囲 | `docs/current-status.md` | `unity/CoffeeGame/Assets/CoffeeGame/Scripts/` |
| Unity 起動と入力 | `unity/CoffeeGame/README.md` | `Scripts/Input/GameInputReader.cs` と `GameInputReader.{Keyboard,Gamepad,SteamDesktop,Touch}.cs` |
| 戦闘スライス | `docs/first-combat-slice.md` | `Scripts/Runtime/Combat/`, `Scripts/Runtime/Bootstrap/` |
| 敵固定・ジャストスロー・剣士音声・親密度デバッグ | `docs/combat-polish-v13.md`, `docs/target-lock-v12.md` | `TargetLockController`, `PlayerDefense`, `PerfectDefenseMoment`, `SpecialCombatVoice`, `CombatGameHudView.Settings` |
| 防御・回避・バク転・2体出現 | `docs/acrobatics-v11.md`, `docs/defense-v10.md` | `PlayerDefense`, `PlayerMotor3D`, `AcrobaticMotionPresentation`, `CombatRunController` |
| Android / タッチ | `docs/mobile-controls-v15.md` | `TouchGestureRouter`, `TouchControlLayout`, `OnScreenTouchControls`, `CameraOrbitInputDriver` |
| HUD / ポーズ | `docs/player-profile-and-pause-ui.md` | `CombatSliceHud`（制御）と `CombatGameHudView`（表示）。設定行は `CombatHudSettingsRows` |
| CoffeeLearning 連携 | `docs/coffeelearning-integration.md` | `Scripts/Integration/`。HUD 接続面は `CombatSliceHud.CoffeeLearning.cs` / `CombatGameHudView.Settings.cs`。ライバル出題は `*.Rival*.cs` |
| HD-2D 素材 | `art/hd2d/README.md` | `Resources/Art/HD2D/` |
| 旧ブラウザ試作 | `archive/haxslasher/README.md` | 現行コードではない |

## 文書の役割

- `README.md` は短い方針とフォルダ案内だけにする。
- `docs/current-status.md` は実装済み範囲と未実装の引継ぎにする。
- `archive/` は現行ゲームではない。
