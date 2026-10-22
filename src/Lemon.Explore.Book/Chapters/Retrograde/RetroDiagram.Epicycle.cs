using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Retrograde;

/// <summary>
/// Epicycle mode: the planet's real motion drawn with Earth held still in the middle (Ptolemy's picture: the planet traces
/// loops, a circle carried round on a circle) or with the Sun held still (Copernicus's: plain orbits), the frame sliding
/// smoothly from one to the other. With Earth in the middle the deferent and the epicycle are drawn: for an outer planet
/// the deferent is its own orbit and the epicycle is Earth's orbit seen from Earth, its arm always parallel to the line to
/// the Sun; for Mercury and Venus the deferent carries the Sun and the epicycle is the planet's orbit.
/// </summary>
public sealed partial class RetroDiagram
{
    /// <summary>The most points drawn along the trail.</summary>
    private const int TrailSamples = 1400;

    /// <summary>Round the epicycles: the middle of the picture and the scale, for dragging.</summary>
    private double _epicycleScale;

    private void DrawEpicycleMode(PaintContext dc)
    {
        var stage = Stage();
        var now = _clock.Elapsed.TotalSeconds;
        var center = new Point(stage.Left + (stage.Width / 2), stage.Top + (stage.Height / 2));
        var fit = (Math.Min(stage.Width, stage.Height) / 2) - 14;
        var a = RetroModel.SemiMajorAxis(Planet);
        var scale = fit / ((a + 1) * 1.1);
        _epicycleScale = scale;

        // 0: Earth held still, 1: the Sun; eased so the slide starts and ends gently.
        var f = _frame * _frame * (3 - (2 * _frame));
        Vec3 Origin(double day) => RetroModel.Earth(day) * (1 - f);
        Point P(Vec3 v) => center + new Vector(v.X * scale, -v.Y * scale);
        var color = PlanetColor(Planet);

        // A faint grid of whole AUs round the centre.
        for (var au = 1; au <= Math.Ceiling(a + 1); au++)
        {
            dc.DrawEllipse(null, Ink.Grid, center, au * scale, au * scale);
        }

        // The trail since it was started (never longer than the planet's pattern); a trail started later is cut there.
        var span = PatternDays(Planet);
        if (_day < TrailStart) Push(TrailStartProperty, Math.Round(_day, 3));
        var from = Math.Max(TrailStart, _day - span);
        var periods = RetroModel.Periods(Planet);
        DrawTrail(dc, from, Origin, P, periods);

        // A year of Earth's and the Sun's own paths in this frame.
        var yearFrom = Math.Max(from, _day - 365.25);
        if (_day - yearFrom > 2)
        {
            var days = Enumerable.Range(0, 121).Select(i => yearFrom + ((_day - yearFrom) * i / 120)).ToList();
            dc.DrawGeometry(null, FadedPen(Ink.Blue, 0.45, 1.2), Line(days.Select(d => P(RetroModel.Earth(d) - Origin(d)))));
            dc.DrawGeometry(null, FadedPen(Ink.Sun, 0.4, 1.2), Line(days.Select(d => P(Origin(d) * -1))));
        }

        var sun = P(Origin(_day) * -1);
        var earth = P(RetroModel.Earth(_day) - Origin(_day));
        var planet = P(RetroModel.Helio(Planet, _day) - Origin(_day));

        if (ShowGuides && f < 0.98) DrawPtolemy(dc, earth, sun, center, scale, 1 - f, now);

        // The loop nearest now, while Earth is (nearly) held still.
        if (f < 0.5 && periods.LastOrDefault(p => p.Middle >= from && p.Middle <= _day) is { } last)
        {
            var loop = P(RetroModel.Helio(Planet, last.Middle) - Origin(last.Middle));
            _targets.Add("loop", loop, 14);
            if (ShowLabels) DrawTargetPill(dc, Loc("Retro_Label_Loop"), loop + ((loop - center) / Math.Max(1, ((Vector)(loop - center)).Length) * 34), Ink.Orange, "loop", 10.5);
        }

        DrawSun(dc, sun, Math.Clamp(fit * 0.035, 6, 12));
        _targets.Add("sun", sun, Math.Clamp(fit * 0.035, 6, 12) + 4);
        var rEarth = Math.Clamp(fit * 0.03, 6, 10);
        var rPlanet = Math.Clamp(fit * BodyShare(Planet), 5, 14);
        DrawBody(dc, "Earth", earth, rEarth, sun);
        DrawBody(dc, RetroModel.Id(Planet), planet, rPlanet, sun);
        _targets.Add("earth", earth, rEarth + 3);
        _targets.Add(PlanetId(Planet), planet, rPlanet + 3);
        var retroNow = RetroModel.During(Planet, _day) is not null;
        if (ShowLabels)
        {
            DrawTargetPill(dc, Loc("Retro_Label_Earth"), earth + new Vector(0, rEarth + 16), Ink.Blue, "earth", 10.5);
            DrawTargetPill(dc, Loc("Retro_Label_Sun"), sun + new Vector(0, -24), Ink.Sun, "sun", 10.5);
            DrawTargetPill(dc, Loc($"Retro_Planet_{Planet}"), planet + new Vector(0, -rPlanet - 16), retroNow ? Ink.Orange : color, PlanetId(Planet), 11);
        }

        // Which body is held still.
        var frame = DrawTargetPill(dc, Loc(f < 0.5 ? "Retro_Frame_Earth" : "Retro_Frame_Sun"), new Point(stage.Left + 140, stage.Top + 16), f < 0.5 ? Ink.Blue : Ink.Sun, "frame", 12);
        DrawText(dc, LocFormat("Retro_Label_TrailYears", Number((_day - from) / 365.25, "0.0")), new Point(frame.Left + 4, frame.Bottom + 12), Ink.Dim, 10.5);
    }

    /// <summary>The planet's trail, older parts fainter, the stretches where it goes backwards in orange.</summary>
    private void DrawTrail(PaintContext dc, double from, Func<double, Vec3> origin, Func<Vec3, Point> p, IReadOnlyList<RetroPeriod> periods)
    {
        if (_day - from < 0.5) return;
        var count = (int)Math.Clamp((_day - from) / (PatternDays(Planet) / TrailSamples), 2, TrailSamples);
        var color = PlanetColor(Planet);

        // One geometry per pen: segments of the same colour and fading share a pen.
        var batches = new Dictionary<(bool Retro, int Fade), StreamGeometryContext>();
        var open = new HashSet<(bool Retro, int Fade)>();
        var geometries = new Dictionary<(bool Retro, int Fade), StreamGeometry>();
        var index = periods.Count > 0 ? Math.Max(0, LastBefore(periods, from)) : 0;
        Point? previous = null;
        (bool Retro, int Fade)? previousKey = null;
        for (var i = 0; i <= count; i++)
        {
            var day = from + ((_day - from) * i / count);
            while (index < periods.Count - 1 && periods[index].End < day) index++;
            var retro = periods.Count > 0 && periods[index].Contains(day);
            var fade = (int)Math.Round(4 * (0.25 + (0.75 * i / count)) * 2);
            var point = p(RetroModel.Helio(Planet, day) - origin(day));
            var key = (retro, fade);
            if (previous is { } last)
            {
                if (!batches.TryGetValue(key, out var context))
                {
                    var geometry = new StreamGeometry();
                    context = geometry.Open();
                    geometries[key] = geometry;
                    batches[key] = context;
                }

                // A run of the same pen is one polyline; a new run starts a new figure where the last one ended
                // (ending the figure that geometry had open).
                if (previousKey != key)
                {
                    if (!open.Add(key)) context.EndFigure(false);
                    context.BeginFigure(last, false);
                }

                context.LineTo(point);
                previousKey = key;
            }

            previous = point;
        }

        foreach (var (key, context) in batches)
        {
            if (open.Contains(key)) context.EndFigure(false);
            context.Dispose();
            var geometry = geometries[key];
            dc.DrawGeometry(null, FlatPen(key.Retro ? Ink.Orange : color, key.Fade / 8.0, key.Retro ? 2.4 : 1.8), geometry);
        }
    }

    /// <summary>The index of the last period starting before <paramref name="day"/>.</summary>
    private static int LastBefore(IReadOnlyList<RetroPeriod> periods, double day)
    {
        var (lo, hi) = (0, periods.Count - 1);
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (periods[mid].Start <= day) lo = mid;
            else hi = mid - 1;
        }

        return lo;
    }

    /// <summary>
    /// Ptolemy's machinery, faded by <paramref name="alpha"/> as the frame slides to the Sun: the deferent round Earth, the
    /// epicycle round the point carried along it, the arm to the planet, and (outer planets) the line to the Sun it follows.
    /// </summary>
    private void DrawPtolemy(PaintContext dc, Point earth, Point sun, Point center, double scale, double alpha, double now)
    {
        var inner = RetroModel.IsInner(Planet);
        var planetNow = RetroModel.Helio(Planet, _day);
        var earthNow = RetroModel.Earth(_day);

        // The point carried round the deferent, as seen from Earth, and the epicycle's arm from it to the planet.
        var carried = inner ? earthNow * -1 : planetNow;
        var arm = inner ? planetNow : earthNow * -1;
        var period = inner ? 365.25 : RetroModel.OrbitDays(Planet);
        var deferent = Enumerable.Range(0, 181).Select(i =>
        {
            var day = _day + (period * i / 180);
            var c = inner ? RetroModel.Earth(day) * -1 : RetroModel.Helio(Planet, day);
            return earth + new Vector(c.X * scale, -c.Y * scale);
        }).ToList();
        var deferentLine = Line(deferent, closed: true);
        dc.DrawGeometry(null, FadedPen(Ink.Violet, 0.6 * alpha, 1.4, dashed: true), deferentLine);
        if (Glows("deferent")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 8), deferentLine);
        for (var i = 10; i < deferent.Count; i += 30) _targets.Add("deferent", deferent[i], 6);

        var c0 = earth + new Vector(carried.X * scale, -carried.Y * scale);
        var radius = new Vector(arm.X, arm.Y).Length * scale;
        dc.DrawEllipse(null, FadedPen(Ink.Cyan, 0.7 * alpha, 1.4), c0, radius, radius);
        if (Glows("epicycle")) dc.DrawEllipse(null, ExploreTargets.GlowPen(now, 8), c0, radius, radius);
        var tip = c0 + new Vector(arm.X * scale, -arm.Y * scale);
        dc.DrawLine(FadedPen(Ink.Cyan, 0.9 * alpha, 1.6), c0, tip);
        dc.DrawLine(FadedPen(Ink.Violet, 0.6 * alpha, 1.2), earth, c0);
        dc.DrawEllipse(FadedBrush(Ink.Cyan, alpha), null, c0, 3.5, 3.5);
        for (var k = 0; k < 8; k++)
        {
            var angle = k * Math.PI / 4;
            _targets.Add("epicycle", c0 + new Vector(Math.Cos(angle) * radius, Math.Sin(angle) * radius), 6);
        }

        // An outer planet's arm always points the way the Sun lies from Earth.
        if (!inner) dc.DrawLine(FadedPen(Ink.Sun, 0.5 * alpha, 1.2, dashed: true), earth, sun);

        if (!ShowLabels || alpha < 0.3) return;
        Vector outward = deferent[45] - center;
        outward /= Math.Max(1, outward.Length);
        DrawTargetPill(dc, Loc("Retro_Label_Deferent"), deferent[45] + (outward * 22), Ink.Violet, "deferent", 10.5);
        DrawTargetPill(dc, Loc("Retro_Label_Epicycle"), c0 + new Vector(0, radius + 18), Ink.Cyan, "epicycle", 10.5);
    }

    private readonly Dictionary<(Color Color, int Alpha, double Thickness), IPen> _flatPens = [];

    /// <summary>A flat-capped pen for the trail's many short segments (round caps on thousands of segments are slow to draw).</summary>
    private IPen FlatPen(Color color, double alpha, double thickness)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20), Math.Round(thickness, 1));
        if (_flatPens.TryGetValue(key, out var pen)) return pen;
        pen = new ImmutablePen(
            new ImmutableSolidColorBrush(Color.FromArgb((byte)(key.Item2 * 12.75), color.R, color.G, color.B)),
            key.Item3, null, PenLineCap.Flat, PenLineJoin.Bevel);
        _flatPens[key] = pen;
        return pen;
    }
}
