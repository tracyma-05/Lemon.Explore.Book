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
/// The magnifier view: rock being made, close up. Cooling: a slice of magma 20 mm wide under the microscope, crystals
/// appearing and growing until it is solid, beside a thermometer and where in a volcano it cools that fast. Settling: a
/// tank a river pours mud into, the grains sinking at their own speeds into graded layers, then buried under more rock,
/// squeezed and cemented. Squeezing: a cube of rock between the plates of a press, heated and squeezed as it is pushed
/// deeper, its minerals turning, growing and banding.
/// </summary>
public sealed partial class VolcanoDiagram
{
    /// <summary>Seconds the whole cooling takes on the screen.</summary>
    private const double CoolSeconds = 11;

    private Rect _coolRect;
    private Rect _tankRect;
    private Rect _bedRect;
    private Rect _overburden;
    private Rect _squeezeSample;
    private Rect _pressPlate;
    private double _shownGrade = -1;

    private void StepMagnifier(double dt)
    {
        switch (Process)
        {
            case MagnifierProcess.Cooling:
                Advance(dt / CoolSeconds, wrap: false);
                if (Crystals is { } sim)
                {
                    var target = (int)Math.Round(Progress * CrystalSim.Steps);
                    if (target > sim.Step) sim.AdvanceTo(target);
                }

                break;

            case MagnifierProcess.Settling:
                if (IsPlaying) Tank?.Step(dt);
                break;

            default:
                var goal = VolcanoModel.Grade(SqueezeDepthKm);
                _shownGrade = _shownGrade < 0 ? goal : _shownGrade + ((goal - _shownGrade) * (1 - Math.Exp(-dt * 2.8)));
                break;
        }
    }

    private void DrawMagnifierMode(PaintContext dc, double now)
    {
        switch (Process)
        {
            case MagnifierProcess.Settling:
                DrawSettling(dc, now);
                break;

            case MagnifierProcess.Squeezing:
                DrawSqueezing(dc, now);
                break;

            default:
                DrawCooling(dc, now);
                break;
        }
    }

    #region cooling

    private WriteableBitmap? _coolBitmap;
    private CrystalSim? _coolDrawn;
    private int _coolVersion = -1;
    private int _coolStep = -1;

    private static Color MineralColor(string id) => id switch
    {
        "Quartz" => Color.FromRgb(0xD6, 0xD9, 0xDF),
        "KFeldspar" => Color.FromRgb(0xE2, 0xA2, 0x8E),
        "Plagioclase" => Color.FromRgb(0xEE, 0xE9, 0xDC),
        "Biotite" => Color.FromRgb(0x36, 0x2A, 0x22),
        "Olivine" => Color.FromRgb(0x8C, 0xB0, 0x4A),
        "Pyroxene" => Color.FromRgb(0x3C, 0x48, 0x36),
        _ => Colors.Gray,
    };

    private static readonly Color GlassColor = Color.FromRgb(0x24, 0x1A, 0x1C);
    private static readonly Color BubbleColor = Color.FromRgb(0x08, 0x0A, 0x10);

    /// <summary>The melt's glow at <paramref name="heat"/> (1 at the liquidus, 0 at the solidus).</summary>
    private static Color MeltColor(double heat) => Lerp(Color.FromRgb(0x6A, 0x12, 0x08), Color.FromRgb(0xFF, 0xA8, 0x30), Math.Clamp(heat, 0, 1));

    private void DrawCooling(PaintContext dc, double now)
    {
        var s = Stage();
        var sim = Crystals;
        var maxWidth = s.Width * 0.64;
        var w = Math.Min(maxWidth, s.Height * 0.9 * 1.5);
        var h = w / 1.5;
        var rect = new Rect(s.Left + 8, s.Top + ((s.Height - h) / 2), w, h);
        _coolRect = rect;

        dc.DrawRoundedRectangle(Ink.Lens, Ink.LensRim, Inflated(rect, 6), 12, 12);
        if (sim is not null)
        {
            SpaceDrawing.DrawSmooth(dc, CoolBitmap(sim), rect);
            if (sim.Step < CrystalSim.Steps)
            {
                // The still-liquid melt glows: a gentle shimmer over it.
                dc.DrawRectangle(FadedBrush(Ink.LavaHot, 0.04 + (0.03 * Math.Sin(now * 3))), null, rect);
            }
        }

        // Scale bar: 5 mm.
        var bar = rect.Width * 5 / CrystalSim.FieldMm;
        var barY = rect.Bottom - 14;
        dc.DrawRectangle(FadedBrush(Colors.Black, 0.55), null, new Rect(rect.Left + 10, barY - 12, bar + 16, 22));
        dc.DrawLine(FadedPen(Colors.White, 0.95, 2.5, flat: true), new Point(rect.Left + 18, barY + 3), new Point(rect.Left + 18 + bar, barY + 3));
        DrawText(dc, Loc("Volc_Label_FiveMm"), new Point(rect.Left + 18 + (bar / 2), barY - 5), Ink.Text, 10, TextAnchor.Center);

        var side = new Rect(rect.Right + 26, s.Top + 4, Math.Max(60, s.Right - rect.Right - 30), s.Height - 8);
        if (sim is null) return;
        DrawCoolingSide(dc, side, sim, now);
    }

    private static Rect Inflated(Rect rect, double by)
    {
        rect = rect.Inflate(new Thickness(by, by));
        return rect;
    }

    /// <summary>The slice's cells as a picture, 2 × 2 pixels a cell with darker edges where crystals meet; redrawn when the cells change.</summary>
    private WriteableBitmap CoolBitmap(CrystalSim sim)
    {
        const int scale = 2;
        const int width = CrystalSim.Width * scale;
        const int height = CrystalSim.Height * scale;
        _coolBitmap ??= new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        if (ReferenceEquals(sim, _coolDrawn) && sim.Version == _coolVersion && sim.Step == _coolStep) return _coolBitmap;
        _coolDrawn = sim;
        _coolVersion = sim.Version;
        _coolStep = sim.Step;

        var cells = sim.Cells;
        var (liquidus, solidus) = CrystalSim.Range(sim.Magma);
        var heat = (sim.Temperature - solidus) / (liquidus - solidus);
        var melt = MeltColor(heat);
        var mineralColors = sim.Minerals.Select(m => MineralColor(m.Id)).ToArray();
        var pixels = new int[width * height];
        for (var cy = 0; cy < CrystalSim.Height; cy++)
        {
            for (var cx = 0; cx < CrystalSim.Width; cx++)
            {
                var value = cells[(cy * CrystalSim.Width) + cx];
                var right = cx < CrystalSim.Width - 1 ? cells[(cy * CrystalSim.Width) + cx + 1] : value;
                var down = cy < CrystalSim.Height - 1 ? cells[((cy + 1) * CrystalSim.Width) + cx] : value;
                for (var py = 0; py < scale; py++)
                {
                    for (var px = 0; px < scale; px++)
                    {
                        var x = (cx * scale) + px;
                        var y = (cy * scale) + py;
                        var noise = (SqueezeTexture.Hash((y * width) + x, 131) - 0.5) * 0.12;
                        Color color;
                        if (value == CrystalSim.Melt) color = Shade(melt, noise * 1.5);
                        else if (value == CrystalSim.Glass) color = Shade(sim.Magma == MagmaKind.Mafic ? Color.FromRgb(0x30, 0x22, 0x18) : GlassColor, noise);
                        else if (value == CrystalSim.Bubble) color = BubbleColor;
                        else
                        {
                            var mineral = sim.MineralOf(value);
                            var id = sim.Minerals[mineral].Id;
                            var own = (SqueezeTexture.Hash(value, 132) - 0.5) * 0.18;
                            color = Shade(mineralColors[mineral], own + (noise * 0.5));

                            // Plagioclase shows thin stripes (twinning); mica and pyroxene fine cleavage lines.
                            if (id is "Plagioclase" or "Biotite" or "Pyroxene")
                            {
                                var angle = sim.AngleOf(value);
                                var across = (-x * Math.Sin(angle)) + (y * Math.Cos(angle));
                                var period = id == "Plagioclase" ? 5.0 : 3.0;
                                if (((across / period) % 1 + 1) % 1 < 0.3) color = Shade(color, id == "Plagioclase" ? -0.12 : 0.12);
                            }
                        }

                        // Where a crystal meets something else: a dark edge.
                        var edge = (px == scale - 1 && right != value) || (py == scale - 1 && down != value);
                        if (edge && value != CrystalSim.Melt) color = Shade(color, -0.45);
                        pixels[(y * width) + x] = (255 << 24) | (color.R << 16) | (color.G << 8) | color.B;
                    }
                }
            }
        }

        SpaceDrawing.WritePixels(_coolBitmap, pixels, width, height);
        return _coolBitmap;
    }

    private static Color Shade(Color color, double by)
    {
        static byte C(double v) => (byte)Math.Clamp(v, 0, 255);
        return by >= 0
            ? Color.FromRgb(C(color.R + ((255 - color.R) * by)), C(color.G + ((255 - color.G) * by)), C(color.B + ((255 - color.B) * by)))
            : Color.FromRgb(C(color.R * (1 + by)), C(color.G * (1 + by)), C(color.B * (1 + by)));
    }

    /// <summary>Beside the slice: the thermometer, where it cools that fast, and what is in it.</summary>
    private void DrawCoolingSide(PaintContext dc, Rect side, CrystalSim sim, double now)
    {
        var (liquidus, solidus) = CrystalSim.Range(sim.Magma);
        var thermo = new Rect(side.Left, side.Top + 14, 22, Math.Min(side.Height * 0.5, 200));
        DrawThermometer(dc, thermo, sim.Temperature, solidus - 50, liquidus + 50, solidus, liquidus);
        DrawText(dc, LocFormat("Volc_Value_Celsius", Number(sim.Temperature, "0")), new Point(thermo.Right + 8, thermo.Top + 4), Ink.Text, 12, bold: true);
        DrawText(dc, Loc("Volc_Label_Liquidus"), new Point(thermo.Right + 8, ThermoY(thermo, liquidus, solidus - 50, liquidus + 50)), Ink.Dim, 10);
        DrawText(dc, Loc("Volc_Label_Solidus"), new Point(thermo.Right + 8, ThermoY(thermo, solidus, solidus - 50, liquidus + 50)), Ink.Dim, 10);
        AddBoxTarget("thermo", new Rect(thermo.Left - 4, thermo.Top - 4, thermo.Width + 8, thermo.Height + 22));

        // Where magma cools this fast: deep in a pluton, in a thin sheet, in a lava flow, or quenched.
        var inset = new Rect(side.Left, thermo.Bottom + 38, Math.Min(side.Width, 190), 108);
        if (inset.Bottom < side.Bottom - 70) DrawWhereItCools(dc, inset, sim);

        // What it is made of: the minerals (in the order they start to crystallise), glass, bubbles.
        var y = Math.Min(side.Bottom - (22 * (sim.Minerals.Count + 3)), (inset.Bottom < side.Bottom - 70 ? inset.Bottom : thermo.Bottom + 20) + 18);
        foreach (var mineral in sim.Minerals)
        {
            var color = MineralColor(mineral.Id);
            var started = sim.Temperature <= mineral.StartC;
            dc.DrawRoundedRectangle(FadedBrush(color, started ? 1 : 0.35), Ink.Frame, new Rect(side.Left, y, 16, 12), 2, 2);
            var text = LocFormat("Volc_Label_MineralFrom", Loc($"Volc_Mineral_{mineral.Id}"), Number(mineral.StartC, "0"));
            var box = DrawText(dc, text, new Point(side.Left + 22, y + 6), started ? Ink.Text : Ink.Dim, 11);
            AddBoxTarget(MineralId(mineral), new Rect(side.Left - 2, y - 3, box.Right - side.Left + 4, 18));
            y += 22;
        }

        if (sim.GlassShare > 0.001 || sim.Gas)
        {
            dc.DrawRoundedRectangle(FadedBrush(GlassColor, 1), Ink.Frame, new Rect(side.Left, y, 16, 12), 2, 2);
            var box = DrawText(dc, Loc("Volc_Obj_Glass"), new Point(side.Left + 22, y + 6), Ink.Text, 11);
            AddBoxTarget("glass", new Rect(side.Left - 2, y - 3, box.Right - side.Left + 4, 18));
            y += 22;
        }

        if (sim.BubbleShare > 0)
        {
            dc.DrawRoundedRectangle(FadedBrush(BubbleColor, 1), FadedPen(Ink.Dim, 0.8, 1), new Rect(side.Left, y, 16, 12), 6, 6);
            var box = DrawText(dc, Loc("Volc_Obj_Vesicle"), new Point(side.Left + 22, y + 6), Ink.Text, 11);
            AddBoxTarget("vesicle", new Rect(side.Left - 2, y - 3, box.Right - side.Left + 4, 18));
            y += 22;
        }

        if (!sim.IsFinished)
        {
            dc.DrawRoundedRectangle(FadedBrush(MeltColor((sim.Temperature - solidus) / (liquidus - solidus)), 1), Ink.Frame, new Rect(side.Left, y, 16, 12), 2, 2);
            var box = DrawText(dc, Loc("Volc_Obj_Melt"), new Point(side.Left + 22, y + 6), Ink.Text, 11);
            AddBoxTarget("melt", new Rect(side.Left - 2, y - 3, box.Right - side.Left + 4, 18));
        }
    }

    private static double ThermoY(Rect r, double value, double min, double max) => r.Bottom - 10 - ((value - min) / (max - min) * (r.Height - 18));

    /// <summary>A thermometer from <paramref name="min"/> to <paramref name="max"/> filled to <paramref name="value"/>, with a band between two marks.</summary>
    private void DrawThermometer(PaintContext dc, Rect r, double value, double min, double max, double bandFrom, double bandTo)
    {
        dc.DrawRoundedRectangle(FadedBrush(Color.FromRgb(0x14, 0x18, 0x22), 1), FadedPen(Ink.Dim, 0.8, 1.2), r, r.Width / 2, r.Width / 2);
        var bulb = new Point(r.Left + (r.Width / 2), r.Bottom - 2);
        var level = ThermoY(r, Math.Clamp(value, min, max), min, max);
        var heat = Math.Clamp((value - min) / (max - min), 0, 1);
        var color = Lerp(Color.FromRgb(0xC8, 0x30, 0x20), Ink.LavaHot, heat);
        dc.DrawRectangle(FadedBrush(color, 1), null, new Rect(r.Left + (r.Width * 0.32), level, r.Width * 0.36, Math.Max(0, bulb.Y - level)));
        dc.DrawEllipse(FadedBrush(color, 1), FadedPen(Ink.Dim, 0.8, 1.2), bulb, r.Width * 0.62, r.Width * 0.62);
        foreach (var mark in new[] { bandFrom, bandTo })
        {
            var y = ThermoY(r, mark, min, max);
            dc.DrawLine(FadedPen(Ink.Text, 0.8, 1), new Point(r.Right - 2, y), new Point(r.Right + 5, y));
        }
    }

    /// <summary>A small volcano in cross-section with a ring where magma cools as fast as now.</summary>
    private void DrawWhereItCools(PaintContext dc, Rect r, CrystalSim sim)
    {
        dc.DrawRoundedRectangle(FadedBrush(Color.FromRgb(0x0C, 0x10, 0x1A), 1), Ink.Frame, r, 8, 8);
        var ground = r.Top + (r.Height * 0.42);
        Point Q(double u, double v) => new(r.Left + (u * r.Width), r.Top + (v * r.Height));
        var surface = new List<Point> { Q(0, 0.42), Q(0.34, 0.42), Q(0.5, 0.14), Q(0.66, 0.42), Q(0.78, 0.42), Q(0.8, 0.5), Q(1, 0.5), Q(1, 1), Q(0, 1) };
        dc.PushClip(new RectangleGeometry(r, 8, 8));
        dc.DrawGeometry(FadedBrush(Ink.Rock, 1), null, Shape(surface));
        dc.DrawRectangle(FadedBrush(Ink.Sea, 0.8), null, new Rect(Q(0.8, 0.42), Q(1, 0.5)));
        dc.DrawEllipse(FadedBrush(Ink.Lava, 0.85), null, Q(0.3, 0.84), r.Width * 0.12, r.Height * 0.1);
        dc.DrawLine(FadedPen(Ink.Lava, 0.9, 3, flat: true), Q(0.5, 0.16), Q(0.5, 0.95));
        dc.DrawLine(FadedPen(Ink.Lava, 0.9, 2.5, flat: true), Q(0.5, 0.6), Q(0.18, 0.56));
        dc.DrawLine(FadedPen(Ink.Lava, 0.9, 3), Q(0.56, 0.24), Q(0.79, 0.45));
        dc.Pop();
        dc.DrawRoundedRectangle(null, Ink.Frame, r, 8, 8);

        var rate = sim.TwoStage ? (sim.Step < CrystalSim.Steps * CrystalSim.FirstStageShare ? CrystalSim.SlowStage : CrystalSim.FastStage) : sim.LogRate;
        var (spot, key) = rate switch
        {
            < 0 => (Q(0.3, 0.84), "Volc_Where_Pluton"),
            < 2.5 => (Q(0.25, 0.57), "Volc_Where_Sill"),
            < 5.2 => (Q(0.66, 0.34), "Volc_Where_Flow"),
            _ => (Q(0.8, 0.46), "Volc_Where_Quench"),
        };
        dc.DrawEllipse(null, FadedPen(Ink.Gold, 1, 2), spot, 9, 9);
        DrawText(dc, Loc(key), new Point(r.Left + 8, r.Top + 10), Ink.Gold, 10.5);
        _ = ground;
    }

    /// <summary>The thing under the mouse in the slice: a crystal's mineral, glass, a bubble or melt.</summary>
    private string? CoolingThingAt(Point point)
    {
        if (Crystals is not { } sim || !_coolRect.Contains(point)) return null;
        var cx = (int)((point.X - _coolRect.Left) / _coolRect.Width * CrystalSim.Width);
        var cy = (int)((point.Y - _coolRect.Top) / _coolRect.Height * CrystalSim.Height);
        if (cx < 0 || cy < 0 || cx >= CrystalSim.Width || cy >= CrystalSim.Height) return null;
        var value = sim.Cells[(cy * CrystalSim.Width) + cx];
        return value switch
        {
            CrystalSim.Melt => "melt",
            CrystalSim.Glass => "glass",
            CrystalSim.Bubble => "vesicle",
            _ => MineralId(sim.Minerals[sim.MineralOf(value)]),
        };
    }

    #endregion

    #region settling

    private WriteableBitmap? _tankBitmap;
    private WriteableBitmap? _bedBitmap;
    private (SedimentSim? Tank, int Version, double Burial) _bedDrawn;

    private static Color GrainColor(byte size) => size switch
    {
        SedimentSim.Gravel => Color.FromRgb(0x8E, 0x84, 0x7A),
        SedimentSim.Sand => Color.FromRgb(0xD9, 0xBE, 0x84),
        SedimentSim.Silt => Color.FromRgb(0xA8, 0x8E, 0x6A),
        _ => Color.FromRgb(0x5E, 0x5A, 0x50),
    };

    private void DrawSettling(PaintContext dc, double now)
    {
        var s = Stage();
        var tank = Tank;
        var maxWidth = s.Width * 0.66;
        var w = Math.Min(maxWidth, s.Height * 0.86 * SedimentSim.Width / SedimentSim.Height);
        var h = w * SedimentSim.Height / SedimentSim.Width;
        var rect = new Rect(s.Left + 40, s.Top + ((s.Height - h) / 2) + 8, w, h);
        _tankRect = rect;

        // The river coming in at the top left.
        var mouth = new Point(rect.Left + 4, rect.Top + 10);
        var river = new List<Point> { new(s.Left, rect.Top - 18), new(rect.Left - 18, rect.Top - 10), mouth };
        dc.DrawGeometry(null, Glows("river") ? ExploreTargets.GlowPen(now, 12) : FadedPen(Color.FromRgb(0x8A, 0x74, 0x52), 1, 9), Line(river));
        dc.DrawGeometry(null, new ImmutablePen(((IBrush)Solid(170, 0xC8, 0xB0, 0x88)).ToImmutable(), 2, new ImmutableDashStyle([3, 5], -now * 5), PenLineCap.Flat, PenLineJoin.Miter), Line(river));

        if (tank is null) return;
        var burial = BurialKm;
        var mud = MudShare(tank);
        var squeeze = (mud * VolcanoModel.Thickness(true, burial)) + ((1 - mud) * VolcanoModel.Thickness(false, burial));
        var bedRect = new Rect(rect.Left, rect.Bottom - (rect.Height * squeeze), rect.Width, rect.Height * squeeze);
        _bedRect = bedRect;

        // Water (or the rock piled on top once buried) and the grains sinking through it.
        SpaceDrawing.DrawSmooth(dc, TankBitmap(tank, burial > 0.02), rect);
        SpaceDrawing.DrawSmooth(dc, BedBitmap(tank, burial), bedRect);
        dc.DrawRectangle(null, Ink.GlassPen, rect);
        var bedTop = rect.Bottom - (tank.Heights.ToArray().Max() * rect.Height / SedimentSim.Height * squeeze);

        _overburden = default(Rect);
        if (burial > 0.02)
        {
            // Later rock fills everything down to the squeezed bed's surface.
            var over = new Rect(rect.Left, rect.Top, rect.Width, Math.Max(4, bedTop - rect.Top));
            _overburden = new Rect(rect.Left, rect.Top, rect.Width, Math.Max(4, rect.Bottom - rect.Top - 6));
            var outline = new List<Point> { rect.TopLeft, rect.TopRight };
            var heights = tank.Heights;
            for (var c = SedimentSim.Width - 1; c >= 0; c -= 2)
            {
                var y = rect.Bottom - (heights[c] * rect.Height / SedimentSim.Height * squeeze);
                outline.Add(new Point(rect.Left + ((c + 0.5) * rect.Width / SedimentSim.Width), Math.Min(rect.Bottom, y + 1)));
            }

            outline.Add(new Point(rect.Left, outline[^1].Y));
            dc.DrawGeometry(_overburdenBrush ??= MakeOverburdenBrush(), null, Shape(outline));
            for (var i = 0; i < 4; i++)
            {
                var x = over.Left + ((i + 0.5) * over.Width / 4);
                DrawArrow(dc, new Point(x, over.Top + 8), new Point(x, Math.Max(over.Top + 20, over.Bottom - 6)), Ink.Text, 2.5, 0.7);
            }

            DrawTargetPill(dc, LocFormat("Volc_Label_Buried", Number(burial, "0.0")), new Point(over.Left + (over.Width / 2), over.Top + (over.Height / 2)), Ink.Text, "overburden");
        }

        AddBoxTarget("river", new Rect(s.Left, rect.Top - 26, rect.Left - s.Left + 14, 26));
        if (ShowLabels) DrawTargetPill(dc, Loc("Volc_Label_Pour"), new Point(rect.Left + 40, rect.Top - 34), Ink.Sand, "river", 11);

        // The layers, once there are any, labelled on their right.
        if (tank.Pours > 0 && bedTop < rect.Bottom - 8)
        {
            var label = burial >= 1.5 ? LocFormat("Volc_Label_LayersRock", tank.Pours) : LocFormat("Volc_Label_Layers", tank.Pours);
            DrawTargetPill(dc, label, new Point(rect.Right - 70, (bedTop + rect.Bottom) / 2), burial >= 1.5 ? Ink.Gold : Ink.Text, "layers", 11);
        }

        DrawGrainLegend(dc, new Rect(rect.Right + 22, rect.Top, Math.Max(80, s.Right - rect.Right - 26), rect.Height), tank, burial);
    }

    private IBrush? _overburdenBrush;

    /// <summary>Layers of later rock in four tones, with grain: a bitmap tile.</summary>
    private static IBrush MakeOverburdenBrush()
    {
        const int n = 40;
        var colors = new[] { Color.FromRgb(0x5A, 0x4E, 0x42), Color.FromRgb(0x6E, 0x64, 0x56), Color.FromRgb(0x4C, 0x44, 0x3C), Color.FromRgb(0x7A, 0x6C, 0x58) };
        var pixels = new int[n * n];
        for (var i = 0; i < pixels.Length; i++)
        {
            var color = Shade(colors[(i / n) / 10], (SqueezeTexture.Hash(i, 171) - 0.5) * 0.2);
            pixels[i] = (255 << 24) | (color.R << 16) | (color.G << 8) | color.B;
        }

        return TileBrush(pixels, n, n);
    }

    private static double MudShare(SedimentSim tank)
    {
        double coarse = tank.Settled(SedimentSim.Gravel) * 9 + tank.Settled(SedimentSim.Sand);
        double fine = tank.Settled(SedimentSim.Silt) + tank.Settled(SedimentSim.Clay);
        return coarse + fine <= 0 ? 0.5 : fine / (coarse + fine);
    }

    /// <summary>The water, cloudy with the fine grains still in it, and the sinking grains; redrawn every frame.</summary>
    private WriteableBitmap TankBitmap(SedimentSim tank, bool buried)
    {
        const int width = SedimentSim.Width;
        const int height = SedimentSim.Height;
        _tankBitmap ??= new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        var pixels = new int[width * height];
        var cloudy = Math.Min(1, (tank.SinkingCount(SedimentSim.Clay) + (tank.SinkingCount(SedimentSim.Silt) * 0.6)) / 900.0);
        for (var y = 0; y < height; y++)
        {
            var t = y / (double)height;
            var water = Lerp(Color.FromRgb(0x2A, 0x6A, 0xA8), Color.FromRgb(0x10, 0x30, 0x58), t);
            water = Lerp(water, Color.FromRgb(0x7A, 0x6A, 0x4E), cloudy * 0.55);
            var argb = (255 << 24) | (water.R << 16) | (water.G << 8) | water.B;
            for (var x = 0; x < width; x++) pixels[(y * width) + x] = argb;
        }

        if (!buried)
        {
            foreach (var grain in tank.Falling)
            {
                if (grain.Delay > 0) continue;
                var c = GrainColor(grain.Size);
                var argb = (255 << 24) | (c.R << 16) | (c.G << 8) | c.B;
                var x = (int)grain.X;
                var y = (int)grain.Y;
                var r = grain.Size == SedimentSim.Gravel ? 1 : 0;
                for (var dy = -r; dy <= r; dy++)
                {
                    for (var dx = -r; dx <= r; dx++)
                    {
                        var px = x + dx;
                        var py = y + dy;
                        if (px >= 0 && py >= 0 && px < width && py < height) pixels[(py * width) + px] = argb;
                    }
                }
            }
        }

        SpaceDrawing.WritePixels(_tankBitmap, pixels, width, height);
        return _tankBitmap;
    }

    /// <summary>The bed: each grain by its size, the layers a little different in tone, pore water turning to cement as it is buried.</summary>
    private WriteableBitmap BedBitmap(SedimentSim tank, double burial)
    {
        const int width = SedimentSim.Width;
        const int height = SedimentSim.Height;
        _bedBitmap ??= new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        var key = (tank, tank.Version, Math.Round(burial, 2));
        if (_bedDrawn == key) return _bedBitmap;
        _bedDrawn = key;

        var bed = tank.Bed;
        var layers = tank.Layers;
        var cement = VolcanoModel.Cement(burial);
        var pixels = new int[width * height];
        for (var i = 0; i < pixels.Length; i++)
        {
            var size = bed[i];
            if (size == SedimentSim.Water) continue;
            var color = GrainColor(size);
            color = Shade(color, ((layers[i] % 2 == 0) ? -0.08 : 0.04) + ((SqueezeTexture.Hash(i, 141) - 0.5) * 0.14));

            // Some of the spaces between the grains: water at first, filled with pale cement once deep enough.
            var muddy = size >= SedimentSim.Silt;
            if (SqueezeTexture.Hash(i, 142) < VolcanoModel.Porosity(muddy, burial) * 0.2)
            {
                color = Lerp(Color.FromRgb(0x2A, 0x5A, 0x8C), Color.FromRgb(0xE8, 0xE2, 0xD0), cement);
            }

            pixels[i] = (255 << 24) | (color.R << 16) | (color.G << 8) | color.B;
        }

        SpaceDrawing.WritePixels(_bedBitmap, pixels, width, height);
        return _bedBitmap;
    }

    /// <summary>The grain sizes beside the tank: their colour, name, real sinking time for a metre, and what rock they make.</summary>
    private void DrawGrainLegend(PaintContext dc, Rect r, SedimentSim tank, double burial)
    {
        var y = r.Top + 8;
        DrawText(dc, Loc("Volc_Label_SinkTime"), new Point(r.Left, y), Ink.Dim, 10.5);
        y += 22;
        for (var size = SedimentSim.Gravel; size <= SedimentSim.Clay; size++)
        {
            var (id, diameter) = VolcanoModel.GrainSizes[size - 1];
            var color = GrainColor(size);
            dc.DrawEllipse(FadedBrush(color, 1), Ink.Frame, new Point(r.Left + 7, y + 8), size == SedimentSim.Gravel ? 6 : 4, size == SedimentSim.Gravel ? 6 : 4);
            var name = burial >= 1.5 ? Loc($"Volc_State_{VolcanoModel.SedimentaryRock(id)}") : Loc($"Volc_Grain_{id}");
            var nameBox = DrawText(dc, name, new Point(r.Left + 20, y + 2), Ink.Text, 11.5, bold: true);
            var time = SinkTimeText(VolcanoModel.SecondsPerMetre(diameter));
            var timeBox = DrawText(dc, time, new Point(r.Left + 20, y + 18), Ink.Dim, 10.5);
            AddBoxTarget(GrainId(size), new Rect(r.Left - 2, y - 4, Math.Max(nameBox.Right, timeBox.Right) - r.Left + 6, 34));
            y += 42;
        }
    }

    /// <summary>"2.5 s", "30 s", "20 min", "3 days" for the time to sink a metre.</summary>
    internal static string SinkTimeText(double seconds) => seconds switch
    {
        < 60 => LocalizationTextFormat("Volc_Time_Seconds", Number(seconds, seconds < 10 ? "0.0" : "0")),
        < 3600 => LocalizationTextFormat("Volc_Time_Minutes", Number(seconds / 60, "0")),
        < 86400 => LocalizationTextFormat("Volc_Time_Hours", Number(seconds / 3600, "0")),
        _ => LocalizationTextFormat("Volc_Time_Days", Number(seconds / 86400, "0.#")),
    };

    private static string LocalizationTextFormat(string key, string value) => LocFormat(key, value);

    /// <summary>The grain size under the mouse in the bed.</summary>
    private string? BedThingAt(Point point)
    {
        if (Tank is not { } tank || !_bedRect.Contains(point)) return null;
        var x = (int)((point.X - _bedRect.Left) / _bedRect.Width * SedimentSim.Width);
        var y = (int)((point.Y - _bedRect.Top) / _bedRect.Height * SedimentSim.Height);
        if (x < 0 || y < 0 || x >= SedimentSim.Width || y >= SedimentSim.Height) return null;

        // The nearest grain within a couple of cells, so a click between grains still finds one.
        for (var r = 0; r <= 2; r++)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    var px = Math.Clamp(x + dx, 0, SedimentSim.Width - 1);
                    var py = Math.Clamp(y + dy, 0, SedimentSim.Height - 1);
                    var size = tank.Bed[(py * SedimentSim.Width) + px];
                    if (size != SedimentSim.Water) return GrainId(size);
                }
            }
        }

        return null;
    }

    #endregion

    #region squeezing

    private WriteableBitmap? _grainBitmap;
    private string? _grainKey;
    private StreamGeometry? _flakeMica;
    private StreamGeometry? _flakeLight;
    private StreamGeometry? _flakeDark;
    private (double Grade, Rect Box) _flakesDrawn;

    private void DrawSqueezing(PaintContext dc, double now)
    {
        var s = Stage();
        var grade = _shownGrade < 0 ? VolcanoModel.Grade(SqueezeDepthKm) : _shownGrade;
        var size = Math.Min(s.Height * 0.72, s.Width * 0.46);
        var box = new Rect(s.Left + 36, s.Top + ((s.Height - size) / 2), size, size);
        _squeezeSample = box;

        // Heat: a red glow round the sample, stronger deeper down.
        var heat = Math.Clamp((VolcanoModel.TemperatureAt(SqueezeDepthKm) - 15) / 760, 0, 1);
        dc.DrawRoundedRectangle(SoftBrush(Ink.Lava, 0.15 + (0.55 * heat)), null, Inflated(box, 26 + (heat * 14)), 30, 30);

        dc.PushClip(new RectangleGeometry(box));
        if (SqueezeParent == "Shale") DrawFoliated(dc, box, grade, now);
        else DrawGrains(dc, box, grade);
        dc.Pop();
        dc.DrawRectangle(null, FadedPen(Ink.Text, 0.6, 1.2), box);
        DrawSqueezeLabels(dc, box);

        // The press: plates above and below, pushed together harder the deeper it is.
        var kbar = VolcanoModel.KilobarsAt(SqueezeDepthKm);
        var push = Math.Clamp(kbar / 8, 0, 1);
        var plateTop = new Rect(box.Left - 14, box.Top - 18, box.Width + 28, 14);
        var plateBottom = new Rect(box.Left - 14, box.Bottom + 4, box.Width + 28, 14);
        _pressPlate = new Rect(plateTop.Left, plateTop.Top - 40, plateTop.Width, plateTop.Height + 40);
        var platePen = Glows("press") ? ExploreTargets.GlowPen(now, 4) : FadedPen(Colors.Black, 0.6, 1);
        dc.DrawRoundedRectangle(Ink.Metal, platePen, plateTop, 3, 3);
        dc.DrawRoundedRectangle(Ink.Metal, platePen, plateBottom, 3, 3);
        for (var i = 0; i < 3; i++)
        {
            var x = box.Left + ((i + 0.5) * box.Width / 3);
            var length = 10 + (push * 34);
            DrawArrow(dc, new Point(x, plateTop.Top - 4 - length), new Point(x, plateTop.Top - 3), Ink.Text, 2 + (push * 2), 0.85);
            DrawArrow(dc, new Point(x, plateBottom.Bottom + 4 + length), new Point(x, plateBottom.Bottom + 3), Ink.Text, 2 + (push * 2), 0.85);
        }

        AddBoxTarget("press", plateTop);
        AddBoxTarget("press", plateBottom);

        // What it has become, big, and the scale of depth beside it.
        var side = new Rect(box.Right + 130, s.Top + 6, Math.Max(80, s.Right - box.Right - 134), s.Height - 12);
        DrawDepthScale(dc, side, grade);
    }

    /// <summary>Shale's flakes: lining up across the push, growing into mica, garnets in them, bands of light and dark, then melt.</summary>
    private void DrawFoliated(PaintContext dc, Rect box, double grade, double now)
    {
        // The rock's own colour: dark grey shale, blue-grey slate, silvery schist, light gneiss.
        var matrix = grade switch
        {
            < 0.25 => Lerp(Color.FromRgb(0x3A, 0x3A, 0x3C), Color.FromRgb(0x40, 0x48, 0x56), grade / 0.25),
            < 0.6 => Lerp(Color.FromRgb(0x40, 0x48, 0x56), Color.FromRgb(0x7A, 0x7C, 0x84), (grade - 0.25) / 0.35),
            _ => Lerp(Color.FromRgb(0x7A, 0x7C, 0x84), Color.FromRgb(0xB8, 0xB0, 0xA4), (grade - 0.6) / 0.4),
        };
        dc.DrawRectangle(FadedBrush(matrix, 1), null, box);

        var banding = SqueezeTexture.Banding(grade);
        if (banding > 0.02)
        {
            for (var k = 0; k <= (int)(1 / SqueezeTexture.BandSpacing) + 1; k++)
            {
                var v = 0.5 + (((k * SqueezeTexture.BandSpacing) - 0.5) * (1 - (0.06 * grade)));
                var y = box.Top + (v * box.Height);
                var band = new Rect(box.Left, y - (box.Height * SqueezeTexture.BandSpacing * 0.22), box.Width, box.Height * SqueezeTexture.BandSpacing * 0.44);
                dc.DrawRectangle(FadedBrush(Color.FromRgb(0x2A, 0x26, 0x28), 0.75 * banding), null, band);
            }
        }

        if (_flakesDrawn.Grade != Math.Round(grade, 3) || _flakesDrawn.Box != box || _flakeMica is null)
        {
            _flakesDrawn = (Math.Round(grade, 3), box);
            (_flakeMica, _flakeLight, _flakeDark) = (Flakes(box, grade, FlakeKind.Mica), Flakes(box, grade, FlakeKind.Light), Flakes(box, grade, FlakeKind.Dark));
        }

        var micaColor = Lerp(Color.FromRgb(0x5A, 0x5C, 0x60), Color.FromRgb(0xD8, 0xDC, 0xE0), VolcanoModel.SmoothStep(0.2, 0.7, grade));
        dc.DrawGeometry(FadedBrush(Color.FromRgb(0xE8, 0xE2, 0xD6), 0.9), null, _flakeLight!);
        dc.DrawGeometry(FadedBrush(Color.FromRgb(0x24, 0x1E, 0x1C), 0.95), null, _flakeDark!);
        dc.DrawGeometry(FadedBrush(micaColor, 0.95), null, _flakeMica!);

        foreach (var (x, y, r) in SqueezeTexture.Garnets(grade))
        {
            var c = new Point(box.Left + (x * box.Width), box.Top + (y * box.Height));
            var radius = r * box.Width;
            var hex = Enumerable.Range(0, 6).Select(i => c + new Vector(Math.Cos((i * Math.PI / 3) + 0.3) * radius, Math.Sin((i * Math.PI / 3) + 0.3) * radius));
            dc.DrawGeometry(FadedBrush(Color.FromRgb(0xA8, 0x24, 0x3A), 1), FadedPen(Color.FromRgb(0x5A, 0x10, 0x1C), 1, 1.2), Shape(hex));
            dc.DrawEllipse(FadedBrush(Colors.White, 0.35), null, c + new Vector(-radius * 0.3, -radius * 0.3), radius * 0.25, radius * 0.2);
        }

        foreach (var (x, y, r) in SqueezeTexture.MeltPockets(grade))
        {
            var c = new Point(box.Left + (x * box.Width), box.Top + (y * box.Height));
            var radius = r * box.Width;
            dc.DrawEllipse(SoftBrush(Ink.LavaHot, 0.8 + (0.15 * Math.Sin(now * 3))), null, c, radius * 1.8, radius);
        }

        _squeezeLabels.Clear();
        var garnet = SqueezeTexture.Garnets(grade).OrderByDescending(g => g.Radius).Select(g => ((double, double, double)?)g).FirstOrDefault();
        if (garnet is var (gx, gy, gr)) _squeezeLabels.Add(("garnet", new Point(box.Left + (gx * box.Width), box.Top + (gy * box.Height)), gr * box.Width));
        var pocket = SqueezeTexture.MeltPockets(grade).Select(m => ((double, double, double)?)m).FirstOrDefault();
        if (pocket is var (mx, my, mr)) _squeezeLabels.Add(("meltpocket", new Point(box.Left + (mx * box.Width), box.Top + (my * box.Height)), mr * box.Width * 1.5));
        if (banding > 0.5) _squeezeLabels.Add(("bands", new Point(box.Left + (box.Width * 0.82), box.Top + (box.Height * (0.5 + (SqueezeTexture.BandSpacing * 0.5)))), box.Width * 0.06));
        else _squeezeLabels.Add(("mica", new Point(box.Left + (box.Width * 0.2), box.Top + (box.Height * 0.22)), box.Width * 0.07));
    }

    /// <summary>Things inside the squeezed sample worth a label: id, where, and how big.</summary>
    private readonly List<(string Id, Point At, double Radius)> _squeezeLabels = [];

    /// <summary>The labels of the things inside the sample, outside it on the left with a line in, or rings when labels are off.</summary>
    private void DrawSqueezeLabels(PaintContext dc, Rect box)
    {
        var y = box.Top + 18;
        foreach (var (id, at, radius) in _squeezeLabels)
        {
            _targets.Add(id, at, Math.Max(8, radius));
            if (!ShowLabels) continue;
            var key = ObjectTitleKey(id)!;
            var pill = DrawTargetPill(dc, Loc(key), new Point(box.Right + 2 + (_labels.Text(Loc(key), 10.5, Ink.Text).Width / 2) + 10, y), Ink.Text, id, 10.5);
            dc.DrawLine(FadedPen(Ink.Text, 0.6, 1), new Point(pill.Left, pill.Top + (pill.Height / 2)), at);
            y += 30;
        }
    }

    /// <summary>The flakes of one kind as rotated rectangles in one geometry.</summary>
    private static StreamGeometry Flakes(Rect box, double grade, FlakeKind kind)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            for (var i = 0; i < SqueezeTexture.FlakeCount; i++)
            {
                var f = SqueezeTexture.FlakeAt(i, grade);
                if (f.Kind != kind) continue;
                var c = new Point(box.Left + (f.X * box.Width), box.Top + (f.Y * box.Height));
                if (kind != FlakeKind.Mica)
                {
                    // Quartz, feldspar and the dark minerals are stubby grains, not flakes.
                    var r = f.Length * box.Width / 2;
                    g.BeginFigure(c + new Vector(Math.Cos(f.Angle) * r, Math.Sin(f.Angle) * r * 0.8), true);
                    for (var k = 1; k < 6; k++)
                    {
                        var a = f.Angle + (k * Math.PI / 3);
                        g.LineTo(c + new Vector(Math.Cos(a) * r, Math.Sin(a) * r * 0.8));
                    }

                    g.EndFigure(true);
                    continue;
                }

                var along = new Vector(Math.Cos(f.Angle), Math.Sin(f.Angle)) * (f.Length * box.Width / 2);
                var across = new Vector(-Math.Sin(f.Angle), Math.Cos(f.Angle)) * (f.Width * box.Width / 2);
                g.BeginFigure(c - along - across, true);
                g.LineTo(c + along - across);
                g.LineTo(c + along + across);
                g.LineTo(c - along + across);
                g.EndFigure(true);
            }
        }
        return geometry;
    }

    /// <summary>Sandstone or limestone: grains that grow into each other as the gaps between them close; shell bits in limestone fade.</summary>
    private void DrawGrains(PaintContext dc, Rect box, double grade)
    {
        var limestone = SqueezeParent == "Limestone";
        var key = $"{SqueezeParent}|{Math.Round(grade, 2):0.00}";
        if (_grainKey != key || _grainBitmap is null)
        {
            _grainKey = key;
            _grainBitmap = GrainBitmap(grade, limestone);
        }

        SpaceDrawing.DrawSmooth(dc, _grainBitmap, box);
        _squeezeLabels.Clear();
        var fossils = SqueezeTexture.Fossils(grade);
        if (limestone && fossils > 0.02)
        {
            var pen = FadedPen(Color.FromRgb(0xF6, 0xF0, 0xE0), fossils * 0.95, 2.2);
            for (var i = 0; i < 14; i++)
            {
                var c = new Point(box.Left + (SqueezeTexture.Hash(i, 151) * box.Width), box.Top + (SqueezeTexture.Hash(i, 152) * box.Height));
                var r = box.Width * (0.03 + (0.03 * SqueezeTexture.Hash(i, 153)));
                var a0 = SqueezeTexture.Hash(i, 154) * Math.PI * 2;
                var arc = new List<Point>();
                for (var a = 0.0; a <= Math.PI * 1.1; a += 0.15) arc.Add(c + new Vector(Math.Cos(a0 + a) * r, Math.Sin(a0 + a) * r * 0.7));
                dc.DrawGeometry(null, pen, Line(arc));
            }
        }

        if (limestone && fossils > 0.5)
        {
            _squeezeLabels.Add(("fossil", new Point(box.Left + (SqueezeTexture.Hash(0, 151) * box.Width), box.Top + (SqueezeTexture.Hash(0, 152) * box.Height)), box.Width * 0.05));
        }

        _squeezeLabels.Add(("grains", new Point(box.Left + (box.Width * 0.7), box.Top + (box.Height * 0.62)), box.Width * 0.06));
    }

    /// <summary>Grains as cells round their seeds (the nearest seed wins), gaps where two seeds are nearly as near, at 200 × 200.</summary>
    private static WriteableBitmap GrainBitmap(double grade, bool limestone)
    {
        const int n = 200;
        var count = SqueezeTexture.GrainCount(grade);
        var seeds = Enumerable.Range(0, count).Select(SqueezeTexture.GrainSeed).ToArray();
        var gap = limestone ? 0.0018 + (0.0018 * (1 - grade)) : 0.003 + (0.05 * SqueezeTexture.PoreShare(grade));
        var grid = (int)Math.Max(1, Math.Sqrt(count) / 1.5);
        var buckets = new List<int>[grid, grid];
        for (var i = 0; i < grid; i++)
        {
            for (var j = 0; j < grid; j++) buckets[i, j] = [];
        }

        for (var i = 0; i < seeds.Length; i++)
        {
            buckets[Math.Min(grid - 1, (int)(seeds[i].X * grid)), Math.Min(grid - 1, (int)(seeds[i].Y * grid))].Add(i);
        }

        var pixels = new int[n * n];
        for (var py = 0; py < n; py++)
        {
            for (var px = 0; px < n; px++)
            {
                var x = (px + 0.5) / n;
                var y = (py + 0.5) / n;
                double best = double.MaxValue, second = double.MaxValue;
                var bestIndex = 0;
                var bx = Math.Min(grid - 1, (int)(x * grid));
                var by = Math.Min(grid - 1, (int)(y * grid));
                for (var gx = Math.Max(0, bx - 2); gx <= Math.Min(grid - 1, bx + 2); gx++)
                {
                    for (var gy = Math.Max(0, by - 2); gy <= Math.Min(grid - 1, by + 2); gy++)
                    {
                        foreach (var i in buckets[gx, gy])
                        {
                            var dx = seeds[i].X - x;
                            var dy = seeds[i].Y - y;
                            var d = Math.Sqrt((dx * dx) + (dy * dy));
                            if (d < best)
                            {
                                second = best;
                                best = d;
                                bestIndex = i;
                            }
                            else if (d < second)
                            {
                                second = d;
                            }
                        }
                    }
                }

                Color color;
                if (second - best < gap)
                {
                    // Between grains: blue-dyed pore space in sandstone, a fine dark line where grains have grown together.
                    color = !limestone && SqueezeTexture.PoreShare(grade) > 0.01 ? Color.FromRgb(0x2C, 0x52, 0x8A) : Color.FromRgb(0x4A, 0x44, 0x3C);
                }
                else
                {
                    var tone = SqueezeTexture.Hash(bestIndex, 161);
                    color = limestone
                        ? Lerp(Color.FromRgb(0xC8, 0xBE, 0xA6), Color.FromRgb(0xF4, 0xF1, 0xEA), (tone * 0.6) + (0.4 * grade))
                        : Lerp(Color.FromRgb(0xC8, 0xA8, 0x78), Color.FromRgb(0xF0, 0xEA, 0xDE), (tone * 0.7) + (0.3 * grade));
                }

                pixels[(py * n) + px] = (255 << 24) | (color.R << 16) | (color.G << 8) | color.B;
            }
        }

        var bitmap = new WriteableBitmap(new PixelSize(n, n), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        SpaceDrawing.WritePixels(bitmap, pixels, n, n);
        return bitmap;
    }

    /// <summary>A depth scale from 0 to 30 km with the rocks shale turns into on it, and where the sample is now.</summary>
    private void DrawDepthScale(PaintContext dc, Rect side, double grade)
    {
        var rock = VolcanoModel.MetamorphicRock(SqueezeParent, SqueezeDepthKm);
        DrawText(dc, Loc($"Volc_State_{rock}"), new Point(side.Left, side.Top + 12), Ink.Gold, 20, bold: true);
        DrawText(dc, LocFormat("Volc_Label_DepthNow", Number(SqueezeDepthKm, "0"), Number(VolcanoModel.TemperatureAt(SqueezeDepthKm), "0"),
            Number(VolcanoModel.KilobarsAt(SqueezeDepthKm), "0.0")), new Point(side.Left, side.Top + 40), Ink.Text, 11.5);

        var bar = new Rect(side.Left + 4, side.Top + 64, 16, Math.Max(60, side.Height - 76));
        double Y(double km) => bar.Top + (km / 30 * bar.Height);
        var zones = SqueezeParent switch
        {
            "Sandstone" => new (double From, double To, string Rock)[] { (0, 9.4, "Sandstone"), (9.4, 30, "Quartzite") },
            "Limestone" => [(0, 9.4, "Limestone"), (9.4, 30, "Marble")],
            _ => [(0, 7.4, "Shale"), (7.4, 12.6, "Slate"), (12.6, 16.2, "Phyllite"), (16.2, 23.4, "Schist"), (23.4, 27.4, "Gneiss"), (27.4, 30, "Migmatite")],
        };
        foreach (var (from, to, name) in zones)
        {
            var t = Math.Clamp(((from + to) / 2) / 30, 0, 1);
            var color = Lerp(Color.FromRgb(0x4A, 0x52, 0x66), Color.FromRgb(0xC8, 0x4A, 0x2A), t);
            var band = new Rect(bar.Left, Y(from), bar.Width, Math.Max(1, Y(to) - Y(from)));
            dc.DrawRectangle(FadedBrush(color, 0.9), Ink.Frame, band);
            DrawText(dc, Loc($"Volc_State_{name}"), new Point(bar.Right + 10, band.Top + (band.Height / 2)), name == rock ? Ink.Gold : Ink.Dim, 11, bold: name == rock);
        }

        for (var km = 0; km <= 30; km += 10)
        {
            DrawText(dc, Km(km), new Point(bar.Left - 4, Y(km)), Ink.Dim, 9.5, TextAnchor.Right);
        }

        var y = Y(Math.Clamp(SqueezeDepthKm, 0, 30));
        dc.DrawGeometry(Brushes.White, null, Polygon(new Point(bar.Left - 2, y), new Point(bar.Left - 11, y - 6), new Point(bar.Left - 11, y + 6)));
        dc.DrawLine(FadedPen(Colors.White, 0.9, 2), new Point(bar.Left, y), new Point(bar.Right, y));
        _ = grade;
    }

    /// <summary>The thing under the mouse in the squeezed sample: a garnet, a melt pocket, the bands, the flakes or the grains.</summary>
    private string? SqueezeThingAt(Point point)
    {
        var box = _squeezeSample;
        if (!box.Contains(point)) return null;
        var grade = _shownGrade < 0 ? VolcanoModel.Grade(SqueezeDepthKm) : _shownGrade;
        var u = (point.X - box.Left) / box.Width;
        var v = (point.Y - box.Top) / box.Height;
        if (SqueezeParent != "Shale")
        {
            return SqueezeParent == "Limestone" && SqueezeTexture.Fossils(grade) > 0.5 ? "fossil" : "grains";
        }

        foreach (var (x, y, r) in SqueezeTexture.MeltPockets(grade))
        {
            if (Math.Abs(u - x) < r * 1.8 && Math.Abs(v - y) < r) return "meltpocket";
        }

        foreach (var (x, y, r) in SqueezeTexture.Garnets(grade))
        {
            if (((u - x) * (u - x)) + ((v - y) * (v - y)) < r * r * 1.4) return "garnet";
        }

        return SqueezeTexture.Banding(grade) > 0.5 ? "bands" : "mica";
    }

    #endregion
}
