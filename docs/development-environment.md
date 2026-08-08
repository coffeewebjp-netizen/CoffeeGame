# Development Environment

## PC概要

確認日: 2026-08-08

- OS: Windows 11 Home 64-bit
- CPU: Intel Core i7-13700HX
- CPU構成: 16コア / 24スレッド
- メモリ: 約64GB
- GPU: NVIDIA GeForce RTX 4070 Laptop GPU / VRAM 8GB
- 内蔵GPU: Intel UHD Graphics

メモ:

- Unity 2D/2.5D、URP、PC/Android向け試作には十分余裕がある構成。
- `Win32_VideoController` ではRTX 4070 Laptop GPUのVRAMが約4GB相当に見えたが、この取得方法は4GB以上を正しく表示しないことがある。ユーザー確認では実VRAMは8GB。

## ストレージ

主要ドライブ:

- `C:` Windows / NVMe SSD 2TB / 空き約1.7TB
- `D:` データ用 / NVMe SSD 2TB / 空き約1.46TB
- `G:` 外付けまたは追加ドライブ / 約1TB
- `H:` 外付けまたは追加ドライブ / 約2TB
- `I:` Google Drive として見えているドライブ

物理ディスク:

- Lexar SSD NM790 2TB / NVMe SSD
- Hanye E30-2TBTN1 / NVMe SSD
- BUFFALO HD-PCFU3 / USB
- ST2000LM 007-1R8174 / USB HDD

判断:

- `C:` と `D:` はどちらもNVMe SSDなので速度面は問題なし。
- Cはシステム用、Dはデータ用という運用方針に合わせて、Unity Editor、Unityプロジェクト、Android SDK、素材、ビルド出力はDに置く。

## インストール済みツール

- .NET SDK: 9.0.101
- Visual Studio Community 2022: インストール済み
- Visual Studio Build Tools 2019: インストール済み
- Git: 2.43.0
- Node.js: 18.20.6
- npm: 10.8.2
- Node管理: `nvm4w` 経由
- Unity Hub: 3.18.0
- Unity Editor: 6000.5.7f1（互換ビルド検証用）
- Blender: 4.5.10 LTS

未確認または未インストール:

- Unity Editor 6000.3.21f1（制作ターゲット）
- Android SDK
- Android NDK
- OpenJDK
- adb

## 現在の配置

Unity Hub本体は小さいためCに置く。

```text
C:\Program Files\Unity Hub\
```

制作プロジェクトと生成素材は、Codexが一貫して編集・検証できるよう現在のワークスペース内に置く。

```text
C:\work\CoffeeGAME\unity\CoffeeGame\
C:\work\CoffeeGAME\art\3d\
C:\work\CoffeeGAME\tools\blender\
```

現在のリポジトリ:

```text
C:\work\CoffeeGAME
```

メモ:

- Unity制作本流も現在のワークスペースへ移行済み。
- Google Drive配下にUnityプロジェクト本体を置くのは避ける。`Library` や一時ファイルが多く、同期トラブルやパフォーマンス低下の原因になる。

## Unity導入時の候補

Unity Hubで入れるもの:

- Unity 6系の安定版
- Universal Render Pipeline対応テンプレート
- Android Build Support
- Android SDK & NDK Tools
- OpenJDK
- Windows Build Support

開発方針:

- 本命は Unity 6 + C# + URP。
- 最初はPCビルドを優先。
- Androidは早い段階で起動確認だけ行う。
- iOSは後回し。
- Webは本命ではなく、プロトタイプや共有用に限定する。

## URPメモ

URPは Universal Render Pipeline の略。

Unityでライト、影、ポストプロセス、2Dライト、発光、色味調整などを扱う描画パイプライン。PCとAndroidの両方を狙う2D/2.5DアクションRPGでは、Built-inより今後の拡張性があり、HDRPより軽く扱いやすい。

今回の企画では、暗い森、廃墟、異質な空間、斬撃の残光、発光、影、画面効果を使いたいため、URPを第一候補にする。
