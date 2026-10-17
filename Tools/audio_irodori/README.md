# ゲームジャム高梁2026 チームD｜音声アセット制作

暗い森を音で探索するゲーム向けに、**お母さん**と**マネッコ妖怪こだま**のセリフ音声を作るサブプロジェクトです。

**ゲーム本体（Unity・マップ・ロジック）の作成・変更は行いません。** 音声の生成・加工・受け渡し専用です。

このフォルダはゲームリポジトリ内の **`Tools/audio_irodori`** です。プロト用の軽い音声は隣の [`Tools/audio/README.md`](../audio/README.md)（Haruka + `build_audio.py`）。**本番の声は Irodori 側だけ**を使い、両方を同時に回す想定ではありません。

---

## 1. 何ができる？

- **Irodori-TTS v4-Small** で、自然な日本語のセリフを音声合成する。
- 採用済みの 40 代女性の基準音声（`assets/reference/mother.wav`）を参照し、母親と妖怪の声質を揃える。
- 妖怪は Irodori に不気味な演技（`irodori.caption`）を指示し、Python（`effects.py`）で低音の重ね合わせ・声の揺れ・語尾低下・短いエコーを**補助的に**足す。**ホワイトノイズは付けない。**
- JSON に登録した **22 セリフ**を、ID 付きのファイル名で一括出力する。
- WebGL 向けに、各セリフ **near / far の 2 WAV**（計 **44 ファイル**）を `assets/export/` に書き、ゲーム担当は **`sync_irodori_export.py`** で Unity にコピーする。

| 分類 | ファイル名（Irodori 内 ID） | 本数 |
| --- | --- | ---: |
| お母さん | `mother_01.wav` ～ `mother_11.wav` | 11 |
| 妖怪こだま | `monster_01.wav` ～ `monster_08.wav` | 8 |
| 追跡時 | `chase_01.wav` ～ `chase_02.wav` | 2 |
| 捕獲時 | `catch_01.wav` | 1 |

ゲーム側の ID はほぼ同名。**例外:** `mother_11` は Unity では `mother_found`（sync がリネーム）。台本の正本は `config/voice_script.json`、ゲームの ID 一覧は `Tools/audio/lines.json`。

主人公の呼び名は素材上 **「たかし」**（あらすじの「かい」と異なる点はチームで共有）。

---

## 2. 初回だけ必要な事前準備

**Windows PowerShell** で説明します。インターネット接続が必要です。

### 2-1. 必要なソフト

| 用途 | ソフト |
| --- | --- |
| Irodori-TTS の取得 | **Git** |
| このフォルダの `generate.py` / `effects.py` | **Python** |
| Irodori-TTS 専用の実行環境 | **uv** |

```powershell
git --version
py --version
uv --version
```

コマンドが見つからない場合は、各公式からインストールしてください。

### 2-2. Irodori-TTS 本体

```powershell
mkdir D:\tools -Force
cd D:\tools
git clone https://github.com/Aratako/Irodori-TTS.git
cd Irodori-TTS
uv sync --extra cpu
```

すでに `D:\tools\Irodori-TTS` がある場合は **clone 不要**です。別のパスに置いたときは `generate.py --repo` または `voice_script.json` の `irodori_defaults.repo_path` で指定します。

GPU の VRAM が少ない環境向けに、ここでは **CPU 版を標準**にしています（生成は時間がかかることがあります）。

### 2-3. お母さんの基準音声

**`assets/reference/mother.wav` はリポジトリに同梱済み**です。通常は差し替え不要です。

別の声にしたい場合のみ、Irodori-TTS で試作します。

```powershell
cd D:\tools\Irodori-TTS
uv run --no-sync python infer.py `
  --hf-checkpoint Aratako/Irodori-TTS-v4-Small `
  --text "たかしー、どこにいるのー？" `
  --caption "40代前半の日本人女性。落ち着いた少し低めの温かい声。息子を心配して遠くから優しく呼ぶ。自然な母親の話し方。" `
  --no-ref `
  --output-wav mother_candidate.wav
```

採用する場合は `Tools/audio_irodori/assets/reference/mother.wav` に置き換え、**チームと確認してから**コミットしてください。

---

## 3. このフォルダ用の Python 依存

ゲームリポジトリを clone したあと、**`Tools/audio_irodori` に移動**して実行します。

```powershell
cd path\to\gamejam-takahasi2026\Tools\audio_irodori
py -m pip install -r requirements.txt
```

> Irodori 本体の `uv sync` と、このフォルダの `pip install` は**別の環境**です。**両方必要**です。

---

## 4. 音声を生成する

コマンドは **`Tools/audio_irodori` 直下**で実行します（Irodori-TTS のフォルダではありません）。

```powershell
# ① JSON だけ確認（モデル読み込み・音声生成なし）
py src/generate.py --dry-run

# ② お母さんを 1 本試す
py src/generate.py --only mother_02

# ③ 妖怪を 1 本試す
py src/generate.py --only monster_02

# ④ 全 22 セリフ（near/far 込み。既に揃っている行はスキップ）
py src/generate.py --skip-existing
```

- 初回は Hugging Face からモデル取得が走ることがあります。
- 1 本ごとに Irodori の `infer.py` を起動するため、**22 本は時間がかかります**。
- すべて上書きする場合は `py src/generate.py`（`--skip-existing` なし）。
- 別パス例: `py src/generate.py --repo "E:\AI\Irodori-TTS" --only mother_02`

`--skip-existing` は、その ID の **near と `_far` が両方あるとき**だけスキップします。

---

## 5. 出力ファイルの場所

### 1 セリフあたり何ができるか（例: `mother_02`）

処理の順番は **常にこの通り**です。

```text
assets/raw/mother_02.wav           … ① Irodori が infer で書く（参照 mother.wav）
assets/export/mother_02.prefar.wav … ② effects.py（母親は fx なしでも PCM 整形。妖怪は V7 FX）
assets/export/mother_02.wav        … ③ game_voices.py の near（リバーブ等）
assets/export/mother_02_far.wav    … ③ の far
```

`sync_irodori_export.py` がゲームにコピーするのは **③ の near と far だけ**（`.prefar.wav` はコピーしない）。

### フォルダ全体（リポジトリに含まれるもの）

```text
Tools/audio_irodori/
├─ README.md
├─ AGENTS.md
├─ requirements.txt
├─ config/voice_script.json
├─ docs/                    … [`受け渡し.md`](docs/受け渡し.md)、[`V7-FX設計.md`](docs/V7-FX設計.md)
├─ assets/
│  ├─ reference/mother.wav    … 全セリフ共通の参照声（`assets/reference/`。音声ファイルはここだけ）
│  ├─ raw/                  … ①（*.wav は .gitignore。未生成時は .gitkeep のみ）
│  └─ export/               … ②③（*.wav / *.prefar.wav も .gitignore。生成後にローカルに増える）
└─ src/
   ├─ generate.py            … ①→②→③ をまとめて実行
   ├─ effects.py             … ②（母親・妖怪どちらも通る。妖怪だけ FX）
   ├─ game_voices.py         … ③
   └─ republish_game_voices.py … ② を済ませたあと ③ だけやり直し
```

- 母親は **V7 FX なし**（`fx: {}`）。③ では `game_voices.py` が Haruka 本線と同程度の軽加工のあと near/far を付ける。
- **左右・シーン内の 3D 定位**はゲーム側。アセットに「どこから聞こえるか」は焼き込まない（near/far は**距離クロスフェード用の 2 バリアント**）。

### ゲームリポジトリに載せる

生成後、**リポジトリルート**で:

```powershell
python Tools/audio/sync_irodori_export.py
```

→ `Assets/Game/Audio/Voices/` に 44 本コピー（`mother_11` → `mother_found` など）。**`build_audio.py` は本番声では使いません。**

詳細: [`docs/受け渡し.md`](docs/受け渡し.md)

---

## 6. セリフや演技を調整する

| 変更したい内容 | 場所 |
| --- | --- |
| セリフ・ID・ファイル名 | `voice_script.json` の `text` / `id` / `filename` |
| Irodori の演技 | `irodori.caption`（妖怪は短く具体的に） |
| 妖怪の後加工 | `fx.doubleVoice`, `fx.doublePitch`, `fx.warble`, `fx.tailDrop`, `fx.ghostEcho`, `fx.stutterAt` など |
| ノイズ | `noise.enabled` は `false`、`noise.level` は `0` を維持 |

FX だけ変えて AI 音声を作り直さない場合:

```powershell
py src/generate.py --fx-only --only monster_02
py src/generate.py --fx-only
```

（`assets/raw/` に元 WAV が必要。prefar → near/far まで更新されます。）

距離ペア（near/far）だけ直す場合:

```powershell
py src/republish_game_voices.py
```

（`.prefar.wav` から再計算。無ければ `raw` から prefar を作ってから near/far を書きます。）

**既存 ID は振り直さない。** `mother_02` と `mother_02.wav` を一致させ、ゲーム担当との受け渡しを単純に保つ。

FX の考え方: [`docs/V7-FX設計.md`](docs/V7-FX設計.md)

---

## 7. よくあるトラブル

| 状況 | 確認すること |
| --- | --- |
| `uv` が見つからない | uv のインストールと PATH |
| `infer.py` が見つからない | Irodori を clone したか、`--repo` / `repo_path` が正しいか |
| `mother.wav` が見つからない | `assets/reference/mother.wav` があるか |
| `ModuleNotFoundError` | `Tools/audio_irodori` で `pip install -r requirements.txt` したか |
| 再生成しても変わらない | `--skip-existing` を外したか、`--only` で 1 本指定したか |
| `--fx-only` で失敗 | 該当 WAV が `assets/raw/` にあるか |
| ゲームに声が入らない | `sync_irodori_export.py` をルートから実行したか、export に near と `_far` があるか |

---

## 8. チーム制作での注意

- **ゲーム本体・マップ・ゲームロジックには手を入れない**（[`AGENTS.md`](AGENTS.md)）。
- 台詞・名前・演出の変更はチームで共有する。
- 参照音声・生成音声は、利用する TTS モデルのライセンス・利用条件を確認して配布する。
- JSON と Python の検証が通っても、**抑揅・感情・妖怪らしさは必ず試聴**する。
