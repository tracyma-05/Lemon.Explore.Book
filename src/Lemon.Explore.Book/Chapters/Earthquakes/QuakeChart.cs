using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Earthquakes;

/// <summary>
/// The side panel's chart for the earthquake page. Plates: how many quakes of each size happen in a year. Boundaries: the
/// ridge's magnetic profile, the depths of the quakes down a sinking plate, India's speed through time, or the stored
/// strain on a fault. Inside: the seismogram at the picked station, drawn as the waves arrive.
/// </summary>
public sealed class QuakeChart : Control
{
    static QuakeChart()
    {
        AffectsRender<QuakeChart>(ModeProperty, SectionProperty, SectionTimeProperty, HalfRateProperty, DipProperty, OverOceanProperty, QuakeCountProperty, CreepProperty, StationDistanceProperty, WaveTimeProperty, FamousMagnitudeProperty, MagnitudeProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SectionProperty)
        {
            _drops.Clear();
        }
        else if (change.Property == QuakeCountProperty)
        {
            OnQuake(change.GetOldValue<int>(), change.GetNewValue<int>());
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    public static readonly StyledProperty<QuakeMode> ModeProperty =
        AvaloniaProperty.Register<QuakeChart, QuakeMode>(nameof(Mode), QuakeMode.Plates);

    public static readonly StyledProperty<SectionKind> SectionProperty =
        AvaloniaProperty.Register<QuakeChart, SectionKind>(nameof(Section), SectionKind.Ridge);

    public static readonly StyledProperty<double> SectionTimeProperty =
        AvaloniaProperty.Register<QuakeChart, double>(nameof(SectionTime), 0.0);

    public static readonly StyledProperty<double> HalfRateProperty =
        AvaloniaProperty.Register<QuakeChart, double>(nameof(HalfRate), 2.5);

    public static readonly StyledProperty<double> DipProperty =
        AvaloniaProperty.Register<QuakeChart, double>(nameof(Dip), 40.0);

    public static readonly StyledProperty<bool> OverOceanProperty =
        AvaloniaProperty.Register<QuakeChart, bool>(nameof(OverOcean), false);

    /// <summary>The fault's or plate edge's quakes so far: each new one adds a drop to the strain chart.</summary>
    public static readonly StyledProperty<int> QuakeCountProperty =
        AvaloniaProperty.Register<QuakeChart, int>(nameof(QuakeCount), 0);

    public static readonly StyledProperty<bool> CreepProperty =
        AvaloniaProperty.Register<QuakeChart, bool>(nameof(Creep), false);

    public static readonly StyledProperty<double> StationDistanceProperty =
        AvaloniaProperty.Register<QuakeChart, double>(nameof(StationDistance), 60.0);

    public static readonly StyledProperty<double> WaveTimeProperty =
        AvaloniaProperty.Register<QuakeChart, double>(nameof(WaveTime), 0.0);

    /// <summary>The famous quake picked: its size is marked on the yearly-count chart.</summary>
    public static readonly StyledProperty<double> FamousMagnitudeProperty =
        AvaloniaProperty.Register<QuakeChart, double>(nameof(FamousMagnitude), 0.0);

    /// <summary>The quake's size inside the Earth view: how big the seismogram's swings are.</summary>
    public static readonly StyledProperty<double> MagnitudeProperty =
        AvaloniaProperty.Register<QuakeChart, double>(nameof(Magnitude), 7.0);


    public QuakeMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public SectionKind Section
    {
        get => GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }

    public double SectionTime
    {
        get => GetValue(SectionTimeProperty);
        set => SetValue(SectionTimeProperty, value);
    }

    public double HalfRate
    {
        get => GetValue(HalfRateProperty);
        set => SetValue(HalfRateProperty, value);
    }

    public double Dip
    {
        get => GetValue(DipProperty);
        set => SetValue(DipProperty, value);
    }

    public bool OverOcean
    {
        get => GetValue(OverOceanProperty);
        set => SetValue(OverOceanProperty, value);
    }

    public int QuakeCount
    {
        get => GetValue(QuakeCountProperty);
        set => SetValue(QuakeCountProperty, value);
    }

    public bool Creep
    {
        get => GetValue(CreepProperty);
        set => SetValue(CreepProperty, value);
    }

    public double StationDistance
    {
        get => GetValue(StationDistanceProperty);
        set => SetValue(StationDistanceProperty, value);
    }

    public double WaveTime
    {
        get => GetValue(WaveTimeProperty);
        set => SetValue(WaveTimeProperty, value);
    }

    public double FamousMagnitude
    {
        get => GetValue(FamousMagnitudeProperty);
        set => SetValue(FamousMagnitudeProperty, value);
    }

    public double Magnitude
    {
        get => GetValue(MagnitudeProperty);
        set => SetValue(MagnitudeProperty, value);
    }

    private static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
    private static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
    private static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    private static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
    private static readonly Color Red = Color.FromRgb(0xFF, 0x5A, 0x5F);
    private static readonly Color Violet = Color.FromRgb(0xB0, 0x8C, 0xFF);
    private static readonly IPen Axis = MakePen(Solid(90, 255, 255, 255), 1);
    private static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);

    private readonly LabelCache _labels;

    /// <summary>Section years at which the strain dropped (a quake), for the strain chart.</summary>
    private readonly List<double> _drops = [];

    public QuakeChart()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    private void OnQuake(int before, int after)
    {
        if (after > before) _drops.Add(SectionTime);
        else _drops.Clear();
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var box = new Rect(0, 0, ActualWidth, ActualHeight);
        if (box.Width < 40 || box.Height < 40) return;
        var plot = new Rect(box.X + 40, box.Y + 14, box.Width - 52, box.Height - 40);
        switch (Mode)
        {
            case QuakeMode.Boundaries:
                switch (Section)
                {
                    case SectionKind.Subduction:
                        DrawSlabDepths(dc, plot);
                        break;

                    case SectionKind.Collision:
                        DrawIndiaSpeed(dc, plot);
                        break;

                    case SectionKind.Transform:
                        DrawStrain(dc, plot);
                        break;

                    default:
                        DrawMagneticProfile(dc, plot);
                        break;
                }

                break;

            case QuakeMode.Inside:
                DrawSeismogram(dc, plot);
                break;

            default:
                DrawPerYear(dc, plot);
                break;
        }
    }

    #region plates: how many a year

    private void DrawPerYear(PaintContext dc, Rect plot)
    {
        var rows = PlateModel.PerYear;
        var maxLog = Math.Log10(rows.Max(r => r.PerYear));
        var barWidth = plot.Width / rows.Count;
        dc.DrawLine(Axis, plot.BottomLeft, plot.BottomRight);
        for (var p = 0; p <= 6; p += 2)
        {
            var y = plot.Bottom - (p / maxLog * plot.Height);
            dc.DrawLine(Grid, new Point(plot.X, y), new Point(plot.Right, y));
            DrawText(dc, Short(Math.Pow(10, p)), new Point(plot.X - 4, y), Dim, 9, right: true);
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var (magnitude, perYear) = rows[i];
            var height = Math.Max(2, Math.Log10(perYear) / maxLog * plot.Height);
            var bar = new Rect(plot.X + (i * barWidth) + 4, plot.Bottom - height, barWidth - 8, height);
            var picked = FamousMagnitude >= magnitude && (i == rows.Count - 1 || FamousMagnitude < rows[i + 1].Magnitude);
            var colour = Lerp(Cyan, Red, i / (double)(rows.Count - 1));
            dc.DrawRoundedRectangle(Solid((byte)(picked ? 255 : 190), colour.R, colour.G, colour.B), picked ? MakePen(Solid(255, 255, 255, 255), 1.5) : null, bar, 3, 3);
            var label = i == rows.Count - 1 ? "8+" : magnitude.ToString("0", CultureInfo.InvariantCulture);
            DrawText(dc, label, new Point(bar.X + (bar.Width / 2), plot.Bottom + 9), Text, 10, center: true);
            DrawText(dc, Short(perYear), new Point(bar.X + (bar.Width / 2), bar.Y - 7), Dim, 9, center: true);
        }

        DrawText(dc, Loc("Quake_Chart_Magnitude"), new Point(plot.Right, plot.Bottom + 22), Dim, 9.5, right: true);
    }

    /// <summary>1.3M, 130k, 1,319 …</summary>
    private static string Short(double value) => value switch
    {
        >= 1e6 => (value / 1e6).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 1e4 => (value / 1e3).ToString("0", CultureInfo.InvariantCulture) + "k",
        _ => value.ToString("#,0", CultureInfo.InvariantCulture),
    };

    #endregion

    #region boundaries

    /// <summary>What a ship towing a magnetometer across the ridge reads, over the stripes it crosses.</summary>
    private void DrawMagneticProfile(PaintContext dc, Rect plot)
    {
        const double half = 400;
        var stripes = new Rect(plot.X, plot.Bottom - 14, plot.Width, 14);
        var curve = new Rect(plot.X, plot.Y, plot.Width, plot.Height - 22);
        var middle = curve.Y + (curve.Height / 2);
        dc.DrawLine(Grid, new Point(curve.X, middle), new Point(curve.Right, middle));

        var normal = Solid(255, 0x24, 0x26, 0x30);
        var reversed = Solid(255, 0xE8, 0xE4, 0xD8);
        var points = new List<Point>();
        for (var x = 0; x < (int)plot.Width; x++)
        {
            var km = ((x + 0.5) / plot.Width * 2 * half) - half;
            var isNormal = BoundaryModel.IsNormal(SectionTime + BoundaryModel.Age(km, HalfRate));
            dc.DrawRectangle(isNormal ? normal : reversed, null, new Rect(plot.X + x, stripes.Y, 1.2, stripes.Height));
            points.Add(new Point(plot.X + x, middle - (BoundaryModel.Anomaly(km, HalfRate, SectionTime) * curve.Height * 0.42)));
        }

        dc.DrawRectangle(null, Axis, stripes);
        dc.DrawGeometry(null, MakePen(Solid(255, Gold.R, Gold.G, Gold.B), 1.6), LineOf(points));
        dc.DrawLine(MakePen(Solid(160, Red.R, Red.G, Red.B), 1, dashed: true), new Point(plot.X + (plot.Width / 2), plot.Y), new Point(plot.X + (plot.Width / 2), plot.Bottom));
        DrawText(dc, Loc("Quake_Chart_Ridge_Up"), new Point(plot.X - 4, curve.Y + 6), Dim, 9, right: true);
        DrawText(dc, Loc("Quake_Chart_Ridge_Down"), new Point(plot.X - 4, curve.Bottom - 6), Dim, 9, right: true);
        DrawText(dc, Loc("Quake_Chart_Ridge_Axis"), new Point(plot.X + (plot.Width / 2), plot.Bottom + 10), Text, 9.5, center: true);
        DrawText(dc, LocFormat("Quake_Value_Km", "400"), new Point(plot.X, plot.Bottom + 10), Dim, 9);
        DrawText(dc, LocFormat("Quake_Value_Km", "400"), new Point(plot.Right, plot.Bottom + 10), Dim, 9, right: true);
    }

    /// <summary>The quakes down the sinking plate: depth against distance from the trench, and where the volcanoes are.</summary>
    private void DrawSlabDepths(PaintContext dc, Rect plot)
    {
        const double maxX = 800;
        const double maxDepth = 700;
        double X(double km) => plot.X + (km / maxX * plot.Width);
        double Y(double km) => plot.Y + (km / maxDepth * plot.Height);
        dc.DrawLine(Axis, plot.TopLeft, plot.TopRight);
        dc.DrawLine(Axis, plot.TopLeft, plot.BottomLeft);
        foreach (var depth in new[] { 0.0, 200, 400, 600 })
        {
            dc.DrawLine(Grid, new Point(plot.X, Y(depth)), new Point(plot.Right, Y(depth)));
            DrawText(dc, depth.ToString("0", CultureInfo.InvariantCulture), new Point(plot.X - 4, Y(depth)), Dim, 9, right: true);
        }

        foreach (var (s, into, magnitude) in BoundaryModel.SlabQuakes(OverOcean ? 680 : 660, Dip))
        {
            var (x, depth) = BoundaryModel.SlabTop(s, Dip);
            if (x > maxX) continue;
            var colour = QuakeDiagram.DepthColour(depth);
            var r = 1.2 + ((magnitude - 4.5) * 0.7);
            dc.DrawEllipse(Solid(210, colour.R, colour.G, colour.B), null, new Point(X(x + (into * 0.3)), Y(depth + (into * 0.7))), r, r);
        }

        var arc = BoundaryModel.ArcDistance(Dip);
        dc.DrawGeometry(Solid(255, 0xFF, 0x6A, 0x2A), null, Polygon(new Point(X(arc) - 6, plot.Y), new Point(X(arc) + 6, plot.Y), new Point(X(arc), plot.Y - 9)));
        dc.DrawLine(MakePen(Solid(150, 0xFF, 0x6A, 0x2A), 1, dashed: true), new Point(X(arc), plot.Y), new Point(X(arc), Y(100)));
        DrawText(dc, Loc("Quake_Chart_Slab_Arc"), new Point(X(arc) + 8, plot.Y + 8), Text, 9.5);
        DrawText(dc, Loc("Quake_Chart_Slab_Axis"), new Point(plot.Right, plot.Bottom + 10), Dim, 9.5, right: true);
        DrawText(dc, Loc("Quake_Chart_Depth_Short"), new Point(plot.X + 4, plot.Bottom + 10), Dim, 9.5);
    }

    /// <summary>India's speed north through the last 80 million years; the drop when it met Asia.</summary>
    private void DrawIndiaSpeed(PaintContext dc, Rect plot)
    {
        const double maxAge = 80;
        const double maxSpeed = 20;
        double X(double age) => plot.X + ((maxAge - age) / maxAge * plot.Width);
        double Y(double speed) => plot.Bottom - (speed / maxSpeed * plot.Height);
        dc.DrawLine(Axis, plot.BottomLeft, plot.BottomRight);
        dc.DrawLine(Axis, plot.BottomLeft, plot.TopLeft);
        foreach (var speed in new[] { 0.0, 5, 10, 15, 20 })
        {
            dc.DrawLine(Grid, new Point(plot.X, Y(speed)), new Point(plot.Right, Y(speed)));
            DrawText(dc, speed.ToString("0", CultureInfo.InvariantCulture), new Point(plot.X - 4, Y(speed)), Dim, 9, right: true);
        }

        foreach (var age in new[] { 80.0, 60, 40, 20, 0 })
        {
            DrawText(dc, age.ToString("0", CultureInfo.InvariantCulture), new Point(X(age), plot.Bottom + 9), Dim, 9, center: true);
        }

        var points = Enumerable.Range(0, 161).Select(i => maxAge - (i * 0.5)).Select(age => new Point(X(age), Y(BoundaryModel.IndiaSpeed(age))));
        dc.DrawGeometry(null, MakePen(Solid(255, Gold.R, Gold.G, Gold.B), 1.8), LineOf(points));
        var contact = X(BoundaryModel.ContactAge);
        dc.DrawLine(MakePen(Solid(160, Violet.R, Violet.G, Violet.B), 1, dashed: true), new Point(contact, plot.Y), new Point(contact, plot.Bottom));
        DrawText(dc, Loc("Quake_Chart_India_Contact"), new Point(contact + 4, plot.Y + 8), Violet, 9.5);

        var now = new Point(X(SectionTime), Y(BoundaryModel.IndiaSpeed(SectionTime)));
        dc.DrawLine(MakePen(Solid(120, 255, 255, 255), 1), new Point(now.X, plot.Y), new Point(now.X, plot.Bottom));
        dc.DrawEllipse(Solid(255, 255, 255, 255), null, now, 4, 4);
        DrawText(dc, Loc("Quake_Chart_India_Axis"), new Point(plot.Right, plot.Bottom + 22), Dim, 9.5, right: true);
        DrawText(dc, Loc("Quake_Chart_Speed_Short"), new Point(plot.X + 4, plot.Y - 6), Dim, 9.5);
    }

    /// <summary>The strain stored on the fault through the years: building steadily, dropping at each quake.</summary>
    private void DrawStrain(PaintContext dc, Rect plot)
    {
        var now = SectionTime;
        var span = Math.Max(QuakeDiagram.FaultCycle * 3, now);
        var from = Math.Max(0, now - span);
        double X(double year) => plot.X + ((year - from) / span * plot.Width);
        double Y(double share) => plot.Bottom - (Math.Clamp(share, 0, 1.05) * plot.Height * 0.9);
        dc.DrawLine(Axis, plot.BottomLeft, plot.BottomRight);
        dc.DrawLine(Axis, plot.BottomLeft, plot.TopLeft);
        dc.DrawLine(MakePen(Solid(120, Red.R, Red.G, Red.B), 1, dashed: true), new Point(plot.X, Y(1)), new Point(plot.Right, Y(1)));
        DrawText(dc, Loc("Quake_Chart_Strain_Breaks"), new Point(plot.Right - 2, Y(1) - 8), Red, 9, right: true);

        // Straight rises between the quakes, a drop to nothing at each.
        var drops = _drops.Where(d => d <= now).ToList();
        double LastBefore(double year) => drops.Where(d => d <= year).DefaultIfEmpty(0).Max();
        double Share(double year, double last) => Creep ? 0.05 : (year - last) / QuakeDiagram.FaultCycle;
        var points = new List<Point>();
        var cursor = from;
        foreach (var next in drops.Where(d => d > from).Append(now))
        {
            var last = LastBefore(cursor);
            points.Add(new Point(X(cursor), Y(Share(cursor, last))));
            points.Add(new Point(X(next), Y(Share(next, last))));
            cursor = next;
        }

        if (points.Count > 1) dc.DrawGeometry(null, MakePen(Solid(255, Gold.R, Gold.G, Gold.B), 1.8), LineOf(points));
        foreach (var drop in drops.Where(d => d >= from)) dc.DrawEllipse(Solid(255, Red.R, Red.G, Red.B), null, new Point(X(drop), plot.Bottom - 3), 3, 3);
        DrawText(dc, Loc("Quake_Chart_Strain_Axis"), new Point(plot.Right, plot.Bottom + 10), Dim, 9.5, right: true);
        DrawText(dc, Loc("Quake_Chart_Strain_Short"), new Point(plot.X + 4, plot.Y - 6), Dim, 9.5);
    }

    #endregion

    #region inside: the seismogram

    /// <summary>
    /// A seismogram at the station: quiet until the P wave, a bigger swing at S, the biggest at the slow surface waves,
    /// drawn up to now. The wiggles are made up; when each kind arrives comes from the traced rays.
    /// </summary>
    private void DrawSeismogram(PaintContext dc, Rect plot)
    {
        var distance = StationDistance;
        var arrivals = EarthModel.Arrivals(distance);
        var (p, s) = EarthModel.FirstArrivals(distance);
        var surface = EarthModel.SurfaceWaveTime(distance);
        var core = arrivals.Where(a => a.Phase is Phase.PKIKP or Phase.PKiKP or Phase.PKP).Select(a => (double?)a.Time).FirstOrDefault();
        const double span = QuakeDiagram.WaveDuration;
        double X(double t) => plot.X + (t / span * plot.Width);
        var middle = plot.Y + (plot.Height / 2);

        dc.DrawLine(Grid, new Point(plot.X, middle), new Point(plot.Right, middle));
        for (var minute = 0; minute <= 25; minute += 5)
        {
            dc.DrawLine(Grid, new Point(X(minute * 60), plot.Y), new Point(X(minute * 60), plot.Bottom));
            DrawText(dc, minute.ToString(CultureInfo.InvariantCulture), new Point(X(minute * 60), plot.Bottom + 9), Dim, 9, center: true);
        }

        // Bigger quakes swing harder; far stations feel less. Clipped at the paper's edge like a real drum.
        var size = Math.Pow(10, (Magnitude - 7) * 0.5);
        var spread = 1 / Math.Sqrt(Math.Max(0.15, Math.Sin(Math.Max(5, distance) * Math.PI / 180)));
        var amplitude = size * spread * 0.45;
        var points = new List<Point>();
        var end = Math.Min(WaveTime, span);
        var random = new Random(17);
        for (var x = 0; x <= (int)(X(end) - plot.X); x++)
        {
            var t = x / plot.Width * span;
            var noise = (random.NextDouble() - 0.5) * 0.04;
            var v = 0.0;
            if (p is { } pt) v += Wavelet(t - pt, 9, 0.35, 0.18);
            if (core is { } ct && p is null) v += Wavelet(t - ct, 9, 0.18, 0.18);
            if (s is { } st) v += Wavelet(t - st, 15, 0.7, 0.11);
            if (distance > 2) v += Wavelet(t - surface, 40, 1.0, 0.045) * (1 - Math.Clamp((distance - 100) / 120, 0, 0.6));
            points.Add(new Point(plot.X + x, middle - (Math.Clamp((v * amplitude) + noise, -1, 1) * plot.Height * 0.46)));
        }

        if (points.Count > 1) dc.DrawGeometry(null, MakePen(Solid(255, 0xE8, 0xEC, 0xF2), 1.1), LineOf(points));
        if (points.Count > 0) dc.DrawEllipse(Solid(255, Red.R, Red.G, Red.B), null, points[^1], 2.5, 2.5);

        // When each kind arrives.
        void Mark(double? at, string key, Color colour)
        {
            if (at is not { } t || t > span) return;
            var x = X(t);
            dc.DrawLine(MakePen(Solid(150, colour.R, colour.G, colour.B), 1, dashed: true), new Point(x, plot.Y), new Point(x, plot.Bottom));
            DrawText(dc, Loc(key), new Point(x + 2, plot.Y + 6), colour, 10);
        }

        Mark(p ?? core, p is null && core is not null ? "Quake_Chart_Seis_PKP" : "Quake_Chart_Seis_P", Gold);
        Mark(s, "Quake_Chart_Seis_S", Cyan);
        Mark(distance > 2 ? surface : null, "Quake_Chart_Seis_L", Violet);
        DrawText(dc, Loc("Quake_Chart_Seis_Axis"), new Point(plot.Right, plot.Bottom + 22), Dim, 9.5, right: true);
    }

    /// <summary>A burst of wiggles starting at 0: rising fast, dying away, at a given number of swings per minute.</summary>
    private static double Wavelet(double t, double perMinute, double strength, double decay)
    {
        if (t < 0) return 0;
        var envelope = (1 - Math.Exp(-t / 6)) * Math.Exp(-t * decay * 0.12);
        return strength * envelope * Math.Sin(t / 60 * perMinute * 2 * Math.PI) * (1 + (0.3 * Math.Sin(t * 0.37)));
    }

    #endregion

    private static StreamGeometry LineOf(IEnumerable<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var first = true;
            foreach (var point in points)
            {
                if (first) ctx.BeginFigure(point, false);
                else ctx.LineTo(point);
                first = false;
            }

            if (!first) ctx.EndFigure(false);
        }
        return geometry;
    }

    private void DrawText(PaintContext dc, string text, Point at, Color colour, double size, bool right = false, bool center = false)
    {
        var formatted = _labels.Text(text, size, colour);
        var x = right ? at.X - formatted.Width : center ? at.X - (formatted.Width / 2) : at.X;
        dc.DrawText(formatted, new Point(x, at.Y - (formatted.Height / 2)));
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);
}
