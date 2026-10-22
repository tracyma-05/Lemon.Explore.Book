using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Typhoon;

/// <summary>
/// Storm mode: a typhoon from above, 1200 km across. Its cloud turns slowly; particles show the near-surface wind
/// spiralling in (anticlockwise in the north, clockwise in the south, straight in with the Earth's turn switched off),
/// coloured by grade. Isobars every 10 hPa; a probe gives the wind, its force and the pressure at any point, with the
/// pressure push and the wind drawn as arrows.
/// </summary>
public sealed partial class TyphoonDiagram
{
    /// <summary>Seconds of the storm's time per second of playback at 1× (15 minutes).</summary>
    private const double StormTimeScale = 900;

    /// <summary>How fast the cloud picture turns, degrees per second of playback.</summary>
    private const double CloudTurn = 5;

    /// <summary>The storm's forward motion when moving: 20 km/h towards the north-west, m/s east and north.</summary>
    private static readonly Vector Motion = new(-3.93, 3.93);

    /// <summary>The latitude the storm is worked out for, degrees (mirrored south of the equator).</summary>
    private const double StormLatitude = 20;

    private readonly List<WindParticle> _particles = [];

    private sealed class WindParticle
    {
        public double X;
        public double Y;
        public double Age;
        public double Life;
    }

    private readonly record struct StormView(Point Center, double Radius)
    {
        public double PxPerKm => Radius / StormCloud.ViewRadius;

        public Point Screen(double x, double y) => new(Center.X + (x * PxPerKm), Center.Y - (y * PxPerKm));
    }

    private StormView StormLayout()
    {
        var stage = Stage();
        var radius = Math.Max(80, (Math.Min(stage.Width - 40, stage.Height) / 2) - 6);
        return new StormView(new Point(stage.Left + (stage.Width / 2), stage.Top + (stage.Height / 2)), radius);
    }

    /// <summary>The storm's own wind at a point (km east and north of the centre), m/s east and north.</summary>
    private Vector StormWind(double x, double y)
    {
        var r = Math.Sqrt((x * x) + (y * y));
        if (r < 0.5) return default;
        var outward = new Vector(x / r, y / r);
        var inward = -outward;
        if (!Spin)
        {
            // No Earth's turn, no swirl: the pressure push drives the air straight in.
            return inward * Math.Min(25, 0.6 * StormModel.Wind(r, CentralPressure, StormLatitude));
        }

        var speed = StormModel.Wind(r, CentralPressure, StormLatitude);
        var tangent = Southern ? new Vector(outward.Y, -outward.X) : new Vector(-outward.Y, outward.X);
        var inflow = StormModel.Inflow(r, CentralPressure, StormLatitude) * Math.PI / 180;
        return ((tangent * Math.Cos(inflow)) + (inward * Math.Sin(inflow))) * speed;
    }

    /// <summary>The wind a person standing there feels: the storm's own plus its forward motion.</summary>
    private Vector GroundWind(double x, double y) => StormWind(x, y) + (Moving ? Motion : default);

    private void StepStorm(double step)
    {
        var eye = StormModel.EyeRadius(CentralPressure, StormLatitude);
        while (_particles.Count < 650) _particles.Add(NewParticle(anywhere: true));
        if (step <= 0) return;
        foreach (var p in _particles)
        {
            var w = StormWind(p.X, p.Y);
            p.X += w.X * StormTimeScale * step / 1000;
            p.Y += w.Y * StormTimeScale * step / 1000;
            p.Age += step;
            var r = Math.Sqrt((p.X * p.X) + (p.Y * p.Y));
            if (p.Age > p.Life || r < (Spin ? eye * 0.8 : 12) || r > StormCloud.ViewRadius)
            {
                var fresh = NewParticle(anywhere: false);
                (p.X, p.Y, p.Age, p.Life) = (fresh.X, fresh.Y, fresh.Age, fresh.Life);
            }
        }
    }

    private WindParticle NewParticle(bool anywhere)
    {
        var life = 2.5 + (2.5 * _random.NextDouble());
        var share = anywhere ? _random.NextDouble() : 0.15 + (0.85 * _random.NextDouble());
        var r = StormCloud.ViewRadius * Math.Sqrt(share) * 0.97;
        var a = _random.NextDouble() * Math.PI * 2;
        return new WindParticle { X = r * Math.Cos(a), Y = r * Math.Sin(a), Life = life, Age = anywhere ? _random.NextDouble() * life : 0 };
    }

    private void MoveProbe(Point screen)
    {
        var view = StormLayout();
        var x = (screen.X - view.Center.X) / view.PxPerKm;
        var y = -(screen.Y - view.Center.Y) / view.PxPerKm;
        var r = Math.Sqrt((x * x) + (y * y));
        if (r > StormCloud.ViewRadius)
        {
            x *= StormCloud.ViewRadius / r;
            y *= StormCloud.ViewRadius / r;
        }

        SetCurrentValue(ProbeXProperty, Math.Round(x, 1));
        SetCurrentValue(ProbeYProperty, Math.Round(y, 1));
    }

    private void DrawStormMode(PaintContext dc, double width, double height)
    {
        var view = StormLayout();
        var c = view.Center;
        var R = view.Radius;

        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        _stars.Draw(dc, width, height, RenderScale(), _clock.Elapsed.TotalSeconds, 0.3);

        // The sea, the cloud turning over it, and the edge of the view fading out.
        dc.DrawEllipse(StormInk.Sea, null, c, R, R);
        var cloud = _cloud.Get(CentralPressure, Southern, Spin);
        var angle = Spin ? (Southern ? 1 : -1) * CloudTurn * _t : 0;
        dc.PushTransform(SpaceDrawing.Rotation(angle, c.X, c.Y));
        dc.DrawImage(cloud, new Rect(c.X - R, c.Y - R, 2 * R, 2 * R));
        dc.Pop();

        if (ShowIsobars) DrawIsobars(dc, view);
        DrawWindParticles(dc, view);
        dc.DrawEllipse(StormInk.Vignette, null, c, R + 2, R + 2);
        dc.DrawEllipse(null, Ink.Frame, c, R, R);

        DrawStormLabels(dc, view);
        DrawProbe(dc, view);
        DrawScaleBar(dc, view);
    }

    private void DrawIsobars(PaintContext dc, StormView view)
    {
        var levels = new List<double> { 1005 };
        for (var p = 1000.0; p > CentralPressure + 2; p -= 10) levels.Add(p);
        foreach (var level in levels)
        {
            var r = StormModel.RadiusOfPressure(level, CentralPressure, StormLatitude);
            if (double.IsInfinity(r) || r > StormCloud.ViewRadius * 0.98 || r * view.PxPerKm < 6) continue;
            if (Glows("isobars")) dc.DrawEllipse(null, Glow, view.Center, r * view.PxPerKm, r * view.PxPerKm);
            dc.DrawEllipse(null, StormInk.Isobar, view.Center, r * view.PxPerKm, r * view.PxPerKm);
            if (ShowLabels && r * view.PxPerKm > 30)
            {
                var at = view.Center + (new Vector(0.66, -0.75) * r * view.PxPerKm);
                AddBoxTarget("isobars", DrawText(dc, Number(level, "0"), at, Ink.Cyan, 9.5, center: true));
            }
        }
    }

    /// <summary>Each particle's tail: this many pieces, each this many seconds of its motion back.</summary>
    private const int TailPieces = 4;

    private const double TailSeconds = 0.3;

    private readonly LineBatch _shadow = new();

    private void DrawWindParticles(PaintContext dc, StormView view)
    {
        foreach (var p in _particles)
        {
            var fade = Math.Min(1, Math.Min(p.Age / 0.4, (p.Life - p.Age) / 0.5));
            if (fade <= 0.05) continue;
            var points = new Point[TailPieces + 1];
            double x = p.X, y = p.Y;
            points[0] = view.Screen(x, y);
            for (var k = 1; k <= TailPieces; k++)
            {
                var w = StormWind(x, y);
                x -= w.X * StormTimeScale * TailSeconds / 1000;
                y -= w.Y * StormTimeScale * TailSeconds / 1000;
                points[k] = view.Screen(x, y);
            }

            // Below gale force the wind is drawn pale blue; above it in its grade's colour.
            var speed = GroundWind(p.X, p.Y).Length;
            var color = speed < 10.8 ? StormInk.Breeze : Quantize(WindColor(speed));
            _shadow.Add(StormInk.ShadowColor, 0.45 * fade, points);
            _batch.Add(color, 0.95 * fade, points);
        }

        _shadow.Draw(dc, this, 3.4);
        _batch.Draw(dc, this, 1.6);
    }

    private void DrawStormLabels(PaintContext dc, StormView view)
    {
        var c = view.Center;
        var rmax = StormModel.RadiusOfMaxWind(CentralPressure, StormLatitude);
        var wind = StormModel.MaxWind(CentralPressure);

        // The low's centre, and the storm's motion.
        if (Moving)
        {
            var direction = new Vector(Motion.X, -Motion.Y);
            direction /= direction.Length;
            DrawArrow(dc, c + (direction * 8), c + (direction * Math.Min(120, view.Radius * 0.35)), FadedBrush(Ink.Gold, 0.95), 2.4, 11);
            if (ShowLabels)
            {
                // The swirl adds to the motion on the right of the track in the north, on the left in the south.
                AddBoxTarget("motion", DrawPill(dc, Loc("Typhoon_Storm_Motion"), c + (direction * Math.Min(150, view.Radius * 0.45)), Ink.Gold, 11));
                var side = Southern ? new Vector(direction.Y, -direction.X) : new Vector(-direction.Y, direction.X);
                AddBoxTarget("strongside", DrawPill(dc, Loc("Typhoon_Storm_StrongSide"), c + (side * view.Radius * 0.62), Ink.Red, 11));
            }
        }

        // The calm eye (or, before there is one, the low's centre) is a target with or without its label.
        var hasEye = Spin && wind >= 30;
        var eye = StormModel.EyeRadius(CentralPressure, StormLatitude) * view.PxPerKm;
        if (hasEye) _targets.Add("eye", c, Math.Max(10, eye));
        if (!ShowLabels)
        {
            if (!hasEye) _targets.Add("low", c, 12);
            return;
        }

        if (hasEye)
        {
            AddBoxTarget("eye", DrawText(dc, Loc("Typhoon_Storm_Eye"), c + new Vector(0, -eye - 10), Ink.Text, 11, center: true, bold: true));
        }
        else
        {
            AddBoxTarget("low", DrawText(dc, Loc("Typhoon_Storm_Low"), c, Ink.Red, 16, center: true, bold: true));
        }

        if (Spin)
        {
            // Callouts to the eyewall and a rain band.
            var wall = c + (new Vector(-0.7, 0.7) * rmax * 1.3 * view.PxPerKm);
            var wallLabel = c + new Vector(-view.Radius * 0.62, view.Radius * 0.55);
            dc.DrawLine(StormInk.Leader, wall, wallLabel);
            AddBoxTarget("eyewall", DrawPill(dc, Loc("Typhoon_Storm_Eyewall"), wallLabel, Ink.Text, 11));

            var bandLabel = c + new Vector(view.Radius * 0.7, view.Radius * 0.72);
            AddBoxTarget("bands", DrawPill(dc, Loc("Typhoon_Storm_Bands"), bandLabel, Ink.Text, 11));
        }

        var note = !Spin ? "Typhoon_Storm_NoSpin" : Southern ? "Typhoon_Storm_Clockwise" : "Typhoon_Storm_Anticlockwise";
        var stage = Stage();
        AddBoxTarget("turn", DrawText(dc, Loc(note), new Point(stage.Left + 28, stage.Top + 10), Spin ? Ink.Cyan : Ink.Orange, 11.5));
    }

    /// <summary>The probe: the wind there (as felt on the ground), the pressure push towards the centre, and a readout.</summary>
    private void DrawProbe(PaintContext dc, StormView view)
    {
        var at = view.Screen(ProbeX, ProbeY);
        var r = Math.Sqrt((ProbeX * ProbeX) + (ProbeY * ProbeY));
        var wind = GroundWind(ProbeX, ProbeY);
        var speed = wind.Length;
        var pressure = StormModel.Pressure(r, CentralPressure, StormLatitude);

        dc.DrawEllipse(StormInk.ProbeShade, Ink.Frame, at, 7, 7);
        dc.DrawEllipse(FadedBrush(Ink.Gold, 1), null, at, 3, 3);

        // Arrows: the wind (gold), and the push of the pressure towards the low (cyan).
        if (r > 3)
        {
            Vector push = view.Center - at;
            push /= push.Length;
            DrawArrow(dc, at, at + (push * 34), FadedBrush(Ink.Cyan, 0.9), 2, 8);
        }

        if (speed > 0.5)
        {
            var screenWind = new Vector(wind.X, -wind.Y) / speed;
            DrawArrow(dc, at, at + (screenWind * Math.Clamp(speed * 1.3, 14, 90)), FadedBrush(Ink.Gold, 1), 2.6, 10);
        }

        var force = StormModel.Beaufort(speed);
        var lines = new[]
        {
            LocFormat("Typhoon_Probe_Distance", Number(r, "0")),
            LocFormat("Typhoon_Probe_Wind", Number(speed, "0"), Number(speed * 3.6, "0"), ForceText(force)),
            LocFormat("Typhoon_Probe_Pressure", Number(pressure, "0")),
        };
        var color = Quantize(WindColor(speed));
        var x = at.X + 14;
        var y = at.Y - 26;
        var widths = lines.Select(l => _labels.Text(l, 11, Ink.Text).Width).ToArray();
        if (x + widths.Max() + 16 > ActualWidth) x = at.X - widths.Max() - 30;
        var box = new Rect(x, y - 6, widths.Max() + 16, (lines.Length * 16) + 8);
        dc.DrawRoundedRectangle(StormInk.ProbeBack, MakePen(FadedBrush(color, 0.9), 1), box, 6, 6);
        for (var i = 0; i < lines.Length; i++) DrawText(dc, lines[i], new Point(x + 8, y + 6 + (i * 16)), i == 1 ? color : Ink.Text, 11);
        AddBoxTarget("probe", box);

        if (ShowLabels && r > 3)
        {
            // Under the tip of the push arrow, away from the readout.
            Vector push = view.Center - at;
            push /= push.Length;
            AddBoxTarget("push", DrawText(dc, Loc("Typhoon_Probe_Push"), at + (push * 40) + new Vector(0, 12), Ink.Cyan, 9.5, center: true));
        }

        // The probe's own dot last, so the walkthrough's ring lands on it.
        _targets.Add("probe", at, 9);
    }

    private void DrawScaleBar(PaintContext dc, StormView view)
    {
        var length = 200 * view.PxPerKm;
        var start = new Point(view.Center.X - view.Radius, view.Center.Y + view.Radius - 4);
        dc.DrawLine(Ink.Axis, start, start + new Vector(length, 0));
        dc.DrawLine(Ink.Axis, start + new Vector(0, -4), start + new Vector(0, 4));
        dc.DrawLine(Ink.Axis, start + new Vector(length, -4), start + new Vector(length, 4));
        AddBoxTarget("scale", DrawText(dc, "200 km", start + new Vector(length / 2, -10), Ink.Dim, 10, center: true));
    }

    private static class StormInk
    {
        public static readonly IBrush Sea = SpaceDrawing.RadialGradient((Color.FromRgb(0x1A, 0x4A, 0x7A), 0), (Color.FromRgb(0x0A, 0x1E, 0x3A), 1));

        public static readonly IBrush Vignette = SpaceDrawing.RadialGradient(
            (Color.FromArgb(0, 0x06, 0x09, 0x10), 0.82),
            (Color.FromArgb(200, 0x06, 0x09, 0x10), 1));

        public static readonly IPen Isobar = MakePen(Solid(120, 0x6F, 0xD8, 0xFF), 1, dashed: true);
        public static readonly IPen Leader = MakePen(Solid(150, 0xEE, 0xF1, 0xF6), 1);
        public static readonly IBrush ProbeShade = Solid(120, 0x06, 0x09, 0x10);
        public static readonly IBrush ProbeBack = Solid(215, 0x08, 0x0C, 0x16);
        public static readonly Color Breeze = Color.FromRgb(0xA8, 0xD8, 0xFF);
        public static readonly Color ShadowColor = Color.FromRgb(0x04, 0x08, 0x14);
    }
}
