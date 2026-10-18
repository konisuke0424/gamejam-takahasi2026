"""既存の .prefar.wav から near / _far だけ再生成する（Irodori 再実行なし）。

.prefar が無いが raw がある行は process で prefar を作ってから距離ペアを書く。
near 済みの export を入力にしない（二重リバーブを防ぐ）。
"""
import json
import sys
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(Path(__file__).resolve().parent))

from effects import process  # noqa: E402
from game_voices import prefar_path, write_near_far_pair  # noqa: E402


def main():
    config = json.loads((ROOT / "config/voice_script.json").read_text(encoding="utf-8"))
    export = ROOT / "assets/export"
    raw_dir = ROOT / "assets/raw"
    for i, v in enumerate(config["voices"]):
        near = export / v["filename"]
        prefar = prefar_path(near)
        if not prefar.is_file():
            raw = raw_dir / v["filename"]
            if not raw.is_file():
                print("skip (no .prefar.wav or raw):", v["id"])
                continue
            process(raw, prefar, 0, v.get("fx", {}), zlib.crc32(v["id"].encode()))
            print("prefar from raw:", v["id"])
        write_near_far_pair(prefar, near, v["character"], i)
        print("publish", v["id"], "→", v["filename"], "+", v["filename"].replace(".wav", "_far.wav"))


if __name__ == "__main__":
    main()
