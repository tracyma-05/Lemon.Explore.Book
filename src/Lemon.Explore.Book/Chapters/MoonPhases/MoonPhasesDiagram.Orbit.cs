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
/// The phases view: Earth and the Moon's orbit seen from far above the North Pole, sunlight coming in from the left.
/// The Moon goes round counter-clockwise and is always lit on its Sun side; how much of that lit half faces Earth is
/// the phase. Small half-lit moons mark eight points of the orbit, and an outer ring shows how the Moon looks from
/// Earth at each. Not to scale: the Moon's orbit is really 30 Earth diameters across.
/// </summary>
public sealed partial class MoonPhasesDiagram
{
    private const double OrbitRadius = 230;
    private const double EarthRadius = 44;
    private const double MoonRadius = 22;
    private const double MarkerRadius = 8;
    private const double RingRadius = 300;
    private const double IconRadius = 13;

    // View transform of the orbit view: Earth's centre on screen and DIPs per unit.
    private Point _orbitOrigin;
    private double _orbitScale = 1;

    /// <summary>Screen point of the Moon at <paramref name="elongation"/> on a circle of <paramref name="radius"/> round Earth.</summary>
    private Point OrbitPoint(double elongation, double radius)
    {
        var (sin, cos) = Math.SinCos(SeasonsGeometry.ToRadians(elongation));
        return new Point(_orbitOrigin.X - (cos * radius * _orbitScale), _orbitOrigin.Y + (sin * radius * _orbitScale));
    }

    /// <summary>The age that puts the Moon nearest to <paramref name="point"/>, or null right over Earth.</summary>
    private double? AgeAt(Point point)
    {
        Vector v = point - _orbitOrigin;
        if (v.Length < EarthRadius * 0.5 * _orbitScale) return null;
        var elongation = SeasonsGeometry.Normalize360(SeasonsGeometry.ToDegrees(Math.Atan2(v.Y, -v.X)));
        return elongation / 360 * MoonPhaseGeometry.SynodicMonth;
    }

    private void DrawOrbit(PaintContext dc, double dpi, double now)
    {
        var width = ActualWidth;
        var height = ActualHeight;

        // Leave room for the title above and the toolbar below.
        // The title block has four lines (with the click hint).
        const double top = 122;
        var bottom = 92 + _reserve.Current;
        _orbitScale = Math.Max(0.3, Math.Min(width / (2 * 380), (height - top - bottom) / (2 * 335)));
        _orbitOrigin = new Point(width / 2, top + ((height - top - bottom) / 2));

        var elongation = MoonPhaseGeometry.Elongation(_age);
        var southern = IsSouthern;
        var s = _orbitScale;

        DrawSunlight(dc, width, height);

        // The Moon's orbit, with arrows showing which way it goes.
        if (Highlight == "orbit") dc.DrawEllipse(null, ExploreTargets.GlowPen(now), _orbitOrigin, OrbitRadius * s, OrbitRadius * s);
        dc.DrawEllipse(null, Orbit.OrbitPen, _orbitOrigin, OrbitRadius * s, OrbitRadius * s);
        foreach (var at in new[] { 112.5, 292.5 })
        {
            var tip = OrbitPoint(at, OrbitRadius);
            DrawArrowHead(dc, Orbit.ArrowBrush, tip, tip - OrbitPoint(at - 3, OrbitRadius), 8);
        }

        // Eight points of the month: on the orbit the Moon is always lit from the left; outside, how it looks from Earth.
        foreach (var phase in Enum.GetValues<MoonPhase>())
        {
            var e = 45.0 * (int)phase;
            var gap = Math.Abs(SeasonsGeometry.Normalize180(elongation - e));
            var visibility = Noise.SmoothStep(8, 22, gap);
            if (visibility > 0.01)
            {
                dc.PushOpacity(0.75 * visibility);
                MoonDrawing.DrawPhaseIcon(dc, OrbitPoint(e, OrbitRadius), MarkerRadius * s, 90, new Vector(-1, 0));
                dc.Pop();
            }

            DrawRingIcon(dc, phase, e, southern, MoonPhaseGeometry.PhaseOf(_age) == phase);
        }

        // Earth, its day side towards the Sun.
        DrawSphere(dc, _earthTop, _orbitOrigin, EarthRadius * s, new LightVector(-1, 0, 0.05), dpi, 1.2);
        _targets.Add("earth", _orbitOrigin, EarthRadius * s);
        _labels.DrawCentered(dc, "SolarSystem_Earth_Name", new Point(_orbitOrigin.X, _orbitOrigin.Y + (EarthRadius * s) + 4), 12.5, MoonDrawing.Text, true);

        // The Moon, its line of sight to Earth, and the two halves that make the phase.
        var moon = OrbitPoint(elongation, OrbitRadius);
        var radius = MoonRadius * s;
        Vector toEarth = _orbitOrigin - moon;
        toEarth /= toEarth.Length;
        dc.DrawLine(Orbit.SightPen, moon + (toEarth * (radius + 4)), _orbitOrigin - (toEarth * ((EarthRadius * s) + 4)));

        // Keeps the same face towards Earth: it turns once on its axis for every time round.
        DrawSphere(dc, _moonTop, moon, radius, new LightVector(-1, 0, 0.05), dpi, -SeasonsGeometry.ToRadians(elongation));
        var arc = radius + 6;
        _targets.Add("moon", moon, arc + 4);
        if (Highlight == "halves")
        {
            var glow = ExploreTargets.GlowPen(now, 9);
            dc.DrawGeometry(null, glow, HalfCircle(moon, arc, new Vector(-1, 0)));
            dc.DrawGeometry(null, glow, HalfCircle(moon, arc + 5, toEarth));
        }

        dc.DrawGeometry(null, Orbit.LitHalfPen, HalfCircle(moon, arc, new Vector(-1, 0)));
        dc.DrawGeometry(null, Orbit.FacingHalfPen, HalfCircle(moon, arc + 5, toEarth));

        // The phase's name, on the Earth side of the Moon.
        var name = _labels.Get(MoonDrawing.PhaseKey(MoonPhaseGeometry.PhaseOf(_age)), 12, MoonDrawing.Text, true);
        var labelAt = moon + (toEarth * (arc + 18 + (name.Height / 2)));
        _labels.DrawPill(dc, MoonDrawing.PhaseKey(MoonPhaseGeometry.PhaseOf(_age)),
            new Point(labelAt.X - (name.Width / 2) - 6, labelAt.Y - (name.Height / 2) - 2), 12, MoonDrawing.Text);
    }

    /// <summary>Sunlight arriving from the far left: a glow at the edge and parallel rays that fade before the orbit.</summary>
    private void DrawSunlight(PaintContext dc, double width, double height)
    {
        var glow = Math.Max(220, height * 0.45);
        dc.DrawEllipse(Orbit.SunGlow, null, new Point(-glow * 0.45, _orbitOrigin.Y), glow, glow);

        var end = _orbitOrigin.X - ((RingRadius + 40) * _orbitScale);
        if (end < 60) return;

        var spacing = 46 * _orbitScale;
        for (var k = -7; k <= 7; k++)
        {
            var y = _orbitOrigin.Y + (k * spacing);
            if (y < 128 || y > height - 90) continue;
            dc.DrawLine(Orbit.RayPen, new Point(12, y), new Point(end, y));
            DrawArrowHead(dc, Orbit.RayArrow, new Point(end, y), new Vector(1, 0), 6);
        }

        var label = _labels.Get("MoonPhases_Label_Sunlight", 12, MoonDrawing.Gold, true);
        var labelY = _orbitOrigin.Y - (spacing / 2) - (label.Height / 2);
        var labelX = Math.Max(16, end - label.Width - 40);
        _labels.DrawPill(dc, "MoonPhases_Label_Sunlight", new Point(labelX, labelY - 2), 12, MoonDrawing.Gold);
        _targets.AddBox("sunlight", new Rect(labelX, labelY - 2, label.Width + 12, label.Height + 4));
    }

    /// <summary>One icon of the outer ring with its name; the current phase is ringed.</summary>
    private void DrawRingIcon(PaintContext dc, MoonPhase phase, double elongation, bool southern, bool current)
    {
        var s = _orbitScale;
        var at = OrbitPoint(elongation, RingRadius);
        MoonDrawing.DrawPhaseIcon(dc, at, IconRadius * s, elongation, MoonDrawing.LitSide(elongation, southern),
            current ? MoonDrawing.CurrentRing : null);
        _targets.Add("ring-" + phase, at, (IconRadius * s) + 3);

        var principal = MoonPhaseGeometry.IsPrincipal(phase);
        var text = _labels.Get(MoonDrawing.PhaseKey(phase), principal ? 11.5 : 10.5, principal ? MoonDrawing.Text : MoonDrawing.DimText, principal);
        Vector outward = at - _orbitOrigin;
        outward /= outward.Length;
        var reach = (IconRadius * s) + 6;
        Point topLeft;
        if (Math.Abs(outward.X) > 0.5)
        {
            // Beside the icon on the far left and right of the ring.
            var x = outward.X > 0 ? at.X + reach : at.X - reach - text.Width;
            topLeft = new Point(x, at.Y - (text.Height / 2) + (outward.Y * reach));
            if (Math.Abs(outward.Y) > 0.3)
            {
                // The diagonal ones: under or over the icon so they stay inside the view.
                topLeft = new Point(at.X - (text.Width / 2), outward.Y > 0 ? at.Y + reach : at.Y - reach - text.Height);
            }
        }
        else
        {
            topLeft = new Point(at.X - (text.Width / 2), outward.Y > 0 ? at.Y + reach : at.Y - reach - text.Height);
        }

        topLeft = topLeft.WithX(Math.Clamp(topLeft.X, 6, Math.Max(6, ActualWidth - text.Width - 6)));
        dc.DrawText(text, topLeft);
    }

    /// <summary>Half a circle round <paramref name="center"/>, centred on <paramref name="direction"/>.</summary>
    private static StreamGeometry HalfCircle(Point center, double radius, Vector direction)
    {
        const int steps = 32;
        var middle = Math.Atan2(direction.Y, direction.X);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i <= steps; i++)
            {
                var (sin, cos) = Math.SinCos(middle - (Math.PI / 2) + (Math.PI * i / steps));
                var point = new Point(center.X + (cos * radius), center.Y + (sin * radius));
                if (i == 0) ctx.BeginFigure(point, false);
                else ctx.LineTo(point);
            }

            ctx.EndFigure(false);
        }
        return geometry;
    }

    private static class Orbit
    {
        public static readonly IPen OrbitPen = MakePen(Solid(90, 255, 255, 255), 1.1, dashed: true);
        public static readonly IBrush ArrowBrush = Solid(150, 255, 255, 255);
        public static readonly IPen SightPen = MakePen(Solid(90, 0x8E, 0xC5, 0xFF), 1, dashed: true);
        public static readonly IPen LitHalfPen = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 2.2);
        public static readonly IPen FacingHalfPen = MakePen(Solid(240, 0x8E, 0xC5, 0xFF), 2, dashed: true);
        public static readonly IPen RayPen = MakePen(SpaceDrawing.LinearGradient(Color.FromArgb(120, 255, 226, 150), Color.FromArgb(18, 255, 226, 150), 0), 1);

        public static readonly IBrush RayArrow = Solid(70, 255, 226, 150);

        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(150, 255, 236, 190), 0),
            (Color.FromArgb(50, 255, 200, 120), 0.45),
            (Color.FromArgb(0, 255, 170, 80), 1));
    }
}
