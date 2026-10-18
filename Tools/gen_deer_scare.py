"""Placeholder for the deer trap's jump-scare picture: a pale, wrong-looking deer head with huge
antlers, hollow glowing eyes and a gaping mouth, on a transparent background.

  python Tools/gen_deer_scare.py      -> Assets/Game/Resources/UI_DeerScare.png (1024x1024 RGBA)

Replace the PNG with a real illustration any time (same name, or set DeerTrap.scareImage).
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(REPO, "Assets", "Game", "Resources", "UI_DeerScare.png")
S = 1024
random.seed(7)


def antler(draw, x, y, angle, length, width, depth):
    """Branching antler: a crooked main beam with tines splitting off."""
    if depth == 0 or length < 18:
        return
    pts = [(x, y)]
    a = angle
    seg = 6
    for i in range(seg):
        a += random.uniform(-0.18, 0.18)
        x += math.cos(a) * length / seg
        y -= math.sin(a) * length / seg
        pts.append((x, y))
    for i in range(len(pts) - 1):
        w = max(2, int(width * (1 - i / seg * 0.55)))
        draw.line([pts[i], pts[i + 1]], fill=(46, 38, 33, 255), width=w)
        draw.ellipse([pts[i][0] - w / 2, pts[i][1] - w / 2, pts[i][0] + w / 2, pts[i][1] + w / 2], fill=(46, 38, 33, 255))
    for k in (2, 4):
        px, py = pts[k]
        side = 1 if random.random() < 0.5 else -1
        antler(draw, px, py, a + side * random.uniform(0.5, 0.9), length * 0.55, width * 0.6, depth - 1)
    antler(draw, x, y, a + random.uniform(-0.3, 0.3), length * 0.45, width * 0.55, depth - 1)


def main():
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = S / 2

    # Antlers first (behind the head).
    antler(d, cx - 70, 330, math.radians(115), 300, 30, 3)
    antler(d, cx + 70, 330, math.radians(65), 300, 30, 3)

    # Ears, drooping and ragged.
    d.polygon([(cx - 120, 360), (cx - 300, 300), (cx - 250, 390), (cx - 130, 420)], fill=(70, 62, 56, 255))
    d.polygon([(cx + 120, 360), (cx + 300, 300), (cx + 250, 390), (cx + 130, 420)], fill=(70, 62, 56, 255))

    # Long skull-like face, pale, narrowing to the muzzle.
    face = [(cx - 150, 340), (cx + 150, 340), (cx + 165, 470), (cx + 110, 640), (cx + 70, 820), (cx + 40, 890),
            (cx - 40, 890), (cx - 70, 820), (cx - 110, 640), (cx - 165, 470)]
    d.polygon(face, fill=(150, 144, 134, 255))
    inner = [(x * 0.82 + cx * 0.18, y * 0.9 + 600 * 0.1) for x, y in face]
    d.polygon(inner, fill=(196, 190, 178, 255))
    # Bridge of the nose.
    d.polygon([(cx - 26, 420), (cx + 26, 420), (cx + 34, 800), (cx - 34, 800)], fill=(214, 208, 196, 255))

    # Hollow eye sockets with pinpoints of light, and dark streaks running down.
    for sx in (-1, 1):
        ex = cx + sx * 92
        d.ellipse([ex - 58, 440, ex + 58, 528], fill=(18, 12, 10, 255))
        d.polygon([(ex - 30, 510), (ex + 30, 510), (ex + 12, 720), (ex - 8, 700)], fill=(40, 30, 28, 255))
    # Black muzzle and a mouth hanging open with teeth.
    d.ellipse([cx - 60, 800, cx + 60, 880], fill=(14, 10, 10, 255))
    d.polygon([(cx - 55, 880), (cx + 55, 880), (cx + 38, 985), (cx - 38, 985)], fill=(26, 10, 10, 255))
    for i in range(7):
        tx = cx - 45 + i * 15
        d.polygon([(tx, 882), (tx + 11, 882), (tx + 5, 905 + (i % 2) * 10)], fill=(225, 218, 200, 255))

    # Grain and a little softness so it reads as a painting, not vectors.
    arr = np.array(img).astype(np.float32)
    noise = np.random.default_rng(3).normal(0, 14, arr.shape[:2])[..., None]
    arr[..., :3] = np.clip(arr[..., :3] + noise, 0, 255)
    img = Image.fromarray(arr.astype(np.uint8), "RGBA").filter(ImageFilter.GaussianBlur(1.2))

    # Eyes glow on top.
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    for sx in (-1, 1):
        ex = cx + sx * 92
        g.ellipse([ex - 22, 468, ex + 22, 500], fill=(255, 210, 160, 200))
    glow = glow.filter(ImageFilter.GaussianBlur(14))
    pin = ImageDraw.Draw(glow)
    for sx in (-1, 1):
        ex = cx + sx * 92
        pin.ellipse([ex - 7, 478, ex + 7, 490], fill=(255, 250, 235, 255))
    img = Image.alpha_composite(img, glow)

    # A dark halo so it pops out of the scene behind it.
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    alpha = img.split()[3].filter(ImageFilter.GaussianBlur(26))
    shadow.putalpha(alpha.point(lambda a: int(a * 0.85)))
    img = Image.alpha_composite(shadow, img)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
