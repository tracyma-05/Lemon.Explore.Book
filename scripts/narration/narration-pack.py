"""Moves the narration audio between this machine and a GitHub Release, so release builds can ship it.

The clips (``assets/narration/audio``, about 280 MB) are not in git. They are rendered once on a developer's machine
(``generate-narration.py``) and kept as the assets of a pre-release, by default ``narration-v1``: one zip per culture.
The release workflow fetches them before it builds, so every package and the web edition carry the voice.

    python -I scripts/narration/narration-pack.py pack --upload     # zip the clips and upload them (replacing)
    python -I scripts/narration/narration-pack.py fetch             # download and unpack into assets/narration/audio

Only the clips the manifest (``narration.json``) names are packed, and ``fetch`` checks every one of them is there:
it warns about missing clips (the book plays those as subtitles), or fails with ``--strict``. After editing ``Narr_*``
strings, render them, then ``pack --upload`` again. The pre-release is never "latest", so the book's updater, which
reads the latest release, ignores it. Uses the GitHub CLI (``gh``), signed in or with ``GH_TOKEN`` set.
"""
from __future__ import annotations

import argparse
import json
import pathlib
import subprocess
import sys
import tempfile
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
NARRATION = ROOT / "assets" / "narration"
AUDIO = NARRATION / "audio"
MANIFEST = NARRATION / "narration.json"
DEFAULT_TAG = "narration-v1"


def expected() -> dict[str, set[str]]:
    """File names the manifest names, per culture: ``<key>.<hash>.mp3``."""
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    return {culture: {f"{key}.{clip['hash']}.mp3" for key, clip in entry["clips"].items()}
            for culture, entry in manifest.items()}


def gh(*args: str) -> subprocess.CompletedProcess:
    result = subprocess.run(["gh", *args], cwd=ROOT, text=True, capture_output=True)
    if result.returncode != 0:
        sys.exit(f"gh {' '.join(args[:2])} failed: {result.stderr.strip() or result.stdout.strip()}")
    return result


def pack(tag: str, upload: bool, out: pathlib.Path) -> None:
    out.mkdir(parents=True, exist_ok=True)
    zips = []
    for culture, names in expected().items():
        missing = sorted(n for n in names if not (AUDIO / culture / n).exists())
        if missing:
            sys.exit(f"{len(missing)} {culture} clip(s) are not rendered (e.g. {missing[0]}); "
                     "run generate-narration.py first.")
        path = out / f"narration-{culture}.zip"
        # mp3s do not compress further: store them.
        with zipfile.ZipFile(path, "w", zipfile.ZIP_STORED) as archive:
            for name in sorted(names):
                archive.write(AUDIO / culture / name, f"{culture}/{name}")
        print(f"{path.name}: {len(names)} clips, {path.stat().st_size // 2**20} MB")
        zips.append(path)

    if not upload:
        return
    exists = subprocess.run(["gh", "release", "view", tag], cwd=ROOT, capture_output=True).returncode == 0
    if not exists:
        gh("release", "create", tag, "--prerelease", "--latest=false", "--title", "Narration audio",
           "--notes", "The book's narration clips, one zip per language. Release builds download them; "
                      "this is not a version of the book.")
    gh("release", "upload", tag, *map(str, zips), "--clobber")
    print(f"Uploaded to the {tag} pre-release.")


def fetch(tag: str, strict: bool) -> None:
    with tempfile.TemporaryDirectory() as temp:
        gh("release", "download", tag, "--pattern", "narration-*.zip", "--dir", temp)
        for path in sorted(pathlib.Path(temp).glob("narration-*.zip")):
            with zipfile.ZipFile(path) as archive:
                archive.extractall(AUDIO)

    problems = 0
    for culture, names in expected().items():
        missing = [n for n in names if not (AUDIO / culture / n).exists()]
        if missing:
            problems += 1
            message = (f"{len(missing)} of {len(names)} {culture} clips are missing from {tag} (e.g. {missing[0]}); "
                       "they will play as subtitles. Render them and run 'narration-pack.py pack --upload'.")
            # A GitHub Actions annotation when run there, plain text otherwise.
            print(f"::{'error' if strict else 'warning'}::{message}", file=sys.stderr)
        else:
            print(f"{culture}: all {len(names)} clips")
    if strict and problems:
        sys.exit(1)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--tag", default=DEFAULT_TAG, help=f"the pre-release holding the packs (default {DEFAULT_TAG})")
    commands = parser.add_subparsers(dest="command", required=True)
    p = commands.add_parser("pack", help="zip the clips the manifest names, one zip per culture")
    p.add_argument("--upload", action="store_true", help="also upload them to the pre-release, replacing")
    p.add_argument("--out", type=pathlib.Path, default=ROOT / "artifacts" / "narration")
    f = commands.add_parser("fetch", help="download the packs and unpack them into assets/narration/audio")
    f.add_argument("--strict", action="store_true", help="fail when a clip the manifest names is missing")
    args = parser.parse_args()

    if args.command == "pack":
        pack(args.tag, args.upload, args.out)
    else:
        fetch(args.tag, args.strict)


if __name__ == "__main__":
    main()
