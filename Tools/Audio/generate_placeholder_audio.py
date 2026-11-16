"""Original deterministic synthesis for M60 wiring tests; no downloaded/recorded samples.
Run with Python 3 from any directory. Reviewed clips/catalogs are never overwritten unless --replace.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import struct
import wave

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Art/Audio/Placeholders"
RATE = 22050
# cue, duration, base frequency, noise amount, priority, distance
RECIPES = [
    ("Footstep", .16, 110, .85, 10, 14), ("Jump", .25, 190, .5, 35, 18),
    ("Dodge", .32, 130, .75, 45, 18), ("Attack", .28, 250, .65, 65, 25),
    ("Hit", .22, 100, .75, 60, 24), ("Block", .25, 610, .3, 70, 26),
    ("Parry", .5, 1050, .15, 85, 28), ("Death", .65, 100, .5, 65, 28),
    ("Chest", .6, 220, .5, 35, 18), ("CampComplete", .8, 440, .05, 65, 32),
    ("ForestAmbience", 8, 85, .7, 5, 1), ("FortressAmbience", 8, 55, .55, 5, 1),
    ("EnemyAggro", .45, 150, .5, 50, 28), ("EnemyAttack", .32, 200, .7, 65, 26),
    ("EnemyDamage", .2, 85, .8, 45, 22), ("EnemyDeath", .65, 75, .6, 55, 28),
    ("MatchStart", 1.1, 330, .02, 100, 1), ("KingUnderAttack", .8, 520, .08, 100, 1),
    ("KingDeath", 1.3, 80, .4, 95, 70), ("Victory", 1.5, 440, .01, 100, 1),
    ("Defeat", 1.5, 220, .04, 100, 1), ("UiClick", .08, 700, .05, 30, 1),
    ("MusicPlaceholder", 8, 110, 0, 5, 1),
]


def synth(cue, duration, freq, noise, variant):
    seed = int.from_bytes(hashlib.sha256((cue + str(variant)).encode()).digest()[:8], "little")
    rng = random.Random(seed)
    loop = "Ambience" in cue or cue == "MusicPlaceholder"
    samples = []
    filtered = 0
    n = round(RATE * duration)
    for i in range(n):
        t = i / RATE
        x = i / max(1, n - 1)
        filtered = filtered * .94 + rng.uniform(-1, 1) * .06
        f = freq * (1 + .04 * variant)
        if loop:
            # Whole cycles make each deterministic bed wrap cleanly.
            f = round(f * duration) / duration
            if cue == "MusicPlaceholder":
                value = sum(math.sin(2 * math.pi * round(f * r * duration) / duration * t) / (j + 2)
                            for j, r in enumerate((1, 1.5, 2))) * .13
            else:
                value = (.045 * math.sin(2 * math.pi * f * t)
                         + .018 * math.sin(2 * math.pi * (f + 1 / duration) * t)) * (.8 + .2 * math.cos(2 * math.pi * x))
        else:
            envelope = min(1, t / .008) * (1 - x) ** 2
            sweep = -90 if cue in ("Dodge", "Death", "EnemyDeath", "Defeat") else 40
            tone = math.sin(2 * math.pi * (f * t + sweep * t * t))
            overtone = math.sin(2 * math.pi * f * 2.73 * t) * .25
            noise_sample = rng.uniform(-1, 1) * .35 + filtered
            value = envelope * ((tone + overtone) * (1 - noise) + noise_sample * noise) * .28
            if cue in ("KingUnderAttack", "MatchStart", "Victory", "CampComplete"):
                value *= .35 + .65 * (1 if (t % .22) < .12 else 0)
        samples.append(value)
    if loop:
        # Correct tiny endpoint delta without changing the period length.
        delta = samples[-1] - samples[0]
        samples = [v - delta * i / (n - 1) for i, v in enumerate(samples)]
    return samples


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--replace", action="store_true", help="Explicitly regenerate owned placeholder WAVs/manifest")
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    manifest = []
    for cue, duration, freq, noise, priority, distance in RECIPES:
        loop = "Ambience" in cue or cue == "MusicPlaceholder"
        category = "Ambience" if "Ambience" in cue else "Music" if cue == "MusicPlaceholder" else "UI" if cue == "UiClick" else "SFX"
        clips = []
        for variant in range(3 if cue == "Footstep" else 1):
            path = OUT / (cue + (f"_{variant + 1:02d}" if cue == "Footstep" else "") + ".wav")
            clips.append(path.relative_to(ROOT).as_posix())
            if path.exists() and not args.replace:
                continue
            samples = synth(cue, duration, freq, noise, variant)
            with wave.open(str(path), "wb") as handle:
                handle.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
                handle.writeframes(struct.pack("<" + "h" * len(samples), *(round(max(-.95, min(.95, s)) * 32767) for s in samples)))
        manifest.append(dict(cue=cue, category=category, clips=clips, loop=loop,
                             spatial=not loop and cue not in ("MatchStart", "KingUnderAttack", "Victory", "Defeat", "UiClick"),
                             volume=.35 if loop else .7, priority=priority,
                             maximumConcurrent=1 if loop or priority >= 95 else 4,
                             cooldown=2 if cue == "KingUnderAttack" else .14 if cue == "Footstep" else .08,
                             pitchVariation=0 if loop or priority >= 95 else .04,
                             minDistance=2 if distance > 1 else 1, maxDistance=distance))
    manifest.append(dict(cue="VoicePlaceholder", category="Voice", clips=[], loop=False, spatial=True,
                         volume=.7, priority=75, maximumConcurrent=2, cooldown=.1, pitchVariation=0, minDistance=2, maxDistance=24))
    destination = ROOT / "Tools/Audio/audio_manifest.json"
    if not destination.exists() or args.replace:
        destination.write_text(json.dumps(dict(sounds=manifest), indent=2) + "\n", encoding="utf-8")
    print(f"M60: {len(manifest)} cue definitions; {sum(len(m['clips']) for m in manifest)} original mono WAV placeholders.")


if __name__ == "__main__":
    main()
