"""Generates seamless ground/bark textures and the brush-style headline images.

Headline text is rendered to PNG from an installed Japanese font (the font file itself is
NOT shipped with the game, only the rendered image). Body text in-game uses Noto Sans JP.

Usage: python Tools/gen_textures.py [--headlines]   (--headlines: only the headline images)
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

PROJ = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT_TEX = os.path.join(PROJ, "Assets", "Game", "Textures")
OUT_UI = os.path.join(PROJ, "Assets", "Game", "UI")
FONT_DIRS = [r"C:\Windows\Fonts", os.path.expandvars(r"%LOCALAPPDATA%\Microsoft\Windows\Fonts")]
HEADLINE_FONTS = ["HGRGY.TTC", "HGRGE.TTC", "yumindb.ttf", "msmincho.ttc"]

rng = np.random.default_rng(5)


def periodic_noise(size, beta, aniso=(1.0, 1.0)):
    """FFT noise is periodic by construction, so it tiles seamlessly."""
    fy = np.fft.fftfreq(size)[:, None] * aniso[1]
    fx = np.fft.fftfreq(size)[None, :] * aniso[0]
    f = np.sqrt(fx ** 2 + fy ** 2)
    f[0, 0] = 1.0
    spec = (rng.standard_normal((size, size)) + 1j * rng.standard_normal((size, size))) / f ** beta
    spec[0, 0] = 0
    n = np.real(np.fft.ifft2(spec))
    n -= n.min()
    return n / n.max()


def save_rgb(path, arr):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(np.clip(arr * 255, 0, 255).astype(np.uint8), "RGB").save(path)


def ground(size=512):
    base = periodic_noise(size, 1.3)
    fine = periodic_noise(size, 0.6)
    leaves = periodic_noise(size, 0.2)
    soil = np.array([0.38, 0.30, 0.22])
    moss = np.array([0.27, 0.33, 0.17])
    litter = np.array([0.52, 0.37, 0.22])
    m = np.clip((base - 0.45) * 3, 0, 1)[..., None]
    col = soil * (1 - m) + moss * m
    spots = (leaves > 0.72)[..., None] * 0.6
    col = col * (1 - spots) + litter * spots
    col *= (0.75 + 0.5 * fine)[..., None]
    save_rgb(os.path.join(OUT_TEX, "T_Ground.png"), col)


def bark(size=512):
    streak = periodic_noise(size, 1.1, aniso=(1.0, 0.08))
    detail = periodic_noise(size, 0.7)
    base = np.array([0.44, 0.32, 0.25])
    col = base * (0.55 + 0.6 * streak[..., None]) * (0.8 + 0.35 * detail[..., None])
    save_rgb(os.path.join(OUT_TEX, "T_Bark.png"), col)


def find_font():
    for name in HEADLINE_FONTS:
        for d in FONT_DIRS:
            p = os.path.join(d, name)
            if os.path.exists(p):
                return p
    raise SystemExit("no Japanese headline font found")


def headline(name, text, size, color, glow):
    font = ImageFont.truetype(find_font(), size)
    l, t, r, b = font.getbbox(text)
    pad = size // 2
    w, h = r - l + pad * 2, b - t + pad * 2
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    glow_layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(glow_layer).text((pad - l, pad - t), text, font=font, fill=glow)
    glow_layer = glow_layer.filter(ImageFilter.GaussianBlur(size / 9))
    img.alpha_composite(glow_layer)
    ImageDraw.Draw(img).text((pad - l, pad - t), text, font=font, fill=color)
    os.makedirs(OUT_UI, exist_ok=True)
    img.save(os.path.join(OUT_UI, name))
    print("ui", name, img.size)


def rich_headline(name, text, size, top, bottom, outline, glow):
    """A showier headline: gold-ish vertical gradient fill, dark brush outline, warm glow and a drop shadow."""
    font = ImageFont.truetype(find_font(), size)
    stroke = max(2, size // 28)
    l, t, r, b = font.getbbox(text, stroke_width=stroke)
    pad = size // 2
    w, h = r - l + pad * 2, b - t + pad * 2
    at = (pad - l, pad - t)

    def mask(stroke_width=0):
        m = Image.new("L", (w, h), 0)
        ImageDraw.Draw(m).text(at, text, font=font, fill=255, stroke_width=stroke_width, stroke_fill=255)
        return m

    fill_mask, outline_mask = mask(), mask(stroke)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    # Drop shadow, then the warm glow around the letters.
    shadow = Image.new("RGBA", (w, h), (0, 0, 0, 200))
    shadow.putalpha(outline_mask.filter(ImageFilter.GaussianBlur(size / 14)).point(lambda a: int(a * 0.8)))
    img.alpha_composite(shadow, (size // 40, size // 28))
    halo = Image.new("RGBA", (w, h), glow[:3] + (0,))
    halo.putalpha(outline_mask.filter(ImageFilter.GaussianBlur(size / 6)).point(lambda a: int(a * glow[3] / 255)))
    img.alpha_composite(halo)
    img.alpha_composite(halo)
    # Brush outline, then the gradient fill inside it.
    edge = Image.new("RGBA", (w, h), outline)
    edge.putalpha(outline_mask)
    img.alpha_composite(edge)
    ys = np.linspace(0.0, 1.0, h)[:, None, None]
    top_c, bottom_c = np.array(top, float), np.array(bottom, float)
    grad = top_c * (1 - ys) + bottom_c * ys
    grad = np.broadcast_to(grad, (h, w, 3))
    fill = Image.fromarray(np.clip(grad, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    fill.putalpha(fill_mask)
    img.alpha_composite(fill)
    os.makedirs(OUT_UI, exist_ok=True)
    img.save(os.path.join(OUT_UI, name))
    print("ui", name, img.size)


if __name__ == "__main__":
    if "--headlines" not in sys.argv:
        ground()
        bark()
    headline("UI_Title.png", "まねごえの森", 200, (235, 230, 220, 255), (200, 40, 30, 150))
    headline("UI_Clear.png", "おかあさんに、あえた", 150, (255, 240, 215, 255), (255, 170, 80, 170))
    headline("UI_GameOver.png", "つかまった", 190, (190, 20, 20, 255), (90, 0, 0, 200))
    rich_headline("UI_GameClear.png", "ゲームクリア", 220, (255, 246, 210), (214, 140, 52), (48, 22, 10, 255), (255, 170, 70, 230))
    print("textures done")
