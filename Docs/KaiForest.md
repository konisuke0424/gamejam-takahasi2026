# まねごえの森（仮） — KaiForest

GameJam プロトタイプ。仕様: Obsidian `Projects/GameJam.md`

森で迷子になった少年カイが、出口で待つお母さんの声を頼りに森を抜ける一人称ホラー。
森にはお母さんの声をまねる化け物がいて、本物と偽物の声を聞き分けることが攻略の鍵になる。

- お母さんに触れたらクリア / 化け物に捕まったらゲームオーバー
- 出口（お母さんの居場所）は毎回3か所の候補からランダム。残り2か所は壊れた鳥居（偽の出口）
- 森の配置は固定シード（`GameManager.forestSeed`）で、毎回同じ

## 操作（マウスのみ）

| 操作 | 動作 |
|---|---|
| 左ボタンを押している間 | 前へ進む（押したままマウスを動かすと曲がる） |
| 右ボタンを押している間 | うしろへ下がる |
| ホイールボタン押しながらドラッグ | 立ち止まって見回す |
| F1（開発用） | デバッグ表示（距離・AI状態・最後に再生した声） |

## 声の聞き分け（仕様で選んだ「声の違和感」と「言葉の違い」）

| | お母さん | 化け物 |
|---|---|---|
| セリフ | 10種類。毎回違うことを言う（「晩ごはん、カレーだよ」など母だけの言葉がある） | 母の決まり文句3つが中心。たまに壊れた言い回し（「どこに、どこに…」「お母さんは、ここに、いないよ」） |
| 抑揚 | 毎回わずかにピッチが変わる | 毎回まったく同じ抑揚（機械的な繰り返し） |
| 声質 | 素の声 | ピッチの揺れ、半音ずれた二重の声、かすかなうなり、語尾が沈む、どもり |

違和感の強さは `python Tools/audio/build_audio.py --monster-strength 0.6` のように下げると難しくなる。

## AI

- **お母さん**: 出口の周り半径20mを歩きながら 4〜7.5秒おきに呼ぶ。ランタンが見える距離（11m）に来るとカイに歩み寄る。
- **化け物**: Search（カイのいるあたりを漂う）→ Investigate（足音が聞こえたら音の場所へ。歩いていると15m、止まっていると4.5mで気づく）→ Chase（12m以内でランタンが見えたら追う。4.5秒見失うと諦める）。追跡中はときどき一気に距離を詰めてくる。
- パラメータは `MotherAI` / `MonsterAI` / `PlayerController` の public フィールドで調整できる。

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
| 声（Windows音声合成 Haruka） | `Tools/audio/gen_tts.ps1`（セリフ表は `lines.json`） | `Tools/audio/raw/*.wav` |
| 声の加工・環境音 | `Tools/audio/build_audio.py` | `Assets/Game/Audio/` |
| 地面・樹皮テクスチャ、見出し文字 | `Tools/gen_textures.py` | `Assets/Game/Textures`, `Assets/Game/UI` |

**本番の声に差し替えるとき**は、`Tools/audio/raw/<id>.wav` を録音で上書きして `build_audio.py` を回すだけでよい（`gen_tts.ps1` は既存ファイルを上書きしない）。化け物のセリフも、お母さん役の録音を元に加工する前提。

本文フォントは Noto Sans JP（SIL Open Font License）。見出しは HG 行書体で描いた画像なので、フォントファイル自体は同梱していない。
