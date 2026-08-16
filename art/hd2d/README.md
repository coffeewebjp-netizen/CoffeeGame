# CoffeeGAME HD-2D art pipeline

このフォルダーは、会話内で提示された少女の原画を基準にしたHD-2D表示用素材の正本です。Unityのゲームロジックや3D当たり判定とは分離し、見た目だけを個別PNGへ差し替えます。

## 採用素材

- `reference/heroine-turnaround-v1.png`: 正面・右・背面・3/4の設定画
- `sheets/heroine-locomotion-v1.png`: 歩行・走行の生成元
- `sheets/heroine-actions-v1.png`: 通常斬り・空中斬り・急降下・回転斬り・氷魔法の生成元
- `sheets/heroine-states-v1.png`: ジャンプ・落下・着地・溜め・被弾・敗北の生成元
- `sheets/heroine-locomotion-{down,right,up,down_right,up_right}-v2.png`: 5方向×歩行4／走行4フレームの透明生成元
- `sheets/heroine-sword-{down,right,up,down_right,up_right}-v2.png`: 5方向×抜刀斬り4フレームの透明生成元
- `sheets/heroine-magic-{down,right,up,down_right,up_right}-v2.png`: 5方向×詠唱3／放出3フレームの透明生成元
- `sheets/heroine-walk-{down,right,up,down_right,up_right}-v3.png`: 5方向×歩行6フレームの透明生成元
- `sheets/heroine-run-{down,right,up,down_right,up_right}-v3.png`: 5方向×走行6フレームの透明生成元
- `sheets/heroine-run-{down,down_right}-v4.png`: frontal Runの遠近拡大を除き、Walkと頭／胴体scaleを合わせた補正版
- `sheets/heroine-jump-{down,right,up,down_right,up_right}-v2.png`: 5方向×ジャンプ4フレームの透明生成元
- `sheets/slime-actions-v1.png`: スライム6状態の生成元
- `frames/hero/*.png`: Unityが読む768x768の個別Heroフレーム
- `atlases/hero/*.png`: v5の歩行／走行／ジャンプをまとめた15枚の実行用atlas
- `frames/slime/*.png`: Unityが読む512x512の個別Slimeフレーム
- `previews/*.png`: 全フレームを目視確認する連絡表

`*-keyed.png` はクロマキー付きの生成原本です。削除・上書きせず、透明版の再生成元として残します。

## 生成方法

画像はCodex内蔵の画像生成機能で作成しました。外部画像APIや手書きの3Dレンダーは使用していません。原画の識別要素を固定し、基礎素材は次の5プロンプト群に分けています。

1. Turnaround: 水色のボブ、琥珀眼、深紅の広袖羽織、白い上衣、杏色プリーツ、黒帯、刀と鞘を維持した正面・右・背面・3/4。
2. Locomotion: 同じ衣装・頭身で、納刀した歩行／走行を正面・右・背面から作成。
3. Actions: 正面斬り、右向き斬り、空中なで斬り、急降下、回転斬り、氷魔法。
4. States: ジャンプ、落下、着地、回転斬り溜め、被弾、敗北。
5. Slime: 淡いシアンのゲル、琥珀眼、口なしで、待機・潰れ・跳躍・攻撃・被弾・敗北。

Heroは緑、Slimeはマゼンタの単色背景で生成し、`remove_chroma_key.py`で透明化しました。個別化時に隣セルの混入を最大連結成分で除き、Slimeのマゼンタspillはcyan/navyへ補正しています。

### Hero animation v4の最終プロンプト契約

v4は `heroine-turnaround-v1.png` と既存sheetを参照する15回のprecise sprite-sheet editです。各プロンプトで共通して、同一人物・水色bob／ahoge・橙眼・赤haori・白top・橙pleated skirt・黒sash／gloves・dark boots・刀と鞘を固定し、camera、body/head scale、ground lineを変えないよう指定しました。背景指定の原文は `perfectly flat solid #00ff00 chroma-key background; no transparency, shadow, floor, gradient, texture, divider, labels, UI, border, logo, or watermark; do not use #00ff00 on the subject` です。

- Locomotion最終プロンプト: `strict 4 columns by 2 rows`。上段を `four-frame WALK loop: contact, passing, opposite contact, passing`、下段を `four-frame RUN loop: contact, flight, opposite contact, flight/recovery` とし、smooth loop、alternating feet、opposing arms、hair／haori／skirt follow-through、fully sheathed katana、equal isolated cellsを指定。
- Sword最終プロンプト: `strict 2x2 equal-cell sheet, read left-to-right then top-to-bottom`。`wind-up / early draw slash / impact-contact / follow-through-recovery toward guard-resheath` の4連続姿勢、exactly one blade and one scabbard、complete body／blade inside each cellを指定。
- Magic最終プロンプト: `strict 3 columns x 2 rows`。上段を `tiny cyan spark / growing compact cyan-white orb / fully charged faceted ice crystal`、下段を `braced anticipation / palm-thrust launch linked by a short glow / follow-through with nearby dissipating shards` とし、刀は全frameで納刀、effectもcell内に収めるよう指定。
- 3群それぞれを `Down/front`、`Right/profile`、`Up/rear`、`DownRight/front-right 45-degree`、`UpRight/rear-right 45-degree` で生成。左側3方向はruntimeの左右反転で解決し、合計8方向にする。

方向ごとの原文では、Downを `directly toward the viewer`、Rightを `directly to screen-right in clean full-body right profile`、Upを `directly away from the viewer in true full-body rear view`、DownRightを `exact 45-degree front-right three-quarter gameplay direction`、UpRightを `exact 45-degree rear-right three-quarter gameplay direction` と固定しています。

### Hero locomotion / jump v5の最終プロンプト契約

v5は同じturnaroundとv4素材を参照し、歩行・走行・ジャンプをそれぞれ5 authored viewで生成した15回のsprite-sheet editです。共通指定はv4の人物・衣装・刀・camera・head/body scale・ground lineを維持し、`perfectly flat solid #00ff00 chroma-key background`、影・床・罫線・文字・透過なしとしました。

- Walk最終プロンプト: `strict 3 columns by 2 rows, read left-to-right then top-to-bottom`。6コマを `left contact / settle / passing / right contact / settle / passing` とし、膝・足首・boot silhouetteを明確に変え、左右の接地と腕振りを交互にしながらroot位置と足元を安定させる。
- Run最終プロンプト: `strict 3 columns by 2 rows`。6コマを `left strike / compression / airborne / right strike / compression / airborne` とし、大きなstride、膝の持ち上げ、両足が離れるflight phase、衣装と髪のfollow-throughを入れる。
- Jump最終プロンプト: `strict 2 columns by 2 rows`。4コマを `takeoff / ascent / apex tuck / early descent` とし、すべてのコマを空中姿勢にする。DownRightとUpRightも横向き素材の流用ではなく、45度の専用姿勢として生成する。
- 5 viewと左右反転の対応はv4と同じで、runtimeでは正面・右斜め前・右・右斜め後・背面・左斜め後・左・左斜め前の8方向をカバーする。

下向き／右斜め前Runは実機確認後にv4 source editを追加しました。既存6phaseを維持しつつ、対応するWalk sheetをpixel-scale基準にして、頭髪幅・胴体・手足の太さ・camera distanceを全cellで固定し、close-camera foreshorteningを禁止しています。さらにRunは外接矩形の高さを一律680pxへ拡大せず、Down `1.00`、DownRight `0.84`、Right `0.72`、UpRight `0.81`、Up `0.87` のanatomical scale multiplierを適用します。これにより前傾・膝曲げ姿勢は自然に低くなり、頭／胴体scaleはWalkへ揃います。Walkのruntime再生は11.25fpsから7.5fpsへ下げ、6frame cycleを約0.53秒から0.8秒へ延ばしました。

生成原本は次のディレクトリにも保存されています。

`C:\Users\coffe\.codex\generated_images\019fdf0b-3835-7372-9e96-6093e8e221f2`

Hero animation v4/v5の生成原本は次です。

`C:\Users\coffe\.codex\generated_images\019fef71-5f83-7550-8976-6ef916fe2a2f`

## 個別フレームの再生成

PowerShellで次を実行します。

```powershell
& .\tools\hd2d\export_hd2d_frames.ps1
& .\tools\hd2d\validate_hd2d_assets.ps1
```

この処理は以下を決定的に行います。

- 生成元から主要キャラクターだけを抽出
- Heroを768x768、Slimeを512x512へ配置
- Hero v4/v5はbody content height 680px、左右safe area 16px、足元Y=720を基準に配置
- 小さな隣セル断片を除去
- Slimeのマゼンタspillを除去
- `art/hd2d/frames` とUnity Resourcesへ同じPNGを出力
- v5の80フレームを15枚のmulti-row atlasへpackし、Unity Resourcesには個別80 textureを複製しない
- `art/hd2d/previews`の確認画像を更新

検証処理はUnityを起動せず、Hero 220枚（v5の歩行30／走行30／ジャンプ20を含む）／Slime 6枚／Hero atlas 15枚の件数、寸法、透明alpha、安全余白、足元、Unity側とのSHA-256一致、manifest必須action、5方向strip、frame数、atlas grid、参照先PNGの存在を確認します。v4/v5は全frame 768x768、manifest全体で540 PPU／pivot Y 0.0625を共有し、actionや方向ごとのPPU補正を持ちません。

Unity側の対応先は次です。

- `Assets/CoffeeGame/Resources/Art/HD2D/Hero/Frames`
- `Assets/CoffeeGame/Resources/Art/HD2D/Hero/Atlases`
- `Assets/CoffeeGame/Resources/Art/HD2D/Slime/Frames`
- `Assets/CoffeeGame/Resources/Art/HD2D/hero-hd2d.json`
- `Assets/CoffeeGame/Resources/Art/HD2D/slime-hd2d.json`

## 表示の境界

攻撃成立、ダメージ、ジャンプ物理、報酬はSpriteのコマやAnimation Eventから発生させません。HD-2Dは既存の決定論的な戦闘状態を表示するだけです。将来3Dへ戻す場合も、`ICharacterVisual`の実装を差し替え、ゲームルールは維持します。
