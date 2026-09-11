# CoffeeGAME

2026-09-11 Steam Party V22：龍少女を通常のSteam起動先へ反映し、三人パーティーの操作対象を最上段、控えを下段に並べるUIへ更新しました。各行の「切替」またはT / RBで操作を交代でき、ステータス画面も操作中の人物へ追従します。開発版の設定画面では猫少女・龍少女の親密度と、ライバル登場クイズまでの討伐数を変更できます。369件のテストと実行29項目を確認し、旧Steam版をローカル退避済みです。[操作・設定・復元](docs/steam-party-v22.md)。

2026-09-11 龍少女 V21：三人目の操作切替、ツメ三連撃、三段階の龍門、龍の呼吸、空中ツメと踏みつけを追加しました。確認用起動は `tools/launch-dragon-girl-trial.cmd`。既存セーブを変更せず三人加入済みで試せます。Steamの通常起動先とは別です。[技・制作・確認記録](docs/dragon-girl-playable-v21.md)。

2026-09-09 猫の動作・時間停止 V18：Meshyスプリント／ジャンプの転送と、必殺技中の入力反転・人物の色の修正。最新の検証・反映先は[猫の動作V18](docs/cat-motion-v18.md)。効果音制作は[依頼仕様](docs/audio-sfx-v18-brief.md)。

CoffeeLearningでの学習を、少女剣士の成長と結び付けるアクションRPGです。最終対象はWindowsとAndroidで、Googleアカウントを介した継続プレイとCoffeeLearningとの双方向連携を目指します。

## 現在の開発方針

2026-09-09 Cat Elements V17（ORC-20260909-001）：猫少女を前傾走行・通常炎弾・周囲への落雷・空中風刃・岩の落下衝撃波へ更新。時間停止は人物を正立させ、背景のみ0.65秒で反転・復帰。347件のテストと猫の実行44項目、スマホ入力22項目を確認し、通常Windows/Steam起動先へ反映。元のモデル、V14モーション、旧ゲーム348ファイルとセーブを保持。[仕様・ビルド・復元](docs/cat-elements-v17.md)。

2026-09-07 Mobile UI V16（ORC-20260907-001）：スマホの戦闘ボタンをアイコン主体の弧状配置へ更新。通常攻撃を大きくし、防御・回避・ジャンプ・魔法を周囲に配置。必殺は実ゲージの輪とREADY、ロック中は水色で表示し、猫少女への交代でアイコンも変わる。340件のテストとWindows実行22項目、2比率4画面を確認。Android用の更新と復元は[モバイルUI V16](docs/mobile-ui-v16.md)を参照。

2026-09-07 Mobile V15（ORC-20260906-009）：既存Android版とタッチ入力を拡張。防御・回避・ジャンプ・攻撃・魔法・必殺・固定・切替を8ボタンで操作でき、指の役割を離すまで保持する。横画面のセーフエリアと複数指、ポーズ・中断復帰を整え、スワイプのカメラ回転をFPS非依存にした。回帰339件とWindows上の仮想Touchscreenによる22項目、2比率4画面を確認。ARM64開発APKを作成・検査済み。Android実機は未接続で、端末上の負荷・描画・指での操作感は未確認。旧APKと元のキャラクター・進行を保持。[APKと操作手順](docs/mobile-controls-v15.md)。

2026-09-06 Cat Motion V14：猫少女の全身モーションと遷移を見直し、通常のSteam起動先へ反映しました。呼吸する待機、走行、上昇・落下・着地、右手→左手→両手の通常魔法、大魔法と時間停止の専用動作、回転回避を使用します。321件のテストと実機28項目を確認。[変更・復元](docs/cat-motion-v14.md)。

2026-09-06 Target Lock V12：防御ジャンプの斜め45度境界を調整し、R3での敵固定・自動振り向き、ジャスト成功時の短いグレーのスロー演出を追加しました。被弾音声3本は剣の少女へ割り当てています。必殺の締め音声はV13で現行必殺技へ導入済みです。[操作と復元](docs/target-lock-v12.md)。

2026-09-06 Acrobatics V11：敵2体の同時出現、約1秒しゃがんで保持する落下突き、防御からの後方バク転と左右の側転を追加しました。停止・歩行中は低い前転、走行中は以前の回転跳びで回避します。落下突きは顔と上半身を前へ倒す姿勢へ調整しました。必殺技の音声はOwner作成ファイルの受け口を用意しています。[操作と復元手順](docs/acrobatics-v11.md)を参照してください。

2026-09-06 Party V9：猫少女の共闘・操作切替・大魔法・時間停止・実時間回復と、承認済みの主人公ボイス・戦闘BGM・効果音を実装しました。245件の関連テストと画像付き実行確認15項目が通過しています。Owner承認後、通常のSteam起動先`Builds/Windows/CoffeeGAME.exe`へ反映し、Steamの「プレイ」から引数なしで起動して赤羽織の主人公と仲間用HUDを確認済みです。元の348ファイルとセーブを退避し、新297ファイルおよび復元ツールを照合しました。[操作・回復・復元手順](docs/cat-companion-v9.md)を参照してください。

ゲームの土台はUnity 6 + URPで、移動・衝突・ジャンプ・戦闘判定は3D空間に置きます。表示は原画へ忠実なHD-2Dを優先し、Blender製3Dモデルは将来の本格3D化に備えたフォールバックとして残します。

2026-09-06、Owner承認により通常Windows版・Steam起動へ赤羽織の改良3Dモデル（保存選択値4）を仮反映しました。Steamの起動先は従来の `Builds/Windows/CoffeeGAME.exe` のままです。ジャンプの違和感は未調整で、Ownerによる総チェック前の段階です。元のHD-2D（記録値0）は `tools/launch-previous-character.cmd`、黒着物Meshy版は `tools/launch-meshy-snow-kimono-default.cmd` で切り替えられます。

IN14でSteamでは黒着物のままだったとの報告を受け、Steam自身が起動したゲームで選択を保存し直しました。一時オプションを空欄へ戻した後、Steamの「プレイ」から引数なしで再起動し、戦闘画面の赤羽織モデルを直接確認済みです。自動実行環境の設定値とEXEの直接起動だけでは、Steamへの反映確認にならないことを検証手順へ追記しています。

水色ボブ・赤羽織・白キャミソール・桃色スカートの改良版は、クリーンなMeshy本体、4K再テクスチャ、Blenderで修正した握りと刀、16動作を備えています。独立した確認用ビルドも `tools/launch-azure-maiden-clean.cmd`（既存の `launch-azure-maiden-trial.cmd` も同じ起動先）に残しています。更新前の通常版全348ファイルと表示設定3項目をローカル保存済みで、ゲーム終了後に `tools/restore-pre-azure-normal-player.cmd` を実行するとゲーム本体と表示設定を更新前へ戻せます。プレイの進行データは巻き戻しません。制作・検証・復元記録は [clean A-pose資料](art/3d/trials/azure-maiden-clean-apose/README.md) にあります。

表示の優先順は次のとおりです。

```text
HD-2D directional sprite
  -> rigged 3D model
  -> static sprite
  -> primitive fallback
```

戦闘、経験値、通貨、素材、入力、セーブ、CoffeeLearning連携は表示方式から独立しています。そのため、HD-2D版を肉付けしても将来の3D化でゲームルールを作り直す必要はありません。

## 最初の戦闘スライス

- 主人公と、加入後に共闘・操作切替できる猫少女。森のゴブリンとスライム（2体同時に出現し、倒れた枠を補充）
- 歩行、一定時間後の走行、ジャンプ
- 通常斬り、空中斬り、急降下攻撃
- 溜め付き回転斬り、溜め付き氷魔法
- スライム1体につきEXP 1、Gold 1、Slime Jelly 1
- ゴブリンは回り込み、棍棒の予備動作、振り下ろし、被弾・撃破モーションを持ち、EXP 1、Gold 1を落とします。制作・検証記録は [ゴブリンV8](docs/art/goblin-v8.md) を参照してください。
- 3体でLevel 2。クリア条件は一時撤去し、5体撃破ごとに最初のライバル登場画面へ移行
- WindowsのSteam ControllerとAndroidを想定した論理入力
- BGM、刀・風切り・魔法・戦闘効果音

## フォルダー

- `unity/CoffeeGame`: 現在の本体。Unity 6 / C# / URP
- `art/hd2d`: 原画準拠のHD-2D生成元、個別フレーム、確認画像
- `art/3d`: Blenderモデル、FBX、制作記録。将来3D化用
- `tools/hd2d`: HD-2D個別フレームの決定的な書き出し処理
- `tools/blender`: 3Dモデル生成・検証処理
- `docs`: 構想、ロードマップ、CoffeeLearning連携、戦闘仕様。入口は `docs/DESIGN_INDEX.md`
- `archive/haxslasher`: 操作感を検証した旧ブラウザ版。通常は読まない

## 重要な設計境界

- `Domain`: 経験値、報酬、調整値
- `Input`: 論理アクション、Steam Desktop fallback、再割当保存
- `Runtime/Actors`: 3D移動、ジャンプ、敵AI
- `Runtime/Combat`: 攻撃、チャージ、投射物、ダメージ
- `Runtime/Presentation`: HD-2D／3Dの交換可能な見た目
- `Runtime/Run`: 1戦のライフサイクル
- `Integration`: CoffeeLearning連携境界

攻撃判定や報酬をSpriteのAnimation Eventへ入れないでください。見た目がHD-2Dでも3Dでも、同じ戦闘結果になることが必須です。

## 現在の確認方法

HD-2D素材の一覧は次で確認できます。

- `art/hd2d/previews/hero-frames-v1.png`
- `art/hd2d/previews/hero-animation-v4.png`
- `art/hd2d/previews/hero-animation-v5.png`
- `art/hd2d/previews/slime-frames-v1.png`

Unityの起動方法と入力仕様は`unity/CoffeeGame/README.md`、画像の生成・再出力方法は`art/hd2d/README.md`を参照してください。

設計の入口は`docs/DESIGN_INDEX.md`です。実装済み範囲、検証結果、Steam Controllerの実測、未実装項目は`docs/current-status.md`に集約しています。

このPCではUnity Hubとbatch Editorを同時に起動すると`Unity.Licensing.Client.exe`が競合することがあります。Hubを閉じ、Unityプロセスを一本だけ起動する条件では、EditModeテスト145件とWindowsビルドを完走できています。
