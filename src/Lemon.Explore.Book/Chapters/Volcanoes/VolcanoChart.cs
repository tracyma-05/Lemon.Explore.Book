using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Volcanoes;

/// <summary>
/// The side panel's chart, one per view. Eruption: how sticky the magma is against how much gas it holds, coloured by the
/// style of eruption, with real volcanoes on it and the magma now (drag it). Cycle: the rock cycle as a diagram, the step
/// the followed grain is on lit up. Magnifier: Bowen's order of crystallisation with the temperature now (cooling); the
/// real time each grain size takes to sink a metre (settling); temperature against depth with the rocks shale becomes and
/// the sample now (squeezing, drag it).
/// </summary>
public sealed class VolcanoChart : Control
{
    static VolcanoChart()
    {
        AffectsRender<VolcanoChart>(ModeProperty, ProcessProperty, SilicaProperty, WaterProperty, JourneyIdProperty, ProgressProperty, CrystalsProperty, SqueezeParentProperty, SqueezeDepthKmProperty);
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    public static readonly StyledProperty<VolcanoMode> ModeProperty =
        AvaloniaProperty.Register<VolcanoChart, VolcanoMode>(nameof(Mode), VolcanoMode.Eruption);

    public static readonly StyledProperty<MagnifierProcess> ProcessProperty =
        AvaloniaProperty.Register<VolcanoChart, MagnifierProcess>(nameof(Process), MagnifierProcess.Cooling);

    public static readonly StyledProperty<double> SilicaProperty =
        AvaloniaProperty.Register<VolcanoChart, double>(nameof(Silica), 50.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> WaterProperty =
        AvaloniaProperty.Register<VolcanoChart, double>(nameof(Water), 0.5, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> JourneyIdProperty =
        AvaloniaProperty.Register<VolcanoChart, string?>(nameof(JourneyId));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<VolcanoChart, double>(nameof(Progress), 0.0);

    /// <summary>The cooling magma: its kind and temperature now. Its steps do not notify, so the chart redraws a few times a second.</summary>
    public static readonly StyledProperty<CrystalSim?> CrystalsProperty =
        AvaloniaProperty.Register<VolcanoChart, CrystalSim?>(nameof(Crystals));

    public static readonly StyledProperty<string> SqueezeParentProperty =
        AvaloniaProperty.Register<VolcanoChart, string>(nameof(SqueezeParent), "Shale");

    public static readonly StyledProperty<double> SqueezeDepthKmProperty =
        AvaloniaProperty.Register<VolcanoChart, double>(nameof(SqueezeDepthKm), 4.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer drags (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<VolcanoChart, ICommand?>(nameof(InteractionCommand));



    public VolcanoMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public MagnifierProcess Process
    {
        get => GetValue(ProcessProperty);
        set => SetValue(ProcessProperty, value);
    }

    public double Silica
    {
        get => GetValue(SilicaProperty);
        set => SetValue(SilicaProperty, value);
    }

    public double Water
    {
        get => GetValue(WaterProperty);
        set => SetValue(WaterProperty, value);
    }

    public string? JourneyId
    {
        get => GetValue(JourneyIdProperty);
        set => SetValue(JourneyIdProperty, value);
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public CrystalSim? Crystals
    {
        get => GetValue(CrystalsProperty);
        set => SetValue(CrystalsProperty, value);
    }

    public string SqueezeParent
    {
        get => GetValue(SqueezeParentProperty);
        set => SetValue(SqueezeParentProperty, value);
    }

    public double SqueezeDepthKm
    {
        get => GetValue(SqueezeDepthKmProperty);
        set => SetValue(SqueezeDepthKmProperty, value);
    }

    public ICommand? InteractionCommand
    {
        get => GetValue(InteractionCommandProperty);
        set => SetValue(InteractionCommandProperty, value);
    }

    private const double MinLogEta = 1;
    private const double MaxLogEta = 9.5;

    private static readonly Color DimColor = Color.FromRgb(0x9A, 0xA5, 0xB5);
    private static readonly Color TextColor = Color.FromRgb(0xEE, 0xF1, 0xF6);
    private static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    private static readonly IPen GridPen = MakePen(Solid(26, 255, 255, 255), 1);
    private static readonly IPen FramePen = MakePen(Solid(60, 255, 255, 255), 1);
    private static readonly IPen CursorPen = MakePen(Solid(240, 255, 255, 255), 1.6);

    private readonly LabelCache _labels;
    private readonly Avalonia.Threading.DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(0.25) };
    private WriteableBitmap? _styleMap;
    private Size _styleMapSize;
    private bool _dragging;

    public VolcanoChart()
    {
        _labels = new LabelCache(this);
        _tick.Tick += (_, _) =>
        {
            if (Mode == VolcanoMode.Magnifier && Process == MagnifierProcess.Cooling) InvalidateVisual();
        };
        Loaded += (_, _) =>
        {
            LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
            _tick.Start();
        };
        Unloaded += (_, _) =>
        {
            LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
            _tick.Stop();
        };
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    private Rect Plot(double left = 40, double bottom = 30) => new(left, 10, Math.Max(10, ActualWidth - left - 12), Math.Max(10, ActualHeight - 10 - bottom));

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 1 || ActualHeight < 1) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        switch (Mode)
        {
            case VolcanoMode.Cycle:
                DrawCycle(dc);
                break;

            case VolcanoMode.Magnifier:
                switch (Process)
                {
                    case MagnifierProcess.Settling:
                        DrawSinkTimes(dc);
                        break;

                    case MagnifierProcess.Squeezing:
                        DrawDepthTemperature(dc);
                        break;

                    default:
                        DrawBowen(dc);
                        break;
                }

                break;

            default:
                DrawStyleMap(dc);
                break;
        }
    }

    #region eruption: the style map

    internal static Color StyleColor(EruptionStyle style) => style switch
    {
        EruptionStyle.Hawaiian => Color.FromRgb(0xC8, 0x5A, 0x1E),
        EruptionStyle.Strombolian => Color.FromRgb(0xB8, 0x80, 0x24),
        EruptionStyle.Dome => Color.FromRgb(0x7A, 0x52, 0x66),
        EruptionStyle.Vulcanian => Color.FromRgb(0x56, 0x6A, 0x86),
        _ => Color.FromRgb(0x3E, 0x4A, 0x66),
    };

    private void DrawStyleMap(PaintContext dc)
    {
        var plot = Plot(34, 30);
        if (_styleMap is null || _styleMapSize != plot.Size)
        {
            _styleMapSize = plot.Size;
            var w = Math.Max(1, (int)plot.Width);
            var h = Math.Max(1, (int)plot.Height);
            var pixels = new int[w * h];
            for (var y = 0; y < h; y++)
            {
                var water = VolcanoModel.MaxWater * (1 - (y / (double)h));
                for (var x = 0; x < w; x++)
                {
                    var eta = MinLogEta + ((MaxLogEta - MinLogEta) * x / w);
                    var c = StyleColor(VolcanoModel.Style(eta, water));
                    pixels[(y * w) + x] = (200 << 24) | (c.R << 16) | (c.G << 8) | c.B;
                }
            }

            _styleMap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            SpaceDrawing.WritePixels(_styleMap, pixels, w, h);
        }

        SpaceDrawing.DrawSmooth(dc, _styleMap, plot);
        dc.DrawRectangle(null, FramePen, plot);

        // The styles' names in their regions.
        Label(dc, Loc("Volc_StyleShort_Hawaiian"), Point(plot, 1.12, 0.22), TextColor, size: 9.5);
        Label(dc, Loc("Volc_StyleShort_Strombolian"), Point(plot, 1.12, 4.6), TextColor, size: 9.5);
        Label(dc, Loc("Volc_StyleShort_Dome"), Point(plot, 7.6, 0.55), TextColor, center: true);
        Label(dc, Loc("Volc_StyleShort_Vulcanian"), Point(plot, 6.6, 2.5), TextColor, center: true);
        Label(dc, Loc("Volc_StyleShort_Plinian"), Point(plot, 4.1, 6.5), TextColor, center: true);

        // Axes.
        for (var e = 2; e <= 9; e += 2)
        {
            var p = Point(plot, e, 0);
            dc.DrawLine(GridPen, new Point(p.X, plot.Top), new Point(p.X, plot.Bottom));
            Label(dc, "10" + Superscript(e), new Point(p.X, plot.Bottom + 9), DimColor, center: true);
        }

        for (var w = 0; w <= 6; w += 2)
        {
            var p = Point(plot, MinLogEta, w);
            Label(dc, w.ToString(CultureInfo.InvariantCulture), new Point(plot.Left - 6, p.Y), DimColor, right: true);
        }

        Label(dc, Loc("Volc_Chart_Viscosity"), new Point(plot.Left + (plot.Width / 2), plot.Bottom + 22), DimColor, center: true);
        Label(dc, Loc("Volc_Chart_Water"), new Point(plot.Left + 4, plot.Top + 8), DimColor);

        // Real volcanoes, and the magma now.
        foreach (var preset in VolcanoModel.Presets)
        {
            var p = Point(plot, VolcanoModel.LogViscosity(preset.Silica), preset.Water);
            dc.DrawEllipse(Brushes.White, null, p, 2.6, 2.6);
            var (offset, right) = preset.Id switch
            {
                "Wudalianchi" => (new Vector(6, -1), false),
                "Merapi" => (new Vector(6, 6), false),
                "Changbaishan" => (new Vector(-6, -8), true),
                _ => (new Vector(6, -7), false),
            };
            Label(dc, Loc($"Volc_Preset_{preset.Id}_Short"), p + offset, TextColor, right: right, size: 9.5);
        }

        var now = Point(plot, VolcanoModel.LogViscosity(Silica), Water);
        dc.DrawEllipse(Solid(255, Gold.R, Gold.G, Gold.B), MakePen(Brushes.Black, 1.2), now, 6, 6);
    }

    private static Point Point(Rect plot, double logEta, double water) =>
        new(plot.Left + ((logEta - MinLogEta) / (MaxLogEta - MinLogEta) * plot.Width), plot.Bottom - (water / VolcanoModel.MaxWater * plot.Height));

    /// <summary>The silica that gives <paramref name="logEta"/> (viscosity rises steadily with silica).</summary>
    internal static double SilicaFor(double logEta)
    {
        double low = VolcanoModel.MinSilica, high = VolcanoModel.MaxSilica;
        for (var i = 0; i < 40; i++)
        {
            var mid = (low + high) / 2;
            if (VolcanoModel.LogViscosity(mid) < logEta) low = mid;
            else high = mid;
        }

        return (low + high) / 2;
    }

    #endregion

    #region cycle

    private void DrawCycle(PaintContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        var nodes = new Dictionary<string, Point>
        {
            ["Igneous"] = new(w * 0.2, h * 0.2),
            ["Sediment"] = new(w * 0.8, h * 0.2),
            ["Sedimentary"] = new(w * 0.8, h * 0.66),
            ["Metamorphic"] = new(w * 0.5, h * 0.88),
            ["Magma"] = new(w * 0.2, h * 0.66),
        };

        var journey = JourneyId is null ? null : VolcanoModel.JourneyOf(JourneyId);
        var step = journey is null ? -1 : VolcanoDiagram.JourneyPosition(journey, Progress).Step;
        var active = journey is null ? (RockProcess?)null : journey.Steps[step].Process;
        var state = journey is null ? null : journey.Steps[Math.Max(0, step - 1)].State;
        var activeNode = state is null ? null : NodeOf(state);

        void Arrow(string from, string to, string key, RockProcess[] processes, double labelU, double labelV, int align = 0, bool shortcut = false, double bend = 0.18)
        {
            var a = nodes[from];
            var b = nodes[to];
            var mid = a + ((b - a) / 2);
            var normal = new Vector(-(b - a).Y, (b - a).X);
            normal = normal.Normalize();
            var control = mid + (normal * ((Vector)(b - a)).Length * bend);
            var lit = active is { } p && processes.Contains(p);
            var color = lit ? Gold : shortcut ? DimColor : TextColor;
            var thickness = lit ? 2.6 : shortcut ? 1.1 : 1.6;
            var points = new List<Point>();
            for (var i = 0; i <= 20; i++)
            {
                var t = 0.16 + (i / 20.0 * 0.68);
                var p1 = a + ((control - a) * t);
                var p2 = control + ((b - control) * t);
                points.Add(p1 + ((p2 - p1) * t));
            }

            var pen = new Pen(new SolidColorBrush(color), thickness);
            if (shortcut) pen.DashStyle = new DashStyle([3, 3], 0);
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(points[0], false);
                foreach (var next in points.Skip(1).ToList()) g.LineTo(next);

                g.EndFigure(false);
            }

            dc.DrawGeometry(null, pen, geometry);
            Vector dir = points[^1] - points[^2];
            dir = dir.Normalize();
            DrawArrowHead(dc, new SolidColorBrush(color), points[^1], dir, lit ? 9 : 7);
            Label(dc, Loc(key), new Point(w * labelU, h * labelV), color, right: align < 0, center: align == 0, size: 9.5);
        }

        Arrow("Igneous", "Sediment", "Volc_Chart_Weather", [RockProcess.Weathering, RockProcess.Erosion], 0.5, 0.05, bend: -0.12);
        Arrow("Sediment", "Sedimentary", "Volc_Chart_Harden", [RockProcess.Deposition, RockProcess.Lithification], 0.765, 0.43, align: -1, bend: 0.02);
        Arrow("Sedimentary", "Metamorphic", "Volc_Chart_Heat", [RockProcess.Metamorphism], 0.83, 0.86, bend: 0.12);
        Arrow("Metamorphic", "Magma", "Volc_Chart_Melt", [RockProcess.Melting], 0.2, 0.86, bend: 0.12);
        Arrow("Magma", "Igneous", "Volc_Chart_Cool", [RockProcess.Crystallization, RockProcess.Eruption], 0.17, 0.43, align: -1, bend: 0.02);
        Arrow("Sedimentary", "Sediment", "Volc_Chart_Uplift", [RockProcess.Uplift], 0.94, 0.43, shortcut: true, bend: 0.3);

        foreach (var (name, p) in nodes)
        {
            var lit = name == activeNode;
            var text = _labels.Text(Loc($"Volc_Family_{name}"), 11.5, lit ? Colors.Black : TextColor, bold: true);
            var box = new Rect(p.X - (text.Width / 2) - 9, p.Y - (text.Height / 2) - 4, text.Width + 18, text.Height + 8);
            dc.DrawRoundedRectangle(lit ? new SolidColorBrush(Gold) : Solid(220, 0x16, 0x1C, 0x2A), MakePen(new SolidColorBrush(lit ? Gold : NodeColor(name)), 1.5), box, 10, 10);
            dc.DrawText(text, new Point(box.Left + 9, box.Top + 4));
        }
    }

    /// <summary>The node of the cycle diagram a grain's state belongs to.</summary>
    internal static string NodeOf(string state) => VolcanoModel.FamilyOf(state) switch
    {
        RockFamily.Igneous => "Igneous",
        RockFamily.Sedimentary => "Sedimentary",
        RockFamily.Metamorphic => "Metamorphic",
        _ => state is "Magma" or "Lava" ? "Magma" : "Sediment",
    };

    private static Color NodeColor(string node) => node switch
    {
        "Igneous" => Color.FromRgb(0xC4, 0xA6, 0x9A),
        "Sediment" => Color.FromRgb(0xD9, 0xBE, 0x84),
        "Sedimentary" => Color.FromRgb(0xCF, 0xCB, 0xB4),
        "Metamorphic" => Color.FromRgb(0xB0, 0x8C, 0xFF),
        _ => Color.FromRgb(0xFF, 0x9A, 0x4A),
    };

    #endregion

    #region magnifier

    /// <summary>Bowen's order: each mineral's bar from the temperature it starts at down to the solidus, and the temperature now.</summary>
    private void DrawBowen(PaintContext dc)
    {
        if (Crystals is not { } sim) return;
        var (liquidus, solidus) = CrystalSim.Range(sim.Magma);
        var top = liquidus + 20;
        var bottom = solidus - 20;
        var plot = Plot(46, 34);
        double Y(double t) => plot.Top + ((top - t) / (top - bottom) * plot.Height);
        for (var t = Math.Ceiling(bottom / 50) * 50; t <= top; t += 50)
        {
            dc.DrawLine(GridPen, new Point(plot.Left, Y(t)), new Point(plot.Right, Y(t)));
            Label(dc, LocalizationService.Instance.Format("Volc_Value_Celsius", t.ToString("0", CultureInfo.InvariantCulture)), new Point(plot.Left - 4, Y(t)), DimColor, right: true, size: 9.5);
        }

        var minerals = sim.Minerals;
        var column = plot.Width / minerals.Count;
        for (var i = 0; i < minerals.Count; i++)
        {
            var m = minerals[i];
            var color = MineralSwatch(m.Id);
            var x = plot.Left + (i * column) + (column * 0.2);
            var bar = new Rect(x, Y(m.StartC), column * 0.6, Math.Max(1, Y(solidus) - Y(m.StartC)));
            dc.DrawRoundedRectangle(Solid(220, color.R, color.G, color.B), FramePen, bar, 3, 3);
            Label(dc, Loc($"Volc_Mineral_{m.Id}"), new Point(x + (column * 0.3), plot.Bottom + 10), TextColor, center: true, size: 9.5);
        }

        dc.DrawLine(MakePen(Brushes.OrangeRed, 1.2, dashed: true), new Point(plot.Left, Y(solidus)), new Point(plot.Right, Y(solidus)));
        var y = Y(sim.Temperature);
        dc.DrawLine(CursorPen, new Point(plot.Left, y), new Point(plot.Right, y));
        Label(dc, Loc("Volc_Chart_Bowen"), new Point(plot.Left, plot.Bottom + 25), DimColor, size: 9.5);
    }

    private static Color MineralSwatch(string id) => id switch
    {
        "Quartz" => Color.FromRgb(0xD6, 0xD9, 0xDF),
        "KFeldspar" => Color.FromRgb(0xE2, 0xA2, 0x8E),
        "Plagioclase" => Color.FromRgb(0xEE, 0xE9, 0xDC),
        "Biotite" => Color.FromRgb(0x6A, 0x52, 0x44),
        "Olivine" => Color.FromRgb(0x8C, 0xB0, 0x4A),
        _ => Color.FromRgb(0x5C, 0x6C, 0x52),
    };

    /// <summary>The real time each grain size takes to sink one metre in still water, on a scale of powers of ten.</summary>
    private void DrawSinkTimes(PaintContext dc)
    {
        var plot = Plot(64, 26);
        const double minLog = 0;
        const double maxLog = 6;
        double X(double seconds) => plot.Left + ((Math.Log10(Math.Max(1, seconds)) - minLog) / (maxLog - minLog) * plot.Width);
        var marks = new (double Seconds, string Key)[] { (1, "Volc_Chart_Second"), (60, "Volc_Chart_Minute"), (3600, "Volc_Chart_Hour"), (86400, "Volc_Chart_Day") };
        foreach (var (seconds, key) in marks)
        {
            var x = X(seconds);
            dc.DrawLine(GridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            Label(dc, Loc(key), new Point(x, plot.Bottom + 10), DimColor, center: true, size: 9.5);
        }

        var row = plot.Height / VolcanoModel.GrainSizes.Count;
        for (var i = 0; i < VolcanoModel.GrainSizes.Count; i++)
        {
            var (id, diameter) = VolcanoModel.GrainSizes[i];
            var seconds = VolcanoModel.SecondsPerMetre(diameter);
            var y = plot.Top + (i * row) + (row * 0.2);
            var color = i switch
            {
                0 => Color.FromRgb(0x8E, 0x84, 0x7A),
                1 => Color.FromRgb(0xD9, 0xBE, 0x84),
                2 => Color.FromRgb(0xA8, 0x8E, 0x6A),
                _ => Color.FromRgb(0x7E, 0x7A, 0x70),
            };
            var bar = new Rect(plot.Left, y, Math.Max(2, X(seconds) - plot.Left), row * 0.6);
            dc.DrawRectangle(Solid(220, color.R, color.G, color.B), null, bar);
            Label(dc, Loc($"Volc_Grain_{id}"), new Point(plot.Left - 6, y + (row * 0.3)), TextColor, right: true, size: 10);
            Label(dc, VolcanoDiagram.SinkTimeText(seconds), new Point(Math.Min(bar.Right + 4, plot.Right - 40), y + (row * 0.3)), TextColor, size: 9.5);
        }
    }

    /// <summary>Temperature against depth: the usual 25 °C a kilometre, the rocks shale becomes along it, where rock starts to melt, and the sample.</summary>
    private void DrawDepthTemperature(PaintContext dc)
    {
        var plot = Plot(40, 28);
        double X(double celsius) => plot.Left + (celsius / 800 * plot.Width);
        double Y(double km) => plot.Top + (km / 30 * plot.Height);

        // Zones by temperature.
        var zones = new (double From, double To, string Rock)[] { (0, 200, "Shale"), (200, 330, "Slate"), (330, 420, "Phyllite"), (420, 600, "Schist"), (600, 700, "Gneiss"), (700, 800, "Migmatite") };
        foreach (var (from, to, rock) in zones)
        {
            var t = (from + to) / 1600;
            var color = Lerp(Color.FromRgb(0x2A, 0x32, 0x46), Color.FromRgb(0x8A, 0x34, 0x22), t);
            dc.DrawRectangle(Solid(170, color.R, color.G, color.B), null, new Rect(X(from), plot.Top, X(to) - X(from), plot.Height));
            Label(dc, Loc($"Volc_State_{rock}"), new Point(X((from + to) / 2), plot.Top + 9 + ((zones.ToList().FindIndex(z => z.Rock == rock) % 2) * 13)), TextColor, center: true, size: 9);
        }

        dc.DrawRectangle(null, FramePen, plot);
        for (var c = 0; c <= 800; c += 200)
        {
            Label(dc, c.ToString(CultureInfo.InvariantCulture), new Point(X(c), plot.Bottom + 9), DimColor, center: true, size: 9.5);
        }

        for (var km = 0; km <= 30; km += 10)
        {
            Label(dc, km.ToString(CultureInfo.InvariantCulture), new Point(plot.Left - 5, Y(km)), DimColor, right: true, size: 9.5);
        }

        Label(dc, Loc("Volc_Chart_DepthAxis"), new Point(plot.Left + (plot.Width / 2), plot.Bottom + 21), DimColor, center: true, size: 9.5);

        // The usual rise of temperature with depth.
        var geotherm = new StreamGeometry();
        using (var g = geotherm.Open())
        {
            g.BeginFigure(new Point(X(VolcanoModel.TemperatureAt(0)), Y(0)), false);
            g.LineTo(new Point(X(Math.Min(800, VolcanoModel.TemperatureAt(30))), Y(Math.Min(30, (800 - 15) / VolcanoModel.Geotherm))));

            g.EndFigure(false);
        }

        dc.DrawGeometry(null, MakePen(Solid(230, 255, 255, 255), 1.4, dashed: true), geotherm);
        var p = new Point(X(Math.Min(800, VolcanoModel.TemperatureAt(SqueezeDepthKm))), Y(Math.Min(30, SqueezeDepthKm)));
        dc.DrawEllipse(Solid(255, Gold.R, Gold.G, Gold.B), MakePen(Brushes.Black, 1.2), p, 6, 6);
    }

    #endregion

    #region dragging

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (!CanDrag) return;
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        e.Pointer.Capture(this);
        _dragging = true;
        DragTo(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Cursor = CanDrag ? new Cursor(StandardCursorType.Hand) : null;
        if (_dragging) DragTo(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    private bool CanDrag => Mode == VolcanoMode.Eruption || (Mode == VolcanoMode.Magnifier && Process == MagnifierProcess.Squeezing);

    private void DragTo(Point point)
    {
        if (Mode == VolcanoMode.Eruption)
        {
            var plot = Plot(34, 30);
            var logEta = MinLogEta + (Math.Clamp((point.X - plot.Left) / plot.Width, 0, 1) * (MaxLogEta - MinLogEta));
            var water = Math.Clamp((plot.Bottom - point.Y) / plot.Height * VolcanoModel.MaxWater, VolcanoModel.MinWater, VolcanoModel.MaxWater);
            SetCurrentValue(SilicaProperty, Math.Round(SilicaFor(logEta), 1));
            SetCurrentValue(WaterProperty, Math.Round(water, 1));
        }
        else
        {
            var plot = Plot(40, 28);
            SetCurrentValue(SqueezeDepthKmProperty, Math.Round(Math.Clamp((point.Y - plot.Top) / plot.Height * 30, 0, 30), 1));
        }
    }

    #endregion

    private void Label(PaintContext dc, string text, Point at, Color color, bool right = false, bool center = false, double size = 10.5)
    {
        var formatted = _labels.Text(text, size, color);
        var x = right ? at.X - formatted.Width : center ? at.X - (formatted.Width / 2) : at.X;
        dc.DrawText(formatted, new Point(x, at.Y - (formatted.Height / 2)));
    }

    private static string Superscript(int value)
    {
        const string digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var text = Math.Abs(value).ToString(CultureInfo.InvariantCulture);
        return (value < 0 ? "⁻" : string.Empty) + string.Concat(text.Select(c => digits[c - '0']));
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);
}
