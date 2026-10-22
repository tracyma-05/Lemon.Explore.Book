using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Controls.Space;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.MoonPhases;

/// <summary>
/// The sky view: a panorama from east to west facing the equator (south in the northern hemisphere, north in the
/// southern), azimuth across and altitude up. The Sun and the Moon follow the same daily path, the Moon lagging behind
/// the Sun by its elongation: the new moon crosses the sky with the Sun, the full moon opposite it, all night.
/// The Moon is drawn several times its real size, with its lit side turned towards the Sun. Altitude is stretched
/// up to twice the scale of azimuth, so the sky fills the view.
/// </summary>
public sealed partial class MoonPhasesDiagram
{
    /// <summary>Half the width of the panorama in degrees of azimuth.</summary>
    private const double SkyHalfWidth = 115;

    private const double SkyMoonRadius = 26;

    private double _skyCenter = 180;
    private double _skyScaleX = 4;
    private double _skyScaleY = 4;
    private Point _moonAt;
    private double _horizonY;
    private double _skyX;

    /// <summary>Screen point of a local direction (east, north, up).</summary>
    private Point SkyPoint((double East, double North, double Up) direction)
    {
        var azimuth = MoonPhaseGeometry.Azimuth(direction);
        var altitude = MoonPhaseGeometry.Altitude(direction);
        return SkyPoint(azimuth, altitude);
    }

    private Point SkyPoint(double azimuth, double altitude) =>
        new(_skyX + (SeasonsGeometry.Normalize180(azimuth - _skyCenter) * _skyScaleX), _horizonY - (altitude * _skyScaleY));

    private void DrawSky(PaintContext dc, double dpi, double now)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        var latitude = Math.Clamp(Latitude, -89, 89);
        var southern = latitude < 0;

        // Leave room for the title above, and the ground with its labels and the toolbar below.
        _horizonY = height - 150 - _reserve.Current;
        _skyScaleX = Math.Max(1, (width - 60) / (2 * SkyHalfWidth));
        _skyScaleY = Math.Clamp((_horizonY - 160) / (90 - Math.Abs(latitude) + 6), _skyScaleX, 2 * _skyScaleX);
        _skyX = width / 2;
        _skyCenter = southern ? 0 : 180;

        var elongation = MoonPhaseGeometry.Elongation(_age);
        var sun = MoonPhaseGeometry.SunDirection(latitude, _time);
        var moon = MoonPhaseGeometry.MoonDirection(latitude, _age, _time);
        var sunAltitude = MoonPhaseGeometry.Altitude(sun);
        var moonAltitude = MoonPhaseGeometry.Altitude(moon);
        var daylight = Noise.SmoothStep(-8, 8, sunAltitude);

        dc.DrawRectangle(SeasonsDiagram.SkyBrush(sunAltitude), null, new Rect(0, 0, width, height));
        _stars.Draw(dc, width, height, dpi, now, 0.85 * (1 - daylight));

        _moonAt = SkyPoint(moon);
        DrawDailyPath(dc, latitude, now);
        DrawMoonHours(dc, latitude);

        // The Sun; the ground covers it once it has set.
        var sunAt = SkyPoint(sun);
        if (sunAltitude > -4)
        {
            var glow = 50 * Math.Max(0.3, daylight);
            dc.PushOpacity(Math.Max(0.2, daylight));
            dc.DrawEllipse(SkyPaint.SunGlow, null, sunAt, glow, glow);
            dc.Pop();
            dc.DrawEllipse(sunAltitude > 3 ? SkyPaint.SunDisc : SkyPaint.LowSunDisc, null, sunAt, 12, 12);
            _targets.Add("skysun", sunAt, 14);
        }

        var moonAt = _moonAt;
        var litSide = SkyLitSide(moon, sun, moonAt, sunAt);
        if (moonAltitude > -4) DrawSkyMoon(dc, dpi, moonAt, elongation, litSide, daylight);
        if (moonAt.Y - SkyMoonRadius < height - 70) _targets.Add("skymoon", moonAt, SkyMoonRadius);

        // The ground in front, the horizon and the compass.
        dc.PushOpacity(0.6 + (0.4 * daylight));
        dc.DrawRectangle(SkyPaint.Ground, null, new Rect(0, _horizonY, width, height - _horizonY));
        dc.Pop();
        dc.DrawLine(SkyPaint.HorizonPen, new Point(0, _horizonY), new Point(width, _horizonY));
        DrawSkyCompass(dc, southern);
        DrawRiseAndSet(dc);

        if (moonAltitude <= 0)
        {
            // Where the Moon is under the horizon, while it is not too far down.
            var radius = SkyMoonRadius;
            if (moonAt.Y - radius < height - 70 && moonAt.X > -radius && moonAt.X < width + radius)
            {
                dc.DrawEllipse(null, SkyPaint.BelowPen, moonAt, radius, radius);
                var below = _labels.Get("MoonPhases_Label_MoonBelow", 11, MoonDrawing.DimText);
                dc.DrawText(below, new Point(moonAt.X - (below.Width / 2), moonAt.Y + radius + 4));
            }
        }
        else
        {
            var text = LocFormat("MoonPhases_Label_MoonUp", Loc(MoonDrawing.PhaseKey(MoonPhaseGeometry.PhaseOf(_age))), moonAltitude);
            var label = _labels.Text(text, 11.5, MoonDrawing.Text);
            var y = moonAt.Y + SkyMoonRadius + 8;
            if (y + label.Height > _horizonY - 4) y = moonAt.Y - SkyMoonRadius - label.Height - 12;
            _labels.DrawTextPill(dc, text, new Point(moonAt.X - (label.Width / 2) - 6, y), 11.5, MoonDrawing.Text);
        }

        // The time, large, in the top right corner.
        var clock = _labels.Text(SeasonsDiagram.ClockText(_time), 30, MoonDrawing.Text);
        dc.DrawText(clock, new Point(width - clock.Width - 26, 22));
        var solar = _labels.Get("MoonPhases_Label_SolarTime", 11, MoonDrawing.DimText);
        dc.DrawText(solar, new Point(width - solar.Width - 26, 22 + clock.Height));
    }

    /// <summary>
    /// Which way (on screen) the Moon's lit limb points: along the great circle from the Moon towards the Sun, turned
    /// into this chart's directions (azimuth across, altitude up, each with its own scale).
    /// </summary>
    private Vector SkyLitSide((double East, double North, double Up) moon, (double East, double North, double Up) sun, Point moonAt, Point sunAt)
    {
        var m = new V3(moon.East, moon.North, moon.Up);
        var s = new V3(sun.East, sun.North, sun.Up);
        var towards = s - (m * V3.Dot(m, s));
        var altitude = Math.Asin(Math.Clamp(m.Z, -1, 1));
        var azimuth = Math.Atan2(m.X, m.Y);
        var right = new V3(Math.Cos(azimuth), -Math.Sin(azimuth), 0);
        var up = new V3(-Math.Sin(altitude) * Math.Sin(azimuth), -Math.Sin(altitude) * Math.Cos(azimuth), Math.Cos(altitude));
        var azimuthRate = V3.Dot(towards, right) / Math.Max(Math.Cos(altitude), 0.05);
        var direction = new Vector(azimuthRate * _skyScaleX, -V3.Dot(towards, up) * _skyScaleY);
        if (direction.Length > 1e-6) return direction / direction.Length;

        // New or full moon: any direction; take the one towards the Sun on screen.
        Vector fallback = sunAt - moonAt;
        return fallback.Length > 1e-6 ? fallback / fallback.Length : new Vector(1, 0);
    }

    private void DrawSkyMoon(PaintContext dc, double dpi, Point at, double elongation, Vector litSide, double daylight)
    {
        var radius = SkyMoonRadius;

        // The dark side shows faintly at night (earthshine) and not at all against the daytime sky.
        dc.PushOpacity(0.3 * (1 - daylight));
        dc.DrawEllipse(SkyPaint.Earthshine, null, at, radius, radius);
        dc.Pop();

        var lit = MoonDrawing.LitShape(at, radius, elongation, litSide);
        if (lit is null) return;

        dc.PushOpacity(0.8 + (0.2 * (1 - daylight)));
        dc.PushClip(lit);
        DrawSphere(dc, MoonDisc(), at, radius, MoonDrawing.DiscLight(elongation, litSide), dpi);
        dc.Pop();
        dc.Pop();
    }

    /// <summary>The path the Sun (and, about, the Moon) takes across the sky today: the celestial equator.</summary>
    private void DrawDailyPath(PaintContext dc, double latitude, double now)
    {
        const int steps = 120;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var open = false;
            for (var i = 0; i <= steps; i++)
            {
                var direction = SeasonsGeometry.SunDirection(latitude, 0, -180 + (360.0 * i / steps));
                if (direction.Up < -0.01)
                {
                    open = false;
                    continue;
                }

                var point = SkyPoint(direction);
                if (!open)
                {
                    ctx.BeginFigure(point, false);
                    open = true;
                }
                else
                {
                    ctx.LineTo(point);
                }
            }

            ctx.EndFigure(false);
        }
        if (Highlight == "path") dc.DrawGeometry(null, ExploreTargets.GlowPen(now), geometry);
        dc.DrawGeometry(null, SkyPaint.PathPen, geometry);

        var top = SkyPoint(SeasonsGeometry.SunDirection(latitude, 0, 0));
        var label = _labels.Get("MoonPhases_Label_DailyPath", 11, MoonDrawing.DimText);
        dc.DrawText(label, new Point(top.X - (label.Width / 2), top.Y - label.Height - 6));
    }

    /// <summary>Where the Moon is at every third hour today, so its rise and set can be read off.</summary>
    private void DrawMoonHours(PaintContext dc, double latitude)
    {
        for (var hour = 0; hour < 24; hour += 3)
        {
            var direction = MoonPhaseGeometry.MoonDirection(latitude, _age, hour);
            if (direction.Up < 0.03) continue;

            var at = SkyPoint(direction);

            // Not under the Moon and its label.
            if (((Vector)(at - _moonAt)).Length < SkyMoonRadius + 34) continue;
            dc.DrawEllipse(SkyPaint.HourDot, null, at, 2.5, 2.5);
            var text = _labels.Text(SeasonsDiagram.ClockText(hour), 10, MoonDrawing.DimText);
            dc.DrawText(text, new Point(at.X - (text.Width / 2), at.Y + 5));
        }
    }

    private void DrawSkyCompass(PaintContext dc, bool southern)
    {
        var points = southern
            ? new[] { (270.0, "Seasons_Compass_W"), (315.0, "MoonPhases_Compass_NW"), (0.0, "Seasons_Compass_N"), (45.0, "MoonPhases_Compass_NE"), (90.0, "Seasons_Compass_E") }
            : [(90.0, "Seasons_Compass_E"), (135.0, "MoonPhases_Compass_SE"), (180.0, "Seasons_Compass_S"), (225.0, "MoonPhases_Compass_SW"), (270.0, "Seasons_Compass_W")];
        foreach (var (azimuth, key) in points)
        {
            var at = SkyPoint(azimuth, 0);
            dc.DrawLine(SkyPaint.HorizonPen, at, new Point(at.X, at.Y + 6));
            var main = azimuth % 90 == 0;
            var text = _labels.Get(key, main ? 13 : 11, main ? MoonDrawing.Text : MoonDrawing.DimText, main);
            dc.DrawText(text, new Point(at.X - (text.Width / 2), at.Y + 8));
        }
    }

    /// <summary>Rise and set times on the horizon: everything on the celestial equator rises due east and sets due west.</summary>
    private void DrawRiseAndSet(PaintContext dc)
    {
        foreach (var (azimuth, moonKey, moonHour, sunKey, sunHour) in new[]
                 {
                     (90.0, "MoonPhases_Label_Moonrise", MoonPhaseGeometry.MoonriseHour(_age), "Seasons_Label_Sunrise", 6.0),
                     (270.0, "MoonPhases_Label_Moonset", MoonPhaseGeometry.MoonsetHour(_age), "Seasons_Label_Sunset", 18.0),
                 })
        {
            var at = SkyPoint(azimuth, 0);
            _targets.Add(azimuth < 180 ? "moonrise" : "moonset", new Point(at.X, at.Y + 40), 24);
            var moon = _labels.Text(LocFormat(moonKey, SeasonsDiagram.ClockText(moonHour)), 11.5, MoonDrawing.MoonColor, true);
            var sun = _labels.Text(LocFormat(sunKey, SeasonsDiagram.ClockText(sunHour)), 11, MoonDrawing.Gold);
            var y = at.Y + 30;
            var moonX = Math.Clamp(at.X - (moon.Width / 2), 8, Math.Max(8, ActualWidth - moon.Width - 8));
            var sunX = Math.Clamp(at.X - (sun.Width / 2), 8, Math.Max(8, ActualWidth - sun.Width - 8));
            dc.DrawText(moon, new Point(moonX, y));
            dc.DrawText(sun, new Point(sunX, y + moon.Height));
        }
    }

    private static class SkyPaint
    {
        public static readonly IPen HorizonPen = MakePen(Solid(170, 255, 255, 255), 1.2);
        public static readonly IPen PathPen = MakePen(Solid(90, 255, 255, 255), 1.1, dashed: true);
        public static readonly IBrush HourDot = Solid(200, 0xD9, 0xDC, 0xE3);
        public static readonly IPen BelowPen = MakePen(Solid(130, 0xD9, 0xDC, 0xE3), 1.1, dashed: true);
        public static readonly IBrush Earthshine = Solid(255, 0x2A, 0x2F, 0x3A);

        public static readonly IBrush Ground = SpaceDrawing.LinearGradient(Color.FromRgb(0x2E, 0x40, 0x25), Color.FromRgb(0x10, 0x16, 0x0D), 90);

        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(170, 255, 240, 200), 0.15),
            (Color.FromArgb(60, 255, 210, 140), 0.45),
            (Color.FromArgb(0, 255, 190, 110), 1));

        public static readonly IBrush SunDisc = SpaceDrawing.RadialGradient((Color.FromRgb(0xFF, 0xFC, 0xF0), 0), (Color.FromRgb(0xFF, 0xD8, 0x7A), 1));
        public static readonly IBrush LowSunDisc = SpaceDrawing.RadialGradient((Color.FromRgb(0xFF, 0xD9, 0xA0), 0), (Color.FromRgb(0xF2, 0x8A, 0x48), 1));
    }
}
