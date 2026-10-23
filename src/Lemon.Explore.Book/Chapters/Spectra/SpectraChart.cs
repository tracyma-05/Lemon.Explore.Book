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

namespace Lemon.Explore.Book.Chapters.Spectra;

/// <summary>
/// The side panel's chart, one per view. Atom: hydrogen's lines in their series, ultraviolet to infrared; a click picks
/// a line for the lamp to fire. Lamps: every gas's lines one under another, a fingerprint card; a click on a row compares
/// it. Stars: how strong each family of dark lines is at each temperature; the temperature drags.
/// </summary>
public sealed class SpectraChart : Control
{
    static SpectraChart()
    {
        AffectsRender<SpectraChart>(ModeProperty, PhotonNmProperty, KelvinProperty, GasProperty, MysteryProperty, SourceProperty, CompareGasProperty);
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

    public static readonly StyledProperty<SpectraMode> ModeProperty =
        AvaloniaProperty.Register<SpectraChart, SpectraMode>(nameof(Mode), SpectraMode.Atom);

    public static readonly StyledProperty<double> PhotonNmProperty =
        AvaloniaProperty.Register<SpectraChart, double>(nameof(PhotonNm), 656.28, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> KelvinProperty =
        AvaloniaProperty.Register<SpectraChart, double>(nameof(Kelvin), SpectraModel.SunKelvin, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<Gas> GasProperty =
        AvaloniaProperty.Register<SpectraChart, Gas>(nameof(Gas), Gas.Hydrogen);

    public static readonly StyledProperty<bool> MysteryProperty =
        AvaloniaProperty.Register<SpectraChart, bool>(nameof(Mystery), false);

    public static readonly StyledProperty<LightSource> SourceProperty =
        AvaloniaProperty.Register<SpectraChart, LightSource>(nameof(Source), LightSource.Lamp);

    public static readonly StyledProperty<Gas?> CompareGasProperty =
        AvaloniaProperty.Register<SpectraChart, Gas?>(nameof(CompareGas), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags in the chart (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<SpectraChart, ICommand?>(nameof(InteractionCommand));



    public SpectraMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double PhotonNm
    {
        get => GetValue(PhotonNmProperty);
        set => SetValue(PhotonNmProperty, value);
    }

    public double Kelvin
    {
        get => GetValue(KelvinProperty);
        set => SetValue(KelvinProperty, value);
    }

    public Gas Gas
    {
        get => GetValue(GasProperty);
        set => SetValue(GasProperty, value);
    }

    public bool Mystery
    {
        get => GetValue(MysteryProperty);
        set => SetValue(MysteryProperty, value);
    }

    public LightSource Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Gas? CompareGas
    {
        get => GetValue(CompareGasProperty);
        set => SetValue(CompareGasProperty, value);
    }

    public ICommand? InteractionCommand
    {
        get => GetValue(InteractionCommandProperty);
        set => SetValue(InteractionCommandProperty, value);
    }

    private static readonly Color TextColor = Color.FromRgb(0xEE, 0xF1, 0xF6);
    private static readonly Color DimColor = Color.FromRgb(0x9A, 0xA5, 0xB5);
    private static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    private static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);

    private const double SeriesFrom = 90;
    private const double SeriesTo = 4100;
    private const double ColdestStar = 2500;
    private const double HottestStar = 40_000;

    private readonly LabelCache _labels;
    private bool _dragging;

    public SpectraChart()
    {
        _labels = new LabelCache(this);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    /// <summary>The plotting area inside the labels.</summary>
    private Rect Plot => new(64, 12, Math.Max(10, ActualWidth - 74), Math.Max(10, ActualHeight - 38));

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 1 || ActualHeight < 1) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        switch (Mode)
        {
            case SpectraMode.Lamps:
                DrawFingerprints(dc);
                break;

            case SpectraMode.Stars:
                DrawFamilies(dc);
                break;

            default:
                DrawSeries(dc);
                break;
        }
    }

    #region atom: hydrogen's series

    private double SeriesX(double nm) => Plot.X + (Plot.Width * (Math.Log(nm) - Math.Log(SeriesFrom)) / (Math.Log(SeriesTo) - Math.Log(SeriesFrom)));

    private double SeriesNm(double x) => Math.Exp(Math.Log(SeriesFrom) + ((x - Plot.X) / Plot.Width * (Math.Log(SeriesTo) - Math.Log(SeriesFrom))));

    private void DrawSeries(PaintContext dc)
    {
        var plot = Plot;
        var from = SeriesX(SpectraModel.VisibleFrom);
        var to = SeriesX(SpectraModel.VisibleTo);
        dc.DrawRectangle(Solid(28, 255, 255, 255), null, new Rect(from, plot.Y, to - from, plot.Height));
        var rowHeight = plot.Height / 4;
        string[] keys = ["Spec_Series_Lyman", "Spec_Series_Balmer", "Spec_Series_Paschen", "Spec_Series_Brackett"];
        for (var lower = 1; lower <= 4; lower++)
        {
            var y = plot.Y + ((lower - 0.5) * rowHeight);
            DrawLabel(dc, Loc(keys[lower - 1]), new Point(plot.X - 6, y), TextColor, right: true);
            dc.DrawLine(Grid, new Point(plot.X, y), new Point(plot.Right, y));
            for (var upper = lower + 1; upper <= lower + 8; upper++)
            {
                var nm = SpectraModel.Wavelength(lower, upper);
                if (nm > SeriesTo) continue;
                var x = SeriesX(nm);
                var colour = SpectraDiagram.PhotonColour(nm);
                var picked = Math.Abs(nm - PhotonNm) < 0.05;
                dc.DrawLine(MakePen(Solid((byte)(picked ? 255 : 200), colour.R, colour.G, colour.B), picked ? 3 : 1.6), new Point(x, y - (rowHeight * 0.32)), new Point(x, y + (rowHeight * 0.32)));
            }
        }

        var lamp = SeriesX(Math.Clamp(PhotonNm, SeriesFrom, SeriesTo));
        dc.DrawLine(MakePen(Solid(200, Gold.R, Gold.G, Gold.B), 1, dashed: true), new Point(lamp, plot.Y), new Point(lamp, plot.Bottom));

        foreach (var nm in new[] { 100.0, 200, 500, 1000, 2000, 4000 })
        {
            DrawLabel(dc, nm.ToString("0", CultureInfo.InvariantCulture), new Point(SeriesX(nm), plot.Bottom + 10), DimColor, center: true);
        }

        DrawLabel(dc, Loc("Spec_Label_Nm"), new Point(plot.Right, plot.Bottom + 22), DimColor, right: true);
    }

    #endregion

    #region lamps: every gas's fingerprint

    private double LampX(double nm) => Plot.X + (Plot.Width * (nm - SpectraModel.VisibleFrom) / (SpectraModel.VisibleTo - SpectraModel.VisibleFrom));

    private void DrawFingerprints(PaintContext dc)
    {
        var plot = Plot;
        var gases = Enum.GetValues<Gas>();
        var rowHeight = plot.Height / gases.Length;
        for (var i = 0; i < gases.Length; i++)
        {
            var gas = gases[i];
            var row = new Rect(plot.X, plot.Y + (i * rowHeight) + 3, plot.Width, rowHeight - 6);
            dc.DrawRectangle(Solid(255, 4, 5, 9), null, row);
            foreach (var line in SpectraModel.Lines(gas))
            {
                var colour = SpectraDiagram.PhotonColour(line.Nm);
                dc.DrawRectangle(Solid((byte)(80 + (175 * line.Strength)), colour.R, colour.G, colour.B), null, new Rect(LampX(line.Nm) - 0.8, row.Y, 1.6, row.Height));
            }

            var current = gas == Gas && !Mystery && Source is LightSource.Lamp or LightSource.ThroughGas;
            var compared = gas == CompareGas;
            if (current || compared) dc.DrawRectangle(null, MakePen(Solid(220, compared ? Gold.R : TextColor.R, compared ? Gold.G : TextColor.G, compared ? Gold.B : TextColor.B), 1.2), row.Inflate(new Thickness(1.5, 1.5)));
            DrawLabel(dc, Loc(SpectraDiagram.GasKey(gas)), new Point(plot.X - 6, row.Y + (row.Height / 2)), compared ? Gold : TextColor, right: true);
        }

        foreach (var nm in new[] { 400.0, 500, 600, 700 })
        {
            DrawLabel(dc, nm.ToString("0", CultureInfo.InvariantCulture), new Point(LampX(nm), plot.Bottom + 10), DimColor, center: true);
        }
    }

    #endregion

    #region stars: which lines at which temperature

    private double StarX(double kelvin) => Plot.X + (Plot.Width * (Math.Log(kelvin) - Math.Log(ColdestStar)) / (Math.Log(HottestStar) - Math.Log(ColdestStar)));

    private double StarKelvin(double x) => Math.Exp(Math.Log(ColdestStar) + (Math.Clamp((x - Plot.X) / Plot.Width, 0, 1) * (Math.Log(HottestStar) - Math.Log(ColdestStar))));

    private void DrawFamilies(PaintContext dc)
    {
        var plot = Plot;

        // Hotter to the left, as astronomers draw it: O B A F G K M.
        double X(double kelvin) => plot.Right - (StarX(kelvin) - plot.X);
        foreach (var (letter, kelvin) in new[] { ('O', 33_000.0), ('B', 17_000.0), ('A', 8_700.0), ('F', 6_700.0), ('G', 5_600.0), ('K', 4_400.0), ('M', 3_100.0) })
        {
            DrawLabel(dc, letter.ToString(), new Point(X(kelvin), plot.Bottom + 10), DimColor, center: true);
        }

        foreach (var boundary in new[] { 30_000.0, 10_000, 7_500, 6_000, 5_200, 3_700 }) dc.DrawLine(Grid, new Point(X(boundary), plot.Y), new Point(X(boundary), plot.Bottom));

        var families = new (string Key, Color Colour, Func<double, double> Strength)[]
        {
            ("Spec_Label_Hydrogen", Color.FromRgb(0xFF, 0x7A, 0x7A), k => SpectraModel.LineFamilies(k).Hydrogen),
            ("Spec_Label_Helium", Color.FromRgb(0x8A, 0xB4, 0xFF), k => SpectraModel.LineFamilies(k).HeliumNeutral),
            ("Spec_Label_Calcium", Color.FromRgb(0xB0, 0x8C, 0xFF), k => SpectraModel.LineFamilies(k).Calcium),
            ("Spec_Label_Molecules", Color.FromRgb(0xFF, 0xB0, 0x5A), k => SpectraModel.LineFamilies(k).Molecules),
        };

        var legendY = plot.Y + 6;
        foreach (var (key, colour, strength) in families)
        {
            var points = new List<Point>();
            for (var x = plot.X; x <= plot.Right; x += 2)
            {
                var kelvin = StarKelvin(plot.Right - (x - plot.X));
                points.Add(new Point(x, plot.Bottom - ((plot.Height - 10) * strength(kelvin))));
            }

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(points[0], false);
                foreach (var p in points.Skip(1)) context.LineTo(p);

                context.EndFigure(false);
            }
            dc.DrawGeometry(null, MakePen(Solid(255, colour.R, colour.G, colour.B), 1.8), geometry);
            DrawLabel(dc, Loc(key), new Point(plot.X - 6, legendY), colour, right: true);
            legendY += 15;
        }

        var now = X(Math.Clamp(Kelvin, ColdestStar, HottestStar));
        dc.DrawLine(MakePen(Solid(220, 255, 255, 255), 1.2), new Point(now, plot.Y), new Point(now, plot.Bottom));
        dc.DrawEllipse(Solid(255, 255, 255, 255), MakePen(Solid(255, Gold.R, Gold.G, Gold.B), 2.5), new Point(now, plot.Bottom - 6), 5, 5);
    }

    #endregion

    #region clicking and dragging

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(this);
        _dragging = true;
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        Act(e.GetPosition(this), click: true);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Cursor = Mode == SpectraMode.Stars ? new Cursor(StandardCursorType.SizeWestEast) : new Cursor(StandardCursorType.Hand);
        if (_dragging && Mode == SpectraMode.Stars) Act(e.GetPosition(this), click: false);
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

    private void Act(Point point, bool click)
    {
        var plot = Plot;
        switch (Mode)
        {
            case SpectraMode.Stars:
            {
                var kelvin = StarKelvin(plot.Right - (point.X - plot.X));
                SetCurrentValue(KelvinProperty, Math.Round(Math.Clamp(kelvin, 2_500, 40_000) / 10) * 10);
                break;
            }

            case SpectraMode.Lamps when click:
            {
                var gases = Enum.GetValues<Gas>();
                var index = (int)Math.Floor((point.Y - plot.Y) / (plot.Height / gases.Length));
                if (index < 0 || index >= gases.Length) return;
                SetCurrentValue(CompareGasProperty, CompareGas == gases[index] ? null : gases[index]);
                break;
            }

            case SpectraMode.Atom when click:
            {
                // The nearest line of the series under the mouse becomes the lamp's colour.
                var lower = Math.Clamp((int)Math.Floor((point.Y - plot.Y) / (plot.Height / 4)) + 1, 1, 4);
                var nm = SeriesNm(point.X);
                var best = Enumerable.Range(lower + 1, 8).Select(u => SpectraModel.Wavelength(lower, u)).MinBy(w => Math.Abs(Math.Log(w / nm)));
                SetCurrentValue(PhotonNmProperty, Math.Round(best, 2));
                break;
            }
        }
    }

    #endregion

    private void DrawLabel(PaintContext dc, string text, Point at, Color color, bool center = false, bool right = false)
    {
        var formatted = _labels.Text(text, 10.5, color);
        var x = center ? at.X - (formatted.Width / 2) : right ? at.X - formatted.Width : at.X;
        dc.DrawText(formatted, new Point(x, at.Y - (formatted.Height / 2)));
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);
}
