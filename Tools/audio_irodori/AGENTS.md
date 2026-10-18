# AI向け指示

- チームDの**音声アセット制作のみ**を担当する。ゲーム・マップ・Unity本体は作成/変更禁止。
- 日本語のコメント・docstringを維持し、新規コードにも付ける。
- `config/voice_script.json` を正とし、セリフやIDを断りなく書き換えない。
- 参照音声: 母は `infer.py` → `mother.wav`（妖怪も同参照）。たかしは専用ツール `scripts/build_takashi_reference_voice_tool.py` + `scripts/takashi_reference.py`（Irodori・caption に子供声を盛らない → WORLD）→ `takashi.wav`。セリフは `src/generate.py`。
- `raw` は無加工。`export/*.prefar.wav` は V7 FX 後、`export/*.wav` と `*_far.wav` はゲーム用 near/far。ゲームへは `Tools/audio/sync_irodori_export.py`（`mother_11` → `mother_found`）。
- Haruka プロト用 `build_audio.py` と本番 Irodori は**どちらか一方**。本番で build_audio を声に使わない。
- 制作中の「たかし」と元のあらすじの「かい」は差異として認識する（台本は `voice_script.json`）。
