"""Validate delivered WAVs/manifest; does not simulate Unity or claim listening tests."""
import hashlib
import json
from pathlib import Path
import re
import struct
import wave

ROOT = Path(__file__).resolve().parents[2]
manifest = json.loads((ROOT / "Tools/Audio/audio_manifest.json").read_text(encoding="utf-8"))["sounds"]
enum_source = (ROOT / "Assets/Game/Audio/AudioCue.cs").read_text(encoding="utf-8")
enum = set(re.findall(r"\w+", enum_source.split("public enum AudioCue", 1)[1].split("{", 1)[1].split("}", 1)[0]))
assert {s["cue"] for s in manifest} == enum and len(manifest) == len(enum), "Cue coverage/duplicates"
assert {s["category"] for s in manifest} == {"Music", "SFX", "UI", "Ambience", "Voice"}
report = []
for sound in manifest:
    assert 1 <= sound["maximumConcurrent"] <= 8
    assert sound["minDistance"] <= sound["maxDistance"]
    assert not sound["loop"] or not sound["spatial"]
    if sound["cue"] == "VoicePlaceholder":
        assert not sound["clips"], "Do not invent recorded voice content"
        continue
    assert sound["clips"]
    for relative in sound["clips"]:
        path = ROOT / relative
        with wave.open(str(path), "rb") as handle:
            assert (handle.getnchannels(), handle.getsampwidth(), handle.getframerate()) == (1, 2, 22050)
            frames = handle.getnframes()
            pcm = handle.readframes(frames)
        samples = struct.unpack("<" + "h" * frames, pcm)
        peak = max(abs(s) for s in samples) / 32768
        rms = (sum(s * s for s in samples) / frames) ** .5 / 32768
        assert .001 < peak < .95 and rms > .0005, relative
        assert abs(sum(samples) / frames / 32768) < .01, "DC offset: " + relative
        if sound["loop"]:
            assert abs(samples[-1] - samples[0]) <= 1, "Discontinuous loop endpoints: " + relative
        else:
            assert samples[0] == samples[-1] == 0, "One-shot endpoint clicks: " + relative
        report.append(dict(path=relative, seconds=round(frames / 22050, 3), peak=round(peak, 5), rms=round(rms, 5),
                           sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
print(f"PASS: {len(manifest)} cue definitions, {len(report)} PCM WAVs; format, amplitude, DC, endpoints, budgets and coverage.")
(ROOT / "Tools/Audio/source_validation.json").write_text(json.dumps(dict(note="Source checks only; Unity playback/mixing/listening not tested.", clips=report), indent=2) + "\n", encoding="utf-8")
