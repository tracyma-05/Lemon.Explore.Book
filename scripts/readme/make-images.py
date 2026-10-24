"""Renders the README's screenshots, in English and Chinese, and writes them as JPEGs under docs/images.

    python -I scripts/readme/make-images.py

Runs the headless test ``ReadmeImagesExport`` (cover, contents, a few chapter spreads, a walkthrough under way and the
phone layout), then converts each PNG to a JPEG small enough to commit:

    docs/images/en/<name>.jpg
    docs/images/zh-CN/<name>.jpg

Run it again after the book's look changes. Needs Pillow (``pip install pillow``).
"""
from __future__ import annotations

import os
import pathlib
import subprocess
import tempfile

from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
TESTS = ROOT / "tests" / "Lemon.Explore.Book.Tests"
OUT = ROOT / "docs" / "images"


def main() -> None:
    with tempfile.TemporaryDirectory() as temp:
        # Release, so it can run while a Debug build is busy (a video being recorded).
        subprocess.run(
            ["dotnet", "test", str(TESTS), "-c", "Release", "--filter", "FullyQualifiedName~ReadmeImagesExport"],
            env=dict(os.environ, BOOK_README_DIR=temp), check=True)
        for png in sorted(pathlib.Path(temp).glob("*/*.png")):
            target = OUT / png.parent.name / (png.stem + ".jpg")
            target.parent.mkdir(parents=True, exist_ok=True)
            Image.open(png).convert("RGB").save(target, quality=86, optimize=True, progressive=True)
            print(target.relative_to(ROOT), f"{target.stat().st_size // 1024} KB")


if __name__ == "__main__":
    main()
