# CoffeeGAME

CoffeeLearningでの学習を、少女剣士の成長と結び付けるアクションRPGです。最終対象はWindowsとAndroidで、Googleアカウントを介した継続プレイとCoffeeLearningとの双方向連携を目指します。

## 現在の開発方針

ゲームの土台はUnity 6 + URPで、移動・衝突・ジャンプ・戦闘判定は3D空間に置きます。表示は原画へ忠実なHD-2Dを優先し、Blender製3Dモデルは将来の本格3D化に備えたフォールバックとして残します。

表示の優先順は次のとおりです。

```text
HD-2D directional sprite
  -> rigged 3D model
  -> static sprite
  -> primitive fallback
```

戦闘、経験値、通貨、素材、入力、セーブ、CoffeeLearning連携は表示方式から独立しています。そのため、HD-2D版を肉付けしても将来の3D化でゲームルールを作り直す必要はありません。

## 最初の戦闘スライス

- 主人公1人とスライム1種類
- 歩行、一定時間後の走行、ジャンプ
- 通常斬り、空中斬り、急降下攻撃
- 溜め付き回転斬り、溜め付き氷魔法
- スライム1体につきEXP 1、Gold 1、Slime Jelly 1
- 3体でLevel 2、5体でクリア
- WindowsのSteam ControllerとAndroidを想定した論理入力
- BGM、刀・風切り・魔法・戦闘効果音

## フォルダー

- `unity/CoffeeGame`: 現在の本体。Unity 6 / C# / URP
- `art/hd2d`: 原画準拠のHD-2D生成元、個別フレーム、確認画像
- `art/3d`: Blenderモデル、FBX、制作記録。将来3D化用
- `tools/hd2d`: HD-2D個別フレームの決定的な書き出し処理
- `tools/blender`: 3Dモデル生成・検証処理
- `haxslasher`: 操作感を検証した旧ブラウザ版
- `docs`: 構想、ロードマップ、CoffeeLearning連携、戦闘仕様

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
- `art/hd2d/previews/slime-frames-v1.png`

Unityの起動方法と入力仕様は`unity/CoffeeGame/README.md`、画像の生成・再出力方法は`art/hd2d/README.md`を参照してください。

現在の実装済み範囲、検証結果、Steam Controllerの実測、未実装項目は`docs/current-status.md`に集約しています。

このPCではUnity Hubとbatch Editorを同時に起動すると`Unity.Licensing.Client.exe`が競合することがあります。Hubを閉じ、Unityプロセスを一本だけ起動する条件では、EditModeテスト75件とWindowsビルドを完走できています。
