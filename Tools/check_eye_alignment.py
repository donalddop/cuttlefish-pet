# /// script
# requires-python = ">=3.11"
# dependencies = ["pillow"]
# ///
"""Composite the eye overlay onto each action exactly as the app does.

The app draws eye.png scaled to 2*eyeRadius centred on the eye anchor, so if the
pupil lands anywhere but inside the eye white here, it is misplaced in-game too.
Writes Tools/eye_check.png: per action, pupil looking left / centre / right,
plus the blink frame.
"""

import json
import os
from PIL import Image

SPR = os.path.join(os.path.dirname(__file__), "..", "CuttlefishPet", "Assets", "sprites")
OUT = os.path.join(os.path.dirname(__file__), "eye_check.png")
ZOOM = 7
ONLY = os.environ.get("ONLY_ACTIONS", "").split(",") if os.environ.get("ONLY_ACTIONS") else None

meta = json.load(open(os.path.join(SPR, "animations.json")))
eye_meta = meta["eye"]
eye_sheet = Image.open(os.path.join(SPR, "eye.png")).convert("RGBA")
ES = eye_meta["frameW"]
eye_frames = [eye_sheet.crop((i * ES, 0, (i + 1) * ES, ES)) for i in range(eye_meta["frames"])]

rows = []
for name, m in meta.items():
    if "eye" not in m or name == "eye":
        continue
    if ONLY and name not in ONLY:
        continue
    ex, ey, er = m["eye"]
    body = Image.open(os.path.join(SPR, m["file"])).convert("RGBA")
    frame = body.crop((0, 0, m["frameW"], m["frameH"]))

    variants = []
    size = max(2, int(round(er * 2)))
    travel = er * 0.34
    for label, (ox, oy, fi) in {
        "left":   (-1, 0, 0),
        "center": (0, 0, 0),
        "right":  (1, -0.5, 0),
        "blink":  (0, 0, 1),
    }.items():
        comp = frame.copy()
        pupil = eye_frames[fi].resize((size, size), Image.LANCZOS)
        comp.alpha_composite(pupil, (int(round(ex + ox * travel - size / 2)),
                                     int(round(ey + oy * travel - size / 2))))
        variants.append(comp)
    rows.append((name, variants))

w = m["frameW"]
sheet = Image.new("RGBA", (4 * w * ZOOM, len(rows) * w * ZOOM), (60, 60, 68, 255))
for r, (name, variants) in enumerate(rows):
    for c, v in enumerate(variants):
        sheet.paste(v.resize((w * ZOOM, w * ZOOM), Image.NEAREST), (c * w * ZOOM, r * w * ZOOM))
sheet.save(OUT)
print("actions checked:", ", ".join(n for n, _ in rows))
print("wrote", os.path.abspath(OUT))
