# /// script
# requires-python = ">=3.11"
# dependencies = ["numpy"]
# ///
"""Generate tiny procedural sound effects (wav) for CuttlefishPet.

All sounds are synthesized (sine/noise + envelopes) — no licensing concerns.
"""

import os
import struct
import wave

import numpy as np

OUT = os.path.join(os.path.dirname(__file__), "..", "CuttlefishPet", "Assets", "sounds")
SR = 22050


def save(name, samples, gain=0.5):
    samples = np.clip(samples * gain, -1, 1)
    data = (samples * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def env(n, attack=0.05, release=0.5):
    e = np.ones(n)
    a = int(n * attack)
    r = int(n * release)
    e[:a] = np.linspace(0, 1, a)
    e[n - r:] = np.linspace(1, 0, r)
    return e


def t(dur):
    return np.linspace(0, dur, int(SR * dur), endpoint=False)


def blip():
    """Curious bubble-blip: two rising sine chirps."""
    x = t(0.18)
    f = 500 + 500 * x / 0.18
    s1 = np.sin(2 * np.pi * f * x) * env(len(x), 0.1, 0.6)
    x2 = t(0.12)
    f2 = 700 + 600 * x2 / 0.12
    s2 = np.sin(2 * np.pi * f2 * x2) * env(len(x2), 0.1, 0.6)
    gap = np.zeros(int(SR * 0.05))
    return np.concatenate([s1, gap, s2])


def bubble():
    """Soft wobbly bubble for idle/camo reveal."""
    x = t(0.35)
    f = 300 + 60 * np.sin(2 * np.pi * 8 * x)
    return np.sin(2 * np.pi * f * x) * env(len(x), 0.15, 0.7)


def squirt():
    """Ink squirt: filtered noise burst with a downward whoosh."""
    x = t(0.4)
    noise = np.random.default_rng(7).uniform(-1, 1, len(x))
    # crude lowpass: moving average
    k = 12
    noise = np.convolve(noise, np.ones(k) / k, mode="same")
    f = 400 * (1 - x / 0.5)
    tone = 0.4 * np.sin(2 * np.pi * np.maximum(f, 60) * x)
    return (noise * 0.8 + tone) * env(len(x), 0.02, 0.6)


def splat():
    """Landing splat: short noise thump."""
    x = t(0.15)
    noise = np.random.default_rng(3).uniform(-1, 1, len(x))
    k = 30
    noise = np.convolve(noise, np.ones(k) / k, mode="same")
    return noise * env(len(x), 0.01, 0.85) * 2.0


def chirp_sleep():
    """Tiny descending snore-whistle."""
    x = t(0.5)
    f = 400 - 200 * x / 0.5
    return np.sin(2 * np.pi * f * x) * env(len(x), 0.2, 0.6) * 0.6


def main():
    os.makedirs(OUT, exist_ok=True)
    save("blip.wav", blip())
    save("bubble.wav", bubble())
    save("squirt.wav", squirt(), gain=0.45)
    save("splat.wav", splat(), gain=0.4)
    save("snore.wav", chirp_sleep(), gain=0.35)
    print("wrote 5 wavs to", os.path.abspath(OUT))


if __name__ == "__main__":
    main()
