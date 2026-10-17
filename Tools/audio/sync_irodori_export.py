"""Irodori の export（near + far）を Unity Voices に同期する。

Haruka プロト用の Tools/audio/raw には書き込まない。build_audio は使わない。
mother_11 → mother_found（および _far）だけゲーム ID が異なる。

Usage: python Tools/audio/sync_irodori_export.py
"""
import json
import os
import shutil

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.normpath(os.path.join(ROOT, "..", ".."))
EXPORT = os.path.join(PROJ, "Tools", "audio_irodori", "assets", "export")
OUT_VOICE = os.path.join(PROJ, "Assets", "Game", "Audio", "Voices")

# Irodori export の stem → ゲームの voice ID
EXPORT_STEM_TO_GAME_ID = {"mother_11": "mother_found"}


def _game_id_from_export_name(filename):
    if not filename.endswith(".wav"):
        return None
    stem = filename[:-4]
    if stem.endswith("_far"):
        base = stem[:-4]
        suffix = "_far"
    else:
        base = stem
        suffix = ""
    game_base = EXPORT_STEM_TO_GAME_ID.get(base, base)
    return game_base + suffix


def main():
    with open(os.path.join(ROOT, "lines.json"), encoding="utf-8") as f:
        game_ids = {line["id"] for line in json.load(f)["lines"]}

    if not os.path.isdir(EXPORT):
        raise SystemExit(f"export フォルダがありません: {EXPORT}")

    os.makedirs(OUT_VOICE, exist_ok=True)
    copied = 0
    for name in sorted(os.listdir(EXPORT)):
        if not name.endswith(".wav"):
            continue
        game_name = _game_id_from_export_name(name)
        if game_name is None:
            continue
        game_id = game_name.replace("_far", "")
        if game_id not in game_ids:
            print("skip:", name)
            continue
        dest = os.path.join(OUT_VOICE, game_name + ".wav")
        shutil.copy2(os.path.join(EXPORT, name), dest)
        print("copy", game_name + ".wav")
        copied += 1

    missing = []
    for lid in sorted(game_ids):
        if not os.path.isfile(os.path.join(OUT_VOICE, lid + ".wav")):
            missing.append(lid)
        if not os.path.isfile(os.path.join(OUT_VOICE, lid + "_far.wav")):
            missing.append(lid + "_far")
    if missing:
        print("missing in Voices after sync:", ", ".join(missing))
    print(f"done: {copied} files → {OUT_VOICE}")


if __name__ == "__main__":
    main()
