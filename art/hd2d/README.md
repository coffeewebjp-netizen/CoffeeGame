# CoffeeGAME HD-2D art pipeline

このフォルダーは、会話内で提示された少女の原画を基準にしたHD-2D表示用素材の正本です。Unityのゲームロジックや3D当たり判定とは分離し、見た目だけを個別PNGへ差し替えます。

## 採用素材

- `reference/heroine-turnaround-v1.png`: 正面・右・背面・3/4の設定画
- `sheets/heroine-locomotion-v1.png`: 歩行・走行の生成元
- `sheets/heroine-actions-v1.png`: 通常斬り・空中斬り・急降下・回転斬り・氷魔法の生成元
- `sheets/heroine-states-v1.png`: ジャンプ・落下・着地・溜め・被弾・敗北の生成元
- `sheets/slime-actions-v1.png`: スライム6状態の生成元
- `frames/hero/*.png`: Unityが読む768x768の個別Heroフレーム
- `frames/slime/*.png`: Unityが読む512x512の個別Slimeフレーム
- `previews/*.png`: 全フレームを目視確認する連絡表

`*-keyed.png` はクロマキー付きの生成原本です。削除・上書きせず、透明版の再生成元として残します。

## 生成方法

画像はCodex内蔵の画像生成機能で作成しました。外部画像APIや手書きの3Dレンダーは使用していません。原画の識別要素を固定し、次の5プロンプト群に分けています。

1. Turnaround: 水色のボブ、琥珀眼、深紅の広袖羽織、白い上衣、杏色プリーツ、黒帯、刀と鞘を維持した正面・右・背面・3/4。
2. Locomotion: 同じ衣装・頭身で、納刀した歩行／走行を正面・右・背面から作成。
3. Actions: 正面斬り、右向き斬り、空中なで斬り、急降下、回転斬り、氷魔法。
4. States: ジャンプ、落下、着地、回転斬り溜め、被弾、敗北。
5. Slime: 淡いシアンのゲル、琥珀眼、口なしで、待機・潰れ・跳躍・攻撃・被弾・敗北。

Heroは緑、Slimeはマゼンタの単色背景で生成し、`remove_chroma_key.py`で透明化しました。個別化時に隣セルの混入を最大連結成分で除き、Slimeのマゼンタspillはcyan/navyへ補正しています。

生成原本は次のディレクトリにも保存されています。

`C:\Users\coffe\.codex\generated_images\019fdf0b-3835-7372-9e96-6093e8e221f2`

## 個別フレームの再生成

PowerShellで次を実行します。

```powershell
& .\tools\hd2d\export_hd2d_frames.ps1
& .\tools\hd2d\validate_hd2d_assets.ps1
```

この処理は以下を決定的に行います。

- 生成元から主要キャラクターだけを抽出
- Heroを768x768、Slimeを512x512へ配置
- 足元基準を揃える
- 小さな隣セル断片を除去
- Slimeのマゼンタspillを除去
- `art/hd2d/frames` とUnity Resourcesへ同じPNGを出力
- `art/hd2d/previews`の確認画像を更新

検証処理はUnityを起動せず、Hero 21枚／Slime 6枚の件数、寸法、Unity側とのSHA-256一致、manifest必須action、参照先PNGの存在を確認します。

Unity側の対応先は次です。

- `Assets/CoffeeGame/Resources/Art/HD2D/Hero/Frames`
- `Assets/CoffeeGame/Resources/Art/HD2D/Slime/Frames`
- `Assets/CoffeeGame/Resources/Art/HD2D/hero-hd2d.json`
- `Assets/CoffeeGame/Resources/Art/HD2D/slime-hd2d.json`

## 表示の境界

攻撃成立、ダメージ、ジャンプ物理、報酬はSpriteのコマやAnimation Eventから発生させません。HD-2Dは既存の決定論的な戦闘状態を表示するだけです。将来3Dへ戻す場合も、`ICharacterVisual`の実装を差し替え、ゲームルールは維持します。
