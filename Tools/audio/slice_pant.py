"""Cut the boy's panting (Assets/Game/Audio/Sfx/player_pant.wav) into one file per breath.

The loop pants too fast; the game now plays the breaths one by one with a pause between them
(PlayerStamina, StaminaSettings.pantGap). The sound itself is not touched: each breath is copied
sample for sample, with a few milliseconds of fade at the cut so it doesn't click.

A breath is a sound and the one right after it (the clip alternates a short "は" and a longer
"ー"); breaths are found from the loudness, so a new take can be cut the same way.

usage: python Tools/audio/slice_pant.py  (from the repo root)
writes Assets/Game/Audio/Sfx/Pant/pant_01.wav ...
"""
import math
import os
import struct
import wave

SRC = "Assets/Game/Audio/Sfx/player_pant.wav"
OUT = "Assets/Game/Audio/Sfx/Pant"
HOP = 0.01          # loudness window (s)
THRESHOLD_DB = -30  # below the clip's peak = between sounds
MIN_SOUND = 0.05    # shorter sounds are clicks, not breath (s)
MERGE_GAP = 0.04    # sounds this close are one sound (s)
PAD = 0.03          # kept before / after each breath (s), less if the next breath is nearer
FADE = 0.008        # fade at the cuts (s)


def main():
    with wave.open(SRC, "rb") as w:
        ch, width, rate, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        assert width == 2, "16-bit wav expected"
        samples = list(struct.unpack("<%dh" % (n * ch), w.readframes(n)))

    hop = int(rate * HOP)
    mono = [sum(samples[i * ch:(i + 1) * ch]) / ch for i in range(n)]
    env = [math.sqrt(sum(v * v for v in mono[i:i + hop]) / hop) for i in range(0, n - hop + 1, hop)]
    peak = max(env)
    loud = [20 * math.log10(max(e, 1e-9) / peak) > THRESHOLD_DB for e in env]

    sounds, start = [], None
    for i, on in enumerate(loud + [False]):
        if on and start is None:
            start = i
        elif not on and start is not None:
            sounds.append([start * HOP, i * HOP])
            start = None
    merged = []
    for s in sounds:
        if merged and s[0] - merged[-1][1] < MERGE_GAP:
            merged[-1][1] = s[1]
        else:
            merged.append(s)
    sounds = [s for s in merged if s[1] - s[0] >= MIN_SOUND]

    breaths = [(sounds[i][0], sounds[min(i + 1, len(sounds) - 1)][1]) for i in range(0, len(sounds), 2)]
    os.makedirs(OUT, exist_ok=True)
    total = n / rate
    for k, (a, b) in enumerate(breaths):
        before = breaths[k - 1][1] if k > 0 else 0.0
        after = breaths[k + 1][0] if k + 1 < len(breaths) else total
        a = max(0.0, a - min(PAD, (a - before) / 2))
        b = min(total, b + min(PAD, (after - b) / 2))
        i0, i1 = int(a * rate), int(b * rate)
        part = samples[i0 * ch:i1 * ch]
        frames = len(part) // ch
        fade = int(FADE * rate)
        for f in range(frames):
            g = min(1.0, f / fade, (frames - 1 - f) / fade)
            if g < 1.0:
                for c in range(ch):
                    part[f * ch + c] = int(part[f * ch + c] * g)
        path = os.path.join(OUT, "pant_%02d.wav" % (k + 1))
        with wave.open(path, "wb") as o:
            o.setnchannels(ch)
            o.setsampwidth(2)
            o.setframerate(rate)
            o.writeframes(struct.pack("<%dh" % len(part), *part))
        print("%s  %.2f-%.2f s  (%.2f s)" % (path, a, b, b - a))


if __name__ == "__main__":
    main()
