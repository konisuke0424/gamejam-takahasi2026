# まねごえの森（仮） — KaiForest

GameJam プロトタイプ。仕様: Obsidian `Projects/GameJam.md`

森で迷子になった少年カイが、出口で待つお母さんの声を頼りに森を抜ける一人称ホラー。
森にはお母さんの声をまねる化け物がいて、本物と偽物の声を聞き分けることが攻略の鍵になる。

- お母さんに触れたらクリア / 化け物に捕まったらゲームオーバー
- 本物の出口は `Forest.unity` に置いた鳥居（`Torii_Exit`）1か所。壊れた鳥居（`BrokenTorii`）は偽の出口としての飾り
- 森は `Forest.unity` に置いたオブジェクトそのもの（下の「マップの編集」を参照）

## 進み方（チェックポイント）

3つの段階を順に進む。拾い物がチェックポイントで、捕まると最後に拾った場所からやり直し（「クリックで　つづきから」）。お母さんに会うとチェックポイントは消え、次は最初から。

- **はじまり**: タイトルでクリック → 日記の絵（`nikki01.png`）へディゾルブ（1秒）→ 3秒見せる（クリックでとばせる）→ 本編へディゾルブ（1.5秒）。捕まったあとのやり直しは日記なしで本編へ
- **おわり**: お母さんに会うと、顔を見たあと本編から日記の絵（`nikki02.png`）へクロスフェード（2秒）。これがクリア画面で、絵の左上に金色の筆文字の「ゲームクリア」（`UI_GameClear.png`）が大きく飛び込み（チャイム付き）、続いてクリア時間がふわっと出る。そのあと「クリックで　タイトルへ」が点滅し、クリックでタイトルへ戻る。リザルトの位置と大きさは Story Settings の `Result Position`（絵の左上からの割合）と `Result Scale`。見出し画像は `python Tools/gen_textures.py --headlines` で作り直せる
- 絵と秒数は `Assets/Game/Resources/StorySettings.asset`（メニュー `KaiForest > Story Settings`）。絵は画面に収まる大きさで、余白は黒

### 主人公のセリフ（`BoyVoice`）

主人公の心の声として、方向のない音で流す（スタミナによる聞こえにくさの影響は受けない）。自分のセリフどうしは重ならない。

| いつ | セリフ | 頻度 |
|---|---|---|
| タイトルから始めて本編が見え始めたとき | `takashi_06`「お母さんを探さなきゃ」。言い終わるまで、その場から動けず見回せない | 1回（捕まったあとのやり直しでは言わない） |
| 外周の見えない壁（`ForestMap/Walls`）にぶつかったとき | `takashi_06`（これ以上進めないことを伝える） | 20秒に1回まで |
| 分かれ道のトリガー（`VoiceTrigger`）に入ったとき | `takashi_07`「右側から声が聞こえる」 | トリガーごとに1回 |
| 偽物の声（化け物・鹿トラップ）が聞こえたとき | `takashi_08` | 60秒に1回まで |
| お母さんの声（お母さん・ハンカチ・かんざし）が聞こえたとき | `takashi_09` | 90秒に1回まで |

- 声を聞いたときのセリフは、その声がある程度の大きさ（0.1 以上）で聞こえたときだけ、声が終わって 0.6 秒後に言う。どのセリフどうしも最低12秒あける
- 音声ファイル・待ち時間・音量は `Assets/Game/Resources/BoyVoiceSettings.asset`（メニュー `KaiForest > Boy Voice Settings`）。ファイルは `Assets/Game/Audio/Voices/takashi_06〜09.wav` が自動で入る
- 分かれ道のトリガーは `Checkpoints/VoiceTrigger_Fork`（緑の円、半径4m）。初期は段階1（ハンカチを探している間）だけ言う。`Place Checkpoints and Traps` で置き、本当の分かれ道へ動かす。別のセリフを言わせたいときは複製して `Line` に音声を入れる

| 段階 | 探すもの | 本物の声が聞こえる場所 | 森にいる偽物 |
|---|---|---|---|
| 1 | お母さんの**ハンカチ** | ハンカチ | なし（追ってくる化け物も、偽物の声もない） |
| 2 | お母さんの**かんざし** | かんざし | 鹿トラップ・化け物トラップ（偽物の声で呼ぶ） |
| 3 | **お母さん**（かんざしを拾うと現れる） | お母さん | トラップ＋森じゅうを追ってくる化け物 1体 |

- 落ちている物は、近く（3m 以内＝`CheckpointItem.pickupRadius`）に入るだけで拾う（クリック不要）
- 物のそばに置いた目印（パーティクルなど）は物と一緒に現れ、拾うと一緒に消える。`CheckpointItem.markers` に入れたもの、空なら物から 4m 以内に単独で（何かの子でなく）置いたパーティクル
- 拾うと、その物の文（`CheckpointItem.message`）が画面に出る
- **鹿トラップ**: 偽物の声で呼ぶ。半径 1m に入ると鹿の絵が画面に飛び出す（驚かすだけでゲームオーバーにはならない）。1回きり
- **化け物トラップ**: 半径 20m の縄張りに化け物が1体いて、偽物の声で呼ぶ。縄張りに入ったカイだけに気づき、縄張りの縁まで追ってくる（外へ逃げると縁で見失う）。捕まるとゲームオーバー
- 段階3の化け物は、かんざしを拾った場所とお母さんのあいだの道すじに現れる（数は `GameManager.monsterCount`）
- `Forest.unity` にチェックポイントもトラップも何も置いていないときは、ゲームが既定の場所（配置メニューと同じ場所）に自分で置いて始める。一部だけ置いてあるときは置いてあるものだけを使い、ハンカチ・かんざしが無い段階はとばす
- 仕組み：`StageDirector`（段階・チェックポイント・拾う操作・トラップと化け物の出現）。F2（エディタと開発ビルドだけ）で次の段階へとばせる（チェックポイントにはならない）

## マップの編集（Forest.unity）

`Assets/Scenes/Forest.unity` を開くと、霧のない明るい状態でマップ全体を編集できる（明かりは編集用で、ゲーム中は消える）。

| オブジェクト | 中身 | 編集のしかた |
|---|---|---|
| `ForestMap` | マップの根元。歩ける範囲（`halfSize`、黄色の枠）を持つ | 範囲を変えたら `Walls` も合わせて動かす |
| `PlayerStart` | カイのスタート地点と向き（緑の球と矢印） | 動かす・回す |
| `Landmarks/Torii_Exit` | 本物の出口。`Checkpoints/Mother` が無いときは、お母さんはこの前方 4m を中心に半径 20m を歩く（水色の円） | 動かす。前方（青い矢印）がカイの来る側 |
| `Checkpoints/Handkerchief` | お母さんのハンカチ（段階1で探す）。黄色の円が拾える範囲 | 動かす。拾える範囲・拾ったときの文・目印（`markers`）は Inspector の `CheckpointItem` |
| `Checkpoints/Kanzashi` | お母さんのかんざし（段階2で探す） | 同上 |
| `Checkpoints/Mother` | 段階3でお母さんが現れる場所。お母さんはここから動かない（`Wander` をオンにすると、水色の円＝`wanderRadius` の中を歩き回り、カイを見つけると近寄る）。お母さんの見本（画像）はエディタでだけ見える（古い人形のままなら `KaiForest > Map > Regenerate Mother Prefab`） | 動かす・回す |
| `Checkpoints/DeerTrap` | 鹿トラップ。赤い円（半径 `triggerRadius` = 1m）に入ると鹿が飛び出す。絵は `scareImage` | 動かす・複製・削除 |
| `Checkpoints/MonsterTrap` | 化け物トラップ。橙の円（半径 `areaRadius` = 20m）が化け物の縄張り | 動かす・複製・削除 |
| `Landmarks/*` | 地蔵・祠・大杉などの目印、壊れた鳥居 | 動かす・複製・削除。`Assets/Game/Map/Prefabs/Landmarks` から追加 |
| `Trees/Area_*/Cedar_*` | 杉（8種類のプレハブ、約4000本） | 動かす・複製・削除。`Assets/Game/Map/Prefabs/Trees` から追加 |
| `Undergrowth` | 茂み・石・草・枝を区画ごとにまとめたメッシュ | 直接は編集しない。目印や出口を動かしたら `KaiForest > Map > Rebake Undergrowth` で、それらを避けて撒き直す |
| `Ground` / `Walls` | 地面と外周の見えない壁 | 通常は触らない |

- プレハブ（杉・目印・鳥居）は仮の見た目。モデルを差し替えるとシーン上の全部が置き換わる。当たり判定（Collider）は残すこと（AI の視線や移動に使う）
- `KaiForest > Map > Rebuild Forest Scene From Seed` は**手での編集をすべて捨てて**最初の配置に戻す
- `KaiForest > Map > Regenerate Placeholder Prefabs` はプレハブを仮の見た目に作り直す（差し替えたモデルは失われる）
- **チェックポイントとトラップの配置**: メニュー `KaiForest > Map > Place Checkpoints and Traps` で、足りないものをスタートから出口への道すじに沿って `ForestMap/Checkpoints` の下に置く（ハンカチ・かんざし・お母さん各1、鹿トラップ3、化け物トラップ2）。あとは手で動かす。もう置いてある種類は増やさない
- 円や名前はシーンビューの表示（ギズモ）で、ゲーム中には見えない。トラップ自体もゲーム中は見えない（化け物トラップの化け物は見える）
- プレハブは `Assets/Game/Map/Prefabs/Checkpoints`（ハンカチ・かんざしは仮の見た目。中の `Model` を差し替えてよい。近くでかすかに光る `Glint` 付き）。`KaiForest > Map > Regenerate Checkpoint Prefabs` で仮の見た目に作り直す
- 置いたあとに茂みが物にかぶっていたら `Rebake Undergrowth`（ハンカチ・かんざし・お母さんのまわりは空けて撒き直す）
- 段階3の化け物は、かんざしを拾った場所からお母さんへの道すじの左右 35m 以内で、カイから 35m 以上・お母さんから 25m 以上・ほかの化け物から 25m 以上離れた空き地に現れる（場所が足りないときは、まず化け物どうしの距離、次に道すじの条件をあきらめる）

## 操作（マウスのみ）

| 操作 | 動作 |
|---|---|
| マウスを動かす | 見回す（クリック不要。上下は ±10度まで＝`PlayerController.pitchLimit`） |
| 左ボタンを押している間 | 前へ進む |
| 右ボタンを押している間 | うしろへ下がる |
| Esc | カーソルのロックを外す（プレイ中にクリックするとまたロックされる） |
| F1（開発用） | デバッグ表示（段階・探す物までの距離・AI状態・最後に再生した声） |
| F2（開発用） | 次の段階へとばす（エディタと開発ビルドだけ） |
| N（開発用） | お母さんの段階へとばし、お母さんの4m手前へ移動（エンディングの確認用。エディタと開発ビルドだけ） |

視点の感度は `PlayerController.lookSensitivity`（ゆっくり動かしたとき 0.225度/ピクセル）。左右はマウスを速く振るほど大きく回り、すばやく振ると最大で1.5倍（`lookAcceleration`）になるので、大きく振れば振り返れる。回りすぎるときは `lookAcceleration` を 0 にするか、`lookSensitivity` を下げる。

プレイ中はカーソルを画面に固定（ポインターロック）して、マウスの動きだけで向きを変える。ブラウザの仕様でロックはクリックしたときにだけかかる。埋め込み先のページ（unityroom など）がポインターロックを許可していないと、ロックされずに画面の端で回れなくなる。

## 声の聞き分け（仕様で選んだ「声の違和感」と「言葉の違い」）

| | お母さん | 化け物 |
|---|---|---|
| セリフ | 10種類。毎回違うことを言う（「晩ごはん、カレーだよ」など母だけの言葉がある） | 母の決まり文句3つが中心。たまに壊れた言い回し（「どこに、どこに…」「お母さんは、ここに、いないよ」） |
| 抑揚 | 毎回わずかにピッチが変わる | 毎回まったく同じ抑揚（機械的な繰り返し） |
| 声質 | 素の声 | ピッチの揺れ、半音ずれた二重の声、かすかなうなり、語尾が沈む、どもり |

上表の聞き分けは **Haruka プロト**（`build_audio.py` の妖怪加工）向け。本番 Irodori は `Tools/audio_irodori` の caption + V7 FX。プロトで違和感を弱めたいときは `python Tools/audio/build_audio.py --monster-strength 0.6` など。

## AI

- **お母さん**: 段階3で `Checkpoints/Mother` のちょうどその場所に現れ、動かずに 4〜7.5秒おきに呼ぶ。ランタンが見える距離（11m）にカイが来ると、呼ぶ間隔が半分になる。触れる（2m 以内に入る）とクリア。`Checkpoints/Mother` の `Wander` をオンにすると、前のように歩き回ってカイに歩み寄る。段階1・2では、ハンカチ・かんざしから同じように本物の声がする。
  - 見た目は `Assets/Denim/mother.png` の立ち絵（高さ 1.6m）。左右にだけ回って常にカイの方を向く（`Billboard`）。顔の前（カイの側）1.1m に明かりがあり、どちらへ歩いても顔が照らされる。マテリアルは `Resources/MotherBillboard.mat`（メニュー `KaiForest > Mother Picture Material`。画像の透明な部分で切り抜き）。画像を差し替えたら `CharacterFactory` の、画像の中での足元・身長・顔の位置の割合も合わせる
- **化け物**: 段階3で現れて森じゅうを追ってくる化け物（初期値 1体）と、化け物トラップの化け物（縄張りの中だけ）。どれも下の動きをする（トラップの化け物は縄張りの中を歩き、縄張りの中のカイにだけ気づく）。数・現れる場所・最初の呼び声のずらし方は `Main.unity` の `GameManager`（`monsterCount` / `monsterSpacing` / `monsterMinFromStart` / `monsterMinFromMother` / `monsterRouteWidth` / `monsterCallStagger`）で変えられる。スタミナ・心音・画面の揺らぎは、いちばん近い化け物と「どれか1体でも追っているか」で決まる。
  - Search（9割はカイの半径18m以内を行き先にして、8秒ごとに選び直す。カイの居場所は知らないが寄ってくる）→ Investigate（足音が聞こえたら音の場所へ。歩いていると22m、止まっていると6mで気づく）→ Chase（16m以内でランタンが見えたら追う。6秒見失うと調査に戻る）→ Retreat（10秒追いかけたら諦め、4秒間カイを無視して反対方向へ去ってから探索に戻る）。追跡中はときどき一気に距離を詰めてくる。退却中も、ぶつかるほど近づけば捕まる。1体に捕まった時点で全員止まる。
  - 追っている化け物の呼び声は仲間を呼ぶ。半径50m以内で探索・調査中のほかの化け物は、2秒ごとに伝わるカイの居場所へ向かう（2体以上に同時に追われることがある）。`MonsterAI.alertRadius` を 0 にすると、それぞれ独立して動く。
- **捕まったとき**: 化け物がカイの前で覆いかぶさり、視界がその顔の正面へ一気に引き寄せられて、顔が画面いっぱいに映る（揺れながら約1.6秒）→ 暗転。手のランタンは画面右下から下へ落ちて画面外に消え、暗くなった光が顔を下から照らす（白飛びしない）。時間・視野角・顔との距離・ランタンの明るさは `Main.unity` の `GameManager`（`scareDuration` / `scareFov` / `scareFaceDistance` / `scareLanternLight`）で調整できる
  - カメラの位置は `scareViewOffset`（カイから見て x: 右＋／左－、y: 上＋／下－。初期値は左斜め下 (-0.18, -0.15)）。どこに置いても顔は画面の中心に来る
  - 画面の中心に来る点は `scareAimOffset`（x: 右／左、y: 上／下、z: 奥／手前）。顔の中心からずれて見えるときに直す。y を－にすると顔が画面の上へ、＋にすると下へ動く
  - 顔の中心の目印は頭の骨（Head）から上へ 1.2、前へ 0.7（Head→HeadTop の長さが 1）。Tripo のリグは顎のあたりで頭の骨が終わっているため。化け物はうつむいていて額が前に出ているので、目〜口はそれより下にある（`MonsterBody.FaceUp` / `FaceAhead`）
- パラメータは `MotherAI` / `MonsterAI` / `PlayerController` の public フィールドで調整できる。

## スタミナ

- 化け物に**追われている間だけ**減る（満タンから10秒で空）。追跡が終わって1.5秒後から回復し、空から満タンまで12秒
- スタミナが減っても**移動の速さは変わらない**。代わりに、7割を切ったあたりから次の影響がだんだん強くなる
  - カイの息切れ（`player_pant.wav`）が聞こえる
  - 心音が大きく、速くなる
  - 周りの音（お母さんと化け物の声、虫・風、化け物の足音と息）が小さくなり、声はこもって聞き分けにくくなる
- 画面にゲージは出さない。F1 のデバッグ表示で残量を確認できる
- 仕組み：`PlayerStamina` が毎フレーム `Hearing`（音量と、こもり具合）を更新し、声（`VoiceEmitter`）・環境音・化け物の音がそれを掛ける

## エディタで調整できる設定

| 設定 | 場所 | 中身 |
|---|---|---|
| ランタン（とデバッグ用の昼モード） | `Assets/Game/Resources/LanternSettings.asset`（メニュー `KaiForest > Lantern Settings` で選択） | 下表 |
| スタミナ | `Assets/Game/Resources/StaminaSettings.asset`（メニュー `KaiForest > Stamina Settings` で選択） | 減る秒数・回復・影響が出始める残量・息切れの音量・聞こえにくさの強さ |
| 遠景 | `Assets/Game/Resources/BackdropSettings.asset`（メニュー `KaiForest > Backdrop Settings` で選択） | 下の「遠景」 |
| 声の聞こえ方 | `Assets/Game/Resources/VoiceSettings.asset`（メニュー `KaiForest > Voice Settings` で選択） | 下の「声の聞こえ方」 |
| はじまりとおわりの日記 | `Assets/Game/Resources/StorySettings.asset`（メニュー `KaiForest > Story Settings` で選択） | 日記の絵（はじまり・おわり）、ディゾルブ・表示の秒数 |

| ランタンの項目 | 意味 | 初期値 |
|---|---|---|
| Range | 光が届く距離（m）。ここで光が途切れる | 11 |
| Intensity | 明るさ。上げると手前が白飛びしやすい | 22 |
| Daylight | **デバッグ用**。0=夜、1=昼。霧・空・環境光・月明かり・描画距離をまとめて昼に寄せてマップを見やすくする。エディタで再生したときだけ効き、ビルドでは常に夜 | 0 |

- 再生中に Inspector で値を変えると即座に画面に反映され、再生を止めても値は残る
- 点光源は距離の2乗で暗くなるので、Range だけ広げても遠くはほとんど明るくならない
- 霧の濃さは固定（`GameManager.FogDensity` = 0.085。約10m先が半分かすみ、20m先はほぼ見えない）
- このファイルは `Setup Project` では上書きされない。消えた場合はスクリプトの読み込み時に初期値で作り直される

### 声の聞こえ方

お母さんの声（お母さん・ハンカチ・かんざし）と偽物の声（化け物・鹿トラップ）の、距離による小さくなり方。音量 = min(1, (基準距離 ÷ (基準距離＋距離))^Falloff × 1.35) × 端のフェード × 種類ごとの音量 × スタミナによる聞こえにくさ。

| 項目 | 意味 | 初期値 |
|---|---|---|
| Falloff | 小さくなり方。大きいほど早く小さくなる（1 で距離2倍ごとに約-6dB、1.3 で約-8dB） | 1.3 |
| Reference Distance | 基準の距離（m）。これより近いとほぼ最大 | 6 |
| Mother Range | お母さんの声が聞こえる距離（m）。75%から小さくなり始め、ここで無音 | 120 |
| Fake Range | 偽物の声が聞こえる距離（m） | 90 |
| Mother Loudness / Fake Loudness | 種類ごとの音量（倍率） | 1 / 1 |
| Near Distance / Far Distance | この間で、はっきりした声からこもった遠くの声へ切り替わる（m） | 6 / 45 |

初期値での音量の目安：10m 0.38、20m 0.20、30m 0.13、50m 0.07。お母さんの声は約 90m から、偽物の声は約 68m から消え始める。

- 初期配置ではスタートからハンカチまで約60m。範囲や Falloff を強くしすぎると、段階1のスタートで道しるべの声が聞こえなくなるので注意

### 遠景（月夜の山並み）

- 画像は `Assets/Denim/back.png`。カメラを中心にした半径30mの帯に貼り、カメラと一緒に動くので近づけない（本物の遠くの山と同じ）
- 霧の影響を受けず、ほかのものはすべてその手前に描かれる。木や地面がない方向（描画距離40m以内に何もないすき間・空）にだけ見える
- 一周に2枚、1枚おきに左右反転して並べるので継ぎ目は出ない（月は正反対の方角にもう1つある）。月は月明かりの来る方角に合わせてある
- 仕組み：`Backdrop`（`Scripts/World`）が帯を作り、`KaiForest/Backdrop` シェーダー（`Assets/Game/Shaders`）で描く。デバッグ用の昼モードでは霧の色に溶ける

| 遠景の項目 | 意味 | 初期値 |
|---|---|---|
| Visible | 遠景を出す | オン |
| Texture | 画像 | back.png |
| Copies | 一周に並べる枚数（偶数なら継ぎ目なし） | 2 |
| Bottom Angle / Top Angle | 画像の下端・上端の高さ（度。0が目の高さ）。差を 360÷枚数×(縦÷横) にすると絵がつぶれない | -60 / 60 |
| Yaw | 画像を横に回す角度（度） | 0 |
| Brightness | 明るさ | 1 |
| Fog Mix | 霧の色を混ぜる割合。上げると遠景が霧に沈む | 0 |

## ビルド・再生成

Unity 6000.6.6f1 / URP。シーンなどの組み立ては全部コードで行う。

```
# エディタのメニュー: KaiForest > Setup Project / Build WebGL / Build Windows
# コマンドライン（Unity を閉じた状態で）:
Unity.exe -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod KaiForest.EditorTools.KaiForestSetup.CI_SetupBuildWebGL
Unity.exe -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod KaiForest.EditorTools.KaiForestSetup.CI_SetupBuildWindows

# 自動テスト（Windows版。スクリーンショットと results.json を出力）
Builds/Windows/KaiForest.exe -autotest -autotestOut <dir> -screen-width 1280 -screen-height 720 -screen-fullscreen 0
```

WebGL版は `Builds/WebGL` に出力（Gzip＋Decompression Fallback なので普通の静的サーバーで動く）。

### 素材

| | 生成スクリプト | 出力 |
|---|---|---|
| 声（プロト: Haruka） | `gen_tts.ps1` + `build_audio.py` | `Assets/Game/Audio/Voices` など |
| 声（本番: Irodori） | `Tools/audio_irodori` → `sync_irodori_export.py` | `Assets/Game/Audio/Voices` |
| 環境音など | `build_audio.py`（プロト声と一緒に回すとき） | `Assets/Game/Audio/Sfx` |
| 地面・樹皮テクスチャ、見出し文字（タイトル・ゲームクリアなど。`--headlines` で見出しだけ） | `Tools/gen_textures.py` | `Assets/Game/Textures`, `Assets/Game/UI` |
| 鹿トラップの絵（仮。本番の絵で上書きしてよい） | `Tools/gen_deer_scare.py` | `Assets/Game/Resources/UI_DeerScare.png` |
| 化け物のモデル（Tripo、使用中） | `Tools/blender/convert_tripo_monster.py`（元は `Art/Source/Tripo/Monster100_run.glb`） | `Assets/Game/Resources/Monster100/`、`Art/Source/Previews/monster100_*.png` |
| 化け物のモデル（Blender、予備） | `Tools/blender/build_monster.py` | `Art/Source/Monster.blend`、`Assets/Game/Resources/MonsterModel.kfmodel`、`Art/Source/Previews/monster_*.png` |

### 化け物のモデル（Tripo）

Tripo Studio で作ってリグと「run」アニメを付けたモデル。ゲームは `Assets/Game/Resources/Monster100/Monster100.fbx` を使う。

```
"C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background --factory-startup --python Tools/blender/convert_tripo_monster.py -- --preview Art/Source/Previews
```

- 元データは Tripo Studio から**アニメーション付きで GLB 書き出し**したもの（`Art/Source/Tripo/Monster100_run.glb`）。差し替えるときはこのファイルを上書きしてスクリプトを回す（別のファイルは `--src` で指定）
- スクリプトがやること
  - 向きをそろえる（Tripo は横向きで書き出すので、Blender 上で +Y に向ける＝Unity の前方 +Z）
  - 「run」をその場走りにする（Tripo の run は 1 ループで約 1.9m 前に進む。移動はゲーム側が行う）
  - 「idle」（腕を下ろして少し猫背、ゆっくり呼吸）を足す（Tripo の基本姿勢は腕を広げた A ポーズのため）
  - テクスチャ（色・法線）を FBX の隣に JPEG で書き出す
- 取り込み設定は `MonsterModelPostprocessor` が自動で行う（Generic リグ、クリップ名 `idle` / `run` とループ、法線マップ）
- マテリアルは `Monster100/Monster100_Mat.mat`（URP Lit、両面表示）。無ければ Unity がスクリプトを読み込んだとき・FBX を取り込んだときに自動で作られ、FBX のマテリアルはこれに置き換えられる（再生中は置き換えを待つが、ゲーム中はコードでこのマテリアルを付ける）（FBX から自動生成されるマテリアルではテクスチャが付かなかったため）。色味や艶はこのマテリアルを Inspector で調整してよい（作り直されない）
- 動き（`MonsterBody`）
  - 追跡中は `run`。探索・調査・退却で歩くときは `idle` と `run` を半々に混ぜた忍び歩き。止まっているときは `idle`
  - 脚の回転は実際の移動速度に合わせる。`MonsterAI.runAnimPace`（初期値 1.25）を上げると脚が速く回る（1 で足と地面がぴったり合う）
  - 首の不自然な傾きとけいれん、捕まえたときに覆いかぶさる動きは、アニメの上からコードで足している
  - 背の高さは `MonsterAI.height`（初期値 2.6m）。Tripo のモデル（約 1m）をこの高さに拡大する
- Tripo の DCC Bridge で Unity に直接送ったモデル（`Assets/TripoModels/`）はゲームでは使わない。アニメが入らず、マテリアルも URP 用にならないため
- `Monster100` が無いときは Blender のモデル、それも無いときはコードで組む仮の化け物に戻る

### 化け物のモデル（Blender、予備）

Blender（5.1）を画面なしで動かして作る。Blender の MCP 接続は不要。

```
"C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background --factory-startup --python Tools/blender/build_monster.py -- --preview Art/Source/Previews
```

- モデルは **Body / Head / ArmL / ArmR の4パーツ**。各パーツの原点が関節（地面・首の付け根・肩）になっていて、`MonsterAI` の動き（首の傾き・痙攣・腕の振り・覆いかぶさり）がそのまま効く
- `.kfmodel` は Unity 用の書き出し（Y上・+Z前の座標に変換済み）。`KaiModelImporter` が読み込み、パーツごとのメッシュと `ModelRig`（パーツの参照）を持つモデルにする。スクリプトを回し直すと自動で読み込み直される
- 色・発光はゲームのマテリアル（`MaterialLibrary`）を使う。Blender のマテリアル名 `MonsterBody` / `Hair` / `MonsterFace` / `MonsterSkin` / `MonsterEye` で対応づける
- `Monster.blend` を Blender で直接編集した場合は、同じ4パーツ名と原点の位置を保ったまま、スクリプトの `export_kfmodel` 部分だけを使って書き出す（形を変えるだけならスクリプト側の数値を直すのが簡単）
- `MonsterModel.kfmodel` が無いときは、コードで組む仮の化け物（`CharacterFactory.BuildMonster`）に戻る

**声は二系統のどちらか一方。** 本番 Irodori は near/far まで `audio_irodori` が書き、`sync_irodori_export.py` で Voices にコピーする（`build_audio` は使わない）。プロトは `gen_tts.ps1` → `build_audio.py`。

本文フォントは Noto Sans JP（SIL Open Font License）。見出しは HG 行書体で描いた画像なので、フォントファイル自体は同梱していない。
