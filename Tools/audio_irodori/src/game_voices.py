"""Irodori export を Unity 用の near / far ペアにする。

build_audio.py の距離用加工と同じパラメータ。Haruka プロト用の妖怪FXは含めない。
たかし（独り言・心の声）は湿ったリバーブをかけず、near は原音に近く far は軽いローパスのみ。
"""
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy import signal

# Unity / build_audio.py と同じサンプルレート
SR = 44100


def prefar_path(near_wav: Path) -> Path:
    """FX 後・距離加工前の中間 WAV（republish はここから near/far を作る）。"""
    return Path(near_wav).with_name(Path(near_wav).stem + ".prefar.wav")


def _butter(x, kind, fc, order=4):
    """バターワースフィルタ（highpass / lowpass など）。"""
    sos = signal.butter(order, fc, btype=kind, fs=SR, output="sos")
    return signal.sosfilt(sos, x, axis=0).astype(np.float32)


def _normalize(x, peak=0.89):
    """ピークを揃えてクリップしにくくする。"""
    m = float(np.abs(x).max())
    return x if m < 1e-9 else x * (peak / m)


def _trim(x, thresh=0.01, pad=0.04):
    """前後の無音を除き、わずかにパッドを残す。"""
    idx = np.where(np.abs(x) > thresh)[0]
    if len(idx) == 0:
        return x
    a = max(0, idx[0] - int(pad * SR))
    b = min(len(x), idx[-1] + int(pad * SR))
    return x[a:b]


def _vary_speed(x, rate):
    """テープのように再生速度をサンプルごとに変える（母親の行ごと微妙な差用）。"""
    pos = np.cumsum(rate)
    pos = pos[pos < len(x) - 1]
    return np.interp(pos, np.arange(len(x)), x).astype(np.float32)


def _reverb(x, seconds=1.4, wet=0.3, dark=2500.0, rng=None):
    """短いインパルス応答で湿り気を足す。near/far の聞こえ分け用。"""
    rng = rng or np.random.default_rng(7)
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = rng.standard_normal(n).astype(np.float32) * np.exp(-t * (6.9 / seconds))
    ir = _butter(ir, "lowpass", dark, 2)
    ir[: int(0.012 * SR)] = 0
    ir /= np.sqrt((ir ** 2).sum())
    tail = signal.fftconvolve(x, ir).astype(np.float32)
    tail = np.pad(tail, (0, len(x) + n - len(tail)))
    dry = np.concatenate([x, np.zeros(n, np.float32)])
    return dry * (1 - wet) + tail * wet * 2.2


def _load_mono(path):
    """WAV をモノラル float32 に読み、必要なら 44.1kHz に揃える。"""
    audio, sr = sf.read(str(path), always_2d=True, dtype="float32")
    x = audio.mean(axis=1).astype(np.float32)
    if sr != SR:
        g = np.gcd(sr, SR)
        x = signal.resample_poly(x, SR // g, sr // g).astype(np.float32)
    return _trim(x)


def _mother_touch(x, line_index):
    """Haruka 本線と同程度の軽い母親用加工（Irodori 本番パス用）。"""
    x = _butter(x, "highpass", 110, 2)
    rate = np.full(len(x) * 2, 1.0 + 0.012 * np.sin(line_index * 1.7), np.float32)
    return _vary_speed(x, rate)


def write_near_far_pair(
    source_wav: Path,
    near_wav: Path,
    character: str,
    line_index: int = 0,
):
    """prefar（FX 後）から Unity 用の near と _far を書き出す。source と near は別パスにすること。"""
    source_wav = Path(source_wav)
    near_wav = Path(near_wav)
    far_wav = near_wav.with_name(near_wav.stem + "_far.wav")
    x = _load_mono(source_wav)
    if character == "mother":
        x = _mother_touch(x, line_index)
    x = _normalize(_trim(x, 0.005))

    if character == "takashi":
        # 独り言・心の声。妖怪用の ghostEcho とは別だが、森の残響も焼き込まない。
        near = _normalize(x, 0.89)
        far = _normalize(_butter(x, "lowpass", 3200, 2) * 0.94, 0.89)
    else:
        # 近距離: 軽いリバーブ。遠距離: ローパス後に強めのリバーブ（WebGL 用の2ファイル運用）。
        near = _normalize(_reverb(x, 1.2, 0.12, 4000), 0.89)
        far = _butter(x, "lowpass", 1100, 4)
        far = _normalize(_reverb(far, 2.0, 0.45, 1500), 0.89)

    sf.write(str(near_wav), near, SR, subtype="PCM_16")
    sf.write(str(far_wav), far, SR, subtype="PCM_16")
