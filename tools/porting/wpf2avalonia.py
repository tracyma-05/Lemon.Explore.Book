"""The mechanical part of porting a chapter from the original WPF app to this book's Avalonia code.

    python -I tools/porting/wpf2avalonia.py drawing   SRC.cs DST.cs --namespace NS --source-prefix ROOT [--map A=B ...]
    python -I tools/porting/wpf2avalonia.py viewmodel SRC.cs DST.cs --namespace NS --source-prefix ROOT [--map A=B ...]
                                                      [--chapter] [--replacements FILE.py]
    python -I tools/porting/wpf2avalonia.py strings   SRC_RESOURCES_DIR DST_RESOURCES_DIR PATTERN [PATTERN ...]

ROOT is the original app's root namespace (passed in, never written into this repository). --map maps a namespace
below it to one below `Lemon.Explore.Book`, e.g. `--map Controls.Eclipse=Chapters.Eclipses`; the shared ones (space
drawing, lessons, narration, localization) are built in.

`drawing` handles a diagram or figure: namespaces and usings, brush and pen types, frozen brushes and gradients,
rotate transforms, ellipse geometries, single-figure StreamGeometry blocks, dependency properties (to styled
properties with their change callbacks and AffectsRender), OnRender, ActualWidth/Height, DPI, mouse handlers,
cursors, the CompositionTarget animation loop, Loaded/Unloaded language subscriptions, and Point/Vector idioms.
`viewmodel` handles a view model or lessons file; with --chapter it also adds the IChapterContent view lifetimes
and immutable brushes. `strings` copies resx keys (exact, or prefixes ending in `*`) into both of this book's resx
files.

Whatever it reports as "fix by hand" is left for a person, and the compiler finds the rest (docs/ROADMAP.md has the
conversion table). Every rewrite is a plain text pattern: read the diff.
"""
from __future__ import annotations

import argparse
import re
import runpy
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from xml.sax.saxutils import escape

TARGET_ROOT = "Lemon.Explore.Book"

# Namespaces below the source root that every chapter shares, and where they live in this book.
SHARED_NAMESPACES = {
    "Controls.SolarSystem": "Controls.Space",
    # Other chapters use the eclipse folder only for its shared drawing helpers; the eclipses port maps it with --map.
    "Controls.Eclipse": "Controls.Space",
    "Controls.Seasons": "Chapters.Seasons",
    "Controls.Explore": "Controls.Explore",
    "ViewModels.Explore.Lessons": "Explore.Lessons",
    "Services.Explore.Narration": "Explore.Narration",
    # The observer location (IExploreLocationService, ExplorePlaces).
    "Services.Explore": "Explore.Places",
    "Infrastructures.Localization": "Infrastructures.Localization",
}


# ---- helpers ---------------------------------------------------------------------------------------------------


def match_bracket(text: str, open_index: int) -> int:
    """Index of the bracket closing the one at open_index ((, [ or {)."""
    depth = 0
    for i in range(open_index, len(text)):
        if text[i] in "([{":
            depth += 1
        elif text[i] in ")]}":
            depth -= 1
            if depth == 0:
                return i
    raise ValueError("unbalanced brackets")


def split_top(text: str) -> list[str]:
    """Splits an argument list at its top-level commas."""
    parts, depth, start = [], 0, 0
    for i, c in enumerate(text):
        if c in "([{<":
            depth += 1
        elif c in ")]}>" and not (c == ">" and text[i - 1:i] == "="):  # the '>' of a lambda's '=>' closes nothing
            depth -= 1
        elif c == "," and depth == 0:
            parts.append(text[start:i].strip())
            start = i + 1
    parts.append(text[start:].strip())
    return [p for p in parts if p]


def namespace_map(source_prefix: str, maps: list[str]) -> dict[str, str]:
    pairs = dict(SHARED_NAMESPACES)
    for m in maps:
        old, new = m.split("=", 1)
        pairs[old] = new
    return {f"{source_prefix}.{old}": f"{TARGET_ROOT}.{new}" for old, new in pairs.items()}


def apply_namespaces(s: str, namespace: str, source_prefix: str, maps: list[str]) -> str:
    s = re.sub(r"^namespace [\w.]+;", f"namespace {namespace};", s, count=1, flags=re.M)
    for old, new in sorted(namespace_map(source_prefix, maps).items(), key=lambda kv: -len(kv[0])):
        s = s.replace(old, new)
    # The original's drawing helpers are this book's SpaceDrawing, whatever namespace they were in.
    s = re.sub(r"using static [\w.]+\.EclipseDrawing;", f"using static {TARGET_ROOT}.Controls.Space.SpaceDrawing;", s)
    s = s.replace("EclipseDrawing", "SpaceDrawing")
    s = re.sub(r"\bControls\.\w+\.SpaceDrawing\.", "SpaceDrawing.", s)
    # Names qualified relative to the original's root (nameof(Controls.SeaBreeze.CellTime.Day)): the mapped namespace.
    for mapping in maps:
        old, _, new = mapping.partition("=")
        s = re.sub(rf"(?<![\w.]){re.escape(old)}\.", f"{new}.", s)
    s = s.replace(f"using {namespace};\n", "")
    # Two source namespaces can map to one (e.g. the solar-system and eclipse helpers): keep each using once.
    seen: set[str] = set()
    lines = []
    for line in s.split("\n"):
        if line.startswith("using ") and line.endswith(";"):
            if line in seen:
                continue
            seen.add(line)
        lines.append(line)
    return "\n".join(lines)


# ---- drawing: brushes, geometry, DPI ---------------------------------------------------------------------------


def _stops(list_text: str) -> list[str]:
    stops = []
    for stop in split_top(list_text):
        if not stop.startswith("new GradientStop("):
            raise ValueError(stop)
        args = split_top(stop[len("new GradientStop("):-1])
        stops.append(f"({args[0]}, {args[1]})")
    return stops


def rewrite_brushes(text: str) -> str:
    """Frozen solid brushes and (frozen or not) gradient brushes to SpaceDrawing's immutable helpers."""
    out, i = [], 0
    pattern = re.compile(r"(Freeze\()?new (Radial|Linear)GradientBrush\(|Freeze\(new SolidColorBrush\(")
    while True:
        m = pattern.search(text, i)
        if not m:
            out.append(text[i:])
            return "".join(out)
        out.append(text[i:m.start()])
        open_index = m.end() - 1
        close = match_bracket(text, open_index)
        inner = text[open_index + 1:close]
        frozen = m.group(0).startswith("Freeze(")
        end = match_bracket(text, m.start() + len("Freeze")) + 1 if frozen else close + 1
        tail = text[close + 1:end - 1] if frozen else ""
        if m.group(0).endswith("SolidColorBrush("):
            out.append(f"SpaceDrawing.Solid({inner})")
            i = end
            continue
        if tail.strip() or text[end:].lstrip().startswith("{"):
            out.append(text[m.start():end])  # an initializer or more arguments: by hand
            i = end
            continue
        args = split_top(inner)
        kind = m.group(2)
        indent = "\n            "
        try:
            if args and args[0].startswith("["):
                stops = _stops(args[0][1:-1])
                if kind == "Radial":
                    out.append("SpaceDrawing.RadialGradient(" + indent + ("," + indent).join(stops) + ")")
                else:
                    out.append(f"SpaceDrawing.LinearGradient({args[1]}," + indent + ("," + indent).join(stops) + ")")
            elif kind == "Linear" and len(args) == 3:
                out.append(f"SpaceDrawing.LinearGradient({args[0]}, {args[1]}, {args[2]})")
            elif kind == "Radial" and len(args) == 2:
                out.append(f"SpaceDrawing.RadialGradient(({args[0]}, 0), ({args[1]}, 1))")
            else:
                out.append(text[m.start():end])
        except ValueError:
            out.append(text[m.start():end])
        i = end


def rewrite_figures(text: str) -> str:
    """`using (var ctx = g.Open()) {…}` with one BeginFigure(p, filled, closed): two-argument BeginFigure + EndFigure."""
    out, i = [], 0
    pattern = re.compile(r"using \(var (\w+) = \w+\.Open\(\)\)\s*\{")
    while True:
        m = pattern.search(text, i)
        if not m:
            out.append(text[i:])
            return "".join(out)
        ctx = m.group(1)
        open_brace = m.end() - 1
        close = match_bracket(text, open_brace)
        body = text[open_brace + 1:close]
        figures = list(re.finditer(rf"{ctx}\.BeginFigure\((.*), (true|false), (true|false)\);", body))
        if len(figures) == 1:
            f = figures[0]
            body = body[:f.start()] + f"{ctx}.BeginFigure({f.group(1)}, {f.group(2)});" + body[f.end():]
            line_start = body.rfind("\n", 0, f.start()) + 1
            indent = body[line_start:f.start()]
            # (`if (i == 0) ctx.BeginFigure(...)` in a loop is one line through all the points: it ends after the loop.)
            loop = enclosing_loop_brace(body, f.start()) if not indent.strip() else None
            if loop is not None:
                # A figure begun on every pass of a loop ends at the end of the loop's body.
                end = match_bracket(body, loop)
                head = body[:end].rstrip()
                body = head + f"\n{indent}{ctx}.EndFigure({f.group(3)});\n" + body[len(head):end].lstrip("\n") + body[end:]
            else:
                indent = re.search(r"\n(\s*)\S", body).group(1)
                body = body.rstrip() + f"\n\n{indent}{ctx}.EndFigure({f.group(3)});\n" + indent[:-4]
        out.append(text[i:open_brace + 1] + body)
        i = close


def enclosing_loop_brace(body: str, pos: int) -> int | None:
    """The opening brace of the innermost block around pos, when that block is the body of a for, foreach or while."""
    stack = []
    for i, ch in enumerate(body[:pos]):
        if ch == "{":
            stack.append(i)
        elif ch == "}" and stack:
            stack.pop()
    if not stack:
        return None
    head = body[:stack[-1]].rstrip()
    line = head[head.rfind("\n") + 1:].strip()
    return stack[-1] if re.match(r"(for|foreach|while)\b", line) else None


USINGS = {
    "using System.Windows;\n": "using Avalonia;\n",
    "using System.Windows.Media;\n": "using Avalonia.Media;\nusing Avalonia.Media.Immutable;\n",
    "using System.Windows.Media.Imaging;\n": "using Avalonia.Media.Imaging;\n",
    "using System.Windows.Input;\n": "using System.Windows.Input;\nusing Avalonia.Input;\n",
    "using System.Windows.Documents;\n": "",
    "using System.Windows.Shapes;\n": "",
    "using System.Windows.Automation.Peers;\n": "",
    "using System.Windows.Threading;\n": "using Avalonia.Threading;\n",
    "using Serilog;\n": "",
    "using Volo.Abp.DependencyInjection;\n": "",
}


def convert_types(s: str, namespace: str) -> str:
    for old, new in USINGS.items():
        s = s.replace(old, new)
    if namespace != f"{TARGET_ROOT}.Controls.Space" and f"using {TARGET_ROOT}.Controls.Space;" not in s and s.startswith("using"):
        s = s.replace("\nnamespace ", f"using {TARGET_ROOT}.Controls.Space;\n\nnamespace ", 1)
    s = re.sub(r"\bDrawingContext dc\b", "PaintContext dc", s)
    s = re.sub(r"\bDpiScale dpi\b", "double dpi", s)
    s = re.sub(r"\bdpi\.(DpiScaleX|DpiScaleY|PixelsPerDip)\b", "dpi", s)
    s = re.sub(r"VisualTreeHelper\.GetDpi\(this\)", "RenderScale()", s)
    s = re.sub(r"(?<![\w.])Pen(?=[\s\[\]?>,)])", "IPen", s)
    s = re.sub(r"(?<![\w.])Brush(?=[\s\[\]?>,)])", "IBrush", s)
    s = s.replace("((SolidColorBrush)", "((ISolidColorBrush)")
    s = s.replace("new RotateTransform(", "SpaceDrawing.Rotation(")
    s = s.replace("new EllipseGeometry(", "SpaceDrawing.Ellipse(")
    s = re.sub(r"^\s*\w+\.Freeze\(\);\s*\n", "", s, flags=re.M)
    s = re.sub(r"^\s*RenderOptions\.SetBitmapScalingMode\(this, BitmapScalingMode\.\w+\);\s*\n", "", s, flags=re.M)
    s = re.sub(r"\b(\w+)\.LineTo\((.*), (?:true|false), (?:true|false)\);", r"\1.LineTo(\2);", s)
    s = re.sub(r"\b(\w+)\.ArcTo\(([^;]*?),\s+(?:true|false), (?:true|false)\);", r"\1.ArcTo(\2);", s)  # (the flags may wrap)
    s = re.sub(r"\b(\w+)\.BezierTo\((.*), (?:true|false), (?:true|false)\);", r"\1.CubicBezierTo(\2);", s)
    s = re.sub(r"\b(\w+)\.QuadraticBezierTo\((.*), (?:true|false), (?:true|false)\);", r"\1.QuadraticBezierTo(\2);", s)
    s = re.sub(r"Freeze\(new Pen\((.*), ([\d.]+)\)\);", r"MakePen(\1, \2);", s)
    s = re.sub(r"SolarSystemCanvas\.SpinAngle\(([\w.]+), ", r"\1.SpinAngle(", s)
    s = s.replace("SweepDirection.Counterclockwise", "SweepDirection.CounterClockwise")
    s = s.replace("FillRule.Nonzero", "FillRule.NonZero")
    s = s.replace(".IntersectsWith(", ".Intersects(")
    s = re.sub(r"\bImageSource\b", "IImage", s)
    # WriteableBitmap and RenderTargetBitmap are both Avalonia Bitmaps.
    s = re.sub(r"\bBitmapSource\b", "Bitmap", s)
    # A bitmap's size in pixels.
    s = re.sub(r"\b(_?\w+[!?]?)\.Pixel(Width|Height)\b", lambda m: f"{m[1]}.PixelSize.{m[2][5:] if False else ('Width' if m[2] == 'Width' else 'Height')}", s)
    # The length of the gap between two points (indexed or not).
    s = re.sub(r"\(([\w.]+(?:\[[^\]]*\])?) - ([\w.]+(?:\[[^\]]*\])?)\)\.Length\b", r"((Vector)(\1 - \2)).Length", s)
    s = s.replace("System.Windows.Threading.DispatcherTimer", "Avalonia.Threading.DispatcherTimer")
    # A pen named in full (beside a property or class of the same name): Avalonia's pens are IPen.
    s = re.sub(r"\bSystem\.Windows\.Media\.Pen\b", "IPen", s)
    s = re.sub(r"\bSystem\.Windows\.Media\.(?=\w)", "", s)
    # A property passed to a helper (Push(DependencyProperty property, object value)).
    s = re.sub(r"\bDependencyProperty (?=\w+\s*[,)])", "AvaloniaProperty ", s)
    s = convert_recurring(s)
    # A two-colour gradient between two points of the drawing (WPF's absolute mapping).
    arg = r"((?:[^,()]|\((?:[^()]|\([^()]*\))*\))+)"
    s = re.sub(rf"new LinearGradientBrush\({arg}, {arg}, 0\)\s*\{{\s*MappingMode = BrushMappingMode\.Absolute,\s*"
               rf"StartPoint = {arg},\s*EndPoint = {arg},\s*\}}",
               r"new ImmutableLinearGradientBrush([new ImmutableGradientStop(0, \1), new ImmutableGradientStop(1, \2)], "
               r"startPoint: new RelativePoint(\3, RelativeUnit.Absolute), endPoint: new RelativePoint(\4, RelativeUnit.Absolute))", s)
    # Premultiplied BGRA bitmaps written from a uint[] of pixels.
    s = re.sub(r"new WriteableBitmap\(([^,]+), ([^,]+), 96, 96, PixelFormats\.(?:Pbgra32|Bgr32), null\)",
               r"new WriteableBitmap(new PixelSize(\1, \2), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul)", s)
    s = re.sub(r"([\w!]+)\.WritePixels\(new Int32Rect\(0, 0, ([^,]+), ([^)]+)\), (\w+), [^;]+\);",
               r"SpaceDrawing.WritePixels(\1, \4, \2, \3);", s)
    if "PixelFormat.Bgra8888" in s and "using Avalonia.Platform;" not in s:
        s = s.replace("using Avalonia;\n", "using Avalonia;\nusing Avalonia.Platform;\n", 1)
    s = rewrite_brushes(s)
    s = rewrite_figures(s)
    return s


# ---- drawing: dependency properties and element plumbing --------------------------------------------------------


HELPER = re.compile(
    r"\n(?:[ \t]*///[^\n]*\n)*[ \t]*(?:private|protected|internal|public) static DependencyProperty (?P<name>\w+)(?:<(?P<gen>[^>]+)>)?"
    r"\((?P<params>[^)]*)\)\s*=>\s*(?P<body>DependencyProperty\.Register\(.*?);\n", re.S)


def expand_register_helpers(s: str) -> str:
    """
    Local helpers (`static DependencyProperty Register<T>(string name, T value, ...) => DependencyProperty.Register(...)`,
    or `TwoWay(string name, double value)` and the like) and their uses become plain DependencyProperty.Register calls,
    which convert_properties understands. The value type is the helper's own (typeof(double)), or comes from the generic
    arguments at the call, or from the CLR property's type.
    """
    helpers = {}
    for m in HELPER.finditer(s):
        body = m.group("body")
        generic = [g.strip() for g in split_top(m.group("gen") or "")]
        typeofs = [t for t in re.findall(r"typeof\((\w+)\)", body) if t not in generic]
        # DependencyProperty.Register(name, typeof(Value), typeof(Owner), ...): the value type is the first.
        value_type = typeofs[0] if len(typeofs) >= 2 else None
        owner = typeofs[-1] if typeofs else None
        flags = [f for f, word in (("FrameworkPropertyMetadataOptions.AffectsRender", "AffectsRender"),
                                   ("FrameworkPropertyMetadataOptions.BindsTwoWayByDefault", "BindsTwoWayByDefault"))
                 if word in body]
        # A helper without a value parameter has its default in its own metadata: new FrameworkPropertyMetadata(false, …).
        default = re.search(r"new FrameworkPropertyMetadata\(([^,()]+)", body)
        helpers[m.group("name")] = (m.span(), owner, value_type, flags, len(generic) == 2, default.group(1).strip() if default else None)
    if not helpers:
        return s

    for span, *_ in sorted(helpers.values(), key=lambda h: h[0][0], reverse=True):
        s = s[:span[0]] + "\n" + s[span[1]:]

    call = re.compile(rf"public static readonly DependencyProperty (\w+)Property = ({'|'.join(map(re.escape, helpers))})(?:<([^>]+)>)?\(")
    out, i = [], 0
    while (m := call.search(s, i)) is not None:
        out.append(s[i:m.start()])
        close = match_bracket(s, m.end() - 1)
        args = split_top(s[m.end():close])
        name = m.group(1)
        _, owner, value_type, flags, _, default = helpers[m.group(2)]
        if m.group(3) and helpers[m.group(2)][4]:
            owner, type_ = (a.strip() for a in split_top(m.group(3)))
        elif m.group(3):
            type_ = m.group(3).strip()
        elif value_type:
            type_ = value_type
        else:
            clr = re.search(rf"public ([\w.<>?,\[\] ]+?) {name}\s*(?:\{{|=>)", s)
            type_ = clr.group(1) if clr else "object"
        meta = [args[1] if len(args) > 1 else default]
        if flags:
            meta.append(" | ".join(flags))
        if len(args) > 2:
            callback = re.sub(r"^\(\s*d\s*,\s*_\s*\)", "(d, e)", args[2].strip())
            meta.append(re.sub(r"\bd\.", f"(({owner})d).", callback))
        out.append(f"public static readonly DependencyProperty {name}Property = DependencyProperty.Register("
                   f"nameof({name}), typeof({type_}), typeof({owner}), new FrameworkPropertyMetadata({', '.join(meta)}))")
        i = close + 1
    out.append(s[i:])
    return "".join(out)


CLASS = re.compile(r"^(?P<head>(?:(?:public|internal|private|protected|sealed|abstract|static|partial)\s+)*class (?P<name>\w+)[^\n]*)\n\{\n", re.M)


def convert_late(s: str) -> str:
    """Rewrites of what the other passes produced: run last."""
    arg = r"((?:[^,()]|\((?:[^()]|\([^()]*\))*\))+)"
    # A pen given a dash style after it is made: the dash goes into the immutable pen.
    s = re.sub(
        rf"(?P<lhs>\w+) = new ImmutablePen\({arg}, {arg}, null, {arg}, {arg}\);\n(?P<i>[ \t]*)if \((?P<c>[\w!.]+)\) (?P=lhs)\.DashStyle = new DashStyle\(\[(?P<d>[^\]]*)\], 0\);",
        lambda m: f"{m['lhs']} = new ImmutablePen({m[2]}, {m[3]}, {m['c']} ? new ImmutableDashStyle([{m['d']}], 0) : null, {m[4]}, {m[5]});",
        s)
    s = re.sub(
        rf"(?P<lhs>\w+) = new Pen\({arg}, {arg}\) \{{ LineJoin = ([\w.]+) \}};\n(?P<i>[ \t]*)if \((?P<c>[\w!.]+)\) (?P=lhs)\.DashStyle = new DashStyle\(\[(?P<d>[^\]]*)\], 0\);",
        lambda m: f"{m['lhs']} = new ImmutablePen(((IBrush){m[2]}).ToImmutable(), {m[3]}, {m['c']} ? new ImmutableDashStyle([{m['d']}], 0) : null, PenLineCap.Flat, {m[4]});",
        s)

    # SpaceDrawing.Freeze(new LinearGradientBrush(...)) becomes SpaceDrawing.SpaceDrawing.LinearGradient(...).
    s = s.replace("SpaceDrawing.SpaceDrawing.", "SpaceDrawing.")
    s = rewrite_visual_bitmaps(s)
    s = rewrite_initialized_gradients(s)
    s = rewrite_frozen_pens(s)
    s = rewrite_static_calls(s, "Rect.Union", lambda a: f"{a[0]}.Union({a[1]})" if len(a) == 2 else None)
    return s


def _relative_point(expr: str, unit: str) -> str:
    point = re.fullmatch(r"new Point\((.*)\)", expr.strip(), re.S)
    if point and len(split_top(point.group(1))) == 2:
        return f"new RelativePoint({point.group(1)}, RelativeUnit.{unit})"
    return f"new RelativePoint({expr}, RelativeUnit.{unit})"


def _initializer(text: str, start: int) -> tuple[dict[str, str], int] | None:
    """The `{ Name = value, ... }` right after start (whitespace allowed), and the index after it."""
    j = start
    while j < len(text) and text[j] in " \t\r\n":
        j += 1
    if j >= len(text) or text[j] != "{":
        return None
    close = match_bracket(text, j)
    entries = {}
    for entry in split_top(text[j + 1:close]):
        name, eq, value = entry.partition("=")
        if not eq or not re.fullmatch(r"\s*\w+\s*", name):
            return None
        entries[name.strip()] = value.strip()
    return entries, close + 1


def rewrite_initialized_gradients(text: str) -> str:
    """
    Gradient brushes set up with an object initializer (MappingMode, StartPoint/EndPoint, Center, GradientOrigin,
    RadiusX/RadiusY), frozen or not, become immutable gradient brushes with those as relative or absolute points.
    """
    out, i = [], 0
    pattern = re.compile(r"(Freeze\()?new (Linear|Radial)GradientBrush\(")
    while (m := pattern.search(text, i)) is not None:
        close = match_bracket(text, m.end() - 1)
        found = _initializer(text, close + 1)
        if found is None:
            out.append(text[i:m.end()])
            i = m.end()
            continue
        entries, end = found
        if m.group(1):
            rest = text[end:].lstrip()
            if not rest.startswith(")"):
                out.append(text[i:m.end()])
                i = m.end()
                continue
            end = len(text) - len(rest) + 1
        args = split_top(text[m.end():close])
        try:
            if args and args[0].startswith("["):
                stops = [split_top(st[1:-1]) for st in _stops(args[0][1:-1])]
            elif m.group(2) == "Linear" and len(args) == 3:
                stops = [[args[0], "0"], [args[1], "1"]]
            elif m.group(2) == "Radial" and len(args) == 2:
                stops = [[args[0], "0"], [args[1], "1"]]
            else:
                raise ValueError(args)
        except ValueError:
            out.append(text[i:m.end()])
            i = m.end()
            continue
        unit = "Absolute" if entries.pop("MappingMode", "").endswith("Absolute") else "Relative"
        line_start = text.rfind("\n", 0, m.start()) + 1
        indent = re.match(r"[ \t]*", text[line_start:]).group(0)
        named = []
        if m.group(2) == "Linear":
            # new LinearGradientBrush(stops, start, end) { MappingMode = … }: the points are arguments.
            if args[0].startswith("[") and len(args) == 3:
                entries.setdefault("StartPoint", args[1])
                entries.setdefault("EndPoint", args[2])
            if "StartPoint" not in entries or "EndPoint" not in entries:  # an angle only: by hand
                out.append(text[i:m.end()])
                i = m.end()
                continue
            known = {"StartPoint": "startPoint", "EndPoint": "endPoint"}
            for key, name in known.items():
                if key in entries:
                    named.append(f"{name}: {_relative_point(entries.pop(key), unit)}")
            brush = "ImmutableLinearGradientBrush"
        else:
            for key, name in {"Center": "center", "GradientOrigin": "gradientOrigin"}.items():
                if key in entries:
                    named.append(f"{name}: {_relative_point(entries.pop(key), unit)}")
            for key, name in {"RadiusX": "radiusX", "RadiusY": "radiusY"}.items():
                named.append(f"{name}: new RelativeScalar({entries.pop(key, '0.5')}, RelativeUnit.{unit})")
            brush = "ImmutableRadialGradientBrush"
        if entries:  # something else set up: by hand
            out.append(text[i:m.end()])
            i = m.end()
            continue
        stop_lines = "".join(f"{indent}    new ImmutableGradientStop({o}, {c}),\n" for c, o in stops)
        out.append(text[i:m.start()])
        out.append(f"new {brush}(\n{indent}[\n{stop_lines}{indent}],\n{indent}    " + f",\n{indent}    ".join(named) + ")")
        i = end
    out.append(text[i:])
    return "".join(out)


VISUAL = re.compile(
    r"(?P<i>[ \t]*)var (?P<v>\w+) = new DrawingVisual\(\);\n[ \t]*using \(var (?P<c>\w+) = (?P=v)\.RenderOpen\(\)\)\s*")
TARGET = re.compile(r"\s*(?P<lhs>(?:var )?(?P<name>[\w.]+)) = new RenderTargetBitmap\(")


def rewrite_visual_bitmaps(text: str) -> str:
    """
    A DrawingVisual drawn into and rendered to a RenderTargetBitmap:
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) {…}
        bitmap = new RenderTargetBitmap(w, h, dpiX, dpiY, PixelFormats.Pbgra32); bitmap.Render(visual);
    becomes bitmap = SpaceDrawing.RenderPixels(w, h, dpiX, dc => {…});
    """
    out, i = [], 0
    while (m := VISUAL.search(text, i)) is not None:
        start = m.end()
        if text[start] == "{":
            body_end = match_bracket(text, start) + 1
            body = text[start:body_end]
        else:
            body_end = text.index(";\n", start) + 1
            body = text[start:body_end - 1]
        t = TARGET.match(text, body_end)
        if t is None:
            out.append(text[i:m.end()])
            i = m.end()
            continue
        close = match_bracket(text, t.end() - 1)
        args = split_top(text[t.end():close])
        render = re.compile(rf";\s*\n[ \t]*{re.escape(t['name'])}\.Render\({m['v']}\);\n").match(text, close + 1)
        if len(args) != 5 or render is None:
            out.append(text[i:m.end()])
            i = m.end()
            continue
        out.append(text[i:m.start()])
        out.append(f"{m['i']}{t['lhs'].strip()} = SpaceDrawing.RenderPixels({args[0]}, {args[1]}, {args[2]}, {m['c']} =>"
                   + (f"\n{m['i']}{body});\n" if body.startswith("{") else f" {body});\n"))
        i = render.end()
    out.append(text[i:])
    return "".join(out)


def rewrite_frozen_pens(text: str) -> str:
    """Freeze(new Pen(brush, width)) and Freeze(new Pen(brush, width) { DashStyle = … }): immutable pens."""
    out, i = [], 0
    pattern = re.compile(r"Freeze\(new Pen\(")
    while (m := pattern.search(text, i)) is not None:
        close = match_bracket(text, m.end() - 1)
        args = split_top(text[m.end():close])
        found = _initializer(text, close + 1)
        entries, end = found if found else ({}, close + 1)
        rest = text[end:].lstrip()
        dash = entries.pop("DashStyle", None)
        if len(args) != 2 or entries or not rest.startswith(")"):
            out.append(text[i:m.end()])
            i = m.end()
            continue
        end = len(text) - len(rest) + 1
        dash_style = dash.replace("new DashStyle(", "new ImmutableDashStyle(") if dash else None
        out.append(text[i:m.start()])
        out.append(f"new ImmutablePen(((IBrush){args[0]}).ToImmutable(), {args[1]}" + (f", {dash_style}" if dash_style else "") + ")")
        i = end
    out.append(text[i:])
    return "".join(out)


def rewrite_static_calls(text: str, name: str, build) -> str:
    """Rewrites each call of name(...) whose arguments build accepts (it returns None to leave one)."""
    out, i = [], 0
    pattern = re.compile(rf"\b{re.escape(name)}\(")
    while (m := pattern.search(text, i)) is not None:
        close = match_bracket(text, m.end() - 1)
        replacement = build(split_top(text[m.end():close]))
        if replacement is None:
            out.append(text[i:m.end()])
            i = m.end()
            continue
        out.append(text[i:m.start()] + replacement)
        i = close + 1
    out.append(text[i:])
    return "".join(out)


def convert_recurring(s: str) -> str:
    """Idioms many of the original's diagrams share, beyond the one-line rewrites."""
    arg = r"((?:[^,()]|\((?:[^()]|\([^()]*\))*\))+)"

    # A line through points as one figure: BeginFigure(p, filled) ... LineTo(p), then EndFigure(closed) after the loop.
    s = re.sub(
        r"(?P<i>[ \t]*)var first = true;\n(?P<j>[ \t]*)foreach \((?P<head>[^\n]*)\)\n(?P<k>[ \t]*)\{\n"
        r"(?P<l>[ \t]*)if \(first\) (?P<c>\w+)\.BeginFigure\((?P<p>\w+), (?P<filled>[\w!.]+), (?P<closed>[\w!.]+)\);\n"
        r"(?P=l)else (?P=c)\.LineTo\((?P=p)(?:, (?:true|false), (?:true|false))?\);\n(?P=l)first = false;\n(?P=k)\}\n",
        lambda m: (f"{m['i']}var first = true;\n{m['j']}foreach ({m['head']})\n{m['k']}{{\n"
                   f"{m['l']}if (first) {m['c']}.BeginFigure({m['p']}, {m['filled']});\n"
                   f"{m['l']}else {m['c']}.LineTo({m['p']});\n{m['l']}first = false;\n{m['k']}}}\n\n"
                   f"{m['i']}if (!first) {m['c']}.EndFigure({m['closed']});\n"),
        s)

    # A radial gradient over what it fills, centre and radii relative to it.
    s = re.sub(
        rf"(?:Freeze\()?new RadialGradientBrush\({arg}, {arg}\)\s*\{{\s*Center = new Point\({arg}, {arg}\),\s*"
        rf"GradientOrigin = new Point\({arg}, {arg}\),\s*RadiusX = {arg},\s*RadiusY = {arg},?\s*\}}\)?",
        lambda m: ("new ImmutableRadialGradientBrush(\n"
                   f"            [new ImmutableGradientStop(0, {m[1]}), new ImmutableGradientStop(1, {m[2]})],\n"
                   f"            center: new RelativePoint({m[3]}, {m[4]}, RelativeUnit.Relative),\n"
                   f"            gradientOrigin: new RelativePoint({m[5]}, {m[6]}, RelativeUnit.Relative),\n"
                   f"            radiusX: new RelativeScalar({m[7]}, RelativeUnit.Relative),\n"
                   f"            radiusY: new RelativeScalar({m[8]}, RelativeUnit.Relative))"),
        s)

    # Rect.Inflate returns a new rect; Rect.Union is an instance method.
    s = re.sub(r"^([ \t]*)(\w+)\.Inflate\(([^;]+)\);$", r"\1\2 = \2.Inflate(new Thickness(\3));", s, flags=re.M)
    s = re.sub(rf"\bRect\.Union\({arg}, {arg}\)", r"\1.Union(\2)", s)

    # Capturing the pointer cannot fail in Avalonia.
    s = re.sub(r"^([ \t]*)if \((.*) \|\| !CaptureMouse\(\)\) return;\n", r"\1if (\2) return;\n\1e.Pointer.Capture(this);\n", s, flags=re.M)
    # (and as a statement block)
    s = re.sub(r"^([ \t]*)if \(!CaptureMouse\(\)\)\n[ \t]*\{\n(?:[^\n]*\n)*?[ \t]*return;\n[ \t]*\}\n",
               r"\1e.Pointer.Capture(this);\n", s, flags=re.M)

    # A pen with line caps (and a join): an immutable pen.
    s = re.sub(
        rf"(?:Freeze\()?new Pen\({arg}, {arg}\)\s*\{{\s*StartLineCap = ([\w.]+),\s*EndLineCap = [\w.]+(?:,\s*LineJoin = ([\w.]+))?,?\s*\}}\)?",
        lambda m: f"new ImmutablePen(((IBrush){m[1]}).ToImmutable(), {m[2]}, null, {m[3]}, {m[4] or 'PenLineJoin.Miter'})",
        s)
    return s


def convert_properties(s: str, part: str | None = None) -> str:
    """
    DependencyProperty.Register -> AvaloniaProperty.Register, with callbacks, AffectsRender and attach/detach, for each
    class of the file. <paramref name="part"/> names the file's part of a partial class (Objects in X.Objects.cs).
    """
    s = expand_register_helpers(s)
    out, i = [], 0
    for m in CLASS.finditer(s):
        if m.start() < i:
            continue
        brace = m.end() - 2
        close = match_bracket(s, brace)
        out.append(s[i:brace + 2])
        out.append(convert_class(s[brace + 2:close], m.group("name"), "partial " in m.group("head"), " : " in m.group("head"), part))
        i = close
    out.append(s[i:])
    s = "".join(out)

    s = re.sub(r"\((?:[\w.]+(?:<[\w.,\s?]+>)?\??)\)GetValue\(", "GetValue(", s)
    s = re.sub(r"class (\w+) : FrameworkElement", r"class \1 : Control", s)
    s = re.sub(r"protected override void OnRender\(PaintContext dc\)\s*\{",
               "public override void Render(DrawingContext context)\n    {\n        using var dc = new PaintContext(context);", s)

    head = s.split("namespace")[0]
    for using in ("Avalonia.Controls", "Avalonia.Data"):
        if f"using {using};" not in head:
            s = s.replace("using Avalonia;\n", f"using Avalonia;\nusing {using};\n", 1)
    return s


def convert_class(body: str, owner: str, partial: bool, has_base: bool, part: str | None) -> str:
    """One class body: its properties, then the members they need, at the top of the class (see convert_properties)."""
    affects_render, callbacks = [], []
    out, i, last_property = [], 0, 0
    pattern = re.compile(r"public static readonly DependencyProperty (\w+)Property = DependencyProperty\.Register\(")
    while True:
        m = pattern.search(body, i)
        if not m:
            out.append(body[i:])
            break
        out.append(body[i:m.start()])
        close = match_bracket(body, m.end() - 1)
        args = split_top(body[m.end():close])
        name = m.group(1)
        type_ = args[1][len("typeof("):-1]
        registered_owner = args[2][len("typeof("):-1]
        default, two_way, render, callback = None, False, False, None
        if len(args) > 3:
            meta = args[3]
            meta_args = split_top(meta[meta.index("(") + 1:meta.rindex(")")])
            if meta_args:
                default = meta_args[0]
            for extra in meta_args[1:]:
                two_way |= "BindsTwoWayByDefault" in extra
                render |= "AffectsRender" in extra
                if "=>" in extra:
                    callback = extra
        t = type_ + ("?" if default == "null" and not type_.endswith("?") else "")
        params = [f"nameof({name})"]
        if default not in (None, "null"):
            params.append(default)
        if two_way:
            params.append("defaultBindingMode: BindingMode.TwoWay")
        out.append(f"public static readonly StyledProperty<{t}> {name}Property =\n"
                   f"        AvaloniaProperty.Register<{registered_owner}, {t}>({', '.join(params)})")
        if render:
            affects_render.append(f"{name}Property")
        if callback:
            cb = callback.split("=>", 1)[1].strip()
            cb = re.sub(rf"\(\({owner}\)d\)\.", "", cb)
            cb = re.sub(r"e\.NewValue as [\w.<>?]+|\([\w.<>?]+\)e\.NewValue", f"change.GetNewValue<{t}>()", cb)
            cb = re.sub(r"e\.OldValue as [\w.<>?]+|\([\w.<>?]+\)e\.OldValue", f"change.GetOldValue<{t}>()", cb)
            callbacks.append((name, cb))
        i = close + 1
        last_property = sum(len(x) for x in out) + 1  # just past the declaration's ';'
    body = "".join(out)

    language = False
    if re.search(r"Loaded \+= \(_, _\) => LocalizationService\.Instance\.PropertyChanged \+= OnLanguageChanged;", body):
        body = re.sub(r"^\s*Loaded \+= \(_, _\) => LocalizationService\.Instance\.PropertyChanged \+= OnLanguageChanged;\n", "", body, flags=re.M)
        body = re.sub(r"^\s*Unloaded \+= \(_, _\) => LocalizationService\.Instance\.PropertyChanged -= OnLanguageChanged;\n", "", body, flags=re.M)
        language = True

    if affects_render and partial:
        # A partial class split over files has one static constructor at most, so each file registers its own
        # properties from a static field placed after them (initializers run in order within a file).
        suffix = part or "Properties"
        redraw = (f"\n\n    // Redraws when these change. A field rather than a static constructor: the class is split over files.\n"
                  f"    private static readonly bool s_redrawsOn{suffix} = RedrawOn{suffix}();\n\n"
                  f"    private static bool RedrawOn{suffix}()\n    {{\n        AffectsRender<{owner}>({', '.join(affects_render)});\n"
                  f"        return true;\n    }}")
        line_end = body.index("\n", last_property) if "\n" in body[last_property:] else len(body)
        body = body[:line_end] + redraw + body[line_end:]

    generated = []
    if affects_render and not partial:
        generated.append(f"    static {owner}()\n    {{\n        AffectsRender<{owner}>({', '.join(affects_render)});\n    }}\n")
    if callbacks:
        method = "protected override void OnPropertyChanged" if not (partial and part) else f"private void OnPropertyChanged{part}"
        lines = [f"    {method}(AvaloniaPropertyChangedEventArgs change)", "    {"]
        if not (partial and part):
            lines.append("        base.OnPropertyChanged(change);")
        for n, (name, cb) in enumerate(callbacks):
            lines += [f"        {'if' if n == 0 else 'else if'} (change.Property == {name}Property)", "        {",
                      f"            {cb};", "        }"]
        lines.append("    }\n")
        generated.append("\n".join(lines))
    if language:
        generated.append("""    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
    }
""")
    # Only on the part that names the base class: the other parts of a partial class share these.
    if has_base and ("ActualWidth" in body or "RenderScale()" in body) and not re.search(r"\bActualWidth\s*=>", body):
        generated.append("""    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
""")
    return ("\n".join(generated) + "\n" if generated else "") + body


PLUMBING = [
    (r"protected override void OnMouseLeftButtonDown\(MouseButtonEventArgs e\)\n(\s*)\{\n(\s*)base\.OnMouseLeftButtonDown\(e\);",
     r"protected override void OnPointerPressed(PointerPressedEventArgs e)\n\1{\n\2base.OnPointerPressed(e);\n\2if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;"),
    (r"if \(!CaptureMouse\(\)\) return;", "e.Pointer.Capture(this);"),
    (r"(?<!\.)CaptureMouse\(\);", "e.Pointer.Capture(this);"),
    (r"protected override void OnMouseMove\(MouseEventArgs e\)", "protected override void OnPointerMoved(PointerEventArgs e)"),
    (r"base\.OnMouseMove\(e\);", "base.OnPointerMoved(e);"),
    (r"protected override void OnMouseLeftButtonUp\(MouseButtonEventArgs e\)", "protected override void OnPointerReleased(PointerReleasedEventArgs e)"),
    (r"base\.OnMouseLeftButtonUp\(e\);", "base.OnPointerReleased(e);"),
    (r"if \(IsMouseCaptured\) ReleaseMouseCapture\(\);", "e.Pointer.Capture(null);"),
    (r"(?<!\.)ReleaseMouseCapture\(\);", "e.Pointer.Capture(null);"),
    (r"\bIsMouseCaptured\b", "ReferenceEquals(e.Pointer.Captured, this)"),
    (r"protected override void OnMouseLeave\(MouseEventArgs e\)", "protected override void OnPointerExited(PointerEventArgs e)"),
    (r"base\.OnMouseLeave\(e\);", "base.OnPointerExited(e);"),
    (r"protected override void OnLostMouseCapture\(MouseEventArgs e\)", "protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)"),
    (r"base\.OnLostMouseCapture\(e\);", "base.OnPointerCaptureLost(e);"),
    (r"protected override void OnMouseWheel\(MouseWheelEventArgs e\)", "protected override void OnPointerWheelChanged(PointerWheelEventArgs e)"),
    (r"base\.OnMouseWheel\(e\);", "base.OnPointerWheelChanged(e);"),
    (r"e\.Delta / 120\.0", "e.Delta.Y"),
    (r"Cursors\.Hand", "new Cursor(StandardCursorType.Hand)"),
    (r"Cursors\.SizeNS", "new Cursor(StandardCursorType.SizeNorthSouth)"),
    (r"Cursors\.SizeWE", "new Cursor(StandardCursorType.SizeWestEast)"),
    (r"Cursors\.SizeAll", "new Cursor(StandardCursorType.SizeAll)"),
    (r"Cursors\.Cross", "new Cursor(StandardCursorType.Cross)"),
    (r"Cursors\.Arrow", "Cursor.Default"),
    (r"^\s*Focusable = false;\n", ""),
    (r"^\s*FocusVisualStyle = null;\n", ""),
    (r"^\s*protected override AutomationPeer OnCreateAutomationPeer\(\) => new FrameworkElementAutomationPeer\(this\);\n\n?", ""),
    (r"\((\w+(?:\.\w+)?) - (\w+(?:\.\w+)?)\)\.Length\b", r"((Vector)(\1 - \2)).Length"),
    (r"^\s*Loaded \+= OnLoaded;\n", ""),
    (r"^\s*Unloaded \+= OnUnloaded;\n", ""),
    (r"private void OnLoaded\(object sender, RoutedEventArgs e\)\n(\s*)\{\n",
     r"protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)\n\1{\n\1    base.OnAttachedToVisualTree(e);\n\1    _attached = true;\n"),
    (r"private void OnUnloaded\(object sender, RoutedEventArgs e\)\n(\s*)\{\n",
     r"protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)\n\1{\n\1    base.OnDetachedFromVisualTree(e);\n\1    _attached = false;\n"),
    (r"^\s*CompositionTarget\.Rendering -= OnRendering;\n", ""),
    (r"CompositionTarget\.Rendering \+= OnRendering;", "RequestFrame();"),
    (r"private void OnRendering\(object\? sender, EventArgs e\)", "private void OnRendering()"),
    (r"if \(!IsVisible \|\|", "if (!IsEffectivelyVisible ||"),
    (r"<see cref=\"CompositionTarget\.Rendering\"/> while loaded and visible", "an animation frame per screen refresh while it is on screen"),
    (r"protected override void OnRenderSizeChanged\(SizeChangedInfo sizeInfo\)\n(\s*)\{\n(\s*)base\.OnRenderSizeChanged\(sizeInfo\);",
     r"protected override void OnSizeChanged(SizeChangedEventArgs e)\n\1{\n\2base.OnSizeChanged(e);"),
]

FRAME_LOOP = """    private bool _attached;

    private void RequestFrame()
    {
        if (_attached)
        {
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
        }
    }

    private void OnFrame(TimeSpan time)
    {
        if (!_attached)
        {
            return;
        }

        OnRendering();
        RequestFrame();
    }

"""


def convert_plumbing(s: str) -> str:
    for pattern, replacement in PLUMBING:
        s = re.sub(pattern, replacement, s, flags=re.M)
    if "RequestFrame();" in s and "private void RequestFrame()" not in s and "protected override void OnAttachedToVisualTree" in s:
        i = s.index("    protected override void OnAttachedToVisualTree")
        s = s[:i] + FRAME_LOOP + s[i:]
    if ("Cursor(" in s or "PointerPressedEventArgs" in s) and "using Avalonia.Input;" not in s:
        s = s.replace("using Avalonia;\n", "using Avalonia;\nusing Avalonia.Input;\n", 1)
    return s


def convert_points(s: str) -> str:
    """WPF Point/Vector idioms that mean something else, or nothing, in Avalonia."""
    # A Point difference is a Point in Avalonia: `var x = a - b;` whose Length is used becomes a Vector.
    for m in reversed(list(re.finditer(r"\bvar (\w+) = ([\w.()]+) - ([\w.()]+);", s))):
        rest = s[m.end():m.end() + 1500]
        if re.search(rf"\b{m.group(1)}\.(?:Length|Normalize\(\))|\b{m.group(1)} /= ", rest):
            s = s[:m.start()] + f"Vector {m.group(1)} = {m.group(2)} - {m.group(3)};" + s[m.end():]
    # WPF's Vector.Normalize() changes the vector; Avalonia's returns a new one and leaves it as it was.
    s = re.sub(r"^(\s*)(\w+)\.Normalize\(\);$", r"\1\2 = \2.Normalize();", s, flags=re.M)
    # (and Rect.Offset moves the rect; Avalonia's Translate returns a moved one)
    s = re.sub(r"^(\s*)(\w+)\.Offset\((.+)\);$", r"\1\2 = \2.Translate(new Vector(\3));", s, flags=re.M)
    s = s.replace("Vector.AngleBetween(", "SpaceDrawing.AngleBetween(")
    s = s.replace("Vector.Multiply(", "Vector.Dot(")
    # (also after an `if (...)` on the same line). Not a variable also changed with `v.X +=`: that is one of the file's
    # own classes with an X field, which assigns fine.
    def with_xy(m):
        if re.search(rf"\b{m.group(2)}\.[XY] [-+*/]= ", s):
            return m.group(0)
        return f"{m.group(1)}{m.group(2)} = {m.group(2)}.With{m.group(3)}({m.group(4)});"
    s = re.sub(r"^(\s*(?:if \((?:[^()\n]|\([^()\n]*\))*\) )?)(\w+)\.([XY]) = (.+);$", with_xy, s, flags=re.M)
    s = s.replace("return Geometry.Empty;", "return null;")
    s = re.sub(r"!(\w+)\.IsEmpty\(\)", r"\1 is not null", s)
    s = re.sub(r"(\w+)\.IsEmpty\(\)", r"\1 is null", s)
    return s


LEFT_FOR_HAND = ("DependencyProperty", "Freeze(", "MouseButton", "CompositionTarget", "WritePixels", "PathGeometry",
                 "PathFigure", "new Pen(", "TextElement", "FormattedText(", "Cursors.", "OnRender(", "RoutedEventArgs",
                 "MappingMode", "Geometry.Empty")

# Patterns rather than text: the book's own immutable brushes and `System.Windows.Input` (ICommand) are fine.
LEFT_FOR_HAND_PATTERNS = {
    "System.Windows.": r"System\.Windows\.(?!Input;)",
    "GradientBrush(": r"(?<!\w)(?:Linear|Radial)?GradientBrush\(",
    "SolidColorBrush(": r"(?<!\w)SolidColorBrush\(",
}


def report(name: str, s: str) -> None:
    left = [n for n in LEFT_FOR_HAND if n in s]
    left += [n for n, pattern in LEFT_FOR_HAND_PATTERNS.items() if re.search(pattern, s)]
    figures = len(re.findall(r"BeginFigure\([^;]*, (?:true|false), (?:true|false)\)", s))
    if figures:
        left.append(f"{figures} BeginFigure(p, filled, closed)")
    inflates = len(re.findall(r"^\s*\w+\.Inflate\(", s, flags=re.M))
    if inflates:
        left.append(f"{inflates} Rect.Inflate used as a statement (it returns a new Rect)")
    print(f"{name}: fix by hand: {', '.join(left)}" if left else f"{name}: nothing flagged")


def drawing(args: argparse.Namespace) -> None:
    s = Path(args.source).read_text(encoding="utf-8")
    s = apply_namespaces(s, args.namespace, args.source_prefix, args.map)
    s = convert_types(s, args.namespace)
    part = Path(args.target).stem.split('.')[1] if '.' in Path(args.target).stem else None
    s = convert_properties(s, part)
    s = convert_plumbing(s)
    s = convert_points(s)
    s = convert_late(s)
    write(args.target, s)
    report(Path(args.target).name, s)
    # Anything still naming the original (resource names, string literals) must not reach the repository.
    for number, line in enumerate(s.splitlines(), 1):
        if args.source_prefix in line:
            print(f"  line {number} still names the original app: {line.strip()[:100]}")


# ---- view models -----------------------------------------------------------------------------------------------

CHAPTER_LIFETIMES = """

    /// <summary>A view of the chapter came on screen.</summary>
    public void ViewAttached() => _views++;

    /// <summary>The last view of the chapter left the screen: the walkthrough and any explanation being read stop.</summary>
    public void ViewDetached()
    {
        _views = Math.Max(0, _views - 1);
        if (_views > 0)
        {
            return;
        }

        Lesson.Stop();
        Card.Close();
    }"""


def viewmodel(args: argparse.Namespace) -> None:
    s = Path(args.source).read_text(encoding="utf-8")
    s = apply_namespaces(s, args.namespace, args.source_prefix, args.map)
    s = s.replace("using Volo.Abp.DependencyInjection;\n", "").replace(", ITransientDependency", "")
    # Usings of the original's own plumbing (DI, navigation, constants, controls folders already mapped away).
    s = re.sub(rf"^using {re.escape(args.source_prefix)}\.[\w.]+;\n", "", s, flags=re.M)

    if args.chapter:
        cls = re.search(r"public (?:sealed )?partial class (\w+ViewModel)", s).group(1)
        s = s.replace("using System.Windows.Media;\n", "using Avalonia.Media;\nusing Avalonia.Media.Immutable;\n")
        s = s.replace(f"using {TARGET_ROOT}.Explore.Lessons;\n",
                      f"using {TARGET_ROOT}.Controls.Explore;\nusing {TARGET_ROOT}.Explore.Lessons;\n")
        s = re.sub(r"(?<![\w.])Brush(?=[\s\[\]?>,)])", "IBrush", s)
        s = re.sub(r"    private static IBrush Frozen\(byte r, byte g, byte b\)\n    \{\n.*?\n    \}\n",
                   "    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));\n",
                   s, flags=re.S)
        s = re.sub(r"    private static IBrush Frozen\(Color color\)\n    \{\n.*?\n    \}\n",
                   "    private static IBrush Frozen(Color color) => new ImmutableSolidColorBrush(color);\n",
                   s, flags=re.S)
        if "SpaceDrawing." in s and f"using {TARGET_ROOT}.Controls.Space;" not in s:
            s = s.replace(f"using {TARGET_ROOT}.Controls.Explore;\n",
                          f"using {TARGET_ROOT}.Controls.Explore;\nusing {TARGET_ROOT}.Controls.Space;\n", 1)
        s = re.sub(r"^\s*\w+\.Freeze\(\);\s*\n", "", s, flags=re.M)
        s = re.sub(r"new SolidColorBrush\(", "new ImmutableSolidColorBrush(", s)
        s = s.replace("System.Windows.Threading.DispatcherTimer", "Avalonia.Threading.DispatcherTimer")
        s = s.replace(": ObservableObject, IDisposable", ": ObservableObject, IChapterContent, IDisposable")
        s = s.replace("    private readonly ILocalizationService _localization;\n",
                      "    private readonly ILocalizationService _localization = LocalizationService.Instance;\n    private int _views;\n")
        s = re.sub(r"ILocalizationService localization, ", "", s)
        s = re.sub(r"^\s*_localization = localization;\n", "", s, flags=re.M)
        ctor = re.search(rf"\n    public {cls}\([^)]*\)\n    \{{\n", s)
        close = match_bracket(s, ctor.end() - 2)
        s = s[:close + 1] + CHAPTER_LIFETIMES + s[close + 1:]
        s = re.sub(r"/// The (\w+) page\b", r"/// The \1 chapter", s)
        # Opening another Explore page turns to that chapter of the book; its slug is the route's last segment.
        s = s.replace("IMenuNavigator", "IBookNavigator")
        # Avalonia's indexer bindings listen for the indexer's own name, not WPF's "Item[]".
        s = s.replace('OnPropertyChanged("Item[]");', 'OnPropertyChanged("Item");')
        s = re.sub(r"_navigator\.NavigateTo\(Constants\.Explore(\w+)\)",
                   lambda m: f'_navigator.GoToPage(BookContents.ChapterPageOf("{re.sub(r"(?<=[a-z])(?=[A-Z])", "-", m.group(1))}"))', s)
        if "IBookNavigator" in s and f"using {TARGET_ROOT}.ViewModels.Pages;" not in s:
            s = s.replace(f"using {TARGET_ROOT}.Explore.Lessons;\n",
                          f"using {TARGET_ROOT}.Explore.Lessons;\nusing {TARGET_ROOT}.Contents;\nusing {TARGET_ROOT}.ViewModels.Pages;\n")

    if args.replacements:
        for old, new in runpy.run_path(args.replacements)["REPLACEMENTS"]:
            if old not in s:
                sys.exit(f"{Path(args.target).name}: replacement not found: {old[:100]!r}")
            s = s.replace(old, new)

    write(args.target, s)
    left = [line.strip() for line in s.splitlines() if args.source_prefix in line or "ITransientDependency" in line]
    print(f"{Path(args.target).name}: fix by hand: {left}" if left else f"{Path(args.target).name}: ok")


# ---- strings ---------------------------------------------------------------------------------------------------

RESX_FILES = ("AppStrings.resx", "AppStrings.zh-CN.resx")


def load_resx(path: Path) -> dict[str, str]:
    root = ET.parse(path).getroot()
    return {d.get("name"): (d.find("value").text or "") for d in root.findall("data")}


LAYOUT_WORDS = re.compile(r"\bon the (?:right|left)\b|\bside panel\b|\bthe panel\b|\bthis page\b|\bmenu\b|右侧|左侧|右边|左边|侧边栏|侧栏|边栏|面板|本页面|菜单", re.I)


def strings(args: argparse.Namespace) -> None:
    def wanted(key: str) -> bool:
        return any(key.startswith(p[:-1]) if p.endswith("*") else key == p for p in args.patterns)

    added = 0
    for name in RESX_FILES:
        source = load_resx(Path(args.source) / name)
        target_path = Path(args.target) / name
        target = load_resx(target_path)
        for key, value in source.items():
            if wanted(key) and key not in target:
                target[key] = value
                added += 1
                # The original put the controls in a side panel on the right; the book puts them on the left page.
                if LAYOUT_WORDS.search(value):
                    print(f"  {name} {key}: mentions the original layout: {value[:90]}")
        text = target_path.read_text(encoding="utf-8")
        header = text[: text.rindex("</resheader>\n") + len("</resheader>\n")]
        body = "".join(f'  <data name="{k}" xml:space="preserve">\n    <value>{escape(v)}</value>\n  </data>\n'
                       for k, v in sorted(target.items()))
        target_path.write_text(header + body + "</root>\n", encoding="utf-8", newline="\r\n")
    print(f"{added} values added")


# ---- command line ----------------------------------------------------------------------------------------------


def write(target: str, s: str) -> None:
    path = Path(target)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(s, encoding="utf-8")


def main(argv: list[str] | None = None) -> None:
    # Reports quote Chinese strings; a console in another code page would garble them.
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    sub = parser.add_subparsers(dest="command", required=True)
    for name, func in (("drawing", drawing), ("viewmodel", viewmodel)):
        p = sub.add_parser(name)
        p.add_argument("source")
        p.add_argument("target")
        p.add_argument("--namespace", required=True, help="namespace of the ported file in this book")
        p.add_argument("--source-prefix", required=True, help="root namespace of the original app")
        p.add_argument("--map", action="append", default=[], help="Original.Sub=Book.Sub, below the two roots")
        if name == "viewmodel":
            p.add_argument("--chapter", action="store_true", help="a chapter view model: IChapterContent, brushes")
            p.add_argument("--replacements", help="a .py file with REPLACEMENTS = [(old, new), ...]")
        p.set_defaults(func=func)
    p = sub.add_parser("strings")
    p.add_argument("source", help="the original app's Resources folder")
    p.add_argument("target", help="this book's Resources folder")
    p.add_argument("patterns", nargs="+", help="keys, or prefixes ending in *")
    p.set_defaults(func=strings)
    args = parser.parse_args(argv)
    args.func(args)


if __name__ == "__main__":
    main()
