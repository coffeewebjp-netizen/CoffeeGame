# CoffeeLearning Integration

## 目的

CoffeeLearning に蓄積された弱点情報を CoffeeGAME の任意サブ要素に使い、ゲームを遊ぶ中で自然に思い出す回数を増やす。

同時に、CoffeeGAME と CoffeeLearning の双方で得た成果が、もう一方でも小さな価値を持つ循環を作る。

この連携はゲーム本編を遊ぶための必須条件にはしない。CoffeeLearning を使わないプレイヤーも、戦闘、成長、物語を最後まで遊べるようにする。

## 現在利用できる CoffeeLearning の情報

CoffeeLearning には、連携元として次の仕組みがすでにある。

- Google OAuth で認証されたユーザー
- DynamoDB 上のユーザー別単語・デッキ
- `mistakeCount`、`lastMistakeAt`、`status`、`lastChecked` を使う弱点スコア
- 単語、意味、CEFR、ラベル、学習プロファイル
- デッキ別の連続記録
- `streakFreeze` として管理される連続フリーズ石
- CoffeeMovie 向けの、ハッシュ保存・失効可能・限定 API 用 Bearer token

現在の弱点スコア計算は CoffeeLearning のブラウザ側にだけある。ゲーム連携を始める前に、同じ計算を CoffeeLearning 画面と連携 API の両方から使えるバックエンド共通関数へ移す。

弱点スコアと単語の `point` は別の値である。弱点スコアは `mistakeCount`、`lastMistakeAt`、`status` などから画面上で計算され、`point` は各単語レコードに保存された難度値である。軽量な単語取得 API でも両方の元データを取得できるため、連携 API は「この単語がどれだけ弱点か」と「クリア時に何ポイント相当か」を同時に返せる。

既存の語彙合計点は、現在 `ok` または `remembered` の単語を足し直したスナップショットであり、獲得履歴や通貨残高ではない。ゲームへの付与にはこの合計を使わず、学習が成立した瞬間に新しい「学習ポイント獲得イベント」を1件作る。

参照実装は CoffeeLearning の `docs/COFFEEMOVIE_INTEGRATION.md`、`backend/coffee-movie-auth.js`、`backend/deck-streaks.js` にある。

## 双方向連携の境界

現在の対象範囲を方向ごとに分ける。

| 方向 | 現在の対象 | 処理 |
| --- | --- | --- |
| CoffeeGAME → CoffeeLearning | 単語系デッキだけ。英単語デッキと汎用単語・カードデッキを含む | 弱点語を出題し、正式なクリアを CoffeeLearning の `ok` へ反映する |
| CoffeeLearning → CoffeeGAME | すべての学習デッキ | 当日に新しく成立したポイントイベントをゲーム通貨として受け取る |

ゲームで単語を正式クリアした場合は、この2方向が一つの操作でつながる。CoffeeLearning では対象語が `ok` になり、その `point` の獲得イベントも作られ、同じ応答系列で CoffeeGAME に受取証が返る。

日記や抽象思考の内容をゲームで弱点補強する機能は現時点では作らない。ただし、将来 `diary-review-v1` や `abstract-review-v1` のような別の出題プロバイダーを追加できるよう、API と DTO は単語専用の形に閉じない。

## デッキ分類

CoffeeLearning には既に `type` として `english`、`empty`、`diary`、`abstract-thinking`、`guidance` がある。ただし、これは画面テンプレートと学習上の意味が混ざっているため、連携判定用に `deckKind` を追加する。

```text
word
diary
abstract-thinking
english-lesson
other
guidance
unknown
```

初期移行規則:

| 既存の `type` または仮想デッキ | `deckKind` | ゲームでの弱点補強 | Learningポイントをゲームへ送る |
| --- | --- | --- | --- |
| `english` | `word` | 対象 | 対象 |
| `empty` の汎用単語・カード | `word` | 対象 | 対象 |
| `diary` | `diary` | 現在は対象外 | 対象 |
| `abstract-thinking` | `abstract-thinking` | 現在は対象外 | 対象 |
| DMM英会話などの仮想レポート | `english-lesson` | 現在は対象外 | 対象 |
| `guidance` など管理用 | `guidance` | 対象外 | 対象外 |
| 既存値の衝突や未分類 | `unknown` | 分類完了まで対象外 | 分類完了まで対象外 |

`deckKind` は作成後にクライアントが自由変更できる値にはしない。CoffeeLearning サーバーはこの値から、次の capability とポイントポリシーを解決してゲームへ返す。capability や倍率そのものはデッキへ保存せず、サーバー側ポリシーから導出する。

```json
{
  "deckKind": "word",
  "bridgeCapabilities": {
    "gameToLearning": { "challengeKinds": ["weakness-word-v1"] },
    "learningToGame": {
      "earningEventKinds": ["word-ok-v1"],
      "pointPolicyId": "word-ok-x1-v1"
    }
  }
}
```

日記・抽象思考は当面 `gameToLearning.challengeKinds: []` としつつ、`learningToGame` の獲得イベントとポイントポリシーは有効にする。将来の弱点補強は `challengeKinds` に新しい種類を追加するだけで開通できる。

移行は停止を伴わない二段階にする。最初に共通 resolver を追加し、保存済み `deckKind` があればそれを使い、なければ既存の `type` または `template` から上表の規則で解決する。値が衝突するものは名前やラベルで推測せず `unknown` にする。その後、dry-run レポートを確認して各デッキへ条件付きで backfill する。移行時点より前の操作から過去ポイントイベントは生成せず、有効化後の新規操作だけを対象にする。

## 基本方針

### 1. 正本を分ける

| データ | 正本 |
| --- | --- |
| 単語、意味、弱点、学習状態 | CoffeeLearning |
| デッキ別の連続記録、連続フリーズ石 | CoffeeLearning |
| 学習ポイントの獲得イベント、日次発行済み額 | CoffeeLearning の専用台帳 |
| キャラクター、装備、ゲーム進行 | CoffeeGAME のセーブ |
| ゲーム内通貨残高、適用済み `claimId` | CoffeeGAME のクラウドセーブ |
| アプリ間で一度だけ受け取る報酬の受取証 | 連携イベント台帳 |

CoffeeGAME から CoffeeLearning の単語レコードを直接更新しない。CoffeeLearning から CoffeeGAME のセーブを直接更新しない。ゲーム内で弱点語をクリアした場合も、CoffeeLearning のサーバーが回答を検証してから共通サービスを通して学習状態を更新する。双方の変化は、用途を限定した連携 API と一意なイベント ID を通す。

### 2. ポイントは「合計のコピー」ではなく獲得イベントにする

CoffeeLearning の既存ポイントを、ゲーム通貨の価値の元として使う。ただし、既存の単語合計やデッキ合計をログインのたびにコピーしない。

用語を次のように分ける。

| 用語 | 意味 |
| --- | --- |
| `rawPoints` | 単語の `point` や抽象思考の評価点など、CoffeeLearning 側の元の点数 |
| `multiplier` | デッキ分類ごとの換算倍率。単語は1、英語日記・抽象思考は100 |
| `amount` | その獲得イベントでゲームへ渡す通貨量 |
| `earnedPoints` | 日本時間の当日に成立した `amount` の合計 |
| `issuedPoints` | その日のうち、すでにゲーム向け受取証を発行した合計 |
| `claimablePoints` | `earnedPoints - issuedPoints`。今新たに受け取れる差分 |
| ゲーム内残高 | 受取証を一度だけ適用して増える CoffeeGAME 側の永続通貨 |

換算規則 v1 は、単語系を1倍、英語日記と抽象思考を100倍とする。

```text
word:              amount = rawPoints
diary:             amount = rawPoints * 100
abstract-thinking: amount = rawPoints * 100
```

例えば英語日記または抽象思考の総合点が78なら、ゲーム通貨は7,800ポイントになる。イベントには `rawPoints: 78`、`multiplier: 100`、`amount: 7800`、`pointPolicyId` を別々に保存する。後から換算率を変えても、作成済みイベントの価値は変えない。その他の学習デッキも対象だが、デッキ種別ごとにサーバー側の明示的な換算ポリシーを登録し、未定義時にクライアント申告値へフォールバックしない。

100倍後の `amount` を既存の `Wards.point` へ書き戻さない。既存フィールドには上限と別用途があるため、ゲーム通貨量は専用の獲得イベントだけに保持する。

現在の `point` はクライアントから編集でき、デッキによって意味も異なる。ゲーム通貨を付与する段階では、CoffeeLearning サーバーが対象デッキ、許容範囲、正規の学習操作を検証し、クライアント申告額をそのまま信用しない。

英語日記と抽象思考の `rawPoints` は、保存リクエスト内の `aiAnalysis` ではなく、採点時にサーバーが保存または署名した一回限りの `analysisReceiptId` から取得する。単語系も編集可能な `point` を無条件には使わず、作成経路・許容範囲を検証したサーバー確定値をイベントへスナップショットする。

日付境界は既存の連続記録と合わせて `Asia/Tokyo` とする。当日中に一度受け取った後でさらに学習した場合は、増えた差分だけをもう一度受け取れる。前日の未受取分は翌日に繰り越さない。受取済みポイントは CoffeeLearning から消費せず、CoffeeGAME の通貨残高だけが増える。

### 3. 学習は任意の安全地帯で行う

戦闘中、回避中、ボス戦中に問題を表示しない。最初の候補は次の場面に限定する。

- 記憶の祠
- キャンプや休憩地点
- 戦闘後の宝箱・レリック鑑定
- リトライ前後の短い振り返り

常に「あとで」「今回は使わない」を選べるようにする。不正解でも HP、通常ドロップ、ストーリー進行を失わせない。

ラン開始時に「学習の残響」を ON/OFF でき、1ランの語数を 0、3、5 などから選べるようにする。連携済みでも毎回の学習を強制しない。

### 4. 見せるだけでなく思い出させる

弱点語を背景やアイテム名に表示するだけではなく、答えを見る前に一度思い出す時間を作る。

MVP の出題手順:

1. 英語表現だけを短時間表示する
2. 頭の中で意味を思い出してもらう
3. 日本語候補を表示する
4. 「自信あり」「曖昧」「わからない」の確信度も記録できるようにする
5. 選択直後に正答と短い補足を返す
6. 不正解または低確信の正解は、同じプレイ中に連打せず後日の候補へ戻す

入力負荷の高い自由記述や AI 採点は、キャンプなど時間を止められる場所で後から追加する。

学習証拠の強さは、単なる表示、選択式、答えを考えてからの選択式、自由回答で分ける。弱い証拠を強い証拠と同じ重みで CoffeeLearning の状態へ反映しない。

## ゲーム内の最小学習ループ

仮称を「記憶の祠」とする。

1. CoffeeGAME が `deckKind: word` のデッキから弱点候補を最大3件取得する
2. 1回の探索で出す祠は少数に制限する
3. プレイヤーが任意で記憶問題に挑戦する
4. 正解なら CoffeeLearning サーバーが発行済み challenge と回答条件を検証する
5. 「クリア」と認める強さの回答なら、単語を `ok` にし、その時点の `point` 相当の学習ポイント獲得イベントを同時に作る
6. ゲームは当日未発行分の受取証を取得し、ゲーム内通貨として一度だけ反映する
7. 弱い選択式正答は補助学習シグナルに留め、その探索中だけ有効な小さな恩恵を返してもよい
8. 不正解なら正解を確認し、次回以降に間隔を空けて再登場する
9. 回答結果を CoffeeLearning へ補助学習シグナルとして残す

候補の優先順位には既存の弱点スコアを使う。ただし、同じ単語を短時間に繰り返さず、ラベルや難度が偏りすぎないようにする。

単に表示しただけ、または答えを見て選べる弱い選択式だけでは `ok` にしない。ゲーム側で `ok` にできる「クリア」は、CoffeeLearning が発行した未使用 challenge に対し、先に思い出す時間を置いた正答や自由回答など、サーバーが定めた条件を満たすものに限定する。ゲーム内の正答だけで `remembered` にはしない。

同じ `attemptId` の再送、同じ challenge の再利用、現在も有効な `ok` または `remembered` の単語には、2回目のポイントを付けない。`ok` の復習期限が切れ、別の `reviewCycleId` で再び出題された場合は、新しい復習として再獲得できる。

英語日記と抽象思考は study pack に入れない。将来対応時は、単語の `weakness-word-v1` とは別の `challengeKind` と採点条件を追加し、現在の単語フローを変更せずに拡張する。

## 学習情報とゲーム表現の対応

MVP の CoffeeGAME → CoffeeLearning 連携は、単語系デッキの弱点語と意味だけを使う。CoffeeLearning の詳細な学習プロファイルは、基本導線が安定してから次のように利用できる。

| CoffeeLearning の情報 | CoffeeGAME での候補 |
| --- | --- |
| 弱点Lv、最終誤答、復習期限 | 祠への出現優先度と再登場間隔 |
| CEFR | 問題の難度表示 |
| 単語の `point` | クリア時の `rawPoints`。サーバー検証後にゲーム通貨へ変換 |
| ラベル | 森、廃墟、魔法など出題テーマのまとまり |
| `confusability`、混同語 | よく混同する語を使った選択肢 |
| `polysemy` | 文脈ごとに意味が変わる分岐問題 |
| `collocation_dependence`、有用コロケーション | ルーンや語句の並べ替え |
| `register_sensitivity` | 会話相手や場面に合う表現の選択 |
| `form_difficulty`、発音補助 | 拠点での綴り・発音サブ課題 |
| AI学習助言 | 祠の短いヒント。詳細分析そのものは表示しない |

日記、抽象思考、個人メモの内容はゲームの弱点補強へ渡さない。ただし CoffeeLearning → CoffeeGAME のポイント連携では、本文を渡さず、すべての学習デッキについて正規の完了イベント、デッキ分類、点数だけを使う。

## 方向別の処理

### CoffeeLearning から CoffeeGAME

CoffeeLearning で当日に新しく獲得したポイントだけを、ゲームの共通通貨として受け取れるようにする。対象はすべての学習デッキであり、ゲームの弱点補強対象として選んでいないデッキの学習も含む。ログイン時点の累計ポイントや現在の `ok` 一覧を再計算して付与しない。

発生元と換算規則:

| `deckKind` | 成立条件 | `rawPoints` | 倍率 | 一意な発生元キー |
| --- | --- | --- | ---: | --- |
| `word` | ゲームまたは CoffeeLearning で単語を正規に `ok` へ進めた | その時点でサーバー検証した単語 `point` | 1 | `wordId + reviewCycleId` |
| `diary` | 英語日記を新規生成・保存した | サーバーが確定した `aiAnalysis.scores.total` | 100 | 新設する `submissionId` |
| `abstract-thinking` | 抽象思考ワークを新規生成・保存した | サーバーが確定した `aiAnalysis.scores.total` | 100 | 新設する `submissionId` |
| `english-lesson` | DMM英会話などの正規レポートを新規完了した | 正規化した総合評価点 | 個別ポリシー | `lessonFingerprint` など |
| `other` | デッキごとに定義した正規完了イベント | デッキごとに定義 | 個別ポリシー | 発生元 ID と評価版 |

抽象思考では現在、AI の総合点が `point` にもコピーされる。英語日記は `point` が既定値10になり、DMM英会話レポートは別テーブルに評価を持つため、保存済みレコードの `point` を一律に足してはならない。英語日記と抽象思考は `aiAnalysis.scores.total × 100` をイベント作成時に確定する。

各デッキの「生成・完了」と採用する点数はサーバー側アダプターで明示する。抽象思考と英語日記は採点結果をクライアント経由で保存しているため、通貨連携前にサーバー署名付き評価 receipt または採点・保存・イベント作成の一体処理も必要になる。

同じ日記・ワークの編集、再採点、通信再送では新しいポイントを作らない。新規 `submissionId` の初回確定だけを対象にし、その時点の点数と100倍換算後の `amount` を固定する。

日記・ワークの獲得日は、ユーザーが入力できる `diaryDate` ではなく、サーバーが初回保存を確定した時刻の日本時間日付とする。

プレイヤー導線:

1. CoffeeGAME の拠点で「今日の学習ポイント」を表示する
2. 「CoffeeLearning で学ぶ」を押してブラウザまたはアプリを開く
3. CoffeeLearning で弱点語、抽象思考などに取り組む
4. CoffeeGAME へ戻ると自動同期し、当日の新しい差分だけを受け取る
5. 同じ日にさらに学習した場合も、次の同期では増えた差分だけを受け取る

共通通貨はキャラクター成長や交換に使えるが、学習しないと本編が進めない価格設計にはしない。必要なら発生元別または日次の換算上限を `pointPolicyId` が指すサーバーポリシーに含める。

### CoffeeGAME から CoffeeLearning

現在は `deckKind: word` かつ `challengeKinds` に `weakness-word-v1` を持つデッキだけを対象にする。英単語だけでなく、同じ単語・意味・学習状態を持つ汎用単語デッキも含む。

- CoffeeLearning が弱点語と未使用 challenge を発行する
- CoffeeGAME は challenge に対する回答だけを送る。点数やデッキ分類を申告しない
- CoffeeLearning が challenge、対象語、現在状態、正答条件を検証する
- 正式クリアなら、単語の `ok` 更新と学習ポイント獲得イベントを CoffeeLearning 内の同一トランザクションで成立させる
- 応答に `eventId` と最新の `claimablePoints` を含め、CoffeeGAME は直後に受取証を発行・適用する
- 通信断時は `attemptId`、`eventId`、`claimId` から同じ画面フローを安全に再開する

ここでの「同時反映」は、二つのサービスをまたぐ一個のDBトランザクションではなく、プレイヤーから見て一回のクリア操作で完了するという意味である。CoffeeLearning 内の `ok` とイベント作成は原子的に行い、CoffeeGAME への反映は冪等な受取証で必ず追いつけるようにする。

英語日記、抽象思考、レッスンレポート、その他の非単語デッキを指定した回答更新は現在拒否する。将来は専用の `challengeKinds` を追加して開通できるが、単語用の正答・`ok` 条件を流用しない。

CoffeeLearning の継続を完全にゲームで代替できないようにし、ゲーム内の弱点補強は任意の補助導線とする。

## 現在の境界外に置く将来候補

ゲームで得た「連続石の欠片」を CoffeeLearning の `streakFreeze` へ交換する案は残すが、今回確定した双方向ポイント循環には含めない。実装する場合は、週次上限、付与先デッキ、サーバー検証を別仕様として決める。

## 認証とアカウント連携

ゲーム本体のクラウドセーブ用アカウントと、CoffeeLearning の学習アカウントは責務を分ける。CoffeeLearning 連携はゲーム設定から明示的に行う。

推奨導線:

1. CoffeeGAME の設定で「CoffeeLearning と連携」を押す
2. 通常ブラウザで CoffeeLearning を開く
3. CoffeeLearning 側で既存の Google ログインを行う
4. ユーザーが CoffeeGAME への限定アクセスを承認する
5. 一回限りの認可コードを CoffeeGAME に返す
6. CoffeeGAME が PKCE verifier と認可コードを限定 token に交換する

Windows は CoffeeMovie と同様に `127.0.0.1` の一時コールバックを利用できる。Android は固定した App Link または専用 URI を使い、許可済みの戻り先以外は拒否する。

新しい token は CoffeeMovie token をそのまま共有せず、CoffeeGAME 専用または汎用 Coffee ecosystem token とする。

必要な scope の例:

- `study-pack:read`
- `study-attempt:write`
- `learning-reward:read`
- `learning-reward:claim`

生の Google token、ユーザーのメールアドレス、CoffeeLearning のブラウザ session cookie はゲームへ保存しない。サーバーには token のハッシュだけを保存し、一覧表示、失効、最終使用日時の確認を可能にする。

## API 契約案

CoffeeLearning に CoffeeGAME 専用の狭い API を追加する。既存の `/words` 全件取得や直接更新をゲームへ許可しない。

```text
GET  /api/coffee-game/v1/bootstrap?studyDeckId=...&limit=3&cursor=...
POST /api/coffee-game/v1/study-attempts
POST /api/coffee-game/v1/reward-claims
```

最初はこの3本で接続を検証し、必要になった時点で study pack と報酬一覧を個別 API へ分ける。

`studyDeckId` は省略可能とし、省略時も全デッキ分の当日ポイントを返す。指定する場合は `deckKind: word` かつ `challengeKinds` に `weakness-word-v1` を持つデッキだけを受理し、英語日記や抽象思考のデッキ ID は拒否する。

連携設定では、非対応デッキも含むデッキ一覧と解決済み capability を返す。CoffeeGAME は `challengeKinds` が空のデッキを「現在はゲーム内復習未対応」と表示できる。将来 capability が追加されたとき、認証やアカウント連携をやり直さずに選択可能になる。

`bootstrap` の最小応答:

```json
{
  "schemaVersion": 1,
  "serverDate": "2026-08-08",
  "syncCursor": "cursor_...",
  "learningPointsToday": {
    "scope": "all-learning-decks",
    "timezone": "Asia/Tokyo",
    "earnedPoints": 16420,
    "issuedPoints": 420,
    "claimablePoints": 16000,
    "breakdown": [
      {
        "deckKind": "word",
        "rawPoints": 420,
        "multiplier": 1,
        "amount": 420
      },
      {
        "deckKind": "diary",
        "rawPoints": 76,
        "multiplier": 100,
        "amount": 7600
      },
      {
        "deckKind": "abstract-thinking",
        "rawPoints": 84,
        "multiplier": 100,
        "amount": 8400
      }
    ]
  },
  "pendingClaims": [
    {
      "claimId": "claim_previous_...",
      "earnedDateJst": "2026-08-08",
      "amount": 420
    }
  ],
  "studyDeck": {
    "deckId": "deck-english-main",
    "deckKind": "word",
    "bridgeCapabilities": {
      "gameToLearning": { "challengeKinds": ["weakness-word-v1"] },
      "learningToGame": {
        "earningEventKinds": ["word-ok-v1"],
        "pointPolicyId": "word-ok-x1-v1"
      }
    }
  },
  "studyPack": {
    "packId": "pack_...",
    "expiresAt": 0,
    "challengeKind": "weakness-word-v1",
    "items": [
      {
        "challengeId": "challenge_...",
        "prompt": "bring oneself to",
        "mode": "meaning-choice",
        "choices": [
          { "id": "a", "text": "～する気になる" },
          { "id": "b", "text": "～を持ってくる" }
        ],
        "weaknessLevel": 4,
        "cefr": "B2",
        "reviewCycleId": "review_...",
        "rewardPreview": {
          "rawPoints": 420,
          "multiplier": 1,
          "amount": 420,
          "pointPolicyId": "word-ok-x1-v1"
        }
      }
    ]
  }
}
```

ゲームには必要最小限の情報だけを渡す。個人メモ、日記、詳細な AI 分析は、明示的な追加同意なしでは返さない。

`study-attempts` は `attemptId`、`challengeId`、`reviewCycleId` で冪等に処理する。サーバーは challenge から対象デッキ、`deckKind`、単語、正答、ポイントを解決し、クライアントから `amount` や分類を受け取らない。クリア条件を満たした正答では、CoffeeLearning 側の共通サービスが「有効な学習状態から `ok` への更新」と「学習ポイント獲得イベントの作成」を一つの論理処理として行う。表示だけ、期限切れ challenge、すでに有効な `ok`、再送された回答には新しいポイントを作らない。`remembered` への変更は行わない。

`reward-claims` ではデッキを指定せず、クライアントも `amount` を指定しない。サーバーがすべての学習デッキを対象に、その日本時間の日付について `earnedPoints - issuedPoints` を計算し、0より大きい差分だけに一意な `claimId` を発行する。

```json
{
  "requestId": "claim-request_...",
  "expectedServerDate": "2026-08-08"
}
```

```json
{
  "claimId": "claim_...",
  "serverDate": "2026-08-08",
  "amount": 16000,
  "status": "issued"
}
```

同じ `requestId` の再送には同じ結果を返す。同時に2端末から請求された場合は、日次集約の `revision` を使った条件付き更新で片方だけが差分を確保する。日次更新と受取証作成は同じトランザクションで行う。CoffeeGAME 側も適用済み `claimId` をクラウドセーブに保存し、同じ受取証で残高を2回増やさない。

前日の「まだ受取証を発行していないポイント」は翌日に請求できない。一方、当日中に発行済みだった受取証は、発行直後の通信断で失わないよう一定期間再取得できる。`bootstrap.pendingClaims` で保持期間内の受取証を返し、ゲーム側で適用済みなら安全に無視する。

## オフラインと再送

- 取得済み study pack は有効期限内だけ端末にキャッシュできる
- 回答は一意な `attemptId` を持つ outbox として保存する
- 再接続後に同じ `attemptId` で再送しても一度だけ処理する
- CoffeeLearning 側で `claimId` が発行されるまで、ゲーム通貨を端末だけで増やさない
- 発行済みで未適用の受取証は再接続後に再取得できるようにする
- ゲーム内の一時的な演出報酬は即時表示してよいが、永続通貨は受取証をクラウドセーブへ一度だけ適用した後に確定する
- 日付を端末時計から決めず、CoffeeLearning のサーバー日時を使う

回答イベントには少なくとも `attemptId`、`challengeId`、`cueDirection`、`answerMode`、`result`、`confidence`、`latencyMs`、`occurredAt`、`feedbackShown` を含める。

## 連携イベント台帳

相互報酬は現在残高から推測せず、作成・発行・適用済みを追跡できるイベントとして扱う。増え続けるイベントを CoffeeLearning の `Users_Table` 内の配列へ保存せず、専用テーブルへ置く。

学習ポイント獲得イベントの最低限のフィールド:

```text
eventId
userId
earnedDateJst
deckId
deckKindAtEarn
sourceType
sourceId
earningTrigger
reviewCycleId または sourceRevision
rawPoints
multiplier
amount
pointPolicyId
earnedAt
idempotencyKey
```

日次集約:

```text
userId
earnedDateJst
earnedPoints
issuedPoints
revision
lastEventAt
```

受取証:

```text
claimId
userId
earnedDateJst
amount
requestId
issuedAt
status
```

学習操作の保存時に `idempotencyKey` を条件付き作成し、同じ操作からイベントを二重生成しない。獲得イベント作成と日次 `earnedPoints` の加算は同じトランザクションにする。弱点語クリアでは、さらに対象単語の条件付き `ok` 更新も同じトランザクションへ含める。受取時は `revision` を使った条件付き更新で `issuedPoints` を進め、確保した差分を不変の受取証にする。

CoffeeGAME 側では、ゲーム通貨の増加と `claimId` の適用済み記録を同じクラウド保存処理で確定する。途中で通信が切れても、未適用の `claimId` は再試行でき、適用済みのものは増額しない。

## 実装順

### 早期に作る土台

1. 既存デッキを `deckKind` へ移行し、新規デッキ作成時に分類を必須にする
2. 方向別 `challengeKinds`、`earningEventKinds` とサーバー管理のポイントポリシーを作る。単語は1倍、英語日記・抽象思考は100倍で固定する
3. `LearningPointEarned`、日次集約、受取証の schema と JST 日付規則を固定する
4. CoffeeLearning の弱点スコアをバックエンド共通関数へ移し、単語系デッキだけを study pack 対象にする
5. 全学習デッキの正規完了を獲得イベントへ変換するサーバー側アダプターを作る
6. 既存の OK 処理を共通サービス化し、状態更新と獲得イベントを一貫して処理する
7. 冪等な獲得イベント作成と「当日差分だけ」の claim 処理を CoffeeLearning に作る
8. Unity 側に `ILearningBridge` と `MockLearningBridge` を定義し、schema version 付き DTO を固定する
9. Windows のブラウザ往復と Android の App Link を小さく検証する
10. CoffeeLearning 側に単語 study pack と全デッキ当日ポイントを返す `bootstrap` API を追加する

### 戦闘と成長の後に作るもの

1. 記憶の祠 UI
2. 回答 outbox と同期
3. 補助学習シグナル
4. 学習ポイントを使う交換・成長 UI
5. 1日後・7日後の保持効果と不正利用対策を確認する
6. 将来用の非単語 `challengeKind` をモックで接続できることを確認する

連携の認証・データ境界は早く決めるが、縦スライスの戦闘が固まる前に学習演出を大量に作らない。

## 成功条件

- CoffeeLearning を連携しなくてもゲームが成立する
- 連携時、英単語と汎用単語を含む `deckKind: word` の弱点語だけが安全地帯で短く再登場する
- 英語日記、抽象思考、その他の非単語デッキは現在のゲーム回答から更新できない
- 不正解でも本編の進行や通常報酬を失わない
- 同じ弱点語を間隔を空けて思い出す機会が増える
- 弱点語の正規クリアでは `ok` 更新とポイントイベントが一度だけ成立する
- ゲームでの正式クリア後、同じ画面フローでゲーム通貨まで反映され、通信断後も重複なく再開できる
- 同じ回答の再送や有効な `ok` の再クリアではポイントが増えない
- すべての学習デッキについて、CoffeeLearning の累計値ではなく日本時間の当日獲得分だけを受け取れる
- 当日一度受け取った後で学習すると、次回は増えた差分だけを受け取れる
- 英語日記と抽象思考は `rawPoints × 100` が正確に通貨へ変換される
- 同じ日記・抽象思考ワークの再保存、再採点、通信再送ではポイントが増えない
- その他のデッキも、発生元ごとの一意キーと明示的なポリシーで一度だけ通貨へ変換される
- ゲームで触れた語の1日後・7日後のCoffeeLearning自由回答保持率を比較できる
- 学習の残響の利用率、問題スキップ率、ラン完走率を確認できる
- Windows と Android の同じ CoffeeLearning アカウントで回答履歴と相互報酬が一致する
- 通信再試行、再ログイン、複数端末からの同時操作で通貨やアイテムが二重付与されない
