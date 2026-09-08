# 猫少女の疾走・真下パンチ V19

ORC-20260909-001 / IN03。V18の胸だけの前傾では脚が真下に残るというOwnerの指摘への修正。

- 採用済みMeshy Lean Forward Sprintの全身ストライドを保持し、骨盤から28度の前傾、腰の前方移動、胸の追加10度、支持足の接地補正を焼き付け。脚も腰と一緒に傾き、胸より後方で蹴り出す。
- ジャンプ＋下は右腕を引いてから肩の真下へ打ち下ろす。高速落下に間に合うよう最初の約25msで伸ばし、猫の落下攻撃だけ切り替え時のブレンドを除去。左手は体側に引く。着地は開いた脚で深く沈み、頭も前へ向け、既存の約1秒の硬直・岩の波紋・ダメージを保持。
- 変更クリップは既存V17ライブラリのRun、Plunge、CatEarthLandだけ。元のモデル、骨の長さ、通常ジャンプ、剣の少女、V14ライブラリ、操作・戦闘ルールは保持。
- Meshyブラウザで「速く走る」「ジャンピングパンチ」を追加。追加成功表示は得たが、新規パンチの猫プレビューが空のままでGLB資産も取得できず、未確認の素材は採用していない。実際のベースはV18でBlender変換済みのMeshy Sprint。パンチは既存骨格へのIKによる修正で、新規Meshy生成と表記しない。24骨のモデルには指骨がないため、握りこぶしの指アニメーションは含まない。

再生成はUnity `CoffeeGame.Editor.SilverCatMotionSetup.ConfigureImpactV19`。全ライブラリ再生成でも同じ姿勢関数を使用する。Windows確認用は `BuildCoffeeGame.BuildCatMotionV19NoSetup`。音を出さず、実セーブと入力設定を保護して検証する。

## 検証・反映

- Unity回帰356/356。最終の打ち下ろしタイミングでモーション検査7/7を再実行。通常の骨長とループ連続性を保持。
- 最終確認用Windows版で51/51。接地前の右肩→右手方向はほぼ真下（Y=-1.00）、空中で打ち下ろしが完成。高フレームレートでは2フレームが約1msだったため、最終検査は描画後かつモーションの経過時間で判定する。途中の失敗ログは保持。
- 2026-09-09 04:21:34 JST、通常起動先 `Builds/Windows/CoffeeGAME.exe` へ297ファイルを照合して反映。元348ファイルは `.task-local-backup/ORC-20260909-001-in03-normal-player` に保存。準備中に入れ替えた5ファイルも別途保持。復元は `tools/restore-pre-cat-motion-v19-player.cmd`。PowerShell版は引数なしで照合のみ、`-Restore`で復元。旧348／新297ファイルの照合済み。
- 実キャラクター選択4と入力・表示設定を保持。実行は全て無音、実セーブを読み書きしない撮影モード。ローカルセーブはハッシュ一致。最後にGoogle Drive側の読み込みが応答待ちになったため、その処理を止め、Drive側の撮影後ハッシュ一致は未確認として記録した。Drive上のセーブを変更・置換していない。
- Android: `Builds/Android-CatMotionV19/CoffeeGAME-CatMotionV19-development.apk`。164234428 bytes、SHA256 `501e8b748930f09f691ef9d64e924d24472ff1388fce874072537a99dc1786f2`。ARM64、minSDK26、以前と同じ開発署名。端末未検出のため未インストール、実機性能は未確認。
- 四方向の姿勢資料: [走り](../art/3d/trials/meshy-rival/motion-v19/gallery/Run.png)、[打ち下ろし](../art/3d/trials/meshy-rival/motion-v19/gallery/Plunge.png)、[着地](../art/3d/trials/meshy-rival/motion-v19/gallery/CatEarthLand.png)。通常の戦闘画像に加え、詳細撮影のみ敵のRendererを一時的に隠し、撮影直後に戻す。戦闘の進行・ダメージは変えない。

通常起動先でも51/51項目を確認済み。強制キャラクター指定なしの起動で確認した。外見の最終的な好みの判断はOwnerの再確認を待つ。公開リポジトリへのpushは保留。
