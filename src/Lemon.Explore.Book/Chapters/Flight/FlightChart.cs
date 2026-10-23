using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Flight;

/// <summary>
/// The side panel's chart, one per view. Wing: the lift coefficient at each angle of attack, real and ideal, with the
/// stall; the angle is a dot that drags. Forces: the drag in level flight at each speed (from friction, from making lift,
/// and both) against the propeller's thrust; dragging picks a speed and trims the plane for level flight there. Turn:
/// how many g a level turn pulls at each bank and the stall speed that brings; the bank drags.
/// </summary>
public sealed class FlightChart : Control
{
    static FlightChart()
    {
        AffectsRender<FlightChart>(ModeProperty, ShapeProperty, InvertedProperty, AlphaProperty, MassProperty, AltitudeProperty, SpeedProperty, ThrottleProperty, OnGroundProperty, BankProperty, TurnSpeedProperty);
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

    public static readonly StyledProperty<FlightMode> ModeProperty =
        AvaloniaProperty.Register<FlightChart, FlightMode>(nameof(Mode), FlightMode.Wing);

    public static readonly StyledProperty<WingShape> ShapeProperty =
        AvaloniaProperty.Register<FlightChart, WingShape>(nameof(Shape), WingShape.Cambered);

    public static readonly StyledProperty<bool> InvertedProperty =
        AvaloniaProperty.Register<FlightChart, bool>(nameof(Inverted), false);

    public static readonly StyledProperty<double> AlphaProperty =
        AvaloniaProperty.Register<FlightChart, double>(nameof(Alpha), 4.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MassProperty =
        AvaloniaProperty.Register<FlightChart, double>(nameof(Mass), 1000.0);

    public static readonly StyledProperty<double> AltitudeProperty =
        AvaloniaProperty.Register<FlightChart, double>(nameof(Altitude), 1000.0);

    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<FlightChart, double>(nameof(Speed), 55.0);

    public static readonly StyledProperty<double> ThrottleProperty =
        AvaloniaProperty.Register<FlightChart, double>(nameof(Throttle), 0.7);

    public static readonly StyledProperty<bool> OnGroundProperty =
        AvaloniaProperty.Register<FlightChart, bool>(nameof(OnGround), false);

    /// <summary>Run with a speed (m/s) while the viewer drags across the drag chart.</summary>
    public static readonly StyledProperty<ICommand?> TrimCommandProperty =
        AvaloniaProperty.Register<FlightChart, ICommand?>(nameof(TrimCommand));

    public static readonly StyledProperty<double> BankProperty =
        AvaloniaProperty.Register<FlightChart, double>(nameof(Bank), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> TurnSpeedProperty =
        AvaloniaProperty.Register<FlightChart, double>(nameof(TurnSpeed), 180.0);

    /// <summary>Run when the viewer starts dragging (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<FlightChart, ICommand?>(nameof(InteractionCommand));



    public FlightMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public WingShape Shape
    {
        get => GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    public bool Inverted
    {
        get => GetValue(InvertedProperty);
        set => SetValue(InvertedProperty, value);
    }

    public double Alpha
    {
        get => GetValue(AlphaProperty);
        set => SetValue(AlphaProperty, value);
    }

    public double Mass
    {
        get => GetValue(MassProperty);
        set => SetValue(MassProperty, value);
    }

    public double Altitude
    {
        get => GetValue(AltitudeProperty);
        set => SetValue(AltitudeProperty, value);
    }

    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public double Throttle
    {
        get => GetValue(ThrottleProperty);
        set => SetValue(ThrottleProperty, value);
    }

    public bool OnGround
    {
        get => GetValue(OnGroundProperty);
        set => SetValue(OnGroundProperty, value);
    }

    public ICommand? TrimCommand
    {
        get => GetValue(TrimCommandProperty);
        set => SetValue(TrimCommandProperty, value);
    }

    public double Bank
    {
        get => GetValue(BankProperty);
        set => SetValue(BankProperty, value);
    }

    public double TurnSpeed
    {
        get => GetValue(TurnSpeedProperty);
        set => SetValue(TurnSpeedProperty, value);
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
    private static readonly Color Violet = Color.FromRgb(0xB0, 0x8C, 0xFF);
    private static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
    private static readonly IPen Axis = MakePen(Solid(70, 255, 255, 255), 1);

    private readonly LabelCache _labels;
    private bool _dragging;

    public FlightChart()
    {
        _labels = new LabelCache(this);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    /// <summary>The plotting area inside the axes' labels.</summary>
    private Rect Plot => new(40, 12, Math.Max(10, ActualWidth - 52 - (Mode == FlightMode.Turn ? 30 : 0)), Math.Max(10, ActualHeight - 40));

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 1 || ActualHeight < 1) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        switch (Mode)
        {
            case FlightMode.Forces:
                DrawDrag(dc);
                break;

            case FlightMode.Turn:
                DrawTurn(dc);
                break;

            default:
                DrawLift(dc);
                break;
        }
    }

    #region wing: lift against angle of attack

    private const double MinAlpha = -10;
    private const double MaxAlpha = 25;
    private const double MinCl = -1.2;
    private const double MaxCl = 2.4;

    private Point LiftPoint(double alpha, double cl)
    {
        var plot = Plot;
        return new Point(plot.X + ((alpha - MinAlpha) / (MaxAlpha - MinAlpha) * plot.Width),
            plot.Bottom - ((Math.Clamp(cl, MinCl - 0.2, MaxCl + 0.2) - MinCl) / (MaxCl - MinCl) * plot.Height));
    }

    private void DrawLift(PaintContext dc)
    {
        var plot = Plot;
        dc.PushClip(new RectangleGeometry(new Rect(plot.X - 2, plot.Y - 4, plot.Width + 6, plot.Height + 6)));
        for (var a = -10; a <= 25; a += 5) dc.DrawLine(a == 0 ? Axis : Grid, LiftPoint(a, MinCl), LiftPoint(a, MaxCl));
        for (var c = -1.0; c <= 2.4; c += 0.5) dc.DrawLine(Math.Abs(c) < 1e-9 ? Axis : Grid, LiftPoint(MinAlpha, c), LiftPoint(MaxAlpha, c));

        var ideal = new List<Point>();
        var real = new List<Point>();
        for (var a = MinAlpha; a <= MaxAlpha + 1e-9; a += 0.25)
        {
            ideal.Add(LiftPoint(a, WingSection.IdealLift(Shape, Inverted, a)));
            real.Add(LiftPoint(a, WingSection.Lift(Shape, Inverted, a)));
        }

        DrawCurve(dc, ideal, Cyan, 1.2, dashed: true);
        DrawCurve(dc, real, Gold, 2);

        var stall = WingSection.StallAlpha(Shape, Inverted);
        dc.DrawLine(MakePen(Solid(150, Red.R, Red.G, Red.B), 1.2, dashed: true), LiftPoint(stall, MinCl), LiftPoint(stall, MaxCl));
        dc.Pop();

        var dot = LiftPoint(Alpha, WingSection.Lift(Shape, Inverted, Alpha));
        dc.DrawEllipse(Solid(255, 255, 255, 255), MakePen(Solid(255, Gold.R, Gold.G, Gold.B), 2.5), dot, 5, 5);

        foreach (var a in new[] { -10, 0, 10, 20 }) DrawLabel(dc, a.ToString(CultureInfo.InvariantCulture) + "°", LiftPoint(a, MinCl) + new Vector(0, 9), DimColor, center: true);
        foreach (var c in new[] { -1.0, 0, 1, 2 }) DrawLabel(dc, c.ToString("0", CultureInfo.InvariantCulture), LiftPoint(MinAlpha, c) + new Vector(-6, 0), DimColor, right: true);
        DrawLabel(dc, Loc("Flight_Chart_LiftAxis"), new Point(plot.X + 6, plot.Y + 6), TextColor);
        DrawLabel(dc, Loc("Flight_Chart_AlphaAxis"), new Point(plot.Right, plot.Bottom - 10), TextColor, right: true);
        DrawLabel(dc, Loc("Flight_Chart_Stall"), LiftPoint(stall, MinCl + 0.8) + new Vector(4, 0), Red);
        DrawLabel(dc, Loc("Flight_Chart_Ideal"), LiftPoint(stall + 1.2, MaxCl - 0.3) + new Vector(4, 0), Cyan);
        DrawLabel(dc, Loc("Flight_Chart_Real"), LiftPoint(MaxAlpha - 1, WingSection.Lift(Shape, Inverted, MaxAlpha - 1)) + new Vector(0, 10), Gold, right: true);
    }

    #endregion

    #region forces: drag and thrust against speed

    private const double MinKmh = 60;
    private const double MaxKmh = 300;
    private const double MaxNewtons = 4000;

    private Point DragPoint(double kmh, double newtons)
    {
        var plot = Plot;
        return new Point(plot.X + ((kmh - MinKmh) / (MaxKmh - MinKmh) * plot.Width), plot.Bottom - (Math.Clamp(newtons, -100, MaxNewtons + 300) / MaxNewtons * plot.Height));
    }

    private void DrawDrag(PaintContext dc)
    {
        var plot = Plot;
        dc.PushClip(new RectangleGeometry(new Rect(plot.X - 2, plot.Y - 4, plot.Width + 6, plot.Height + 6)));
        for (var k = 100; k <= MaxKmh; k += 50) dc.DrawLine(Grid, DragPoint(k, 0), DragPoint(k, MaxNewtons));
        for (var n = 0; n <= MaxNewtons; n += 1000) dc.DrawLine(n == 0 ? Axis : Grid, DragPoint(MinKmh, n), DragPoint(MaxKmh, n));

        var parasite = new List<Point>();
        var induced = new List<Point>();
        var total = new List<Point>();
        var full = new List<Point>();
        var now = new List<Point>();
        for (var k = MinKmh; k <= MaxKmh + 1e-9; k += 2)
        {
            var v = k / 3.6;
            var (p, i) = Trainer.LevelDrag(v, Mass, Altitude);
            parasite.Add(DragPoint(k, p));
            induced.Add(DragPoint(k, i));
            total.Add(DragPoint(k, p + i));
            full.Add(DragPoint(k, Trainer.Thrust(1, v, Altitude)));
            now.Add(DragPoint(k, Trainer.Thrust(Throttle, v, Altitude)));
        }

        DrawCurve(dc, full, Green, 1, dashed: true, alpha: 110);
        DrawCurve(dc, now, Green, 1.8);
        DrawCurve(dc, parasite, DimColor, 1.1, dashed: true);
        DrawCurve(dc, induced, Violet, 1.1, dashed: true);
        DrawCurve(dc, total, Gold, 2);

        var stall = Trainer.StallSpeed(Mass, Altitude) * 3.6;
        dc.DrawRectangle(Solid(40, Red.R, Red.G, Red.B), null, new Rect(DragPoint(MinKmh, MaxNewtons), DragPoint(stall, 0)));
        dc.DrawLine(MakePen(Solid(170, Red.R, Red.G, Red.B), 1.2, dashed: true), DragPoint(stall, 0), DragPoint(stall, MaxNewtons));
        var best = Trainer.MinDragSpeed(Mass, Altitude) * 3.6;
        var (bp, bi) = Trainer.LevelDrag(best / 3.6, Mass, Altitude);
        dc.DrawEllipse(null, MakePen(Solid(220, Gold.R, Gold.G, Gold.B), 1.4), DragPoint(best, bp + bi), 4, 4);
        dc.Pop();

        var kmhNow = Speed * 3.6;
        if (!OnGround && kmhNow >= MinKmh && kmhNow <= MaxKmh)
        {
            var (p, i) = Trainer.LevelDrag(Speed, Mass, Altitude);
            var x = DragPoint(kmhNow, 0).X;
            dc.DrawLine(MakePen(Solid(150, 255, 255, 255), 1.2), new Point(x, plot.Y), new Point(x, plot.Bottom));
            dc.DrawEllipse(Solid(255, 255, 255, 255), MakePen(Solid(255, Gold.R, Gold.G, Gold.B), 2.5), DragPoint(kmhNow, p + i), 5, 5);
        }

        foreach (var k in new[] { 100, 150, 200, 250, 300 }) DrawLabel(dc, k.ToString(CultureInfo.InvariantCulture), DragPoint(k, 0) + new Vector(0, 9), DimColor, center: true);
        foreach (var n in new[] { 0, 1000, 2000, 3000, 4000 }) DrawLabel(dc, (n / 1000).ToString(CultureInfo.InvariantCulture), DragPoint(MinKmh, n) + new Vector(-6, 0), DimColor, right: true);
        DrawLabel(dc, Loc("Flight_Chart_DragAxis"), new Point(plot.X + 6, plot.Y + 6), TextColor);
        DrawLabel(dc, Loc("Flight_Chart_SpeedAxis"), new Point(plot.Right, plot.Bottom - 10), TextColor, right: true);
        DrawLabel(dc, Loc("Flight_Chart_StallZone"), DragPoint(MinKmh, MaxNewtons * 0.5) + new Vector(4, 0), Red);
        DrawLabel(dc, Loc("Flight_Chart_Thrust"), DragPoint(MaxKmh - 4, Trainer.Thrust(Throttle, (MaxKmh - 4) / 3.6, Altitude)) + new Vector(0, -9), Green, right: true);
        DrawLabel(dc, Loc("Flight_Chart_Induced"), DragPoint(stall + 22, Trainer.LevelDrag((stall + 22) / 3.6, Mass, Altitude).Induced) + new Vector(6, 0), Violet);
        DrawLabel(dc, Loc("Flight_Chart_Parasite"), DragPoint(MaxKmh - 10, Trainer.LevelDrag((MaxKmh - 10) / 3.6, Mass, Altitude).Parasite) + new Vector(0, 10), DimColor, right: true);
        DrawLabel(dc, Loc("Flight_Chart_Total"), DragPoint(best, bp + bi) + new Vector(0, -12), Gold, center: true);
    }

    #endregion

    #region turn: load factor and stall speed against bank

    private const double MaxBank = 80;
    private const double MaxG = 5;

    /// <summary>The right-hand axis: one g of the left axis is this many km/h.</summary>
    private const double KmhPerG = 60;

    private Point TurnPoint(double bank, double g)
    {
        var plot = Plot;
        return new Point(plot.X + (bank / MaxBank * plot.Width), plot.Bottom - (Math.Clamp(g, 0, MaxG + 0.3) / MaxG * plot.Height));
    }

    private void DrawTurn(PaintContext dc)
    {
        var plot = Plot;
        dc.PushClip(new RectangleGeometry(new Rect(plot.X - 2, plot.Y - 4, plot.Width + 6, plot.Height + 6)));
        for (var b = 10; b <= MaxBank; b += 10) dc.DrawLine(Grid, TurnPoint(b, 0), TurnPoint(b, MaxG));
        for (var g = 1; g <= MaxG; g++) dc.DrawLine(Grid, TurnPoint(0, g), TurnPoint(MaxBank, g));

        var load = new List<Point>();
        var stall = new List<Point>();
        var vs = Trainer.StallSpeed(1000, 0) * 3.6;
        for (var b = 0.0; b <= MaxBank + 1e-9; b += 0.5)
        {
            var n = Trainer.LoadFactor(b);
            load.Add(TurnPoint(b, n));
            stall.Add(TurnPoint(b, vs * Math.Sqrt(n) / KmhPerG));
        }

        DrawCurve(dc, stall, Red, 1.6, dashed: true);
        DrawCurve(dc, load, Gold, 2);

        // The speed flown, on the right-hand axis: where it meets the stall line is the steepest bank it allows.
        var speedG = TurnSpeed / KmhPerG;
        dc.DrawLine(MakePen(Solid(170, Cyan.R, Cyan.G, Cyan.B), 1.2), TurnPoint(0, speedG), TurnPoint(MaxBank, speedG));
        var limit = Math.Acos(Math.Clamp(Math.Pow(vs / TurnSpeed, 2), 0, 1)) * 180 / Math.PI;
        if (limit < MaxBank)
        {
            dc.DrawLine(MakePen(Solid(150, Red.R, Red.G, Red.B), 1, dashed: true), TurnPoint(limit, 0), TurnPoint(limit, speedG));
        }

        dc.Pop();

        var bank = Math.Min(Math.Abs(Bank), MaxBank);
        var dot = TurnPoint(bank, Trainer.LoadFactor(bank));
        dc.DrawEllipse(Solid(255, 255, 255, 255), MakePen(Solid(255, Gold.R, Gold.G, Gold.B), 2.5), dot, 5, 5);

        foreach (var b in new[] { 0, 20, 40, 60, 80 }) DrawLabel(dc, b.ToString(CultureInfo.InvariantCulture) + "°", TurnPoint(b, 0) + new Vector(0, 9), DimColor, center: true);
        for (var g = 1; g <= MaxG; g++)
        {
            DrawLabel(dc, g.ToString(CultureInfo.InvariantCulture) + "g", TurnPoint(0, g) + new Vector(-6, 0), DimColor, right: true);
            DrawLabel(dc, (g * KmhPerG).ToString(CultureInfo.InvariantCulture), TurnPoint(MaxBank, g) + new Vector(6, 0), Red);
        }

        DrawLabel(dc, Loc("Flight_Chart_LoadAxis"), new Point(plot.X + 6, plot.Y + 6), Gold);
        DrawLabel(dc, Loc("Flight_Chart_StallSpeed"), new Point(plot.X + 6, plot.Y + 20), Red);
        DrawLabel(dc, Loc("Flight_Chart_BankAxis"), new Point(plot.Right, plot.Bottom - 10), TextColor, right: true);
        DrawLabel(dc, Loc("Flight_Chart_YourSpeed"), TurnPoint(2, speedG) + new Vector(0, -9), Cyan);
    }

    #endregion

    #region dragging

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(this);
        _dragging = true;
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        DragTo(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
        if (_dragging) DragTo(e.GetPosition(this));
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
        var t = Math.Clamp((point.X - plot.X) / plot.Width, 0, 1);
        switch (Mode)
        {
            case FlightMode.Forces:
            {
                var kmh = Math.Round(MinKmh + (t * (MaxKmh - MinKmh)));
                if (TrimCommand is { } trim && trim.CanExecute(kmh / 3.6)) trim.Execute(kmh / 3.6);
                break;
            }

            case FlightMode.Turn:
            {
                var bank = Math.Round(Math.Min(t * MaxBank, 75));
                SetCurrentValue(BankProperty, Bank < 0 ? -bank : bank);
                break;
            }

            default:
                SetCurrentValue(AlphaProperty, Math.Round((MinAlpha + (t * (MaxAlpha - MinAlpha))) * 2) / 2);
                break;
        }
    }

    #endregion

    private static void DrawCurve(PaintContext dc, List<Point> points, Color colour, double thickness, bool dashed = false, byte alpha = 255)
    {
        if (points.Count < 2) return;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], false);
            for (var i = 1; i < points.Count; i++) context.LineTo(points[i]);

            context.EndFigure(false);
        }
        dc.DrawGeometry(null, MakePen(Solid(alpha, colour.R, colour.G, colour.B), thickness, dashed), geometry);
    }

    private void DrawLabel(PaintContext dc, string text, Point at, Color color, bool center = false, bool right = false)
    {
        var formatted = _labels.Text(text, 10.5, color);
        var x = center ? at.X - (formatted.Width / 2) : right ? at.X - formatted.Width : at.X;
        dc.DrawText(formatted, new Point(x, at.Y - (formatted.Height / 2)));
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);
}
