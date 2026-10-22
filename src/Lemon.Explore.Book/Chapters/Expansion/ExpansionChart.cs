using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Expansion;

/// <summary>
/// The side panel's chart, one per view. Hubble: speed against distance for the galaxies round home, a straight line
/// whose slope is the expansion rate then (steeper in the past). Redshift: our universe's size through time, with the
/// light's journey shaded from when it left until now; dragging along the curve picks when the light left. History: the
/// size of the three universes lined up on today, the classic picture of their pasts and futures; clicking a curve picks
/// that universe and dragging moves the moment looked at.
/// </summary>
public sealed class ExpansionChart : Control
{
    static ExpansionChart()
    {
        AffectsRender<ExpansionChart>(ModeProperty, HubbleProgressProperty, HomeProperty, RedshiftProperty, JourneyProperty, UniverseProperty, CosmicTimeProperty);
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

    public static readonly StyledProperty<ExpansionMode> ModeProperty =
        AvaloniaProperty.Register<ExpansionChart, ExpansionMode>(nameof(Mode), ExpansionMode.Hubble);

    public static readonly StyledProperty<double> HubbleProgressProperty =
        AvaloniaProperty.Register<ExpansionChart, double>(nameof(HubbleProgress), 0.0);

    public static readonly StyledProperty<GalaxyCell> HomeProperty =
        AvaloniaProperty.Register<ExpansionChart, GalaxyCell>(nameof(Home), GalaxyCell.MilkyWay);

    /// <summary>Redshift: z of the light; dragging along the size curve picks when it left.</summary>
    public static readonly StyledProperty<double> RedshiftProperty =
        AvaloniaProperty.Register<ExpansionChart, double>(nameof(Redshift), 1.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> JourneyProperty =
        AvaloniaProperty.Register<ExpansionChart, double>(nameof(Journey), 0.0);

    public static readonly StyledProperty<UniverseKind> UniverseProperty =
        AvaloniaProperty.Register<ExpansionChart, UniverseKind>(nameof(Universe), UniverseKind.Ours, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> CosmicTimeProperty =
        AvaloniaProperty.Register<ExpansionChart, double>(nameof(CosmicTime), 13.8, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer starts dragging or clicks (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<ExpansionChart, ICommand?>(nameof(InteractionCommand));



    public ExpansionMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double HubbleProgress
    {
        get => GetValue(HubbleProgressProperty);
        set => SetValue(HubbleProgressProperty, value);
    }

    public GalaxyCell Home
    {
        get => GetValue(HomeProperty);
        set => SetValue(HomeProperty, value);
    }

    public double Redshift
    {
        get => GetValue(RedshiftProperty);
        set => SetValue(RedshiftProperty, value);
    }

    public double Journey
    {
        get => GetValue(JourneyProperty);
        set => SetValue(JourneyProperty, value);
    }

    public UniverseKind Universe
    {
        get => GetValue(UniverseProperty);
        set => SetValue(UniverseProperty, value);
    }

    public double CosmicTime
    {
        get => GetValue(CosmicTimeProperty);
        set => SetValue(CosmicTimeProperty, value);
    }

    public ICommand? InteractionCommand
    {
        get => GetValue(InteractionCommandProperty);
        set => SetValue(InteractionCommandProperty, value);
    }

    private static readonly Color TextColor = Color.FromRgb(0xEE, 0xF1, 0xF6);
    private static readonly Color DimColor = Color.FromRgb(0x9A, 0xA5, 0xB5);
    private static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
    private static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    private static readonly Color Green = Color.FromRgb(0x7C, 0xFF, 0x9E);
    private static readonly Color Red = Color.FromRgb(0xFF, 0x5A, 0x5F);
    private static readonly Color Pink = Color.FromRgb(0xFF, 0x7E, 0xC0);
    private static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
    private static readonly IPen Axis = MakePen(Solid(70, 255, 255, 255), 1);

    private readonly LabelCache _labels;
    private bool _dragging;

    public ExpansionChart()
    {
        _labels = new LabelCache(this);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    /// <summary>The plotting area inside the axes' labels.</summary>
    private Rect Plot => new(40, 12, Math.Max(10, ActualWidth - 54), Math.Max(10, ActualHeight - 40));

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 1 || ActualHeight < 1) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        switch (Mode)
        {
            case ExpansionMode.Redshift:
                DrawJourney(dc);
                break;

            case ExpansionMode.History:
                DrawUniverses(dc);
                break;

            default:
                DrawHubble(dc);
                break;
        }
    }

    #region hubble: speed against distance

    private const double MaxGly = 1.4;
    private const double MaxKms = 30_000;

    private Point HubblePoint(double gly, double kms)
    {
        var plot = Plot;
        return new Point(plot.X + (gly / MaxGly * plot.Width), plot.Bottom - (Math.Min(kms, MaxKms * 1.05) / MaxKms * plot.Height));
    }

    private void DrawHubble(PaintContext dc)
    {
        var plot = Plot;
        var a = HubbleSpan.Scale(HubbleProgress);
        var u = Expansion.Universe.Ours;
        var rate = u.Hubble(a);
        dc.PushClip(new RectangleGeometry(new Rect(plot.X - 4, plot.Y - 4, plot.Width + 8, plot.Height + 8)));
        for (var g = 0.2; g <= MaxGly + 1e-9; g += 0.2) dc.DrawLine(Grid, HubblePoint(g, 0), HubblePoint(g, MaxKms));
        for (var v = 10_000.0; v <= MaxKms; v += 10_000) dc.DrawLine(Grid, HubblePoint(0, v), HubblePoint(MaxGly, v));

        // Today's rate, dashed, and the rate at this moment.
        var maxMpc = MaxGly / Expansion.Universe.GlyPerMpc;

        // Each line runs until it leaves the plot, at its top or at its right.
        Point LineEnd(double h) => HubblePoint(Math.Min(MaxGly, MaxKms * 1.05 / h * Expansion.Universe.GlyPerMpc), Math.Min(MaxKms * 1.05, h * maxMpc));
        dc.DrawLine(MakePen(Solid(120, Green.R, Green.G, Green.B), 1.2, dashed: true), HubblePoint(0, 0), LineEnd(u.H0));
        dc.DrawLine(MakePen(Solid(230, Gold.R, Gold.G, Gold.B), 1.8), HubblePoint(0, 0), LineEnd(rate));

        // The galaxies round home: every one sits on the line.
        var home = GalaxyField.At(Home);
        var reach = maxMpc / a;
        var dot = Solid(200, 255, 255, 255);
        foreach (var galaxy in GalaxyField.Within(new Rect(home.Position.X - reach, home.Position.Y - reach, 2 * reach, 2 * reach)))
        {
            if (galaxy.Cell == Home) continue;
            var d = ((Vector)(galaxy.Position - home.Position)).Length * a;
            if (d > maxMpc) continue;
            dc.DrawEllipse(dot, null, HubblePoint(d * Expansion.Universe.GlyPerMpc, rate * d), 2, 2);
        }

        // The three tape measures' galaxies in their colours.
        var colours = new[] { Green, Cyan, Pink };
        var offsets = new[] { (1, 0), (-2, 1), (3, -2) };
        for (var i = 0; i < offsets.Length; i++)
        {
            var g = GalaxyField.At(new GalaxyCell(Home.I + offsets[i].Item1, Home.J + offsets[i].Item2));
            var d = ((Vector)(g.Position - home.Position)).Length * a;
            dc.DrawEllipse(Solid(255, colours[i].R, colours[i].G, colours[i].B), MakePen(Solid(255, 0, 0, 0), 1), HubblePoint(d * Expansion.Universe.GlyPerMpc, rate * d), 4, 4);
        }

        dc.Pop();

        foreach (var g in new[] { 0.0, 0.4, 0.8, 1.2 }) DrawLabel(dc, g.ToString("0.#", CultureInfo.InvariantCulture), HubblePoint(g, 0) + new Vector(0, 9), DimColor, center: true);
        foreach (var v in new[] { 0, 10, 20, 30 }) DrawLabel(dc, v.ToString(CultureInfo.InvariantCulture), HubblePoint(0, v * 1000) + new Vector(-6, 0), DimColor, right: true);
        DrawLabel(dc, Loc("Expand_Chart_SpeedAxis"), new Point(plot.X + 6, plot.Y + 6), TextColor);
        DrawLabel(dc, Loc("Expand_Chart_DistanceAxis"), new Point(plot.Right, plot.Bottom - 10), TextColor, right: true);
        var labelKms = MaxKms * 0.62;
        DrawLabel(dc, LocalizationService.Instance.Format("Expand_Chart_Rate", rate.ToString("0", CultureInfo.InvariantCulture)),
            HubblePoint(labelKms / rate * Expansion.Universe.GlyPerMpc, labelKms) + new Vector(-8, -2), Gold, right: true);
        if (HubbleProgress < 0.97)
        {
            DrawLabel(dc, Loc("Expand_Chart_Today"), HubblePoint(MaxGly * 0.92, u.H0 * maxMpc * 0.92) + new Vector(4, 8), Green);
        }
    }

    #endregion

    #region redshift: the size of the universe while the light travels

    private const double JourneyAxisEnd = 14;

    private Point SizePoint(double gyr, double a)
    {
        var plot = Plot;
        return new Point(plot.X + (gyr / JourneyAxisEnd * plot.Width), plot.Bottom - (Math.Clamp(a, 0, 1.08) * plot.Height));
    }

    private void DrawJourney(PaintContext dc)
    {
        var plot = Plot;
        var u = Expansion.Universe.Ours;
        var aE = 1 / (1 + Redshift);
        var tE = u.TimeAt(aE);
        var t = tE + (Math.Clamp(Journey, 0, 1) * (u.Age - tE));
        dc.PushClip(new RectangleGeometry(new Rect(plot.X - 4, plot.Y - 4, plot.Width + 8, plot.Height + 8)));
        for (var g = 2.0; g <= JourneyAxisEnd; g += 2) dc.DrawLine(Grid, SizePoint(g, 0), SizePoint(g, 1));
        for (var s = 0.25; s <= 1 + 1e-9; s += 0.25) dc.DrawLine(Math.Abs(s - 1) < 1e-9 ? Axis : Grid, SizePoint(0, s), SizePoint(JourneyAxisEnd, s));

        // The journey: from when the light left until now, the part already travelled brighter.
        var travelled = new List<Point> { SizePoint(tE, 0) };
        var remaining = new List<Point> { SizePoint(t, 0) };
        var curve = new List<Point>();
        for (var g = 0.0; g <= u.Age + 1e-9; g += 0.05)
        {
            var p = SizePoint(g, u.ScaleAt(g));
            curve.Add(p);
            if (g >= tE && g <= t) travelled.Add(p);
            if (g >= t) remaining.Add(p);
        }

        travelled.Add(SizePoint(t, u.ScaleAt(t)));
        travelled.Add(SizePoint(t, 0));
        remaining.Add(SizePoint(u.Age, 1));
        remaining.Add(SizePoint(u.Age, 0));
        DrawFill(dc, remaining, Solid(26, Cyan.R, Cyan.G, Cyan.B));
        DrawFill(dc, travelled, Solid(70, Cyan.R, Cyan.G, Cyan.B));
        DrawCurve(dc, curve, Gold, 2);

        dc.DrawLine(MakePen(Solid(170, 255, 255, 255), 1.1, dashed: true), SizePoint(tE, 0), SizePoint(tE, aE));
        dc.DrawLine(MakePen(Solid(120, 255, 255, 255), 1, dashed: true), SizePoint(0, aE), SizePoint(tE, aE));
        dc.DrawEllipse(Solid(255, 255, 255, 255), MakePen(Solid(255, Cyan.R, Cyan.G, Cyan.B), 2.5), SizePoint(t, u.ScaleAt(t)), 5, 5);
        dc.DrawEllipse(Solid(255, Green.R, Green.G, Green.B), null, SizePoint(u.Age, 1), 3.5, 3.5);
        dc.Pop();

        foreach (var g in new[] { 0, 4, 8, 12 }) DrawLabel(dc, g.ToString(CultureInfo.InvariantCulture), SizePoint(g, 0) + new Vector(0, 9), DimColor, center: true);
        foreach (var s in new[] { 0.0, 0.5, 1.0 }) DrawLabel(dc, s.ToString("0.#", CultureInfo.InvariantCulture), SizePoint(0, s) + new Vector(-6, 0), DimColor, right: true);
        DrawLabel(dc, Loc("Expand_Chart_SizeAxis"), new Point(plot.X + 6, plot.Y + 6), TextColor);
        DrawLabel(dc, Loc("Expand_Chart_AgeAxis"), new Point(plot.Right, plot.Bottom - 10), TextColor, right: true);
        var leftLabel = SizePoint(tE, aE) + new Vector(5, -10);
        DrawLabel(dc, Loc("Expand_Chart_LightLeft"), leftLabel, TextColor, right: tE > JourneyAxisEnd * 0.6);
        DrawLabel(dc, Loc("Expand_Chart_Now"), SizePoint(u.Age, 1) + new Vector(-4, 12), Green, right: true);
    }

    #endregion

    #region history: three universes lined up on today

    private const double PastSpan = 15;
    private const double FutureSpan = 17;
    private const double MaxScale = 2.6;

    private Point UniversePoint(double fromNow, double a)
    {
        var plot = Plot;
        return new Point(plot.X + ((fromNow + PastSpan) / (PastSpan + FutureSpan) * plot.Width), plot.Bottom - (Math.Clamp(a, 0, MaxScale * 1.05) / MaxScale * plot.Height));
    }

    private static Color UniverseColour(UniverseKind kind) => kind switch
    {
        UniverseKind.MatterOnly => Cyan,
        UniverseKind.Closed => Red,
        _ => Gold,
    };

    private List<Point> UniverseCurve(Universe u)
    {
        var points = new List<Point>();
        for (var x = -PastSpan; x <= FutureSpan + 1e-9; x += 0.1)
        {
            var t = u.Age + x;
            if (t < 0) continue;
            if (u.CrunchTime is { } crunch && t > crunch)
            {
                points.Add(UniversePoint(crunch - u.Age, 0));
                break;
            }

            points.Add(UniversePoint(x, u.ScaleAt(t)));
        }

        return points;
    }

    private void DrawUniverses(PaintContext dc)
    {
        var plot = Plot;
        dc.PushClip(new RectangleGeometry(new Rect(plot.X - 4, plot.Y - 4, plot.Width + 8, plot.Height + 8)));
        for (var x = -15; x <= 15; x += 5) dc.DrawLine(x == 0 ? Axis : Grid, UniversePoint(x, 0), UniversePoint(x, MaxScale));
        for (var s = 0.5; s <= MaxScale; s += 0.5) dc.DrawLine(Math.Abs(s - 1) < 1e-9 ? Axis : Grid, UniversePoint(-PastSpan, s), UniversePoint(FutureSpan, s));

        foreach (var kind in new[] { UniverseKind.Closed, UniverseKind.MatterOnly, UniverseKind.Ours })
        {
            if (kind == Universe) continue;
            DrawCurve(dc, UniverseCurve(Expansion.Universe.Of(kind)), UniverseColour(kind), 1.2, alpha: 150);
        }

        var selected = Expansion.Universe.Of(Universe);
        DrawCurve(dc, UniverseCurve(selected), UniverseColour(Universe), 2.6);
        var time = Math.Min(CosmicTime, selected.CrunchTime ?? double.MaxValue);
        var fromNow = time - selected.Age;
        if (fromNow >= -PastSpan && fromNow <= FutureSpan)
        {
            dc.DrawEllipse(Solid(255, 255, 255, 255), MakePen(Solid(255, Gold.R, Gold.G, Gold.B), 2.5), UniversePoint(fromNow, selected.ScaleAt(time)), 5, 5);
        }

        dc.Pop();

        foreach (var x in new[] { -10, -5, 0, 5, 10, 15 })
        {
            DrawLabel(dc, x == 0 ? Loc("Expand_Chart_Now") : x.ToString("+0;-0", CultureInfo.InvariantCulture), UniversePoint(x, 0) + new Vector(0, 9), x == 0 ? Green : DimColor, center: true);
        }

        foreach (var s in new[] { 0.0, 1, 2 }) DrawLabel(dc, s.ToString("0", CultureInfo.InvariantCulture), UniversePoint(-PastSpan, s) + new Vector(-6, 0), DimColor, right: true);
        DrawLabel(dc, Loc("Expand_Chart_SizeAxis"), new Point(plot.X + 6, plot.Y + 6), TextColor);
        DrawLabel(dc, Loc("Expand_Chart_FromNowAxis"), new Point(plot.Right, plot.Bottom - 10), TextColor, right: true);
        DrawLabel(dc, Loc("Expand_Universe_Ours"), UniversePoint(15.5, Expansion.Universe.Ours.ScaleAt(Expansion.Universe.Ours.Age + 15.5)) + new Vector(-2, -10), Gold, right: true);
        DrawLabel(dc, Loc("Expand_Universe_MatterOnly"), UniversePoint(13, Expansion.Universe.MatterOnly.ScaleAt(Expansion.Universe.MatterOnly.Age + 13)) + new Vector(6, 9), Cyan, right: true);
        var closed = Expansion.Universe.Closed;
        DrawLabel(dc, Loc("Expand_Universe_Closed"), UniversePoint(9, closed.ScaleAt(closed.Age + 9)) + new Vector(0, 12), Red, center: true);
    }

    /// <summary>The universe whose curve passes nearest a point, if one is within a few pixels.</summary>
    private UniverseKind? UniverseNear(Point point)
    {
        UniverseKind? best = null;
        var bestDistance = 10.0;
        foreach (var kind in new[] { UniverseKind.Ours, UniverseKind.MatterOnly, UniverseKind.Closed })
        {
            foreach (var p in UniverseCurve(Expansion.Universe.Of(kind)))
            {
                var d = ((Vector)(p - point)).Length;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = kind;
                }
            }
        }

        return best;
    }

    #endregion

    #region dragging and clicking

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (Mode == ExpansionMode.Hubble) return;
        e.Pointer.Capture(this);
        e.Handled = true;
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        var point = e.GetPosition(this);
        if (Mode == ExpansionMode.History && UniverseNear(point) is { } kind && kind != Universe)
        {
            SetCurrentValue(UniverseProperty, kind);
            return;
        }

        _dragging = true;
        DragTo(point);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_dragging)
        {
            DragTo(point);
            return;
        }

        Cursor = Mode switch
        {
            ExpansionMode.Redshift when Plot.Contains(point) => new Cursor(StandardCursorType.SizeWestEast),
            ExpansionMode.History when UniverseNear(point) is { } kind && kind != Universe => new Cursor(StandardCursorType.Hand),
            ExpansionMode.History when Plot.Contains(point) => new Cursor(StandardCursorType.SizeWestEast),
            _ => null,
        };
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    private void DragTo(Point point)
    {
        var plot = Plot;
        var fraction = Math.Clamp((point.X - plot.X) / plot.Width, 0, 1);
        if (Mode == ExpansionMode.Redshift)
        {
            // When the light left: from 300 million years after the start (z ≈ 14) to 60 million years ago.
            var u = Expansion.Universe.Ours;
            var t = Math.Clamp(fraction * JourneyAxisEnd, 0.3, u.Age - 0.06);
            var z = (1 / u.ScaleAt(t)) - 1;
            SetCurrentValue(RedshiftProperty, Math.Round(z, z < 1 ? 3 : 2));
            return;
        }

        var selected = Expansion.Universe.Of(Universe);
        var time = selected.Age + (fraction * (PastSpan + FutureSpan)) - PastSpan;
        SetCurrentValue(CosmicTimeProperty, Math.Clamp(time, 1e-6, Math.Min(CosmicHistory.End, selected.CrunchTime ?? double.MaxValue)));
    }

    #endregion

    #region drawing helpers

    private static void DrawCurve(PaintContext dc, IReadOnlyList<Point> points, Color color, double thickness, bool dashed = false, byte alpha = 255)
    {
        if (points.Count < 2) return;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], false);
            for (var i = 1; i < points.Count; i++) context.LineTo(points[i]);

            context.EndFigure(false);
        }
        var pen = new ImmutablePen(((IBrush)Solid(alpha, color.R, color.G, color.B)).ToImmutable(), thickness, dashed ? new ImmutableDashStyle([4, 3], 0) : null, PenLineCap.Flat, PenLineJoin.Round);
        dc.DrawGeometry(null, pen, geometry);
    }

    private static void DrawFill(PaintContext dc, IReadOnlyList<Point> points, IBrush brush)
    {
        if (points.Count < 3) return;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], true);
            for (var i = 1; i < points.Count; i++) context.LineTo(points[i]);

            context.EndFigure(true);
        }
        dc.DrawGeometry(brush, null, geometry);
    }

    private void DrawLabel(PaintContext dc, string text, Point at, Color color, bool center = false, bool right = false)
    {
        var formatted = _labels.Text(text, 10.5, color);
        var x = center ? at.X - (formatted.Width / 2) : right ? at.X - formatted.Width : at.X;
        dc.DrawText(formatted, new Point(x, at.Y - (formatted.Height / 2)));
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    #endregion
}
