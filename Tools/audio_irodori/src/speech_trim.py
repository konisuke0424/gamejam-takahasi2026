"""Irodori 生成後の余計な末尾発話を落とす（幻覚の短い一言など）。"""
from __future__ import annotations

from pathlib import Path

import librosa
import numpy as np
import soundfile as sf

SR = 44100


def _fade_out_tail(y: np.ndarray, fade_samples: int) -> np.ndarray:
    fade = min(int(fade_samples), len(y))
    if fade <= 0:
        return y
    ramp = np.linspace(1.0, 0.0, fade, dtype=np.float32)
    out = y.copy()
    out[-fade:] *= ramp
    return out


def trim_isolated_tail(
    x: np.ndarray,
    sr: int = SR,
    top_db: float = 30.0,
    min_gap_sec: float = 0.05,
    max_tail_sec: float = 1.2,
    fade_ms: float = 22.0,
    pre_hallucination_ms: float = 4.0,
) -> np.ndarray:
    """無音で本編と離れた、短い末尾区間だけ削除する。

    本編（例: 「ハンカチだ」）は librosa の main 区間に任せ、切りは幻覚区間直前。
    """
    x = np.asarray(x, dtype=np.float32).ravel()
    if len(x) < sr // 10:
        return x
    intervals = librosa.effects.split(x, top_db=top_db)
    if len(intervals) < 2:
        return x
    main_start, main_end = int(intervals[-2][0]), int(intervals[-2][1])
    tail_start, tail_end = int(intervals[-1][0]), int(intervals[-1][1])
    gap = tail_start - main_end
    tail_len = tail_end - tail_start
    if gap < int(min_gap_sec * sr) or tail_len > int(max_tail_sec * sr):
        return x

    # 幻覚の先頭（tail_start）手前で切る。本編 main_end より前には戻らない。
    shave = int(pre_hallucination_ms * sr / 1000.0)
    cut = max(main_end, tail_start - shave)
    cut = min(cut, len(x))
    y = _fade_out_tail(x[:cut], int(fade_ms * sr / 1000.0))
    return y


def trim_trailing_hallucination_file(
    path: Path,
    sr: int = SR,
    **kwargs,
) -> bool:
    """WAV を読み、末尾トリムして上書き。変更時 True。"""
    path = Path(path)
    audio, file_sr = sf.read(str(path), always_2d=True, dtype="float32")
    x = audio.mean(axis=1)
    if file_sr != sr:
        x = librosa.resample(x, orig_sr=file_sr, target_sr=sr).astype(np.float32)
    y = trim_isolated_tail(x, sr=sr, **kwargs)
    if len(y) >= len(x) - 8:
        return False
    sf.write(str(path), y, sr, subtype="PCM_16")
    return True
