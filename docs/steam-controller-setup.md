# CoffeeGAME — Steam Controller 設定

CoffeeGAME は Unity Input System の標準 Gamepad 入力を受け取ります。Steam Controller は、CoffeeGAME 専用のショートカットから **Gamepad テンプレート**で起動してください。

Steamの設定画面にある「Aボタン」「Xボタン」等はCoffeeGAMEのアクション名ではなく、仮想Xboxコントローラーへ送るボタンです。CoffeeGAMEの標準アクションは次のとおりです。

| Steam／Xbox入力 | CoffeeGAMEの動作 |
| --- | --- |
| 左Stick／D-pad | 移動、メニュー選択 |
| A／South | ジャンプ、決定 |
| B／East | メニュー取消。戦闘アクションなし |
| X／West | 回転斬り |
| Y／North | 氷魔法 |
| RT | 刀。空中ではなで斬り |
| Start | ポーズ、決定 |
| View／Select | ボタン設定 |
| LB／RB | 現在未使用 |

## 初回設定

1. Steam の「ゲームを追加」から「非 Steam ゲームを追加」を選びます。
2. `CoffeeGAME.exe` を参照して登録します。
3. 登録した CoffeeGAME のコントローラーレイアウトを開きます。
4. テンプレートから `Gamepad` を選び、適用します。
5. 以後は、その Steam ライブラリ項目から CoffeeGAME を起動します。
6. CoffeeGAMEの起動時選択で `Controller / Gamepad` を選びます。

アプリを直接起動すると Steam の Desktop Layout が使われることがあります。Desktop Layout で X に「仮想キーボードを表示」が割り当てられている場合、X はゲームへ届く前に Steam に処理されます。

直起動版とSteam起動版を同時に開かないでください。Steam Controllerを使うときはCoffeeGAMEを一つだけ起動します。Steamから起動してNative Gamepadが届いている場合は、起動時の `Steam Desktop compatibility` を選びません。

## ゲーム内のボタン変更

- `View / Select`（キーボードは `Tab`）: ボタン設定を開く／閉じる
- 左スティックまたは方向パッド: 項目を選ぶ
- South 位置のボタン: 決定
- East 位置のボタン: 取消

変更したい行で決定し、決定に使ったボタンをいったん離してから、新しいボタンを押します。A と X のように既に使われているボタンを指定した場合は、2つの割当を自動で交換します。設定は終了後も保存されます。

## X でキーボードが出る場合

1. Steam の CoffeeGAME 専用コントローラーレイアウトを開きます。
2. X の割当から「仮想キーボードを表示」を外します。
3. X を通常の Gamepad の West ボタンとして割り当てます。
4. CoffeeGAME を Steam から再起動します。

ゲーム画面の「接続」が `Gamepad ... [XInput...]` になれば標準 Gamepad として届いています。`対象外HID` または「検出されていません」の場合は、Steam 側で Gamepad/XInput 出力がまだ有効になっていません。

画面の ABXY 表示は物理印字を推測しません。診断欄の実受信名と `<Gamepad>/buttonWest` などの control pathを基準に確認してください。

2026-08-09の実機確認では、Steamライブラリから起動したCoffeeGAMEが `Controller (XBOX 360 For Windows)` としてUnityへ届き、戦闘開始まで進めました。

## 参考

- [Valve: Legacy Mode Bindings](https://partner.steamgames.com/doc/features/steam_controller/legacy_mode?language=english)
- [Valve: Steam Input Gamepad Emulation](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices?language=english)
