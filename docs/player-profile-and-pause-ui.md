# プレイヤープロフィール／ポーズUI契約

更新日: 2026-08-11

## 永続化するデータ

ローカルプロフィールは `Application.persistentDataPath/CoffeeGAME/player-profile.json` にversion付きJSONとして保存する。保存時は同じディレクトリの一時ファイルへ書き出してflushし、置換する。読み込み不能なファイルは `.invalid-<UTC timestamp>` へ退避して初期プロフィールを返す。

保存対象はLevel、現在Level内のEXP、Gold、Slime Jelly、受取済み報酬ID、クラス／才能の安定IDと表示名、能力値、成長端数である。Gamepad／Steam Desktopのボタン割当は既存どおり `GameInputReader` のPlayerPrefsへ分離して保存する。

## 能力値

能力値は固定フィールドではなく、安定IDと整数値の組として保持する。現在の標準IDは次の5つ。

- `strength`: 力（攻撃力）
- `agility`: 素早さ（回避率、移動速度）
- `technique`: 技（クリティカル率、居合斬りの溜め速度）
- `luck`: 運（クリティカル率、回避率）
- `vitality`: 体力（最大スタミナ、防御補正）

標準値はすべて10。カタログへ将来の標準能力を追加すると、古いプロフィールにはその既定値が補われる。未知IDも読み捨てず再保存するため、新旧build間の往復で将来のパラメータを失わない。

戦闘補正は保存値から `PlayerDerivedStatCalculator` が算出する。保存形式に計算済み倍率を含めない。標準値10では全倍率が1、クリティカル率と回避率が0になり、従来の戦闘調整を維持する。

## 才能別のLevel成長

才能は `talentId` で成長Profileを選ぶ。各Profileは能力IDごとの1 Level当たり成長量を1/1000ポイント単位で定義し、割り切れない分をプロフィールの成長端数として保持する。例えば1500なら、最初のLevelで+1／端数500、次のLevelで+2／端数0となる。

現在の「なし」は全標準能力が1 Level当たり+1の中立Profile。新しい才能は `TalentGrowthProfile` を追加またはresolverから供給し、能力値やsave DTOを変更せず成長率だけ差し替える。

## HUDとポーズ

戦闘HUDとポーズ画面はuGUIで実行時に生成する。左上に専用顔画像とHP／ST／MP／EXP、右上にpointer／touch対応ポーズボタンを置き、safe areaと1920x1080基準のCanvas scalingを適用する。

ポーズ中は `Time.timeScale = 0`、Player移動停止、UI input map有効を維持する。Start／Esc／取消で戦闘へ戻り、左右で `ステータス` → `持ち物` → `システム` → `仲間` の4タブを移動する。ステータス／持ち物／仲間では上下入力が内容scroll、システムでは上下と決定が項目操作になる。常時見えるscrollbarをmouseでdragでき、wheel／touch dragも同じScrollRectへ届く。

- ステータス: 全身画像、クラス、才能、Level、EXP、Gold、全能力値、現在の戦闘補正
- 持ち物: 素材として現在のSlime Jellyを表示し、消耗品／装備品の将来拡張枠を分ける
- システム: 既存Gamepad／Steam Desktop割当、入力方式変更、プロフィールとボタン設定の手動保存、初期化、再割当取消
- 仲間: 未加入状態

能力一覧はプロフィール内の値を列挙し、内容高に応じてScrollRectが伸びるため、パラメータ追加で画面の固定枠を変更する必要はない。持ち物の装備効果、仲間編成、才能獲得方法、クラウド同期はこの契約の対象外。

## 戦闘カメラ

戦闘中のカメラはPlayerを中心に水平360度回転でき、垂直方向は上下反転や地面潜りを避けるため基準角から下20度～上35度へclampする。標準入力はGamepad右StickXY、KeyboardのZ／C（水平）とV／R（下降／上昇）、右Mouse buttonを押しながらの2軸dragとする。回転入力はBattle contextだけで有効で、ポーズ中や入力方式選択中には動かさない。追従点はStageのcamera bounds内へclampし、既存のcamera相対移動、方向Sprite解決、奥行きsortを維持する。

4タブは、ポーズ面が非表示の時点で内容を構築しても実寸を計算できる共通の永続ScrollRectを使う。Statusは固定5枠ではなくプロフィールの能力Snapshotを列挙し、クラス、才能、Level、EXP、Gold、全能力、算出済み戦闘補正を表示する。Systemはコントローラー設定とセーブを明示的な見出しで分け、選択行を自動的にviewport内へscrollする。
