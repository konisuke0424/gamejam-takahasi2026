"""たかし（10歳）用参照音声の声質調整（F0 分析 + WORLD 再合成）。

同フォルダの `build_takashi_reference_voice_tool.py` が Irodori の出力に本処理をかけ、`takashi.wav` を作る。
設定は `voice_script.json` の `takashi_reference_*`。

Irodori-TTS v4-Small は caption で子供声を指定しても F0 が成人域に寄ることが多い。
再生速度変更や一括ピッチシフトだけでは不自然になりやすいため、
有声区間の F0 を目標中央値へ寄せ、スペクトル包絡（フォルマント）を部分的に追従させる。
"""
from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any

import librosa
import numpy as np
import pyworld as pw
import soundfile as sf

SR = 44100


@dataclass
class F0Stats:
    """有声フレームの F0 統計（Hz）。"""

    median: float
    mean: float
    p10: float
    p90: float
    voiced_ratio: float


@dataclass
class ChildVoiceParams:
    target_f0_median_hz: float = 300.0
    f0_goal_min_hz: float = 250.0
    f0_goal_max_hz: float = 350.0
    f0_scale_max: float = 2.25
    formant_alpha: float = 0.55
    fine_semitones: float = 0.0
    f0_floor: float = 63.0
    f0_ceil: float = 800.0


def load_mono(path: Path) -> np.ndarray:
    audio, sr = sf.read(str(path), always_2d=True, dtype="float32")
    x = audio.mean(axis=1)
    if sr != SR:
        x = librosa.resample(x, orig_sr=sr, target_sr=SR).astype(np.float32)
    return x


def normalize_peak(x: np.ndarray, peak: float = 0.89) -> np.ndarray:
    m = float(np.abs(x).max())
    return x if m < 1e-9 else x * (peak / m)


def analyze_f0(x: np.ndarray, fs: int = SR, f0_floor: float = 63.0, f0_ceil: float = 800.0) -> F0Stats:
    """WORLD で F0 を推定し、有声区間の統計を返す。"""
    xd = x.astype(np.float64)
    frame_period = 5.0
    f0, _t = pw.harvest(xd, fs, f0_floor=f0_floor, f0_ceil=f0_ceil, frame_period=frame_period)
    f0 = pw.stonemask(xd, f0, _t, fs)
    voiced = f0[f0 > 1.0]
    if len(voiced) == 0:
        return F0Stats(0.0, 0.0, 0.0, 0.0, 0.0)
    return F0Stats(
        median=float(np.median(voiced)),
        mean=float(np.mean(voiced)),
        p10=float(np.percentile(voiced, 10)),
        p90=float(np.percentile(voiced, 90)),
        voiced_ratio=float(len(voiced) / max(len(f0), 1)),
    )


def _warp_formants(sp: np.ndarray, fs: int, ratio: float) -> np.ndarray:
    """スペクトル包絡を周波数方向に伸縮（ratio>1 でフォルマント上げ）。抑揚用の F0 とは独立。"""
    if abs(ratio - 1.0) < 1e-3:
        return sp
    n_bins = sp.shape[1]
    src_f = np.linspace(0.0, fs / 2.0, n_bins)
    out = np.empty_like(sp)
    for i in range(sp.shape[0]):
        warped_f = np.clip(src_f * ratio, 0.0, fs / 2.0)
        out[i] = np.interp(src_f, warped_f, sp[i], left=sp[i, 0], right=sp[i, -1])
    return out


def _world_analysis(x: np.ndarray, fs: int, p: ChildVoiceParams) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    xd = np.ascontiguousarray(x.astype(np.float64).ravel())
    _fs = int(fs)
    f0, t = pw.harvest(
        xd,
        _fs,
        f0_floor=float(p.f0_floor),
        f0_ceil=float(p.f0_ceil),
        frame_period=5.0,
    )
    f0 = pw.stonemask(xd, f0.astype(np.float64), t.astype(np.float64), _fs)
    return xd, f0, t


def apply_child_voice_world(x: np.ndarray, p: ChildVoiceParams, fs: int = SR) -> tuple[np.ndarray, dict[str, Any]]:
    """F0 を目標域へ、フォルマントを alpha 付きで追従させて再合成。話速は維持。"""
    _fs = int(fs)
    xd, f0, t = _world_analysis(x, _fs, p)
    before = analyze_f0(x, fs, p.f0_floor, p.f0_ceil)

    voiced = f0 > 1.0
    if not np.any(voiced) or before.median <= 0:
        y = xd
        meta = {"f0_ratio": 1.0, "formant_ratio": 1.0, "before": before, "after": before}
        return y.astype(np.float32), meta

    target = float(np.clip(p.target_f0_median_hz, p.f0_goal_min_hz, p.f0_goal_max_hz))
    f0_ratio = target / before.median
    if before.median >= p.f0_goal_min_hz:
        f0_ratio = min(f0_ratio, 1.0)
    f0_ratio = float(np.clip(f0_ratio, 1.0, p.f0_scale_max))

    formant_ratio = 1.0 + (f0_ratio - 1.0) * float(np.clip(p.formant_alpha, 0.0, 1.0))

    f0_new = f0.copy()
    f0_new[voiced] = f0[voiced] * f0_ratio

    sp = pw.cheaptrick(xd, f0, t, _fs)
    ap = pw.d4c(xd, f0, t, _fs)
    sp = _warp_formants(sp, _fs, formant_ratio)
    y = pw.synthesize(f0_new.astype(np.float64), sp, ap, _fs).astype(np.float64)

    if abs(p.fine_semitones) > 1e-3:
        y = librosa.effects.pitch_shift(y.astype(np.float32), sr=fs, n_steps=p.fine_semitones).astype(np.float64)

    after = analyze_f0(y.astype(np.float32), fs, p.f0_floor, p.f0_ceil)
    meta = {
        "f0_ratio": f0_ratio,
        "formant_ratio": formant_ratio,
        "target_median_hz": target,
        "before": before,
        "after": after,
    }
    return y.astype(np.float32), meta


def params_from_config(d: dict[str, Any]) -> ChildVoiceParams:
    """voice_script.json の irodori_defaults から後処理パラメータを読む。"""
    return ChildVoiceParams(
        target_f0_median_hz=float(d.get("takashi_reference_target_f0_median_hz", 300.0)),
        f0_goal_min_hz=float(d.get("takashi_reference_f0_goal_min_hz", 250.0)),
        f0_goal_max_hz=float(d.get("takashi_reference_f0_goal_max_hz", 400.0)),
        f0_scale_max=float(d.get("takashi_reference_f0_scale_max", 2.25)),
        formant_alpha=float(d.get("takashi_reference_formant_alpha", 0.55)),
        fine_semitones=float(d.get("takashi_reference_fine_semitones", 0.0)),
        f0_floor=float(d.get("takashi_reference_f0_floor", 63.0)),
        f0_ceil=float(d.get("takashi_reference_f0_ceil", 800.0)),
    )


def write_reference(processed: np.ndarray, out: Path) -> None:
    out.parent.mkdir(parents=True, exist_ok=True)
    sf.write(str(out), normalize_peak(processed), SR, subtype="PCM_16")


def format_f0_report(label: str, s: F0Stats) -> str:
    if s.median <= 0:
        return f"{label}: 有声 F0 なし"
    return (
        f"{label}: 中央値 {s.median:.0f} Hz "
        f"(mean {s.mean:.0f}, P10-P90 {s.p10:.0f}-{s.p90:.0f}, 有声率 {s.voiced_ratio * 100:.0f}%)"
    )
