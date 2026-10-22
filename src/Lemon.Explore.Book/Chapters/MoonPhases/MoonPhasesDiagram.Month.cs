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
/// The month view: a stretch of Earth's orbit round the Sun seen from above, with the Moon going round Earth. After
/// 27.3 days the Moon is back in line with the same distant star, but Earth has moved on round the Sun, so the Moon
/// needs about two more days to be between Earth and the Sun again: new moon to new moon takes 29.5 days. Not to
/// scale: the Moon's orbit is drawn hundreds of times too large for Earth's.
/// </summary>
public sealed partial class MoonPhasesDiagram
{
    /// <summary>Where Earth is on its orbit at the start (degrees round the Sun), so the month is centred in view.</summary>
    private const double MonthStart = -14.6;

    private Point _sunAt;
    private double _earthOrbit;
    private double _monthScale = 1;

    /// <summary>Earth on screen, <paramref name="age"/> days after the new moon.</summary>
    private Point EarthOnOrbit(double age)
    {
        var (sin, cos) = Math.SinCos(SeasonsGeometry.ToRadians(MonthStart + MoonPhaseGeometry.EarthOrbitAngle(age)));
        return new Point(_sunAt.X + (cos * _earthOrbit), _sunAt.Y - (sin * _earthOrbit));
    }

    /// <summary>Direction (screen, y down) of the Moon from Earth: towards the Sun at new moon, turning counter-clockwise.</summary>
    private static Vector MoonFromEarth(double age)
    {
        var angle = MonthStart + 180 + MoonPhaseGeometry.SiderealAngle(age);
        var (sin, cos) = Math.SinCos(SeasonsGeometry.ToRadians(angle));
        return new Vector(cos, -sin);
    }

    private void DrawMonth(PaintContext dc, double dpi, double now)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        _monthScale = Math.Max(0.4, Math.Min(width / 1000, (height - 190) / 560));
        var k = _monthScale;
        const double top = 120;
        var bottom = height - 100 - _reserve.Current;
        var moonOrbit = 112 * k;
        _sunAt = new Point(Math.Max(70, width * 0.1), (top + bottom) / 2);

        // Over the month Earth goes from 14.6° below the Sun to as far above it: keep it and the Moon's orbit round it
        // between the title and the toolbar.
        var fit = (((bottom - top) / 2) - moonOrbit) / Math.Sin(SeasonsGeometry.ToRadians(-MonthStart));
        _earthOrbit = Math.Max(200, Math.Min((width * 0.86) - _sunAt.X - (150 * k), fit));
        var age = Math.Clamp(_age, 0, MoonPhaseGeometry.SynodicMonth);

        // Earth's orbit: dashed ahead, solid where Earth has been this month.
        dc.DrawGeometry(null, MonthPaint.OrbitPen, OrbitArc(-8, MoonPhaseGeometry.SynodicMonth + 8));
        dc.DrawGeometry(null, MonthPaint.TravelledPen, OrbitArc(0, age));

        // The Sun.
        var sunRadius = 40 * k;
        dc.DrawEllipse(MonthPaint.SunGlow, null, _sunAt, sunRadius * 3, sunRadius * 3);
        DrawSphere(dc, _sun, _sunAt, sunRadius, new LightVector(0, 0, 1), dpi, _sunBody.SpinAngle(now));
        _targets.Add("sun", _sunAt, sunRadius);
        _labels.DrawCentered(dc, "SolarSystem_Sun_Name", new Point(_sunAt.X, _sunAt.Y + sunRadius + 6), 13, MoonDrawing.Text, true);

        // Where the month started, and where Earth will be after a sidereal and a synodic month.
        var start = EarthOnOrbit(0);
        dc.PushOpacity(0.45);
        dc.DrawEllipse(MonthPaint.GhostEarth, null, start, 9 * k, 9 * k);
        dc.DrawEllipse(null, MonthPaint.GhostOrbit, start, moonOrbit, moonOrbit);
        MoonDrawing.DrawPhaseIcon(dc, start + (MoonFromEarth(0) * moonOrbit), 6 * k, 0, new Vector(1, 0));
        dc.Pop();

        // The same distant star from the start, along a parallel line: it is far enough to be in the same direction
        // from anywhere on Earth's orbit, unlike the Sun.
        var reach = moonOrbit + (46 * k);
        dc.PushOpacity(0.55);
        dc.DrawLine(MonthPaint.StarLinePen, start, start + (MoonFromEarth(0) * reach));
        DrawStarIcon(dc, start + (MoonFromEarth(0) * (reach + 8)), 4);
        dc.Pop();
        DrawOrbitMark(dc, 0, "MoonPhases_Label_MonthStart");
        DrawOrbitMark(dc, MoonPhaseGeometry.SiderealMonth, "MoonPhases_Label_SiderealMark");
        DrawOrbitMark(dc, MoonPhaseGeometry.SynodicMonth, "MoonPhases_Label_SynodicMark");
        _targets.Add("start", EarthOnOrbit(0), 10);
        _targets.Add("sidereal", EarthOnOrbit(MoonPhaseGeometry.SiderealMonth), 10);
        _targets.Add("synodic", EarthOnOrbit(MoonPhaseGeometry.SynodicMonth), 10);

        // Earth now, the Moon's orbit round it, and the two directions the month can be measured against.
        var earth = EarthOnOrbit(age);
        Vector toSun = _sunAt - earth;
        toSun /= toSun.Length;
        var toStar = MoonFromEarth(0);

        dc.DrawEllipse(null, MonthPaint.MoonOrbitPen, earth, moonOrbit, moonOrbit);
        dc.DrawLine(MonthPaint.SunLinePen, earth, earth + (toSun * reach));
        dc.DrawLine(MonthPaint.StarLinePen, earth, earth + (toStar * reach));
        var star = earth + (toStar * (reach + 8));
        DrawStarIcon(dc, star, 5);
        _targets.Add("star", star, 10);
        var starLabel = _labels.Get("MoonPhases_Label_ToStar", 10.5, MoonDrawing.DimText);
        var sunLabel = _labels.Get("MoonPhases_Label_ToSun", 10.5, MoonDrawing.Gold);
        var apart = SpaceDrawing.AngleBetween(toStar, toSun);
        var starAbove = apart > 0;
        dc.DrawText(starLabel, new Point(star.X - starLabel.Width - 10, star.Y + (starAbove ? -starLabel.Height - 2 : 2)));
        var sunEnd = earth + (toSun * reach);
        if (Math.Abs(apart) > 4)
        {
            dc.DrawText(sunLabel, new Point(sunEnd.X - sunLabel.Width - 6, sunEnd.Y + (starAbove ? 2 : -sunLabel.Height - 2)));

            // How far Earth has gone round the Sun is the extra angle the Moon has to make up.
            var arcRadius = moonOrbit * 0.55;
            if (Highlight == "angle") dc.DrawGeometry(null, ExploreTargets.GlowPen(now), AngleArc(earth, arcRadius, toStar, toSun));
            dc.DrawGeometry(null, MonthPaint.AnglePen, AngleArc(earth, arcRadius, toStar, toSun));
            var middle = toStar + toSun;
            middle /= middle.Length;
            var degrees = _labels.Text($"{Math.Abs(apart):0}°", 11, MoonDrawing.Gold, true);
            var at = earth + (middle * (arcRadius + 12));
            dc.DrawText(degrees, new Point(at.X - (degrees.Width / 2), at.Y - (degrees.Height / 2)));
        }

        var light = LightFrom(earth, _sunAt, 0.05);
        DrawSphere(dc, _earthTop, earth, 20 * k, light, dpi, 1.2);
        _targets.Add("month-earth", earth, 20 * k);

        var moonDirection = MoonFromEarth(age);
        var moon = earth + (moonDirection * moonOrbit);
        // The same face towards Earth all month.
        var spin = -Math.Atan2(moonDirection.Y, -moonDirection.X);
        DrawSphere(dc, _moonTop, moon, 10 * k, LightFrom(moon, _sunAt, 0.05), dpi, spin);
        _targets.Add("month-moon", moon, 12 * k);

        DrawMonthMoment(dc, age, moon, earth);
    }

    /// <summary>A tick across Earth's orbit after <paramref name="age"/> days, with a label beyond the orbit.</summary>
    private void DrawOrbitMark(PaintContext dc, double age, string key)
    {
        var at = EarthOnOrbit(age);
        Vector outward = at - _sunAt;
        outward /= outward.Length;
        dc.DrawLine(MonthPaint.TickPen, at - (outward * 8), at + (outward * 8));

        // The label sits beyond the Moon's orbit, joined to its tick by a thin leader.
        var text = _labels.Get(key, 11, MoonDrawing.DimText);
        var anchor = at + (outward * ((128 * _monthScale) + 6));
        dc.DrawLine(MonthPaint.LeaderPen, at + (outward * 10), anchor - (outward * 4));
        dc.DrawText(text, new Point(Math.Min(anchor.X, ActualWidth - text.Width - 8), anchor.Y - (text.Height / 2)));
    }

    /// <summary>Calls out the two moments that make the point: back to the star, then new moon again.</summary>
    private void DrawMonthMoment(PaintContext dc, double age, Point moon, Point earth)
    {
        string? key = null;
        if (Math.Abs(age - MoonPhaseGeometry.SiderealMonth) < 0.45) key = "MoonPhases_Moment_Sidereal";
        else if (age > MoonPhaseGeometry.SynodicMonth - 0.45 || age < 0.3) key = "MoonPhases_Moment_Synodic";
        if (key is null) return;

        var text = _labels.Get(key, 12, MoonDrawing.Gold, true);
        Vector away = moon - earth;
        away /= away.Length;
        var at = moon + (away * (20 * _monthScale));
        _labels.DrawPill(dc, key, new Point(Math.Max(8, at.X - text.Width - 18), at.Y - (text.Height / 2) - 2), 12, MoonDrawing.Gold);
    }

    private StreamGeometry OrbitArc(double fromAge, double toAge)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            const int steps = 60;
            for (var i = 0; i <= steps; i++)
            {
                var point = EarthOnOrbit(fromAge + ((toAge - fromAge) * i / steps));
                if (i == 0) ctx.BeginFigure(point, false);
                else ctx.LineTo(point);
            }

            ctx.EndFigure(false);
        }
        return geometry;
    }

    private static StreamGeometry AngleArc(Point center, double radius, Vector from, Vector to)
    {
        var a = Math.Atan2(from.Y, from.X);
        var sweep = SeasonsGeometry.ToRadians(SpaceDrawing.AngleBetween(from, to));
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            const int steps = 24;
            for (var i = 0; i <= steps; i++)
            {
                var (sin, cos) = Math.SinCos(a + (sweep * i / steps));
                var point = new Point(center.X + (cos * radius), center.Y + (sin * radius));
                if (i == 0) ctx.BeginFigure(point, false);
                else ctx.LineTo(point);
            }

            ctx.EndFigure(false);
        }
        return geometry;
    }

    private static void DrawStarIcon(PaintContext dc, Point at, double size)
    {
        var points = new Point[8];
        for (var i = 0; i < 8; i++)
        {
            var (s, c) = Math.SinCos((i * Math.PI / 4) - (Math.PI / 2));
            var r = i % 2 == 0 ? size : size * 0.38;
            points[i] = new Point(at.X + (c * r), at.Y + (s * r));
        }

        dc.DrawGeometry(MonthPaint.StarBrush, null, Polygon(points));
    }

    private static class MonthPaint
    {
        public static readonly IPen OrbitPen = MakePen(Solid(80, 255, 255, 255), 1, dashed: true);
        public static readonly IPen TravelledPen = MakePen(Solid(170, 0x8E, 0xC5, 0xFF), 1.8);
        public static readonly IPen TickPen = MakePen(Solid(200, 255, 255, 255), 1.6);
        public static readonly IPen LeaderPen = MakePen(Solid(60, 255, 255, 255), 1);
        public static readonly IPen MoonOrbitPen = MakePen(Solid(90, 255, 255, 255), 1, dashed: true);
        public static readonly IPen GhostOrbit = MakePen(Solid(120, 255, 255, 255), 1, dashed: true);
        public static readonly IBrush GhostEarth = Solid(255, 0x5A, 0x8F, 0xD8);
        public static readonly IPen SunLinePen = MakePen(Solid(170, 0xFF, 0xD3, 0x6B), 1.2);
        public static readonly IPen StarLinePen = MakePen(Solid(170, 255, 255, 255), 1.2, dashed: true);
        public static readonly IPen AnglePen = MakePen(Solid(220, 0xFF, 0xD3, 0x6B), 1.4);
        public static readonly IBrush StarBrush = Solid(240, 255, 244, 214);

        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(200, 255, 236, 190), 0.25),
            (Color.FromArgb(70, 255, 200, 120), 0.4),
            (Color.FromArgb(0, 255, 170, 80), 1));
    }
}
