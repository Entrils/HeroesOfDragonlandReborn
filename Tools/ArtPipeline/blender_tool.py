"""Resolve Blender or provision the pinned portable LTS inside the ignored .utmp folder."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[2]
VERSION = "4.5.15"
ARCHIVE = f"blender-{VERSION}-windows-x64.zip"
BASE = "https://download.blender.org/release/Blender4.5/"
CACHE = ROOT / ".utmp" / "Blender"


def resolve(explicit=None):
    candidates = [explicit, os.environ.get("BLENDER_EXE"), shutil.which("blender")]
    candidates += sorted(Path("C:/Program Files/Blender Foundation").glob("Blender */blender.exe"), reverse=True)
    candidates += [CACHE / f"blender-{VERSION}-windows-x64" / "blender.exe"]
    return next((Path(p).resolve() for p in candidates if p and Path(p).is_file()), None)


def provision():
    CACHE.mkdir(parents=True, exist_ok=True)
    hashes = urllib.request.urlopen(BASE + f"blender-{VERSION}.sha256", timeout=30).read().decode()
    expected = next(line.split()[0] for line in hashes.splitlines() if line.split()[-1].lstrip("*") == ARCHIVE)
    archive = CACHE / ARCHIVE
    if not archive.exists():
        print(f"Downloading official Blender {VERSION} portable (~380 MiB)...", flush=True)
        with urllib.request.urlopen(BASE + ARCHIVE, timeout=60) as source, archive.open("wb") as target:
            shutil.copyfileobj(source, target)
    with archive.open("rb") as stream:
        actual = hashlib.file_digest(stream, "sha256").hexdigest()
    if actual != expected:
        raise RuntimeError(f"SHA256 mismatch: {archive}; archive retained for inspection")
    print(f"Verified SHA256 {actual}; extracting to {CACHE}", flush=True)
    with zipfile.ZipFile(archive) as package:
        for entry in package.infolist():
            if not (CACHE / entry.filename).resolve().is_relative_to(CACHE.resolve()):
                raise RuntimeError("Archive entry escapes the portable tools folder")
        package.extractall(CACHE)
    return resolve()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--blender", help="Existing Blender executable; BLENDER_EXE is also supported")
    parser.add_argument("--download", action="store_true", help="Download verified official portable Blender if absent")
    parser.add_argument("--create-test", action="store_true", help="Generate the one test crate and export its FBX")
    parser.add_argument("--create-humanoid", action="store_true", help="Generate the milestone-40 skinned mannequin and in-place clips")
    parser.add_argument("--export", type=Path, help="Export an existing .blend source through the shared EXPORT-collection profile")
    parser.add_argument("--output", type=Path, help="Destination .fbx inside Assets/Art (required with --export)")
    args = parser.parse_args()
    if bool(args.export) != bool(args.output) or sum(bool(value) for value in (args.create_test, args.create_humanoid, args.export)) > 1:
        parser.error("Choose --create-test, --create-humanoid, or --export SOURCE.blend --output DESTINATION.fbx")
    executable = resolve(args.blender)
    if executable is None and args.download:
        executable = provision()
    if executable is None:
        raise SystemExit("Blender not found. Supply --blender/BLENDER_EXE, or use --download on Windows x64.")
    version = subprocess.check_output([str(executable), "--version"], text=True).splitlines()[0]
    CACHE.mkdir(parents=True, exist_ok=True)
    (CACHE / "toolchain.json").write_text(json.dumps({"executable": str(executable), "version": version}, indent=2))
    print(version, executable, flush=True)
    if args.create_test:
        subprocess.run([str(executable), "--background", "--factory-startup", "--python-exit-code", "1",
                        "--python", str(ROOT / "Tools/ArtPipeline/create_test_crate.py"), "--", str(ROOT)], check=True)
    if args.export:
        source, output = args.export.resolve(), args.output.resolve()
        if not source.is_file() or source.suffix.lower() != ".blend":
            parser.error("Source must be an existing .blend")
        if not output.is_relative_to(ROOT / "Assets/Art") or output.suffix.lower() != ".fbx":
            parser.error("Output must be an .fbx inside this project's Assets/Art")
        subprocess.run([str(executable), "--background", "--factory-startup", str(source), "--python-exit-code", "1",
                        "--python", str(ROOT / "Tools/ArtPipeline/export_fbx.py"), "--", str(output)], check=True)
    if args.create_humanoid:
        subprocess.run([str(executable), "--background", "--factory-startup", "--python-exit-code", "1",
                        "--python", str(ROOT / "Tools/ArtPipeline/create_humanoid.py"), "--", str(ROOT)], check=True)


if __name__ == "__main__":
    main()
