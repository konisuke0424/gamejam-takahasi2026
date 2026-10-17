# ゲーム用音声（どちらか一方）

## A. プロトタイプ（Haruka + build_audio）

```powershell
powershell -ExecutionPolicy Bypass -File Tools/audio/gen_tts.ps1
python Tools/audio/build_audio.py
```

`lines.json` → `raw/*.wav` → `Assets/Game/Audio/Voices`（near/far 含む）と SFX。

## B. 本番（Irodori）

重い生成は [`../audio_irodori/README.md`](../audio_irodori/README.md)。**build_audio は声に使わない。**

```powershell
cd Tools/audio_irodori
py src/generate.py --skip-existing
# 距離ペアだけ直すとき: py src/republish_game_voices.py

cd ../..
python Tools/audio/sync_irodori_export.py
```

`sync` は `assets/export` の 44 本（22 near + 22 far）を `Assets/Game/Audio/Voices` にコピーする。  
`mother_11` → ゲーム ID `mother_found`（`_far` も同様）。

環境音（`Assets/Game/Audio/Sfx`）は通常プロト時の `build_audio.py` 生成物をそのまま使う。本番は声だけ差し替える。
