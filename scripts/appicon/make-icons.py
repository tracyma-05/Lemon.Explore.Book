"""Draws the book's icon, a small solar system, and writes every size the apps use.

    python -I scripts/appicon/make-icons.py

Writes (all committed):
    src/Lemon.Explore.Book/Assets/book.png                       512 px; also the source of the macOS .icns
    src/Lemon.Explore.Book/Assets/book.ico                       16-256 px, the Windows exe and window icon
    src/Lemon.Explore.Book.Browser/wwwroot/icons/icon-*.png      the web edition's favicon, PWA and Apple touch icons

A rounded square of night sky with the Sun in the middle and three tilted orbits, each with a planet: a blue one, a red
one and a ringed one. Below 48 px only the Sun, one orbit and one planet are drawn, so it still reads in a title bar.
Needs Pillow (``pip install pillow``).
"""
from __future__ import annotations

import math
import pathlib

from PIL import Image, ImageDraw, ImageFilter

ROOT = pathlib.Path(__file__).resolve().parents[2]
ASSETS = ROOT / "src" / "Lemon.Explore.Book" / "Assets"
WEB = ROOT / "src" / "Lemon.Explore.Book.Browser" / "wwwroot" / "icons"

# Drawn at this size and scaled down, which smooths every edge.
BIG = 2048

SKY_TOP = (22, 32, 66)
SKY_BOTTOM = (12, 16, 36)
SUN_CORE = (255, 236, 170)
SUN_EDGE = (246, 176, 66)
ORBIT = (170, 196, 255, 120)

# Orbits as (radius, tilt to the ground, planet's angle round the orbit, planet radius, colour, ringed), in units of
# the icon's width.
ORBITS = [
    (0.20, 0.42, 200, 0.034, (102, 178, 255), False),
    (0.30, 0.42, 330, 0.030, (232, 112, 82), False),
    (0.41, 0.42, 140, 0.048, (232, 200, 140), True),
]
TILT_DEGREES = -18  # the whole system is turned a little, so it is not flat on the page


def sky(size: int, rounded: bool) -> Image.Image:
    """The background: a vertical gradient, in a rounded square (or full bleed for the maskable icon)."""
    gradient = Image.new("RGB", (1, size))
    for y in range(size):
        t = y / (size - 1)
        gradient.putpixel((0, y), tuple(round(a + (b - a) * t) for a, b in zip(SKY_TOP, SKY_BOTTOM)))
    image = gradient.resize((size, size)).convert("RGBA")
    if rounded:
        mask = Image.new("L", (size, size), 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, size - 1, size - 1), radius=round(size * 0.22), fill=255)
        image.putalpha(mask)
    return image


def stars(image: Image.Image, count: int) -> None:
    draw = ImageDraw.Draw(image)
    size = image.width
    # A seeded scatter, so the icon is the same on every run.
    seed = 20261024
    for i in range(count):
        seed = (seed * 1103515245 + 12345) % 2**31
        x = 0.06 + 0.88 * seed / 2**31
        seed = (seed * 1103515245 + 12345) % 2**31
        y = 0.06 + 0.88 * seed / 2**31
        if math.hypot(x - 0.5, y - 0.5) < 0.18:
            continue
        r = size * (0.004 + 0.003 * (i % 3))
        alpha = 110 + 40 * (i % 4)
        draw.ellipse((x * size - r, y * size - r, x * size + r, y * size + r), fill=(255, 255, 255, alpha))


def orbit_point(radius: float, tilt: float, degrees: float) -> tuple[float, float]:
    """A point on a tilted orbit, centred on the origin, then the whole system turned by TILT_DEGREES."""
    a = math.radians(degrees)
    x, y = radius * math.cos(a), radius * tilt * math.sin(a)
    t = math.radians(TILT_DEGREES)
    return x * math.cos(t) - y * math.sin(t), x * math.sin(t) + y * math.cos(t)


def ellipse_layer(size: int, radius: float, tilt: float, width: float) -> Image.Image:
    layer = Image.new("RGBA", (size, size))
    draw = ImageDraw.Draw(layer)
    w = radius * size
    h = radius * tilt * size
    c = size / 2
    draw.ellipse((c - w, c - h, c + w, c + h), outline=ORBIT, width=max(1, round(width * size)))
    return layer.rotate(-TILT_DEGREES, resample=Image.BICUBIC, center=(c, c))


def planet(image: Image.Image, x: float, y: float, r: float, colour, ringed: bool) -> None:
    size = image.width
    cx, cy, pr = size / 2 + x * size, size / 2 + y * size, r * size
    ring = (cx - pr * 2.0, cy - pr * 0.62, cx + pr * 2.0, cy + pr * 0.62)
    ring_width = max(1, round(pr * 0.22))
    draw = ImageDraw.Draw(image)
    if ringed:
        draw.ellipse(ring, outline=(246, 226, 180, 255), width=ring_width)
    draw.ellipse((cx - pr, cy - pr, cx + pr, cy + pr), fill=colour + (255,))

    # The side facing the Sun is lit: a soft lighter patch towards the centre, blended in on its own layer.
    length = max(math.hypot(x, y), 1e-6)
    hx, hy, hr = cx - x / length * pr * 0.3, cy - y / length * pr * 0.3, pr * 0.55
    light = Image.new("RGBA", image.size)
    ImageDraw.Draw(light).ellipse((hx - hr, hy - hr, hx + hr, hy + hr), fill=tuple(min(255, c + 70) for c in colour) + (170,))
    disc = Image.new("L", image.size)
    ImageDraw.Draw(disc).ellipse((cx - pr, cy - pr, cx + pr, cy + pr), fill=255)
    light = light.filter(ImageFilter.GaussianBlur(pr * 0.25))
    light.putalpha(Image.composite(light.getchannel("A"), Image.new("L", image.size), disc))
    image.alpha_composite(light)

    if ringed:
        # The front half of the ring passes in front of the planet.
        ImageDraw.Draw(image).arc(ring, 0, 180, fill=(246, 226, 180, 255), width=ring_width)


def sun(image: Image.Image, radius: float) -> None:
    size = image.width
    c = size / 2
    glow = Image.new("RGBA", (size, size))
    gd = ImageDraw.Draw(glow)
    gr = radius * 2.1 * size
    gd.ellipse((c - gr, c - gr, c + gr, c + gr), fill=SUN_EDGE + (120,))
    glow = glow.filter(ImageFilter.GaussianBlur(radius * size * 0.9))
    image.alpha_composite(glow)

    # The disc: a radial gradient from a pale core to an orange rim.
    disc = Image.new("RGBA", (size, size))
    dd = ImageDraw.Draw(disc)
    r = radius * size
    steps = 48
    for i in range(steps, 0, -1):
        t = i / steps
        colour = tuple(round(a + (b - a) * t) for a, b in zip(SUN_CORE, SUN_EDGE)) + (255,)
        rr = r * t
        dd.ellipse((c - rr, c - rr, c + rr, c + rr), fill=colour)
    image.alpha_composite(disc)


def draw_icon(size: int, *, rounded: bool = True, simple: bool = False, safe: float = 1.0) -> Image.Image:
    """The icon at ``size`` px; ``safe`` shrinks the system to fit a maskable icon's safe zone."""
    image = sky(BIG, rounded)
    if not simple:
        stars(image, 26)

    system = Image.new("RGBA", (BIG, BIG))
    orbits = ORBITS[2:] if simple else ORBITS
    for radius, tilt, _, _, _, _ in orbits:
        system.alpha_composite(ellipse_layer(BIG, radius, tilt, 0.012 if simple else 0.006))
    sun(system, 0.15 if simple else 0.12)
    for radius, tilt, angle, r, colour, ringed in orbits:
        x, y = orbit_point(radius, tilt, angle)
        planet(system, x, y, r * (1.5 if simple else 1.0), colour, ringed and not simple)

    if safe != 1.0:
        scaled = system.resize((round(BIG * safe),) * 2, Image.LANCZOS)
        system = Image.new("RGBA", (BIG, BIG))
        offset = (BIG - scaled.width) // 2
        system.alpha_composite(scaled, (offset, offset))
    image.alpha_composite(system)
    return image.resize((size, size), Image.LANCZOS)


def main() -> None:
    full = draw_icon(512)
    full.save(ASSETS / "book.png")
    full.save(WEB / "icon-512.png")
    draw_icon(192).save(WEB / "icon-192.png")
    # Apple touch icons are square: iOS rounds the corners itself.
    draw_icon(180, rounded=False).save(WEB / "icon-180.png")
    # Maskable: full bleed, the system inside the central 80 % that every mask keeps.
    draw_icon(512, rounded=False, safe=0.8).save(WEB / "icon-512-maskable.png")

    sizes = [16, 24, 32, 48, 64, 128, 256]
    frames = [draw_icon(s, simple=s < 48) for s in sizes]
    frames[-1].save(ASSETS / "book.ico", sizes=[(s, s) for s in sizes], append_images=frames[:-1])
    for path in (ASSETS / "book.png", ASSETS / "book.ico", *sorted(WEB.glob("icon-*.png"))):
        print(path.relative_to(ROOT))


if __name__ == "__main__":
    main()
