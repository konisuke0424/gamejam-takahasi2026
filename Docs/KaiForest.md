# まねごえの森（仮） — KaiForest

GameJam プロトタイプ。仕様: Obsidian `Projects/GameJam.md`

森で迷子になった少年カイが、出口で待つお母さんの声を頼りに森を抜ける一人称ホラー。
森にはお母さんの声をまねる化け物がいて、本物と偽物の声を聞き分けることが攻略の鍵になる。

- お母さんに触れたらクリア / 化け物に捕まったらゲームオーバー
- 出口（お母さんの居場所）は `Forest.unity` に置いた鳥居（`Torii_Exit`）1か所。壊れた鳥居（`BrokenTorii`）は偽の出口としての飾り
- 森は `Forest.unity` に置いたオブジェクトそのもの（下の「マップの編集」を参照）

## マップの編集（Forest.unity）

`Assets/Scenes/Forest.unity` を開くと、霧のない明るい状態でマップ全体を編集できる（明かりは編集用で、ゲーム中は消える）。

| オブジェクト | 中身 | 編集のしかた |
|---|---|---|
| `ForestMap` | マップの根元。歩ける範囲（`halfSize`、黄色の枠）を持つ | 範囲を変えたら `Walls` も合わせて動かす |
| `PlayerStart` | カイのスタート地点と向き（緑の球と矢印） | 動かす・回す |
| `Landmarks/Torii_Exit` | 本物の出口。お母さんはこの前方 4m を中心に半径 20m を歩く（水色の円） | 動かす。前方（青い矢印）がカイの来る側 |
| `Landmarks/*` | 地蔵・祠・大杉などの目印、壊れた鳥居 | 動かす・複製・削除。`Assets/Game/Map/Prefabs/Landmarks` から追加 |
| `Trees/Area_*/Cedar_*` | 杉（8種類のプレハブ、約4000本） | 動かす・複製・削除。`Assets/Game/Map/Prefabs/Trees` から追加 |
| `Undergrowth` | 茂み・石・草・枝を区画ごとにまとめたメッシュ | 直接は編集しない。目印や出口を動かしたら `KaiForest > Map > Rebake Undergrowth` で、それらを避けて撒き直す |
| `Ground` / `Walls` | 地面と外周の見えない壁 | 通常は触らない |

- プレハブ（杉・目印・鳥居）は仮の見た目。モデルを差し替えるとシーン上の全部が置き換わる。当たり判定（Collider）は残すこと（AI の視線や移動に使う）
- `KaiForest > Map > Rebuild Forest Scene From Seed` は**手での編集をすべて捨てて**最初の配置に戻す
- `KaiForest > Map > Regenerate Placeholder Prefabs` はプレハブを仮の見た目に作り直す（差し替えたモデルは失われる）
- 化け物の出現位置は、スタートから 75m 以上・お母さんから 55m 以上離れた空き地からランダム

## 操作（マウスのみ）

| 操作 | 動作 |
|---|---|
| マウスを動かす | 見回す（クリック不要。上下は ±10度まで＝`PlayerController.pitchLimit`） |
| 左ボタンを押している間 | 前へ進む |
| 右ボタンを押している間 | うしろへ下がる |
| Esc | カーソルのロックを外す（プレイ中にクリックするとまたロックされる） |
| F1（開発用） | デバッグ表示（距離・AI状態・最後に再生した声） |

プレイ中はカーソルを画面に固定（ポインターロック）して、マウスの動きだけで向きを変える。ブラウザの仕様でロックはクリックしたときにだけかかる。埋め込み先のページ（unityroom など）がポインターロックを許可していないと、ロックされずに画面の端で回れなくなる。

## 声の聞き分け（仕様で選んだ「声の違和感」と「言葉の違い」）

| | お母さん | 化け物 |
|---|---|---|
| セリフ | 10種類。毎回違うことを言う（「晩ごはん、カレーだよ」など母だけの言葉がある） | 母の決まり文句3つが中心。たまに壊れた言い回し（「どこに、どこに…」「お母さんは、ここに、いないよ」） |
| 抑揚 | 毎回わずかにピッチが変わる | 毎回まったく同じ抑揚（機械的な繰り返し） |
| 声質 | 素の声 | ピッチの揺れ、半音ずれた二重の声、かすかなうなり、語尾が沈む、どもり |

上表の聞き分けは **Haruka プロト**（`build_audio.py` の妖怪加工）向け。本番 Irodori は `Tools/audio_irodori` の caption + V7 FX。プロトで違和感を弱めたいときは `python Tools/audio/build_audio.py --monster-strength 0.6` など。

## AI

- **お母さん**: 出口の周り半径20mを歩きながら 4〜7.5秒おきに呼ぶ。ランタンが見える距離（11m）に来るとカイに歩み寄る。
- **化け物**: Search（カイのいるあたりを漂う）→ Investigate（足音が聞こえたら音の場所へ。歩いていると15m、止まっていると4.5mで気づく）→ Chase（12m以内でランタンが見えたら追う。4.5秒見失うと調査に戻る）→ Retreat（10秒追いかけたら諦め、8秒間カイを無視して反対方向へ去ってから探索に戻る）。追跡中はときどき一気に距離を詰めてくる。退却中も、ぶつかるほど近づけば捕まる。
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

| ランタンの項目 | 意味 | 初期値 |
|---|---|---|
| Range | 光が届く距離（m）。ここで光が途切れる | 11 |
| Intensity | 明るさ。上げると手前が白飛びしやすい | 22 |
| Daylight | **デバッグ用**。0=夜、1=昼。霧・空・環境光・月明かり・描画距離をまとめて昼に寄せてマップを見やすくする。エディタで再生したときだけ効き、ビルドでは常に夜 | 0 |

- 再生中に Inspector で値を変えると即座に画面に反映され、再生を止めても値は残る
- 点光源は距離の2乗で暗くなるので、Range だけ広げても遠くはほとんど明るくならない
- 霧の濃さは固定（`GameManager.FogDensity` = 0.085。約10m先が半分かすみ、20m先はほぼ見えない）
- このファイルは `Setup Project` では上書きされない。消えた場合はスクリプトの読み込み時に初期値で作り直される

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
| 地面・樹皮テクスチャ、見出し文字 | `Tools/gen_textures.py` | `Assets/Game/Textures`, `Assets/Game/UI` |
| 化け物のモデル（Blender） | `Tools/blender/build_monster.py` | `Art/Source/Monster.blend`、`Assets/Game/Resources/MonsterModel.kfmodel`、`Art/Source/Previews/*.png` |

### 化け物のモデル

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
