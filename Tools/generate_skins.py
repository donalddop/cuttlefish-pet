# /// script
# requires-python = ">=3.11"
# dependencies = ["pillow"]
# ///
"""Skin textures laid over the cuttlefish body at runtime.

Real cuttlefish skin is never flat: chromatophore dots, mottled blotches and bold
bands sit on top of the base colour, with an iridophore sheen shifting over all of
it. Each texture here is masked to the body silhouette by the renderer, so one set
works for every pose. The sheen is wide and tileable so it can be panned sideways
for a moving rainbow.
"""

import math
import os
import random
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "CuttlefishPet", "Assets", "sprites")
S = 128          # texture size (stretched over a 64px frame)
PREVIEW = os.path.join(os.path.dirname(__file__), "skins.png")


def dorsal_fade(img, top=1.0, bottom=0.22):
    """Cuttlefish wear their markings on the back; the belly stays pale."""
    a = img.getchannel("A")
    px = a.load()
    for y in range(img.height):
        k = top + (bottom - top) * (y / (img.height - 1))
        for x in range(img.width):
            px[x, y] = int(px[x, y] * k)
    img.putalpha(a)
    return img


def spots(seed=1):
    """Chromatophore dots, the everyday resting pattern."""
    rng = random.Random(seed)
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(48):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        r = rng.uniform(3, 9)
        a = rng.randint(95, 195)
        d.ellipse([x - r, y - r * 0.78, x + r, y + r * 0.78], fill=(52, 30, 26, a))
    for _ in range(20):  # a few pale ones between the dark, for depth
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        r = rng.uniform(2, 5)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 248, 232, rng.randint(70, 140)))
    return dorsal_fade(img.filter(ImageFilter.GaussianBlur(0.9)))


def mottle(seed=2):
    """Big soft blotches — broken-up camouflage."""
    rng = random.Random(seed)
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(22):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        rx, ry = rng.uniform(10, 26), rng.uniform(7, 19)
        a = rng.randint(80, 165)
        dark = rng.random() < 0.62
        col = (42, 26, 22, a) if dark else (255, 248, 230, int(a * 0.85))
        d.ellipse([x - rx, y - ry, x + rx, y + ry], fill=col)
    return dorsal_fade(img.filter(ImageFilter.GaussianBlur(3.4)))


def bands(seed=3):
    """Bold transverse bars — the display they flash when showing off."""
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for i in range(4):
        x = 12 + i * (S - 24) / 3.2
        w = 11 + (i % 2) * 5
        d.rounded_rectangle([x - w, -10, x + w, S + 10], radius=8, fill=(34, 22, 26, 205))
        d.rounded_rectangle([x + w, -10, x + w + 5, S + 10], radius=3,
                            fill=(255, 250, 235, 120))   # bright edge next to each bar
    return dorsal_fade(img.filter(ImageFilter.GaussianBlur(1.6)), bottom=0.35)


def reticulate(seed=4):
    """Net-like pale lines over dark cells."""
    rng = random.Random(seed)
    img = Image.new("RGBA", (S, S), (40, 25, 24, 120))
    d = ImageDraw.Draw(img)
    for i in range(7):
        y = i * S / 6 + rng.uniform(-5, 5)
        d.line([(0, y), (S, y + rng.uniform(-12, 12))], fill=(255, 248, 228, 185), width=5)
    for i in range(7):
        x = i * S / 6 + rng.uniform(-5, 5)
        d.line([(x, 0), (x + rng.uniform(-12, 12), S)], fill=(255, 248, 228, 185), width=5)
    return dorsal_fade(img.filter(ImageFilter.GaussianBlur(1.6)), bottom=0.3)


def flecks(seed=5):
    """Scattered iridophore sparks."""
    rng = random.Random(seed)
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(70):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        r = rng.uniform(1.8, 4.5)
        tint = rng.choice([(170, 245, 255), (210, 190, 255), (185, 255, 220), (255, 235, 190)])
        d.ellipse([x - r, y - r, x + r, y + r], fill=tint + (rng.randint(140, 235),))
    for _ in range(14):  # dark specks so it is not only sparkle
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        r = rng.uniform(2, 5)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(46, 30, 30, rng.randint(80, 150)))
    return dorsal_fade(img.filter(ImageFilter.GaussianBlur(0.7)), bottom=0.4)


def sheen(width=384):
    """Wide tileable mother-of-pearl gradient, panned sideways for shimmer."""
    img = Image.new("RGBA", (width, S), (0, 0, 0, 0))
    px = img.load()
    for x in range(width):
        # two overlapping hue sweeps so the rainbow never looks like a flat ramp
        t = x / width
        hue = (t * 360 * 2) % 360
        for y in range(S):
            shift = (hue + y / S * 90) % 360
            r, g, b = hsv(shift, 0.55, 1.0)
            # soft vertical falloff keeps the sheen strongest across the middle
            fall = math.sin(math.pi * (y / S)) ** 0.6
            px[x, y] = (r, g, b, int(120 * fall))
    return img.filter(ImageFilter.GaussianBlur(6))


def hsv(h, s, v):
    c = v * s
    x = c * (1 - abs((h / 60) % 2 - 1))
    m = v - c
    r, g, b = [(c, x, 0), (x, c, 0), (0, c, x), (0, x, c), (x, 0, c), (c, 0, x)][int(h // 60) % 6]
    return int((r + m) * 255), int((g + m) * 255), int((b + m) * 255)


SKINS = {
    "skin_spots": spots,
    "skin_mottle": mottle,
    "skin_bands": bands,
    "skin_reticulate": reticulate,
    "skin_flecks": flecks,
}


def main():
    os.makedirs(OUT, exist_ok=True)
    made = []
    for name, fn in SKINS.items():
        img = fn()
        img.save(os.path.join(OUT, f"{name}.png"))
        made.append((name, img))
    sh = sheen()
    sh.save(os.path.join(OUT, "skin_sheen.png"))

    # preview strip on a mid grey so alpha reads correctly
    sheet = Image.new("RGBA", (len(made) * S + sh.width, S + 20), (120, 120, 128, 255))
    d = ImageDraw.Draw(sheet)
    for i, (name, img) in enumerate(made):
        sheet.alpha_composite(img, (i * S, 20))
        d.text((i * S + 4, 5), name.replace("skin_", ""), fill=(255, 255, 255, 255))
    sheet.alpha_composite(sh, (len(made) * S, 20))
    d.text((len(made) * S + 4, 5), "sheen (tileable)", fill=(255, 255, 255, 255))
    sheet.save(PREVIEW)
    print(f"wrote {len(made) + 1} skin textures; preview: {os.path.abspath(PREVIEW)}")


if __name__ == "__main__":
    main()
