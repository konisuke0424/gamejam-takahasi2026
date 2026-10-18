# ゲーム用音声（どちらか一方）

| 用途 | フォルダ | 概要 |
| --- | --- | --- |
| **本番** | [`audio_irodori/README.md`](../audio_irodori/README.md) | Irodori-TTS・22 セリフ・near/far・ゲームへの sync。**手順・インストールはこちらの README。** |
| **プロト** | このフォルダ（`gen_tts.ps1`, `build_audio.py`, `lines.json`） | Windows 音声合成で素早く試す。Voices + Sfx を一括生成。 |

本番の声を入れたあと、ゲーム担当がよく使うのはリポジトリルートの:

```powershell
python Tools/audio/sync_irodori_export.py
```

（詳細は `audio_irodori` の README・[`docs/受け渡し.md`](../audio_irodori/docs/受け渡し.md)）
