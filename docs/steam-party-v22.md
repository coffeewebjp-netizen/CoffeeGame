# Steam Party V22

ORC-20260911-002。龍少女V21を通常のSteam起動先 `unity/CoffeeGame/Builds/Windows/CoffeeGAME.exe` へ反映し、三人編成の表示・交代・設定を整理した開発版です。既存のセーブ形式と加入記録を維持し、更新前のWindowsプレイヤー297ファイルを `.task-local-backup/ORC-20260911-002-normal-player/previous` に退避しています。

## パーティー操作

- 主人公、猫少女、龍少女を最大三人まで同時に戦闘へ参加させられます。
- パーティー欄は操作中の人物を最上段に表示し、控えをその下へ並べます。
- 各控え行の「切替」を選ぶと、その人物を直接操作できます。現在の操作対象は「操作中」と表示します。
- キーボードのT、コントローラーのRB、スマホの「切替」は参加中の人物を順番に交代します。設定 > システム > 操作設定から割り当てを変更できます。
- ステータス画面の名前、能力値、レベル、経験値、立ち絵は操作中の人物へ追従します。
- 休息へ移した人物は戦闘画面から外れ、既存仕様どおり実時間でHP・MPを回復します。

## 開発版の設定

戦闘中にメニューを開き、設定 > システムを下へスクロールします。

- 「ライバル登場クイズまで」：1体減らす、1体増やす、初期値へ戻す。範囲は1～50体、初期値は5体です。変更は端末へ保存し、次の討伐判定から使用します。
- 「猫少女の親密度」：-10、+10、0%、100%。
- 「龍少女の親密度」：-10、+10、0%、100%。100%にすると加入し、加入後に数値を下げても仲間として残ります。

親密度の直接操作はDevelopment Buildだけに表示します。今回Steamへ入れたV22は総チェック用のDevelopment Buildです。ライバル登場間隔の設定は通常ビルドにも残ります。

## Steam起動と確認

SteamライブラリのCoffeeGAMEは従来と同じ実行先を参照します。2026-09-11に `steam://rungameid/11848876491142070272` から起動し、Steamと `Builds/Windows/CoffeeGAME.exe` のゲームプロセスを確認しました。以前の依頼に合わせ、現在のSteamショートカットには `-noaudio` を設定しています。

- EditMode：369 / 369成功。設定の保存・既定値・上限下限、操作中の龍少女が最上段になること、直接切替ボタン、ステータスと立ち絵の追従を含みます。結果は `unity/CoffeeGame/Builds/steam-party-v22-tests-final.xml`。
- Windows実行：29 / 29成功。V21の戦闘23項目に加え、パーティー順、操作中表示、龍少女ステータス、立ち絵、討伐数設定、龍少女親密度UIを確認しました。結果は `unity/CoffeeGame/Builds/steam-party-v22-final/report.json`。
- ビルド：`unity/CoffeeGame/Builds/steam-party-v22-build-final.log` に `Build Finished, Result: Success.`。Steam配置後の297ファイルを候補版とSHA-256で照合しました。

## 元へ戻す

CoffeeGAMEを終了してから `tools/restore-pre-steam-party-v22-player.cmd` を実行します。通常のSteam実行先を更新前の297ファイルへ戻し、V22は `retained-v22` に移します。セーブ、親密度、入力設定、表示設定は巻き戻しません。

Steamの無音指定だけを元へ戻す場合は、Steamを終了してから `.task-local-backup/ORC-20260911-002-normal-player/set-steam-noaudio.ps1 -Mode Restore` を実行します。これはタスク開始時の `shortcuts.vdf` を復元します。
