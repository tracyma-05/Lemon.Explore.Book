using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Lightning;

/// <summary>
/// Cloud mode: a thunderstorm in slice, 36 km across and 16.5 km up (to scale). The updraft carries small ice crystals
/// up through falling graupel; where they rub past each other the graupel takes negative charge if it is colder than
/// −15 °C and positive if warmer, the light crystals carry their charge to the top. So the cloud ends up positive on top,
/// negative in the middle and a little positive below; the ground under it turns positive, the field at the ground turns
/// over, and when the charge is big enough the air breaks down: a flash in the cloud or one to the ground.
/// </summary>
public sealed partial class LightningDiagram
{
    private const double CloudLeft = -14;
    private const double CloudRight = 22;
    private const double CloudHeight = 16.5;

    private readonly Random _cloudRandom = new(20261009);
    private readonly List<Grain> _grains = [];
    private List<(Point At, double R)>? _cloudBalls;
    private double _cloudTime;
    private double _upper = 0.6;
    private double _main = 0.6;
    private double _lower = 0.6;
    private double _threshold = 1;
    private int _flashCount;
    private CloudFlash? _flash;

    /// <summary>A flash asked for (<see cref="FlashRequest"/>), made on the storm's next step.</summary>
    private FlashKind _pendingFlash;

    /// <summary>An ice crystal (small, carried up) or a graupel pellet (soft hail, falling), in km, with its charge's sign.</summary>
    private sealed class Grain
    {
        public bool Graupel;
        public double X;
        public double Z;
        public int Sign;
        public double Rate;
        public int Direction;
        public double Sparkle = -10;
    }

    /// <summary>A flash: its kind, when it started, its channels (km) and, for one to the ground, where it hit.</summary>
    private sealed record CloudFlash(FlashKind Kind, double Start, List<Point[]> Channels, double GroundX);

    private readonly record struct CloudView(double X0, double Y0, double Scale)
    {
        public Point P(double km, double z) => new(X0 + ((km - CloudLeft) * Scale), Y0 - (z * Scale));

        public double Km(double screenX) => CloudLeft + ((screenX - X0) / Scale);
    }

    private CloudView CloudLayout()
    {
        var stage = Stage();
        var room = new Rect(stage.Left + 62, stage.Top + 4, Math.Max(100, stage.Width - 62 - 150), Math.Max(100, stage.Height - 40));
        var scale = Math.Min(room.Width / (CloudRight - CloudLeft), room.Height / CloudHeight);
        var x0 = room.Left + ((room.Width - (scale * (CloudRight - CloudLeft))) / 2);
        return new CloudView(x0, room.Top + (CloudHeight * scale), scale);
    }

    private void MoveMeter(Point screen)
    {
        var km = Math.Clamp(CloudLayout().Km(screen.X), CloudLeft + 1, CloudRight - 1);
        SetCurrentValue(MeterXProperty, Math.Round(km, 1));
    }

    #region the simulation

    private void StepCloud(double step)
    {
        if (_grains.Count == 0) SeedGrains();
        if (step <= 0) return;
        _cloudTime += step;

        // Charging: the collisions pull the charges apart at the rate the storm's flash rate needs.
        var rate = CloudModel.ChargingRate(CloudTop) * step;
        _upper = Math.Min(1.25, _upper + rate);
        _main = Math.Min(1.25, _main + rate);
        _lower = Math.Min(1.0, _lower + (rate * 0.8));
        if (_pendingFlash != FlashKind.None)
        {
            Flash(_pendingFlash);
            _pendingFlash = FlashKind.None;
        }
        else if (_main >= _threshold)
        {
            Flash();
        }

        StepGrains(step);

        Push(GroundFieldProperty, Math.Round(CloudModel.GroundField(MeterX, CloudTop, _upper, _main, _lower), 2));
        Push(CloudTimeProperty, Math.Round(_cloudTime, 2));
    }

    /// <summary>A flash: of the kind asked for, or in the cloud or to the ground by the latitude's odds.</summary>
    private void Flash(FlashKind kind = FlashKind.None)
    {
        var toGround = kind == FlashKind.None ? _cloudRandom.NextDouble() < CloudModel.GroundShare(Latitude) : kind == FlashKind.CloudToGround;
        kind = toGround ? FlashKind.CloudToGround : FlashKind.Intracloud;
        var charges = CloudModel.Charges(CloudTop);
        var main = charges[1];
        var start = new Point(main.X + ((_cloudRandom.NextDouble() - 0.5) * 3), main.Height);
        var channels = new List<Point[]>();
        var groundX = 0.0;

        if (toGround)
        {
            var path = Walk(start, 0.38, down: true, 60);
            groundX = path[^1].X;
            channels.Add(path);
            for (var i = 2; i < path.Length - 3; i++)
            {
                if (_cloudRandom.NextDouble() < 0.28) channels.Add(Walk(path[i], 0.3, down: true, 2 + _cloudRandom.Next(4), stopAbove: 0.4));
            }

            // It starts in the cloud: a few channels spread through the negative layer first.
            for (var k = 0; k < 3; k++) channels.Add(Sideways(start, (k % 2 == 0 ? 1 : -1) * (2 + (3 * _cloudRandom.NextDouble())), 0.4));
            _main = Math.Max(0, _main - CloudModel.FlashShare);
            _lower = Math.Max(0, _lower - 0.3);
        }
        else
        {
            // Up from the negative middle to the positive top, then out through the positive layer and the anvil.
            var upper = charges[0];
            var up = Line(start, new Point(upper.X + ((_cloudRandom.NextDouble() - 0.5) * 2), upper.Height - 0.3), 9, 0.5);
            channels.Add(up);
            for (var k = 0; k < 4; k++) channels.Add(Sideways(up[^1], (k % 2 == 0 ? 1 : -1) * (3 + (6 * _cloudRandom.NextDouble())), 0.5));
            for (var k = 0; k < 2; k++) channels.Add(Sideways(start, (k % 2 == 0 ? 1 : -1) * (1.5 + (2 * _cloudRandom.NextDouble())), 0.35));
            _main = Math.Max(0, _main - CloudModel.FlashShare);
            _upper = Math.Max(0, _upper - CloudModel.FlashShare);
        }

        _threshold = 1 + ((_cloudRandom.NextDouble() - 0.5) * 0.2);
        _flash = new CloudFlash(kind, _cloudTime, channels, groundX);
        _flashCount++;
        Push(LastFlashProperty, kind);
        Push(FlashCountProperty, _flashCount);
    }

    /// <summary>A jagged channel stepping from <paramref name="from"/>, down to the ground (or a few steps).</summary>
    private Point[] Walk(Point from, double step, bool down, int steps, double stopAbove = 0)
    {
        var points = new List<Point> { from };
        var drift = (_cloudRandom.NextDouble() - 0.5) * 0.6;
        for (var i = 0; i < steps; i++)
        {
            var at = points[^1];
            var angle = drift + (Gaussian() * 0.5);
            var next = new Point(at.X + (step * Math.Sin(angle)), at.Y - (step * Math.Abs(Math.Cos(angle))));
            if (next.Y <= stopAbove)
            {
                if (stopAbove <= 0) points.Add(new Point(next.X, 0));
                break;
            }

            points.Add(next);
        }

        return [.. points];
    }

    /// <summary>A channel wandering sideways for <paramref name="km"/> (negative: to the left).</summary>
    private Point[] Sideways(Point from, double km, double wobble)
    {
        var n = Math.Max(3, (int)(Math.Abs(km) / 0.45));
        var points = new Point[n + 1];
        points[0] = from;
        for (var i = 1; i <= n; i++)
        {
            var p = points[i - 1];
            points[i] = new Point(p.X + (km / n), p.Y + (Gaussian() * wobble * 0.5));
        }

        return points;
    }

    /// <summary>A jagged channel from one point to another.</summary>
    private Point[] Line(Point from, Point to, int pieces, double wobble)
    {
        var points = new Point[pieces + 1];
        for (var i = 0; i <= pieces; i++)
        {
            var f = i / (double)pieces;
            var off = i == 0 || i == pieces ? 0 : Gaussian() * wobble * 0.5;
            points[i] = new Point(Mix(from.X, to.X, f) + off, Mix(from.Y, to.Y, f));
        }

        return points;
    }

    private double Gaussian()
    {
        var u = 1 - _cloudRandom.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * _cloudRandom.NextDouble());
    }

    private void SeedGrains()
    {
        for (var i = 0; i < 170; i++)
        {
            _grains.Add(new Grain
            {
                X = (Hash(i, 1.1) - 0.5) * 5,
                Z = CloudModel.CloudBase + (Hash(i, 2.2) * 9),
                Rate = 0.7 + (0.6 * Hash(i, 3.3)),
                Direction = Hash(i, 4.4) < 0.75 ? 1 : -1,
            });
        }

        var reversal = CloudModel.HeightOf(CloudModel.ReversalTemperature);
        for (var i = 0; i < 70; i++)
        {
            _grains.Add(new Grain
            {
                Graupel = true,
                X = (Hash(i, 5.5) - 0.4) * 4.5,
                Z = CloudModel.HeightOf(0) + (Hash(i, 6.6) * (reversal + 3 - CloudModel.HeightOf(0))),
                Rate = 0.7 + (0.6 * Hash(i, 7.7)),
            });
        }
    }

    /// <summary>
    /// The crystals ride the updraft up the core and out along the anvil; the graupel sinks slowly through them. Each
    /// takes its charge where they meet: above the −15 °C level crystals positive and graupel negative, below it the other
    /// way round.
    /// </summary>
    private void StepGrains(double step)
    {
        var top = CloudTop;
        var freezing = CloudModel.HeightOf(0);
        var reversal = CloudModel.HeightOf(CloudModel.ReversalTemperature);
        var lift = 0.55 + (0.5 * Smooth(8, 16, top));
        var n = 0;
        foreach (var g in _grains)
        {
            n++;
            var old = g.Sign;
            if (g.Graupel)
            {
                // Held up by the updraft, it sinks slowly; below the freezing level it melts into rain.
                g.Z -= 0.12 * g.Rate * step;
                g.X += Math.Sin((_t * 0.7) + n) * 0.1 * step;
                if (g.Z < freezing)
                {
                    g.Z = reversal + 0.5 + (Hash(n + (int)_cloudTime, 8.8) * 2.5);
                    g.X = (Hash(n + (int)(_cloudTime * 3), 9.9) - 0.4) * 4.5;
                    g.Sign = 0;
                    old = 0;
                }

                // Colder than the reversal it takes negative charge; a few that fall below it turn positive (the small
                // positive charge low in the cloud).
                if (g.Z > reversal) g.Sign = -1;
                else if (g.Sign <= 0 && Hash(n, 4.2) < 0.3 && g.Z < reversal - 0.6) g.Sign = 1;
            }
            else
            {
                var core = Math.Max(0, 1 - Math.Pow(g.X / 3.2, 2));
                if (g.Z < top - 1.4 && core > 0)
                {
                    g.Z += lift * g.Rate * (0.3 + (0.7 * core)) * step;
                    g.X += (Hash(n, 1.9) - 0.5) * 0.2 * step;
                }
                else
                {
                    // Out along the anvil, sinking slowly as it goes.
                    g.X += g.Direction * 1.3 * g.Rate * step;
                    g.Z = Math.Max(top - 2.4, g.Z - (0.05 * step));
                }

                if (g.X > CloudRight - 6 || g.X < -7)
                {
                    g.X = (Hash(n + (int)_cloudTime, 2.9) - 0.5) * 5;
                    g.Z = CloudModel.CloudBase + (Hash(n + (int)(_cloudTime * 7), 3.9) * 1.5);
                    g.Sign = 0;
                    old = 0;
                }

                if (g.Z > freezing && g.Z < top - 1.2) g.Sign = g.Z > reversal ? 1 : -1;
            }

            if (g.Sign != old && g.Sign != 0) g.Sparkle = _t;
        }
    }

    #endregion

    #region drawing

    private void DrawCloudMode(PaintContext dc, double width, double height)
    {
        var view = CloudLayout();
        var flash = _flash is { } f && _cloudTime - f.Start < 1.0 ? f : null;
        var bright = flash is null ? 0 : FlashBrightness(flash);

        dc.DrawRectangle(CloudInk.Sky, null, new Rect(0, 0, width, height));
        if (flash?.Kind == FlashKind.CloudToGround) dc.DrawRectangle(FadedBrush(Ink.Bolt, 0.22 * bright), null, new Rect(0, 0, width, height));

        DrawTemperatureLines(dc, view);
        DrawRainShaft(dc, view);
        DrawCloudBody(dc, view, flash, bright);
        DrawGrains(dc, view);
        if (ShowCharges) DrawChargeCentres(dc, view);
        if (flash is not null) DrawCloudFlash(dc, view, flash, bright);
        DrawGround(dc, view, flash, bright);
        DrawCloudAxes(dc, view);
        if (ShowLabels) DrawCloudLabels(dc, view);
    }

    /// <summary>How bright the flash is now: a quick leader then three or four strokes for one to the ground, a flicker for one in the cloud.</summary>
    private double FlashBrightness(CloudFlash flash)
    {
        var age = _cloudTime - flash.Start;
        if (flash.Kind == FlashKind.CloudToGround)
        {
            if (age < 0.12) return 0.25;
            var b = 0.0;
            foreach (var s in new[] { 0.12, 0.3, 0.46, 0.6 }) if (age >= s) b = Math.Max(b, Math.Exp(-(age - s) / 0.09));
            return Math.Max(b, 0.35 * Math.Exp(-(age - 0.12) / 0.4));
        }

        return Math.Max(0, (0.55 + (0.45 * Math.Sin(age * 47))) * (1 - Smooth(0.1, 0.8, age)));
    }

    private void DrawTemperatureLines(PaintContext dc, CloudView view)
    {
        foreach (var (celsius, key, color, id) in new[] { (0.0, "Lightning_Cloud_Freezing", Ink.Cyan, "freezing"), (CloudModel.ReversalTemperature, "Lightning_Cloud_Reversal", Ink.Gold, "reversal"), (-40.0, "Lightning_Cloud_Glaciation", Ink.Violet, "glaciation") })
        {
            var z = CloudModel.HeightOf(celsius);
            var y = view.P(0, z).Y;
            var from = new Point(view.P(CloudLeft, 0).X, y);
            var to = new Point(view.P(CloudRight, 0).X, y);
            if (Glows(id)) dc.DrawLine(GlowLine, from, to);
            dc.DrawLine(CloudInk.Dashed(color), from, to);
            if (ShowLabels) AddBoxTarget(id, DrawText(dc, LocFormat(key, Number(celsius, "0")), new Point(view.P(CloudRight, 0).X + 6, y), color, 10.5));
        }
    }

    private void DrawRainShaft(PaintContext dc, CloudView view)
    {
        for (var i = 0; i < 120; i++)
        {
            var x = -3.5 + (Hash(i, 1.7) * 6.5);
            var fall = Frac((_t * 0.9) + Hash(i, 2.8));
            var z = CloudModel.CloudBase * (1 - fall);
            var p = view.P(x, z);
            _batch.Add(CloudInk.Rain, 0.5, [p, p + new Vector(-1, -7)]);
        }

        _batch.Draw(dc, this, 1);
    }

    /// <summary>The cumulonimbus: a tower up the updraft, an anvil spreading downwind, an overshooting top over the core.</summary>
    private List<(Point At, double R)> CloudBalls(CloudView view)
    {
        if (_cloudBalls is not null) return _cloudBalls;
        var top = CloudTop;
        var balls = new List<(Point, double)>();
        var n = 0;

        // The tower, widening a little with height.
        for (var z = CloudModel.CloudBase + 0.6; z < top - 1; z += 0.8)
        {
            var half = 3.4 + (0.6 * (z / top));
            var lean = 0.15 * z;
            for (var x = -half; x <= half; x += 1.0, n++)
            {
                var r = (0.75 + (0.35 * Hash(n, 1.4))) * view.Scale;
                balls.Add((view.P(x + lean + ((Hash(n, 2.4) - 0.5) * 0.6), z + ((Hash(n, 3.4) - 0.5) * 0.5)), r));
            }
        }

        // The anvil: thick over the core, thinning downwind; a short overhang upwind.
        for (var x = -6.0; x <= 18; x += 0.8, n++)
        {
            var thick = x < 2 ? 1.8 : Math.Max(0.5, 2.2 - ((x - 2) * 0.09));
            var mid = top - 1.1 - (x > 6 ? (x - 6) * 0.03 : 0);
            for (var k = 0; k < 2; k++, n++)
            {
                var r = (thick * 0.55 * (0.8 + (0.4 * Hash(n, 4.4)))) * view.Scale;
                balls.Add((view.P(x + ((Hash(n, 5.4) - 0.5) * 0.5), mid + ((k - 0.5) * thick * 0.5)), r));
            }
        }

        // The overshooting top: the updraft punches through the anvil.
        for (var k = 0; k < 4; k++, n++) balls.Add((view.P(0.6 + ((k - 1.5) * 0.9), top - 0.3 + (Hash(n, 6.4) * 0.4)), (0.8 + (0.3 * Hash(n, 7.4))) * view.Scale));

        _cloudBalls = balls;
        return balls;
    }

    private void DrawCloudBody(PaintContext dc, CloudView view, CloudFlash? flash, double bright)
    {
        var balls = CloudBalls(view);
        var light = CloudInk.CloudLight;
        var dark = CloudInk.CloudDark;
        if (flash is not null)
        {
            light = Quantize(Lerp(light, Ink.Bolt, 0.35 * bright));
            dark = Quantize(Lerp(dark, Color.FromRgb(0x9A, 0x96, 0xC8), 0.5 * bright));
        }

        DrawPuffs(dc, balls, view.P(0, CloudTop).Y, view.P(0, CloudModel.CloudBase).Y, 0.92, light, dark);
    }

    private void DrawGrains(PaintContext dc, CloudView view)
    {
        foreach (var g in _grains)
        {
            var p = view.P(g.X, g.Z);
            var color = g.Sign > 0 ? Ink.Plus : g.Sign < 0 ? Ink.Minus : Ink.White;
            var r = g.Graupel ? 2.6 : 1.4;
            dc.DrawEllipse(FadedBrush(g.Graupel ? Quantize(Lerp(Color.FromRgb(0xE8, 0xEC, 0xF2), color, 0.6)) : color, g.Graupel ? 0.95 : 0.85), null, p, r, r);
            var since = _t - g.Sparkle;
            if (since >= 0 && since < 0.3) dc.DrawEllipse(Glow(Ink.White, 0.9 * (1 - (since / 0.3))), null, p, 6, 6);
        }
    }

    private void DrawChargeCentres(PaintContext dc, CloudView view)
    {
        var centres = CloudModel.Charges(CloudTop);
        double[] levels = [_upper, _main, _lower];
        for (var i = 0; i < centres.Length; i++)
        {
            var c = centres[i];
            var level = Math.Clamp(levels[i], 0, 1.25);
            var small = i == 2;
            var rx = (small ? 1.6 : 3.6) * view.Scale;
            var ry = (small ? 0.6 : 1.2) * view.Scale;
            var at = view.P(c.X, c.Height);
            var color = c.Charge > 0 ? Ink.Plus : Ink.Minus;
            dc.DrawEllipse(Glow(color, 0.45 * level), null, at, rx * 1.4, ry * 1.6);
            _targets.AddBox(i switch { 0 => "upper", 1 => "main", _ => "lower" }, new Rect(at.X - rx, at.Y - ry, 2 * rx, 2 * ry));

            var count = (int)Math.Round(level * (small ? 3 : 9));
            for (var k = 0; k < count; k++)
            {
                var col = small ? k : k % 5;
                var row = small ? 0 : k / 5;
                var cols = small ? count : Math.Min(5, count - (row * 5));
                var x = at.X + ((col - ((cols - 1) / 2.0)) * rx * 0.36);
                var y = at.Y + ((row - (small ? 0 : 0.5)) * ry * 0.75);
                DrawSign(dc, new Point(x, y), c.Charge > 0, 10);
            }
        }
    }

    private void DrawCloudFlash(PaintContext dc, CloudView view, CloudFlash flash, double bright)
    {
        var age = _cloudTime - flash.Start;
        if (flash.Kind == FlashKind.Intracloud)
        {
            // The cloud lights up from inside.
            var middle = flash.Channels[0][^1];
            dc.DrawEllipse(Glow(Ink.Bolt, 0.6 * bright), null, view.P(middle.X, middle.Y), 9 * view.Scale, 5 * view.Scale);
        }

        // To the ground: the leader works its way down, then the strokes light the whole channel.
        var reveal = flash.Kind == FlashKind.CloudToGround ? Math.Min(1, age / 0.12) : 1;
        for (var c = 0; c < flash.Channels.Count; c++)
        {
            var channel = flash.Channels[c];
            var main = c == 0;
            var count = Math.Max(2, (int)Math.Ceiling(channel.Length * reveal));
            var points = channel.Take(count).Select(p => view.P(p.X, p.Y)).ToArray();
            var alpha = flash.Kind == FlashKind.Intracloud ? 0.55 * bright : (age < 0.12 ? 0.5 : bright) * (main ? 1 : 0.55);
            if (alpha < 0.03) continue;
            _batch.Add(Ink.Violet, alpha * 0.5, points);
        }

        _batch.Draw(dc, this, flash.Kind == FlashKind.CloudToGround ? 8 : 5);
        for (var c = 0; c < flash.Channels.Count; c++)
        {
            var channel = flash.Channels[c];
            var count = Math.Max(2, (int)Math.Ceiling(channel.Length * reveal));
            var points = channel.Take(count).Select(p => view.P(p.X, p.Y)).ToArray();
            var alpha = flash.Kind == FlashKind.Intracloud ? 0.7 * bright : (age < 0.12 ? 0.6 : bright) * (c == 0 ? 1 : 0.6);
            _batch.Add(Ink.White, alpha, points);
        }

        _batch.Draw(dc, this, flash.Kind == FlashKind.CloudToGround && age >= 0.12 ? 2.6 : 1.3);
        if (flash.Kind == FlashKind.CloudToGround && age >= 0.12) dc.DrawEllipse(Glow(Ink.Bolt, 0.8 * bright), null, view.P(flash.GroundX, 0), 40, 22);
    }

    /// <summary>The ground: its induced charge, the field over it, a tree with corona, and you with the field meter.</summary>
    private void DrawGround(PaintContext dc, CloudView view, CloudFlash? flash, double bright)
    {
        var left = view.P(CloudLeft, 0);
        var right = view.P(CloudRight, 0);
        dc.DrawRectangle(CloudInk.Ground, null, new Rect(left.X, left.Y, right.X - left.X, 14));
        if (flash?.Kind == FlashKind.CloudToGround) dc.DrawRectangle(FadedBrush(Ink.Bolt, 0.3 * bright), null, new Rect(left.X, left.Y, right.X - left.X, 14));

        var top = CloudTop;
        for (var x = CloudLeft + 1; x <= CloudRight - 1; x += 1.5)
        {
            var e = CloudModel.GroundField(x, top, _upper, _main, _lower);
            var p = view.P(x, 0);
            if (Math.Abs(e) > 0.4) DrawSign(dc, p + new Vector(0, 7), e > 0, 7, Math.Min(1, Math.Abs(e) / 4));

            // The field: up where the ground is positive under the storm, down in fair weather and far out.
            var length = Math.Clamp(Math.Abs(e) * 3.2, 0, 34);
            if (length > 4)
            {
                var from = p + new Vector(0, -4 - (e > 0 ? 0 : length));
                DrawArrow(dc, from, from + new Vector(0, e > 0 ? -length : length), FadedBrush(Ink.Gold, 0.75), 1.4, 6);
                _targets.Add("field", from + new Vector(0, e > 0 ? -length / 2 : length / 2), 7);
            }
        }

        // A tree right under the storm: its tips glow (corona) when the field is strong.
        const double treeX = -1.8;
        var foot = view.P(treeX, 0);
        dc.DrawLine(CloudInk.Trunk, foot, foot + new Vector(0, -14));
        dc.DrawEllipse(CloudInk.Tree, null, foot + new Vector(0, -20), 9, 10);
        _targets.Add("corona", foot + new Vector(0, -20), 10);
        var treeField = CloudModel.GroundField(treeX, top, _upper, _main, _lower);
        if (treeField > 5) dc.DrawEllipse(Glow(Ink.Violet, Math.Min(0.9, (treeField - 5) / 4) * (0.6 + (0.4 * Math.Sin(_t * 31)))), null, foot + new Vector(0, -29), 10, 10);

        // You and the field meter.
        var you = view.P(MeterX, 0);
        var field = CloudModel.GroundField(MeterX, top, _upper, _main, _lower);
        DrawPerson(dc, you, field > 5);
        var box = new Rect(you.X + 9, you.Y - 18, 14, 18);
        dc.DrawRectangle(CloudInk.Meter, CloudInk.MeterEdge, box);
        dc.DrawEllipse(FadedBrush(field > 5 ? Ink.Red : Ink.Green, 1), null, new Point(box.Left + 7, box.Top + 5), 2.5, 2.5);
        _targets.Add("meter", you + new Vector(6, -12), 13);
        if (ShowLabels)
        {
            var color = field > 5 ? Ink.Red : Ink.Gold;
            AddBoxTarget("meter", DrawPill(dc, LocFormat(field >= 0 ? "Lightning_Cloud_Meter_Up" : "Lightning_Cloud_Meter_Down", Number(Math.Abs(field), "0.0")), you + new Vector(0, -54), color, 11));
        }
    }

    /// <summary>A person, not to scale (24 px tall), their hair on end in a strong field.</summary>
    private void DrawPerson(PaintContext dc, Point foot, bool hairUp)
    {
        var pen = CloudInk.Person;
        var hip = foot + new Vector(0, -10);
        var neck = foot + new Vector(0, -19);
        dc.DrawLine(pen, foot + new Vector(-3.5, 0), hip);
        dc.DrawLine(pen, foot + new Vector(3.5, 0), hip);
        dc.DrawLine(pen, hip, neck);
        dc.DrawLine(pen, neck + new Vector(0, 2), neck + new Vector(-5, 8));
        dc.DrawLine(pen, neck + new Vector(0, 2), neck + new Vector(5, 8));
        var head = neck + new Vector(0, -3.5);
        dc.DrawEllipse(CloudInk.Skin, null, head, 3.3, 3.3);
        if (hairUp)
        {
            for (var k = -2; k <= 2; k++) dc.DrawLine(CloudInk.Hair, head + new Vector(k * 1.2, -2.5), head + new Vector(k * 2.6, -9 + Math.Abs(k)));
        }
    }

    private void DrawCloudAxes(PaintContext dc, CloudView view)
    {
        for (var z = 0; z <= 16; z += 2)
        {
            var y = view.P(0, z).Y;
            DrawTextRight(dc, Number(z, "0"), new Point(view.X0 - 8, y), Ink.Dim, 10);
            dc.DrawLine(Ink.Frame, new Point(view.X0 - 4, y), new Point(view.X0, y));
        }

        DrawTextRight(dc, "km", new Point(view.X0 - 8, view.P(0, 16.4).Y - 6), Ink.Dim, 10);

        // A 10 km scale bar under the ground.
        var a = view.P(CloudRight - 11, 0) + new Vector(0, 26);
        var b = view.P(CloudRight - 1, 0) + new Vector(0, 26);
        dc.DrawLine(Ink.Frame, a, b);
        dc.DrawLine(Ink.Frame, a + new Vector(0, -3), a + new Vector(0, 3));
        dc.DrawLine(Ink.Frame, b + new Vector(0, -3), b + new Vector(0, 3));
        DrawText(dc, "10 km", Mix(a, b, 0.5) + new Vector(0, 9), Ink.Dim, 10, center: true);
    }

    private void DrawCloudLabels(PaintContext dc, CloudView view)
    {
        var top = CloudTop;
        AddBoxTarget("updraft", DrawPill(dc, Loc("Lightning_Cloud_Updraft"), view.P(-4.8, 4.5), Ink.Text, 11));
        DrawArrow(dc, view.P(-4.8, 5.6), view.P(-4.8, 8.4), FadedBrush(Ink.Text, 0.8), 2, 9);
        var anvil = view.P(12, top + 0.9);
        AddBoxTarget("anvil", DrawPill(dc, Loc("Lightning_Cloud_Anvil"), new Point(anvil.X, Math.Max(Stage().Top + 4, anvil.Y)), Ink.Text, 11));
        var topLabel = view.P(0.6, top + 1.4);
        AddBoxTarget("top", DrawPill(dc, LocFormat("Lightning_Cloud_Top", Number(top, "0.0"), Number(CloudModel.Temperature(top), "0")), new Point(topLabel.X, Math.Max(Stage().Top + 4, topLabel.Y)), Ink.Gold, 11));
        AddBoxTarget("graupel", DrawPill(dc, Loc("Lightning_Cloud_Graupel"), view.P(7.2, CloudModel.HeightOf(CloudModel.ReversalTemperature) - 1.6), Ink.Text, 10.5));
        AddBoxTarget("ice", DrawPill(dc, Loc("Lightning_Cloud_Ice"), view.P(7.2, CloudModel.HeightOf(CloudModel.ReversalTemperature) + 1.9), Ink.Text, 10.5));
        AddBoxTarget("rain", DrawPill(dc, Loc("Lightning_Cloud_Rain"), view.P(-8, 0.8), Ink.Cyan, 10.5));

        // A key to the two kinds of grain, by the side labels.
        var key = view.P(5.2, CloudModel.HeightOf(CloudModel.ReversalTemperature) - 1.6);
        dc.DrawEllipse(FadedBrush(Color.FromRgb(0xE8, 0xEC, 0xF2), 1), null, key, 2.6, 2.6);
        var key2 = view.P(5.2, CloudModel.HeightOf(CloudModel.ReversalTemperature) + 1.9);
        dc.DrawEllipse(FadedBrush(Ink.White, 1), null, key2, 1.4, 1.4);
    }

    #endregion

    private static class CloudInk
    {
        public static readonly IBrush Sky = SpaceDrawing.LinearGradient(Color.FromRgb(0x0A, 0x0E, 0x18), Color.FromRgb(0x22, 0x28, 0x36), 90);
        public static readonly IBrush Ground = SpaceDrawing.LinearGradient(Color.FromRgb(0x2E, 0x4A, 0x2A), Color.FromRgb(0x1A, 0x22, 0x16), 90);
        public static readonly Color CloudLight = Color.FromRgb(0xC8, 0xD0, 0xDC);
        public static readonly Color CloudDark = Color.FromRgb(0x3A, 0x40, 0x4E);
        public static readonly Color Rain = Color.FromRgb(0x9A, 0xB8, 0xE0);
        public static readonly IPen Trunk = MakePen(Solid(255, 0x5A, 0x40, 0x2A), 2);
        public static readonly IBrush Tree = Solid(255, 0x2E, 0x6A, 0x36);
        public static readonly IPen Person = MakePen(Solid(255, 0xE8, 0xEC, 0xF2), 1.8);
        public static readonly IBrush Skin = Solid(255, 0xE8, 0xC0, 0x98);
        public static readonly IPen Hair = MakePen(Solid(255, 0x3A, 0x28, 0x1A), 1.1);
        public static readonly IBrush Meter = Solid(255, 0x2A, 0x30, 0x3C);
        public static readonly IPen MeterEdge = MakePen(Solid(200, 0xC8, 0xD0, 0xDC), 1);

        private static readonly Dictionary<Color, IPen> DashedPens = [];

        public static IPen Dashed(Color color)
        {
            if (DashedPens.TryGetValue(color, out var pen)) return pen;
            pen = MakePen(Solid(110, color.R, color.G, color.B), 1, dashed: true);
            DashedPens[color] = pen;
            return pen;
        }
    }
}
