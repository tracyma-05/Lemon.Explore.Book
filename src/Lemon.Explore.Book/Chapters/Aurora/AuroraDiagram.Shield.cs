using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Aurora;

/// <summary>
/// Shield mode: the noon–midnight slice of the magnetosphere, the Sun off to the left. The magnetopause and bow shock are
/// sized by the solar wind (Shue et al. 1998); field lines are schematic. One interplanetary field line is followed round
/// the Dungey cycle: it arrives, joins Earth's field at the nose (southward field only), is swept over the poles into the
/// tail lobes, joins again at the tail's X-line and snaps back towards Earth, throwing electrons down into the auroral oval.
/// Lengths are in Earth radii with u pointing away from the Sun and z north.
/// </summary>
public sealed partial class AuroraDiagram
{
    private const int WindCount = 260;

    /// <summary>How far towards the Sun the view starts, Earth radii (with no field, much closer in).</summary>
    private double ViewSunward => NoField ? 8 : 24;

    /// <summary>The bow shock's flaring (a little short of a parabola).</summary>
    private const double ShockFlaring = 0.9;

    // Dungey cycle stages (fractions of the cycle): arrival, dayside opening and sweep over the poles, sinking to the tail
    // X-line, tail reconnection and snap back, fading as the closed line goes round to the day side.
    internal const double StageOpen = 0.16;
    internal const double StageSink = 0.50;
    internal const double StageSnap = 0.64;
    internal const double StageFade = 0.80;

    private readonly Random _windRandom = new(7);
    private readonly double[] _windU = new double[WindCount];
    private readonly double[] _windZ = new double[WindCount];
    private readonly bool[] _windHot = new bool[WindCount];
    private readonly double[] _windVu = new double[WindCount];
    private readonly double[] _windVz = new double[WindCount];
    private bool _windReady;
    private readonly List<(double U, double Z, double Age)> _sparks = [];
    private double _imfOffset;

    // The layout of the last frame: Earth's centre on screen, pixels per Earth radius, the view's reach.
    private Point _ec;
    private double _es = 12;
    private double _uRight = 40;
    private double _zTop = 20;

    private Point S(double u, double z) => new(_ec.X + (u * _es), _ec.Y - (z * _es));

    /// <summary>The magnetopause (or with no field, the edge of the air) and bow shock for the wind now.</summary>
    private (double Nose, double Flaring, double Shock, double TailX) ShieldShape()
    {
        if (NoField) return (1.05, 0.62, 1.55, 6);
        var pressure = AuroraModel.DynamicPressure(WindDensity, WindSpeed);
        var nose = AuroraModel.Standoff(pressure, Bz);
        var flaring = AuroraModel.Flaring(pressure, Bz);
        var shock = AuroraModel.BowShockStandoff(nose, AuroraModel.MachNumber(WindDensity, WindSpeed, Bz));
        return (nose, flaring, shock, Math.Max(15, 2.0 * nose));
    }

    /// <summary>
    /// The sunward front of a Shue-shaped surface at height <paramref name="z"/>: the u where it is crossed going down the
    /// wind (it flares out, so every height has one).
    /// </summary>
    private static double FrontU(double z, double nose, double flaring)
    {
        var target = Math.Abs(z);
        double lo = 0, hi = 179.5;
        for (var i = 0; i < 40; i++)
        {
            var mid = (lo + hi) / 2;
            var r = AuroraModel.ShapeRadius(mid, nose, flaring);
            if (r * Math.Sin(mid * Math.PI / 180) < target) lo = mid;
            else hi = mid;
        }

        var theta = (lo + hi) / 2;
        return -AuroraModel.ShapeRadius(theta, nose, flaring) * Math.Cos(theta * Math.PI / 180);
    }

    /// <summary>The half-width of a Shue-shaped surface at distance <paramref name="u"/> down the wind.</summary>
    private static double HalfWidth(double u, double nose, double flaring)
    {
        if (u <= -nose) return 0;
        double lo = 0, hi = 179.5;
        for (var i = 0; i < 40; i++)
        {
            var mid = (lo + hi) / 2;
            var r = AuroraModel.ShapeRadius(mid, nose, flaring);
            if (-r * Math.Cos(mid * Math.PI / 180) < u) lo = mid;
            else hi = mid;
        }

        var theta = (lo + hi) / 2;
        return AuroraModel.ShapeRadius(theta, nose, flaring) * Math.Sin(theta * Math.PI / 180);
    }

    /// <summary>The surface as one line from far south round the nose to far north, as far as the view reaches.</summary>
    private List<Point> SurfaceLine(double nose, double flaring)
    {
        var north = new List<Point>();
        for (var theta = 0.0; theta < 179; theta += 1.5)
        {
            var r = AuroraModel.ShapeRadius(theta, nose, flaring);
            var (s, c) = Math.SinCos(theta * Math.PI / 180);
            var u = -r * c;
            var z = r * s;
            north.Add(new Point(u, z));
            if (u > _uRight + 4) break;
        }

        var line = new List<Point>(north.Count * 2);
        for (var i = north.Count - 1; i > 0; i--) line.Add(S(north[i].X, -north[i].Y));
        foreach (var p in north) line.Add(S(p.X, p.Y));
        return line;
    }

    #region the solar wind

    private void ResetWind() => _windReady = false;

    private void SpawnWind(int i, bool anywhere)
    {
        _windU[i] = anywhere ? -ViewSunward + (_windRandom.NextDouble() * (ViewSunward + _uRight)) : -ViewSunward - (_windRandom.NextDouble() * 3);
        _windZ[i] = ((_windRandom.NextDouble() * 2) - 1) * (_zTop + 2);
        _windHot[i] = false;
    }

    /// <summary>
    /// Moves the solar wind on: the flow round a Rankine half-body the size of the magnetopause, slowed and heated behind
    /// the bow shock. With no field it flows round the air and some of it is caught there.
    /// </summary>
    private void StepWind(double dt)
    {
        if (!_windReady)
        {
            for (var i = 0; i < WindCount; i++) SpawnWind(i, anywhere: true);
            _windReady = true;
        }

        var (nose, flaring, shock, _) = ShieldShape();
        var body = NoField ? 1.05 : nose;
        var speed = 6 * Math.Clamp(WindSpeed, 200, 1000) / 400;
        _imfOffset += dt * speed * 0.5;
        for (var i = 0; i < WindCount; i++)
        {
            var u = _windU[i];
            var z = _windZ[i];
            var r2 = Math.Max(0.01, (u * u) + (z * z));
            var vu = speed * (1 + (body * u / r2));
            var vz = speed * body * z / r2;
            var r = Math.Sqrt(r2);
            var theta = Math.Acos(Math.Clamp(-u / r, -1, 1)) * 180 / Math.PI;
            if (r < AuroraModel.ShapeRadius(theta, shock, ShockFlaring))
            {
                _windHot[i] = true;
                var slow = 0.35 + (0.65 * Ramp(theta, 30, 140));
                vu *= slow;
                vz *= slow;
            }

            _windVu[i] = vu;
            _windVz[i] = vz;
            u += vu * dt;
            z += vz * dt;

            if (NoField)
            {
                if ((u * u) + (z * z) < 1.22 * 1.22)
                {
                    _sparks.Add((u, z, 0));
                    SpawnWind(i, anywhere: false);
                    continue;
                }
            }
            else
            {
                // Kept out of the magnetosphere: put back on its surface.
                var rr = Math.Sqrt((u * u) + (z * z));
                var th = Math.Acos(Math.Clamp(-u / Math.Max(1e-6, rr), -1, 1)) * 180 / Math.PI;
                var edge = AuroraModel.ShapeRadius(th, nose, flaring);
                if (rr < edge && th < 175)
                {
                    u = -edge * 1.002 * Math.Cos(th * Math.PI / 180);
                    z = edge * 1.002 * Math.Sin(th * Math.PI / 180) * Math.Sign(z == 0 ? 1 : z);
                }
            }

            _windU[i] = u;
            _windZ[i] = z;
            if (u > _uRight + 1 || Math.Abs(z) > _zTop + 3) SpawnWind(i, anywhere: false);
        }

        for (var i = _sparks.Count - 1; i >= 0; i--)
        {
            var (su, sz, age) = _sparks[i];
            age += dt;
            if (age > 0.8) _sparks.RemoveAt(i);
            else _sparks[i] = (su, sz, age);
        }

        if (_sparks.Count > 80) _sparks.RemoveRange(0, _sparks.Count - 80);
    }

    private void DrawWind(PaintContext dc)
    {
        if (!_windReady) return;
        var cool = new StreamGeometry();
        var hot = new StreamGeometry();
        using (var c = cool.Open())
        using (var h = hot.Open())
        {
            for (var i = 0; i < WindCount; i++)
            {
                var head = S(_windU[i], _windZ[i]);
                var tail = S(_windU[i] - (_windVu[i] * 0.05), _windZ[i] - (_windVz[i] * 0.05));
                var context = _windHot[i] ? h : c;
                context.BeginFigure(tail, false);
                context.LineTo(head);
                context.EndFigure(false);
            }
        }
        dc.DrawGeometry(null, FadedPen(Color.FromRgb(0xFF, 0xF0, 0xC0), 0.55, 1.6, flat: true), cool);
        dc.DrawGeometry(null, FadedPen(Ink.Orange, 0.7, 1.8, flat: true), hot);
    }

    #endregion

    private void DrawShieldMode(PaintContext dc)
    {
        var stage = Stage();
        _es = NoField
            ? Math.Clamp(Math.Min(stage.Width / 24, stage.Height / 14), 10, 90)
            : Math.Clamp(Math.Min(stage.Width / 70, stage.Height / 30), 4, 40);
        _ec = new Point(stage.Left + (ViewSunward * _es) + 8, stage.Top + (stage.Height / 2));
        _uRight = (stage.Right - _ec.X) / _es;
        _zTop = stage.Height / 2 / _es;
        var (nose, flaring, shock, tailX) = ShieldShape();
        var now = _clock.Elapsed.TotalSeconds;
        var kp = AuroraModel.EstimatedKp(WindDensity, WindSpeed, Bz);
        var southward = Bz < 0 && !NoField;

        dc.PushClip(new RectangleGeometry(new Rect(0, stage.Top - 6, ActualWidth, stage.Height + 12)));

        // The Sun, far off to the left (not to scale: it is 23 500 Earth radii away).
        var sunAt = new Point(stage.Left - (stage.Height * 0.32), _ec.Y);
        dc.DrawEllipse(Ink.SunGlow, null, sunAt, stage.Height * 0.62, stage.Height * 0.62);
        _targets.Add("sun", new Point(stage.Left + 14, _ec.Y), 18);

        // The sheath between the shock and the magnetopause, then the shock and the magnetopause.
        var shockLine = SurfaceLine(shock, ShockFlaring);
        var bodyLine = SurfaceLine(nose, flaring);
        if (!NoField)
        {
            var sheath = new List<Point>(shockLine);
            sheath.AddRange(Enumerable.Reverse(bodyLine));
            dc.DrawGeometry(FadedBrush(Ink.Orange, 0.07), null, Line(sheath, closed: true, filled: true));
            dc.DrawGeometry(FadedBrush(Ink.Blue, 0.06), null, Line(bodyLine, closed: true, filled: true));
        }

        DrawWind(dc);

        var shockGeometry = Line(shockLine);
        if (Glows("bowshock")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 8), shockGeometry);
        dc.DrawGeometry(null, FadedPen(Ink.Orange, 0.75, 1.6, dashed: true), shockGeometry);

        if (NoField)
        {
            DrawNoField(dc, now);
        }
        else
        {
            var bodyGeometry = Line(bodyLine);
            if (ShowBelts) DrawBeltsSide(dc);
            DrawPlasmaSheet(dc, nose, flaring, tailX);
            DrawStaticField(dc, nose, flaring, tailX);
            DrawImfBackground(dc, nose, flaring, shock);
            if (Glows("magnetopause")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 8), bodyGeometry);
            dc.DrawGeometry(null, FadedPen(Ink.Cyan, 0.85, 2), bodyGeometry);
            DrawDungey(dc, nose, flaring, shock, tailX, southward, now);
        }

        // Earth, lit from the left, and the aurora round its poles.
        DrawEarthSide(dc, _ec, _es, sunAt);
        _targets.Add("earth", _ec, Math.Max(10, _es));
        if (!NoField) DrawOvalGlow(dc, kp, southward, now);

        DrawShieldLabels(dc, nose, flaring, shock, tailX);
        if (!NoField) DrawCycleSteps(dc, stage, southward);
        dc.Pop();
    }

    #region the field

    /// <summary>Schematic field lines: closed loops on the day and night sides, and the open lines of the tail lobes.</summary>
    private void DrawStaticField(PaintContext dc, double nose, double flaring, double tailX)
    {
        var closed = new StreamGeometry();
        var open = new StreamGeometry();
        using (var c = closed.Open())
        using (var o = open.Open())
        {
            // Day side: feet from 42° to 77°, crossing the equator out to 0.9 of the nose.
            var dayMax = 0.9 * nose;
            for (var i = 0; i < 6; i++)
            {
                var t = i / 5.0;
                var shell = 1.8 + ((dayMax - 1.8) * Math.Pow(t, 1.3));
                var foot = 42 + ((77 - 42) * Math.Pow(t, 0.8));
                AddFigure(c, DayLine(shell, foot));
            }

            // Night side: feet from 42° to 70°, the outer ones stretched into the tail out to the X-line.
            for (var i = 0; i < 6; i++)
            {
                var t = i / 5.0;
                var foot = 42 + ((70 - 42) * Math.Pow(t, 0.8));
                AddFigure(c, NightLine(foot, NightCrossing(foot, tailX)));
            }

            // The lobes: open lines from the polar cap into the tail, north and south.
            foreach (var phi in new[] { 80.0, 87, 94, 100, 106 })
            {
                foreach (var sign in new[] { 1, -1 })
                {
                    AddFigure(o, LobeLine(phi, sign, nose, flaring));
                }
            }
        }
        dc.DrawGeometry(null, FadedPen(Ink.Field, 0.5, 1.2), closed);
        dc.DrawGeometry(null, FadedPen(Ink.Field, 0.32, 1.1), open);

        // Earth's field points north across the equator (out of the south, into the north).
        foreach (var u in new[] { -0.55 * nose, 0.55 * nose })
        {
            var at = S(u, 0);
            DrawArrowHead(dc, FadedBrush(Ink.Field, 0.9), at, new Vector(0, -1), 8);
        }
    }

    private static void AddFigure(StreamGeometryContext context, IReadOnlyList<Point> points)
    {
        if (points.Count < 2) return;
        context.BeginFigure(points[0], false);
        for (var i = 1; i < points.Count; i++) context.LineTo(points[i]);
        context.EndFigure(false);
    }

    /// <summary>
    /// A day-side closed line from foot latitude <paramref name="footDegrees"/> (both hemispheres), squeezed so it crosses the
    /// equator at <paramref name="shell"/>: the dipole's shape, pressed in by the wind.
    /// </summary>
    private List<Point> DayLine(double shell, double footDegrees)
    {
        var foot = footDegrees * Math.PI / 180;
        var dipole = 1 / Math.Pow(Math.Cos(foot), 2);
        var k = shell / dipole;
        var sinFoot = Math.Sin(foot);
        var points = new List<Point>(48);
        for (var i = 0; i <= 46; i++)
        {
            var lambda = -foot + (2 * foot * i / 46);
            var (s, c) = Math.SinCos(lambda);
            var w = k + ((1 - k) * Math.Pow(s / sinFoot, 2));
            var r = dipole * c * c * w;
            points.Add(S(-r * c, r * s));
        }

        return points;
    }

    /// <summary>How far down the tail the night-side line from a foot latitude crosses the equator.</summary>
    private static double NightCrossing(double footDegrees, double tailX)
    {
        var dipole = 1 / Math.Pow(Math.Cos(footDegrees * Math.PI / 180), 2);
        var outer = 1 / Math.Pow(Math.Cos(70 * Math.PI / 180), 2);
        var t = Math.Clamp((dipole - 3) / (outer - 3), 0, 1);
        return dipole + ((tailX - outer) * t * t);
    }

    /// <summary>
    /// A night-side closed line from foot latitude <paramref name="footDegrees"/>, stretched down the tail so it crosses the
    /// equator <paramref name="crossing"/> away (strongest near the equator, where the tail's current sheet pulls it out).
    /// </summary>
    private List<Point> NightLine(double footDegrees, double crossing)
    {
        var foot = footDegrees * Math.PI / 180;
        var dipole = 1 / Math.Pow(Math.Cos(foot), 2);
        var stretch = (crossing / dipole) - 1;
        var points = new List<Point>(64);
        for (var i = 0; i <= 62; i++)
        {
            var lambda = -foot + (2 * foot * i / 62);
            var (s, c) = Math.SinCos(lambda);
            var r = dipole * c * c;
            var factor = 1 + (stretch * Math.Pow(c, 6));
            points.Add(S(r * c * factor, r * s));
        }

        return points;
    }

    /// <summary>
    /// An open line of a tail lobe from the foot at <paramref name="phi"/> degrees round from the sunward equator (90 is the
    /// pole): out over the polar cap, then straight down the tail inside the magnetopause.
    /// </summary>
    private List<Point> LobeLine(double phi, int sign, double nose, double flaring)
    {
        var p = phi * Math.PI / 180;
        var foot = new Point(-Math.Cos(p), Math.Sin(p));
        var far = _uRight + 3;
        var farWidth = HalfWidth(far, nose, flaring);
        // Lines from nearer the cusp lie nearer the magnetopause; those from nearer the oval, nearer the plasma sheet.
        var t = Math.Clamp((106 - phi) / 26, 0, 1);
        var height = 3 + ((0.86 * farWidth - 3) * t);
        var p1 = new Point(foot.X * 3.2, foot.Y * 3.2);
        var p2 = new Point(9, Math.Min(height, 0.8 * HalfWidth(9, nose, flaring)));
        var p3 = new Point(far, height);
        var points = new List<Point>(40);
        for (var i = 0; i <= 38; i++)
        {
            var b = Bezier(foot, p1, p2, p3, i / 38.0);
            var limit = b.X > -nose * 0.5 ? 0.94 * HalfWidth(b.X, nose, flaring) : double.MaxValue;
            points.Add(S(b.X, sign * Math.Min(b.Y, limit)));
        }

        return points;
    }

    /// <summary>The Van Allen belts cut through: the inner (shells 1.3–2.2) and outer (3.2–6) on both sides.</summary>
    private void DrawBeltsSide(PaintContext dc)
    {
        foreach (var (inner, outer, alpha) in new[] { (1.3, 2.2, 0.2), (3.2, 6.0, 0.15) })
        {
            foreach (var side in new[] { -1, 1 })
            {
                var points = new List<Point>();
                var limit = Math.Min(48, AuroraModel.FootLatitude(inner)) * Math.PI / 180;
                for (var i = 0; i <= 30; i++)
                {
                    var lambda = -limit + (2 * limit * i / 30);
                    var (s, c) = Math.SinCos(lambda);
                    var r = outer * c * c;
                    points.Add(S(side * r * c, r * s));
                }

                for (var i = 30; i >= 0; i--)
                {
                    var lambda = -limit + (2 * limit * i / 30);
                    var (s, c) = Math.SinCos(lambda);
                    var r = inner * c * c;
                    points.Add(S(side * r * c, r * s));
                }

                dc.DrawGeometry(FadedBrush(Ink.Belt, alpha), null, Line(points, closed: true, filled: true));
            }
        }

        _targets.Add("belts", S(-4.5, 0), Math.Max(8, _es * 1.2));
    }

    /// <summary>The plasma sheet: a band of hot plasma along the middle of the tail, glowing.</summary>
    private void DrawPlasmaSheet(PaintContext dc, double nose, double flaring, double tailX)
    {
        var top = new List<Point>();
        var bottom = new List<Point>();
        for (var u = 4.0; u <= _uRight + 3; u += 0.5)
        {
            var half = Math.Min(Math.Min(0.15 + (0.35 * (u - 4)), 1.4 + (0.07 * (u - 6))), 0.4 * HalfWidth(u, nose, flaring));
            top.Add(S(u, half));
            bottom.Add(S(u, -half));
        }

        bottom.Reverse();
        top.AddRange(bottom);
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, _ec.Y - (4 * _es), RelativeUnit.Absolute),
            EndPoint = new RelativePoint(0, _ec.Y + (4 * _es), RelativeUnit.Absolute),
            GradientStops =
            [
                new GradientStop(WithAlpha(Ink.Orange, 0), 0),
                new GradientStop(WithAlpha(Ink.Orange, 0.32), 0.5),
                new GradientStop(WithAlpha(Ink.Orange, 0), 1),
            ],
        };
        dc.DrawGeometry(brush, null, Line(top, closed: true, filled: true));
        _targets.Add("plasmasheet", S(Math.Min(_uRight - 4, tailX + 8), 0), 12);
    }

    /// <summary>
    /// Field lines of the solar wind coming in, until they meet the bow shock; southward (pointing down) or northward. The
    /// line the cycle follows is drawn on its own.
    /// </summary>
    private void DrawImfBackground(PaintContext dc, double nose, double flaring, double shock)
    {
        var strength = Math.Clamp(Math.Abs(Bz) / 10, 0, 1);
        if (strength < 0.05) return;
        var geometry = new StreamGeometry();
        var spacing = 5.5;
        var heads = new List<Point>();
        using (var context = geometry.Open())
        {
            var first = (_imfOffset % spacing) - ViewSunward - spacing;
            for (var u = first; u < -shock; u += spacing)
            {
                if (u < -ViewSunward - 1) continue;
                var started = false;
                for (var z = -_zTop - 1; z <= _zTop + 1; z += 0.5)
                {
                    var inside = u > FrontU(z, shock, ShockFlaring) - 0.2;
                    if (inside)
                    {
                        started = false;
                        continue;
                    }

                    var p = S(u, z);
                    if (!started) context.BeginFigure(p, false);
                    else context.LineTo(p);
                    started = true;
                }

                heads.Add(S(u, _zTop * (Bz < 0 ? 0.6 : -0.6)));
            }

            context.EndFigure(false);
        }
        dc.DrawGeometry(null, FadedPen(Ink.Imf, 0.25 + (0.25 * strength), 1.1), geometry);
        var brush = FadedBrush(Ink.Imf, 0.7);
        foreach (var head in heads) DrawArrowHead(dc, brush, head, new Vector(0, Bz < 0 ? 1 : -1), 7);
        if (heads.Count > 0) _targets.Add("imf", heads[0], 10);
    }

    #endregion

    #region the Dungey cycle

    /// <summary>
    /// The followed line. Southward field: it arrives, is cut and rejoined at the nose to Earth's field, swept over the poles
    /// with the wind into the lobes, sinks to the tail's X-line, is rejoined there into a closed line that snaps back towards
    /// Earth (electrons racing down into the oval) and fades as it goes round to the day side. Northward: it drapes over the
    /// nose and slides past.
    /// </summary>
    private void DrawDungey(PaintContext dc, double nose, double flaring, double shock, double tailX, bool southward, double now)
    {
        var s = _phase;
        var top = _zTop + 2;
        var arrivalEnd = -nose + 4;
        var color = Ink.Imf;
        var glow = Glows("openline") || Glows("closedline") || Glows("imf");

        if (s < StageOpen || !southward)
        {
            // Arrival (and for northward field, draping and sliding past).
            var span = southward ? StageOpen : 0.62;
            var u = -ViewSunward - 1 + ((arrivalEnd - (-ViewSunward - 1) + (southward ? 0 : 30)) * Math.Min(1, s / span));
            var alpha = southward ? 1 : 1 - Ramp(s, 0.5, 0.62);
            if (alpha <= 0) return;
            var points = new List<Point>();
            for (var z = -top; z <= top; z += 0.35)
            {
                var x = u;
                var shockFront = FrontU(z, shock, ShockFlaring);
                if (x > shockFront) x = shockFront + ((x - shockFront) * 0.55);
                x = Math.Min(x, FrontU(z, nose, flaring) - 0.15);
                points.Add(S(x, z));
            }

            var geometry = Line(points);
            if (glow) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 8), geometry);
            dc.DrawGeometry(null, FadedPen(color, 0.95 * alpha, 2.4), geometry);
            var middle = points[points.Count / 2];
            DrawArrowHead(dc, FadedBrush(color, alpha), points[(int)(points.Count * (southward ? 0.8 : 0.2))], new Vector(0, southward ? 1 : -1), 9);
            _targets.Add("imf", middle, 12);
            return;
        }

        var footStart = 77.0;
        var footEnd = 108.0;
        if (s < StageSnap)
        {
            // Opened at the nose: two open lines, Earth's end over the pole, the wind's end swept down the tail. Where the
            // line crosses the magnetopause runs from the nose down the tail (kept inside the view), and then sinks to the
            // tail's middle.
            var b = Ramp(s, StageOpen, StageSink);
            var c = Ramp(s, StageSink, StageSnap);
            var kinkU = -nose + ((tailX + 4 + nose) * Smooth(b));
            var kink = new Point(kinkU, Math.Min(0.96 * HalfWidth(kinkU, nose, flaring), 0.84 * _zTop));
            var windU = arrivalEnd + ((tailX + 24 - arrivalEnd) * ((s - StageOpen) / (StageSnap - StageOpen)));
            var phi = footStart + ((footEnd - footStart) * Smooth((s - StageOpen) / (StageSnap - StageOpen)));
            var xPoint = new Point(tailX, 0.05);
            var end = Mix(kink, xPoint, Smooth(c));

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                foreach (var sign in new[] { 1, -1 })
                {
                    var p = phi * Math.PI / 180;
                    var foot = new Point(-Math.Cos(p), Math.Sin(p));
                    var reach = 2.4 + (1.6 * b);
                    var p1 = new Point(foot.X * reach, foot.Y * reach);
                    var p2 = Mix(new Point(Math.Min(kink.X, 0) * 0.5 + (Math.Max(kink.X, 0) * 0.35), kink.Y), new Point(tailX * 0.55, 1.4), Smooth(c));
                    var earthPart = new List<Point>();
                    for (var i = 0; i <= 44; i++)
                    {
                        var q = Bezier(foot, p1, p2, end, i / 44.0);
                        earthPart.Add(S(q.X, sign * q.Y));
                    }

                    // The wind's end: out through the magnetopause and up to the edge, carried along with the wind.
                    var outward = new Point(end.X + 3 + (3 * c), end.Y + 3 - (1.5 * c));
                    var windTop = new Point(windU, top);
                    var windPart = new List<Point>();
                    for (var i = 1; i <= 30; i++)
                    {
                        var q = Bezier(end, outward, new Point(windU, top - 5), windTop, i / 30.0);
                        windPart.Add(S(q.X, sign * q.Y));
                    }

                    AddFigure(context, earthPart.Concat(windPart).ToList());
                }
            }
            if (glow) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 8), geometry);
            dc.DrawGeometry(null, FadedPen(color, 0.95, 2.4), geometry);
            var along = Bezier(new Point(0, 1), new Point(0, 3), end, end, 0.5);
            _targets.Add("openline", S(along.X, along.Y), 12);

            // The X where the lines were cut and rejoined at the nose.
            if (b < 0.2) DrawReconnection(dc, S(-nose + 0.1, 0), 1 - (b / 0.2), "xday");
            return;
        }

        // Rejoined in the tail: a closed line snapping back, and the cut-off piece of the wind's line flowing away.
        var d = Ramp(s, StageSnap, StageFade);
        var e = Ramp(s, StageFade, 1);
        var crossing = tailX + ((7.5 - tailX) * Smooth(Ramp(d, 0.1, 0.9)));
        var footLatitude = 180 - footEnd - (2 * Smooth(d));
        var closedLine = NightLine(footLatitude, crossing);
        var closedGeometry = Line(closedLine);
        var lineAlpha = 1 - e;
        if (lineAlpha > 0.02)
        {
            if (glow) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 8), closedGeometry);
            dc.DrawGeometry(null, FadedPen(color, 0.95 * lineAlpha, 2.4), closedGeometry);
            _targets.Add("closedline", S(crossing * 0.92, 0), 12);
        }

        // The piece that never touched Earth: two arms meeting at a point carried down the tail.
        var away = new Point(tailX + (32 * d), 0);
        var awayWind = tailX + 24 + (30 * d) + (20 * e);
        if (away.X < _uRight + 4)
        {
            var pieces = new StreamGeometry();
            using (var context = pieces.Open())
            {
                foreach (var sign in new[] { 1, -1 })
                {
                    var arm = new List<Point>();
                    for (var i = 0; i <= 24; i++)
                    {
                        var q = Bezier(away, new Point(away.X + 4, 4), new Point(awayWind, top - 5), new Point(awayWind, top), i / 24.0);
                        arm.Add(S(q.X, sign * q.Y));
                    }

                    AddFigure(context, arm);
                }
            }
            dc.DrawGeometry(null, FadedPen(color, 0.55 * (1 - d), 2), pieces);
        }

        if (d < 0.35) DrawReconnection(dc, S(tailX, 0), 1 - (d / 0.35), "xtail");

        // Electrons racing down the snapping line to both feet.
        var beam = Ramp(d, 0.2, 1) * (1 - Ramp(e, 0, 0.6));
        if (beam > 0.01)
        {
            var brush = FadedBrush(Ink.Green, 0.9 * beam);
            var count = closedLine.Count;
            for (var k = 0; k < 8; k++)
            {
                var f = ((now * 0.9) + (k / 8.0)) % 1;
                var half = (int)((count / 2) * (1 - f));
                foreach (var index in new[] { (count / 2) - half, (count / 2) + half })
                {
                    if (index >= 0 && index < count) dc.DrawEllipse(brush, null, closedLine[index], 2.4, 2.4);
                }
            }
        }
    }

    /// <summary>The angle round the magnetopause from the nose at which it is <paramref name="u"/> down the wind.</summary>
    private static double KinkAngle(double nose, double flaring, double u)
    {
        double lo = 0, hi = 179;
        for (var i = 0; i < 40; i++)
        {
            var mid = (lo + hi) / 2;
            if (-AuroraModel.ShapeRadius(mid, nose, flaring) * Math.Cos(mid * Math.PI / 180) < u) lo = mid;
            else hi = mid;
        }

        return (lo + hi) / 2;
    }

    /// <summary>A flash and an X where field lines are cut and rejoined.</summary>
    private void DrawReconnection(PaintContext dc, Point at, double strength, string id)
    {
        var r = 10 + (16 * strength);
        dc.DrawEllipse(Glow(Ink.Gold, 0.9 * strength), null, at, r * 1.6, r * 1.6);
        var pen = FadedPen(Ink.Gold, 0.5 + (0.5 * strength), 2.2);
        dc.DrawLine(pen, at + new Vector(-7, -7), at + new Vector(7, 7));
        dc.DrawLine(pen, at + new Vector(-7, 7), at + new Vector(7, -7));
        _targets.Add(id, at, 14);
    }

    /// <summary>
    /// The aurora round both poles: the night side of the oval (and dimly the cusp on the day side), brighter with the
    /// wind's push and flaring when the cycle's electrons arrive.
    /// </summary>
    private void DrawOvalGlow(PaintContext dc, double kp, bool southward, double now)
    {
        var flash = southward ? Math.Sin(Math.PI * Ramp(_phase, StageSnap + 0.05, 0.95)) : 0;
        var steady = 0.12 + (0.6 * kp / 9);
        var night = Math.Clamp(steady + (0.7 * flash), 0, 1);
        var shimmer = 0.85 + (0.15 * Math.Sin(now * 5.3));
        foreach (var sign in new[] { 1, -1 })
        {
            var p = 108 * Math.PI / 180;
            var at = S(-Math.Cos(p) * 1.04, sign * Math.Sin(p) * 1.04);
            var r = _es * (0.45 + (0.35 * night));
            dc.DrawEllipse(Glow(Ink.Green, night * shimmer), null, at, r, r);
            var q = 77 * Math.PI / 180;
            var cusp = S(-Math.Cos(q) * 1.04, sign * Math.Sin(q) * 1.04);
            dc.DrawEllipse(Glow(Ink.Red, 0.5 * steady), null, cusp, _es * 0.4, _es * 0.4);
            if (sign > 0)
            {
                _targets.Add("aurora", at, Math.Max(8, r));
                _targets.Add("cusp", S(-Math.Cos(q) * 2.6, Math.Sin(q) * 2.6), 10);
            }
        }
    }

    #endregion

    #region no field

    private readonly double[] _ionU = new double[90];
    private readonly double[] _ionZ = new double[90];
    private readonly double[] _ionAge = new double[90];
    private double _ionTime;

    /// <summary>
    /// With no magnetic field: the wind reaches the air (drawn far thicker than it is), a little is caught there, and ions
    /// picked up from the air on the day side and flanks are carried off down the wind in a plume.
    /// </summary>
    private void DrawNoField(PaintContext dc, double now)
    {
        dc.DrawEllipse(Glow(Ink.Blue, 0.75), null, _ec, _es * 1.55, _es * 1.55);

        var dt = Math.Clamp(now - _ionTime, 0, 0.05);
        _ionTime = now;
        var speed = Math.Clamp(WindSpeed, 200, 1000) / 400;
        for (var i = 0; i < _ionU.Length; i++)
        {
            _ionAge[i] += dt;
            var first = _ionU[i] == 0 && _ionZ[i] == 0;
            if (_ionAge[i] > 4 || first)
            {
                // Picked up somewhere over the day side or the flanks; at the start, already some way along.
                var angle = (35 + (_windRandom.NextDouble() * 110)) * Math.PI / 180;
                var side = _windRandom.NextDouble() < 0.5 ? -1 : 1;
                _ionAge[i] = first ? _windRandom.NextDouble() * 4 : 0;
                _ionU[i] = (-1.22 * Math.Cos(angle)) + (speed * ((0.6 * _ionAge[i]) + (0.125 * _ionAge[i] * _ionAge[i])));
                _ionZ[i] = side * ((1.22 * Math.Sin(angle)) + (0.12 * _ionAge[i]));
            }

            _ionU[i] += dt * speed * (0.6 + (0.25 * _ionAge[i]));
            _ionZ[i] += dt * Math.Sign(_ionZ[i]) * 0.12;
            dc.DrawEllipse(FadedBrush(Ink.Cyan, 0.9 * (1 - (_ionAge[i] / 4))), null, S(_ionU[i], _ionZ[i]), 2, 2);
        }

        foreach (var (u, z, age) in _sparks)
        {
            dc.DrawEllipse(Glow(Ink.Gold, 1 - (age / 0.8)), null, S(u, z), 8, 8);
        }

        _targets.Add("stripped", S(3.2, 0.8), 14);
    }

    #endregion

    #region labels

    /// <summary>A pill with the thing's name (also a target), or with labels off just the target.</summary>
    private void Label(PaintContext dc, string key, Point at, Color color, string id)
    {
        if (ShowLabels) DrawTargetPill(dc, Loc(key), at, color, id);
        else _targets.Add(id, at, 10);
    }

    private void DrawShieldLabels(PaintContext dc, double nose, double flaring, double shock, double tailX)
    {
        Label(dc, "Aurora_Label_Sun", new Point(Stage().Left + 40, _ec.Y - (_es * 3)), Ink.Sun, "sun");
        Label(dc, "Aurora_Label_Wind", S(-ViewSunward + 6, _zTop * 0.72), Ink.Text, "wind");
        var shockAt = 58 * Math.PI / 180;
        var rs = AuroraModel.ShapeRadius(58, shock, ShockFlaring);
        Label(dc, "Aurora_Label_BowShock", S(-rs * Math.Cos(shockAt), rs * Math.Sin(shockAt)), Ink.Orange, "bowshock");
        if (NoField)
        {
            Label(dc, "Aurora_Label_Stripped", S(3.6, -2.2), Ink.Cyan, "stripped");
            return;
        }

        var sheathAt = 35 * Math.PI / 180;
        var rm = (AuroraModel.ShapeRadius(35, nose, flaring) + AuroraModel.ShapeRadius(35, shock, ShockFlaring)) / 2;
        Label(dc, "Aurora_Label_Sheath", S(-rm * Math.Cos(sheathAt), -rm * Math.Sin(sheathAt)), Ink.Orange, "sheath");
        var mpAt = 75 * Math.PI / 180;
        var rp = AuroraModel.ShapeRadius(75, nose, flaring);
        Label(dc, "Aurora_Label_Magnetopause", S(-rp * Math.Cos(mpAt), -rp * Math.Sin(mpAt)), Ink.Cyan, "magnetopause");
        var lobeU = Math.Min(_uRight - 8, tailX + 4);
        Label(dc, "Aurora_Label_Lobe", S(lobeU, 0.55 * HalfWidth(lobeU, nose, flaring)), Ink.Field, "lobe");
        Label(dc, "Aurora_Label_Tail", S(Math.Min(_uRight - 6, 0.45 * (_uRight + tailX)), -0.62 * HalfWidth(lobeU, nose, flaring)), Ink.Field, "tail");
        Label(dc, "Aurora_Label_PlasmaSheet", S(Math.Min(_uRight - 5, tailX + 9), -2.4), Ink.Orange, "plasmasheet");
        if (ShowBelts) Label(dc, "Aurora_Label_Belts", S(-4.6, -3.6), Ink.Belt, "belts");
        Label(dc, "Aurora_Label_Cusp", S(-0.9, 4.2), Ink.Red, "cusp");
        Label(dc, "Aurora_Label_Aurora", S(1.9, 2.0), Ink.Green, "aurora");
    }

    /// <summary>The cycle's steps along the bottom, the one now lit (southward field), or a note that the shield stays shut.</summary>
    private void DrawCycleSteps(PaintContext dc, Rect stage, bool southward)
    {
        var y = stage.Bottom - 14;
        if (!southward)
        {
            DrawPill(dc, Loc(Bz > 0 ? "Aurora_Cycle_North" : "Aurora_Cycle_Zero"), new Point(stage.Left + (stage.Width / 2), y), Ink.Cyan, 12);
            return;
        }

        var keys = new[] { "Aurora_Cycle_Arrive", "Aurora_Cycle_Open", "Aurora_Cycle_Sink", "Aurora_Cycle_Snap", "Aurora_Cycle_Return" };
        var bounds = new[] { 0, StageOpen, StageSink, StageSnap, StageFade, 1 };
        var texts = keys.Select(k => _labels.Text(Loc(k), 11.5, Ink.Text)).ToList();
        var total = texts.Sum(t => t.Width + 12) + ((keys.Length - 1) * 18);
        var x = stage.Left + Math.Max(8, (stage.Width - total) / 2);
        for (var i = 0; i < keys.Length; i++)
        {
            var on = _phase >= bounds[i] && _phase < bounds[i + 1];
            var width = texts[i].Width + 12;
            DrawPill(dc, Loc(keys[i]), new Point(x + (width / 2), y), on ? Ink.Gold : Ink.Dim, 11.5);
            if (i < keys.Length - 1) DrawText(dc, "›", new Point(x + width + 5, y), Ink.Dim, 13);
            x += width + 18;
        }
    }

    #endregion
}
