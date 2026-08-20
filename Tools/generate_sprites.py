# /// script
# requires-python = ">=3.11"
# dependencies = ["pillow"]
# ///
"""Cuttlefish sprite generator v3.

Frames are drawn supersampled at 256x256 and downscaled to 64x64 (LANCZOS) for a
smooth cartoon look. Signature cuttlefish features: an undulating skirt fin, an
animated arm crown, chromatophore displays (passing cloud, zebra), and feeding
tentacles that shoot out on a strike.

The eye is split in two: the body art draws the white + iris, while the pupil lives
in a separate `eye.png` overlay (frame 0 = W-pupil, frame 1 = closed lid) that the
app positions per action, so the pet can track the cursor and blink in any pose.

Output: horizontal strips + animations.json in CuttlefishPet/Assets/sprites/,
plus Tools/preview.png as a contact sheet for review.
"""

import json
import math
import os
from PIL import Image, ImageChops, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "CuttlefishPet", "Assets", "sprites")
PREVIEW = os.path.join(os.path.dirname(__file__), "preview.png")
BIG = 256      # supersample canvas
S = 64         # final frame size
BOTTOM = 216   # ground line on the big canvas

OUTLINE = (92, 58, 41, 255)
MANTLE = (208, 132, 92, 255)
MANTLE_DARK = (186, 110, 74, 255)
BELLY = (243, 210, 172, 255)
FIN = (235, 172, 126, 235)
FIN_EDGE = (205, 138, 96, 255)
ARM = (216, 148, 106, 255)
ARM_DARK = (196, 128, 90, 255)
SPOT = (170, 102, 70, 110)
EYE_WHITE = (252, 250, 242, 255)
IRIS = (120, 88, 60, 255)
PUPIL = (38, 28, 26, 255)
INK = (44, 38, 52, 255)
FLUSH = (232, 146, 146, 255)        # happy/excited colour flush
FLUSH_DARK = (212, 118, 122, 255)
PALE = (238, 228, 214, 255)         # zebra display base
PALE_DARK = (214, 200, 184, 255)
CLOUD = (108, 68, 48, 165)          # passing-cloud band
STRIPE = (58, 42, 38, 225)          # zebra stripe


def curve(p0, p1, p2, n=14):
    pts = []
    for i in range(n + 1):
        t = i / n
        pts.append((
            (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
            (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]))
    return pts


def tapered(d, p0, p1, p2, w0, w1, color):
    pts = curve(p0, p1, p2)
    for i, (x, y) in enumerate(pts):
        r = w0 + (w1 - w0) * (i / (len(pts) - 1))
        d.ellipse([x - r, y - r, x + r, y + r], fill=color)


def blend(img, painter):
    """Composite a translucent pass over the image (ImageDraw would punch holes)."""
    layer = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
    painter(ImageDraw.Draw(layer))
    img.alpha_composite(layer)


def masked_overlay(img, painter, mask_shape):
    """Paint through an ellipse mask so displays stay inside the mantle."""
    layer = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
    painter(ImageDraw.Draw(layer))
    mask = Image.new("L", (BIG, BIG), 0)
    ImageDraw.Draw(mask).ellipse(mask_shape, fill=255)
    layer.putalpha(ImageChops.multiply(layer.split()[3], mask))
    img.alpha_composite(layer)


def tilt_point(pt, center, angle):
    """Where a point ends up after Image.rotate(angle) — measured, not derived."""
    dot = Image.new("L", (BIG, BIG), 0)
    ImageDraw.Draw(dot).ellipse([pt[0] - 3, pt[1] - 3, pt[0] + 3, pt[1] + 3], fill=255)
    box = dot.rotate(angle, center=center, resample=Image.BICUBIC).getbbox()
    return pt if box is None else ((box[0] + box[2]) / 2, (box[1] + box[3]) / 2)


def draw_cuttlefish(
    fin_phase=0.0, arm_sway=0.0, arm_splay=0.0, arms_up=False, arms_dangle=False,
    arms_tucked=False, arms_to_mouth=False, squash=0.0, stretch_x=1.0,
    baked_eye=None, tilt=0.0, fin_amp=7.0, puff=False, wide_eye=False,
    cloud=None, zebra=False, flush=False, tentacles=0.0,
    grip=0, canopy=False, scuff=False,
    sink=0.0, balloon=False, shock=False, ghost=False, display_arms=0.0,
):
    """One 256x256 frame, facing right. Returns (image, eye).

    Normally bottom-aligned (standing). With `grip` > 0 the body hangs from that
    many arms reaching up to the top of the frame, for ceilings and ledges.
    """
    img = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    base, base_dark, belly = MANTLE, MANTLE_DARK, BELLY
    if zebra:
        base, base_dark, belly = PALE, PALE_DARK, (250, 244, 236, 255)
    elif flush:
        base, base_dark, belly = FLUSH, FLUSH_DARK, (250, 216, 216, 255)
    elif ghost:
        base, base_dark, belly = (206, 226, 240, 150), (176, 200, 220, 150), (232, 242, 250, 130)
    elif shock:
        base, base_dark, belly = (252, 250, 232, 255), (236, 226, 178, 255), (255, 255, 248, 255)

    # Fin and arms follow the body, or the whole thing looks half-transformed.
    fin_col, fin_edge_col = FIN, FIN_EDGE
    arm_a, arm_b = ARM, ARM_DARK
    if zebra:
        fin_col, fin_edge_col = (242, 232, 220, 235), PALE_DARK
    elif ghost:
        fin_col, fin_edge_col = (214, 232, 244, 120), (182, 206, 226, 150)
        arm_a, arm_b = (200, 222, 238, 145), (176, 200, 220, 145)
    elif shock:
        fin_col, fin_edge_col = (250, 244, 206, 235), (232, 218, 160, 255)
        arm_a, arm_b = (248, 240, 200, 255), (232, 220, 166, 255)

    h = 92 * (1 - squash * 0.55)
    w = 148 * stretch_x * (1 + squash * 0.25)
    cx = 118
    cy = 150 if grip else BOTTOM - h / 2
    cy += sink * 96  # burrowing down out of sight
    body = (cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2)

    # Arms reaching up to the ceiling/ledge, spread along the top of the mantle.
    if grip:
        for i in range(grip):
            t = i / max(1, grip - 1)
            ax = body[0] + 22 + t * (w - 44)
            wig = math.sin(arm_sway + i * 1.1) * 6
            # bow the control point sideways so the arms curve instead of standing
            # up like table legs
            bow = (t - 0.5) * 34 + wig
            tapered(d, (ax, cy - h * 0.3), (ax + bow, cy - h * 0.95),
                    (ax + wig * 1.6, 34 + (i % 2) * 7), 8, 3.5,
                    ARM if i % 2 else ARM_DARK)
            tip = (ax + wig * 1.6, 34 + (i % 2) * 7)
            d.ellipse([tip[0] - 7, tip[1] - 5, tip[0] + 7, tip[1] + 5], fill=ARM_DARK)

    if canopy:  # fin spread wide as a parachute (kept clear of the frame edge)
        top = cy - h * 1.15
        d.chord([cx - 84, top - 34, cx + 84, top + 44], 180, 360,
                fill=FIN, outline=FIN_EDGE, width=4)
        for sx in (-62, 62):
            d.line([(cx + sx, top + 10), (cx + sx * 0.35, cy - h * 0.45)],
                   fill=FIN_EDGE, width=4)

    # feeding tentacles shoot out from behind the arm crown
    if tentacles > 0:
        reach = 16 + tentacles * 44  # stays inside the frame, reads as a quick jab
        for sgn in (-1, 1):
            ty = cy + sgn * h * 0.10
            tip = (cx + w / 2 + reach, ty + sgn * 14 * (1 - tentacles))
            tapered(d, (cx + w / 2 - 14, ty), (cx + w / 2 + reach * 0.55, ty + sgn * 6),
                    tip, 6, 3, ARM_DARK)
            d.ellipse([tip[0] - 11, tip[1] - 8, tip[0] + 11, tip[1] + 8],
                      fill=ARM, outline=OUTLINE, width=3)

    # skirt fin
    fin_pts = []
    for k in range(72):
        th = k / 72 * 2 * math.pi
        wave = fin_amp * math.sin(5 * th + fin_phase)
        fin_pts.append((cx + (w / 2 + 12 + wave) * math.cos(th),
                        cy + (h / 2 + 10 + wave * 0.7) * math.sin(th)))
    d.polygon(fin_pts, fill=fin_col, outline=fin_edge_col, width=5)

    # mantle
    d.ellipse(body, fill=base_dark, outline=OUTLINE, width=7)
    d.ellipse((body[0] + 4, body[1] + h * 0.18, body[2] - 4, body[3] - 2), fill=base)
    # Counter-shading: a soft pale underside, not a big flat white patch, plus a
    # darker back so the body reads as round instead of a sticker.
    blend(img, lambda ld: ld.ellipse(
        (body[0] + 26, body[1] + h * 0.66, body[2] - 34, body[3] - 6),
        fill=(belly[0], belly[1], belly[2], 165)))
    blend(img, lambda ld: ld.ellipse(
        (body[0] + 6, body[1] + 3, body[2] - 6, body[1] + h * 0.55),
        fill=(28, 16, 20, 46)))

    # No baked-in mottling: the app lays a skin pattern over the body at runtime, so
    # the art stays a clean canvas for whatever chromatophore pattern is active.

    if cloud is not None:  # passing-cloud hunting display
        def paint(ld):
            for ph in cloud:
                bx = body[0] - 30 + ph * (w + 60)
                ld.rounded_rectangle([bx - 17, body[1] - 20, bx + 17, body[3] + 20],
                                     radius=16, fill=CLOUD)
        masked_overlay(img, paint, body)

    if zebra:  # rival display: bold stripes
        def paint(ld):
            for k in range(5):
                bx = body[0] + 14 + k * (w - 28) / 4
                ld.rounded_rectangle([bx - 11, body[1] - 20, bx + 11, body[3] + 20],
                                     radius=10, fill=STRIPE)
        masked_overlay(img, paint, body)

    if scuff:  # friction marks while sliding down a wall
        for i, oy in enumerate((-40, 0, 40)):
            x = body[0] - 16 - (i % 2) * 6
            d.line([(x, cy + oy - 14), (x - 10, cy + oy + 14)],
                   fill=(255, 255, 255, 150), width=4)

    # Courtship display: the arms held up and spread in a showy fan.
    if display_arms > 0:
        base_x = cx + w / 2 - 10
        for i in range(8):
            t = i / 7
            angle = -1.45 + t * 1.15                       # sweeping up and forward
            reach = (58 + t * 26) * display_arms
            fy = cy - h * 0.2 + t * h * 0.35
            tip = (base_x + math.cos(angle) * reach * 0.6 + 26,
                   fy + math.sin(angle) * reach)
            curl = math.sin(arm_sway + i * 0.7) * 9
            tapered(d, (base_x, fy), (base_x + 30, fy + math.sin(angle) * reach * 0.45 + curl),
                    tip, 8, 2.5, ARM if i % 2 else ARM_DARK)

    # arm crown
    base_x = cx + w / 2 - 8
    if not arms_tucked and not grip and display_arms == 0:
        for i in range(6):
            fy = cy - h * 0.18 + i * (h * 0.42 / 5)
            sway = math.sin(arm_sway + i * 0.9) * 7
            if arms_to_mouth:
                p1, p2 = (base_x + 26, fy + 6), (base_x + 6 + sway * 0.5, fy + 16)
            elif arms_dangle:
                p1, p2 = (base_x + 18, fy + 26), (base_x + 10 + sway, fy + 58 + i % 3 * 6)
            elif arms_up:
                p1, p2 = (base_x + 22, fy - 18), (base_x + 30 + sway, fy - 44 - i % 3 * 6)
            else:
                splay = (i - 2.5) * (4 + arm_splay * 7)
                p1 = (base_x + 26, fy + splay * 0.4 + sway * 0.4)
                p2 = (base_x + 48 + sway, fy + splay)
            tapered(d, (base_x, fy), p1, p2, 7, 2.4, ARM if i % 2 else ARM_DARK)

    # eye: white + iris here, pupil comes from the overlay (unless baked)
    ex, ey = cx + w * 0.26, cy - h * 0.10
    er = 30 if wide_eye else 26
    if baked_eye == "closed":
        d.arc([ex - er, ey - er * 0.5, ex + er, ey + er * 0.9], 20, 160, fill=OUTLINE, width=6)
        eye = None
    else:
        d.ellipse([ex - er, ey - er, ex + er, ey + er], fill=EYE_WHITE, outline=OUTLINE, width=4)
        ir = er * 0.62
        d.ellipse([ex - ir, ey - ir, ex + ir, ey + ir], fill=IRIS)
        eye = (ex, ey, er)
        if baked_eye == "open":  # no overlay for this action: draw the pupil in place
            pw = ir * 0.85
            d.line([(ex - pw, ey - pw * 0.1), (ex - pw * 0.5, ey + pw * 0.42), (ex, ey - pw * 0.05),
                    (ex + pw * 0.5, ey + pw * 0.42), (ex + pw, ey - pw * 0.1)],
                   fill=PUPIL, width=int(ir * 0.55), joint="curve")
            eye = None

    if puff:
        def paint_puff(ld):
            for r, ox, a in ((16, -18, 150), (11, -40, 110), (7, -58, 70)):
                px, py = body[0] + ox, cy + h * 0.18
                ld.ellipse([px - r, py - r, px + r, py + r], fill=(220, 230, 240, a))
        blend(img, paint_puff)

    if sink:  # spray of disturbed grit at the surface line
        def paint_grit(ld):
            for i in range(7):
                ang = i / 7 * math.pi
                r = 13 + (i % 3) * 5
                px = cx - 60 + i * 20 + math.cos(ang) * 8
                py = BOTTOM - 14 - math.sin(ang) * 26 * sink
                ld.ellipse([px - r, py - r, px + r, py + r], fill=(214, 196, 172, 205))
        blend(img, paint_grit)

    if balloon:  # a big bubble hauling the pet upward
        def paint_balloon(ld):
            bx, by, br = cx + 6, cy - h * 1.35, 46
            ld.ellipse([bx - br, by - br, bx + br, by + br],
                       outline=(228, 242, 250, 225), width=5, fill=(206, 230, 246, 70))
            ld.ellipse([bx - br * 0.5, by - br * 0.58, bx - br * 0.14, by - br * 0.2],
                       fill=(255, 255, 255, 205))
            ld.line([(bx - 12, by + br - 6), (cx - 6, cy - h * 0.5)],
                    fill=(214, 232, 244, 200), width=3)
        blend(img, paint_balloon)

    if shock:  # zigzag bolts crackling off the body
        for sx in (-1, 1):
            ox = cx + sx * (w / 2 + 22)
            d.line([(ox, cy - 44), (ox + sx * 14, cy - 18), (ox - sx * 8, cy - 6),
                    (ox + sx * 18, cy + 26)], fill=(255, 244, 150, 240), width=5, joint="curve")

    if tilt:
        img = img.rotate(tilt, center=(cx, cy), resample=Image.BICUBIC)
        if eye:
            tx, ty = tilt_point((eye[0], eye[1]), (cx, cy), tilt)
            eye = (tx, ty, eye[2])
    return img, eye


def finish(img):
    return img.resize((S, S), Image.LANCZOS)


def build(frames):
    """frames: list of (bigImage, eye). Returns (small frames, eye of frame 0)."""
    eye = frames[0][1]
    small = [finish(f) for f, _ in frames]
    if eye:
        eye = (eye[0] * S / BIG, eye[1] * S / BIG, eye[2] * S / BIG)
    return small, eye


def zzz(img, phase):
    d = ImageDraw.Draw(img)
    for i in range(2):
        a = 255 if (phase + i) % 2 == 0 else 130
        size, x, y = 26 + i * 10, 190 + i * 22, 96 - i * 30
        d.line([(x, y), (x + size, y), (x, y + size), (x + size, y + size)],
               fill=(235, 235, 250, a), width=7, joint="curve")
    return img


# ---------------- actions ----------------

def a_idle(n=6):
    return [draw_cuttlefish(fin_phase=i / n * 6.28, arm_sway=i / n * 4.4,
                            squash=0.05 + 0.03 * math.sin(i / n * 6.28)) for i in range(n)]


def a_swim(n=8):
    return [draw_cuttlefish(fin_phase=i / n * 12.6, arm_sway=i / n * 6.28, arm_splay=-0.3,
                            stretch_x=1.05, fin_amp=9) for i in range(n)]


def a_fall(n=4):
    return [draw_cuttlefish(fin_phase=i / n * 18.8, arms_up=True, arm_sway=i / n * 12.6,
                            squash=-0.08, tilt=-10 + 6 * math.sin(i / n * 6.28),
                            fin_amp=11, wide_eye=True) for i in range(n)]


def a_drag(n=4):
    return [draw_cuttlefish(fin_phase=i / n * 6.28, arms_dangle=True, arm_sway=i / n * 6.28,
                            squash=-0.12, fin_amp=5) for i in range(n)]


def a_climb(n=6):
    return [draw_cuttlefish(fin_phase=i / n * 9.4, arms_up=True,
                            arm_sway=i / n * 6.28 + math.pi * (i % 2), squash=0.1,
                            tilt=55, fin_amp=6) for i in range(n)]


def a_sit(n=4):
    return [draw_cuttlefish(fin_phase=i / n * 3.14, squash=0.38 + 0.02 * math.sin(i / n * 6.28),
                            arm_splay=0.5, arm_sway=i / n * 1.9, fin_amp=4) for i in range(n)]


def a_sleep(n=4):
    out = []
    for i in range(n):
        img, _ = draw_cuttlefish(fin_phase=i / n * 1.9, squash=0.45 + 0.03 * math.sin(i / n * 6.28),
                                 arms_tucked=True, baked_eye="closed", fin_amp=3)
        out.append((zzz(img, i // 2), None))
    return out


def a_flatten(n=6):
    out = []
    for i in range(n):
        k = i / (n - 1)
        out.append(draw_cuttlefish(fin_phase=k * 4, squash=0.1 + k * 0.62, stretch_x=1 + k * 0.25,
                                   arm_splay=k, baked_eye="closed" if k > 0.6 else "open",
                                   fin_amp=7 * (1 - k) + 2))
    return out


def a_mimic_icon():
    img = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([48, 48, 208, 208], radius=34, fill=MANTLE, outline=OUTLINE, width=5)
    d.arc([100, 120, 156, 160], 20, 160, fill=OUTLINE, width=5)
    return [(img, None)]


def a_ink(n=6):
    out = []
    for i in range(n):
        k = i / (n - 1)
        img = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        r, a = 26 + k * 86, int(235 * (1 - k * 0.8))
        for ox, oy, rf in ((0, 0, 1.0), (-0.7, -0.35, 0.62), (0.65, -0.45, 0.5),
                           (-0.4, 0.55, 0.55), (0.5, 0.5, 0.45)):
            rr, x, y = r * rf, 128 + ox * r, 128 + oy * r
            d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=(INK[0], INK[1], INK[2], a))
        out.append((img, None))
    return out


def a_jump(n=4):
    return [draw_cuttlefish(fin_phase=i / n * 18.8, stretch_x=1.18, squash=-0.15,
                            arm_splay=-0.6, arm_sway=i / n * 6.28, fin_amp=4, puff=True,
                            tilt=8) for i in range(n)]


def a_wiggle(n=6):
    return [draw_cuttlefish(fin_phase=i / n * 12.6, arm_sway=i / n * 15.7, arm_splay=1.1,
                            squash=0.12 + 0.08 * math.sin(i / n * 12.6), wide_eye=True,
                            fin_amp=10) for i in range(n)]


def a_startle(n=2):
    return [draw_cuttlefish(fin_phase=i * 2.5, arm_splay=1.6, arm_sway=i * 3.0, squash=-0.18,
                            wide_eye=True, fin_amp=13) for i in range(n)]


def a_hunt(n=6):
    """Passing-cloud display: dark bands sweep along the body while stalking."""
    return [draw_cuttlefish(fin_phase=i / n * 6.28, arm_splay=-0.5, arm_sway=i / n * 3.1,
                            stretch_x=1.08, squash=0.06, fin_amp=5, wide_eye=True,
                            cloud=[(i / n + k / 3) % 1.0 for k in range(3)]) for i in range(n)]


def a_strike(n=4):
    """Feeding tentacles shoot out at the prey."""
    ext = (0.15, 0.75, 1.0, 0.5)
    return [draw_cuttlefish(fin_phase=i * 1.6, arm_splay=-0.8, stretch_x=1.12, squash=-0.05,
                            fin_amp=6, wide_eye=True, tentacles=ext[i]) for i in range(n)]


def a_zebra(n=4):
    """Rival display: high-contrast stripes, arms flared wide."""
    return [draw_cuttlefish(fin_phase=i / n * 6.28, arm_splay=1.8, arm_sway=i / n * 6.28,
                            squash=-0.1, stretch_x=1.06, fin_amp=12, wide_eye=True,
                            zebra=True) for i in range(n)]


def a_happy(n=6):
    """Pink flush and a bouncy arm flourish — being petted or a good meal."""
    return [draw_cuttlefish(fin_phase=i / n * 12.6, arms_up=(i % 2 == 0), arm_splay=1.2,
                            arm_sway=i / n * 12.6, squash=0.05 + 0.12 * math.sin(i / n * 12.6),
                            fin_amp=11, flush=True) for i in range(n)]


def a_eat(n=4):
    return [draw_cuttlefish(fin_phase=i / n * 6.28, arms_to_mouth=True, arm_sway=i * 2.2,
                            squash=0.18 + 0.1 * math.sin(i / n * 12.6), fin_amp=6)
            for i in range(n)]


def a_stretch(n=4):
    """Waking up: elongate, reach out, shake the fin."""
    return [draw_cuttlefish(fin_phase=i / n * 6.28, arms_up=True, arm_sway=i * 1.1,
                            stretch_x=1.0 + 0.22 * math.sin(i / n * 3.14),
                            squash=0.3 - 0.3 * math.sin(i / n * 3.14), fin_amp=8)
            for i in range(n)]


def a_peek(n=4):
    """Leaning over an edge, arms gripping down, looking below."""
    return [draw_cuttlefish(fin_phase=i / n * 4.7, arms_dangle=True, arm_sway=i / n * 3.1,
                            squash=0.12, tilt=-22, fin_amp=5) for i in range(n)]


def a_ceiling(n=6):
    """Upside-down along the ceiling, six arms walking hand-over-hand."""
    return [draw_cuttlefish(fin_phase=i / n * 9.4, arm_sway=i / n * 6.28, grip=6,
                            squash=0.05, fin_amp=6) for i in range(n)]


def a_hang(n=4):
    """Dangling from a ledge by two arms."""
    return [draw_cuttlefish(fin_phase=i / n * 4.7, arm_sway=i / n * 3.1, grip=2,
                            squash=-0.08, fin_amp=5) for i in range(n)]


def a_slide(n=4):
    """Squeaking down a wall, arms trailing above."""
    return [draw_cuttlefish(fin_phase=i * 1.4, arms_up=True, arm_sway=i * 1.7,
                            squash=0.2, tilt=90, fin_amp=4, scuff=True,
                            wide_eye=True) for i in range(n)]


def a_burrow(n=5):
    """Digging down into the taskbar until only a puff of grit is left."""
    return [draw_cuttlefish(fin_phase=i * 2.1, arm_sway=i * 2.6, arm_splay=0.8,
                            squash=0.25, fin_amp=5, sink=i / (n - 1),
                            baked_eye="closed" if i >= n - 2 else None)
            for i in range(n)]


def a_ghost(n=4):
    """Translucent spirit drifting upward."""
    return [draw_cuttlefish(fin_phase=i / n * 6.28, arms_dangle=True, arm_sway=i / n * 3.1,
                            squash=-0.1, fin_amp=9, ghost=True, baked_eye="closed")
            for i in range(n)]


def a_balloon(n=4):
    """Hauled up by a bubble, arms dangling."""
    return [draw_cuttlefish(fin_phase=i / n * 4.7, arms_dangle=True, arm_sway=i / n * 3.1,
                            squash=0.05, fin_amp=5, balloon=True) for i in range(n)]


def a_shock(n=2):
    """Static jolt: blanched white with bolts crackling off."""
    return [draw_cuttlefish(fin_phase=i * 3.1, arm_splay=1.9, arm_sway=i * 4.0,
                            squash=-0.2, fin_amp=14, wide_eye=True, shock=True)
            for i in range(n)]


def a_court(n=6):
    """Courtship: arms thrown up in a fan while colour ripples along the mantle."""
    # No passing-cloud here: the app flashes the palette during courtship, and bands
    # on top of that just read as a smudge.
    return [draw_cuttlefish(fin_phase=i / n * 9.4, arm_sway=i / n * 4.7,
                            display_arms=0.65 + 0.35 * math.sin(i / n * 3.14),
                            squash=-0.06, stretch_x=1.04, fin_amp=14, wide_eye=True)
            for i in range(n)]


def a_parachute(n=4):
    """Fin spread into a canopy for a slow descent."""
    return [draw_cuttlefish(fin_phase=i / n * 6.28, arms_dangle=True,
                            arm_sway=i / n * 3.1, squash=0.1, canopy=True,
                            fin_amp=4) for i in range(n)]


# ---------------- props ----------------

def prop_eye():
    """Overlay: frame 0 = W-pupil, frame 1 = closed lid. 16px final."""
    big, small = 64, 16
    frames = []

    pupil = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(pupil)
    c, pw = big / 2, big * 0.26
    d.line([(c - pw, c - pw * 0.1), (c - pw * 0.5, c + pw * 0.42), (c, c - pw * 0.05),
            (c + pw * 0.5, c + pw * 0.42), (c + pw, c - pw * 0.1)],
           fill=PUPIL, width=int(big * 0.17), joint="curve")
    d.ellipse([c - pw * 0.3, c - pw * 0.85, c + pw * 0.05, c - pw * 0.45],
              fill=(255, 255, 255, 200))
    frames.append(pupil.resize((small, small), Image.LANCZOS))

    lid = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(lid)
    r = big * 0.46
    d.ellipse([c - r, c - r, c + r, c + r], fill=MANTLE, outline=OUTLINE, width=4)
    d.arc([c - r * 0.75, c - r * 0.3, c + r * 0.75, c + r * 0.8], 15, 165, fill=OUTLINE, width=4)
    frames.append(lid.resize((small, small), Image.LANCZOS))
    return frames, small


def prop_shrimp(n=4):
    """A little shrimp treat, wiggling where it landed. 32px final."""
    big, small = 128, 32
    body_c, shell = (238, 138, 118, 255), (250, 178, 158, 255)
    out = []
    for i in range(n):
        img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        wig = math.sin(i / n * 6.28) * 7
        cx, cy = 62, 74
        for k in range(5):  # segmented tail curling up to the right
            t = k / 4
            x = cx + 8 + t * 40
            y = cy + t * t * 22 - wig * t
            r = 15 - t * 8
            d.ellipse([x - r, y - r, x + r, y + r], fill=shell if k % 2 else body_c,
                      outline=(196, 96, 84, 255), width=2)
        d.ellipse([cx - 22, cy - 18, cx + 12, cy + 16], fill=body_c,
                  outline=(196, 96, 84, 255), width=3)  # head
        d.ellipse([cx - 14, cy - 9, cx - 6, cy - 1], fill=(40, 30, 30, 255))  # eye
        for sgn in (-1, 1):  # antennae
            d.line([(cx - 20, cy - 6), (cx - 44, cy - 22 + sgn * 12 + wig * 0.5)],
                   fill=(206, 116, 100, 255), width=3)
        for k in range(3):  # legs
            d.line([(cx - 4 + k * 9, cy + 12), (cx - 8 + k * 9, cy + 26)],
                   fill=(206, 116, 100, 255), width=3)
        out.append(img.resize((small, small), Image.LANCZOS))
    return out, small


def prop_egg(n=4):
    """A cluster of cuttlefish eggs ("sea grapes"), wobbling then cracking open."""
    big, small = 96, 24
    out = []
    for i in range(n):
        img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        wob = math.sin(i / n * 6.28) * 3
        for ex, ey, er in ((30, 62, 19), (54, 58, 21), (43, 40, 17)):
            d.ellipse([ex - er + wob * 0.4, ey - er, ex + er + wob * 0.4, ey + er],
                      fill=(232, 236, 240, 245), outline=(150, 160, 172, 255), width=3)
            d.ellipse([ex - er * 0.45, ey - er * 0.6, ex - er * 0.05, ey - er * 0.15],
                      fill=(255, 255, 255, 200))
        if i == n - 1:  # a crack in the top egg
            d.line([(36, 34), (44, 44), (39, 50), (50, 56)], fill=(120, 130, 145, 255), width=3)
        out.append(img.resize((small, small), Image.LANCZOS))
    return out, small


def prop_blot(n=3):
    """A small ink splat left behind on a ledge."""
    big, small = 64, 16
    out = []
    for i in range(n):
        img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        a = 215 - i * 25
        d.ellipse([12, 30, 52, 50], fill=(INK[0], INK[1], INK[2], a))
        for ox, oy, r in ((14, 28, 5), (48, 30, 4), (32, 24, 6)):
            d.ellipse([ox - r, oy - r + i, ox + r, oy + r + i],
                      fill=(INK[0], INK[1], INK[2], a))
        out.append(img.resize((small, small), Image.LANCZOS))
    return out, small


def prop_fish(n=4):
    """A little silvery fish to hunt: swims facing right, tail flicking."""
    big, small = 128, 32
    body_c, fin_c = (188, 214, 232, 255), (150, 184, 210, 255)
    out = []
    for i in range(n):
        img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        flick = math.sin(i / n * 6.28) * 12
        cx, cy = 68, 64
        # tail
        d.polygon([(cx - 30, cy), (cx - 56, cy - 18 + flick), (cx - 56, cy + 18 + flick)],
                  fill=fin_c, outline=(96, 128, 156, 255))
        # body
        d.ellipse([cx - 34, cy - 20, cx + 34, cy + 20], fill=body_c,
                  outline=(96, 128, 156, 255), width=3)
        d.ellipse([cx - 20, cy - 2, cx + 30, cy + 18], fill=(224, 238, 248, 255))
        # dorsal fin
        d.polygon([(cx - 8, cy - 19), (cx + 6, cy - 34 + flick * 0.3), (cx + 16, cy - 17)],
                  fill=fin_c, outline=(96, 128, 156, 255))
        # eye
        d.ellipse([cx + 16, cy - 10, cx + 26, cy], fill=(255, 255, 255, 255),
                  outline=(96, 128, 156, 255), width=2)
        d.ellipse([cx + 20, cy - 7, cx + 25, cy - 2], fill=(30, 30, 36, 255))
        out.append(img.resize((small, small), Image.LANCZOS))
    return out, small


def prop_label(n=1):
    """A blurred two-line filename to sit under a cuttlefish posing as a shortcut."""
    big, small = 128, 32
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for y, (x0, x1) in ((34, (26, 102)), (58, (40, 88))):
        d.rounded_rectangle([x0, y, x1, y + 13], radius=6, fill=(238, 240, 245, 190))
    return [img.filter(ImageFilter.GaussianBlur(1.1)).resize((small, small), Image.LANCZOS)], small


def prop_bubble(n=4):
    big, small = 64, 16
    out = []
    for i in range(n):
        img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        r = 14 + i * 5
        c = big / 2
        d.ellipse([c - r, c - r, c + r, c + r], outline=(226, 240, 248, 210), width=4,
                  fill=(210, 232, 245, 60))
        d.ellipse([c - r * 0.55, c - r * 0.62, c - r * 0.12, c - r * 0.2],
                  fill=(255, 255, 255, 190))
        out.append(img.resize((small, small), Image.LANCZOS))
    return out, small


# name -> (builder, fps, loop)
# Frame rates are deliberately unhurried: at a couple of frames a second you can
# actually read each pose instead of watching a blur.
ACTIONS = {
    "idle":       (a_idle,        3.5, True),
    "swim":       (a_swim,        6,   True),
    "fall":       (a_fall,        5,   True),
    "drag":       (a_drag,        4,   True),
    "climb":      (a_climb,       4.5, True),
    "sit":        (a_sit,         2,   True),
    "flatten":    (a_flatten,     4,   False),
    # No sleep action: dozing off is dull to watch, so they never do it.
    "mimic_icon": (a_mimic_icon,  1,   True),
    "ink":        (a_ink,         8,   False),
    "jump":       (a_jump,        6,   True),
    "wiggle":     (a_wiggle,      7,   True),
    "startle":    (a_startle,     5,   True),
    "hunt":       (a_hunt,        4,   True),
    "strike":     (a_strike,      7,   False),
    "zebra":      (a_zebra,       6,   True),
    "happy":      (a_happy,       7,   True),
    "eat":        (a_eat,         5,   True),
    "stretch":    (a_stretch,     3,   False),
    "peek":       (a_peek,        2.5, True),
    "ceiling":    (a_ceiling,     4,   True),
    "hang":       (a_hang,        2.5, True),
    "slide":      (a_slide,       5,   True),
    "burrow":     (a_burrow,      4,   False),
    "ghost":      (a_ghost,       3,   True),
    "balloon":    (a_balloon,     2.5, True),
    "shock":      (a_shock,      10,   True),
    "court":      (a_court,       4,   True),
}

# Contact point per action; default is the foot. Ceiling/ledge poses hang from
# their arm tips at the top of the frame instead.
ANCHORS = {
    "climb": [32, 50],
    "slide": [32, 50],
    "ceiling": [30, 9],
    "hang": [30, 9],
}


def make_icon():
    """A crisp multi-size .ico so the tray entry is recognisably the pet."""
    img, _ = draw_cuttlefish(fin_phase=1.0, arm_splay=0.4, squash=0.05,
                            baked_eye="open", fin_amp=8)
    box = img.getbbox()
    art = img.crop(box)
    side = max(art.size) + 24
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(art, ((side - art.width) // 2, (side - art.height) // 2), art)
    canvas = canvas.resize((256, 256), Image.LANCZOS)
    path = os.path.join(OUT, "..", "app.ico")
    canvas.save(path, sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    return path


# A clutch of eggs has to be spotted from across the screen; a prey fish should
# look like a mouthful, not a rival.
PROP_SCALE = {
    "label": 1.9,
    "egg": 1.9,
    "blot": 1.6,
    "shrimp": 1.4,
    "fish": 1.0,
    "bubble": 1.0,
    "eye": 1.0,
}


def save_strip(name, frames, size):
    strip = Image.new("RGBA", (size * len(frames), size), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        strip.paste(f, (i * size, 0))
    strip.save(os.path.join(OUT, f"{name}.png"))
    return strip


def main():
    os.makedirs(OUT, exist_ok=True)
    meta, sheets = {}, []

    for name, (fn, fps, loop) in ACTIONS.items():
        frames, eye = build(fn())
        sheets.append((name, save_strip(name, frames, S)))
        meta[name] = {
            "file": f"{name}.png", "frameW": S, "frameH": S, "frames": len(frames),
            "fps": fps, "loop": loop, "anchor": ANCHORS.get(name, [30, 55]),
        }
        if eye:
            meta[name]["eye"] = [round(eye[0], 2), round(eye[1], 2), round(eye[2], 2)]

    for name, (frames, size), fps, loop in (
        ("eye", prop_eye(), 1, False),
        ("shrimp", prop_shrimp(), 4, True),
        ("fish", prop_fish(), 6, True),
        ("bubble", prop_bubble(), 6, False),
        ("egg", prop_egg(), 3, True),
        ("blot", prop_blot(), 2, True),
        ("label", prop_label(), 1, True),
    ):
        sheets.append((name, save_strip(name, frames, size)))
        # Things that rest on a ledge hang from their base; free swimmers and
        # effects are positioned by their middle.
        anchor = ([size / 2, size - 2] if name in ("shrimp", "egg", "blot")
                  else [size / 2, size / 2])
        meta[name] = {
            "file": f"{name}.png", "frameW": size, "frameH": size, "frames": len(frames),
            "fps": fps, "loop": loop, "anchor": anchor,
            # Drawn size relative to the source frame. Props come from smaller
            # frames than the pets, so each is sized to sit right beside one.
            "scale": PROP_SCALE.get(name, 1.0),
        }

    with open(os.path.join(OUT, "animations.json"), "w") as f:
        json.dump(meta, f, indent=2)

    maxw = max(s.width for _, s in sheets)
    sheet = Image.new("RGBA", (maxw + 100, len(sheets) * (S + 8) + 6), (58, 58, 66, 255))
    d = ImageDraw.Draw(sheet)
    for row, (name, strip) in enumerate(sheets):
        y = row * (S + 8) + 3
        d.text((6, y + S // 2 - 6), name, fill=(240, 240, 240, 255))
        sheet.paste(strip, (100, y), strip)
    sheet.save(PREVIEW)
    icon = make_icon()
    print(f"wrote {len(meta)} strips + animations.json + {os.path.basename(icon)}; "
          f"preview: {os.path.abspath(PREVIEW)}")


if __name__ == "__main__":
    main()
