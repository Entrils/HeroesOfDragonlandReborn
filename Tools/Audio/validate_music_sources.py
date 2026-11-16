import hashlib
import json
from pathlib import Path
import re
import struct
import wave

ROOT = Path(__file__).resolve().parents[2]
data = json.loads((ROOT / "Tools/Audio/music_manifest.json").read_text(encoding="utf-8"))
source = (ROOT / "Assets/Game/Audio/Music/MusicPolicy.cs").read_text(encoding="utf-8")
expected = set(re.findall(r"\w+", source.split("public enum MusicState", 1)[1].split("{", 1)[1].split("}", 1)[0]))
assert set(t["state"] for t in data["tracks"]) == expected
assert len(data["tracks"]) == len(expected)
report = []
for track in data["tracks"]:
    path = ROOT / track["clip"]
    with wave.open(str(path), "rb") as handle:
        assert (handle.getnchannels(), handle.getsampwidth(), handle.getframerate()) == (1, 2, 22050)
        frames = handle.getnframes()
        samples = struct.unpack("<" + "h" * frames, handle.readframes(frames))
    peak = max(abs(s) for s in samples) / 32768
    rms = (sum(s * s for s in samples) / frames) ** .5 / 32768
    assert .001 < rms < peak < .5
    assert abs(sum(samples) / frames / 32768) < .01
    assert abs(samples[-1] - samples[0]) <= 1
    assert track["loop"] == (track["state"] not in ("MatchStart", "Victory", "Defeat"))
    report.append(dict(state=track["state"], seconds=frames / 22050, peak=round(peak, 5), rms=round(rms, 5),
                       sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
assert len(set(t["sha256"] for t in report)) == 10, "Placeholders should be audibly distinguishable fixtures"
(ROOT / "Tools/Audio/music_source_validation.json").write_text(json.dumps(dict(note="Source validation, not listening/Unity playback.", tracks=report), indent=2) + "\n", encoding="utf-8")
print("PASS: 10 distinct music states/WAVs, format, headroom, DC, endpoints and loop flags.")
