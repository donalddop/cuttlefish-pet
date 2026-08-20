# /// script
# requires-python = ">=3.11"
# dependencies = ["pillow"]
# ///
"""Reproduce exactly what the app composites onto a cuttlefish.

Layer order matches Rendering/SpriteRenderer.Update: the hue-shifted body, then the
skin pattern clipped to the body alpha, then the iridescent sheen on top. Use it to
judge and tune colours without chasing pets around the screen.

  uv run preview_skin.py [skin_strength] [sheen_strength]
"""

import colorsys
import json
import os
import sys
from PIL import Image

SPR = os.path.join(os.path.dirname(__file__), "..", "CuttlefishPet", "Assets", "sprites")
OUT = os.path.join(os.path.dirname(__file__), "skin_preview.png")
Z = 3

# Keep in sync with Rendering/Palette.cs
PALETTES = [
    ("pearl", 322, 0.42, 1.04), ("opal", 250, 0.50, 1.00), ("violet", 236, 0.85, 0.98),
    ("plum", 264, 0.90, 0.72), ("indigo", 214, 0.85, 0.88), ("azure", 190, 0.95, 1.02),
    ("teal", 158, 1.00, 0.95), ("emerald", 120, 0.95, 0.92), ("moss", 96, 0.80, 0.68),
    ("ink", 212, 0.60, 0.28), ("coral", -20, 1.30, 1.00), ("sand", 0, 1.00, 1.00),
]
PATTERNS = ["skin_spots", "skin_mottle", "skin_bands", "skin_reticulate", "skin_flecks"]

SKIN = float(sys.argv[1]) if len(sys.argv) > 1 else 0.40
SHEEN = float(sys.argv[2]) if len(sys.argv) > 2 else 0.14


def recolour(img, hue_shift, sat, val):
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            h = ((h * 360 + hue_shift) % 360) / 360
            r2, g2, b2 = colorsys.hsv_to_rgb(h, min(1, s * sat), min(1, v * val))
            px[x, y] = (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)
    return out


def overlay(base, texture, strength):
    """Texture clipped to the body silhouette, blended at `strength` — as WPF does."""
    tex = texture.convert("RGBA").resize(base.size, Image.LANCZOS)
    tex_px, base_px = tex.load(), base.load()
    out = base.copy()
    out_px = out.load()
    for y in range(base.height):
        for x in range(base.width):
            ba = base_px[x, y][3]
            if ba == 0:
                continue
            tr, tg, tb, ta = tex_px[x, y]
            k = (ta / 255) * strength
            if k <= 0:
                continue
            br, bg, bb, _ = base_px[x, y]
            out_px[x, y] = (round(br * (1 - k) + tr * k),
                            round(bg * (1 - k) + tg * k),
                            round(bb * (1 - k) + tb * k), ba)
    return out


meta = json.load(open(os.path.join(SPR, "animations.json")))
m = meta["swim"]
sheet = Image.open(os.path.join(SPR, m["file"])).convert("RGBA")
frame = sheet.crop((0, 0, m["frameW"], m["frameH"]))

patterns = [Image.open(os.path.join(SPR, p + ".png")).convert("RGBA") for p in PATTERNS]
sheen_img = Image.open(os.path.join(SPR, "skin_sheen.png")).convert("RGBA")

S = m["frameW"] * Z
label = 80
cols = len(PATTERNS) + 1
sheet_out = Image.new("RGBA", (label + cols * S, len(PALETTES) * S), (38, 40, 48, 255))
from PIL import ImageDraw
d = ImageDraw.Draw(sheet_out)
d.text((6, 4), f"skin={SKIN} sheen={SHEEN}", fill=(255, 255, 255, 255))

for row, (name, hue, sat, val) in enumerate(PALETTES):
    y = row * S
    d.text((6, y + S // 2), name, fill=(235, 235, 235, 255))
    body = recolour(frame, hue, sat, val)
    # first column: colour only, no skin
    sheet_out.alpha_composite(body.resize((S, S), Image.NEAREST), (label, y))
    for col, pat in enumerate(patterns):
        comp = overlay(body, pat, SKIN)
        comp = overlay(comp, sheen_img.crop((0, 0, 128, 128)), SHEEN)
        sheet_out.alpha_composite(comp.resize((S, S), Image.NEAREST), (label + (col + 1) * S, y))

sheet_out.save(OUT)
print("wrote", os.path.abspath(OUT), "| column 1 = plain colour, then each pattern")
