"""Builds every game audio asset.

Voices: reads Tools/audio/raw/<id>.wav (TTS or hand-recorded), applies the per-kind
processing and writes a "near" and a "far" variant into Assets/Game/Audio/Voices.
The game cross-fades near/far by distance, because WebGL has no realtime audio filters.

Monster processing is the gameplay tell: pitch warble, a slightly detuned double voice,
an occasional stutter and a tape-like pitch drop at the end. --monster-strength scales it
(lower = harder to tell apart).

SFX: everything else (insects, wind, footsteps, heartbeat, ...) is synthesized here.

Usage: python Tools/audio/build_audio.py [--monster-strength 1.0]

本番 Irodori 音声は Tools/audio_irodori と sync_irodori_export.py を使う（本スクリプトは Haruka プロト用）。
"""
import argparse
import json
import os

import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100
ROOT = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.normpath(os.path.join(ROOT, "..", ".."))
RAW = os.path.join(ROOT, "raw")
OUT_VOICE = os.path.join(PROJ, "Assets", "Game", "Audio", "Voices")
OUT_SFX = os.path.join(PROJ, "Assets", "Game", "Audio", "Sfx")


# ---------------------------------------------------------------- helpers

def load(path):
    sr, x = wavfile.read(path)
    x = x.astype(np.float32)
    if x.ndim > 1:
        x = x.mean(axis=1)
    if np.abs(x).max() > 2.0:
        x /= 32768.0
    if sr != SR:
        g = np.gcd(sr, SR)
        x = signal.resample_poly(x, SR // g, sr // g).astype(np.float32)
    return x


def save(path, x):
    x = np.asarray(x, dtype=np.float32)
    x = np.clip(x, -1.0, 1.0)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    wavfile.write(path, SR, (x * 32767.0).astype(np.int16))


def trim(x, thresh=0.01, pad=0.04):
    idx = np.where(np.abs(x) > thresh)[0]
    if len(idx) == 0:
        return x
    a = max(0, idx[0] - int(pad * SR))
    b = min(len(x), idx[-1] + int(pad * SR))
    return x[a:b]


def normalize(x, peak=0.89):
    m = np.abs(x).max()
    return x if m < 1e-9 else x * (peak / m)


def butter(x, kind, fc, order=4):
    sos = signal.butter(order, fc, btype=kind, fs=SR, output="sos")
    return signal.sosfilt(sos, x, axis=0).astype(np.float32)


def env_adsr(n, a, d, s_level, r):
    """Simple attack/decay/sustain/release envelope in seconds over n samples."""
    t = np.arange(n) / SR
    dur = n / SR
    e = np.ones(n, dtype=np.float32) * s_level
    e[t < a] = t[t < a] / max(a, 1e-6)
    m = (t >= a) & (t < a + d)
    e[m] = 1.0 - (1.0 - s_level) * (t[m] - a) / max(d, 1e-6)
    rel = t > dur - r
    e[rel] *= np.clip((dur - t[rel]) / max(r, 1e-6), 0, 1)
    return e


def smooth_noise(n, rate_hz, rng):
    """Band-limited random curve in [-1, 1] that changes about rate_hz times a second."""
    pts = max(4, int(n / SR * rate_hz) + 4)
    knots = rng.uniform(-1, 1, pts)
    xs = np.linspace(0, n, pts)
    return np.interp(np.arange(n), xs, knots).astype(np.float32)


def vary_speed(x, rate):
    """Tape-style variable speed playback: rate[i] is playback speed at output sample i."""
    pos = np.cumsum(rate)
    pos = pos[pos < len(x) - 1]
    return np.interp(pos, np.arange(len(x)), x).astype(np.float32)


def reverb(x, seconds=1.4, wet=0.3, dark=2500.0, rng=None, stereo_seed=0):
    rng = rng or np.random.default_rng(7 + stereo_seed)
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = rng.standard_normal(n).astype(np.float32) * np.exp(-t * (6.9 / seconds))
    ir = butter(ir, "lowpass", dark, 2)
    ir[: int(0.012 * SR)] = 0  # pre-delay
    ir /= np.sqrt((ir ** 2).sum())
    tail = signal.fftconvolve(x, ir).astype(np.float32)
    tail = np.pad(tail, (0, len(x) + n - len(tail)))
    dry = np.concatenate([x, np.zeros(n, np.float32)])
    return dry * (1 - wet) + tail * wet * 2.2


def loopify(x, fade_s=2.0):
    """Cross-fade the tail into the head so the clip loops seamlessly."""
    f = int(fade_s * SR)
    head, body, tail = x[:f], x[f:-f], x[-f:]
    ramp = np.linspace(0, 1, f, dtype=np.float32)
    if x.ndim == 2:
        ramp = ramp[:, None]
    mixed = tail * (1 - ramp) + head * ramp
    return np.concatenate([body, mixed])


def pan(mono, p):
    """p in [-1, 1] -> stereo with equal power panning."""
    a = (p + 1) * np.pi / 4
    return np.stack([mono * np.cos(a), mono * np.sin(a)], axis=1)


# ---------------------------------------------------------------- voices

def process_mother(x, idx):
    # Keep it clean; tiny per-line speed offset so the lines don't all sit on one pitch.
    x = butter(x, "highpass", 110, 2)
    x = vary_speed(x, np.full(len(x) * 2, 1.0 + 0.012 * np.sin(idx * 1.7), np.float32))
    return x


def process_monster(x, fx, strength, rng):
    warble = fx.get("warble", 1.0) * strength
    down = fx.get("down", 0.6)
    tail_drop = fx.get("tailDrop", 0.0) * strength
    stutter_at = fx.get("stutterAt")

    x = butter(x, "highpass", 90, 2)

    if stutter_at is not None and strength > 0:
        voiced = np.where(np.abs(x) > 0.02)[0]
        if len(voiced):
            c = int(voiced[0] + (voiced[-1] - voiced[0]) * stutter_at)
            g = int(0.075 * SR)
            grain = x[c: c + g] * np.hanning(g).astype(np.float32)
            x = np.concatenate([x[:c], grain, grain, grain * 0.8, x[c:]])

    n_out = int(len(x) * 1.3)
    base = 2 ** (-down / 12.0)
    t = np.arange(n_out) / SR
    rate = base * (1.0
                   + 0.020 * warble * np.sin(2 * np.pi * 5.3 * t)
                   + 0.016 * warble * smooth_noise(n_out, 3.0, rng))
    if tail_drop > 0:
        # Last third of the line sags like a dying tape.
        est_len = len(x) / base
        start = est_len * 0.65
        k = np.clip((np.arange(n_out) - start) / max(est_len - start, 1), 0, 1)
        rate *= 1.0 - 0.20 * tail_drop * k
    main = vary_speed(x, rate.astype(np.float32))

    # Detuned double voice that slowly drifts out of sync.
    rate2 = rate * (2 ** (-1.3 / 12.0))
    double = vary_speed(x, rate2.astype(np.float32))
    n = max(len(main), len(double))
    main = np.pad(main, (0, n - len(main)))
    double = np.pad(double, (0, n - len(double)))
    y = main + double * 0.28 * min(1.0, strength)

    # Rough amplitude modulation gives a faint throaty buzz.
    am = 1.0 - 0.18 * strength + 0.18 * strength * np.sin(2 * np.pi * 37.0 * np.arange(n) / SR)
    y = y * am.astype(np.float32)

    # Breath/hiss that follows the voice envelope.
    env = butter(np.abs(y), "lowpass", 20, 2)
    hiss = butter(rng.standard_normal(n).astype(np.float32), "bandpass", [2500, 7000], 2)
    y = y + hiss * env * 0.25 * strength
    return y


def process_chase(x, fx, strength, rng):
    y = process_monster(x, fx, max(1.0, strength), rng)
    return butter(y, "lowpass", 5000, 2)


def build_voices(strength):
    with open(os.path.join(ROOT, "lines.json"), encoding="utf-8") as f:
        cfg = json.load(f)
    rng = np.random.default_rng(1234)
    for i, line in enumerate(cfg["lines"]):
        src = os.path.join(RAW, line["id"] + ".wav")
        if not os.path.exists(src):
            print("missing raw", src)
            continue
        x = trim(load(src))
        kind = line["kind"]
        fx = line.get("fx", {})
        if kind == "mother":
            y = process_mother(x, i)
        elif kind == "monster":
            y = process_monster(x, fx, strength, rng)
        else:
            y = process_chase(x, fx, strength, rng)
        y = normalize(trim(y, 0.005))

        near = normalize(reverb(y, 1.2, 0.12, 4000), 0.89)
        far = butter(y, "lowpass", 1100, 4)
        far = normalize(reverb(far, 2.0, 0.45, 1500), 0.89)
        save(os.path.join(OUT_VOICE, line["id"] + ".wav"), near)
        save(os.path.join(OUT_VOICE, line["id"] + "_far.wav"), far)
        print("voice", line["id"], f"{len(near) / SR:.2f}s")


# ---------------------------------------------------------------- sfx

def sfx_insects(rng, seconds=32.0):
    n = int(seconds * SR)
    out = np.zeros((n, 2), np.float32)
    t_all = np.arange(n) / SR
    # Suzumushi: sustained "riiin" chirps with fast wing-stroke modulation.
    for k in range(5):
        f0 = rng.uniform(3900, 4700)
        p = rng.uniform(-0.9, 0.9)
        level = rng.uniform(0.04, 0.09)
        mono = np.zeros(n, np.float32)
        t = rng.uniform(0, 1.5)
        while t < seconds - 1:
            d = rng.uniform(0.25, 0.45)
            a, b = int(t * SR), int((t + d) * SR)
            tt = t_all[a:b] - t
            chirp = np.sin(2 * np.pi * (f0 + 30 * tt) * tt) * (0.6 + 0.4 * np.sin(2 * np.pi * 48 * tt))
            mono[a:b] += chirp * env_adsr(b - a, 0.02, 0.05, 0.8, 0.08)
            t += d + rng.uniform(0.5, 1.6)
        out += pan(mono * level, p)
    # Korogi: short "koro koro" pulse trains.
    for k in range(4):
        f0 = rng.uniform(4600, 5400)
        p = rng.uniform(-0.9, 0.9)
        level = rng.uniform(0.03, 0.06)
        mono = np.zeros(n, np.float32)
        t = rng.uniform(0, 1.0)
        while t < seconds - 1:
            pulses = rng.integers(3, 7)
            for j in range(pulses):
                a = int((t + j * 0.055) * SR)
                b = a + int(0.03 * SR)
                tt = t_all[a:b] - t_all[a]
                mono[a:b] += np.sin(2 * np.pi * f0 * tt) * np.hanning(b - a)
            t += pulses * 0.055 + rng.uniform(0.25, 0.9)
        out += pan(mono * level, p)
    air = butter(rng.standard_normal((n, 2)).astype(np.float32), "highpass", 3000, 2) * 0.004
    return normalize(loopify(out + air), 0.5)


def sfx_wind(rng, seconds=32.0):
    n = int(seconds * SR)
    gust = (0.55 + 0.45 * smooth_noise(n, 0.25, rng)) ** 2
    out = []
    for ch in range(2):
        white = rng.standard_normal(n).astype(np.float32)
        brown = np.cumsum(white)
        brown = butter(brown - butter(brown, "lowpass", 0.5, 1), "lowpass", 500, 2)
        brown /= np.abs(brown).max() + 1e-9
        leaves = butter(rng.standard_normal(n).astype(np.float32), "bandpass", [1500, 6000], 2)
        leaves *= (0.4 + 0.6 * np.abs(smooth_noise(n, 9.0, rng))) * 0.12
        out.append((brown * 0.8 + leaves) * gust)
    return normalize(loopify(np.stack(out, axis=1)), 0.45)


def sfx_footstep(rng, variant):
    n = int(0.28 * SR)
    t = np.arange(n) / SR
    rustle = butter(rng.standard_normal(n).astype(np.float32), "bandpass", [700, 6500], 2)
    rustle *= np.exp(-t * rng.uniform(22, 30)) * (t < 0.25)
    crunch = np.zeros(n, np.float32)
    for _ in range(rng.integers(6, 14)):
        i = int(rng.uniform(0.0, 0.12) * SR)
        crunch[i] += rng.uniform(-1, 1)
    crunch = butter(crunch, "highpass", 2500, 2) * 0.8
    thump = np.sin(2 * np.pi * (90 - 40 * t) * t) * np.exp(-t * 45) * 0.5
    y = rustle + crunch + thump
    a = int(0.004 * SR)
    y[:a] *= np.linspace(0, 1, a)
    return normalize(y, 0.6 + 0.1 * variant / 4)


def sfx_heartbeat():
    n = int(0.75 * SR)
    t = np.arange(n) / SR
    y = np.zeros(n, np.float32)
    for start, amp in ((0.0, 1.0), (0.26, 0.65)):
        tt = t - start
        m = tt >= 0
        y[m] += amp * np.sin(2 * np.pi * (62 - 18 * tt[m]) * tt[m]) * np.exp(-tt[m] * 16)
    return normalize(butter(y, "lowpass", 180, 2), 0.9)


def sfx_breath_loop(rng, seconds=2.6):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    noise = rng.standard_normal(n).astype(np.float32)
    inhale = butter(noise, "bandpass", [700, 2200], 2) * np.clip(np.sin(np.pi * t / 1.0), 0, 1) * (t < 1.0)
    ex_env = np.clip(np.sin(np.pi * (t - 1.1) / 1.4), 0, 1) * (t > 1.1)
    exhale = butter(noise, "bandpass", [250, 900], 2) * ex_env * 1.6
    exhale *= 0.7 + 0.3 * np.sin(2 * np.pi * 31 * t)
    y = np.concatenate([inhale + exhale, np.zeros(1, np.float32)])[:n]
    return normalize(y, 0.8)


def sfx_monster_step(rng):
    n = int(0.45 * SR)
    t = np.arange(n) / SR
    thud = np.sin(2 * np.pi * (55 - 20 * t) * t) * np.exp(-t * 12)
    crack = butter(rng.standard_normal(n).astype(np.float32), "bandpass", [900, 5000], 2) * np.exp(-t * 35) * 0.5
    snap_i = int(rng.uniform(0.02, 0.08) * SR)
    crack[snap_i: snap_i + 60] += rng.uniform(-1, 1, 60) * 1.5
    return normalize(thud + crack, 0.9)


def sfx_clear_chime(rng):
    seconds = 7.0
    n = int(seconds * SR)
    t = np.arange(n) / SR
    y = np.zeros(n, np.float32)
    # Furin (wind bell) strikes.
    for k, start in enumerate((0.0, 0.9, 1.7, 3.1)):
        f0 = 1250 * (1.0 + 0.01 * rng.uniform(-1, 1))
        tt = t - start
        m = tt >= 0
        for ratio, decay, amp in ((1.0, 1.6, 1.0), (2.76, 3.5, 0.45), (5.40, 6.0, 0.25), (8.93, 9.0, 0.12)):
            y[m] += amp * np.sin(2 * np.pi * f0 * ratio * tt[m]) * np.exp(-tt[m] * decay) * (0.9 ** k)
    # Warm pad underneath (C major add9), slow swell.
    pad = np.zeros(n, np.float32)
    for f in (130.81, 196.0, 261.63, 329.63, 293.66):
        pad += np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * f * 2.003 * t)
    pad *= np.clip(t / 1.8, 0, 1) * np.clip((seconds - t) / 2.5, 0, 1) * 0.08
    return normalize(y * 0.5 + pad, 0.8)


def sfx_gameover(rng):
    seconds = 3.5
    n = int(seconds * SR)
    t = np.arange(n) / SR
    y = np.zeros(n, np.float32)
    sweep = np.exp(-t * 0.9)
    for f in (110, 116.5, 155.6, 164.8, 233.1):
        ph = 2 * np.pi * np.cumsum(f * (0.6 + 0.4 * sweep)) / SR
        y += signal.sawtooth(ph).astype(np.float32)
    y = butter(y, "lowpass", 2200, 2) * np.exp(-t * 0.8) * 0.3
    burst = butter(rng.standard_normal(n).astype(np.float32), "bandpass", [300, 6000], 2) * np.exp(-t * 6)
    y = y + burst * 0.8
    y[: int(0.005 * SR)] *= np.linspace(0, 1, int(0.005 * SR))
    return normalize(y, 0.95)


def sfx_drone(rng, seconds=24.0):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    out = []
    for ch in range(2):
        y = np.zeros(n, np.float32)
        for f, a in ((55.0, 1.0), (55.0 * 1.498, 0.5), (110.0 * 1.06, 0.18), (82.4, 0.35)):
            det = 1 + 0.003 * (ch * 2 - 1)
            y += a * np.sin(2 * np.pi * f * det * t + rng.uniform(0, 6.28))
        y *= 0.7 + 0.3 * smooth_noise(n, 0.15, rng)
        hiss = butter(rng.standard_normal(n).astype(np.float32), "bandpass", [200, 900], 2) * 0.05
        out.append(y + hiss)
    return normalize(loopify(np.stack(out, axis=1), 3.0), 0.5)


def build_sfx():
    rng = np.random.default_rng(42)
    save(os.path.join(OUT_SFX, "amb_insects.wav"), sfx_insects(rng))
    save(os.path.join(OUT_SFX, "amb_wind.wav"), sfx_wind(rng))
    save(os.path.join(OUT_SFX, "amb_drone.wav"), sfx_drone(rng))
    for i in range(5):
        save(os.path.join(OUT_SFX, f"step_{i + 1:02d}.wav"), sfx_footstep(rng, i))
    for i in range(3):
        save(os.path.join(OUT_SFX, f"monster_step_{i + 1:02d}.wav"), sfx_monster_step(rng))
    save(os.path.join(OUT_SFX, "heartbeat.wav"), sfx_heartbeat())
    save(os.path.join(OUT_SFX, "monster_breath.wav"), sfx_breath_loop(rng))
    save(os.path.join(OUT_SFX, "clear_chime.wav"), sfx_clear_chime(rng))
    save(os.path.join(OUT_SFX, "gameover_sting.wav"), sfx_gameover(rng))
    print("sfx done")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--monster-strength", type=float, default=1.0)
    ap.add_argument("--skip-sfx", action="store_true")
    args = ap.parse_args()
    build_voices(args.monster_strength)
    if not args.skip_sfx:
        build_sfx()
