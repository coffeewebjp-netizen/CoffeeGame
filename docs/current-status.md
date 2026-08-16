# CoffeeGAME 開発状況

更新日: 2026-08-16

この文書は、CoffeeGAMEの現在のゴール、採用済みの設計、実装済み範囲、実機確認結果、未実装項目を一か所で把握するための引継ぎ資料です。個別仕様の詳細は末尾の関連資料を参照してください。

## 現在地

CoffeeGAMEは、ブラウザ試作からUnity製Windows／Androidアプリへ本流を移行済みです。現在は、3D空間の移動・物理・戦闘判定に、原画準拠の方向別HD-2D画像を表示する構成です。

```text
3D world / collision / combat rules
                 |
        ICharacterVisual
                 |
       HD-2D（現在の本流）
                 |
      rigged 3D model fallback
```

ブラウザ版は操作感と仕様を決めた旧プロトタイプとして残します。今後の機能追加は原則として `unity/CoffeeGame` へ行います。

## 最終ゴール

- WindowsとAndroidで動くネイティブアクションRPG
- Googleアカウントに紐づくキャラクター進行のクラウド同期
- CoffeeLearningとの双方向連携
- HD-2Dでゲームを成立させる。表示の本線はHD-2D（`ORC-20260816-034-DEC01`）。3D表示への交換は将来オプションとして残すが、現状の girl1 / Grok Blender 試作では120点ルックに届かないため進めない

## 実装済みの縦スライス

### 戦闘と成長

- 3DのXZ平面を使うプレイヤー移動
- 歩行と、同方向入力を続けた後の走行
- ジャンプ、空中移動、落下、着地
- 通常斬り
- 1ジャンプ1回の空中なで斬り
- 下入力による急降下突き、着地衝撃波、着地硬直
- STを使う溜め付き360度居合斬り（暗転、太刀筋、白フラッシュに同期して1回だけ命中）
- MPを使う溜め付き氷魔法
- スライムの追跡、攻撃予兆、攻撃、被弾、撃破
- スライム1体ごとのEXP 1、Gold 1、Slime Jelly 1の一度だけの付与
- 3体撃破でLevel 2。5体クリアは撤去済み。5体撃破ごとに戦闘を止め、承認済み立ち絵のライバルが直近14日の苦手問題を1問出す。キーボード回答は編集・確認後にCoffeeLearningへ送信し、結果確認または戦闘復帰できる
- HP、MP、ST、EXP、Gold、素材のHUD
- リトライ時のProfileとRunの分離
- ID付き能力値、才能別Level成長、Level／EXP／Gold／素材／才能ポイント／ライバル親密度・仲間状態／受取済み報酬のprofile v2ローカル保存（v1読込移行あり）
- 力／素早さ／技／運／体力から、攻撃・移動・クリティカル・回避・居合速度・ST・防御を別Calculatorで導出

現在の調整数値は [first-combat-slice.md](first-combat-slice.md) にあります。

### 表示

- 最初の戦闘エリアを、反復する草地床と空・丘・遠い木立の草原背景で表示
- 草原の遠景は描画専用で、従来の9.6 x 5.4メートルの戦闘境界とジャンプ衝突は維持
- 主人公220枚（v5の歩行30／走行30／ジャンプ20を含む）、スライム6枚の個別透過原画。v5は実行時15枚のatlasへ集約
- 正面／右斜め前／横／右斜め後／背面の5 authored viewと左右反転による8方向解決
- 22.5度／67.5度境界とhysteresisによるcardinal／diagonal切替のちらつき抑制
- 768x768、540 PPU、共通pivot／足元契約でaction間・方向間の見かけscaleを統一
- 歩行6frame、走行6frame、ジャンプ4frame、通常斬り4frame、氷魔法の詠唱3frame／放出3frameを5 authored viewに実装
- 歩行は7.5fps／0.8秒cycle。下向き・右斜め前走行はWalkと頭／胴体scaleを揃え、近接camera風の拡大を除去
- 移動、ジャンプ、落下、着地、通常斬り、空中斬り、急降下、居合斬り、魔法、被弾、敗北の表示状態
- 氷魔法は安全マージン付きの詠唱姿勢から専用の放出姿勢へ遷移し、収束する氷片と飛翔光跡を表示
- 草原に非衝突の前景草・石、控えめなBloom／色調／ビネットを加え、キャラとVFXの奥行きを強化
- カメラ奥行きによるSpriteの前後ソート
- manifest不正や画像欠落時の段階的fallback
- 左上の透過顔画像付きuGUI HUD
- 透過全身画像と全能力を表示するポーズ画面（ステータス／持ち物／システム／仲間）。常時見えるscrollbar、mouse wheel／drag／touch、keyboard／controller上下scrollに対応
- 戦闘中に右Stick／右Mouse dragで水平360度・垂直clamp回転でき、KeyboardではZ／CとV／Rを使える追従カメラ
- 画面ポーズボタン、safe area、keyboard／Gamepad／pointer／touchのメニュー導線
- 右上のFPS／frame time表示と、システム画面の現状維持・Balanced・Smooth・Quality描画preset

表示優先順は次のとおりです。

```text
HD-2D directional sprite
  -> rigged 3D model
  -> static sprite
  -> primitive
```

Blender製の `heroine-v4` と `slime-v2` は、将来3Dへ戻るための基礎として保存しています。現在の通常表示には使いません。

### 音声

- BGM `Rituals_of_the_Jade_Valley.mp3`
- 刀の命中音と風切り音
- 居合斬り、氷魔法、スライム、被弾、報酬、レベルアップ等の効果音
- BGM／SFXの有効化、音量、ブラウザ試作時に確認した音声ライフサイクルの仕様

## 入力

### 起動時の入力方式

起動するたびに、次のいずれかを明示的に選びます。前回値はカーソルの初期位置にだけ使い、自動選択しません。

1. `Keyboard / Mouse`
2. `Controller / Gamepad`
3. `Steam Desktop compatibility`
4. `タッチ（画面操作）` — 横画面。左半分をスワイプして押しっぱなしで移動、右半分でカメラ、右下に跳／刀／居合／氷

選択後は一つのInput System binding groupだけを有効にし、別方式からの戦闘入力を混在させません。GamepadがUnityへ届いていないときは `Controller / Gamepad` を選択できません。モバイルでは起動時カーソルがタッチ方式になります。

### Native Gamepadの標準配置

Steamのレイアウト画面に表示される「Aボタン」等はCoffeeGAMEのアクション名ではなく、仮想Xboxコントローラーへ送るボタンです。CoffeeGAME側の標準配置は次のとおりです。

| 物理／仮想入力 | CoffeeGAMEの動作 |
| --- | --- |
| 左Stick／D-pad | 移動、メニュー選択 |
| 右Stick | 戦闘カメラを水平360度／垂直clamp回転 |
| A／South | ジャンプ、メニュー決定 |
| B／East | メニュー取消。戦闘アクションなし |
| X／West | 居合斬り |
| Y／North | 氷魔法 |
| RT | 刀。空中ではなで斬り |
| Start | ポーズ、メニュー決定 |
| View／Select | ボタン設定 |
| LB／RB | 現在未使用 |

ポーズ画面の `システム` では、Jump、Sword、Special、Magicを再割当できます。同じボタンを指定した場合は既存の動作と交換し、保存値は次回起動でも復元します。`セーブする` はプレイヤープロフィールと現在のボタン設定をまとめて保存し、結果を同じ画面へ表示します。描画presetは初期値を「現状維持」とし、Balanced（1080p/60fps）、Smooth（native/120fps）、Quality（native/60fps）を明示的に選べます。FPS表示も個別に切り替えられます。

### Steam Controllerの実測結果

2026-08-09に次を確認しました。

- CoffeeGAMEを非Steamゲームとして正しいEXEへ登録済み
- SteamのCoffeeGAME専用レイアウトとして `Gamepad with Mouse Trackpad` が選択された
- Steamライブラリから起動したCoffeeGAMEをSteamがtracked processとして認識した
- Unity Playerが `Controller (XBOX 360 For Windows)` を列挙した
- HD-2D主人公とスライムの初期化、戦闘開始まで到達した
- 入力・ゲーム実行の例外はなかった

一度、検証用の直起動版とSteam起動版が二重起動し、Steam設定が効かない方を操作できる状態になりました。検証用プロセスは終了済みです。今後はCoffeeGAMEを一つだけ起動し、Steam Controllerを使う場合は必ずSteamライブラリから開始して `Controller / Gamepad` を選びます。

`Steam Desktop compatibility` は、SteamがA／X／Y／RTをEnter／PageUp／PageDown／Mouse等へ変換している場合だけ使う退避経路です。Native Gamepadが届いている状態では選びません。

詳細な設定手順は [steam-controller-setup.md](steam-controller-setup.md) を参照してください。

## 検証済み環境と結果

### 開発環境

- Windows 11
- Unity Editor `6000.5.7f1`
- URP `17.5.0`
- Input System `1.20`
- Blender `4.5.10 LTS`

### 2026-08-11の最終確認

- HD-2D asset validator: **Hero 220 / Slime 6 / Hero atlas 15、contract passed**
- Unity EditMode tests: **145 / 145 passed**
- Windows player build: **Success（377,099,288 bytes）**（2026-08-15、日本語IMEのEnterを明示提出から分離、実API成功レスポンス修正、苦手問題typed回答、報酬・親密度・profile v2を含む）
- Windows player smoke: profileとheroine HD-2D v5の初期化例外なし。下向きRun scale／Walk 7.5fps補正を含む再buildを15秒headless起動し、`HD-2D visual initialized: heroine` を確認。slime resource／manifestはasset validatorとEditMode testsで検証
- Steam Input: 仮想Xbox 360 Gamepadとして列挙成功
- Windows実行ファイル: `unity/CoffeeGame/Builds/Windows/CoffeeGAME.exe`

Unity Hubとbatch Editorを同時に起動すると、異なるUnity Licensing Clientが競合して例外ダイアログが出ることがあります。自動テストやビルドはHubを閉じ、Unityプロセスを一本だけ起動して実行します。この条件ではテストとWindowsビルドを完走できています。

## CoffeeLearning連携

contract v1の実HTTP bridge、WindowsのDPAPI資格情報、ブラウザ接続設定、アカウント表示、苦手問題の取得・出題・typed回答確認・送信・pending結果回復、AI判定feedback表示、正解時のGold／EXP／才能ポイント／親密度報酬と仲間化・profile v2永続化まで実装済みです。CoffeeLearning側の正解は通常OKと同じWordの`okCount`加算を結果IDで一度だけ行います。音声入力は未実装です。AndroidはKeystoreでtokenを保存し、Windowsと同じブラウザ往復（端末内の 127.0.0.1 コールバック）でCoffeeLearning接続できます。セーブは端末ローカルのままなので、システム画面の書き出し／取り込みで JSON を Google Drive などに渡せます。自動クラウド同期は未接続です。

### CoffeeGAMEからCoffeeLearning

- 対象は単語系デッキだけ
- 英単語デッキと汎用単語・カードデッキを含む
- ゲームで正式に弱点語をクリアしたとき、CoffeeLearningを `ok` にする
- 同じ操作でCoffeeGAME側にもポイント受取証を反映する
- 英語日記と抽象思考のゲーム内弱点補強は現在対象外
- 将来providerを追加できるcapability導線は残す

### CoffeeLearningからCoffeeGAME

- すべての学習デッキを対象にする
- 単語系は正規の `ok` 成立時のポイントを1倍
- 英語日記と抽象思考は、その日に新規生成・保存された正規評価点を100倍
- 累計値を毎回配らず、日本時間の当日に新しく成立した未受取差分だけを渡す

### 実装前に必要なこと

- server-ownedな `deckKind` とcapability resolver
- 不変で冪等な学習ポイント獲得イベント
- client申告点を信用しないserver-side評価証跡
- 日本時間の日次集約とclaim ledger
- 複数端末から同時受取しても二重付与しない `claimId`
- GoogleアカウントとCoffeeGAME専用tokenの認証導線

詳細は [coffeelearning-integration.md](coffeelearning-integration.md) を参照してください。

## 未実装または要調整

### 優先度が高いもの

- Steam Controllerで最終ボタン配置を試遊し、A／RT／X／Yの役割を確定する
- 新しい6／4／3frame animationの遷移速度、斬撃hit timing、魔法effect量を実機試遊で追加調整する
- 斬撃、居合斬り、氷魔法、被弾の手触りを実プレイで調整する
- 回避アクションをUnity版へ追加する
- Android Build Support、SDK、NDK、OpenJDKを導入して実機APKを作る
- Android横画面のタッチ操作と復帰時の音声／入力を確認する

### 縦スライス拡張

- タイトル画面と正式な開始導線
- 敵3種類と小ボス
- ステージ1つ
- 装備、ドロップテーブル、クラフトまたは素材の用途
- ダメージ表示、ヒットストップ、カメラ揺れ、VFXの仕上げ
- Production Buildと配布パッケージ

### アカウントと連携

- Googleアカウント連携とWindows／Android間のクラウド同期
- CoffeeLearningのmock bridge
- CoffeeLearning専用API、認証、日次ポイント受取

## 次の推奨順序

1. Steam Controllerの標準配置を確定し、2～3分の操作レビューを行う
2. HD-2Dと戦闘フィードバックを、同じ縦スライスの範囲で調整する
3. Android toolchainを導入し、早期に開発APKを実機確認する
4. 回避、敵追加、ドロップ用途を加えて縦スライスを完成させる
5. ローカルProfileをGoogleアカウント同期へ接続する境界を実装する
6. CoffeeLearning mockから始め、確定済みの台帳契約へ接続する

## 主要ファイル

- [README.md](../README.md): プロジェクト入口
- [concept.md](concept.md): 企画、世界観、制作判断
- [roadmap.md](roadmap.md): 実装順
- [first-combat-slice.md](first-combat-slice.md): 戦闘仕様と調整数値
- [steam-controller-setup.md](steam-controller-setup.md): Steam Controller設定
- [player-profile-and-pause-ui.md](player-profile-and-pause-ui.md): 能力値、才能成長、ローカルsave、HUD／ポーズUI契約
- [coffeelearning-integration.md](coffeelearning-integration.md): CoffeeLearning連携契約
- [3d-character-pipeline.md](3d-character-pipeline.md): 将来3D化の資産契約
- [../art/hd2d/README.md](../art/hd2d/README.md): HD-2D素材生成・検証
- [../unity/CoffeeGame/README.md](../unity/CoffeeGame/README.md): Unityプロジェクトの詳細
