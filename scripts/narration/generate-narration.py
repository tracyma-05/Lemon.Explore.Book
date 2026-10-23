"""Renders the book's narration to mp3 with edge-tts and keeps the manifest the app reads.

Every resource key that starts with ``Narr_`` in ``AppStrings.resx`` (English) and ``AppStrings.zh-CN.resx`` (Chinese)
is one clip. A clip is rendered again only when its text or voice settings change (a hash in the manifest), so a run
after editing a few lines only fetches those. Clips whose key is gone are deleted.

    python -I scripts/narration/generate-narration.py            # render what is missing or stale
    python -I scripts/narration/generate-narration.py --check    # exit 1 if anything is missing or stale (no network)
    python -I scripts/narration/generate-narration.py --force Narr_Seasons_   # render keys with this prefix again
    python -I scripts/narration/generate-narration.py --seed-from <folder>    # copy clips rendered elsewhere first

``--seed-from`` takes another narration folder in the same format (``narration.json`` plus ``<culture>/<key>.mp3``)
and copies every clip whose hash matches the book's text, so only the lines that differ are rendered.

Needs ``pip install edge-tts`` (7.x). The voices are Microsoft Edge's online "Read aloud" voices; the endpoint is not an
official API, so the audio is rendered here once, and the app never depends on it at run time.

Output (``assets/narration``):
    narration.json                      committed: per culture the voice settings, and per key the hash, duration
                                        and sentence timings (seconds). The app embeds it.
    audio/<culture>/<key>.<hash>.mp3    not committed: 24 kHz 48 kbps mono. The hash in the name makes each file
                                        immutable, so the web edition can cache it for good.

The hash is the first 16 hex digits of SHA-256 over UTF-8 ``voice|rate|pitch|text``;
``NarrationManifestTests`` computes the same to catch text edited without rendering again.
"""
from __future__ import annotations

import argparse
import asyncio
import hashlib
import json
import pathlib
import shutil
import sys
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "src" / "Lemon.Explore.Book" / "Resources"
OUT = ROOT / "assets" / "narration"
AUDIO = OUT / "audio"
MANIFEST = OUT / "narration.json"
PREFIX = "Narr_"

# The same voices as the original app's narration, so clips can be shared: neutral delivery, a little slower than
# default for teaching.
VOICES = {
    "en": {"resx": "AppStrings.resx", "voice": "en-US-AvaMultilingualNeural", "rate": "-10%", "pitch": "+0Hz"},
    "zh-CN": {"resx": "AppStrings.zh-CN.resx", "voice": "zh-CN-XiaoxiaoNeural", "rate": "-10%", "pitch": "+0Hz"},
}

BYTES_PER_SECOND = 48000 / 8  # audio-24khz-48kbitrate-mono-mp3
PARALLEL = 4
RETRIES = 3


def read_resx(path: pathlib.Path) -> dict[str, str]:
    texts = {}
    for data in ET.parse(path).getroot().iter("data"):
        key = data.get("name", "")
        value = data.find("value")
        if key.startswith(PREFIX) and value is not None:
            texts[key] = value.text or ""
    return texts


def clip_hash(settings: dict, text: str) -> str:
    raw = f"{settings['voice']}|{settings['rate']}|{settings['pitch']}|{text}"
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()[:16]


def clip_file(culture: str, key: str, h: str) -> pathlib.Path:
    return AUDIO / culture / f"{key}.{h}.mp3"


async def render(settings: dict, text: str, target: pathlib.Path) -> dict:
    """Streams one clip to ``target`` and returns its duration and sentence timings."""
    import edge_tts  # only needed when something has to be rendered

    last_error: Exception | None = None
    for attempt in range(RETRIES):
        try:
            audio = bytearray()
            sentences = []
            communicate = edge_tts.Communicate(text, settings["voice"], rate=settings["rate"], pitch=settings["pitch"],
                                               boundary="SentenceBoundary")
            async for chunk in communicate.stream():
                if chunk["type"] == "audio":
                    audio.extend(chunk["data"])
                elif chunk["type"] == "SentenceBoundary":
                    start = chunk["offset"] / 1e7
                    sentences.append([round(start, 3), round(start + (chunk["duration"] / 1e7), 3), chunk["text"]])
            if not audio:
                raise RuntimeError("no audio received")
            # The service now and then ends a stream early: too little audio for the sentences, or sentences missing.
            duration = len(audio) / BYTES_PER_SECOND
            spoken = sum(len(s[2]) for s in sentences)
            if not sentences or duration + 0.3 < sentences[-1][1] or spoken < 0.8 * len("".join(text.split())):
                raise RuntimeError(f"stream cut short ({duration:.1f}s, {spoken}/{len(text)} characters)")
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(bytes(audio))
            return {"duration": round(len(audio) / BYTES_PER_SECOND, 3), "sentences": sentences}
        except Exception as error:  # network hiccups: try again a couple of times
            last_error = error
            await asyncio.sleep(1.5 * (attempt + 1))
    raise RuntimeError(f"{target.name}: {last_error}")


def seed(source: pathlib.Path, culture: str, settings: dict, texts: dict[str, str], clips: dict) -> int:
    """Copies clips from another narration folder whose hash matches the text here; returns how many."""
    other = json.loads((source / "narration.json").read_text(encoding="utf-8")).get(culture, {})
    if any(other.get(k) != settings[k] for k in ("voice", "rate", "pitch")):
        print(f"  {culture}: voice settings differ in {source}, nothing seeded")
        return 0

    copied = 0
    for key, text in texts.items():
        h = clip_hash(settings, text)
        entry = other.get("clips", {}).get(key)
        mp3 = source / culture / f"{key}.mp3"
        target = clip_file(culture, key, h)
        if entry is None or entry.get("hash") != h or not mp3.exists():
            continue
        if clips.get(key, {}).get("hash") == h and target.exists():
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(mp3, target)
        clips[key] = {"hash": h, "duration": entry["duration"], "sentences": entry["sentences"]}
        copied += 1
    return copied


async def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="only report missing or stale clips; exit 1 if any")
    parser.add_argument("--force", metavar="PREFIX", help="render keys starting with PREFIX again")
    parser.add_argument("--seed-from", metavar="FOLDER", type=pathlib.Path,
                        help="copy matching clips from another narration folder before rendering")
    args = parser.parse_args()

    manifest = json.loads(MANIFEST.read_text(encoding="utf-8")) if MANIFEST.exists() else {}
    stale_total = 0
    gate = asyncio.Semaphore(PARALLEL)

    for culture, settings in VOICES.items():
        texts = read_resx(RESOURCES / settings["resx"])
        entry = manifest.setdefault(culture, {})
        clips = entry.get("clips", {}) if all(entry.get(k) == settings[k] for k in ("voice", "rate", "pitch")) else {}
        entry.update({k: settings[k] for k in ("voice", "rate", "pitch")})

        if args.seed_from and not args.check:
            print(f"{culture}: {seed(args.seed_from, culture, settings, texts, clips)} clips seeded")

        todo = []
        for key, text in sorted(texts.items()):
            h = clip_hash(settings, text)
            fresh = clips.get(key, {}).get("hash") == h and clip_file(culture, key, h).exists()
            if not fresh or (args.force and key.startswith(args.force)):
                todo.append((key, text, h))

        orphans = sorted(set(clips) - set(texts))
        stale_total += len(todo) + len(orphans)
        print(f"{culture}: {len(texts)} clips, {len(todo)} to render, {len(orphans)} to remove")
        if args.check:
            for key, _, _ in todo:
                print(f"  stale: {culture}/{key}")
            continue

        for key in orphans:
            clips.pop(key, None)

        async def one(key: str, text: str, h: str) -> None:
            async with gate:
                info = await render(settings, text, clip_file(culture, key, h))
                clips[key] = {"hash": h, **info}
                print(f"  {culture}/{key} {info['duration']:.1f}s")

        results = await asyncio.gather(*(one(*item) for item in todo), return_exceptions=True)
        failures = [r for r in results if isinstance(r, Exception)]
        for failure in failures:
            print(f"  FAILED {failure}", file=sys.stderr)

        # Files from older text or removed keys.
        wanted = {clip_file(culture, key, clip["hash"]).name for key, clip in clips.items()}
        for mp3 in (AUDIO / culture).glob("*.mp3"):
            if mp3.name not in wanted:
                mp3.unlink()

        entry["clips"] = dict(sorted(clips.items()))
        MANIFEST.parent.mkdir(parents=True, exist_ok=True)
        MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=1, sort_keys=True) + "\n", encoding="utf-8",
                            newline="\n")
        if failures:
            return 1

    if args.check and stale_total:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))
