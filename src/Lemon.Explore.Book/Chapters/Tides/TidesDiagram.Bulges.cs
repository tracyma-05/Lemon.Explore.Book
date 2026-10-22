using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Chapters.MoonPhases;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Controls.Space;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Tides;

/// <summary>
/// The bulges view: Earth from far above the North Pole, sunlight from the left, and the Moon on its orbit. The Moon's
/// tidal force (its pull at each point minus its pull on Earth's centre) points out under the Moon and on the far side,
/// and in halfway between, so the water forms two bulges. Earth turns under them once a day, carrying you through two
/// high and two low tides; the Moon moves on meanwhile, so it takes 24 h 50 min to come round to the same bulge.
/// Not to scale: the bulges are exaggerated millions of times, and the Moon is really 60 Earth radii away.
/// </summary>
public sealed partial class TidesDiagram
{
    private const double EarthRadius = 100;
    private const double WaterRadius = 113;
    private const double BulgeHeight = 11;
    private const double MoonOrbit = 300;
    private const double MoonRadius = 27;
    private const double ForceRadius = 142;
    private const double ForceLength = 19;

    private void DrawBulges(PaintContext dc, double dpi, double now)
    {
        LayoutTop(380, 350);

        var day = _day;
        var moonAngle = TideGeometry.MoonAngle(day);
        var observer = TideGeometry.ObserverAngle(day);
        var s = _scale;

        DrawSunGlow(dc, false);

        // The Moon's orbit, which way it goes, and the line from Earth to the Moon the bulges lie along.
        dc.DrawEllipse(null, TidesDrawing.OrbitPen, _origin, MoonOrbit * s, MoonOrbit * s);
        foreach (var at in new[] { 112.5, 292.5 })
        {
            var tip = TopPoint(at, MoonOrbit);
            DrawArrowHead(dc, TidesDrawing.ArrowBrush, tip, tip - TopPoint(at - 3, MoonOrbit), 8);
        }

        dc.DrawLine(Paint.AxisPen, TopPoint(moonAngle + 180, 215), TopPoint(moonAngle, MoonOrbit - MoonRadius - 6));

        // The water with its two bulges, and Earth turning under it.
        var water = WaterShape(WaterRadius, BulgeHeight, a => TideGeometry.WaterAt(a, moonAngle, includeSun: false));
        if (Glows("bulges")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 9), water);
        dc.DrawGeometry(TidesDrawing.WaterFill, TidesDrawing.WaterEdge, water);
        DrawSphere(dc, _earthTop, _origin, EarthRadius * s, new LightVector(-1, 0, 0.05), dpi, -SeasonsGeometry.ToRadians(observer));
        _targets.Add("earth", _origin, EarthRadius * s);
        DrawSpinArrow(dc);

        if (ShowForces) DrawForces(dc, moonAngle);

        // Where it is high and low water, beyond the force arrows.
        DrawWaterLabel(dc, "Tides_Label_HighNear", "highnear", moonAngle, 222, TidesDrawing.High, observer);
        DrawWaterLabel(dc, "Tides_Label_HighFar", "highfar", moonAngle + 180, 222, TidesDrawing.High, observer);
        DrawWaterLabel(dc, "Tides_Label_Low", "low", moonAngle + 90, 190, TidesDrawing.Low, observer);
        DrawWaterLabel(dc, "Tides_Label_Low", "low", moonAngle - 90, 190, TidesDrawing.Low, observer);

        DrawObserver(dc, observer, day, includeSun: false);

        // The Moon keeps the same face towards Earth.
        var moon = TopPoint(moonAngle, MoonOrbit);
        DrawSphere(dc, _moonTop, moon, MoonRadius * s, new LightVector(-1, 0, 0.05), dpi, -SeasonsGeometry.ToRadians(moonAngle));
        _labels.DrawCentered(dc, "Tides_Label_Moon", new Point(moon.X, moon.Y + (MoonRadius * s) + 4), 12.5, TidesDrawing.Text, true);
        _targets.Add("moon", moon, MoonRadius * s);
    }

    /// <summary>
    /// A high or low water label round Earth, fading while the observer's label passes over it. The label keeps its size
    /// in pixels while the drawing scales, so it is pulled in toward Earth when its end would reach under the Moon.
    /// </summary>
    private void DrawWaterLabel(PaintContext dc, string key, string id, double angle, double radius, Color color, double observer)
    {
        var gap = Math.Abs(SeasonsGeometry.Normalize180(observer - angle));
        var visibility = Noise.SmoothStep(12, 30, gap);
        if (visibility < 0.01) return;

        var text = Loc(key);
        var formatted = _labels.Text(text, 12, color);
        Vector direction = TopPoint(angle, 1) - _origin;
        direction /= direction.Length;
        var halfReach = (Math.Abs(direction.X) * ((formatted.Width / 2) + 6)) + (Math.Abs(direction.Y) * ((formatted.Height / 2) + 2));
        var clear = ((MoonOrbit - MoonRadius) * _scale) - 6 - halfReach;
        var center = Math.Min(radius * _scale, Math.Max(clear, (WaterRadius + BulgeHeight) * _scale));

        dc.PushOpacity(visibility);
        DrawCenteredPill(dc, text, _origin + (direction * center), 12, color);
        dc.Pop();
        if (visibility > 0.5) AddCenteredPillTarget(id, text, _origin + (direction * center), 12, color);
    }

    /// <summary>Fits a view from above that reaches <paramref name="halfWidth"/> and <paramref name="halfHeight"/> units from Earth.</summary>
    private void LayoutTop(double halfWidth, double halfHeight)
    {
        // Leave room for the title above and the toolbar below.
        const double top = 104;
        var bottom = 92 + _reserve.Current;
        var width = ActualWidth;
        var height = ActualHeight;
        _scale = Math.Max(0.3, Math.Min(width / (2 * halfWidth), (height - top - bottom) / (2 * halfHeight)));
        _origin = new Point(width / 2, top + ((height - top - bottom) / 2));
    }

    /// <summary>Arrows of the tidal force round Earth, every 22.5° from the direction of the Moon.</summary>
    private void DrawForces(PaintContext dc, double moonAngle)
    {
        var s = _scale;
        for (var k = 0; k < 16; k++)
        {
            var angle = moonAngle + (k * 22.5);
            var (radial, tangential) = TideGeometry.TidalForce(angle - moonAngle);
            var (sin, cos) = Math.SinCos(SeasonsGeometry.ToRadians(angle));
            var outward = new Vector(-cos, sin);
            var along = new Vector(sin, cos);
            var force = ((outward * radial) + (along * tangential)) * ForceLength * s;

            // Inward arrows end at the base circle, outward ones start there, so none crosses the water.
            var start = TopPoint(angle, ForceRadius);
            if (radial < 0) start -= force;
            var end = start + force;
            dc.DrawLine(TidesDrawing.ForcePen, start, end);
            DrawArrowHead(dc, TidesDrawing.ForceBrush, end, force, 7);
        }

        var label = TopPoint(moonAngle + 45, ForceRadius + 46);
        DrawCenteredPill(dc, Loc("Tides_Label_Force"), label, 11.5, TidesDrawing.Force);
        AddCenteredPillTarget("force", Loc("Tides_Label_Force"), label, 11.5, TidesDrawing.Force);
    }

    /// <summary>The observer on the equator (the rim of Earth seen from above) with the local time.</summary>
    private void DrawObserver(PaintContext dc, double observer, double day, bool includeSun)
    {
        var s = _scale;
        var at = TopPoint(observer, EarthRadius);
        Vector outward = at - _origin;
        outward /= outward.Length;
        dc.DrawLine(Paint.PinPen, at, at + (outward * 26 * s));
        dc.DrawEllipse(Paint.PinBrush, TidesDrawing.DotOutline, at, 5, 5);

        var text = LocFormat("Tides_Label_You", SeasonsDiagram.ClockText(TideGeometry.HourOfDay(day)),
            Loc(StageKey(TideGeometry.StageAt(day, includeSun))));
        var formatted = _labels.Text(text, 11.5, TidesDrawing.Gold);
        var anchor = at + (outward * ((26 * s) + 14 + (Math.Abs(outward.X) * formatted.Width / 2)));
        var pillAt = new Point(anchor.X - (formatted.Width / 2) - 6, anchor.Y - (formatted.Height / 2) - 2);
        _labels.DrawTextPill(dc, text, pillAt, 11.5, TidesDrawing.Gold);
        _targets.Add("you", at, 6);
        AddPillTarget("you", text, pillAt, 11.5, TidesDrawing.Gold);
    }

    /// <summary>A curved arrow beside Earth: it turns counter-clockwise, once a day.</summary>
    private void DrawSpinArrow(PaintContext dc)
    {
        const double radius = 240;
        const double from = 205;
        const double to = 245;
        var r = radius * _scale;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(TopPoint(from, radius), false);
            ctx.ArcTo(TopPoint(to, radius), new Size(r, r), 0, false, SweepDirection.CounterClockwise);
            ctx.EndFigure(false);
        }

        dc.DrawGeometry(null, Paint.SpinPen, geometry);
        var tip = TopPoint(to, radius);
        DrawArrowHead(dc, Paint.SpinBrush, tip, tip - TopPoint(to - 3, radius), 8);

        var label = _labels.Get("Tides_Label_Spin", 11.5, TidesDrawing.DimText);
        var at = TopPoint((from + to) / 2, radius + 14);
        dc.DrawText(label, new Point(at.X + 4, at.Y - label.Height));
        AddTextTarget("spin", label, new Point(at.X + 4, at.Y - label.Height));
    }

    /// <summary>A glow at the left edge where the sunlight comes from; with rays and a label in spring and neap mode.</summary>
    private void DrawSunGlow(PaintContext dc, bool rays)
    {
        var height = ActualHeight;
        var glow = Math.Max(220, height * 0.45);
        dc.DrawEllipse(Paint.SunGlow, null, new Point(-glow * 0.45, _origin.Y), glow, glow);
        if (!rays) return;

        var end = _origin.X - (310 * _scale);
        if (end < 60) return;

        var spacing = 46 * _scale;
        for (var k = -6; k <= 6; k++)
        {
            var y = _origin.Y + (k * spacing);
            if (y < 128 || y > height - 90) continue;
            dc.DrawLine(Paint.RayPen, new Point(12, y), new Point(end, y));
            DrawArrowHead(dc, Paint.RayArrow, new Point(end, y), new Vector(1, 0), 6);
        }

        var label = _labels.Get("Tides_Label_Sun", 12, TidesDrawing.Gold, true);
        var sunAt = new Point(Math.Max(16, end - label.Width - 40), _origin.Y - (spacing / 2) - (label.Height / 2) - 2);
        _labels.DrawPill(dc, "Tides_Label_Sun", sunAt, 12, TidesDrawing.Gold);
        var pill = _labels.Get("Tides_Label_Sun", 12, TidesDrawing.Gold);
        _targets.AddBox("sun", new Rect(sunAt.X, sunAt.Y, pill.Width + 12, pill.Height + 4));
    }

    private void DrawCenteredPill(PaintContext dc, string text, Point center, double size, Color color)
    {
        var formatted = _labels.Text(text, size, color);
        _labels.DrawTextPill(dc, text, new Point(center.X - (formatted.Width / 2) - 6, center.Y - (formatted.Height / 2) - 2), size, color);
    }

    internal static string StageKey(TideStage stage) => $"Tides_Stage_{stage}";

    private static class Paint
    {
        public static readonly IPen AxisPen = MakePen(Solid(110, 0xB8, 0xC7, 0xFF), 1, dashed: true);
        public static readonly IPen PinPen = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 1.6);
        public static readonly IBrush PinBrush = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IPen SpinPen = MakePen(Solid(130, 255, 255, 255), 1.4);
        public static readonly IBrush SpinBrush = Solid(160, 255, 255, 255);

        public static readonly IPen RayPen = MakePen(SpaceDrawing.LinearGradient(Color.FromArgb(120, 255, 226, 150), Color.FromArgb(18, 255, 226, 150), 0), 1);

        public static readonly IBrush RayArrow = Solid(70, 255, 226, 150);

        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(150, 255, 236, 190), 0),
            (Color.FromArgb(50, 255, 200, 120), 0.45),
            (Color.FromArgb(0, 255, 170, 80), 1));
    }
}
