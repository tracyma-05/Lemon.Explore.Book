using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.SeaBreeze;

/// <summary>Coast mode: a cross-section from the sea over a coast, through a whole day, with the breeze that turns round.</summary>
public sealed partial class BreezeDiagram
{
    private readonly List<CellParticle> _coastParticles = [];
    private readonly Random _random = new(7);

    /// <summary>One particle of air going round a cell, in the cell's own coordinates: across (0…1) and up (0…1).</summary>
    private sealed class CellParticle
    {
        public double S;
        public double Q;
        public double Age;
        public double Life;
    }

    /// <summary>The coast's coordinates on screen: kilometres from the coast across, metres up.</summary>
    private readonly record struct CoastView(Rect Stage, double GroundY)
    {
        public const double Left = -30;
        public const double Right = 50;
        public const double Top = 2000;

        public double X(double km) => Stage.Left + ((km - Left) / (Right - Left) * Stage.Width);

        public double Y(double metres) => GroundY - (metres / Top * (GroundY - Stage.Top - 8));

        public Point P(double km, double metres) => new(X(km), Y(metres));

        public double PerKm => Stage.Width / (Right - Left);
    }

    /// <summary>The cell of the breeze at an hour: from where to where it reaches, how deep it is, which way it turns and how hard.</summary>
    private readonly record struct CoastCellShape(double From, double To, double Depth, double Sign, double Strength);

    /// <summary>The land's height above the sea, metres: a flat coastal plain rising gently to hills inland.</summary>
    private static double Ground(double km) =>
        km <= 0 ? 0 : (Math.Min(km, 2) * 3) + (Math.Max(0, km - 2) * 2.5) + (230 * Smooth(36, 49, km));

    private CoastCellShape CoastCell(double hour)
    {
        var wind = BreezeModel.ShoreWind(hour, Sky);
        if (wind >= 0) return new CoastCellShape(-22, Math.Max(4, BreezeModel.FrontDistance(hour, Sky)), 1800, 1, Math.Min(1.2, wind / 5));
        return new CoastCellShape(-Math.Max(4, BreezeModel.LandBreezeReach(hour, Sky)), 12, 650, -1, Math.Min(1, -wind / 2));
    }

    /// <summary>A point of a cell on screen: the left half of the cell lies over the sea, the right half over the land.</summary>
    private static Point CellPoint(CoastView view, CoastCellShape cell, double s, double q)
    {
        var km = s < 0.5 ? Mix(cell.From, 0, s / 0.5) : Mix(0, cell.To, (s - 0.5) / 0.5);
        return view.P(km, Ground(km) + (q * cell.Depth));
    }

    #region the air going round

    /// <summary>
    /// Moves particles round a cell whose streamfunction is sin(πs)·sin(πq): along the ground from the sea to the land (for
    /// <paramref name="sign"/> 1), up over the land, back aloft and down over the sea. Old particles fade and are reborn.
    /// </summary>
    private void AdvanceParticles(List<CellParticle> particles, int count, double sign, double rate, double step)
    {
        while (particles.Count < count) particles.Add(NewParticle(true));
        if (step <= 0) return;
        foreach (var p in particles)
        {
            var (ds, dq) = CellVelocity(p.S, p.Q, sign);
            p.S += ds * rate * step;
            p.Q += dq * rate * step;
            p.Age += step;
            if (p.Age > p.Life || p.S < 0.01 || p.S > 0.99 || p.Q < 0.01 || p.Q > 0.99)
            {
                var fresh = NewParticle(false);
                (p.S, p.Q, p.Age, p.Life) = (fresh.S, fresh.Q, fresh.Age, fresh.Life);
            }
        }
    }

    private static (double S, double Q) CellVelocity(double s, double q, double sign) =>
        (sign * Math.PI * Math.Sin(Math.PI * s) * Math.Cos(Math.PI * q),
         -sign * Math.PI * Math.Cos(Math.PI * s) * Math.Sin(Math.PI * q));

    private CellParticle NewParticle(bool anyAge)
    {
        var life = 5 + (_random.NextDouble() * 6);
        return new CellParticle
        {
            S = 0.04 + (_random.NextDouble() * 0.92),
            Q = 0.04 + (_random.NextDouble() * 0.92),
            Life = life,
            Age = anyAge ? _random.NextDouble() * life : 0,
        };
    }

    /// <summary>Draws particles with a short tail behind each, faded in and out over their life.</summary>
    private void DrawParticles(PaintContext dc, List<CellParticle> particles, double sign, double rate, double opacity, Func<double, double, Point> toScreen, Color color)
    {
        if (opacity <= 0.02) return;
        foreach (var p in particles)
        {
            var life = p.Age / p.Life;
            var alpha = Math.Min(1, Math.Min(life, 1 - life) * 6) * opacity;
            if (alpha <= 0.05) continue;
            var (ds, dq) = CellVelocity(p.S, p.Q, sign);
            var at = toScreen(p.S, p.Q);
            var tail = toScreen(p.S - (ds * rate * 0.45), p.Q - (dq * rate * 0.45));
            _batch.Add(FadedPen(color, alpha * 0.5, 1.5), tail, at);
            _batch.Dot(FadedPen(color, alpha, 3.6), at);
        }

        _batch.Draw(dc);
    }

    private void AdvanceCoastParticles(double step)
    {
        var cell = CoastCell(_hour);
        AdvanceParticles(_coastParticles, 130, cell.Sign, 0.06 * cell.Strength, step);
    }

    #endregion

    private void DrawCoast(PaintContext dc, double width, double height)
    {
        var stage = Stage(TitleSpace);
        var view = new CoastView(stage, stage.Bottom - (stage.Height * 0.17));
        var hour = _hour;
        var night = Nightness(hour);
        var wind = BreezeModel.ShoreWind(hour, Sky);
        var cell = CoastCell(hour);
        var overcast = Sky == BreezeSky.Overcast;

        DrawCoastSky(dc, width, view, hour, night, overcast);
        DrawCoastGround(dc, view, width, height, hour, night, wind);
        if (overcast) DrawOvercastDeck(dc, view, width, night);

        if (ShowPressure) DrawCoastPressure(dc, view, hour);
        DrawCoastClouds(dc, view, hour, cell, overcast, night);
        if (ShowFlow)
        {
            DrawParticles(dc, _coastParticles, cell.Sign, 0.06 * cell.Strength, Smooth(0.08, 0.3, cell.Strength),
                (s, q) => CellPoint(view, cell, s, q), cell.Sign > 0 ? Ink.Text : Color.FromRgb(0xC8, 0xD4, 0xFF));
        }

        DrawCellArrows(dc, view, cell, wind);
        if (ShowPressure) DrawCoastPressureMarks(dc, view, hour);

        DrawThermometers(dc, view, hour);
        DrawCoastScale(dc, view);
        DrawCoastClock(dc, width, stage, hour, wind);
    }

    /// <summary>0 by day, 1 at night, easing through dawn and dusk.</summary>
    private static double Nightness(double hour) =>
        hour < 12 ? 1 - Smooth(BreezeModel.Sunrise - 0.5, BreezeModel.Sunrise + 1, hour) : Smooth(BreezeModel.Sunset - 0.2, BreezeModel.Sunset + 1.3, hour);

    #region sky

    private static readonly (double Hour, Color Top, Color Bottom)[] SkyKeys =
    [
        (0, Color.FromRgb(0x04, 0x08, 0x15), Color.FromRgb(0x0B, 0x16, 0x30)),
        (4.8, Color.FromRgb(0x05, 0x0A, 0x1A), Color.FromRgb(0x10, 0x1E, 0x3C)),
        (5.5, Color.FromRgb(0x1A, 0x24, 0x48), Color.FromRgb(0xD9, 0x8A, 0x5E)),
        (6.5, Color.FromRgb(0x2A, 0x5A, 0x9A), Color.FromRgb(0xE8, 0xB9, 0x8A)),
        (8, Color.FromRgb(0x2A, 0x6A, 0xB4), Color.FromRgb(0x8C, 0xC0, 0xE8)),
        (17, Color.FromRgb(0x2A, 0x6A, 0xB4), Color.FromRgb(0x8C, 0xC0, 0xE8)),
        (18.5, Color.FromRgb(0x2B, 0x2F, 0x62), Color.FromRgb(0xF0, 0x8A, 0x4B)),
        (19.3, Color.FromRgb(0x0B, 0x12, 0x30), Color.FromRgb(0x2A, 0x2A, 0x55)),
        (20, Color.FromRgb(0x05, 0x0A, 0x1A), Color.FromRgb(0x0E, 0x1A, 0x36)),
        (24, Color.FromRgb(0x04, 0x08, 0x15), Color.FromRgb(0x0B, 0x16, 0x30)),
    ];

    private static (Color Top, Color Bottom) SkyColors(double hour, bool overcast)
    {
        var i = 0;
        while (i < SkyKeys.Length - 2 && SkyKeys[i + 1].Hour <= hour) i++;
        var (h0, top0, bottom0) = SkyKeys[i];
        var (h1, top1, bottom1) = SkyKeys[i + 1];
        var t = Math.Clamp((hour - h0) / (h1 - h0), 0, 1);
        var top = Lerp(top0, top1, t);
        var bottom = Lerp(bottom0, bottom1, t);
        if (!overcast) return (top, bottom);

        var grey = Lerp(Color.FromRgb(0x62, 0x6A, 0x76), Color.FromRgb(0x10, 0x13, 0x1A), Nightness(hour));
        return (Lerp(top, grey, 0.7), Lerp(bottom, grey, 0.6));
    }

    private void DrawCoastSky(PaintContext dc, double width, CoastView view, double hour, double night, bool overcast)
    {
        var (top, bottom) = SkyColors(hour, overcast);
        var sky = SpaceDrawing.LinearGradient(top, bottom, 90);
        dc.DrawRectangle(sky, null, new Rect(0, 0, width, view.GroundY + 1));
        if (!overcast && night > 0.05) _stars.Draw(dc, width, view.GroundY, RenderScale(), _clock.Elapsed.TotalSeconds, night);

        // The Sun rises over the land (east, right) and sets over the sea (west, left); the Moon follows at night.
        var arcTop = view.Stage.Top + 26;
        if (hour > BreezeModel.Sunrise - 0.4 && hour < BreezeModel.Sunset + 0.4)
        {
            var f = (hour - BreezeModel.Sunrise) / (BreezeModel.Sunset - BreezeModel.Sunrise);
            var at = new Point(Mix(width * 0.95, width * 0.05, f), view.GroundY - (Math.Sin(Math.PI * Math.Clamp(f, 0, 1)) * (view.GroundY - arcTop)));
            dc.PushOpacity(overcast ? 0.25 : 1);
            dc.DrawEllipse(Ink.SunGlow, null, at, 70, 70);
            dc.DrawEllipse(Ink.SunCore, null, at, 15, 15);
            dc.Pop();
            if (!overcast && at.Y < view.GroundY - 10) _targets.Add("sun", at, 20);
        }
        else if (!overcast)
        {
            var f = BreezeModel.Wrap(hour - BreezeModel.Sunset) / (24 - (BreezeModel.Sunset - BreezeModel.Sunrise));
            var at = new Point(Mix(width * 0.92, width * 0.08, f), view.GroundY - (Math.Sin(Math.PI * Math.Clamp(f, 0, 1)) * (view.GroundY - arcTop) * 0.85));
            dc.DrawEllipse(Ink.MoonGlow, null, at, 40, 40);
            dc.DrawEllipse(Ink.MoonCore, null, at, 10, 10);
        }
    }

    /// <summary>A grey deck of cloud across the top of the sky that keeps the Sun off the land.</summary>
    private static void DrawOvercastDeck(PaintContext dc, CoastView view, double width, double night)
    {
        var baseY = view.Y(1500);
        var color = Lerp(Color.FromRgb(0xA8, 0xB0, 0xBC), Color.FromRgb(0x2A, 0x30, 0x3A), night);
        var deck = SpaceDrawing.LinearGradient(WithAlpha(color, 0.9), WithAlpha(color, 0.75), 90);
        dc.DrawRectangle(deck, null, new Rect(0, view.Stage.Top, width, baseY - view.Stage.Top));
        var puff = new SolidColorBrush(WithAlpha(color, 0.85));
        for (var x = -20.0; x < width + 40; x += 34)
        {
            var r = 16 + (6 * Math.Sin(x * 0.37));
            dc.DrawEllipse(puff, null, new Point(x, baseY - 2), r * 1.3, r * 0.7);
        }
    }

    #endregion

    #region sea and land

    private void DrawCoastGround(PaintContext dc, CoastView view, double width, double height, double hour, double night, double wind)
    {
        var bottom = Math.Max(height, view.Stage.Bottom) + 2;
        var coastX = view.X(0);
        var chop = 1.2 + (Math.Abs(wind) * 0.35);

        // The sea, its surface moving with the waves.
        var sea = new StreamGeometry();
        using (var context = sea.Open())
        {
            context.BeginFigure(new Point(0, bottom), true);
            for (var x = 0.0; x <= coastX; x += 5)
            {
                context.LineTo(new Point(x, view.GroundY + (Math.Sin((x * 0.09) - (_t * 2.2)) * chop)));
            }

            context.LineTo(new Point(coastX, view.GroundY));
            context.LineTo(new Point(coastX, bottom));

            context.EndFigure(true);
        }
        dc.DrawGeometry(Ink.Sea, null, sea);
        for (var i = 0; i < 9; i++)
        {
            var x = Frac((i * 0.137) + (_t * 0.01)) * coastX;
            var y = view.GroundY + 8 + (i % 3 * 7);
            dc.DrawLine(Ink.Wave, new Point(x, y), new Point(x + 14, y));
        }

        // Surf at the beach.
        var surf = Frac(_t * 0.25);
        dc.PushOpacity(1 - surf);
        dc.DrawLine(Ink.Foam, new Point(coastX - 4 - (surf * 18), view.GroundY + 1), new Point(coastX - (surf * 18) + 6, view.GroundY + 1));
        dc.Pop();

        // The land: soil below, sand at the beach, grass inland.
        var land = new StreamGeometry();
        using (var context = land.Open())
        {
            context.BeginFigure(new Point(coastX, bottom), true);
            for (var km = 0.0; km <= CoastView.Right + 0.01; km += 0.5) context.LineTo(view.P(km, Ground(km)));
            context.LineTo(new Point(width, bottom));

            context.EndFigure(true);
        }
        dc.DrawGeometry(Ink.Soil, null, land);
        dc.DrawLine(Ink.SandEdge, view.P(0, Ground(0)), view.P(1.4, Ground(1.4)));
        var grass = new StreamGeometry();
        using (var context = grass.Open())
        {
            context.BeginFigure(view.P(1.4, Ground(1.4)), false);
            for (var km = 2.0; km <= CoastView.Right + 0.01; km += 0.5) context.LineTo(view.P(km, Ground(km)));

            context.EndFigure(false);
        }
        dc.DrawGeometry(null, Ink.Grass, grass);
        var underground = (bottom - view.GroundY) * 0.4;
        _targets.Add("sea", view.P(-10, 0) + new Vector(0, Math.Min(40, underground)), 24);
        _targets.Add("land", view.P(18, Ground(18)) + new Vector(0, Math.Min(40, underground)), 24);

        // The warm (or cold) air lying on the land.
        DrawLandAir(dc, view, hour);

        DrawTown(dc, view, night);
        DrawSmoke(dc, view, wind);
        DrawFlag(dc, view, wind);
        DrawBoat(dc, view, -7, wind, 0);
        DrawBoat(dc, view, -17, wind, 1.7);

        // Night: everything on the ground in the dark.
        if (night > 0.02)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(night * 110), 0, 0, 8)), null,
                new Rect(0, view.GroundY - 2, width, bottom - view.GroundY + 2));
        }
    }

    /// <summary>
    /// A glow of warm air over the land by day, a shallow layer of cold air by night, as strong as the land is warmer or
    /// colder than the sea.
    /// </summary>
    private void DrawLandAir(PaintContext dc, CoastView view, double hour)
    {
        var contrast = BreezeModel.Contrast(hour, Sky);
        var warm = contrast > 0;
        var strength = Math.Clamp(Math.Abs(contrast) / (warm ? 8 : 4), 0, 1);
        if (strength < 0.03) return;
        var depth = warm ? 320 : 140;
        var color = warm ? Ink.Orange : Ink.Blue;
        var layer = new StreamGeometry();
        using (var context = layer.Open())
        {
            context.BeginFigure(view.P(0, 0), true);
            for (var km = 0.0; km <= CoastView.Right + 0.01; km += 1) context.LineTo(view.P(km, Ground(km) + (depth * Smooth(-1, 6, km))));
            for (var km = CoastView.Right; km >= -0.01; km -= 1) context.LineTo(view.P(km, Ground(km)));

            context.EndFigure(true);
        }
        var brush = SpaceDrawing.LinearGradient(WithAlpha(color, 0), WithAlpha(color, 0.32 * strength), 90);
        dc.DrawGeometry(brush, null, layer);

        // Shimmer rising off the hot ground.
        if (!warm) return;
        var pen = MakePen(new SolidColorBrush(WithAlpha(Ink.Orange, 0.5 * strength)), 1.2);
        for (var i = 0; i < 7; i++)
        {
            var km = 4 + (i * 6.3);
            var rise = Frac((_t * 0.35) + (i * 0.37));
            var baseY = view.Y(Ground(km) + 30 + (rise * 220));
            var x = view.X(km);
            var wave = new StreamGeometry();
            using (var context = wave.Open())
            {
                context.BeginFigure(new Point(x, baseY), false);
                for (var k = 1; k <= 6; k++) context.LineTo(new Point(x + (Math.Sin(k * 1.4) * 3), baseY - (k * 4)));

                context.EndFigure(false);
            }
            dc.PushOpacity(Math.Sin(Math.PI * rise));
            dc.DrawGeometry(null, pen, wave);
            dc.Pop();
        }
    }

    /// <summary>Houses behind the beach, trees inland and a factory chimney.</summary>
    private void DrawTown(PaintContext dc, CoastView view, double night)
    {
        double[] houses = [2.6, 3.7, 4.9, 6.2, 7.6];
        for (var i = 0; i < houses.Length; i++)
        {
            var at = view.P(houses[i], Ground(houses[i]));
            var w = 13 + (i % 2 * 4);
            var h = 11 + (i % 3 * 3);
            dc.DrawRectangle(night > 0.5 ? Ink.HouseNight : Ink.House, null, new Rect(at.X - (w / 2), at.Y - h, w, h));
            dc.DrawGeometry(Ink.Roof, null, Polygon(new Point(at.X - (w / 2) - 2, at.Y - h), new Point(at.X, at.Y - h - 7), new Point(at.X + (w / 2) + 2, at.Y - h)));
            if (night > 0.3)
            {
                dc.PushOpacity(night);
                dc.DrawRectangle(Ink.Window, null, new Rect(at.X - 3, at.Y - h + 4, 4, 4));
                dc.Pop();
            }
        }

        double[] trees = [9.5, 11, 17, 20.5, 24, 28.5, 32, 39, 44];
        foreach (var km in trees)
        {
            var at = view.P(km, Ground(km));
            dc.DrawRectangle(Ink.Trunk, null, new Rect(at.X - 1.5, at.Y - 9, 3, 9));
            dc.DrawEllipse(night > 0.5 ? Ink.LeavesNight : Ink.Leaves, null, new Point(at.X, at.Y - 14), 7, 8);
        }

        var chimney = view.P(ChimneyKm, Ground(ChimneyKm));
        dc.DrawRectangle(Ink.Chimney, null, new Rect(chimney.X - 3, chimney.Y - ChimneyHeight, 6, ChimneyHeight));
        dc.DrawRectangle(Ink.House, null, new Rect(chimney.X - 16, chimney.Y - 12, 22, 12));
    }

    private const double ChimneyKm = 13.5;
    private const double ChimneyHeight = 36;

    /// <summary>Smoke from the chimney, carried by the wind near the ground: inland by day, out to sea by night, straight up in a calm.</summary>
    private void DrawSmoke(PaintContext dc, CoastView view, double wind)
    {
        var top = view.P(ChimneyKm, Ground(ChimneyKm)) - new Vector(0, ChimneyHeight);
        var drift = Math.Clamp(wind, -3, 6) * 13;
        var rise = 30 + (40 * (1 - Math.Min(1, Math.Abs(wind) / 3)));
        _targets.Add("smoke", top + new Vector(drift * 0.45, -rise * 0.6), 16);
        for (var i = 0; i < 14; i++)
        {
            var a = Frac((_t * 0.32) + (i / 14.0));
            var at = top + new Vector((drift * a) + (Math.Sin((a * 7) + i) * 2), -rise * Math.Sqrt(a));
            dc.PushOpacity((1 - a) * 0.85);
            dc.DrawEllipse(Ink.Smoke, null, at, 3 + (a * 9), 3 + (a * 7));
            dc.Pop();
        }
    }

    /// <summary>A flag on the beach that streams with the wind and hangs in a calm.</summary>
    private void DrawFlag(PaintContext dc, CoastView view, double wind)
    {
        var foot = view.P(0.7, Ground(0.7));
        var top = foot - new Vector(0, 36);
        dc.DrawLine(Ink.Pole, foot, top);
        _targets.Add("flag", top + new Vector(wind >= 0 ? 8 : -8, 8), 14);
        var strength = Math.Clamp(Math.Abs(wind) / 4, 0, 1);
        var direction = wind >= 0 ? 1 : -1;
        var length = 18;
        var droop = ((1 - strength) * 12) + (Math.Sin(_t * 9) * 1.5 * strength);
        var tip = top + new Vector(direction * length * (0.35 + (0.65 * strength)), droop);
        var flag = Polygon(top, tip, tip + new Vector(0, 8 - (droop * 0.2)), top + new Vector(0, 10));
        dc.DrawGeometry(Ink.Flag, null, flag);
    }

    /// <summary>A sailing boat bobbing on the waves, its sail filled toward where the wind blows.</summary>
    private void DrawBoat(PaintContext dc, CoastView view, double km, double wind, double phase)
    {
        var at = view.P(km, 0) + new Vector(0, Math.Sin((_t * 1.6) + phase) * 1.2);
        _targets.Add("boat", at + new Vector(0, -13), 15);
        dc.DrawGeometry(Ink.Hull, null, Polygon(at + new Vector(-11, -4), at + new Vector(11, -4), at + new Vector(7, 1), at + new Vector(-7, 1)));
        var mastTop = at + new Vector(0, -28);
        var mastFoot = at + new Vector(0, -5);
        dc.DrawLine(Ink.Mast, mastFoot, mastTop);
        var direction = wind >= 0 ? 1 : -1;
        var fill = Math.Clamp(Math.Abs(wind) / 3, 0.25, 1);
        var belly = mastTop + new Vector(direction * 13 * fill, 15);
        dc.DrawGeometry(Ink.Sail, null, Polygon(mastTop + new Vector(direction * 1, 1), belly, mastFoot + new Vector(direction * 2, -1)));
    }

    #endregion

    #region the breeze

    /// <summary>
    /// The pressure surfaces over the coast: by day they bend down over the land near the ground (low pressure there) and up
    /// over it aloft (high), the other way over the sea; at night everything turns round. Exaggerated many times.
    /// </summary>
    private void DrawCoastPressure(PaintContext dc, CoastView view, double hour)
    {
        var contrast = BreezeModel.Contrast(hour - BreezeModel.Lag, Sky);
        var amount = Math.Clamp(contrast / 7, -1, 1);
        var bend = 34 * amount;
        double[] levels = [250, 650, 1050, 1450];
        var width = view.Stage.Width;
        foreach (var z in levels)
        {
            var shape = -Math.Cos(Math.PI * z / 1700);
            var line = new StreamGeometry();
            using (var context = line.Open())
            {
                for (var x = 0.0; x <= width + 1; x += 6)
                {
                    var km = CoastView.Left + (x / width * (CoastView.Right - CoastView.Left));
                    var land = Smooth(-8, 8, km);
                    var y = view.Y(z) - (bend * shape * land) + (0.4 * bend * shape * (1 - land));
                    if (x == 0) context.BeginFigure(new Point(x, y), false);
                    else context.LineTo(new Point(x, y));
                }

                context.EndFigure(false);
            }
            if (Glows("isobars")) dc.DrawGeometry(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds), line);
            dc.DrawGeometry(null, Ink.Isobar, line);
        }

        if (ShowLabels)
        {
            AddBoxTarget("isobars", DrawTextRight(dc, Loc("Breeze_Isobar"), new Point(width - 10, view.Y(1450) - (bend * 0.9) - 10), Ink.Text, 10.5));
        }
    }

    /// <summary>"High" and "low" at the ground and aloft over the sea and the land, drawn over the air flow.</summary>
    private void DrawCoastPressureMarks(PaintContext dc, CoastView view, double hour)
    {
        var contrast = BreezeModel.Contrast(hour - BreezeModel.Lag, Sky);
        var opacity = Math.Clamp(Math.Abs(contrast) / 2.5, 0, 1);
        var day = contrast >= 0;
        DrawPressureMark(dc, view.P(-15, 1750), !day, 16, opacity);
        DrawPressureMark(dc, view.P(24, 1750), day, 16, opacity);
        DrawPressureMark(dc, view.P(-15, 120), day, 18, opacity);
        DrawPressureMark(dc, view.P(24, Ground(24) + 140), !day, 18, opacity);
    }

    /// <summary>
    /// The four arrows of the cell (along the ground, up, back aloft, down), as thick as the breeze is strong, and what to
    /// call each of them.
    /// </summary>
    private void DrawCellArrows(PaintContext dc, CoastView view, CoastCellShape cell, double wind)
    {
        var strength = Math.Clamp(cell.Strength, 0, 1);
        if (strength < 0.08)
        {
            if (ShowLabels) AddBoxTarget("calm", DrawPill(dc, Loc("Breeze_Calm"), view.P(0, 260), Ink.Gold, 12));
            return;
        }

        void Arrow(string id, Point from, Point control, Point to, Color color, double width, double alpha)
        {
            DrawCurvedArrow(dc, from, control, to, color, width, alpha);
            if (alpha > 0.3) _targets.Add(id, CurveMiddle(from, control, to), 10 + width);
        }

        var thickness = 2 + (4 * strength);
        var opacity = Math.Min(1, strength * 2);
        var from = cell.From;
        var to = cell.To;
        if (cell.Sign > 0)
        {
            var low = 230.0;
            var high = 1450.0;
            var surfaceFrom = view.P(from * 0.6, low);
            var surfaceTo = view.P(to * 0.7, Ground(to * 0.7) + low);
            Arrow("seabreeze", surfaceFrom, view.P(to * 0.05, low - 40), surfaceTo, Ink.Cyan, thickness, opacity);
            Arrow("rising", view.P(to * 0.85, Ground(to * 0.85) + 380), view.P(to * 0.98, 900), view.P(to * 0.88, 1300), Ink.Orange, thickness * 0.8, opacity);
            Arrow("return", view.P(to * 0.7, high), view.P(to * 0.05, high + 60), view.P(from * 0.55, high), Ink.Text, thickness * 0.7, opacity * 0.8);
            Arrow("sinking", view.P(from * 0.75, 1300), view.P(from * 0.88, 800), view.P(from * 0.75, 400), Ink.Blue, thickness * 0.8, opacity);

            if (!ShowLabels) return;
            AddBoxTarget("seabreeze", DrawPill(dc, LocFormat("Breeze_SeaBreeze_Label", Number(wind, "0.0")), view.P(from * 0.25, low + 330), Ink.Cyan, 12));
            AddBoxTarget("rising", DrawPill(dc, Loc("Breeze_Rising"), view.P(to * 0.88, 1300) + new Vector(0, -24), Ink.Orange, 11));
            AddBoxTarget("return", DrawPill(dc, Loc("Breeze_Return"), view.P(to * 0.1, high + 220), Ink.Text, 11));
            AddBoxTarget("sinking", DrawPill(dc, Loc("Breeze_Sinking"), view.P(from * 0.75, 1500), Ink.Blue, 11));
            return;
        }

        var lowNight = 90.0;
        var highNight = 540.0;
        Arrow("landbreeze", view.P(to * 0.6, Ground(to * 0.6) + lowNight), view.P(from * 0.1, lowNight - 20), view.P(from * 0.7, lowNight), Color.FromRgb(0xC8, 0xB8, 0xFF), thickness, opacity);
        Arrow("rising", view.P(from * 0.82, 140), view.P(from * 0.95, 330), view.P(from * 0.85, 480), Ink.Orange, thickness * 0.7, opacity);
        Arrow("return", view.P(from * 0.55, highNight), view.P(to * 0.1, highNight + 30), view.P(to * 0.6, Ground(to * 0.6) + highNight), Ink.Text, thickness * 0.6, opacity * 0.8);
        Arrow("sinking", view.P(to * 0.78, Ground(to * 0.78) + 480), view.P(to * 0.9, Ground(to * 0.9) + 300), view.P(to * 0.75, Ground(to * 0.75) + 150), Ink.Blue, thickness * 0.7, opacity);

        if (!ShowLabels) return;
        AddBoxTarget("landbreeze", DrawPill(dc, LocFormat("Breeze_LandBreeze_Label", Number(-wind, "0.0")), view.P(to * 0.2, 260), Color.FromRgb(0xC8, 0xB8, 0xFF), 12));
        AddBoxTarget("rising", DrawPill(dc, Loc("Breeze_Rising"), view.P(from * 0.85, 700), Ink.Orange, 11));
        AddBoxTarget("sinking", DrawPill(dc, Loc("Breeze_Sinking"), view.P(to * 0.8, Ground(to * 0.8) + 700), Ink.Blue, 11));
    }

    /// <summary>
    /// Cumulus where the sea breeze's front pushes the air up (only under a clear sky), and by night a few showers far out
    /// where the land breeze meets the sea air.
    /// </summary>
    private void DrawCoastClouds(PaintContext dc, CoastView view, double hour, CoastCellShape cell, bool overcast, double night)
    {
        var wind = BreezeModel.ShoreWind(hour, Sky);
        var front = BreezeModel.FrontDistance(hour, Sky);
        if (front > 0)
        {
            var x = view.X(front);
            var ground = view.Y(Ground(front));
            dc.DrawLine(Ink.Front, new Point(x, ground - 2), new Point(x, view.Y(1150)));
            if (!overcast)
            {
                var grow = Math.Clamp((wind - 1) / 3.5, 0, 1);
                DrawCumulus(dc, new Point(x - 6, view.Y(1150)), 40 + (60 * grow), 14 + (46 * grow), Math.Min(1, grow * 1.6), (int)front, night);
                if (grow > 0.2) _targets.Add("front", new Point(x - 6, view.Y(1150) - ((14 + (46 * grow)) / 2)), 12 + (20 * grow));
            }

            if (ShowLabels) AddBoxTarget("front", DrawPill(dc, LocFormat("Breeze_Front_Label", Number(front, "0")), new Point(x, view.Y(1150) + 22), Ink.Gold, 11));
        }

        if (cell.Sign < 0 && !overcast && cell.Strength > 0.3)
        {
            var x = view.X(cell.From);
            DrawCumulus(dc, new Point(x, view.Y(800)), 46, 22, (cell.Strength - 0.3) * 1.3, 3, night);
            if (cell.Strength > 0.45) _targets.Add("shower", new Point(x, view.Y(800) - 12), 22);
        }
    }

    /// <summary>A heaped cloud from a flat base (at <paramref name="baseCenter"/>) upward, its balls merged into one.</summary>
    private static void DrawCumulus(PaintContext dc, Point baseCenter, double width, double height, double opacity, int seed, double night)
    {
        if (opacity <= 0.02 || height < 4) return;
        var random = new Random(seed);
        var balls = new List<(Point Center, double R)>();
        var rows = Math.Max(1, (int)(height / 16));
        for (var row = 0; row < rows; row++)
        {
            var f = rows == 1 ? 0 : row / (double)(rows - 1);
            var r = Math.Clamp(height / (rows + 0.5), 8, 22) * (1 + (0.3 * f));
            var half = Math.Max(r * 0.3, (width / 2 * (1 - (0.6 * f * f))) - (r * 0.6));
            var y = baseCenter.Y - (f * (height - r)) - (r * 0.6);
            var n = Math.Max(1, (int)Math.Round(half * 2 / (r * 1.1)));
            for (var j = 0; j <= n; j++)
            {
                var x = baseCenter.X + ((((double)j / n * 2) - 1) * half) + ((random.NextDouble() - 0.5) * r * 0.4);
                balls.Add((new Point(x, y), r * (0.9 + (0.25 * random.NextDouble()))));
            }
        }

        var top = balls.Min(b => b.Center.Y - b.R);
        var light = Lerp(Color.FromRgb(0xFA, 0xFB, 0xFD), Color.FromRgb(0x5A, 0x62, 0x74), night);
        var shade = Lerp(Color.FromRgb(0xB8, 0xC1, 0xCD), Color.FromRgb(0x30, 0x36, 0x44), night);
        var core = new ImmutableLinearGradientBrush(
        [
            new ImmutableGradientStop(0, light),
            new ImmutableGradientStop(1, shade),
        ],
            startPoint: new RelativePoint(0, top, RelativeUnit.Absolute),
            endPoint: new RelativePoint(0, baseCenter.Y, RelativeUnit.Absolute));
        dc.PushOpacity(Math.Clamp(opacity, 0, 1));
        dc.PushClip(new RectangleGeometry(new Rect(baseCenter.X - width, top - 30, width * 2, baseCenter.Y - top + 30)));
        foreach (var (center, r) in balls) dc.DrawEllipse(Ink.CloudRim, null, center, r * 1.15, r * 1.08);
        foreach (var (center, r) in balls) dc.DrawEllipse(core, null, center, r * 0.95, r * 0.86);
        dc.Pop();
        dc.Pop();
    }

    #endregion

    #region readings

    /// <summary>A thermometer on the land and one on a buoy at sea, with their readings.</summary>
    private void DrawThermometers(PaintContext dc, CoastView view, double hour)
    {
        var land = BreezeModel.LandTemperature(hour, Sky);
        var sea = BreezeModel.SeaTemperature(hour, Sky);
        var landFoot = view.P(26, Ground(26));
        DrawThermometer(dc, landFoot + new Vector(0, -6), 34, land);
        var seaFoot = view.P(-21, 0) + new Vector(0, Math.Sin(_t * 1.6) * 1.2);
        dc.DrawRectangle(Ink.Flag, null, new Rect(seaFoot.X - 5, seaFoot.Y - 6, 10, 6));
        DrawThermometer(dc, seaFoot + new Vector(0, -12), 30, sea);

        var landColor = land > sea ? Ink.Orange : Ink.Cyan;
        AddBoxTarget("land", DrawPill(dc, LocFormat("Breeze_Land_Reading", Number(land, "0.0")), landFoot + new Vector(0, -64), landColor, 12));
        AddBoxTarget("sea", DrawPill(dc, LocFormat("Breeze_Sea_Reading", Number(sea, "0.0")), seaFoot + new Vector(0, -62), Ink.Cyan, 12));
    }

    /// <summary>Heights up the left edge and a ten-kilometre bar on the ground, with the vertical exaggeration.</summary>
    private void DrawCoastScale(PaintContext dc, CoastView view)
    {
        for (var z = 500; z <= 1500; z += 500)
        {
            var y = view.Y(z);
            dc.DrawLine(Ink.Tick, new Point(0, y), new Point(7, y));
            DrawText(dc, $"{Number(z, "N0")} m", new Point(10, y), Ink.Dim, 10);
        }

        var barY = view.GroundY + ((view.Stage.Bottom - view.GroundY) * 0.62);
        var right = view.Stage.Right - 24;
        var left = right - (10 * view.PerKm);
        dc.DrawLine(Ink.Axis, new Point(left, barY), new Point(right, barY));
        dc.DrawLine(Ink.Axis, new Point(left, barY - 4), new Point(left, barY + 4));
        dc.DrawLine(Ink.Axis, new Point(right, barY - 4), new Point(right, barY + 4));
        var scale = DrawTextRight(dc, LocFormat("Breeze_Scale", Number(10, "0")), new Point(left - 8, barY), Ink.Text, 10.5);
        AddBoxTarget("scale", scale.Union(new Rect(left, barY - 4, right - left, 8)));
    }

    /// <summary>The time of day, large, at the top right, with what the wind at the beach is doing.</summary>
    private void DrawCoastClock(PaintContext dc, double width, Rect stage, double hour, double wind)
    {
        var right = width - 22;
        var y = Math.Max(30, stage.Top - 70);
        DrawTextRight(dc, Clock(hour), new Point(right, y), Ink.Text, 30);
        var phase = BreezeModel.Phase(hour, Sky);
        var color = phase switch
        {
            CoastPhase.LandBreeze => Color.FromRgb(0xC8, 0xB8, 0xFF),
            CoastPhase.SeaBreeze or CoastPhase.SeaBreezeBuilding or CoastPhase.SeaBreezeFading => Ink.Cyan,
            _ => Ink.Gold,
        };
        DrawTextRight(dc, Loc($"Breeze_Coast_{phase}_Title"), new Point(right, y + 30), color, 13, bold: true);
    }

    #endregion
}
