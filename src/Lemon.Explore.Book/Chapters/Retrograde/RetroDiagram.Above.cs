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
/// Above mode: the Sun with Earth's and the planet's orbits to scale, seen from the north of the ecliptic (everything
/// goes round anticlockwise), and the band of zodiac stars round the edge. The line of sight from Earth through the planet
/// runs out to the stars; at seven equal steps through the loop the earlier lines stay, numbered where they meet the stars,
/// so the numbers go forward, back and forward again as the inner, faster planet overtakes the outer one.
/// </summary>
public sealed partial class RetroDiagram
{
    /// <summary>How many lines of sight are kept through the loop.</summary>
    internal const int SightMarks = 7;

    /// <summary>Earth's and the planet's orbits, worked out once per planet.</summary>
    private (RetroPlanet Planet, IReadOnlyList<Vec3> Earth, IReadOnlyList<Vec3> Body)? _orbits;

    /// <summary>The Sun on screen and the scale (pixels per AU), for dragging Earth round its orbit.</summary>
    private Point _aboveCenter;

    private double _aboveScale;

    /// <summary>
    /// The days of the kept lines of sight: the first day shown, halfway to the first station, the station, the middle of the
    /// loop, the second station, halfway on, and the last day shown.
    /// </summary>
    internal static IReadOnlyList<double> SightDays(RetroPeriod period, double from, double to) =>
        [from, (from + period.Start) / 2, period.Start, period.Middle, period.End, (period.End + to) / 2, to];

    /// <summary>The colour of a kept line of sight: gold going east, cyan at a station, orange going back.</summary>
    private static Color MarkInk(int index) => index switch
    {
        2 or 4 => Ink.Cyan,
        3 => Ink.Orange,
        _ => Ink.Gold,
    };

    private void DrawAboveMode(PaintContext dc)
    {
        var stage = Stage();
        var now = _clock.Elapsed.TotalSeconds;
        var center = new Point(stage.Left + (stage.Width / 2), stage.Top + (stage.Height / 2));
        var fit = (Math.Min(stage.Width, stage.Height) / 2) - 18;

        if (_orbits is not { } orbits || orbits.Planet != Planet)
        {
            orbits = (Planet, RetroModel.OrbitPoints(null, 0, 240), RetroModel.OrbitPoints(Planet, 0, 240));
            _orbits = orbits;
        }

        var reach = Math.Max(orbits.Body.Max(v => v.Length), orbits.Earth.Max(v => v.Length));
        var ring = reach * 1.3;
        var scale = fit / (ring * 1.1);
        _aboveCenter = center;
        _aboveScale = scale;
        Point P(Vec3 v) => center + new Vector(v.X * scale, -v.Y * scale);
        var ringR = ring * scale;
        var color = PlanetColor(Planet);

        DrawStarBand(dc, center, ringR, now);

        // The two orbits.
        var earthOrbit = Line(orbits.Earth.Select(P), closed: true);
        var planetOrbit = Line(orbits.Body.Select(P), closed: true);
        dc.DrawGeometry(null, FadedPen(Ink.Blue, 0.5, 1.3), earthOrbit);
        dc.DrawGeometry(null, FadedPen(color, 0.5, 1.3), planetOrbit);
        if (Glows("earthorbit")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 8), earthOrbit);
        if (Glows("planetorbit")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 8), planetOrbit);
        for (var i = 0; i < orbits.Earth.Count; i += 30) _targets.Add("earthorbit", P(orbits.Earth[i]), 6);
        for (var i = 15; i < orbits.Body.Count; i += 30) _targets.Add("planetorbit", P(orbits.Body[i]), 6);

        DrawSun(dc, center, Math.Clamp(fit * 0.035, 6, 13));
        _targets.Add("sun", center, Math.Clamp(fit * 0.035, 6, 13) + 4);

        // The lines of sight kept through the loop: from Earth then, through the planet then, on towards the stars.
        var period = RetroModel.Apparition(Planet, (WindowStart + WindowEnd) / 2);
        var marks = SightDays(period, WindowStart, WindowEnd);
        var shown = new List<(int Index, double Day)>();
        for (var i = 0; i < marks.Count && marks[i] <= _day + 1e-6; i++)
        {
            shown.Add((i, marks[i]));
            if (!ShowGuides) continue;
            var e = P(RetroModel.Earth(marks[i]));
            var p = P(RetroModel.Helio(Planet, marks[i]));
            var ink = MarkInk(i);
            dc.DrawLine(FadedPen(ink, 0.45, 1), e, p);
            DrawSightTail(dc, e, p, ink, 0.45);
            dc.DrawEllipse(FadedBrush(Ink.Blue, 0.8), null, e, 3, 3);
            dc.DrawEllipse(FadedBrush(color, 0.8), null, p, 3, 3);
            if (ShowLabels)
            {
                AddBoxTarget("marks", DrawText(dc, Number(i + 1, "0"), e + new Vector(0, 11), Ink.Blue, 10.5, center: true, bold: true));
                AddBoxTarget("marks", DrawText(dc, Number(i + 1, "0"), p + new Vector(0, 11), ink, 10.5, center: true, bold: true));
            }
        }

        // Now: Earth, the planet, and the line of sight between them, on towards the stars.
        var earth = P(RetroModel.Earth(_day));
        var planet = P(RetroModel.Helio(Planet, _day));
        var retroNow = period.Contains(_day);
        var sightInk = retroNow ? Ink.Orange : Ink.Gold;
        dc.DrawLine(FadedPen(Ink.Text, 0.9, 1.6), earth, planet);
        var tail = DrawSightTail(dc, earth, planet, sightInk, 0.95);
        if (Glows("sightline")) dc.DrawLine(ExploreTargets.GlowPen(now, 8), earth, tail);
        foreach (var k in new[] { 0.35, 0.65, 1.0 }) _targets.Add("sightline", Mix(planet, tail, k), 7);

        var rEarth = Math.Clamp(fit * 0.03, 6, 11);
        var rPlanet = Math.Clamp(fit * BodyShare(Planet), 5, 15);
        DrawSpeedArrow(dc, null, earth, Ink.Blue);
        DrawSpeedArrow(dc, Planet, planet, color);
        DrawBody(dc, "Earth", earth, rEarth, center);
        DrawBody(dc, RetroModel.Id(Planet), planet, rPlanet, center);
        _targets.Add("earth", earth, rEarth + 3);
        _targets.Add(PlanetId(Planet), planet, rPlanet + 3);

        // Names on the side away from the Sun.
        Vector Away(Point at)
        {
            Vector v = at - center;
            return v.Length < 1 ? new Vector(0, -1) : v / v.Length;
        }

        DrawTargetPill(dc, Loc("Retro_Label_Earth"), earth + (Away(earth) * (rEarth + 16)) + new Vector(0, rEarth + 4), Ink.Blue, "earth", 11);
        DrawTargetPill(dc, Loc($"Retro_Planet_{Planet}"), planet + (Away(planet) * (rPlanet + 16)) - new Vector(0, rPlanet + 4), retroNow ? Ink.Orange : color, PlanetId(Planet), 11);

        DrawFan(dc, stage, shown, now);
    }

    /// <summary>A line of sight carried on past the planet towards the stars, with an arrowhead; where it ends.</summary>
    private Point DrawSightTail(PaintContext dc, Point earth, Point planet, Color ink, double alpha)
    {
        Vector v = planet - earth;
        if (v.Length < 1) return planet;
        var length = Math.Clamp(v.Length * 0.6, 40, 140);
        var direction = v / v.Length;
        var tip = planet + (direction * length);
        dc.DrawLine(FadedPen(ink, alpha, 1.4, dashed: true), planet, tip - (direction * 5));
        DrawArrowHead(dc, FadedBrush(ink, alpha), tip, direction, 8);
        return tip;
    }

    /// <summary>
    /// The lines of sight all drawn from one point, in the corner: the stars are so far away that only a line's direction
    /// counts, and here the direction turns forward, back and forward again. The angles between the lines are spread out
    /// (by the factor shown) so the small turn shows; the line now is drawn bold in the planet's colour.
    /// </summary>
    private void DrawFan(PaintContext dc, Rect stage, IReadOnlyList<(int Index, double Day)> shown, double now)
    {
        var r = Math.Clamp(stage.Height * 0.17, 56, 96);
        var c = new Point(stage.Right - r - 26, stage.Top + r + 40);
        dc.DrawEllipse(FadedBrush(Color.FromRgb(0x06, 0x09, 0x12), 0.88), Ink.Frame, c, r + 12, r + 12);

        // Spread the directions of the whole loop over about 120° round the middle one.
        var marks = SightDays(RetroModel.Apparition(Planet, (WindowStart + WindowEnd) / 2), WindowStart, WindowEnd);
        var middle = RetroModel.Geo(Planet, marks[3]).Longitude;
        var offsets = marks.Select(d => RetroModel.Wrap180(RetroModel.Geo(Planet, d).Longitude - middle)).ToList();
        var spread = Math.Max(4, offsets.Max() - offsets.Min());
        var factor = Math.Clamp(Math.Floor(120 / spread), 1, 12);
        var mid = (offsets.Max() + offsets.Min()) / 2;
        Point At(double lon, double radius)
        {
            var angle = (middle + mid + ((RetroModel.Wrap180(lon - middle) - mid) * factor)) * Math.PI / 180;
            return c + new Vector(Math.Cos(angle) * radius, -Math.Sin(angle) * radius);
        }

        // Bright zodiac stars inside the spread, for bearings.
        foreach (var star in ZodiacSky.Stars.Where(s => s.Magnitude < 4))
        {
            var off = RetroModel.Wrap180(star.Longitude - middle) - mid;
            if (Math.Abs(off * factor) > 170) continue;
            dc.DrawEllipse(FadedBrush(StarColor(star.ColorIndex), 0.75), null, At(star.Longitude, r + 5), 1.3, 1.3);
        }

        // Each line a little longer than the one before, their ends joined: the turn back shows as a zigzag.
        var tips = new List<Point>();
        foreach (var (index, day) in shown)
        {
            var lon = RetroModel.Geo(Planet, day).Longitude;
            var ink = MarkInk(index);
            var length = r * (0.48 + (0.075 * index));
            var tip = At(lon, length);
            tips.Add(tip);
            dc.DrawLine(FadedPen(ink, 0.75, 1.4), c, tip);
            dc.DrawEllipse(FadedBrush(ink, 1), null, tip, 2.5, 2.5);
            if (ShowLabels) DrawText(dc, Number(index + 1, "0"), At(lon, length + 11), ink, 11, center: true, bold: true);
        }

        if (tips.Count > 1) dc.DrawGeometry(null, FadedPen(Ink.Text, 0.5, 1.2, dashed: true), Line(tips));

        var nowLon = RetroModel.Geo(Planet, _day).Longitude;
        dc.DrawLine(FadedPen(PlanetColor(Planet), 1, 2.4), c, At(nowLon, r));
        dc.DrawEllipse(FadedBrush(PlanetColor(Planet), 1), null, At(nowLon, r), 4, 4);
        dc.DrawEllipse(FadedBrush(Color.FromRgb(0x3A, 0x7A, 0xD8), 1), null, c, 5, 5);

        // East: the way the directions turn as the planet drifts forward.
        var eastFrom = At(middle + mid + (spread * 0.62), r + 5);
        var eastTo = At(middle + mid + (spread * 0.62) + (8 / factor), r + 5);
        DrawArrowHead(dc, FadedBrush(Ink.Gold, 0.85), eastTo, (eastTo - eastFrom) / Math.Max(0.1, ((Vector)(eastTo - eastFrom)).Length), 8);

        var caption = DrawPill(dc, LocFormat("Retro_Label_Fan", Number(factor, "0")), c + new Vector(0, -r - 24), Ink.Text, 10.5);
        _targets.Add("fan", c, r);
        AddBoxTarget("fan", caption);
        if (Glows("fan")) dc.DrawEllipse(null, ExploreTargets.GlowPen(now, 6), c, r + 12, r + 12);
    }

    /// <summary>The planet's ball as a share of the picture's half-size (Jupiter and Saturn drawn bigger).</summary>
    private static double BodyShare(RetroPlanet planet) => planet switch
    {
        RetroPlanet.Jupiter or RetroPlanet.Saturn => 0.045,
        RetroPlanet.Venus => 0.03,
        _ => 0.026,
    };

    /// <summary>The band of zodiac stars round the edge, with the constellations' names and the direction of east.</summary>
    private void DrawStarBand(PaintContext dc, Point center, double radius, double now)
    {
        Point At(double lon, double r) => center + new Vector(Math.Cos(lon * Math.PI / 180) * r, -Math.Sin(lon * Math.PI / 180) * r);
        dc.DrawEllipse(null, FadedPen(Ink.Figure, 0.08, 22), center, radius, radius);
        foreach (var star in ZodiacSky.Stars)
        {
            var at = At(star.Longitude, radius + (star.Latitude * 0.45));
            var r = Math.Clamp(2.4 - (0.45 * star.Magnitude), 0.6, 3);
            dc.DrawEllipse(FadedBrush(StarColor(star.ColorIndex), Math.Clamp(1.1 - (0.16 * star.Magnitude), 0.3, 0.95)), null, at, r, r);
        }

        if (ShowLabels)
        {
            foreach (var (id, _) in ZodiacSky.Constellations)
            {
                var box = DrawText(dc, Loc($"Retro_Zodiac_{id}"), At(ZodiacSky.MiddleOf(id), radius + 30), Ink.Figure, 10, center: true);
                AddBoxTarget("zodiac", box);
            }
        }

        // East is the way the planets usually drift among the stars: anticlockwise seen from here.
        var arrowAt = At(118, radius - 22);
        DrawArrowHead(dc, FadedBrush(Ink.Gold, 0.8), arrowAt, new Vector(-Math.Sin(118 * Math.PI / 180), -Math.Cos(118 * Math.PI / 180)), 10);
        var arc = Enumerable.Range(0, 13).Select(i => At(88 + (i * 2.5), radius - 22));
        dc.DrawGeometry(null, FadedPen(Ink.Gold, 0.7, 1.6), Line(arc));
        DrawTargetPill(dc, Loc("Retro_Label_East"), At(103, radius - 44), Ink.Gold, "east", 10.5);
        DrawTargetPill(dc, Loc("Retro_Label_Stars"), At(214, radius + 6), Ink.Figure, "stars", 10.5);
        if (Glows("stars")) dc.DrawEllipse(null, ExploreTargets.GlowPen(now, 10), center, radius, radius);
    }

    /// <summary>The body's velocity as an arrow, its length by its speed, with the speed in km/s.</summary>
    private void DrawSpeedArrow(PaintContext dc, RetroPlanet? planet, Point at, Color ink)
    {
        if (!ShowLabels) return;
        Vec3 Where(double day) => planet is { } p ? RetroModel.Helio(p, day) : RetroModel.Earth(day);
        var a = Where(_day - 0.5);
        var b = Where(_day + 0.5);
        var v = new Vector(b.X - a.X, -(b.Y - a.Y));
        if (v.Length < 1e-9) return;
        v /= v.Length;
        var speed = RetroModel.SpeedKmS(planet, _day);
        var length = Math.Clamp(speed * 2.2, 18, 90);
        var tip = at + (v * length);
        DrawArrow(dc, at, tip, FadedBrush(ink, 0.9), 2, 9);
        var box = DrawText(dc, LocFormat("Retro_Label_Speed", Number(speed, "0.0")), tip + (v * 8) + new Vector(v.Y * 14, -v.X * 14), ink, 10.5, center: true);
        AddBoxTarget("speed", box);
    }
}
