# /// script
# requires-python = ">=3.11"
# dependencies = ["pillow"]
# ///
"""Preview every chromatophore palette side by side.

Mirrors the hue/saturation/value maths in CuttlefishPet/Rendering/Palette.cs, so
what shows up here is what the app renders. Keep the two lists in sync.
"""

import colorsys
import json
import os
from PIL import Image, ImageDraw

SPR = os.path.join(os.path.dirname(__file__), "..", "CuttlefishPet", "Assets", "sprites")
OUT = os.path.join(os.path.dirname(__file__), "palettes.png")
Z = 3

PALETTES = [
    ("sand", 0, 1.00, 1.00),
    ("coral", -22, 1.30, 1.00),
    ("crimson", -38, 1.15, 0.80),
    ("amber", 18, 1.25, 1.10),
    ("emerald", 108, 0.90, 0.90),
    ("teal", 152, 1.00, 0.95),
    ("azure", 190, 0.95, 1.00),
    ("violet", 232, 0.85, 0.95),
    ("magenta", 292, 0.95, 1.00),
    ("ink", 205, 0.60, 0.30),
    ("pearl", 330, 0.28, 1.18),
]

ACTIONS = ["idle", "swim", "hunt", "ceiling"]


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


meta = json.load(open(os.path.join(SPR, "animations.json")))
frames = []
for name in ACTIONS:
    m = meta[name]
    sheet = Image.open(os.path.join(SPR, m["file"])).convert("RGBA")
    frames.append(sheet.crop((0, 0, m["frameW"], m["frameH"])))

S = 64 * Z
label_w = 90
sheet = Image.new("RGBA", (label_w + len(ACTIONS) * S, len(PALETTES) * S), (46, 48, 56, 255))
d = ImageDraw.Draw(sheet)

for row, (name, hue, sat, val) in enumerate(PALETTES):
    y = row * S
    d.text((8, y + S // 2 - 6), name, fill=(240, 240, 240, 255))
    for col, frame in enumerate(frames):
        tinted = recolour(frame, hue, sat, val).resize((S, S), Image.NEAREST)
        sheet.alpha_composite(tinted, (label_w + col * S, y))

sheet.save(OUT)
print("wrote", os.path.abspath(OUT))
