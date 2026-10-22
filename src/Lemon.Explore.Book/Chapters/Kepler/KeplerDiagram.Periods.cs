using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Kepler;

/// <summary>
/// The periods view, Kepler's third law: the planets from Mercury to Saturn at their real places, seen from above,
/// going round at their real speeds, and a planet of your own on a circle you drag nearer or farther. It starts in line
/// with Earth; the farther out, the longer its year (T² = a³), so it falls behind. To scale: the view zooms out to keep
/// your planet in sight.
/// </summary>
public sealed partial class KeplerDiagram
{
    /// <summary>Playback wraps round after this many years.</summary>
    public const double MaxYears = 100;

    private static readonly (string Id, double Radius)[] PeriodPlanets =
        [("Mercury", 5), ("Venus", 7), ("Earth", 7.5), ("Mars", 6), ("Jupiter", 12), ("Saturn", 11)];

    // View transform of the periods view: DIPs per AU, and the half-size of the view in AU, easing as your planet moves.
    private double _periodScale = 100;
    private double _viewRadius = double.NaN;
    private DateTime _pathsFor;
    private readonly Dictionary<string, (double X, double Y)[]> _paths = [];

    /// <summary>Half the view in AU: the inner planets, or out to just beyond your planet.</summary>
    private double TargetViewRadius => Math.Max(1.75, YourAxis * 1.18);

    /// <summary>Years per second: Earth's year in four seconds when zoomed in, faster as the view zooms out.</summary>
    private double YearsPerSecond => 0.25 * Math.Pow((double.IsNaN(_viewRadius) ? TargetViewRadius : _viewRadius) / 1.75, 1.5);

    private void DrawPeriods(PaintContext dc, double dpi, double now)
    {
        const double top = 120;
        var bottom = 100 + _reserve.Current;
        var halfWidth = (ActualWidth - 40) / 2;
        var halfHeight = (ActualHeight - top - bottom) / 2;
        var view = double.IsNaN(_viewRadius) ? TargetViewRadius : _viewRadius;
        _periodScale = Math.Max(4, Math.Min(halfWidth, halfHeight) / view);
        _sunScreen = new Point(ActualWidth / 2, top + halfHeight);
        var s = _periodScale;

        var start = StartUtc;
        EnsurePaths(start);
        var jd = KeplerOrbit.JulianDay(start) + (_years * KeplerGeometry.DaysPerYear);

        // The orbits, then the planets at their places and their years.
        foreach (var (id, _) in PeriodPlanets)
        {
            var path = _paths[id];
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                for (var i = 0; i < path.Length; i++)
                {
                    var p = new Point(_sunScreen.X + (path[i].X * s), _sunScreen.Y - (path[i].Y * s));
                    if (i == 0) ctx.BeginFigure(p, false);
                    else ctx.LineTo(p);
                }

                ctx.EndFigure(true);
            }
            dc.DrawGeometry(null, KeplerDrawing.FaintOrbitPen, geometry);
        }

        // Your planet's circle.
        var yours = YourAxis;
        if (Glows("yourorbit")) dc.DrawEllipse(null, ExploreTargets.GlowPen(now), _sunScreen, yours * s, yours * s);
        dc.DrawEllipse(null, PeriodsPaint.YourOrbit, _sunScreen, yours * s, yours * s);

        var sunRadius = Math.Clamp(0.09 * s, 6, 14);
        DrawSun(dc, dpi, _sunScreen, sunRadius, now);
        _targets.Add("sun", _sunScreen, sunRadius);

        foreach (var (id, radius) in PeriodPlanets)
        {
            var body = Body(id);
            var state = body.Orbit!.At(jd);
            var (x, y) = state.PositionAt(state.EccentricAnomaly);
            var at = new Point(_sunScreen.X + (x * s), _sunScreen.Y - (y * s));
            var orbitRadius = body.Orbit.SemiMajorAxis * s;
            if (orbitRadius > Math.Max(ActualWidth, ActualHeight)) continue;

            // Crowded near the Sun when zoomed out: a dot, then a planet without its name.
            if (orbitRadius < 18)
            {
                dc.DrawEllipse(PeriodsPaint.SmallDot, null, at, 2, 2);
                continue;
            }

            DrawSphere(dc, Renderer(id), at, Math.Min(radius, orbitRadius / 3), LightFrom(at, _sunScreen, 0.15), dpi);
            _targets.Add(PlanetPrefix + id, at, Math.Min(radius, orbitRadius / 3) + 2);
            if (orbitRadius < 48) continue;

            var period = KeplerGeometry.PeriodPoints.First(p => p.Id == id).PeriodYears;
            var text = LocFormat("Kepler_Label_PlanetYear", Loc(BodyNameKey(id)), KeplerFormat.Duration(period));
            var label = _labels.Text(text, 11, KeplerDrawing.DimText);
            var left = Math.Clamp(at.X - (label.Width / 2), 4, Math.Max(4, ActualWidth - label.Width - 4));
            dc.DrawText(label, new Point(left, at.Y + radius + 3));
        }

        // Your planet started in line with Earth.
        var earthStart = Body("Earth").Orbit!.At(KeplerOrbit.JulianDay(start));
        var (ex, ey) = earthStart.PositionAt(earthStart.EccentricAnomaly);
        var angle = Math.Atan2(ey, ex) + (2 * Math.PI * _years / KeplerGeometry.PeriodYears(yours));
        var (sin, cos) = Math.SinCos(angle);
        var you = new Point(_sunScreen.X + (cos * yours * s), _sunScreen.Y - (sin * yours * s));
        dc.DrawEllipse(PeriodsPaint.YouGlow, null, you, 16, 16);
        dc.DrawEllipse(KeplerDrawing.YouBrush, KeplerDrawing.DotOutline, you, 7, 7);
        var yourText = LocFormat("Kepler_Label_You", KeplerFormat.Au(yours), KeplerFormat.Duration(KeplerGeometry.PeriodYears(yours)));
        var yourLabel = _labels.Text(yourText, 12, KeplerDrawing.You);
        var side = cos >= 0 ? 14 : -yourLabel.Width - 26;
        var yourAt = new Point(you.X + side, you.Y - (yourLabel.Height / 2) - 2);
        _labels.DrawTextPill(dc, yourText, yourAt, 12, KeplerDrawing.You);
        AddPillTarget("you", yourText, yourAt, 12, KeplerDrawing.You);
        _targets.Add("you", you, 8);

        DrawScaleBar(dc, s);
    }

    /// <summary>A bar of 1 AU (or more when zoomed out) at the bottom left.</summary>
    private void DrawScaleBar(PaintContext dc, double scale)
    {
        var au = scale >= 60 ? 1.0 : scale >= 25 ? 2.0 : 5.0;
        var length = au * scale;
        var y = ActualHeight - 112 - _reserve.Current;
        const double left = 28;
        if (y < 140) return;
        dc.DrawLine(PeriodsPaint.Scale, new Point(left, y), new Point(left + length, y));
        dc.DrawLine(PeriodsPaint.Scale, new Point(left, y - 4), new Point(left, y + 4));
        dc.DrawLine(PeriodsPaint.Scale, new Point(left + length, y - 4), new Point(left + length, y + 4));
        var text = _labels.Text(LocFormat("Kepler_Label_ScaleAu", au), 11, KeplerDrawing.DimText);
        dc.DrawText(text, new Point(left, y - text.Height - 4));
        _targets.AddBox("scale", new Rect(new Point(left - 2, y - text.Height - 5), new Point(left + Math.Max(length, text.Width) + 2, y + 5)));
    }

    /// <summary>The planets' orbits in AU, worked out once for the start date (they barely change in a century).</summary>
    private void EnsurePaths(DateTime start)
    {
        if (_pathsFor == start && _paths.Count == PeriodPlanets.Length) return;
        _pathsFor = start;
        _paths.Clear();
        var jd = KeplerOrbit.JulianDay(start);
        foreach (var (id, _) in PeriodPlanets)
        {
            var state = Body(id).Orbit!.At(jd);
            _paths[id] = Enumerable.Range(0, 180).Select(i => state.PositionAt(i * 2 * Math.PI / 180)).ToArray();
        }
    }

    private static class PeriodsPaint
    {
        public static readonly IPen YourOrbit = MakePen(SpaceDrawing.Solid(WithAlpha(KeplerDrawing.You, 0.55)), 1.4, dashed: true);
        public static readonly IBrush YouGlow = SpaceDrawing.RadialGradient((WithAlpha(KeplerDrawing.You, 0.55), 0), (WithAlpha(KeplerDrawing.You, 0), 1));
        public static readonly IBrush SmallDot = Solid(200, 255, 255, 255);
        public static readonly IPen Scale = MakePen(Solid(170, 255, 255, 255), 1.2);
    }
}
