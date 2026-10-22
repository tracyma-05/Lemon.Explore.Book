using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Volcanoes;

/// <summary>
/// The rock cycle view: a slice of the Earth from the deep sea over a coast and a mountain range to a volcano. Rain wears
/// the granite peak down, a river carries the bits to the sea, they settle in layers that harden into sandstone, shale and
/// limestone; deeper down the layers are squeezed and heated into schist, deeper still they melt; the magma rises to cool
/// into granite under the mountains or erupts as basalt, and the mountains push old sea floor up again. Each step is a
/// label to click; a followed grain travels the journey chosen in the side panel.
/// </summary>
public sealed partial class VolcanoDiagram
{
    /// <summary>Seconds the followed grain takes from one stop to the next.</summary>
    private const double JourneyStepSeconds = 3.6;

    /// <summary>The ground's height (a share of the stage's height) along the slice: sea floor, coast, mountains, volcano.</summary>
    private static readonly (double U, double V)[] Ground =
    [
        (0, 0.53), (0.19, 0.53), (0.29, 0.43), (0.36, 0.395), (0.405, 0.34), (0.47, 0.322), (0.52, 0.23),
        (0.585, 0.095), (0.615, 0.16), (0.65, 0.13), (0.71, 0.255), (0.77, 0.27), (0.845, 0.115), (0.86, 0.125),
        (0.875, 0.115), (0.95, 0.27), (1.0, 0.28),
    ];

    private const double SeaLevel = 0.34;

    /// <summary>Where each of a journey's places is, as shares of the stage.</summary>
    private static readonly Dictionary<string, (double U, double V)> Places = new()
    {
        ["peak"] = (0.585, 0.115),
        ["slope"] = (0.548, 0.17),
        ["river"] = (0.47, 0.31),
        ["beach"] = (0.395, 0.355),
        ["sandlayer"] = (0.3, 0.56),
        ["folds"] = (0.505, 0.27),
        ["foothill"] = (0.475, 0.315),
        ["lava"] = (0.915, 0.215),
        ["lavasoil"] = (0.8, 0.245),
        ["deepsea"] = (0.1, 0.52),
        ["mudlayer"] = (0.1, 0.62),
        ["metamorphic"] = (0.25, 0.82),
        ["melt"] = (0.45, 0.945),
        ["pluton"] = (0.6, 0.55),
        ["reef"] = (0.33, 0.405),
        ["reefbed"] = (0.3, 0.44),
        ["limelayer"] = (0.45, 0.5),
        ["marble"] = (0.545, 0.47),
        ["cliff"] = (0.535, 0.2),
        ["chamber"] = (0.86, 0.6),
        ["vent"] = (0.86, 0.12),
    };

    /// <summary>Where the rock cycle's labels sit, as shares of the stage.</summary>
    private static readonly (RockProcess Process, double U, double V)[] ProcessLabels =
    [
        (RockProcess.Weathering, 0.5, 0.1),
        (RockProcess.Erosion, 0.445, 0.25),
        (RockProcess.Deposition, 0.25, 0.47),
        (RockProcess.Lithification, 0.13, 0.69),
        (RockProcess.Uplift, 0.465, 0.42),
        (RockProcess.Metamorphism, 0.33, 0.82),
        (RockProcess.Melting, 0.3, 0.95),
        (RockProcess.Crystallization, 0.69, 0.5),
        (RockProcess.Eruption, 0.8, 0.05),
    ];

    private static double GroundV(double u)
    {
        u = Math.Clamp(u, 0, 1);
        for (var i = 1; i < Ground.Length; i++)
        {
            if (u > Ground[i].U) continue;
            var (u0, v0) = Ground[i - 1];
            var (u1, v1) = Ground[i];
            var t = (u - u0) / Math.Max(1e-6, u1 - u0);
            t = (1 - Math.Cos(t * Math.PI)) / 2;
            return v0 + ((v1 - v0) * t);
        }

        return Ground[^1].V;
    }

    /// <summary>How far the old sea-floor layers are pushed up under the coast and the mountains' flank.</summary>
    private static double Lift(double u) => 0.31 * VolcanoModel.SmoothStep(0.36, 0.54, u);

    /// <summary>The top of layer <paramref name="k"/> (0 the youngest) of hardened sea-floor rock.</summary>
    private static double LayerV(int k, double u) => 0.535 + (0.045 * k) - Lift(u) + (0.008 * Math.Sin((u * 19) + k));

    /// <summary>The rock of layer <paramref name="k"/>: sandstone, shale, limestone in turn.</summary>
    private static (string Rock, Color Color) LayerRock(int k) => (k % 3) switch
    {
        0 => ("Sandstone", Ink.Sand),
        1 => ("Shale", Ink.Mud),
        _ => ("Limestone", Ink.Lime),
    };

    private const int LayerCount = 5;

    private void OnJourneyChanged() => InvalidateVisual();

    private void StepCycle(double dt)
    {
        if (JourneyId is null) return;
        var steps = VolcanoModel.JourneyOf(JourneyId).Steps.Count;
        Advance(dt / (JourneyStepSeconds * Math.Max(1, steps - 1)), wrap: false);
    }

    /// <summary>The followed grain's place now, the step it is on (the stop it is heading for) and how far along that step it is.</summary>
    internal static (int Step, double Along) JourneyPosition(Journey journey, double progress)
    {
        var segments = Math.Max(1, journey.Steps.Count - 1);
        var x = Math.Clamp(progress, 0, 1) * segments;
        var segment = Math.Min(segments - 1, (int)Math.Floor(x));
        return (segment + 1, x - segment);
    }

    private void DrawCycleMode(PaintContext dc, double now)
    {
        var s = Stage();
        Point P(double u, double v) => new(s.Left + (u * s.Width), s.Top + (v * s.Height));

        // Sky, then everything below the ground as one dark mass the layers are drawn into.
        var groundLine = new List<Point>();
        for (var u = 0.0; u <= 1.0001; u += 0.004) groundLine.Add(P(u, GroundV(u)));
        dc.DrawRoundedRectangle(Ink.Sky, null, new Rect(s.Left, s.Top, s.Width, s.Height * 0.5), 10, 10);
        var below = new List<Point>(groundLine) { P(1, 1), P(0, 1) };
        var earth = Shape(below);
        dc.DrawGeometry(FadedBrush(Ink.Rock, 1), null, earth);

        dc.PushClip(earth);
        DrawCycleUnderground(dc, s, P, now);
        dc.Pop();

        DrawSea(dc, s, P, now);
        DrawSurfaceThings(dc, s, P, now);
        dc.DrawGeometry(null, Ink.Surface, Line(groundLine));
        DrawCycleArrows(dc, P);
        DrawCycleLabels(dc, s, P, now);
        DrawFollowedGrain(dc, s, P, now);
    }

    private void DrawCycleUnderground(PaintContext dc, Rect s, Func<double, double, Point> P, double now)
    {
        // Loose young sediment on the sea floor, then the hardened layers below it, bent up under the coast.
        var young = new List<Point>();
        for (var u = 0.0; u <= 0.47; u += 0.01) young.Add(P(u, GroundV(u) - 0.01));
        for (var u = 0.47; u >= 0; u -= 0.01) young.Add(P(u, LayerV(0, u)));
        dc.DrawGeometry(FadedBrush(Color.FromRgb(0x9A, 0x8A, 0x6A), 0.9), null, Shape(young));

        for (var k = 0; k < LayerCount; k++)
        {
            var band = new List<Point>();
            for (var u = 0.0; u <= 0.6; u += 0.01) band.Add(P(u, LayerV(k, u)));
            for (var u = 0.6; u >= 0; u -= 0.01) band.Add(P(u, LayerV(k + 1, u)));
            var (_, color) = LayerRock(k);
            dc.DrawGeometry(FadedBrush(color, 0.85), FadedPen(Colors.Black, 0.25, 1), Shape(band));
        }

        // Older rock below, then the hot zone where rock changes, then where it melts.
        var meta = new Rect(P(0, 0.75), P(1, 0.89));
        dc.DrawRectangle(MetaBrush, null, meta);
        var foliation = FadedPen(Color.FromRgb(0xB0, 0xA0, 0xC8), 0.3, 1);
        for (var i = 0; i < 4; i++)
        {
            var points = new List<Point>();
            for (var u = 0.0; u <= 1.0001; u += 0.01) points.Add(P(u, 0.77 + (i * 0.03) + (0.006 * Math.Sin((u * 40) + i))));
            dc.DrawGeometry(null, foliation, Line(points));
        }

        var melt = new Rect(P(0, 0.89), P(1, 1));
        dc.DrawRectangle(MeltBrush, null, melt);
        for (var i = 0; i < 18; i++)
        {
            var u = SqueezeTexture.Hash(i, 91);
            var rise = ((now * 0.05) + SqueezeTexture.Hash(i, 92)) % 1;
            var p = P(u, 1 - (rise * 0.12));
            dc.DrawEllipse(SoftBrush(Ink.LavaHot, 0.5 * (1 - rise)), null, p, 9, 6);
        }

        // The granite body under the mountains, cooled from magma and now pushed up to the peak, with marble where it baked limestone.
        var pluton = new List<Point>();
        double HalfWidth(double v) => 0.026 + (0.075 * Math.Pow(Math.Clamp((v - 0.08) / 0.7, 0, 1), 0.8));
        for (var v = 0.08; v <= 0.78; v += 0.01) pluton.Add(P(0.6 - HalfWidth(v) + (0.004 * Math.Sin(v * 31)), v));
        for (var a = 0.0; a <= Math.PI; a += 0.15) pluton.Add(P(0.6 - (Math.Cos(a) * HalfWidth(0.78)), 0.78 + (Math.Sin(a) * 0.03)));
        for (var v = 0.78; v >= 0.08; v -= 0.01) pluton.Add(P(0.6 + HalfWidth(v) + (0.004 * Math.Sin(v * 27)), v));
        var plutonShape = Shape(pluton);

        // Marble: a baked rim where the limestone layer meets the hot granite.
        var aureole = new List<Point>();
        for (var v = 0.4; v <= 0.62; v += 0.01) aureole.Add(P(0.6 - HalfWidth(v) - 0.012, v));
        for (var v = 0.62; v >= 0.4; v -= 0.01) aureole.Add(P(0.6 - HalfWidth(v) + 0.004, v));
        dc.DrawGeometry(FadedBrush(Ink.Marble, 0.95), null, Shape(aureole));
        dc.DrawGeometry(_graniteBrush ??= MakeGraniteBrush(), FadedPen(Colors.Black, 0.3, 1), plutonShape);
        var feeder = new List<Point> { P(0.59, 0.86), P(0.6, 0.78), P(0.61, 0.78), P(0.62, 0.86) };
        dc.DrawGeometry(FadedBrush(Ink.LavaDark, 0.9), null, Shape(feeder));

        // Under the volcano: its chamber and the pipe up from the melt.
        var conduit = new List<Point>();
        for (var v = 0.89; v >= 0.12; v -= 0.01) conduit.Add(P(0.86 + (0.006 * Math.Sin(v * 20)), v));
        dc.DrawGeometry(null, FadedPen(Ink.Lava, 0.9, Math.Max(3, s.Width * 0.006), flat: true), Line(conduit));
        var chamber = P(0.86, 0.6);
        dc.DrawEllipse(_chamberBrush ??= new ImmutableRadialGradientBrush(
        [
            new ImmutableGradientStop(0, Ink.LavaHot),
            new ImmutableGradientStop(1, Ink.LavaDark),
        ],
            gradientOrigin: new RelativePoint(0.45, 0.4, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.5, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.5, RelativeUnit.Relative)),
            null, chamber, s.Width * 0.045, s.Height * 0.045);
        for (var i = 0; i < 3; i++)
        {
            var t = ((now * 0.12) + (i / 3.0)) % 1;
            dc.DrawEllipse(SoftBrush(Ink.LavaHot, 0.8), null, P(0.86, 0.88 - (t * 0.26)), 5, 7);
        }
    }

    private IBrush? _graniteBrush;

    private static readonly IBrush MetaBrush = SpaceDrawing.LinearGradient(Color.FromRgb(0x4A, 0x40, 0x5C), Color.FromRgb(0x5E, 0x38, 0x3A), 90);
    private static readonly IBrush MeltBrush = SpaceDrawing.LinearGradient(Color.FromRgb(0x8A, 0x26, 0x10), Ink.Lava, 90);
    private static readonly IBrush SeaBrush = SpaceDrawing.LinearGradient(Color.FromArgb(200, Ink.Sea.R, Ink.Sea.G, Ink.Sea.B), Color.FromArgb(230, Ink.SeaDeep.R, Ink.SeaDeep.G, Ink.SeaDeep.B), 90);

    /// <summary>Pink-grey with dark and white specks, like granite: a small bitmap tile, so the tiles meet without seams.</summary>
    private static IBrush MakeGraniteBrush()
    {
        const int n = 32;
        var pixels = new int[n * n];
        for (var i = 0; i < pixels.Length; i++)
        {
            var h = SqueezeTexture.Hash(i, 101);
            var color = h switch
            {
                < 0.07 => Color.FromRgb(0x2A, 0x24, 0x24),
                < 0.16 => Color.FromRgb(0xF4, 0xEE, 0xE8),
                < 0.3 => Color.FromRgb(0xD8, 0x8E, 0x82),
                _ => Lerp(Ink.Granite, Color.FromRgb(0xB0, 0x98, 0x90), SqueezeTexture.Hash(i, 102)),
            };
            pixels[i] = (255 << 24) | (color.R << 16) | (color.G << 8) | color.B;
        }

        return TileBrush(pixels, n, 64);
    }

    /// <summary>An image brush tiling an <paramref name="n"/> × <paramref name="n"/> picture every <paramref name="size"/> DIPs, unsmoothed.</summary>
    private static IBrush TileBrush(int[] pixels, int n, double size)
    {
        // An Avalonia image brush can't be told to scale nearest-neighbour: the tile is made at its drawn size instead,
        // each of its pixels repeated.
        var k = Math.Max(1, (int)Math.Round(size / n));
        var m = n * k;
        var tile = new int[m * m];
        for (var y = 0; y < m; y++)
        {
            for (var x = 0; x < m; x++) tile[(y * m) + x] = pixels[((y / k) * n) + (x / k)];
        }

        var bitmap = new WriteableBitmap(new PixelSize(m, m), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        SpaceDrawing.WritePixels(bitmap, tile, m, m);
        return new ImageBrush(bitmap) { TileMode = TileMode.Tile, DestinationRect = new RelativeRect(0, 0, size, size, RelativeUnit.Absolute) };
    }

    private void DrawSea(PaintContext dc, Rect s, Func<double, double, Point> P, double now)
    {
        var sea = new List<Point>();
        for (var u = 0.0; u <= 0.406; u += 0.004)
        {
            var wave = 0.004 * Math.Sin((u * 90) + (now * 1.6));
            sea.Add(P(u, SeaLevel + wave));
        }

        for (var u = 0.406; u >= 0; u -= 0.004) sea.Add(P(u, GroundV(u)));
        dc.DrawGeometry(SeaBrush, null, Shape(sea));

        // Mud and sand drifting out from the river mouth and settling.
        for (var i = 0; i < 40; i++)
        {
            var t = ((now * 0.07) + SqueezeTexture.Hash(i, 111)) % 1;
            var u = 0.4 - (t * (0.12 + (0.25 * SqueezeTexture.Hash(i, 112))));
            var floor = GroundV(u);
            var v = SeaLevel + 0.01 + ((floor - SeaLevel - 0.015) * Math.Pow(t, 0.7));
            dc.DrawRectangle(FadedBrush(SqueezeTexture.Hash(i, 113) < 0.4 ? Ink.Sand : Ink.Mud, 0.8), null, new Rect(P(u, v), new Size(2, 2)));
        }

        // A reef of shells and coral on the shelf.
        for (var i = 0; i < 9; i++)
        {
            var u = 0.315 + (i * 0.005);
            var p = P(u, GroundV(u));
            dc.DrawEllipse(FadedBrush(i % 2 == 0 ? Ink.Pink : Ink.Lime, 0.9), null, p + new Vector(0, -3), 3, 4);
        }
    }

    private void DrawSurfaceThings(PaintContext dc, Rect s, Func<double, double, Point> P, double now)
    {
        // The river from the mountains down to the coast, its water moving.
        var river = new List<Point>();
        for (var u = 0.66; u >= 0.4; u -= 0.004) river.Add(P(u, GroundV(u) - 0.006));
        var glow = Glows("river");
        dc.DrawGeometry(null, FadedPen(Ink.Blue, 0.9, 3.2), Line(river));
        var dash = new ImmutablePen(((IBrush)Solid(200, 0xC8, 0xE8, 0xFF)).ToImmutable(), 1.6, new ImmutableDashStyle([2, 6], -now * 6), PenLineCap.Flat, PenLineJoin.Miter);
        dc.DrawGeometry(null, dash, Line(river));
        if (glow) dc.DrawGeometry(null, ExploreTargets.GlowPen(now), Line(river));

        // Rain on the peaks.
        var cloud = P(0.6, 0.03);
        foreach (var (dx, dy, r) in new[] { (-22.0, 4.0, 14.0), (0.0, -2.0, 18.0), (22.0, 3.0, 13.0), (40.0, 6.0, 10.0) })
        {
            dc.DrawEllipse(SoftBrush(Color.FromRgb(0xB8, 0xC0, 0xCC), 0.9), null, cloud + new Vector(dx, dy), r * 1.4, r);
        }

        var rain = FadedPen(Color.FromRgb(0xA8, 0xD0, 0xFF), 0.55, 1);
        for (var i = 0; i < 24; i++)
        {
            var x = cloud.X - 30 + (SqueezeTexture.Hash(i, 121) * 75);
            var fall = ((now * 1.3) + SqueezeTexture.Hash(i, 122)) % 1;
            var ground = s.Top + (GroundV((x - s.Left) / s.Width) * s.Height);
            var y = cloud.Y + 12 + (fall * Math.Max(10, ground - cloud.Y - 12));
            dc.DrawLine(rain, new Point(x, y), new Point(x - 1.5, y + 6));
        }

        // Grass on the plain, snow-free peaks; the volcano's lava flow of basalt and a little gas.
        var lava = new List<Point>();
        for (var u = 0.875; u <= 0.95; u += 0.004) lava.Add(P(u, GroundV(u) - 0.004));
        dc.DrawGeometry(null, FadedPen(Ink.Crust, 1, 5), Line(lava));
        dc.DrawGeometry(null, FadedPen(Ink.Lava, 0.7 + (0.2 * Math.Sin(now * 3)), 2.2), Line(lava.Skip(lava.Count / 2)));
        for (var i = 0; i < 6; i++)
        {
            var t = ((now * 0.15) + (i / 6.0)) % 1;
            dc.DrawEllipse(SoftBrush(Ink.Steam, 0.35 * (1 - t)), null, P(0.86 + (t * 0.03), 0.11 - (t * 0.1)), 6 + (t * 16), 6 + (t * 16));
        }
    }

    /// <summary>Faint arrows round the cycle: down the river, into the layers, down to the heat, up as magma, up with the mountains.</summary>
    private void DrawCycleArrows(PaintContext dc, Func<double, double, Point> P)
    {
        var c = Color.FromRgb(0xFF, 0xFF, 0xFF);
        DrawCurvedArrow(dc, P(0.2, 0.55), P(0.18, 0.66), P(0.22, 0.76), c, 2, 0.3);
        DrawCurvedArrow(dc, P(0.38, 0.8), P(0.4, 0.86), P(0.4, 0.9), c, 2, 0.3);
        DrawCurvedArrow(dc, P(0.52, 0.92), P(0.58, 0.88), P(0.6, 0.82), c, 2, 0.3);
        DrawCurvedArrow(dc, P(0.78, 0.92), P(0.84, 0.86), P(0.85, 0.66), c, 2, 0.3);
        DrawCurvedArrow(dc, P(0.47, 0.5), P(0.48, 0.42), P(0.5, 0.33), c, 2.4, 0.45);
        DrawCurvedArrow(dc, P(0.555, 0.13), P(0.52, 0.19), P(0.49, 0.27), c, 2, 0.3);
    }

    private void DrawCycleLabels(PaintContext dc, Rect s, Func<double, double, Point> P, double now)
    {
        var journey = JourneyId is null ? null : VolcanoModel.JourneyOf(JourneyId);
        var active = journey is null ? (RockProcess?)null : journey.Steps[JourneyPosition(journey, Progress).Step].Process;
        foreach (var (process, u, v) in ProcessLabels)
        {
            var color = active == process ? Ink.Gold : Ink.Text;
            var box = DrawTargetPill(dc, Loc($"Volc_Process_{process}"), P(u, v), color, ProcessId(process), 12);
            if (active == process) dc.DrawRoundedRectangle(null, FadedPen(Ink.Gold, 0.9, 1.6), box, 8, 8);
        }

        // The rocks, where they are.
        void Rock(string id, string key, double u, double v, Color color)
        {
            if (ShowLabels) DrawTargetPill(dc, Loc(key), P(u, v), color, id, 10.5);
            else _targets.Add(id, P(u, v), 10);
        }

        Rock("rock_granite", "Volc_Obj_Granite", 0.6, 0.33, Ink.Granite);
        Rock("rock_basalt", "Volc_Obj_Basalt", 0.955, 0.2, Ink.Orange);
        Rock("rock_sandstone", "Volc_Obj_Sandstone", 0.06, LayerV(0, 0.06) + 0.022, Ink.Sand);
        Rock("rock_shale", "Volc_Obj_Shale", 0.06, LayerV(1, 0.06) + 0.022, Ink.Dim);
        Rock("rock_limestone", "Volc_Obj_Limestone", 0.06, LayerV(2, 0.06) + 0.022, Ink.Lime);
        Rock("rock_marble", "Volc_Obj_Marble", 0.522, 0.56, Ink.Marble);
        Rock("rock_schist", "Volc_Obj_Schist", 0.12, 0.82, Color.FromRgb(0xC8, 0xB8, 0xE8));
        Rock("rock_magma", "Volc_Obj_Magma", 0.72, 0.95, Ink.LavaHot);
        Rock("rock_sediment", "Volc_Obj_Sediment", 0.2, 0.555, Color.FromRgb(0xD8, 0xC8, 0xA8));
    }

    private void DrawFollowedGrain(PaintContext dc, Rect s, Func<double, double, Point> P, double now)
    {
        if (JourneyId is null) return;
        var journey = VolcanoModel.JourneyOf(JourneyId);
        var (step, along) = JourneyPosition(journey, Progress);
        Point PlaceAt(int i)
        {
            var (u, v) = Places.TryGetValue(journey.Steps[i].Place, out var p) ? p : (0.5, 0.5);
            return P(u, v);
        }

        // The way it has come, dotted.
        var trail = new List<Point>();
        for (var i = 0; i < step; i++) trail.Add(PlaceAt(i));
        var eased = along * along * (3 - (2 * along));
        var now0 = PlaceAt(step - 1);
        var next = PlaceAt(step);
        var here = now0 + ((next - now0) * eased);
        trail.Add(here);
        if (trail.Count > 1)
        {
            var pen = new ImmutablePen(((IBrush)Solid(200, Ink.Gold.R, Ink.Gold.G, Ink.Gold.B)).ToImmutable(), 2, new ImmutableDashStyle([1, 3], 0), PenLineCap.Flat, PenLineJoin.Miter);
            dc.DrawGeometry(null, pen, Line(trail));
        }

        for (var i = 0; i < journey.Steps.Count; i++)
        {
            var p = PlaceAt(i);
            dc.DrawEllipse(i <= step - 1 ? FadedBrush(Ink.Gold, 0.9) : null, FadedPen(Ink.Gold, 0.7, 1.2), p, 3.2, 3.2);
        }

        // The grain itself, glowing, and what it is now (it becomes the next thing when it arrives).
        var pulse = 0.5 + (0.5 * Math.Sin(now * 5));
        dc.DrawEllipse(SoftBrush(Ink.Gold, 0.5 + (0.3 * pulse)), null, here, 16, 16);
        dc.DrawEllipse(FadedBrush(Ink.Gold, 1), FadedPen(Colors.White, 0.9, 1.5), here, 5.5, 5.5);
        var state = along > 0.92 || Progress >= 1 ? journey.Steps[step].State : journey.Steps[step - 1].State;
        DrawTargetPill(dc, Loc($"Volc_State_{state}"), here + new Vector(0, -22), Ink.Gold, "grain", 11.5);
        _targets.Add("grain", here, 12);
    }
}
