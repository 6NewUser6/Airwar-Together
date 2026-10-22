"""Bake original, editable 3D flight audio to PCM assets; the game never synthesizes it."""
from __future__ import annotations

from array import array
import math
import hashlib
from pathlib import Path
import random
import struct
import sys
import wave


ROOT = Path(__file__).resolve().parents[1] / "airwar-together" / "assets" / "audio" / "arena"
RATE = 22050
TAU = math.tau


def write(name, duration, synthesizer, loop=False):
    count = round(duration * RATE)
    rng = random.Random(703 + sum(map(ord, name)))
    data = array("h")
    for index in range(count):
        value = synthesizer(index / RATE, duration, rng)
        if not loop:
            # Remove clicks without smearing the transient.
            value *= min(1, index / (RATE * .003), (count - index - 1) / (RATE * .015))
        data.append(round(max(-.96, min(.96, value)) * 32767))
    if sys.byteorder != "little":
        data.byteswap()
    path = ROOT / (name + ".wav")
    with wave.open(str(path), "wb") as target:
        target.setnchannels(1)
        target.setsampwidth(2)
        target.setframerate(RATE)
        target.writeframes(data.tobytes())
    if loop:
        # Standard WAV sampler loop. Committed Godot import settings also keep this loop explicit.
        sampler = struct.pack("<9I", 0, 0, round(1e9 / RATE), 60, 0, 0, 0, 1, 0)
        sampler += struct.pack("<6I", 0, 0, 0, count - 1, 0, 0)
        payload = path.read_bytes() + b"smpl" + struct.pack("<I", len(sampler)) + sampler
        payload = payload[:4] + struct.pack("<I", len(payload) - 8) + payload[8:]
        path.write_bytes(payload)
    resource = "res://assets/audio/arena/" + path.name
    imported = "res://.godot/imported/" + path.name + "-" + hashlib.md5(resource.encode()).hexdigest() + ".sample"
    import_path = path.with_suffix(".wav.import")
    if not import_path.exists():
        import_path.write_text(f'''[remap]

importer="wav"
type="AudioStreamWAV"
path="{imported}"

[deps]

source_file="{resource}"
dest_files=["{imported}"]

[params]

force/8_bit=false
force/mono=false
force/max_rate=false
force/max_rate_hz=44100
edit/trim=false
edit/normalize=false
edit/loop_mode={2 if loop else 1}
edit/loop_begin=0
edit/loop_end={count if loop else -1}
compress/mode=0
''', encoding="utf-8")
    print(f"{name}: {count} samples, {duration:.2f}s")


def laser(t, duration, rng):
    envelope = math.exp(-t * 21)
    phase = TAU * (1650 * t - 2250 * t * t)
    return (.42 * math.sin(phase) + .13 * math.sin(phase * .5) + .055 * rng.uniform(-1, 1)) * envelope


def impact(t, duration, rng):
    return (.40 * rng.uniform(-1, 1) * math.exp(-t * 28)
            + .35 * math.sin(TAU * (140 * t - 85 * t * t)) * math.exp(-t * 12))


def destruction(t, duration, rng):
    # Layered, soft low burst rather than a harsh full-scale white-noise blast.
    low = math.sin(TAU * (73 * t - 16 * t * t))
    crackle = rng.uniform(-1, 1) * (.5 + .5 * math.sin(TAU * 29 * t))
    return (.38 * low + .34 * crackle) * math.exp(-t * 4.7)


def boost(t, duration, rng):
    envelope = math.sin(math.pi * t / duration) ** 1.5
    phase = TAU * (78 * t + 140 * t * t)
    return (.28 * math.sin(phase) + .15 * math.sin(phase * 2) + .07 * rng.uniform(-1, 1)) * envelope


def charge(t, duration, rng):
    envelope = min(1, t / .3) * (1 - .3 * t / duration)
    phase = TAU * (95 * t + 135 * t * t)
    return (.17 * math.sin(phase) + .12 * math.sin(phase * 1.5) + .06 * math.sin(phase * 3)) * envelope


def arrival(t, duration, rng):
    swell = math.sin(math.pi * min(1, t / .12)) if t < .12 else math.exp(-(t - .12) * 5)
    return swell * (.22 * math.sin(TAU * 220 * t) + .16 * math.sin(TAU * 330 * t)
                    + .12 * math.sin(TAU * 440 * t) + .12 * rng.uniform(-1, 1) * math.exp(-t * 7))


def engine(t, duration, rng):
    # Every frequency is an exact multiple of 0.5 Hz, so the two-second loop is seamless.
    breath = .8 + .2 * math.sin(TAU * 1.5 * t)
    return breath * (.16 * math.sin(TAU * 31.5 * t) + .10 * math.sin(TAU * 63 * t)
                     + .045 * math.sin(TAU * 126 * t) + .025 * math.sin(TAU * 252 * t))


def music(t, duration, rng):
    # Four six-second pads overlap gently, with sparse navigation-like arpeggios.
    chords = ((110, 164.8138, 220, 261.6256), (97.9989, 146.8324, 195.9977, 246.9417),
              (87.3071, 130.8128, 174.6141, 220), (97.9989, 146.8324, 195.9977, 293.6648))
    block = int(t / 6)
    local = t % 6
    pad_envelope = math.sin(math.pi * local / 6) ** 1.4
    value = 0
    for index, frequency in enumerate(chords[block % 4]):
        value += .027 * math.sin(TAU * frequency * t + index * .4) * pad_envelope
        value += .008 * math.sin(TAU * frequency * 2 * t) * pad_envelope
    beat = t % .75
    note = chords[block % 4][int(t / .75) % 4] * 2
    value += .026 * math.sin(TAU * note * beat) * math.exp(-beat * 8) * min(1, beat / .015)
    # The join of the full loop fades to zero, avoiding phase discontinuities.
    return value * min(1, t / .2, (duration - t) / .2)


def main():
    ROOT.mkdir(parents=True, exist_ok=True)
    for name, duration, synth, loop in (
        ("cannon", .19, laser, False), ("impact", .35, impact, False),
        ("destruction", 1.1, destruction, False), ("boost_start", .75, boost, False),
        ("warp_charge", 2.95, charge, False), ("warp_arrival", .85, arrival, False),
        ("engine", 2, engine, True), ("orbital_ambient", 24, music, True),
    ):
        write(name, duration, synth, loop)


if __name__ == "__main__":
    main()
