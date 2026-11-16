"""Original M61 synthesis; small mix/transition fixtures, not a production score.
No external samples. Existing clips/manifest are preserved unless --replace is explicit.
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
RATE = 22050
STATES = ["Lobby", "MatchStart", "Exploration", "NeutralCampCombat", "PvPCombat",
          "BossEncounter", "FortressAssault", "KingDanger", "Victory", "Defeat"]


def synth(index, duration, loop):
    rng = random.Random(6100 + index)
    frames = round(duration * RATE)
    intensity = [.08, .3, .06, .3, .45, .6, .5, .7, .2, .12][index]
    # D minor-compatible pad/motif across all contexts so crossfades remain consonant.
    tonic = 146.8324
    notes = [0, 7, 12, 10, 7, 3, 5, 7]
    if index == 9:
        notes = [12, 10, 7, 5, 3, 0, 0, 0]
    if index == 8:
        notes = [0, 3, 7, 12, 15, 12, 7, 12]
    samples = []
    for i in range(frames):
        t = i / RATE
        pad = 0
        for j, semitone in enumerate((0, 7, 12, 15)):
            freq = round(tonic * 2 ** (semitone / 12) * duration) / duration
            pad += math.sin(2 * math.pi * freq * t) * .025 / (1 + j * .3)
        beat = t % .5
        step = int(t * 2) % len(notes)
        freq = tonic * 2 ** (notes[(step + index) % len(notes)] / 12)
        motif = math.sin(2 * math.pi * freq * beat) * math.sin(math.pi * min(1, beat / .46)) ** 2 * .035
        drum = (math.sin(2 * math.pi * 64 * beat) * math.exp(-beat * 35)
                + rng.uniform(-1, 1) * math.exp(-beat * 70) * .2) * intensity * .06
        envelope = 1 if loop else min(1, t / .12) * min(1, (duration - t) / .8)
        samples.append((pad + motif + drum) * max(0, envelope))
    if loop:
        # Crossfade the last 30 ms into the periodic beginning to remove an endpoint click.
        count = round(.03 * RATE)
        for i in range(count):
            alpha = i / (count - 1)
            samples[-count + i] = samples[-count + i] * (1 - alpha) + samples[0] * alpha
    else:
        samples[0] = samples[-1] = 0
    return samples


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--replace", action="store_true")
    args = parser.parse_args()
    folder = ROOT / "Assets/Art/Audio/MusicPlaceholders"
    folder.mkdir(parents=True, exist_ok=True)
    tracks = []
    for i, state in enumerate(STATES):
        loop = state not in ("MatchStart", "Victory", "Defeat")
        duration = 8 if loop else 3 if state == "MatchStart" else 4
        path = folder / ("Music_" + state + ".wav")
        if args.replace or not path.exists():
            samples = synth(i, duration, loop)
            with wave.open(str(path), "wb") as handle:
                handle.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
                handle.writeframes(struct.pack("<" + "h" * len(samples), *(round(max(-.95, min(.95, v)) * 32767) for v in samples)))
        tracks.append(dict(state=state, clip=path.relative_to(ROOT).as_posix(), loop=loop))
    destination = ROOT / "Tools/Audio/music_manifest.json"
    if args.replace or not destination.exists():
        destination.write_text(json.dumps(dict(volume=.35, crossfadeSeconds=1.25, tracks=tracks), indent=2) + "\n", encoding="utf-8")
    print(f"M61: {len(tracks)} original music placeholders; existing sources preserved by default.")


if __name__ == "__main__":
    main()
