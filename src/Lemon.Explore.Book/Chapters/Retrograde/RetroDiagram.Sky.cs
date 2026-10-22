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
/// Sky mode: a strip of the zodiac along the ecliptic, longitude across and latitude up at the same scale, with the real
/// stars and the planet's path through one loop: gold while it drifts east, orange while it goes back west, the two
/// stations where it stands still, and the opposition (or inferior conjunction) in the middle. East is on the left for a
/// viewer in the northern hemisphere facing the planet, on the right in the southern.
/// </summary>
public sealed partial class RetroDiagram
{
    /// <summary>The planet's path through the loop being shown, worked out once per planet and window.</summary>
    private sealed record SkyPath(RetroPlanet Planet, double From, double To, RetroPeriod Period, IReadOnlyList<(double Day, GeoPosition Geo)> Points);

    private SkyPath? _skyPath;

    /// <summary>The sky strip's frame and scale, kept for dragging (days per pixel).</summary>
    private Rect _skyArea;

    private SkyPath PathFor()
    {
        var (from, to) = (WindowStart, WindowEnd);
        if (_skyPath is { } cached && cached.Planet == Planet && cached.From == from && cached.To == to) return cached;
        var period = RetroModel.Apparition(Planet, (from + to) / 2);
        var steps = 600;
        var points = Enumerable.Range(0, steps + 1)
            .Select(i => from + ((to - from) * i / steps))
            .Select(day => (day, RetroModel.Geo(Planet, day)))
            .ToList();
        _skyPath = new SkyPath(Planet, from, to, period, points);
        return _skyPath;
    }

    /// <summary>Days apart of the dots along the path.</summary>
    private static double DotDays(RetroPlanet planet) => planet switch
    {
        RetroPlanet.Mercury => 1,
        RetroPlanet.Venus => 2,
        RetroPlanet.Mars => 4,
        _ => 5,
    };

    private void DrawSkyMode(PaintContext dc)
    {
        var stage = Stage();
        var area = new Rect(stage.Left + 28, stage.Top + 4, Math.Max(220, stage.Width - 56), Math.Max(120, stage.Height - 8));
        _skyArea = area;
        var now = _clock.Elapsed.TotalSeconds;
        var path = PathFor();
        var period = path.Period;
        var center = RetroModel.Geo(Planet, period.Middle).Longitude;

        // Fit the loop across the strip, longitude and latitude at the same scale.
        var us = path.Points.Select(p => RetroModel.Wrap180(p.Geo.Longitude - center)).ToList();
        var (uMin, uMax) = (us.Min(), us.Max());
        var (bMin, bMax) = (path.Points.Min(p => p.Geo.Latitude), path.Points.Max(p => p.Geo.Latitude));
        var midU = (uMin + uMax) / 2;
        var midB = (bMin + bMax) / 2;
        var scale = area.Width / ((Math.Max(uMax - uMin, 10) * 1.5) + 8);
        scale = Math.Min(scale, area.Height / ((bMax - bMin) + 7));
        var dir = EastLeft ? -1.0 : 1.0;
        Point P(double lon, double lat) => new(
            area.Left + (area.Width / 2) + (dir * (RetroModel.Wrap180(lon - center) - midU) * scale),
            area.Top + (area.Height / 2) - ((lat - midB) * scale));
        var halfLon = area.Width / 2 / scale;
        var halfLat = area.Height / 2 / scale;

        dc.PushClip(new RectangleGeometry(area, 14, 14));
        dc.DrawRectangle(Ink.NightSky, null, area);
        DrawSkyGrid(dc, area, center, midU, midB, halfLon, halfLat, scale, P);
        DrawSkyStars(dc, area, P);
        DrawZodiacNames(dc, area, center, midU, halfLon, scale, P);
        DrawSkySun(dc, area, scale, P);
        DrawSkyPath(dc, path, P, now);
        dc.Pop();
        dc.DrawRoundedRectangle(null, Ink.Frame, area, 14, 14);

        // The date, east and west, and a measure of the sky.
        DrawPill(dc, DateText(_day), new Point(area.Left + 70, area.Top + 18), Ink.Text, 12.5);
        var eastX = EastLeft ? area.Left + 22 : area.Right - 22;
        var westX = EastLeft ? area.Right - 22 : area.Left + 22;
        DrawText(dc, Loc("Retro_Dir_E"), new Point(eastX, area.Top + 48), Ink.Gold, 13, center: true, bold: true);
        DrawText(dc, Loc("Retro_Dir_W"), new Point(westX, area.Top + 18), Ink.Dim, 13, center: true, bold: true);
        var arrowFrom = new Point(area.Left + (area.Width / 2) - (dir * 70), area.Top + 18);
        var arrowTo = new Point(area.Left + (area.Width / 2) + (dir * 70), area.Top + 18);
        DrawArrow(dc, arrowFrom, arrowTo, FadedBrush(Ink.Gold, 0.8), 1.6, 8);
        DrawTargetPill(dc, Loc("Retro_Label_East"), new Point(area.Left + (area.Width / 2), area.Top + 38), Ink.Gold, "east", 11);

        var degrees = 10 * scale < area.Width / 4 ? 10.0 : 5.0;
        var barLeft = new Point(area.Left + 18, area.Bottom - 40);
        var barRight = barLeft + new Vector(degrees * scale, 0);
        dc.DrawLine(FadedPen(Ink.Text, 0.8, 2), barLeft, barRight);
        dc.DrawLine(FadedPen(Ink.Text, 0.8, 2), barLeft + new Vector(0, -5), barLeft + new Vector(0, 5));
        dc.DrawLine(FadedPen(Ink.Text, 0.8, 2), barRight + new Vector(0, -5), barRight + new Vector(0, 5));
        var scaleText = LocFormat(degrees >= 10 ? "Retro_Label_Scale10" : "Retro_Label_Scale5", Number(degrees, "0"));
        var scaleBox = DrawText(dc, scaleText, barLeft + new Vector(0, 16), Ink.Dim, 10.5);
        AddBoxTarget("scale", scaleBox.Union(new Rect(barLeft.X, barLeft.Y - 6, barRight.X - barLeft.X, 12)));
    }

    private void DrawSkyGrid(PaintContext dc, Rect area, double center, double midU, double midB, double halfLon, double halfLat, double scale, Func<double, double, Point> p)
    {
        var latStep = scale > 60 ? 1.0 : scale > 28 ? 2.0 : 5.0;
        for (var lat = Math.Ceiling((midB - halfLat) / latStep) * latStep; lat <= midB + halfLat; lat += latStep)
        {
            if (Math.Abs(lat) < 1e-9) continue;
            var y = p(center, lat).Y;
            dc.DrawLine(Ink.Grid, new Point(area.Left, y), new Point(area.Right, y));
            DrawText(dc, $"{(lat > 0 ? "+" : "−")}{Number(Math.Abs(lat), "0")}°", new Point(area.Right - 34, y - 8), Ink.Dim, 9.5);
        }

        var lonStep = scale > 40 ? 2.0 : scale > 18 ? 5.0 : 10.0;
        var first = center + midU - halfLon;
        for (var lon = Math.Ceiling(first / lonStep) * lonStep; lon <= center + midU + halfLon; lon += lonStep)
        {
            var x = p(lon, 0).X;
            dc.DrawLine(Ink.Grid, new Point(x, area.Top), new Point(x, area.Bottom));
            DrawText(dc, $"{Number(RetroModel.Wrap360(lon), "0")}°", new Point(x, area.Bottom - 10), Ink.Dim, 9.5, center: true);
        }

        // The ecliptic: the Sun's yearly path, the middle of the zodiac.
        var left = p(center + midU - (halfLon * 1.1), 0);
        var right = p(center + midU + (halfLon * 1.1), 0);
        if (left.Y < area.Top || left.Y > area.Bottom) return;
        dc.DrawLine(FadedPen(Ink.Sun, 0.45, 1.3, dashed: true), left, right);
        if (Glows("ecliptic")) dc.DrawLine(ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 8), left, right);
        var labelX = area.Left + (area.Width * 0.82);
        DrawTargetPill(dc, Loc("Retro_Label_Ecliptic"), new Point(labelX, left.Y + 16), Ink.Sun, "ecliptic", 10.5);
        for (var x = area.Left + 40; x < area.Right - 40; x += 90) _targets.Add("ecliptic", new Point(x, left.Y), 6);
    }

    private void DrawSkyStars(PaintContext dc, Rect area, Func<double, double, Point> p)
    {
        var room = area;
        room = room.Inflate(new Thickness(12, 12));
        foreach (var (a, b) in ZodiacSky.Lines)
        {
            if (ZodiacSky.Find(a) is not { } sa || ZodiacSky.Find(b) is not { } sb) continue;
            var pa = p(sa.Longitude, sa.Latitude);
            var pb = p(sb.Longitude, sb.Latitude);
            if (Math.Abs(pa.X - pb.X) > area.Width * 0.6 || (!room.Contains(pa) && !room.Contains(pb))) continue;
            dc.DrawLine(FadedPen(Ink.Figure, 0.32, 1), pa, pb);
        }

        foreach (var star in ZodiacSky.Stars)
        {
            var at = p(star.Longitude, star.Latitude);
            if (!room.Contains(at)) continue;
            var r = Math.Clamp(3.3 - (0.6 * star.Magnitude), 0.8, 4.4);
            var color = StarColor(star.ColorIndex);
            if (star.Magnitude < 1.6) dc.DrawEllipse(FadedBrush(color, 0.18), null, at, r * 2.6, r * 2.6);
            dc.DrawEllipse(FadedBrush(color, Math.Clamp(1.15 - (0.16 * star.Magnitude), 0.35, 1)), null, at, r, r);
        }
    }

    private void DrawZodiacNames(PaintContext dc, Rect area, double center, double midU, double halfLon, double scale, Func<double, double, Point> p)
    {
        foreach (var (id, start) in ZodiacSky.Constellations)
        {
            // Where the constellation's stretch of the ecliptic begins: a tick across it.
            var tick = p(start, 0);
            if (tick.X > area.Left && tick.X < area.Right)
            {
                dc.DrawLine(FadedPen(Ink.Figure, 0.5, 1, dashed: true), tick + new Vector(0, -1.5 * scale), tick + new Vector(0, 1.5 * scale));
            }

            var middle = ZodiacSky.MiddleOf(id);
            if (Math.Abs(RetroModel.Wrap180(middle - center - midU)) > halfLon) continue;
            var at = new Point(p(middle, 0).X, area.Bottom - 30);
            var box = DrawText(dc, Loc($"Retro_Zodiac_{id}"), at, Ink.Figure, 12, center: true, bold: true);
            if (box.Left > area.Left && box.Right < area.Right) AddBoxTarget("zodiac", box);
        }
    }

    /// <summary>The Sun where it is on the ecliptic, at its true size, with the glare round it that hides what is near it.</summary>
    private void DrawSkySun(PaintContext dc, Rect area, double scale, Func<double, double, Point> p)
    {
        var at = p(RetroModel.SunLongitude(_day), 0);
        var glare = Math.Min(12 * scale, area.Height * 0.7);
        if (at.X < area.Left - glare || at.X > area.Right + glare) return;
        var brush = SpaceDrawing.RadialGradient((Color.FromArgb(90, 0xFF, 0xD8, 0x6A), 0), (Color.FromArgb(0, 0xFF, 0xC8, 0x6A), 1));
        dc.DrawEllipse(brush, null, at, glare, glare);
        DrawSun(dc, at, Math.Max(7, 0.27 * scale));
        _targets.Add("sun", at, Math.Max(12, 0.27 * scale) + 4);
        if (ShowLabels) DrawPill(dc, Loc("Retro_Label_Sun"), at + new Vector(0, Math.Max(26, (0.27 * scale) + 22)), Ink.Sun, 10.5);
    }

    private void DrawSkyPath(PaintContext dc, SkyPath path, Func<double, double, Point> p, double now)
    {
        var period = path.Period;
        var color = PlanetColor(Planet);
        Point At(double day)
        {
            var g = RetroModel.Geo(Planet, day);
            return p(g.Longitude, g.Latitude);
        }

        // The whole loop, faint, so the shape shows before the planet gets there.
        if (ShowWholePath) dc.DrawGeometry(null, FadedPen(Ink.Text, 0.28, 1.2, dashed: true), Line(path.Points.Select(q => p(q.Geo.Longitude, q.Geo.Latitude))));

        // The way it has come: gold going east, orange going back west.
        var done = path.Points.Where(q => q.Day <= _day).Select(q => (q.Day, Point: p(q.Geo.Longitude, q.Geo.Latitude))).ToList();
        done.Add((_day, At(_day)));
        var runs = new List<(bool Retro, List<Point> Points)>();
        foreach (var (day, point) in done)
        {
            var retro = period.Contains(day);
            if (runs.Count == 0 || runs[^1].Retro != retro)
            {
                var run = new List<Point>();
                if (runs.Count > 0) run.Add(runs[^1].Points[^1]);
                runs.Add((retro, run));
            }

            runs[^1].Points.Add(point);
        }

        foreach (var (retro, points) in runs.Where(r => r.Points.Count > 1))
        {
            dc.DrawGeometry(null, FadedPen(retro ? Ink.Orange : Ink.Gold, 0.95, 2.6), Line(points));
        }

        // The backwards stretch glows when the walkthrough points at it; it is clickable along its length.
        var retroPoints = path.Points.Where(q => period.Contains(q.Day) && (ShowWholePath || q.Day <= _day)).Select(q => p(q.Geo.Longitude, q.Geo.Latitude)).ToList();
        if (retroPoints.Count > 1 && Glows("retro")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 9), Line(retroPoints));
        for (var i = 0; i < retroPoints.Count; i += 8) _targets.Add("retro", retroPoints[i], 7);

        // Dots a few days apart, and the first of each month.
        var step = DotDays(Planet);
        for (var day = Math.Ceiling(path.From / step) * step; day <= path.To; day += step)
        {
            var past = day <= _day;
            if (!past && !ShowWholePath) break;
            dc.DrawEllipse(FadedBrush(period.Contains(day) ? Ink.Orange : Ink.Gold, past ? 0.95 : 0.35), null, At(day), past ? 2.2 : 1.6, past ? 2.2 : 1.6);
        }

        // Stations' places, so a month's label does not sit on a station's.
        var stations = new[] { At(period.Start), At(period.End) };
        if (ShowLabels)
        {
            var month = RetroModel.DateOf(path.From);
            for (var date = new DateTime(month.Year, month.Month, 1).AddMonths(1); RetroModel.DayOf(date) <= path.To; date = date.AddMonths(1))
            {
                var day = RetroModel.DayOf(date);
                if (day > _day && !ShowWholePath) break;
                var at = At(day);
                dc.DrawEllipse(null, FadedPen(Ink.Text, 0.7, 1), at, 3.6, 3.6);
                if (stations.All(st => ((Vector)(st - at)).Length > 40)) DrawText(dc, LocFormat("Retro_Month", date), at + new Vector(0, 14), Ink.Dim, 9.5, center: true);
            }
        }

        // The two stations and the middle, with their dates.
        var middle = At(period.Middle);
        void Mark(double day, string id, string key, Color ink)
        {
            if (day > _day && !ShowWholePath) return;
            var at = At(day);
            dc.DrawEllipse(null, FadedPen(ink, 1, 2), at, 6.5, 6.5);
            _targets.Add(id, at, 9);
            if (!ShowLabels) return;

            // Below the station and out beyond the loop's end; the planet's own name goes above it.
            var side = Math.Sign(at.X - middle.X);
            DrawTargetPill(dc, LocFormat(key, DateText(day)), at + new Vector(side * 36, 28), ink, id, 10.5);
        }

        Mark(period.Start, "station-r", "Retro_Label_StationRetro", Ink.Cyan);
        Mark(period.End, "station-d", "Retro_Label_StationDirect", Ink.Cyan);
        if (period.Middle <= _day || ShowWholePath)
        {
            var id = RetroModel.IsInner(Planet) ? "conjunction" : "opposition";
            dc.DrawGeometry(FadedBrush(Ink.Orange, 1), null, Diamond(middle, 5));
            _targets.Add(id, middle, 9);
            if (ShowLabels)
            {
                var key = RetroModel.IsInner(Planet) ? "Retro_Label_Conjunction" : "Retro_Label_Opposition";
                // Under the whole path, joined to its mark: the planet's own name goes above the planet.
                var lowest = path.Points.Max(q => p(q.Geo.Longitude, q.Geo.Latitude).Y);
                var label = new Point(middle.X, Math.Max(middle.Y, lowest) + 30);
                dc.DrawLine(FadedPen(Ink.Orange, 0.5, 1, dashed: true), middle + new Vector(0, 6), label + new Vector(0, -9));
                DrawTargetPill(dc, LocFormat(key, DateText(period.Middle)), label, Ink.Orange, id, 10.5);
            }
        }

        // The planet, brighter (bigger) the nearer it is.
        var here = RetroModel.Geo(Planet, _day);
        var planet = p(here.Longitude, here.Latitude);
        var r = Math.Clamp(BaseRadius(Planet) * Math.Sqrt(TypicalDistance(Planet) / here.Distance), 3, 13);
        dc.DrawEllipse(FadedBrush(color, 0.22), null, planet, r * 2.6, r * 2.6);
        dc.DrawEllipse(FadedBrush(color, 1), null, planet, r, r);
        dc.DrawEllipse(FadedBrush(Colors.White, 0.85), null, planet, r * 0.45, r * 0.45);
        _targets.Add(PlanetId(Planet), planet, r + 4);
        var retroNow = period.Contains(_day);
        DrawTargetPill(dc, Loc($"Retro_Planet_{Planet}"), planet + new Vector(0, -r - 20), retroNow ? Ink.Orange : color, PlanetId(Planet), 11.5);
    }

    private static StreamGeometry Diamond(Point at, double r) =>
        Line([at + new Vector(0, -r), at + new Vector(r, 0), at + new Vector(0, r), at + new Vector(-r, 0)], closed: true, filled: true);

    /// <summary>The planet's dot at its usual distance, pixels.</summary>
    private static double BaseRadius(RetroPlanet planet) => planet switch
    {
        RetroPlanet.Mercury => 3.6,
        RetroPlanet.Venus => 6,
        RetroPlanet.Mars => 4.6,
        RetroPlanet.Jupiter => 6.4,
        _ => 5.2,
    };

    /// <summary>The planet's average distance from Earth, AU.</summary>
    private static double TypicalDistance(RetroPlanet planet)
    {
        var a = RetroModel.SemiMajorAxis(planet);
        return Math.Sqrt((a * a) + 1);
    }
}
