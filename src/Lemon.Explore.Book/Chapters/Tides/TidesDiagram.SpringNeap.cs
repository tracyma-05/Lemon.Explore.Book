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
/// The spring and neap view: the same view from above, with the Sun's tide added. The Sun raises bulges of its own,
/// a little under half the Moon's, along the line to the Sun. At new and full moon the two pairs line up and add up
/// (spring tides, the biggest range); at the quarters they are at right angles and partly cancel (neap tides). The
/// Moon's and the Sun's bulges are outlined, the water is their sum.
/// </summary>
public sealed partial class TidesDiagram
{
    private const double SpringMoonOrbit = 268;
    private const double SpringBulge = 12;
    private const double MarkerRadius = 9;

    /// <summary>The day that puts the Moon nearest to <paramref name="point"/>, or null right over Earth.</summary>
    private double? DayAt(Point point)
    {
        if (((Vector)(point - _origin)).Length < EarthRadius * 0.5 * _scale) return null;
        return AngleOf(point) / 360 * MoonPhaseGeometry.SynodicMonth;
    }

    private void DrawSpringNeap(PaintContext dc, double dpi, double now)
    {
        LayoutTop(360, 330);

        var moonAngle = TideGeometry.MoonAngle(_day);
        var s = _scale;
        var r = TideGeometry.SolarRatio;

        DrawSunGlow(dc, true);

        dc.DrawEllipse(null, TidesDrawing.OrbitPen, _origin, SpringMoonOrbit * s, SpringMoonOrbit * s);
        foreach (var at in new[] { 67.5, 247.5 })
        {
            var tip = TopPoint(at, SpringMoonOrbit);
            DrawArrowHead(dc, TidesDrawing.ArrowBrush, tip, tip - TopPoint(at - 3, SpringMoonOrbit), 8);
        }

        // The lines the two pairs of bulges lie along: towards the Sun and towards the Moon.
        dc.DrawLine(Spring.SunAxis, TopPoint(0, 205), TopPoint(180, 205));
        dc.DrawLine(Paint.AxisPen, TopPoint(moonAngle + 180, 205), TopPoint(moonAngle, SpringMoonOrbit - MoonRadius - 6));

        // The four principal phases with what they do to the tides, faded as the Moon passes over them.
        for (var quarter = 0; quarter < 4; quarter++)
        {
            var e = 90.0 * quarter;
            var gap = Math.Abs(SeasonsGeometry.Normalize180(moonAngle - e));
            var at = TopPoint(e, SpringMoonOrbit);
            var visibility = Noise.SmoothStep(8, 22, gap);
            if (visibility > 0.01)
            {
                dc.PushOpacity(0.8 * visibility);
                MoonDrawing.DrawPhaseIcon(dc, at, MarkerRadius * s, 90, new Vector(-1, 0));
                dc.Pop();
            }

            var key = quarter % 2 == 0 ? "Tides_Label_Spring" : "Tides_Label_Neap";
            var text = _labels.Get(key, 12, quarter % 2 == 0 ? TidesDrawing.High : TidesDrawing.Low, true);
            Vector outward = at - _origin;
            outward /= outward.Length;
            var reach = (MarkerRadius * s) + (MoonRadius * s) + 8;
            var center = at + (outward * (reach + (Math.Abs(outward.X) * text.Width / 2) + (Math.Abs(outward.Y) * text.Height / 2)));
            dc.DrawText(text, new Point(center.X - (text.Width / 2), center.Y - (text.Height / 2)));
            AddTextTarget(quarter % 2 == 0 ? "spring" : "neap", text, new Point(center.X - (text.Width / 2), center.Y - (text.Height / 2)));
        }

        // The water: the sum of the Moon's and the Sun's bulges, each outlined on its own.
        dc.DrawGeometry(TidesDrawing.WaterFill, TidesDrawing.WaterEdge,
            WaterShape(WaterRadius, SpringBulge, a => TideGeometry.WaterAt(a, moonAngle)));
        DrawSphere(dc, _earthTop, _origin, EarthRadius * s, new LightVector(-1, 0, 0.05), dpi, 0);
        _targets.Add("earth", _origin, EarthRadius * s);
        var solar = WaterShape(WaterRadius, SpringBulge, a => r * Math.Cos(2 * SeasonsGeometry.ToRadians(a)));
        var lunar = WaterShape(WaterRadius, SpringBulge, a => Math.Cos(2 * SeasonsGeometry.ToRadians(a - moonAngle)));
        if (Glows("sunbulge")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now), solar);
        if (Glows("moonbulge")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now), lunar);
        dc.DrawGeometry(null, TidesDrawing.SolarPen, solar);
        dc.DrawGeometry(null, TidesDrawing.LunarPen, lunar);

        // The Moon, lit from the left.
        var moon = TopPoint(moonAngle, SpringMoonOrbit);
        DrawSphere(dc, _moonTop, moon, MoonRadius * s, new LightVector(-1, 0, 0.05), dpi, -SeasonsGeometry.ToRadians(moonAngle));
        _targets.Add("moon", moon, MoonRadius * s);

        // How big the tides are today, under Earth.
        var fraction = TideGeometry.SpringFraction(_day);
        var kind = Loc(fraction > 0.8 ? "Tides_Kind_Spring" : fraction < 0.2 ? "Tides_Kind_Neap"
            : TideGeometry.IsRangeGrowing(_day) ? "Tides_Kind_Growing" : "Tides_Kind_Shrinking");
        var range = LocFormat("Tides_Label_Range", kind, TideGeometry.Range(_day) * TideGeometry.CoastMetres);
        var color = KindColor(fraction);
        DrawCenteredPill(dc, range, new Point(_origin.X, _origin.Y + (WaterRadius * s) + 40), 12.5, color);
        AddCenteredPillTarget("range", range, new Point(_origin.X, _origin.Y + (WaterRadius * s) + 40), 12.5, color);
    }

    /// <summary>From the colour of low water at neap tides to that of high water at spring tides.</summary>
    private static Color KindColor(double fraction) => Lerp(TidesDrawing.Low, TidesDrawing.High, fraction);

    private static class Spring
    {
        public static readonly IPen SunAxis = MakePen(Solid(110, 0xFF, 0xD3, 0x6B), 1, dashed: true);
    }
}
