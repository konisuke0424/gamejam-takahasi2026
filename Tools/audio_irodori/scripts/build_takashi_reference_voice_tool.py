"""たかし参照音声専用ビルドツール（CLI）。

`src/generate.py`（母・妖怪・たかしのセリフ本線）とは別物。
ここでだけ `assets/reference/takashi.wav` と `takashi_irodori.wav` を作る。

手順: Irodori（--no-ref）→ 同フォルダの `takashi_reference.py`（WORLD）→ `takashi.wav`。
初回作成も、`takashi_reference_*` を変えたあとの更新も同じコマンド。

出力（assets/reference/）:
  takashi_irodori.wav … Irodori のみ（固定・比較用）
  takashi.wav         … WORLD 後（generate.py の takashi_XX が --ref-wav で使う）

Usage:
  py scripts/build_takashi_reference_voice_tool.py
  py scripts/build_takashi_reference_voice_tool.py --post-only
"""
import argparse
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

from takashi_reference import (
    ChildVoiceParams,
    apply_child_voice_world,
    format_f0_report,
    load_mono,
    params_from_config,
    write_reference,
)


def run_irodori(d: dict, repo: Path, raw: Path, text: str, caption: str, seed: int) -> None:
    raw.parent.mkdir(parents=True, exist_ok=True)
    cmd = [
        "uv",
        "run",
        "--no-sync",
        "python",
        "infer.py",
        "--hf-checkpoint",
        d["model"],
        "--text",
        text,
        "--caption",
        caption,
        "--no-ref",
        "--cfg-scale-caption",
        str(d.get("takashi_reference_cfg_scale_caption", 3.0)),
        "--duration-scale",
        str(d.get("takashi_reference_duration_scale", 1.0)),
        "--seed",
        str(seed),
        "--num-steps",
        str(d.get("num_steps", 40)),
        "--model-device",
        d.get("device", "cpu"),
        "--codec-device",
        d.get("device", "cpu"),
        "--output-wav",
        str(raw),
    ]
    print("① Irodori（F0 はモデル側では制御不可）:", text)
    subprocess.run(cmd, cwd=repo, check=True)


def world_pass(x, params: ChildVoiceParams, title: str) -> tuple:
    print(title)
    print(
        "   設定: F0 目標 {:.0f} Hz、フォルマント alpha {:.2f}".format(
            params.target_f0_median_hz, params.formant_alpha
        )
    )
    y, meta = apply_child_voice_world(x, params)
    print(format_f0_report("   処理前", meta["before"]))
    print(
        "   適用: F0×{:.3f}  フォルマント×{:.3f}  微調整 {:+.2f} 半音".format(
            meta["f0_ratio"], meta["formant_ratio"], params.fine_semitones
        )
    )
    print(format_f0_report("   処理後", meta["after"]))
    return y, meta


def main():
    ap = argparse.ArgumentParser(
        prog="build_takashi_reference_voice_tool",
        description="たかし参照音声専用ビルドツール（Irodori + takashi_reference.py → takashi.wav）",
    )
    ap.add_argument(
        "--post-only",
        action="store_true",
        help="Irodori を飛ばし、既存 takashi_irodori.wav から takashi.wav だけ作る",
    )
    args = ap.parse_args()

    config = json.loads((ROOT / "config/voice_script.json").read_text(encoding="utf-8"))
    d = config["irodori_defaults"]
    repo = Path(d["repo_path"])
    text = d.get("takashi_reference_text", "お母さん、どこにいるの？")
    caption = d.get(
        "takashi_reference_caption",
        "日本語を話す若い男性の自然な声。落ち着いた声質で、普通の会話に近い話し方。暗い森で本当の母を探している。「お母さん～」は母を遠くから呼ぶ、伸ばした呼びかけ。疑問符で区切らず、一続きの自然な会話として「どこにいるの？」に続ける。朗読調・説明口調にしない。作り声は使わない。",
    )
    seed = int(d.get("takashi_reference_seed", 20261018))
    out = ROOT / d.get("takashi_reference", "assets/reference/takashi.wav")
    raw = ROOT / d.get("takashi_reference_irodori", "assets/reference/takashi_irodori.wav")
    params = params_from_config(d)

    if not args.post_only:
        if not (repo / "infer.py").is_file():
            raise SystemExit(f"Irodori-TTS が見つかりません: {repo}")
        tmp = raw.with_suffix(".tmp.wav")
        run_irodori(d, repo, tmp, text, caption, seed)
        shutil.move(str(tmp), str(raw))
    elif not raw.is_file():
        raise SystemExit(f"--post-only ですが加工前 WAV がありません: {raw}")

    x = load_mono(raw)
    y, _ = world_pass(x, params, "② WORLD（takashi_reference.py）→ takashi.wav")
    write_reference(y, out)
    print("保存（加工前）:", raw)
    print("保存（本番）:", out)


if __name__ == "__main__":
    main()
