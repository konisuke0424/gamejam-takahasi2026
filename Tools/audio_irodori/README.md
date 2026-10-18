# ゲームジャム高梁2026 チームD｜音声アセット制作

暗い森を音で探索するゲーム向けに、**お母さん**と**マネッコ妖怪こだま**のセリフ音声を作るサブプロジェクトです。

**ゲーム本体（Unity・マップ・ロジック）の作成・変更は行いません。** 音声の生成・加工・受け渡し専用です。

このフォルダはゲームリポジトリ内の **`Tools/audio_irodori`** です。プロト用の軽い音声は隣の [`Tools/audio/README.md`](../audio/README.md)（Haruka + `build_audio.py`）。**本番の声は Irodori 側だけ**を使い、両方を同時に回す想定ではありません。

---

## 1. 何ができる？

- **Irodori-TTS v4-Small** で、自然な日本語のセリフを音声合成する。
- 採用済みの 40 代女性の基準音声（`assets/reference/mother.wav`）を参照し、母親と妖怪の声質を揃える。
- 妖怪は Irodori に不気味な演技（`irodori.caption`）を指示し、Python（`effects.py`）で低音の重ね合わせ・声の揺れ・語尾低下・短いエコーを**補助的に**足す。**ホワイトノイズは付けない。**
- JSON に登録したセリフ（母・妖怪・たかしなど）を、ID 付きのファイル名で一括出力する。
- WebGL 向けに、各セリフ **near / far の 2 WAV** を `assets/export/` に書き、ゲーム担当は **`sync_irodori_export.py`** で Unity にコピーする。

| 分類 | ファイル名（Irodori 内 ID） | 本数 |
| --- | --- | ---: |
| お母さん | `mother_01.wav` ～ `mother_11.wav` | 11 |
| 妖怪こだま | `monster_01.wav` ～ `monster_08.wav` | 8 |
| 追跡時 | `chase_01.wav` ～ `chase_02.wav` | 2 |
| 捕獲時 | `catch_01.wav` | 1 |
| たかし（独り言） | `takashi_01.wav` ～（台本に追記） | — |

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

### 2-4. たかしの基準音声

`generate.py` の `takashi_XX` は **`assets/reference/takashi.wav` を `--ref-wav` で参照**します。未配置だと生成は失敗します。

お母さん（§2-3）は Irodori だけで声質まで足りますが、たかしは **Irodori で演技 → Python（WORLD）で 10 歳らしい高さ** の二段構成です。妖怪用の `effects.py`（V7 FX）とは別経路です。

参照 WAV の作成はセリフ本線の `src/generate.py` では行わない。**たかし参照音声専用ビルドツール** `scripts/build_takashi_reference_voice_tool.py` を使う（`scripts/` 配下はこの用途だけ）。

#### いまの本番（採用済み）

| 項目 | 内容 |
| --- | --- |
| 本番ファイル | `assets/reference/takashi.wav` |
| 加工前（固定） | `assets/reference/takashi_irodori.wav`（Irodori のみ。比較・再加工の入力） |
| 基準セリフ | `お母さん～どこにいるの？`（`takashi_reference_text`） |
| WORLD パラメータ | `takashi_reference_target_f0_median_hz` **352**、`takashi_reference_formant_alpha` **0.52** |
| 加工後の目安 | 有声 F0 中央値 おおよそ **352 Hz**（話速は変えない） |

セリフ生成時の caption は `takashi_caption_base` ＋ 各シーンの演技メモ（「幼い」「高い」など子供声の指定は **入れない**）。

#### なぜ二段か

| 段階 | ツール | 担当 |
| --- | --- | --- |
| 1 | Irodori（`infer.py` + caption + `--no-ref`） | 若い男性の**自然な話し方・抑揚**（infer に F0 指定はない） |
| 2 | `scripts/takashi_reference.py`（WORLD） | **10 歳男子向けの F0・フォルマント**（基準音 `takashi.wav` だけ） |

#### 採用の経緯（2026-10-18）

1. **Irodori を固定** … 「お母さん～」の呼び方を試行し、`takashi_irodori.wav` を採用（caption は自然な若い男性の演技向け。子供声は後処理に任せる）。
2. **WORLD で複数試作** … 同じ `takashi_irodori.wav` から F0 目標だけ変えた WAV を並べて試聴（例: 315 / 328 / 336 / **352** / 368 / 384 Hz 目標など）。
3. **聴き比べ** … 制作側・第三者向けに、F0 目標の異なる試作 WAV を複数用意して比較した（採用後は削除済み）。
4. **決定** … F0 目標 **352 Hz** の試作を採用。音源を `takashi.wav` に置き、上表のパラメータを `voice_script.json` の正とした。

`assets/reference/` に置くのは **`takashi_irodori.wav` と `takashi.wav` の 2 本だけ**です。

#### 参照ファイル

| ファイル | 意味 |
| --- | --- |
| `takashi_irodori.wav` | **加工前**。演技・「お母さん～」がおかしいときはここだけ聞く |
| `takashi.wav` | **本番参照**（WORLD 後） |

#### よく使うコマンド

**たかしのセリフを 1 本出す**（本番参照は既にある前提）:

```powershell
cd Tools\audio_irodori
py src/generate.py --only takashi_01
```

**たかし参照音声専用ツール**（初回もパラメータ変更後も同じ。Irodori → `takashi_reference.py` → `takashi.wav`）:

```powershell
py scripts/build_takashi_reference_voice_tool.py
```

**`takashi_irodori.wav` はそのまま、WORLD だけやり直す**（`takashi_reference_*` を変えたあと）:

```powershell
py scripts/build_takashi_reference_voice_tool.py --post-only
```

設定の正本は `voice_script.json` の `takashi_reference_*`。専用ツールは処理前後の F0 をコンソールに表示します。

#### 本番パラメータ（`voice_script.json`）

| キー | 採用値の意味 |
| --- | --- |
| `takashi_reference_target_f0_median_hz` | WORLD の F0 目標中央値（**352**） |
| `takashi_reference_formant_alpha` | フォルマント追従の強さ（**0.52**） |
| `takashi_reference_f0_goal_min_hz` / `_max_hz` | クリップ用レンジ（**260** / **400**） |
| `takashi_reference_fine_semitones` | WORLD 後の半音微調整（**0**） |
| `takashi_reference_f0_scale_max` | F0 倍率上限（**2.2**） |

#### セリフ生成（`takashi_XX`）の経緯と対策

**経緯（2026-10-18 の試聴）**

- 参照 `takashi.wav` は基準台詞（「お母さん～…」）の録りなので、セリフ本文と参照・caption がぶつかりやすい。台詞そのものは合っていても、**正しい読みのあとにだけ**別の一言（末尾幻覚）が乗ることがある。
- 独り言は心の声扱いのため、母・妖怪向けの湿ったリバーブはかけない（§5）。

**対策（`generate.py` → `voice_script.json` の `irodori_defaults`）**

| 困りごと | 設定・処理 |
| --- | --- |
| 参照・caption に引っ張られる | `takashi_infer_use_caption`: **false**（infer に長い caption を渡さない）。`takashi_cfg_scale_text` **6** / `takashi_cfg_scale_speaker` **2.5** |
| 末尾に謎の一言 | `takashi_duration_scale` **0.92**（尺を少し短く）。続いて `src/speech_trim.py`（本編と**無音で分かれた**短い末尾だけ切る。`generate.py` が raw 保存直後に実行） |
| 語尾が欠ける | トリムが強すぎる。`speech_trim.py` のパラメータか、再生成前に `--fx-only` で試聴 |

末尾だけ直し直す: `py src/generate.py --only takashi_XX --fx-only`（既存 raw をトリム＋export 再作成）。

**台詞の書き方（安定しやすいパターン）**

- 冒頭の **「えっ、」は読みが安定しにくい**。**「あれ？」** に寄せた（例: `takashi_04`）。
- **一行が短すぎると不安定**になりやすい。語尾に **「どうして？」** など自然な一言を足し、**おおよそ 5 秒前後**の長さになるようにすると安定しやすい（例: `takashi_05`）。
- 台詞を変えたら `py src/generate.py --only takashi_XX` で再生成。

#### うまくいかないとき（基準音 `takashi.wav`）

- **「お母さん」の言い方がおかしい** → `takashi_irodori.wav` を聞く。直すのは `takashi_reference_text`・`takashi_reference_caption`・`seed`・`cfg-scale-caption`（いまは **2.0**）。直ったら WORLD を `--post-only` でかけ直す。
- **高さだけ合わない** → `target_f0_median_hz` を変えて `--post-only`（Irodori は再生成しない）。

Irodori を手で試すときは `voice_script.json` の `takashi_reference_text` / `takashi_reference_caption` を `infer.py` にそのまま渡す。セリフ本編の CFG・末尾トリムは上の **「セリフ生成の経緯と対策」**。

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
- 母・妖怪の text 条件: `irodori_defaults.cfg_scale_text` **4.0**（[Parameter Guide](https://github.com/Aratako/Irodori-TTS/blob/main/docs/parameters.md)）。
- **たかし**の末尾幻覚・CFG・台詞の長さ・「えっ／あれ？」など → **§2-4「セリフ生成の経緯と対策」**。

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
│  ├─ reference/mother.wav    … 母・妖怪の参照（Irodori のみ）
│  ├─ reference/takashi_irodori.wav … たかし・Irodori のみ（§2-4）
│  ├─ reference/takashi.wav   … たかし・本番参照（WORLD 後）
│  ├─ raw/                  … ①（*.wav は .gitignore。未生成時は .gitkeep のみ）
│  └─ export/               … ②③（*.wav / *.prefar.wav も .gitignore。生成後にローカルに増える）
├─ scripts/                 … たかし参照音声専用（`src/` のセリフ本線とは別）
│  ├─ build_takashi_reference_voice_tool.py … 専用ビルドツール（CLI）
│  └─ takashi_reference.py  … 上記ツールが使う WORLD（10歳向け F0・フォルマント）
└─ src/
   ├─ generate.py            … 母・妖怪・たかしのセリフ ①→②→③
   ├─ speech_trim.py         … たかし①直後の末尾幻覚トリム（§2-4）
   ├─ effects.py             … ②（母親・妖怪どちらも通る。妖怪だけ FX）
   ├─ game_voices.py         … ③
   └─ republish_game_voices.py … ② を済ませたあと ③ だけやり直し
```

- 母親は **V7 FX なし**（`fx: {}`）。③ では `game_voices.py` が Haruka 本線と同程度の軽加工のあと near/far を付ける。
- たかしは **V7 FX なし**（`fx: {}`）。③ では **リバーブをかけない**（独り言・心の声）。far は距離クロス用に軽いローパスだけ。
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
