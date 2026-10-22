using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Expansion;

/// <summary>
/// The history view: the universe's size through time drawn as a horn whose height is the scale factor, coloured by what
/// filled space (white-hot fog, the dark ages, the first stars, galaxies), with the moments of history pinned above it.
/// A ring marks the moment looked at; below it a round window shows a patch of space then, its galaxies closer together
/// the smaller the universe was. Time runs evenly or in powers of ten; the universe can be ours, one without dark energy,
/// or one with so much matter that it falls back.
/// </summary>
public sealed partial class ExpansionDiagram
{
    /// <summary>Seconds the cursor takes to cross the whole axis when playing.</summary>
    private const double HistorySeconds = 26;

    private IImage? _horn;
    private Rect _hornStage;
    private (UniverseKind Universe, TimeAxis Axis) _hornKey;

    /// <summary>The scale factor the horn's full height stands for: ours at the end of the axis (the same for every universe).</summary>
    private static readonly double ReferenceScale = Expansion.Universe.Ours.ScaleAt(CosmicHistory.End);

    /// <summary>The smallest scale factor on the log axis.</summary>
    private static readonly double MinLogScale = Math.Log10(Expansion.Universe.Ours.ScaleAt(CosmicHistory.LogStartSeconds / Expansion.Universe.SecondsPerGyr));

    private void OnHornChanged() => _horn = null;

    private Universe Cosmos => Expansion.Universe.Of(Universe);

    /// <summary>The latest time the cursor can reach: the end of the axis, or a crunch.</summary>
    private double HistoryEnd => Math.Min(CosmicHistory.End, Cosmos.CrunchTime ?? double.MaxValue);

    private void StepHistory(double dt)
    {
        var fraction = CosmicHistory.Fraction(Axis, CosmicTime);
        var endFraction = CosmicHistory.Fraction(Axis, HistoryEnd);
        if (fraction >= endFraction - 1e-6)
        {
            if (IsLessonActive) return;
            _hold += dt;
            if (_hold < 2.5) return;
            _hold = 0;
            SetCurrentValue(CosmicTimeProperty, CosmicHistory.Earliest(Axis));
            return;
        }

        var next = Math.Min(endFraction, fraction + (dt / HistorySeconds));
        SetCurrentValue(CosmicTimeProperty, Math.Min(HistoryEnd, CosmicHistory.Time(Axis, next)));
    }

    /// <summary>The horn's place: its axis from left to right, its centre line and its greatest half-height.</summary>
    private readonly record struct HornLayout(double Left, double Right, double Centre, double Radius, double AxisY, Rect Stage)
    {
        public double Width => Right - Left;
    }

    private HornLayout HistoryLayout(Rect stage)
    {
        var radius = Math.Clamp(stage.Height * 0.22, 46, 118);
        var centre = stage.Top + 86 + radius;
        return new HornLayout(stage.Left + 60, stage.Right - 40, centre, radius, centre + radius + 14, stage);
    }

    private double HistoryX(HornLayout layout, double time) => layout.Left + (CosmicHistory.Fraction(Axis, time) * layout.Width);

    private double HistoryTimeAt(HornLayout layout, double x) => CosmicHistory.Time(Axis, (x - layout.Left) / layout.Width);

    /// <summary>The horn's half-height at scale factor a.</summary>
    private double HornRadius(HornLayout layout, double a)
    {
        if (a <= 0) return 0;
        if (Axis == TimeAxis.Linear) return layout.Radius * Math.Min(1.15, a / ReferenceScale);
        var top = Math.Log10(ReferenceScale);
        return layout.Radius * Math.Clamp((Math.Log10(a) - MinLogScale) / (top - MinLogScale), 0, 1.1);
    }

    /// <summary>The round window into space at the moment looked at: its centre and radius.</summary>
    private (Point Centre, double Radius) PatchPlace(HornLayout layout, double cursorX)
    {
        var below = layout.Stage.Bottom - (layout.AxisY + 46);
        var radius = Math.Clamp((below / 2) - 2, 34, 76);
        var x = Math.Clamp(cursorX, layout.Left + radius + 8, layout.Right - radius - 8);
        return (new Point(x, layout.AxisY + 46 + radius), radius);
    }

    private void DrawHistoryMode(PaintContext dc, double now)
    {
        var stage = Stage();
        var layout = HistoryLayout(stage);
        var universe = Cosmos;
        var dpi = RenderScale();

        if (_horn is null || _hornStage != stage || _hornKey != (Universe, Axis)) RenderHorn(layout, universe, dpi);
        if (_horn is not null) dc.DrawImage(_horn, new Rect(0, 0, ActualWidth, ActualHeight));

        var time = Math.Min(CosmicTime, HistoryEnd);
        var x = HistoryX(layout, time);
        DrawHistoryAxis(dc, layout, x);
        DrawEvents(dc, layout, universe);
        DrawSizeArrow(dc, layout, universe);

        // Today: a green ring in every universe (they all have today's size and rate now).
        var nowX = HistoryX(layout, universe.Age);
        var nowR = HornRadius(layout, 1);
        dc.DrawEllipse(null, FadedPen(Ink.Green, 0.8, 1.6), new Point(nowX, layout.Centre), Math.Max(3, nowR * 0.17), nowR);

        // The moment looked at.
        var a = universe.ScaleAt(time);
        var r = Math.Max(4, HornRadius(layout, a));
        var pulse = 0.5 + (0.5 * Math.Sin(now * 3));
        dc.DrawEllipse(null, FadedPen(Ink.Gold, 0.25 + (0.25 * pulse), 6), new Point(x, layout.Centre), Math.Max(3, r * 0.18), r);
        dc.DrawEllipse(null, FadedPen(Ink.Gold, 1, 2), new Point(x, layout.Centre), Math.Max(3, r * 0.18), r);
        dc.DrawLine(FadedPen(Ink.Gold, 0.6, 1, dashed: true), new Point(x, layout.Centre + r), new Point(x, layout.AxisY + 22));
        _targets.Add("cursor", new Point(x, layout.Centre - r), 12);

        var timeText = ExpansionFormat.Time(time);
        DrawPill(dc, timeText, new Point(x, layout.AxisY + 31), Ink.Gold, 11.5);

        DrawPatchWindow(dc, layout, universe, time, a, x, now);
    }

    /// <summary>Renders the horn (its shape, colours, rings and sprinkled stars and galaxies) once into a bitmap.</summary>
    private void RenderHorn(HornLayout layout, Universe universe, double dpi)
    {
        _hornStage = layout.Stage;
        _hornKey = (Universe, Axis);
        var width = Math.Max(1, ActualWidth);
        var height = Math.Max(1, ActualHeight);
        _horn = SpaceDrawing.RenderToBitmap(width, height, dpi, dc =>
        {
            const int samples = 360;
            var end = HistoryEnd;
            var top = new List<Point>();
            var bottom = new List<Point>();
            var stops = new List<(Color Color, double Offset)>();
            var endX = HistoryX(layout, end);
            for (var i = 0; i <= samples; i++)
            {
                var x = layout.Left + (i * (endX - layout.Left) / samples);
                var t = HistoryTimeAt(layout, x);
                var a = universe.ScaleAt(t);
                var r = HornRadius(layout, a);
                top.Add(new Point(x, layout.Centre - r));
                bottom.Add(new Point(x, layout.Centre + r));
                if (i % 6 == 0) stops.Add((SpaceColour(universe, t, a), (x - layout.Left) / Math.Max(1, endX - layout.Left)));
            }

            var outline = new List<Point>(top);
            for (var i = bottom.Count - 1; i >= 0; i--) outline.Add(bottom[i]);
            var shape = Line(outline, closed: true, filled: true);
            var fill = SpaceDrawing.LinearGradient(0, [.. stops]);
            dc.DrawGeometry(fill, null, shape);

            // Rings at even steps of time, like slices through a tube.
            dc.PushClip(shape);
            var ringPen = MakePen(Solid(34, 255, 255, 255), 1);
            var ringTimes = Axis == TimeAxis.Linear
                ? Enumerable.Range(1, 15).Select(i => i * 2.0)
                : Enumerable.Range(0, 12).Select(i => Math.Pow(10, -4 + (2 * i)) / Expansion.Universe.SecondsPerGyr);
            foreach (var t in ringTimes)
            {
                if (t >= end) continue;
                var x = HistoryX(layout, t);
                var r = HornRadius(layout, universe.ScaleAt(t));
                if (r > 3) dc.DrawEllipse(null, ringPen, new Point(x, layout.Centre), r * 0.17, r);
            }

            SprinkleHorn(dc, layout, universe, endX);

            // Light from above, shade below: a tube, not a ribbon.
            var shade = new ImmutableLinearGradientBrush(
            [
                new ImmutableGradientStop(0, Color.FromArgb(40, 255, 255, 255)),
                new ImmutableGradientStop(0.4, Color.FromArgb(0, 255, 255, 255)),
                new ImmutableGradientStop(0.6, Color.FromArgb(0, 0, 0, 0)),
                new ImmutableGradientStop(1, Color.FromArgb(110, 0, 0, 0)),
            ],
                startPoint: new RelativePoint(0, layout.Centre - (layout.Radius * 1.15), RelativeUnit.Absolute),
                endPoint: new RelativePoint(0, layout.Centre + (layout.Radius * 1.15), RelativeUnit.Absolute));
            dc.DrawRectangle(shade, null, new Rect(0, layout.Centre - layout.Radius * 1.2, width, layout.Radius * 2.4));
            dc.Pop();

            var edge = MakePen(Solid(110, 0xC8, 0xD8, 0xFF), 1.2);
            dc.DrawGeometry(null, edge, Line(top));
            dc.DrawGeometry(null, edge, Line(bottom));

            // A crunch ends in a flash.
            if (universe.CrunchTime is { } crunch && crunch <= CosmicHistory.End)
            {
                var cx = HistoryX(layout, crunch);
                var flash = SpaceDrawing.RadialGradient(
            (Color.FromArgb(255, 255, 250, 230), 0),
            (Color.FromArgb(160, 255, 170, 80), 0.35),
            (Color.FromArgb(0, 255, 120, 40), 1));
                dc.DrawEllipse(flash, null, new Point(cx, layout.Centre), 22, 22);
            }

            // The very start: a point of light at the tip.
            var tip = SpaceDrawing.RadialGradient(
            (Color.FromArgb(255, 255, 255, 255), 0),
            (Color.FromArgb(150, 255, 240, 200), 0.3),
            (Color.FromArgb(0, 255, 200, 120), 1));
            dc.DrawEllipse(tip, null, new Point(layout.Left, layout.Centre), 14, 14);
        });
    }

    /// <summary>
    /// Specks inside the horn, evenly along it: sparks of hot gas, then nothing in the dark ages, then blue first stars and
    /// golden galaxies. Each keeps its place across the horn's height, so they spread out as the horn widens.
    /// </summary>
    private void SprinkleHorn(PaintContext dc, HornLayout layout, Universe universe, double endX)
    {
        var random = new Random(7);
        var hot = Solid(150, 255, 236, 190);
        var star = Solid(220, 190, 215, 255);
        var galaxyCore = Solid(230, 255, 232, 180);
        var galaxyGlow = Solid(60, 170, 200, 255);
        for (var i = 0; i < 1400; i++)
        {
            var x = layout.Left + (random.NextDouble() * (endX - layout.Left));
            var u = (random.NextDouble() * 2) - 1;
            var size = random.NextDouble();
            var t = HistoryTimeAt(layout, x);
            var a = universe.ScaleAt(t);
            var r = HornRadius(layout, a);
            if (r < 4) continue;
            var p = new Point(x, layout.Centre + (u * r * 0.92));
            switch (CosmicHistory.EraAt(universe, t))
            {
                case CosmicEra.Start or CosmicEra.Plasma or CosmicEra.Crunch:
                    if (size < 0.5) dc.DrawEllipse(hot, null, p, 0.8, 0.8);
                    break;

                case CosmicEra.Dawn:
                    if (size < 0.35) dc.DrawEllipse(star, null, p, 0.9, 0.9);
                    break;

                case CosmicEra.Galaxies or CosmicEra.Speeding or CosmicEra.Now or CosmicEra.FutureFast or CosmicEra.FutureSlowing or CosmicEra.Contracting:
                    if (size < 0.6)
                    {
                        dc.DrawEllipse(galaxyGlow, null, p, 2.4, 1.5);
                        dc.DrawEllipse(galaxyCore, null, p, 0.9, 0.7);
                    }

                    break;
            }
        }
    }

    /// <summary>The colour space glows with at a moment: white-hot to orange fog, the dark ages, then deep blue.</summary>
    private static Color SpaceColour(Universe universe, double time, double a)
    {
        var era = CosmicHistory.EraAt(universe, time);
        var cmb = 1 / (1 + Expansion.Universe.CmbRedshift);
        if (a <= 0 || era == CosmicEra.Ended) return Color.FromRgb(0xFF, 0xF8, 0xE8);
        if (a < cmb)
        {
            // Hotter is whiter: from 3000 K (orange) up.
            var heat = Math.Clamp(Math.Log10(cmb / a) / 6, 0, 1);
            return Lerp(Color.FromRgb(0xFF, 0x8C, 0x3A), Color.FromRgb(0xFF, 0xF6, 0xDC), heat);
        }

        var cool = Math.Log10(a / cmb);
        if (cool < 0.25) return Lerp(Color.FromRgb(0xFF, 0x8C, 0x3A), Color.FromRgb(0x4A, 0x1C, 0x12), cool / 0.25);
        if (a < 1 / 21.0) return Lerp(Color.FromRgb(0x4A, 0x1C, 0x12), Color.FromRgb(0x0C, 0x0A, 0x12), Math.Clamp((cool - 0.25) / 1.4, 0, 1));
        if (a < 1 / 6.0) return Lerp(Color.FromRgb(0x0C, 0x0A, 0x12), Color.FromRgb(0x14, 0x22, 0x4A), Math.Clamp((a - (1 / 21.0)) / ((1 / 6.0) - (1 / 21.0)), 0, 1));
        if (era == CosmicEra.FutureFast) return Lerp(Color.FromRgb(0x12, 0x20, 0x46), Color.FromRgb(0x06, 0x09, 0x16), Math.Clamp((time - universe.Age) / 14, 0, 1));
        return Color.FromRgb(0x14, 0x22, 0x4A);
    }

    /// <summary>The time axis under the horn, with its labels and the Big Bang at its start.</summary>
    private void DrawHistoryAxis(PaintContext dc, HornLayout layout, double cursorX)
    {
        var y = layout.AxisY;
        dc.DrawLine(Ink.Faint, new Point(layout.Left, y), new Point(layout.Right, y));
        var caption = DrawText(dc, Loc(Axis == TimeAxis.Linear ? "Expand_Axis_Linear" : "Expand_Axis_Log"), new Point(layout.Right + 30, y + 12), Ink.Dim, 10, TextAnchor.Right);
        AddBoxTarget("axis", caption);

        var bang = DrawText(dc, Loc("Expand_Axis_BigBang"), new Point(layout.Left - 6, y + 12), Ink.Text, 10.5, TextAnchor.Center);
        AddBoxTarget("bigbang", bang);
        _targets.Add("bigbang", new Point(layout.Left, layout.Centre), 10);

        IEnumerable<(double Time, string Label)> ticks = Axis == TimeAxis.Linear
            ? new[] { 5.0, 10, 15, 20, 25, 30 }.Select(t => (t, ExpansionFormat.Time(t)))
            :
            [
                (1e-4 / Expansion.Universe.SecondsPerGyr, ExpansionFormat.Time(1e-4 / Expansion.Universe.SecondsPerGyr)),
                (1 / Expansion.Universe.SecondsPerGyr, Loc("Expand_Tick_Second")),
                (60 / Expansion.Universe.SecondsPerGyr, Loc("Expand_Tick_Minute")),
                (86_400 / Expansion.Universe.SecondsPerGyr, Loc("Expand_Tick_Day")),
                (1e-9, Loc("Expand_Tick_Year")),
                (1e-6, Loc("Expand_Tick_Thousand")),
                (1e-3, Loc("Expand_Tick_Million")),
                (1, Loc("Expand_Tick_Billion")),
            ];
        foreach (var (t, label) in ticks)
        {
            var x = HistoryX(layout, t);
            dc.DrawLine(Ink.Faint, new Point(x, y - 3), new Point(x, y + 3));
            if (!ShowLabels) continue;
            var size = _labels.Text(label, 9.5, Ink.Dim);
            var box = new Rect(x - (size.Width / 2) - 4, y + 4, size.Width + 8, 16);
            if (box.Intersects(caption) || box.Intersects(bang) || (cursorX > box.Left && cursorX < box.Right)) continue;
            DrawText(dc, label, new Point(x, y + 12), Ink.Dim, 9.5, TextAnchor.Center);
        }
    }

    /// <summary>Which moments are pinned on this axis: the early ones only where they are not squeezed into the tip.</summary>
    private bool ShownOnAxis(string id) => Axis == TimeAxis.Linear
        ? id is not ("Protons" or "Nuclei")
        : id is "Protons" or "Nuclei" or "Cmb" or "FirstStars" or "Now";

    /// <summary>Pins the moments of history above the horn, in up to three rows, each joined to its place by a thin line.</summary>
    private void DrawEvents(PaintContext dc, HornLayout layout, Universe universe)
    {
        var events = new List<(string Id, double Time, string Text, Color Color)>();
        foreach (var e in CosmicEvent.All)
        {
            if (!ShownOnAxis(e.Id) || e.TimeIn(universe) is not { } t || t > HistoryEnd) continue;
            events.Add((e.Id, t, Loc(EventTitleKey(e.Id)), e.Id == "Now" ? Ink.Green : e.Id is "Speedup" ? Ink.Pink : Ink.Text));
        }

        if (universe.TurnaroundTime is { } turn && turn < HistoryEnd) events.Add(("Turnaround", turn, Loc("Expand_Obj_Turnaround"), Ink.Orange));
        if (universe.CrunchTime is { } crunch && crunch <= CosmicHistory.End) events.Add(("Crunch", crunch, Loc("Expand_Obj_Crunch"), Ink.Red));
        events.Sort((p, q) => p.Time.CompareTo(q.Time));

        const int rows = 3;
        var rowRight = new double[rows];
        Array.Fill(rowRight, double.NegativeInfinity);
        var rowTop = layout.Stage.Top + 6;
        foreach (var (id, time, text, color) in events)
        {
            var x = HistoryX(layout, time);
            var size = PillSize(text, 11);
            var centre = Math.Max(x, layout.Stage.Left + (size.Width / 2) + 4);
            var row = -1;
            for (var i = 0; i < rows; i++)
            {
                if (centre - (size.Width / 2) > rowRight[i] + 6)
                {
                    row = i;
                    break;
                }
            }

            if (row < 0)
            {
                row = Array.IndexOf(rowRight, rowRight.Min());
                centre = rowRight[row] + 6 + (size.Width / 2);
            }

            centre = Math.Min(centre, layout.Stage.Right - (size.Width / 2) - 4);
            rowRight[row] = centre + (size.Width / 2);
            var pillY = rowTop + (row * 23) + 9;
            var r = HornRadius(layout, universe.ScaleAt(time));
            var glow = Glows(id.ToLowerInvariant());
            dc.DrawLine(FadedPen(color, glow ? 0.9 : 0.4, glow ? 1.6 : 1), new Point(centre, pillY + 9), new Point(x, layout.Centre - r - 3));
            dc.DrawEllipse(FadedBrush(color, 0.9), null, new Point(x, layout.Centre - r - 3), 2.2, 2.2);
            if (!ShowLabels && id != "Now") continue;
            var pill = DrawPill(dc, text, new Point(centre, pillY), color, 11);
            AddBoxTarget(id.ToLowerInvariant(), pill);
        }
    }

    /// <summary>A double arrow across the horn near its right end: its height is the universe's size.</summary>
    private void DrawSizeArrow(PaintContext dc, HornLayout layout, Universe universe)
    {
        var t = Math.Min(HistoryEnd, CosmicHistory.Time(Axis, 0.93)) - 0.01;
        var x = HistoryX(layout, t) + 18;
        var r = HornRadius(layout, universe.ScaleAt(t));
        if (r < 14) return;
        var glow = Glows("size");
        var color = Color.FromRgb(0xC8, 0xD8, 0xFF);
        DrawArrow(dc, new Point(x, layout.Centre), new Point(x, layout.Centre - r + 2), color, glow ? 2.4 : 1.4, 0.85);
        DrawArrow(dc, new Point(x, layout.Centre), new Point(x, layout.Centre + r - 2), color, glow ? 2.4 : 1.4, 0.85);
        if (!ShowLabels) return;
        var pill = DrawPill(dc, Loc("Expand_Label_Size"), new Point(x + 4 + (PillSize(Loc("Expand_Label_Size"), 10.5).Width / 2), layout.Centre), color, 10.5);
        AddBoxTarget("size", pill);
    }

    /// <summary>The round window into space at the moment looked at, joined to the ring on the horn.</summary>
    private void DrawPatchWindow(PaintContext dc, HornLayout layout, Universe universe, double time, double a, double cursorX, double now)
    {
        var (centre, radius) = PatchPlace(layout, cursorX);
        var join = FadedPen(Ink.Gold, 0.35, 1);
        dc.DrawLine(join, new Point(cursorX, layout.AxisY + 40), new Point(centre.X - (radius * 0.7), centre.Y - (radius * 0.7)));
        dc.DrawLine(join, new Point(cursorX, layout.AxisY + 40), new Point(centre.X + (radius * 0.7), centre.Y - (radius * 0.7)));

        dc.PushClip(SpaceDrawing.Ellipse(centre, radius, radius));
        DrawPatch(dc, universe, time, a, centre, radius, now);
        dc.Pop();
        dc.DrawEllipse(null, FadedPen(Colors.White, 0.55, 1.4), centre, radius, radius);
        dc.DrawEllipse(null, FadedPen(Ink.Gold, 0.5, 1), centre, radius + 3, radius + 3);
        _targets.Add("patch", centre, radius);

        if (!ShowLabels) return;
        var era = CosmicHistory.EraAt(universe, time);
        var text = Loc(EraTitleKey(era));
        var size = PillSize(text, 11.5);
        var right = centre.X + radius + 12 + size.Width < layout.Stage.Right - 4;
        var pillCentre = right
            ? new Point(centre.X + radius + 10 + (size.Width / 2), centre.Y - 12)
            : new Point(centre.X - radius - 10 - (size.Width / 2), centre.Y - 12);
        DrawPill(dc, text, pillCentre, Ink.Gold, 11.5);
        if (a > 0)
        {
            var detail = LocFormat("Expand_Patch_Detail", ExpansionFormat.Size(a), ExpansionFormat.Kelvin(Expansion.Universe.Temperature(a)));
            var detailSize = PillSize(detail, 10.5);
            var detailCentre = right
                ? new Point(centre.X + radius + 10 + (detailSize.Width / 2), centre.Y + 12)
                : new Point(centre.X - radius - 10 - (detailSize.Width / 2), centre.Y + 12);
            DrawPill(dc, detail, detailCentre, Ink.Dim, 10.5);
        }
    }

    /// <summary>
    /// A patch of space at scale factor a: hot fog with light bouncing about; dark gas; the first stars switching on;
    /// galaxies on a lattice whose spacing grows with a (they are drawn far bigger than they are).
    /// </summary>
    private void DrawPatch(PaintContext dc, Universe universe, double time, double a, Point centre, double radius, double now)
    {
        var era = CosmicHistory.EraAt(universe, time);
        var background = SpaceColour(universe, time, a);
        var fill = SpaceDrawing.RadialGradient((Lerp(background, Colors.White, 0.08), 0), (Lerp(background, Colors.Black, 0.35), 1));
        dc.DrawEllipse(fill, null, centre, radius, radius);
        var dpi = RenderScale();

        switch (era)
        {
            case CosmicEra.Start or CosmicEra.Plasma or CosmicEra.Crunch or CosmicEra.Ended:
            {
                // Nuclei and electrons jostling; light (short wiggles) bounces off them and gets nowhere.
                var particle = FadedBrush(Color.FromRgb(0xFF, 0x6A, 0x3A), 0.85);
                var electron = FadedBrush(Color.FromRgb(0x9A, 0xC8, 0xFF), 0.85);
                var photon = FadedPen(Colors.White, 0.7, 1.1);
                for (var i = 0; i < 46; i++)
                {
                    var p = Wander(i, now, centre, radius);
                    dc.DrawEllipse(i % 2 == 0 ? particle : electron, null, p, i % 2 == 0 ? 2.2 : 1.2, i % 2 == 0 ? 2.2 : 1.2);
                }

                for (var i = 0; i < 16; i++)
                {
                    // Each photon flies straight for a moment, then is knocked into a new direction.
                    var hop = Math.Floor((now * 3) + (i * 0.37));
                    var seed = (int)(hop * 31) + (i * 977);
                    var random = new Random(seed);
                    var start = centre + new Vector(((random.NextDouble() * 2) - 1) * radius * 0.8, ((random.NextDouble() * 2) - 1) * radius * 0.8);
                    var angle = random.NextDouble() * 2 * Math.PI;
                    var dir = new Vector(Math.Cos(angle), Math.Sin(angle));
                    var along = ((now * 3) + (i * 0.37) - hop) * 14;
                    var p0 = start + (dir * along);
                    var normal = new Vector(-dir.Y, dir.X);
                    var points = Enumerable.Range(0, 9).Select(j => p0 + (dir * j * 1.4) + (normal * 1.6 * Math.Sin(j * 1.5))).ToList();
                    dc.DrawGeometry(null, photon, Line(points));
                }

                break;
            }

            case CosmicEra.DarkAges:
            {
                // Gas gathering into faint clumps, darker and lumpier as time goes on.
                var lumpy = Math.Clamp(Math.Log10(a * (1 + Expansion.Universe.CmbRedshift)) / 1.7, 0, 1);
                var random = new Random(3);
                for (var i = 0; i < 14; i++)
                {
                    var p = centre + new Vector(((random.NextDouble() * 2) - 1) * radius, ((random.NextDouble() * 2) - 1) * radius);
                    var r = radius * (0.12 + (0.12 * random.NextDouble()));
                    var blob = SpaceDrawing.RadialGradient((Color.FromArgb((byte)(20 + (50 * lumpy)), 150, 130, 170), 0), (Color.FromArgb(0, 150, 130, 170), 1));
                    dc.DrawEllipse(blob, null, p, r, r);
                }

                break;
            }

            default:
            {
                // Galaxies (or, at dawn, first stars) on a lattice that grows with the universe.
                var spacing = Math.Max(7, radius * 0.42 * a);
                var sprite = Math.Clamp(spacing * 0.32, 2.2, 7);
                var cells = (int)Math.Ceiling(radius / spacing) + 1;
                var dawn = era == CosmicEra.Dawn;
                var starBrush = FadedBrush(Color.FromRgb(0xC8, 0xDC, 0xFF), 0.95);
                var halo = FadedBrush(Color.FromRgb(0x9C, 0xC4, 0xFF), 0.25);
                for (var j = -cells; j <= cells; j++)
                {
                    for (var i = -cells; i <= cells; i++)
                    {
                        var g = GalaxyField.At(new GalaxyCell(i + 7, j + 3));
                        var offset = (g.Position / GalaxyField.Spacing) - new Vector(7, 3);
                        var p = centre + (offset * spacing);
                        if (((Vector)(p - centre)).Length > radius + sprite) continue;
                        if (dawn)
                        {
                            // Stars switch on one by one through the dawn.
                            var on = (a - (1 / 21.0)) / ((1 / 6.0) - (1 / 21.0));
                            if (((i * 7) + (j * 13) + 100) % 10 / 10.0 > on * 1.2) continue;
                            dc.DrawEllipse(halo, null, p, 3.2, 3.2);
                            dc.DrawEllipse(starBrush, null, p, 1.2, 1.2);
                        }
                        else
                        {
                            _sprites.Draw(dc, g.Look, p, sprite * g.Size, dpi);
                        }
                    }
                }

                break;
            }
        }
    }

    /// <summary>A particle's place in the hot fog: drifting round its own spot.</summary>
    private static Point Wander(int i, double now, Point centre, double radius)
    {
        var bx = Math.Sin((i * 12.9898) + 1.3) * 43758.5453;
        var by = Math.Sin((i * 78.233) + 4.1) * 12345.6789;
        var fx = bx - Math.Floor(bx);
        var fy = by - Math.Floor(by);
        var home = centre + new Vector(((fx * 2) - 1) * radius, ((fy * 2) - 1) * radius);
        return home + new Vector(Math.Sin((now * (2.1 + (i % 5))) + i) * 4, Math.Cos((now * (1.7 + (i % 3))) + (i * 2)) * 4);
    }

    /// <summary>The resource key of a moment's short title.</summary>
    internal static string EventTitleKey(string id) => id switch
    {
        "Protons" => "Expand_Obj_Protons",
        "Nuclei" => "Expand_Obj_Nuclei",
        "Cmb" => "Expand_Obj_Cmb",
        "FirstStars" => "Expand_Obj_FirstStars",
        "Jades" => "Expand_Obj_Jades",
        "Noon" => "Expand_Obj_Noon",
        "Speedup" => "Expand_Obj_Speedup",
        "Sun" => "Expand_Obj_Sun",
        "Now" => "Expand_Obj_Now",
        "Andromeda" => "Expand_Obj_Andromeda",
        "Turnaround" => "Expand_Obj_Turnaround",
        _ => "Expand_Obj_Crunch",
    };

    /// <summary>The resource key of an era's name.</summary>
    internal static string EraTitleKey(CosmicEra era) => era switch
    {
        CosmicEra.Start => "Expand_Era_Start",
        CosmicEra.Plasma => "Expand_Era_Plasma",
        CosmicEra.DarkAges => "Expand_Era_DarkAges",
        CosmicEra.Dawn => "Expand_Era_Dawn",
        CosmicEra.Galaxies => "Expand_Era_Galaxies",
        CosmicEra.Speeding => "Expand_Era_Speeding",
        CosmicEra.Now => "Expand_Era_Now",
        CosmicEra.FutureFast => "Expand_Era_FutureFast",
        CosmicEra.FutureSlowing => "Expand_Era_FutureSlowing",
        CosmicEra.Contracting => "Expand_Era_Contracting",
        CosmicEra.Crunch => "Expand_Era_Crunch",
        _ => "Expand_Era_Ended",
    };
}
