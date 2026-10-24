"""Makes a video of one chapter's walkthroughs, for video sites: the desktop spread (16:9) or a phone page (9:16).

The headless test ``ChapterVideoExport`` plays every view's walkthrough in the book and records the picture
(``frames.mp4``, subtitles included, as the app shows them) and when each narration clip started and stopped
(``timeline.json``). This script runs it, then lays the committed narration mp3s on that timeline and writes the video.

    python -I scripts/video/make-chapter-video.py                       # Seasons, Chinese
    python -I scripts/video/make-chapter-video.py --chapter Rainbow --culture en
    python -I scripts/video/make-chapter-video.py --views SunPath          # one view: a short clip
    python -I scripts/video/make-chapter-video.py --scale 1.5              # 1920 x 1080 instead of 1280 x 720
    python -I scripts/video/make-chapter-video.py --layout phone           # one page, 720 x 1280
    python -I scripts/video/make-chapter-video.py --skip-record          # only mix the audio again

Needs ffmpeg on the PATH and the narration audio (``scripts/narration/generate-narration.py``). The picture is
rendered offline at 30 fps on a stepped clock, so it is smooth however slow the machine, but slow: at 1280 x 720
about 14 times the video's length (a one-minute view about a quarter of an hour); the time grows with the pixels.

Output: ``artifacts/video/<name>/<name>.mp4``, ``<name>`` being ``<chapter>-<culture>-<layout>[-<views>]``
(artifacts/ is git-ignored).
"""
from __future__ import annotations

import argparse
import json
import os
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
AUDIO = ROOT / "assets" / "narration" / "audio"
TESTS = ROOT / "tests" / "Lemon.Explore.Book.Tests"


def record(args: argparse.Namespace, out: pathlib.Path) -> None:
    env = dict(os.environ, BOOK_VIDEO_DIR=str(out), BOOK_VIDEO_CHAPTER=args.chapter, BOOK_VIDEO_CULTURE=args.culture,
               BOOK_VIDEO_VIEWS=args.views, BOOK_VIDEO_LAYOUT=args.layout,
               BOOK_VIDEO_SCALE="" if args.scale is None else str(args.scale))
    subprocess.run(
        ["dotnet", "test", str(TESTS), "--filter", "FullyQualifiedName~ChapterVideoExport"],
        env=env, check=True)


def mix(out: pathlib.Path, name: str) -> pathlib.Path:
    timeline = json.loads((out / "timeline.json").read_text(encoding="utf-8"))
    clips = [c for c in timeline["clips"] if (AUDIO / c["file"]).exists()]
    missing = len(timeline["clips"]) - len(clips)
    if missing:
        print(f"warning: {missing} clip(s) have no audio here; run generate-narration.py", file=sys.stderr)
    if not clips:
        sys.exit("no narration audio found")

    # Each clip starts where the walkthrough started it, cut where it was stopped early (a question answered, a step
    # skipped); then all are summed. The voice is mono at 24 kHz; the video gets stereo 48 kHz AAC.
    inputs: list[str] = []
    filters: list[str] = []
    for i, clip in enumerate(clips):
        inputs += ["-i", str(AUDIO / clip["file"])]
        chain = f"[{i + 1}:a]aresample=48000"
        if clip["end"] is not None:
            chain += f",atrim=0:{clip['end'] - clip['start']:.3f}"
        chain += f",adelay={int(clip['start'] * 1000)}:all=1[a{i}]"
        filters.append(chain)
    labels = "".join(f"[a{i}]" for i in range(len(clips)))
    filters.append(f"{labels}amix=inputs={len(clips)}:normalize=0,apad[voice]")

    script = out / "mix.txt"
    script.write_text(";\n".join(filters), encoding="utf-8")
    video = out / f"{name}.mp4"
    subprocess.run(
        ["ffmpeg", "-y", "-loglevel", "error", "-i", str(out / "frames.mp4"), *inputs,
         "-/filter_complex", str(script), "-map", "0:v", "-map", "[voice]",
         "-c:v", "copy", "-c:a", "aac", "-b:a", "160k", "-ac", "2", "-shortest", "-movflags", "+faststart",
         str(video)],
        check=True)
    return video


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--chapter", default="Seasons", help="chapter slug, as in Contents/BookContents.cs")
    parser.add_argument("--culture", default="zh-CN", choices=["zh-CN", "en"])
    parser.add_argument("--views", default="", help="comma-separated views to record (the chapter's Mode names); all if empty")
    parser.add_argument("--layout", default="desktop", choices=["desktop", "phone"])
    parser.add_argument("--scale", type=float, help="pixels per layout unit (default 1 for desktop, 1.8 for phone)")
    parser.add_argument("--skip-record", action="store_true", help="reuse frames.mp4 and timeline.json")
    args = parser.parse_args()

    name = f"{args.chapter}-{args.culture}-{args.layout}" +(f"-{args.views.replace(',', '+')}" if args.views else "")
    out = ROOT / "artifacts" / "video" / name
    out.mkdir(parents=True, exist_ok=True)
    if not args.skip_record:
        record(args, out)
    print(mix(out, name))


if __name__ == "__main__":
    main()
