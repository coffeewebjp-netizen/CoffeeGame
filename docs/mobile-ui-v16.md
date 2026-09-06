# Android戦闘UI V16

2026-09-07 / ORC-20260907-001。現行モデルと戦闘操作を維持して、タッチ操作の見た目と配置を更新する。

## 配置

右下の大きな刀が通常攻撃。左下の二重矢印が回避、右上の上矢印がジャンプ。内側へ防御の盾・魔法の雪結晶・必殺の星を弧状に置く。上段の照準はロックオン、循環矢印は仲間への切替。猫少女では通常攻撃が魔法弾、魔法が星、必殺が時計へ変わる。短い日本語ラベルを添える。

半透明の暗い台座と細い白線を基本に、押下とロック中は水色、必殺ゲージは金色で示す。必殺の外周と数値は実際の消費STに対する充足率。READYはゲージ満了を示し、接地や戦闘中の発動条件は従来どおり。押下時は約6%沈み、離すと戻る。

タップ判定は見える円より広く取り、セーフエリア内で8領域が重ならない。攻撃の判定幅は108基準px、補助操作は72〜84基準px。指の役割保持、移動＋攻撃、方向＋防御＋ジャンプ、空き領域でのカメラ、ポーズ／中断時の解除はV15を継続する。タッチ時だけ左上の情報を少し縮小し、一時停止を「Ⅱ」にする。

参考にした戦闘画面：[Zenless Zone Zeroの操作画面解説](https://techwiser.com/all-zenless-zone-zero-icons-and-symbols-meaning-complete-guide/)、[原神のモバイル戦闘UI解説](https://www.hoyolab.com/article/20104358)。作品の画像をゲームへ取り込まず、アイコンはUnityの頂点描画で作成した。

## ビルド・復元

`BuildAndroidMobileV16NoSetup` → `unity/CoffeeGame/Builds/Android-MobileV16/CoffeeGAME-MobileV16-development.apk`。同じシーンからARM64開発版を作成し、古いキャラを生成するセットアップ処理は呼ばない。Windows確認用は `BuildMobileControlsV16NoSetup`。通常Windows/Steam版の配置は今回は変更しない。

Androidは既存アプリへ `adb install -r` で更新し、進行データと赤羽織のモデル選択4を保持する。戻す場合は同じ署名の `Builds/Android-MobileV15/CoffeeGAME-MobileV15-development.apk` を `install -r` で上書きする。アプリの削除やデータ消去は不要。

## 検証

340/340 EditModeテスト、Windows開発プレイヤーの仮想Touchscreenで22/22項目成功。1536×864と1920×864の計4画面で剣士・猫・必殺READY・ポーズを確認した。Windowsの実セーブは不変。隠れたウィンドウ用のオフスクリーン描画では、透明な葉やエフェクトがUIへ重なる場合がある。通常実行はScreenSpaceOverlayのままで、Android画面の実測とは区別する。

APKは166,926,976 bytes、SHA-256 `52d8013886b631850bb98b44bfa70ec071ff64eee14284599ca734f76a2c1347`。ARM64／API26以上の開発署名を検査し、2026-09-07 03:11 JSTに接続中のPixel 9aへ `install -r` で更新した。更新の前後で端末のセーブJSON2件とPlayerPrefsが一致。モデル選択4、タッチ入力4を保持した。更新前設定はアプリ内 `files/playerprefs-before-ui-v16.xml` にも退避した。

端末はロック中のため、端末上の実描画・ノッチ・物理的な押しやすさは未確認。解除後にCoffeeGAMEを開くと新しいUIを使用できる。以前のV15 APKは元のハッシュのまま保持。公開ソースpush、ストア公開、通常Windows版への反映は行っていない。
