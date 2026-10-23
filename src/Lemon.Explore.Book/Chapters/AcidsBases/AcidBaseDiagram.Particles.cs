using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.AcidsBases;

/// <summary>
/// The particles view: four bottles and a jug on a shelf, a beaker of water under them with a pH meter's probe in it, a
/// pH strip, and a magnifier showing the particles in the beaker. A clicked bottle lets a drop fall into the beaker
/// (held down, drop after drop); the jug waters the beaker down ten times.
/// </summary>
public sealed partial class AcidBaseDiagram
{
    /// <summary>Seconds a drop takes from a bottle's mouth to the beaker.</summary>
    private const double DropFlight = 0.55;

    /// <summary>Seconds the jug pours before the beaker counts as watered down.</summary>
    private const double PourSeconds = 0.9;

    /// <summary>The beaker's drawn capacity, mL.</summary>
    private const double BeakerCapacity = 200;

    private static readonly Reagent[] Bottles = [Reagent.StrongAcid, Reagent.WeakAcid, Reagent.StrongBase, Reagent.WeakBase, Reagent.Water];

    private readonly ParticleBox _beakerBox = new();
    private readonly List<(Reagent Reagent, double Age)> _drops = [];
    private readonly List<double> _ripples = [];

    /// <summary>Seconds the jug has been pouring (0: not pouring).</summary>
    private double _pour;

    /// <summary>The bottle held down, dripping again every so often; null when none is.</summary>
    private Reagent? _holding;

    private double _holdNext;

    private readonly record struct ParticlesLayout(Rect Left, Rect[] Bottles, Rect Beaker, Rect Liquid, Rect Meter, Rect Strip, Point Lens, double LensRadius);

    private ParticlesLayout LayOutParticles()
    {
        var stage = Stage();
        var leftWidth = Math.Min(stage.Width * 0.52, 560);
        var left = new Rect(stage.Left, stage.Top, leftWidth, stage.Height);

        // Shelf of bottles along the top.
        var shelfHeight = Math.Clamp(stage.Height * 0.25, 80, 118);
        var slot = left.Width / Bottles.Length;
        var bottleWidth = Math.Min(slot * 0.5, 46);
        var bottles = new Rect[Bottles.Length];
        for (var i = 0; i < Bottles.Length; i++)
        {
            var cx = left.Left + (slot * (i + 0.5));
            bottles[i] = new Rect(cx - (bottleWidth / 2), left.Top + 8, bottleWidth, shelfHeight - 30);
        }

        // The beaker under the shelf, the meter beside it, the strip below.
        var stripHeight = 16.0;
        var below = left.Top + shelfHeight + 34;
        var beakerHeight = Math.Clamp(left.Bottom - below - stripHeight - 78, 90, 300);
        // (on a phone 46% of the shelf can be under the 90 the beaker would like: the shelf wins)
        var beakerWidth = Math.Clamp(beakerHeight * 0.85, Math.Min(90, left.Width * 0.46), left.Width * 0.46);
        var beaker = new Rect(left.Left + (left.Width * 0.36) - (beakerWidth / 2), below, beakerWidth, beakerHeight);
        var fill = Math.Clamp(Beaker.VolumeMl / BeakerCapacity, 0.05, 0.97) * (beaker.Height - 10);
        var liquid = new Rect(beaker.Left + 4, beaker.Bottom - 4 - fill, beaker.Width - 8, fill);
        var meterWidth = Math.Min(118, left.Right - beaker.Right - 30);
        var meter = new Rect(beaker.Right + 26, beaker.Top + 6, Math.Max(80, meterWidth), 70);
        var strip = new Rect(left.Left + 18, beaker.Bottom + 44, left.Width - 36, stripHeight);

        // The magnifier fills the right.
        var right = new Rect(left.Right + 20, stage.Top, Math.Max(80, stage.Right - left.Right - 20), stage.Height);
        var radius = Math.Max(60, Math.Min(right.Width * 0.44, (right.Height - 90) / 2));
        var lens = new Point(right.Left + (right.Width / 2), right.Top + 30 + radius);
        return new ParticlesLayout(left, bottles, beaker, liquid, meter, strip, lens, radius);
    }

    private void StepParticles(double dt)
    {
        _beakerBox.SetTarget(AcidBaseModel.SpeciesOf(Beaker.Solution));
        _beakerBox.Step(dt);

        // (_holding is cleared when the button comes up or the pointer's capture is lost)
        if (_holding is { } held)
        {
            _holdNext -= dt;
            if (_holdNext <= 0)
            {
                _drops.Add((held, 0));
                _holdNext = 0.16;
            }
        }

        for (var i = _drops.Count - 1; i >= 0; i--)
        {
            var (reagent, age) = _drops[i];
            age += dt;
            if (age >= DropFlight)
            {
                _drops.RemoveAt(i);
                _ripples.Add(0);
                Run(DropCommand, reagent);
                continue;
            }

            _drops[i] = (reagent, age);
        }

        for (var i = _ripples.Count - 1; i >= 0; i--)
        {
            _ripples[i] += dt;
            if (_ripples[i] > 0.7) _ripples.RemoveAt(i);
        }

        if (_pour > 0)
        {
            _pour += dt;
            if (_pour >= PourSeconds)
            {
                _pour = 0;
                _ripples.Add(0);
                Run(DropCommand, Reagent.Water);
            }
        }
    }

    /// <summary>A bottle clicked or held: its first drop falls at once (the jug pours instead).</summary>
    private void StartDropping(Reagent reagent, bool hold)
    {
        if (reagent == Reagent.Water)
        {
            if (_pour <= 0) _pour = 0.001;
            return;
        }

        _drops.Add((reagent, 0));
        _holding = hold ? reagent : null;
        _holdNext = 0.4;
    }

    private static void Run(System.Windows.Input.ICommand? command, object parameter)
    {
        if (command is not null && command.CanExecute(parameter)) command.Execute(parameter);
    }

    private void DrawParticlesMode(PaintContext dc, double now)
    {
        var layout = LayOutParticles();
        var solution = Beaker.Solution;
        var species = AcidBaseModel.SpeciesOf(solution);
        var ph = species.Ph;

        // Shelf and bottles.
        var shelfY = layout.Bottles[0].Bottom + 2;
        dc.DrawRoundedRectangle(Ink.Wood, null, new Rect(layout.Left.Left, shelfY, layout.Left.Width, 7), 2, 2);
        for (var i = 0; i < Bottles.Length; i++) DrawBottle(dc, Bottles[i], layout.Bottles[i], now);

        // Beaker, liquid, ripples, the meter's probe.
        var liquidColor = AcidBaseModel.LiquidColor(BeakerIndicator, ph);
        DrawBeaker(dc, layout.Beaker, layout.Liquid, liquidColor, now);
        DrawMeter(dc, layout, ph, now);

        // Drops in flight and the jug's stream.
        foreach (var (reagent, age) in _drops)
        {
            var from = BottleMouth(layout, reagent);
            var to = new Point(layout.Liquid.Left + (layout.Liquid.Width * 0.45), layout.Liquid.Top);
            var t = age / DropFlight;
            var control = new Point((from.X + to.X) / 2, Math.Min(from.Y, to.Y) - 40);
            var at = Bezier(from, control, to, t);
            DrawDrop(dc, at, ReagentColor(reagent), 4.5);
        }

        if (_pour > 0) DrawPouringJug(dc, layout);

        // pH strip under it all.
        DrawPhStrip(dc, layout.Strip, Indicator.Universal);
        AddBoxTarget("phstrip", layout.Strip);
        if (ShowLabels)
        {
            DrawText(dc, Loc("Acid_Strip_Acidic"), new Point(layout.Strip.Left, layout.Strip.Bottom + 28), Ink.Red, 10.5);
            DrawText(dc, Loc("Acid_Strip_Neutral"), new Point(PhX(layout.Strip, 7), layout.Strip.Bottom + 28), Ink.Green, 10.5, TextAnchor.Center);
            DrawText(dc, Loc("Acid_Strip_Basic"), new Point(layout.Strip.Right, layout.Strip.Bottom + 28), Ink.Violet, 10.5, TextAnchor.Right);
        }

        DrawPhPointer(dc, layout.Strip, ph, "phvalue");

        // The magnifier on the beaker's water.
        if (!ShowParticles) return;
        var spot = new Point(layout.Liquid.Left + (layout.Liquid.Width * 0.3), layout.Liquid.Top + Math.Min(24, layout.Liquid.Height * 0.5));
        DrawMagnifier(dc, _beakerBox, layout.Lens, layout.LensRadius, spot, "magnifier", now);
        DrawText(dc, LocFormat("Acid_Lens_Unit", Scientific(_beakerBox.Unit)), new Point(layout.Lens.X, layout.Lens.Y - layout.LensRadius - 14), Ink.Dim, 10.5, TextAnchor.Center);
        if (_beakerBox.IsSwapping && _beakerBox.SwapAt is { } swap)
        {
            var at = layout.Lens + ((Vector)swap * layout.LensRadius) + new Vector(0, -26);
            DrawTargetPill(dc, Loc("Acid_Lens_Swap"), at, Ink.Gold, "swap", 10.5);
        }

        var kinds = KindsIn(species, _beakerBox.Unit);
        DrawLegend(dc, kinds, new Point(layout.Lens.X - layout.LensRadius, layout.Lens.Y + layout.LensRadius + 12), layout.LensRadius * 2);
    }

    /// <summary>
    /// The jug lifted over the beaker, tipped, with water running from its spout into the beaker; it eases up and away
    /// again at the end of the pour.
    /// </summary>
    private void DrawPouringJug(PaintContext dc, ParticlesLayout layout)
    {
        var t = _pour / PourSeconds;
        var lift = Math.Min(1, Math.Min(t, 1 - t) * 5);
        var size = layout.Bottles[^1].Size;
        var spout = new Point(layout.Liquid.Left + (layout.Liquid.Width * 0.5), layout.Beaker.Top - 26 - (14 * (1 - lift)));
        var body = new Rect(spout.X + 4, spout.Y - (size.Height * 0.5), size.Width + 8, size.Height * 0.72);
        dc.PushTransform(SpaceDrawing.Rotation(-38 * lift, spout.X, spout.Y));
        dc.DrawRoundedRectangle(FadedBrush(Color.FromRgb(0xB8, 0xDC, 0xF4), 0.3), Ink.GlassPen, body, 8, 8);
        dc.DrawRoundedRectangle(FadedBrush(Color.FromRgb(0x7C, 0xC0, 0xF0), 0.45), null,
            new Rect(body.Left + 3, body.Top + (body.Height * 0.35), body.Width - 6, (body.Height * 0.65) - 3), 6, 6);
        dc.DrawLine(Ink.GlassPen, new Point(body.Left, body.Top + 4), spout);
        dc.Pop();
        if (lift > 0.6)
        {
            var pen = FadedPen(Color.FromRgb(0xB8, 0xDC, 0xF4), 0.8, 4, flat: true);
            dc.DrawLine(pen, spout, new Point(spout.X, layout.Liquid.Top));
        }
    }

    /// <summary>A dropper bottle with its label band; the jug is a wide water jug. Registered as a target.</summary>
    private void DrawBottle(PaintContext dc, Reagent reagent, Rect box, double now)
    {
        var id = BottleId(reagent);
        var color = ReagentColor(reagent);

        if (reagent == Reagent.Water)
        {
            // While it pours, the jug is over the beaker (DrawPouringJug); its place on the shelf stays empty.
            if (_pour <= 0) DrawJug(dc, box);
        }
        else
        {
            var neck = new Rect(box.Left + (box.Width * 0.36), box.Top + 6, box.Width * 0.28, box.Height * 0.22);
            var body = new Rect(box.Left, neck.Bottom, box.Width, box.Bottom - neck.Bottom);
            dc.DrawRoundedRectangle(Ink.MeterBody, null, new Rect(neck.Left - 2, box.Top, neck.Width + 4, 9), 2, 2);
            dc.DrawRectangle(FadedBrush(Ink.Steel, 0.3), Ink.GlassThin, neck);
            dc.DrawRoundedRectangle(FadedBrush(color, 0.22), Ink.GlassPen, body, 7, 7);
            var liquid = new Rect(body.Left + 3, body.Top + (body.Height * 0.25), body.Width - 6, (body.Height * 0.75) - 3);
            dc.DrawRoundedRectangle(FadedBrush(Color.FromRgb(0xB8, 0xDC, 0xF4), 0.32), null, liquid, 5, 5);
            var band = new Rect(body.Left + 2, body.Top + (body.Height * 0.38), body.Width - 4, Math.Min(18, body.Height * 0.32));
            dc.DrawRectangle(FadedBrush(color, 0.95), null, band);
            DrawText(dc, Loc(FormulaKey(reagent)), new Point(band.Left + (band.Width / 2), band.Top + (band.Height / 2)), Color.FromRgb(0x10, 0x14, 0x1E), 9.5, TextAnchor.Center, bold: true);
        }

        var name = Loc(BottleNameKey(reagent));
        var label = DrawText(dc, name, new Point(box.Left + (box.Width / 2), box.Bottom + 20), color, 10.5, TextAnchor.Center);
        var target = box.Union(label);
        AddBoxTarget(id, target);
        if (Glows(id)) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(now, 3), Inflated(target, 4, 4), 8, 8);
    }

    /// <summary>The water jug standing on the shelf.</summary>
    private void DrawJug(PaintContext dc, Rect box)
    {
        var jug = new Rect(box.Left - 4, box.Top + (box.Height * 0.28), box.Width + 8, box.Height * 0.72);
        dc.DrawRoundedRectangle(FadedBrush(Color.FromRgb(0xB8, 0xDC, 0xF4), 0.28), Ink.GlassPen, jug, 8, 8);
        var water = new Rect(jug.Left + 3, jug.Top + (jug.Height * 0.3), jug.Width - 6, (jug.Height * 0.7) - 3);
        dc.DrawRoundedRectangle(FadedBrush(Color.FromRgb(0x7C, 0xC0, 0xF0), 0.45), null, water, 6, 6);
        dc.DrawGeometry(null, Ink.GlassPen, Polygon(new Point(jug.Left + 4, jug.Top), new Point(jug.Left - 8, jug.Top - 8)));
        dc.DrawArc(Ink.GlassPen, new Point(jug.Right, jug.Top + (jug.Height * 0.3)), jug.Height * 0.22);
    }

    private void DrawBeaker(PaintContext dc, Rect beaker, Rect liquid, Color color, double now)
    {
        // Liquid first, then the glass over it.
        dc.DrawRectangle(LiquidBrush(color), null, liquid);
        dc.DrawLine(FadedPen(Colors.White, 0.35, 1.5), liquid.TopLeft, liquid.TopRight);
        foreach (var ripple in _ripples)
        {
            var r = 6 + (ripple * 40);
            var center = new Point(liquid.Left + (liquid.Width * 0.5), liquid.Top);
            dc.DrawEllipse(null, FadedPen(Colors.White, 0.6 * (1 - (ripple / 0.7)), 1.2), center, r, r * 0.22);
        }

        var outline = Line(
        [
            new Point(beaker.Left - 6, beaker.Top - 4), new Point(beaker.Left, beaker.Top + 2), new Point(beaker.Left, beaker.Bottom - 6),
            new Point(beaker.Left + 6, beaker.Bottom), new Point(beaker.Right - 6, beaker.Bottom), new Point(beaker.Right, beaker.Bottom - 6),
            new Point(beaker.Right, beaker.Top),
        ]);
        dc.DrawGeometry(null, Glows("beaker") ? ExploreTargets.GlowPen(now, 4) : Ink.GlassPen, outline);
        dc.DrawRectangle(Ink.Shine, null, new Rect(beaker.Left + 6, beaker.Top + 10, 6, beaker.Height - 24));

        // Graduations every 50 mL.
        for (var ml = 50; ml < BeakerCapacity; ml += 50)
        {
            var y = beaker.Bottom - 4 - (ml / BeakerCapacity * (beaker.Height - 10));
            dc.DrawLine(Ink.GlassThin, new Point(beaker.Right - 16, y), new Point(beaker.Right - 4, y));
            if (ShowLabels) DrawText(dc, Number(ml, "0"), new Point(beaker.Right - 19, y), Ink.Dim, 9, TextAnchor.Right);
        }

        AddBoxTarget("beaker", beaker);
    }

    private void DrawMeter(PaintContext dc, ParticlesLayout layout, double ph, double now)
    {
        var meter = layout.Meter;
        var probeX = layout.Beaker.Right - (layout.Beaker.Width * 0.34);
        var probeTop = layout.Beaker.Top - 22;
        var probeBottom = Math.Max(layout.Liquid.Top + 18, layout.Beaker.Bottom - 26);

        // Cable from the meter over the rim to the probe.
        dc.DrawGeometry(null, FadedPen(Color.FromRgb(0x30, 0x36, 0x44), 1, 2.5), Curve(new Point(meter.Left, meter.Top + 18), new Point(probeX + 10, probeTop - 30), new Point(probeX, probeTop)));
        dc.DrawRoundedRectangle(Ink.Metal, null, new Rect(probeX - 4, probeTop, 8, probeBottom - probeTop), 3, 3);
        dc.DrawEllipse(FadedBrush(Color.FromRgb(0xDD, 0xEE, 0xFA), 0.7), null, new Point(probeX, probeBottom), 5, 6);

        dc.DrawRoundedRectangle(Ink.MeterBody, Glows("meter") ? ExploreTargets.GlowPen(now, 3) : Ink.Frame, meter, 8, 8);
        var screen = new Rect(meter.Left + 8, meter.Top + 8, meter.Width - 16, 34);
        dc.DrawRoundedRectangle(Ink.Screen, null, screen, 4, 4);
        DrawText(dc, Number(ph, "0.00"), new Point(screen.Right - 8, screen.Top + (screen.Height / 2)), Ink.Green, 20, TextAnchor.Right, bold: true);
        DrawText(dc, "pH", new Point(screen.Left + 6, screen.Top + (screen.Height / 2)), FadedColor(Ink.Green, 0.7), 11, TextAnchor.Left);
        DrawText(dc, Loc("Acid_Nature_" + AcidBaseModel.Nature(ph)), new Point(meter.Left + (meter.Width / 2), meter.Bottom - 14), NatureColor(ph), 11, TextAnchor.Center);
        AddBoxTarget("meter", meter);
    }

    private static Color FadedColor(Color color, double alpha) => Color.FromArgb((byte)(alpha * 255), color.R, color.G, color.B);

    /// <summary>Red for acidic, green for neutral, violet for basic.</summary>
    internal static Color NatureColor(double ph) => AcidBaseModel.Nature(ph) switch
    {
        "Acidic" => Ink.Red,
        "Basic" => Ink.Violet,
        _ => Ink.Green,
    };

    private Point BottleMouth(ParticlesLayout layout, Reagent reagent)
    {
        var box = layout.Bottles[Array.IndexOf(Bottles, reagent)];
        return reagent == Reagent.Water
            ? new Point(box.Left - 8, box.Top + (box.Height * 0.28) - 8)
            : new Point(box.Left + (box.Width / 2), box.Top);
    }

    private static void DrawDrop(PaintContext dc, Point at, Color color, double size)
    {
        var drop = Polygon(at + new Vector(0, -size * 2), at + new Vector(size * 0.9, 0), at + new Vector(0, size), at + new Vector(-size * 0.9, 0));
        dc.DrawGeometry(Solid(230, color.R, color.G, color.B), null, drop);
        dc.DrawEllipse(Solid(240, color.R, color.G, color.B), null, at, size, size);
    }

    private static Point Bezier(Point a, Point c, Point b, double t)
    {
        var u = 1 - t;
        return new Point((u * u * a.X) + (2 * u * t * c.X) + (t * t * b.X), (u * u * a.Y) + (2 * u * t * c.Y) + (t * t * b.Y));
    }

    private static StreamGeometry Curve(Point a, Point c, Point b)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(a, false);
            context.QuadraticBezierTo(c, b);

            context.EndFigure(false);
        }
        return geometry;
    }

    internal static string BottleId(Reagent reagent) => reagent switch
    {
        Reagent.StrongAcid => "hcl",
        Reagent.WeakAcid => "acetic",
        Reagent.StrongBase => "naoh",
        Reagent.WeakBase => "ammonia",
        _ => "jug",
    };

    internal static Reagent? ReagentOf(string? id) => id switch
    {
        "hcl" => Reagent.StrongAcid,
        "acetic" => Reagent.WeakAcid,
        "naoh" => Reagent.StrongBase,
        "ammonia" => Reagent.WeakBase,
        "jug" => Reagent.Water,
        _ => null,
    };

    private static string BottleNameKey(Reagent reagent) => reagent switch
    {
        Reagent.StrongAcid => "Acid_Bottle_StrongAcid",
        Reagent.WeakAcid => "Acid_Bottle_WeakAcid",
        Reagent.StrongBase => "Acid_Bottle_StrongBase",
        Reagent.WeakBase => "Acid_Bottle_WeakBase",
        _ => "Acid_Bottle_Water",
    };

    private static string FormulaKey(Reagent reagent) => reagent switch
    {
        Reagent.StrongAcid => "Acid_Formula_Hcl",
        Reagent.WeakAcid => "Acid_Formula_Acetic",
        Reagent.StrongBase => "Acid_Formula_Naoh",
        _ => "Acid_Formula_Ammonia",
    };

    private static Color ReagentColor(Reagent reagent) => reagent switch
    {
        Reagent.StrongAcid => Ink.Red,
        Reagent.WeakAcid => Ink.Orange,
        Reagent.StrongBase => Ink.Violet,
        Reagent.WeakBase => Ink.Cyan,
        _ => Color.FromRgb(0xB8, 0xDC, 0xF4),
    };
}

/// <summary>Small drawing extras for this page.</summary>
internal static class AcidBaseDrawingExtensions
{
    /// <summary>A half circle bulging right from <paramref name="top"/> (a jug's handle) of <paramref name="radius"/>.</summary>
    public static void DrawArc(this PaintContext dc, IPen pen, Point top, double radius)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(top, false);
            context.ArcTo(top + new Vector(0, radius * 2), new Size(radius, radius), 0, false, SweepDirection.Clockwise);

            context.EndFigure(false);
        }
        dc.DrawGeometry(null, pen, geometry);
    }
}
