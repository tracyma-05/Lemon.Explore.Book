using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Kepler;

/// <summary>
/// The areas view, Kepler's second law: the orbit cut into <see cref="Sectors"/> stretches of equal time, each a slice
/// from the Sun. They fill in as the planet goes round: short and wide near the Sun, long and thin far out, all the same
/// area. So the planet covers more of its orbit in the same time near the Sun: it goes faster there.
/// </summary>
public sealed partial class KeplerDiagram
{
    private void DrawAreas(PaintContext dc, double dpi, double now)
    {
        var e = ShownEccentricity;
        LayoutOrbit(e);
        var body = KeplerGeometry.Body(BodyId);
        var s = _orbitScale;
        var b = Math.Sqrt(1 - (e * e));
        var count = Math.Clamp(Sectors, 2, 48);
        var phase = _phase;
        var current = Math.Min((int)Math.Floor(phase * count), count - 1);

        // The stretches already swept this time round, and the one being swept now up to the planet.
        for (var i = 0; i <= current; i++)
        {
            var from = (double)i / count;
            var to = i == current ? phase : (i + 1.0) / count;
            if (to - from < 1e-6) continue;
            var fill = i % 2 == 0 ? AreasPaint.FillA : AreasPaint.FillB;
            var edge = i % 2 == 0 ? AreasPaint.EdgeA : AreasPaint.EdgeB;
            dc.DrawGeometry(fill, edge, Sector(from, to, e));
        }

        // Where each stretch begins: far apart near the Sun, close together far out.
        for (var i = 0; i < count; i++)
        {
            var p = KeplerGeometry.At((double)i / count, e);
            var at = OrbitScreen(p.X, p.Y);
            dc.DrawLine(AreasPaint.Boundary, _sunScreen, at);
        }

        // The walkthrough's two slices to compare: the one leaving perihelion and the one leaving aphelion.
        if (Glows("sectors"))
        {
            var glow = ExploreTargets.GlowPen(now, 5);
            var half = count / 2;
            dc.DrawGeometry(null, glow, Sector(0, 1.0 / count, e));
            dc.DrawGeometry(null, glow, Sector((double)half / count, (half + 1.0) / count, e));
        }

        if (Glows("orbit")) dc.DrawEllipse(null, ExploreTargets.GlowPen(now), _center, s, b * s);
        dc.DrawEllipse(null, KeplerDrawing.OrbitPen, _center, s, b * s);
        for (var i = 0; i < count; i++)
        {
            var p = KeplerGeometry.At((double)i / count, e);
            dc.DrawEllipse(AreasPaint.Tick, null, OrbitScreen(p.X, p.Y), 2.6, 2.6);
        }

        // Number the stretches that are swept, where there is room.
        for (var i = 0; i < current; i++)
        {
            var middle = KeplerGeometry.At((i + 0.5) / count, e);
            var start = KeplerGeometry.At((double)i / count, e);
            var end = KeplerGeometry.At((i + 1.0) / count, e);
            var width = ((Vector)(OrbitScreen(end.X, end.Y) - OrbitScreen(start.X, start.Y))).Length;
            var at = OrbitScreen(middle.X * 0.62, middle.Y * 0.62);
            if (width < 16 || ((Vector)(at - _sunScreen)).Length < 22) continue;
            var text = _labels.Text((i + 1).ToString(), 11, i % 2 == 0 ? KeplerDrawing.SectorA : KeplerDrawing.SectorB, true);
            dc.DrawText(text, new Point(at.X - (text.Width / 2), at.Y - (text.Height / 2)));
        }

        DrawApsides(dc, body, e, true);
        DrawSun(dc, dpi, _sunScreen, SunRadius(e), now);
        _targets.Add("sun", _sunScreen, SunRadius(e));

        var point = KeplerGeometry.At(phase, e);
        var planet = OrbitScreen(point.X, point.Y);
        if (Glows("radius")) dc.DrawLine(ExploreTargets.GlowPen(now), _sunScreen, planet);
        dc.DrawLine(AreasPaint.RadiusVector, _sunScreen, planet);

        if (ShowVelocity)
        {
            var length = Math.Clamp(46 * point.Speed, 12, 220);
            var direction = new Vector(point.DirectionX, -point.DirectionY);
            var tip = planet + (direction * (length + PlanetRadius(e)));
            var start = planet + (direction * PlanetRadius(e));
            dc.DrawLine(KeplerDrawing.FastPen, start, tip);
            DrawArrowHead(dc, KeplerDrawing.FastBrush, tip, direction, 9);
            var speed = LocFormat("Kepler_Label_Speed", KeplerGeometry.SpeedKmS(body.SemiMajorAxisAu, point.Radius).ToString("0.#"));
            DrawCenteredPill(dc, speed, tip + (direction * 30), 11.5, KeplerDrawing.Fast);
            AddCenteredPillTarget("velocity", speed, tip + (direction * 30), 11.5, KeplerDrawing.Fast);
        }

        DrawPlanet(dc, dpi, body, point, planet, e);

        // Under the orbit: each stretch takes the same time and has the same area.
        var under = _center.Y + (b * s) + 30;
        if (under < ActualHeight - 96 - _reserve.Current)
        {
            var equal = LocFormat("Kepler_Label_EqualAreas", count, KeplerFormat.Duration(body.PeriodYears / count));
            DrawCenteredPill(dc, equal, new Point(_center.X, under), 12, KeplerDrawing.Text);
            AddCenteredPillTarget("equalareas", equal, new Point(_center.X, under), 12, KeplerDrawing.Text);
        }
    }

    /// <summary>The slice from the Sun to the orbit between two phases.</summary>
    private StreamGeometry Sector(double from, double to, double e)
    {
        var steps = Math.Max(4, (int)Math.Ceiling((to - from) * 360));
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(_sunScreen, true);
            for (var i = 0; i <= steps; i++)
            {
                var p = KeplerGeometry.At(from + ((to - from) * i / steps), e);
                ctx.LineTo(OrbitScreen(p.X, p.Y));
            }

            ctx.EndFigure(true);
        }
        return geometry;
    }

    private static class AreasPaint
    {
        public static readonly IBrush FillA = SpaceDrawing.Solid(WithAlpha(KeplerDrawing.SectorA, 0.3));
        public static readonly IBrush FillB = SpaceDrawing.Solid(WithAlpha(KeplerDrawing.SectorB, 0.26));
        public static readonly IPen EdgeA = MakePen(SpaceDrawing.Solid(WithAlpha(KeplerDrawing.SectorA, 0.7)), 1);
        public static readonly IPen EdgeB = MakePen(SpaceDrawing.Solid(WithAlpha(KeplerDrawing.SectorB, 0.7)), 1);
        public static readonly IPen Boundary = MakePen(Solid(34, 255, 255, 255), 1);
        public static readonly IBrush Tick = Solid(220, 255, 255, 255);
        public static readonly IPen RadiusVector = MakePen(Solid(170, 255, 255, 255), 1.4);
    }
}
