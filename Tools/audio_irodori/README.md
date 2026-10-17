# ゲームジャム高梁2026 チームD — Irodori-TTS音声アセット（V6）

**ゲーム本体の実装・変更は禁止。音声素材制作専用。**

## 前提

- `D:\tools\Irodori-TTS` にはIrodori-TTSをclone済みで、`uv sync --extra cpu` まで完了している。
- `assets/reference/mother.wav` は採用済みの `mother_40s_test.wav` をそのまま同梱したもの。
- 主人公の音声上の名前は **「たかし」**。元のあらすじの「かい」と異なるため、最終的にチームへ確認すること。
- `config/voice_script.json` は22本のセリフと演出を保持。`tts` は過去のVOICEVOX設定として残すが本スクリプトでは使わない。

配置: ゲームリポジトリ内 `Tools/audio_irodori`（軽量パイプラインは `Tools/audio`）。

## ゲームへの渡し

`generate.py` は `assets/export` に **near と `_far` の 44 本**を書く（距離用加工は `src/game_voices.py`）。Haruka 用の `build_audio.py` は使わない。

```powershell
# リポジトリルートから（生成後）
python Tools/audio/sync_irodori_export.py
```

距離ペアだけ作り直す: `py src/republish_game_voices.py`（入力は `.prefar.wav`。無ければ `raw` から prefar を作る）

`mother_11` / `mother_11_far` だけ sync 時に `mother_found` / `mother_found_far` になる（`voice_script.json` の ID は変更しない）。

## 実行（PowerShell、`Tools/audio_irodori` 直下から）

```powershell
cd Tools/audio_irodori
py -m pip install -r requirements.txt
py src/generate.py --dry-run
py src/generate.py --only mother_02
py src/generate.py --only monster_01
# 2本を聞いて問題なければ全22本
py src/generate.py --skip-existing
```

- `assets/raw/*.wav` は Irodori の元音声。
- `assets/export/*.prefar.wav` は V7 FX 後（距離加工前）。`*.wav` / `*_far.wav` はゲーム用 near/far（`game_voices.py`）。
- `--skip-existing` は **near と `_far` の両方**があるときだけスキップする。
- `--repo D:\tools\Irodori-TTS` で本体フォルダを指定可能。
- GPU 4GBの環境ではまずCPU推奨。`--device cuda` は使える環境でのみ検証する。
- 各行の `irodori.caption` が演技指示。`direction` は人向けメモ。
- 同じ参照音声を使っても生成音声が完全に同一声質になるとは限らず、試聴が必要。
- 初期のモデル読込・生成には時間がかかる。現在の方式は1行ごとに `infer.py` を起動するため、22回のモデル読込が発生する。まず少数で品質検証する。

## JSONの変更

- 母親：`noise.level = 0`、`fx = {}`。
- 妖怪：同じ `mother.wav` を参照し `noise.level` と `fx` で後加工。
- `warble` は声の揺らぎ、`tailDrop` は語尾低下、`down` は全体ピッチ低下、`stutterAt` は0〜1の相対時間に反復を追加。
- `mother_01` 等のIDは変更しない。IDとファイル名は常に一致させる。

## 検証状況

JSONとPython構文、合成済みテスト信号のFX処理を検証。**この環境にはIrodoriモデルがないため22本の音声生成・試聴は未実施**。

## V7：Irodori妖怪演技＋原音重視エフェクト

- 妖怪はユーザー試聴で良かった**短いcaption**を全件に指定。「女性の声をベースにした不気味な妖怪の声。声全体が低く、ガラガラとした低い響きが混じる。語尾は不自然に伸び音程が低下する。人間ではない声。」
- `noise.level=0`、`noise.enabled=false`：**ノイズは一切付加しません。**
- `fx.doubleVoice`（0〜0.6）：4〜6半音下げた声を少量重ねる。`fx.doublePitch`（-9〜0）：重ねる声の音程。
- `fx.warble`（0〜9）：微細な揺れ。`fx.tailDrop`（0〜9）：語尾だけ低音化。`fx.ghostEcho`（0〜0.5）：95ms遅延の薄い声。
- `fx.stutterAt`（0〜1）：従来の相対位置指定の反復（設定があるもののみ）。
- 母親は `fx: {}`（V7 FX なし）。`game_voices` では母親だけ Haruka 本線と同程度の軽加工のあと near/far を付ける。3D 定位はゲーム側。

```powershell
# 必要ライブラリをプロジェクト側に導入
py -m pip install -r requirements.txt
# 設定確認
py src/generate.py --dry-run
# まずmonster_02のみIrodoriで生成して比較
py src/generate.py --only monster_02
# 全素材生成
py src/generate.py --skip-existing
# 既存のraw WAVを使い、TTSを再実行せず加工だけ作り直したいとき
py src/generate.py --fx-only --only monster_02
```

注意：設定を変えた音声を生成し直すときは `--only` を使う。`--fx-only` は `assets/raw/` に該当 WAV が必要（prefar → near/far を更新）。
