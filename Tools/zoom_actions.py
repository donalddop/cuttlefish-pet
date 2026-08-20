# /// script
# requires-python = ">=3.11"
# dependencies = ["pillow"]
# ///
"""Blow up a few actions for close inspection: uv run zoom_actions.py idle,swim

Draws each frame on a checkerboard with the anchor point marked in red, so it is
obvious whether the contact point sits where the pose needs it (feet on the floor,
arm tips on the ceiling).
"""

import json
import os
import sys
from PIL import Image, ImageDraw

SPR = os.path.join(os.path.dirname(__file__), "..", "CuttlefishPet", "Assets", "sprites")
OUT = os.path.join(os.path.dirname(__file__), "zoom.png")
Z = 5

names = (sys.argv[1] if len(sys.argv) > 1 else "idle").split(",")
meta = json.load(open(os.path.join(SPR, "animations.json")))

rows = []
for name in names:
    m = meta[name]
    sheet = Image.open(os.path.join(SPR, m["file"])).convert("RGBA")
    frames = [sheet.crop((i * m["frameW"], 0, (i + 1) * m["frameW"], m["frameH"]))
              for i in range(m["frames"])]
    rows.append((name, m, frames))

fw = max(m["frameW"] for _, m, _ in rows) * Z
cols = max(len(f) for _, _, f in rows)
sheet = Image.new("RGBA", (cols * fw, len(rows) * fw), (255, 255, 255, 255))
d = ImageDraw.Draw(sheet)

# checkerboard so transparent areas are obvious
for cy in range(0, sheet.height, 16):
    for cx in range(0, sheet.width, 16):
        if (cx // 16 + cy // 16) % 2:
            d.rectangle([cx, cy, cx + 15, cy + 15], fill=(226, 226, 232, 255))

for r, (name, m, frames) in enumerate(rows):
    ax, ay = m["anchor"]
    for c, f in enumerate(frames):
        x0, y0 = c * fw, r * fw
        sheet.alpha_composite(f.resize((fw, fw), Image.NEAREST), (x0, y0))
        px, py = x0 + ax * Z, y0 + ay * Z
        d.line([(px - 9, py), (px + 9, py)], fill=(230, 40, 40, 255), width=2)
        d.line([(px, py - 9), (px, py + 9)], fill=(230, 40, 40, 255), width=2)
    d.text((r and 4 or 4, r * fw + 4), name, fill=(20, 20, 20, 255))

sheet.save(OUT)
print("wrote", os.path.abspath(OUT), "-- red cross = anchor (contact point)")
