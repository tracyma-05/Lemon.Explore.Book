using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Typhoon;

/// <summary>
/// Engine mode: a slice through a typhoon, 600 km across and 18 km up (heights stretched). Warm, moist air spirals in
/// over the sea, shoots up the eyewall where its water vapour condenses and gives off heat (the warm core), spreads out
/// at the top as a cirrus shield and slowly sinks far out: a heat engine running on warm sea. With landfall, the coast
/// slides away behind the storm, the air coming in is dry and the storm runs down.
/// </summary>
public sealed partial class TyphoonDiagram
{
    /// <summary>The slice reaches this far each side of the centre, km.</summary>
    private const double SliceRadius = 300;

    /// <summary>And this high, km.</summary>
    private const double SliceTop = 18;

    /// <summary>The storm's forward speed over land, km/h (the coast slides back by this much an hour).</summary>
    private const double InlandSpeed = 20;

    private readonly List<Parcel> _parcels = [];

    private sealed class Parcel
    {
        public int Side;
        public double Outer;
        public double Phase;
        public double Rate;
        public bool InEye;
    }

    private readonly record struct EngineView(Rect Slice, double Sea, double Bottom)
    {
        public double X(double km) => Slice.Left + ((km + SliceRadius) / (2 * SliceRadius) * Slice.Width);

        public double Y(double z) => Sea - (z / SliceTop * (Sea - Slice.Top));

        public Point P(double km, double z) => new(X(km), Y(z));

        public double PxPerKm => Slice.Width / (2 * SliceRadius);
    }

    private EngineView EngineLayout()
    {
        var stage = Stage();
        var slice = new Rect(stage.Left + 64, stage.Top + 6, Math.Max(200, stage.Width - 64 - 28), Math.Max(100, stage.Height - 60));
        return new EngineView(slice, slice.Bottom, stage.Bottom - 6);
    }

    /// <summary>The storm's strongest wind now: what the sea allows (90% of it), run down after landfall.</summary>
    internal static double EngineWind(double seaTemperature, bool landfall, double hours)
    {
        var start = 0.9 * StormModel.PotentialIntensity(seaTemperature);
        if (start <= 0) return 0;
        return landfall ? StormModel.AfterLandfall(start, hours) : start;
    }

    private double CurrentEngineWind => EngineWind(SeaTemperature, Landfall, _landfallHours);

    /// <summary>The coast, km from the centre (negative: behind the storm), or null while over the open sea.</summary>
    private double? Coast => Landfall ? -_landfallHours * InlandSpeed : null;

    /// <summary>The storm's shape now: radius of the eyewall, height of its top, how strong it looks (0…1).</summary>
    private (double Wall, double Top, double Strength) EngineShape()
    {
        var wind = CurrentEngineWind;
        var pressure = StormModel.CentralPressure(Math.Max(15, wind));
        var wall = 1.3 * StormModel.RadiusOfMaxWind(pressure);
        var strength = Smooth(12, 70, wind);
        return (wall, 8 + (7 * strength), strength);
    }

    private void StepEngine(double step)
    {
        if (_parcels.Count == 0)
        {
            for (var i = 0; i < 230; i++)
            {
                _parcels.Add(new Parcel
                {
                    Side = i % 2 == 0 ? 1 : -1,
                    Outer = 120 + (170 * Hash(i, 1.7)),
                    Phase = Hash(i, 2.9),
                    Rate = 0.8 + (0.4 * Hash(i, 4.1)),
                    InEye = i % 23 == 0,
                });
            }
        }

        if (step <= 0) return;
        var (_, _, strength) = EngineShape();
        foreach (var p in _parcels) p.Phase = Frac(p.Phase + (step * p.Rate * (0.02 + (0.05 * strength))));
    }

    /// <summary>
    /// Where a parcel is on its loop (km from the centre, km up) and which leg: 0 in along the sea, 1 up the eyewall,
    /// 2 out at the top, 3 slowly down far out; parcels in the eye just sink.
    /// </summary>
    private static (double R, double Z, int Leg, double T) ParcelAt(Parcel p, double wall, double top)
    {
        if (p.InEye)
        {
            var sink = p.Phase;
            return (wall * 0.35 * (0.4 + (0.6 * Hash((int)(p.Outer * 10), 3.3))), top - 2 - (sink * (top - 4)), 4, sink);
        }

        // The legs' shares of the loop: going in and going out take a while, the climb is quick, the sinking slow.
        double[] shares = [0.24, 0.12, 0.22, 0.42];
        var phase = p.Phase;
        var leg = 0;
        while (leg < 3 && phase > shares[leg])
        {
            phase -= shares[leg];
            leg++;
        }

        var t = Math.Clamp(phase / shares[leg], 0, 1);
        var foot = wall * 1.05;
        var head = foot + (0.9 * top);
        var far = p.Outer;
        return leg switch
        {
            0 => (Mix(far, foot, t), 0.4 + (0.8 * t * t), 0, t),
            1 => (Mix(foot, head, t * t), Mix(1.2, top - 1, t), 1, t),
            2 => (Mix(head, far + 15, Math.Sqrt(t)), Mix(top - 1, top - 2.5, t), 2, t),
            _ => (Mix(far + 15, far, t), Mix(top - 2.5, 0.4, t), 3, t),
        };
    }

    private void DrawEngineMode(PaintContext dc, double width, double height)
    {
        var view = EngineLayout();
        var (wall, top, strength) = EngineShape();
        var wind = CurrentEngineWind;

        dc.DrawRectangle(EngineInk.Sky, null, new Rect(0, 0, width, height));
        DrawSurface(dc, view, wind);

        if (wind <= 0)
        {
            DrawScatteredShowers(dc, view);
            DrawEngineAxes(dc, view);
            if (ShowLabels) AddBoxTarget("showers", DrawPill(dc, LocFormat("Typhoon_Engine_TooCool", Number(StormModel.GenesisSea, "0.0")), new Point(view.X(0), view.Y(13)), Ink.Cyan, 12));
            return;
        }

        // The warm core: the heat let go as the water vapour condenses, warmest high up over the eye.
        var core = view.P(0, 0.5 * top);
        var coreBrush = SpaceDrawing.RadialGradient(
            (Color.FromArgb((byte)(130 * strength), 0xFF, 0x9A, 0x4A), 0),
            (Color.FromArgb(0, 0xFF, 0x9A, 0x4A), 1));
        dc.DrawEllipse(coreBrush, null, core, Math.Max(40, wall * 3 * view.PxPerKm), (view.Sea - view.Y(top)) * 0.5);

        DrawStormClouds(dc, view, wall, top, strength);
        DrawRain(dc, view, wall, strength);
        DrawParcels(dc, view, wall, top);
        DrawEngineAxes(dc, view);
        if (ShowLabels) DrawEngineLabels(dc, view, wall, top, wind);
    }

    /// <summary>The sea (its colour by its temperature, its waves by the wind) and, after landfall, the land behind the storm.</summary>
    private void DrawSurface(PaintContext dc, EngineView view, double wind)
    {
        var seaColor = Quantize(Lerp(Color.FromRgb(0x1E, 0x4A, 0x8A), Color.FromRgb(0x2A, 0x7A, 0x9A), Smooth(24, 31, SeaTemperature)));
        var surface = new List<Point>();
        var pressure = StormModel.CentralPressure(Math.Max(15, wind));
        for (var x = view.Slice.Left; x <= view.Slice.Right; x += 3)
        {
            var km = ((x - view.Slice.Left) / view.PxPerKm) - SliceRadius;
            var w = wind > 0 ? StormModel.Wind(Math.Abs(km), pressure) : 3;
            var wave = Math.Sin((x * 0.25) - (_t * 4)) * Math.Min(5, 0.6 + (w * 0.08));
            surface.Add(new Point(x, view.Sea + wave));
        }

        var water = new List<Point>(surface) { new(view.Slice.Right, view.Bottom), new(view.Slice.Left, view.Bottom) };
        dc.DrawGeometry(FadedBrush(seaColor, 1), null, Shape([.. water]));
        dc.DrawGeometry(null, EngineInk.Foam, Line(surface));

        // Warm sea gives off water vapour: faint wisps rising.
        if (wind > 0)
        {
            for (var i = 0; i < 26; i++)
            {
                var x = view.Slice.Left + (Hash(i, 7.7) * view.Slice.Width);
                var rise = Frac((_t * 0.35) + Hash(i, 8.8));
                var y = view.Sea - 4 - (rise * 22);
                if (Coast is { } c && x > view.X(c)) continue;
                dc.DrawLine(FadedPen(Ink.Cyan, 0.35 * (1 - rise) * Smooth(26, 30, SeaTemperature), 1.2), new Point(x, y), new Point(x + 2, y - 6));
            }
        }

        if (Coast is { } coast)
        {
            // In the storm's frame the coast slides back as the storm moves inland: land ahead, sea behind.
            var x = Math.Max(view.Slice.Left, view.X(coast));
            var land = new Rect(x, view.Sea - 2, view.Slice.Right - x, view.Bottom - view.Sea + 2);
            dc.DrawRectangle(EngineInk.Land, null, land);
            if (land.Width > 12) _targets.AddBox("coast", land);
            dc.DrawLine(EngineInk.LandEdge, new Point(x, view.Sea - 2), new Point(view.Slice.Right, view.Sea - 2));
            for (var i = 0; i < 18; i++)
            {
                var tx = x + 10 + (i * (view.Slice.Right - x) / 18.0);
                if (tx > view.Slice.Right - 6) break;
                var sway = Math.Sin((_t * 3) + i) * Math.Min(6, wind * 0.12);
                dc.DrawLine(EngineInk.Trunk, new Point(tx, view.Sea - 2), new Point(tx + (sway * 0.5), view.Sea - 12));
                dc.DrawEllipse(EngineInk.Tree, null, new Point(tx + sway, view.Sea - 14), 4, 5);
            }

            if (ShowLabels && view.X(coast) > view.Slice.Left + 30)
            {
                AddBoxTarget("coast", DrawText(dc, Loc("Typhoon_Engine_Coast"), new Point(view.X(coast), view.Sea + 16), Ink.Text, 10.5, center: true));
            }
        }
    }

    /// <summary>A soft cloud of balls: all rims first, then the cores with one top-to-bottom shade (so the balls merge).</summary>
    private static void DrawPuffs(PaintContext dc, IReadOnlyList<(Point At, double R)> balls, double top, double bottom, double alpha)
    {
        if (balls.Count == 0) return;
        var rim = Solid((byte)(60 * alpha), 0xEE, 0xF1, 0xF6);
        foreach (var (at, r) in balls) dc.DrawEllipse(rim, null, at, r * 1.15, r * 1.15);
        var shade = new ImmutableLinearGradientBrush(
        [
            new ImmutableGradientStop(0, Color.FromArgb((byte)(240 * alpha), 0xF6, 0xF8, 0xFB)),
            new ImmutableGradientStop(1, Color.FromArgb((byte)(230 * alpha), 0xA8, 0xB2, 0xC2)),
        ],
            startPoint: new RelativePoint(0, top, RelativeUnit.Absolute),
            endPoint: new RelativePoint(0, bottom, RelativeUnit.Absolute));
        foreach (var (at, r) in balls) dc.DrawEllipse(shade, null, at, r, r);
    }

    private void DrawStormClouds(PaintContext dc, EngineView view, double wall, double top, double strength)
    {
        var px = view.PxPerKm;
        foreach (var side in new[] { -1, 1 })
        {
            // The outflow's cirrus shield: thin and fading outward.
            var anvil = new List<Point>();
            for (var i = 0; i <= 30; i++)
            {
                var r = (wall * 1.4) + (i / 30.0 * (SliceRadius - (wall * 1.4)));
                var thick = (2.4 - (1.6 * i / 30.0)) * (0.5 + (0.5 * strength));
                anvil.Add(view.P(side * r, top - 0.3 + (thick * 0.5)));
            }

            for (var i = 30; i >= 0; i--)
            {
                var r = (wall * 1.4) + (i / 30.0 * (SliceRadius - (wall * 1.4)));
                var thick = (2.4 - (1.6 * i / 30.0)) * (0.5 + (0.5 * strength));
                anvil.Add(view.P(side * r, top - 0.3 - (thick * 0.5)));
            }

            var cirrus = new ImmutableLinearGradientBrush(
            [
                new ImmutableGradientStop(0, Color.FromArgb(200, 0xEE, 0xF1, 0xF6)),
                new ImmutableGradientStop(1, Color.FromArgb(20, 0xEE, 0xF1, 0xF6)),
            ],
                startPoint: new RelativePoint(view.X(0), 0, RelativeUnit.Absolute),
                endPoint: new RelativePoint(view.X(side * SliceRadius), 0, RelativeUnit.Absolute));
            dc.DrawGeometry(cirrus, null, Shape([.. anvil]));

            // The eyewall: a wall of towering clouds leaning outward with height.
            var wallWidth = Math.Max(30, wall * 0.9 * px);
            DrawPuffs(dc, Tower(view, side, wall * 1.2, 1, top, wallWidth, lean: 2.2, salt: side * 1.3), view.Y(top), view.Sea, 1);

            // Rain bands further out: lower towers.
            foreach (var (distance, height, salt) in new[] { (95.0, 0.6, 1.0), (175.0, 0.45, 2.0), (250.0, 0.3, 3.0) })
            {
                var bandTop = Math.Max(3, height * top * (0.6 + (0.4 * strength)));
                DrawPuffs(dc, Tower(view, side, distance, 1.2, bandTop, 38, lean: 0.6, salt: (salt * 7) + side), view.Y(bandTop), view.Sea, 0.6 + (0.4 * strength));
            }
        }

        // Low cloud in the eye's floor.
        var eyeFloor = new List<(Point, double)>();
        for (var i = 0; i < 5; i++) eyeFloor.Add((view.P((i - 2) * wall * 0.3, 1.2 + (Hash(i, 9.3) * 0.5)), 5 + (Hash(i, 9.9) * 3)));
        DrawPuffs(dc, eyeFloor, view.Y(2), view.Y(0.8), 0.5);
    }

    /// <summary>Rain falling from the eyewall and the bands.</summary>
    private void DrawRain(PaintContext dc, EngineView view, double wall, double strength)
    {
        var bases = new[] { (wall * 1.4, 18.0, 1.0), (95.0, 10.0, 0.7), (175.0, 9.0, 0.55), (250.0, 8.0, 0.4) };
        var n = 0;
        foreach (var (distance, spread, weight) in bases)
        {
            foreach (var side in new[] { -1, 1 })
            {
                for (var i = 0; i < 12; i++, n++)
                {
                    var km = side * (distance + ((Hash(n, 5.1) - 0.5) * spread * 2));
                    var fall = Frac((_t * 1.6) + Hash(n, 6.2));
                    var z = 1.1 * (1 - fall);
                    var at = view.P(km, z);
                    _batch.Add(EngineInk.RainColor, 0.6 * weight * (0.4 + (0.6 * strength)), [at, at + new Vector(-1.5, -7)]);
                }
            }
        }

        _batch.Draw(dc, this, 1.1);
    }

    private void DrawParcels(PaintContext dc, EngineView view, double wall, double top)
    {
        var coast = Coast;
        foreach (var p in _parcels)
        {
            var (r, z, leg, t) = ParcelAt(p, wall, top);
            var x = p.Side * r;
            var back = new Parcel { Side = p.Side, Outer = p.Outer, InEye = p.InEye, Phase = Frac(p.Phase - 0.006) };
            var (r0, z0, _, _) = ParcelAt(back, wall, top);
            var from = view.P(p.Side * r0, z0);
            var to = view.P(x, z);
            if (((Vector)(to - from)).Length > 40) from = to;

            // In along the sea it is warm and moist (cyan); over land it comes in dry (brown); it gives off its heat
            // climbing the eyewall (white to orange), goes out icy (violet) and sinks dry (grey).
            var overLand = coast is { } c && x > c;
            var (color, alpha) = leg switch
            {
                0 => overLand ? (EngineInk.DryColor, 0.8) : (Ink.Cyan, 0.85),
                1 => (Quantize(Lerp(Ink.White, Ink.Orange, t)), 0.95),
                2 => (EngineInk.IcyColor, 0.8),
                4 => (Ink.Orange, 0.55),
                _ => (Ink.Dim, 0.18),
            };
            _batch.Add(color, alpha, [from, to]);
            dc.DrawEllipse(FadedBrush(color, alpha), null, to, leg == 3 ? 1.1 : 1.8, leg == 3 ? 1.1 : 1.8);
        }

        _batch.Draw(dc, this, 1.6);
    }

    /// <summary>Thunderstorms that cannot get organised over cool sea.</summary>
    private void DrawScatteredShowers(PaintContext dc, EngineView view)
    {
        for (var k = 0; k < 6; k++)
        {
            var km = -260 + (k * 104) + ((Hash(k, 1.1) - 0.5) * 40);
            var height = 3 + (4 * Hash(k, 2.2));
            var side = km < 0 ? -1 : 1;
            DrawPuffs(dc, Tower(view, side, Math.Abs(km), 1.2, height, 34 + (16 * Hash(k, 5.5)), lean: 0, salt: k + 11), view.Y(height), view.Sea, 0.85);
        }
    }

    /// <summary>
    /// A cumulus tower as overlapping puffs: a few across at each level, the top wider (a cauliflower head), leaning
    /// outward by <paramref name="lean"/> km per km of height.
    /// </summary>
    private static List<(Point At, double R)> Tower(EngineView view, int side, double distance, double baseZ, double topZ, double width, double lean, double salt)
    {
        var balls = new List<(Point, double)>();
        var height = view.Y(baseZ) - view.Y(topZ);
        var rows = Math.Max(2, (int)Math.Ceiling(height / (width * 0.42)));
        var n = 0;
        for (var row = 0; row <= rows; row++)
        {
            var f = row / (double)rows;
            var z = baseZ + (f * (topZ - baseZ));
            var head = Smooth(0.7, 1, f);
            var across = width * (0.8 + (0.45 * head));
            var count = 2 + (int)Math.Round(head);
            var centre = view.X(side * (distance + (lean * (z - baseZ))));
            for (var k = 0; k < count; k++, n++)
            {
                var offset = ((k + 0.5) / count) - 0.5;
                var x = centre + (offset * across * 0.8) + ((Hash(n, salt) - 0.5) * width * 0.25);
                var r = (width * (0.32 + (0.12 * head))) * (0.8 + (0.4 * Hash(n, salt + 0.5)));
                balls.Add((new Point(x, view.Y(z) + ((Hash(n, salt + 0.9) - 0.5) * width * 0.2)), r));
            }
        }

        return balls;
    }

    private void DrawEngineAxes(PaintContext dc, EngineView view)
    {
        for (var z = 0; z <= 15; z += 5)
        {
            var y = view.Y(z);
            dc.DrawLine(Ink.Grid, new Point(view.Slice.Left, y), new Point(view.Slice.Right, y));
            DrawTextRight(dc, Number(z, "0"), new Point(view.Slice.Left - 8, y), Ink.Dim, 10);
        }

        DrawTextRight(dc, Loc("Typhoon_Engine_Height"), new Point(view.Slice.Left - 8, view.Y(17)), Ink.Dim, 10);
        foreach (var km in new[] { -300, -200, -100, 0, 100, 200, 300 })
        {
            var x = view.X(km);
            DrawText(dc, Number(Math.Abs(km), "0"), new Point(x, view.Bottom - 8), Ink.Dim, 10, center: true);
        }

        DrawTextRight(dc, Loc("Typhoon_Engine_Distance"), new Point(view.Slice.Right, view.Bottom - 22), Ink.Dim, 10);
    }

    private void DrawEngineLabels(PaintContext dc, EngineView view, double wall, double top, double wind)
    {
        AddBoxTarget("eye", DrawPill(dc, Loc("Typhoon_Engine_Eye"), view.P(0, top * 0.62), Ink.Text, 11));
        AddBoxTarget("eyewall", DrawPill(dc, Loc("Typhoon_Engine_Eyewall"), view.P(wall + 40, top * 0.8), Ink.Text, 11));
        AddBoxTarget("outflow", DrawPill(dc, Loc("Typhoon_Engine_Outflow"), view.P(-190, top + 1.6), Ink.Violet, 11));
        AddBoxTarget("bands", DrawPill(dc, Loc("Typhoon_Engine_Bands"), view.P(175, top * 0.5), Ink.Text, 11));
        AddBoxTarget("inflow", DrawPill(dc, Loc("Typhoon_Engine_Inflow"), view.P(-170, 2.3), Ink.Cyan, 11));
        AddBoxTarget("warmcore", DrawPill(dc, Loc("Typhoon_Engine_WarmCore"), view.P(0, top * 0.38), Ink.Orange, 11));
        // The sea's label over open sea: left of the coast after landfall, hidden once the coast has gone.
        var seaRight = Coast is { } coast ? view.X(coast) : view.Slice.Right;
        if (seaRight - view.Slice.Left > 190)
        {
            AddBoxTarget("sea", DrawPill(dc, LocFormat("Typhoon_Engine_Sea", Number(SeaTemperature, "0.0")), new Point(Math.Min(view.X(-200), seaRight - 110), view.Sea + 16), Ink.Gold, 10.5));
        }

        AddBoxTarget("wind", DrawPill(dc, LocFormat("Typhoon_Engine_Wind", Number(wind, "0"), ForceText(StormModel.Beaufort(wind))), view.P(wall + 70, 4), Quantize(WindColor(wind)), 11));
    }

    private static class EngineInk
    {
        public static readonly IBrush Sky = SpaceDrawing.LinearGradient(Color.FromRgb(0x06, 0x09, 0x14), Color.FromRgb(0x1A, 0x2A, 0x44), 90);
        public static readonly IPen Foam = MakePen(Solid(150, 0xD8, 0xEC, 0xFF), 1.2);
        public static readonly IBrush Land = SpaceDrawing.LinearGradient(Color.FromRgb(0x4A, 0x6A, 0x3A), Color.FromRgb(0x3A, 0x2E, 0x22), 90);
        public static readonly IPen LandEdge = MakePen(Solid(200, 0x6A, 0x9A, 0x4A), 1.5);
        public static readonly IPen Trunk = MakePen(Solid(255, 0x5A, 0x40, 0x2A), 1.5);
        public static readonly IBrush Tree = Solid(255, 0x3E, 0x8A, 0x46);
        public static readonly Color RainColor = Color.FromRgb(0xA8, 0xC8, 0xF0);
        public static readonly Color DryColor = Color.FromRgb(0xC8, 0x9A, 0x6A);
        public static readonly Color IcyColor = Color.FromRgb(0xC8, 0xC0, 0xFF);
    }
}
