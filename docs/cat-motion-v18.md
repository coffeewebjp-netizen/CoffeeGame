# 猫少女の動作・時間停止修正 V18

ORC-20260909-001 / IN02。V17を実際に操作したOwnerの指摘に基づく修正。画面と操作感の最終判断はOwnerの再確認を待つ。

## 変更

- 時間停止中に移動Y入力を反転させていた `PartyRuntime.HumanCommands` の処理を削除。ジャンプ＋下判定に渡す方向も通常時と共通。ロック表示のY反転も除去。
- 背景を滑らかに上下反転し、背景だけを完全な白黒へ。人物・人物に属する魔法・HUDの向きと色を維持。10秒の選択的停止と終了時の復帰は継続。
- Meshyの猫資産 `01a07530-58ac-74f6-ae1a-1038e806a3aa` に「前に傾いてスプリント」「ジャンプダッシュ」「通常のジャンプ」を追加して比較。「前に傾いてスプリント」「通常のジャンプ」を採用。既存プリセットの使用で、新規契約や有料追加契約なし。
- Blenderでスキンなしの骨格FBXと基準姿勢を出力し、Unityで既存24本骨格へ回転差分を適用。体型・スキン・装備は変更しない。走りに追加前傾と接地補正を施し、足・腰・腕を同じモーションから動かす。ループ端を合わせ、平面方向のルート移動を除去。
- 通常ジャンプ素材を離陸・空中・着地に切り分け、空中の高さはゲームの物理処理へ任せる。膝の引き上げと着地の圧縮を保持。素材そのままの両手を上げる姿勢は避け、スプリントから抽出した腕の前後構えを空中へ90%ブレンド。V17の `Run/Jump/Fall/Land` の4クリップを更新し、他の属性攻撃・V14は保持。
- 効果音は `Resources/Audio/Combat/<CombatSound名>` を優先する納品素材用の差し替え口を追加。新規音素材は未採用。[制作依頼仕様](audio-sfx-v18-brief.md)を別作業に渡せる。専用音素材ではピッチを変更しない。

## 再生成

Blender 4.5用 `tools/blender/prepare_cat_locomotion_v18.py -- sprint.glb jump.glb output_dir`。ソースのハッシュとフレーム解析は `art/3d/trials/meshy-rival/motion-v18/source/motion-source.json`。採用した骨格FBXは `Assets/CoffeeGame/Editor/MotionSources/CatV18` にあり、ゲームには焼き付け済みの4クリップのみ入る。

Unity `CoffeeGame.Editor.CatMeshyLocomotionSetup.Configure` で4クリップ再作成。`SilverCatMotionSetup.ConfigureElements` でも最後に同処理を実行し、旧手順で再生成しても今回の動きが失われない。

## 検証・反映

- Unity Domain/Input/Runtime回帰 354/354。最終ジャンプ腕修正後のモーション検査 5/5。背景用シェーダーの画素検査で「背景だけ白黒・反転、人物の色と位置を保持」を確認。
- 最終Windows実行 50/50、通常起動先でも 50/50、通常起動先のスマホ入力 22/22。入力リーダー→PartyRuntime→Motor/Combatを通し、停止中の4方向とカメラ前方への実移動を確認。自動入力試験ではタッチUIによる仮入力の上書きだけ停止し、物理タッチUIは別の22項目で確認。
- 2026-09-09 03:12:58 JST、通常起動先 `Builds/Windows/CoffeeGAME.exe` へ反映。新297ファイルをハッシュ確認。元348ファイル620,564,541 bytesと旧V17/V14モデル・アセット・過去のバックアップを保持。キャラクター選択4を変更せず、クラウド／ローカルの実セーブは全検証でバイト一致。
- ゲームを終了して通常のSteam起動から再起動すれば今回の版になる。Steam UI自体のボタン操作は今回は未実施。復元は `tools/restore-pre-cat-motion-v18-player.cmd`。PowerShell版は引数なしで照合のみ、`-Restore`で今回の更新前へ戻す。348旧ファイル／297導入ファイルの事前照合済み。さらに古いV17以前へ戻す場合は、まず今回の復元を実行してから旧V17復元ツールを使う。
- Android: `Builds/Android-CatMotionV18/CoffeeGAME-CatMotionV18-development.apk`。ARM64、minSDK26、jp.coffeetools.coffeegame、開発署名は以前のAPKと一致。164,221,866 bytes、SHA256 `c62adf93877fa2e37b166f815eaa38ca7b96fde8dfcc57d58d5b990370cd5c7e`。端末未検出のため未インストール、実機描画・操作感・性能は未確認。
- 全実行確認は `-noaudio`。新しい効果音素材は未採用で、音の品質向上は専用素材の制作・聴感確認が次の作業。公開ソースへのpushは保留、今回分のみローカルコミット。

確認画像: [走り](../art/3d/trials/meshy-rival/motion-v18/previews/03-run.png)、[ジャンプ](../art/3d/trials/meshy-rival/motion-v18/previews/04-jump.png)、[空中](../art/3d/trials/meshy-rival/motion-v18/previews/05-fall.png)、[背景のみ反転](../art/3d/trials/meshy-rival/motion-v18/previews/15-time-stop.png)。

作業中の失敗も保存: 入力方式選択のテスト準備とEditModeのSendMessage制約を修正して再実行。最初の354件通過後、画像でジャンプの両腕上げを認めたため姿勢を再調整。途中の成功をOwnerの見た目承認として扱わない。
