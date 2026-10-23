using System.ComponentModel;
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

namespace Lemon.Explore.Book.Chapters.AcidsBases;

/// <summary>
/// The side panel's chart, one per view. Particles: [H₃O⁺] and [OH⁻] on one ladder of powers of ten, whose exponents
/// always add up to 14. Scale: the six indicators' colours from pH 0 to 14 with the chosen liquid's pH (click a row to
/// use that indicator). Neutralise: what is in the flask, in millimoles: acid left, salt made, base left over.
/// </summary>
public sealed class AcidBaseChart : Control
{
    static AcidBaseChart()
    {
        AffectsRender<AcidBaseChart>(ModeProperty, BeakerProperty, PhProperty, ScaleIndicatorProperty, AcidProperty, BaseMlProperty, MolarProperty);
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

    public static readonly StyledProperty<AcidBaseMode> ModeProperty =
        AvaloniaProperty.Register<AcidBaseChart, AcidBaseMode>(nameof(Mode), AcidBaseMode.Particles);

    public static readonly StyledProperty<AcidBaseModel.Beaker> BeakerProperty =
        AvaloniaProperty.Register<AcidBaseChart, AcidBaseModel.Beaker>(nameof(Beaker), AcidBaseModel.Beaker.Water());

    public static readonly StyledProperty<double> PhProperty =
        AvaloniaProperty.Register<AcidBaseChart, double>(nameof(Ph), 7.0);

    public static readonly StyledProperty<Indicator> ScaleIndicatorProperty =
        AvaloniaProperty.Register<AcidBaseChart, Indicator>(nameof(ScaleIndicator), Indicator.Universal, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<TitrationAcid> AcidProperty =
        AvaloniaProperty.Register<AcidBaseChart, TitrationAcid>(nameof(Acid), TitrationAcid.Strong);

    public static readonly StyledProperty<double> BaseMlProperty =
        AvaloniaProperty.Register<AcidBaseChart, double>(nameof(BaseMl), 0.0);

    public static readonly StyledProperty<double> MolarProperty =
        AvaloniaProperty.Register<AcidBaseChart, double>(nameof(Molar), AcidBaseModel.BottleMolar);

    /// <summary>Run when the viewer clicks (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<AcidBaseChart, ICommand?>(nameof(InteractionCommand));


    public AcidBaseMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public AcidBaseModel.Beaker Beaker
    {
        get => GetValue(BeakerProperty);
        set => SetValue(BeakerProperty, value);
    }

    /// <summary>Scale: the chosen liquid's pH.</summary>
    public double Ph
    {
        get => GetValue(PhProperty);
        set => SetValue(PhProperty, value);
    }

    public Indicator ScaleIndicator
    {
        get => GetValue(ScaleIndicatorProperty);
        set => SetValue(ScaleIndicatorProperty, value);
    }

    public TitrationAcid Acid
    {
        get => GetValue(AcidProperty);
        set => SetValue(AcidProperty, value);
    }

    public double BaseMl
    {
        get => GetValue(BaseMlProperty);
        set => SetValue(BaseMlProperty, value);
    }

    public double Molar
    {
        get => GetValue(MolarProperty);
        set => SetValue(MolarProperty, value);
    }

    public ICommand? InteractionCommand
    {
        get => GetValue(InteractionCommandProperty);
        set => SetValue(InteractionCommandProperty, value);
    }

    private static readonly Color DimColor = Color.FromRgb(0x9A, 0xA5, 0xB5);
    private static readonly Color TextColor = Color.FromRgb(0xEE, 0xF1, 0xF6);
    private static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    private static readonly Color Red = Color.FromRgb(0xFF, 0x5A, 0x5F);
    private static readonly Color Blue = Color.FromRgb(0x5A, 0xA8, 0xFF);
    private static readonly Color Orange = Color.FromRgb(0xFF, 0x9A, 0x4A);
    private static readonly Color Violet = Color.FromRgb(0xB0, 0x8C, 0xFF);
    private static readonly Color Green = Color.FromRgb(0x7C, 0xFF, 0x9E);
    private static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
    private static readonly IPen Frame = MakePen(Solid(60, 255, 255, 255), 1);
    private static readonly IPen CursorPen = MakePen(Solid(240, 255, 255, 255), 2);
    private static readonly IPen GoldPen = MakePen(Solid(255, Gold.R, Gold.G, Gold.B), 1.6);

    private readonly LabelCache _labels;

    public AcidBaseChart()
    {
        _labels = new LabelCache(this);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 1 || ActualHeight < 1) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        switch (Mode)
        {
            case AcidBaseMode.Scale:
                DrawIndicators(dc);
                break;

            case AcidBaseMode.Neutralize:
                DrawAmounts(dc);
                break;

            default:
                DrawLadder(dc);
                break;
        }
    }

    #region the charts

    /// <summary>[H₃O⁺] and [OH⁻] as two bars on one ladder of powers of ten from 1 to 10⁻¹⁴ mol/L.</summary>
    private void DrawLadder(PaintContext dc)
    {
        var species = AcidBaseModel.SpeciesOf(Beaker.Solution);
        var plot = new Rect(58, 14, Math.Max(10, ActualWidth - 72), Math.Max(10, ActualHeight - 42));
        double Y(double exponent) => plot.Top + (-exponent / 14 * plot.Height);
        for (var e = 0; e >= -14; e -= 2)
        {
            var y = Y(e);
            dc.DrawLine(Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            Label(dc, e == 0 ? "1" : "10" + Superscript(e), new Point(plot.Left - 6, y), DimColor, right: true);
        }

        var barWidth = Math.Min(54, plot.Width * 0.24);
        void Bar(double center, double amount, Color color, string nameKey)
        {
            var top = Y(Math.Log10(amount));
            var rect = new Rect(center - (barWidth / 2), top, barWidth, Math.Max(1, plot.Bottom - top));
            dc.DrawRectangle(Solid(170, color.R, color.G, color.B), null, rect);
            dc.DrawLine(MakePen(Solid(255, color.R, color.G, color.B), 2), rect.TopLeft, rect.TopRight);
            Label(dc, AcidBaseDiagram.Scientific(amount), new Point(center, top - 9), TextColor, center: true);
            Label(dc, Loc(nameKey), new Point(center, plot.Bottom + 12), color, center: true);
        }

        Bar(plot.Left + (plot.Width * 0.3), species.Hydronium, Red, "Acid_Chart_Hydronium");
        Bar(plot.Left + (plot.Width * 0.72), species.Hydroxide, Blue, "Acid_Chart_Hydroxide");
    }

    /// <summary>Each indicator's colours from pH 0 to 14 as a strip, the chosen one marked, with the liquid's pH as a line.</summary>
    private void DrawIndicators(PaintContext dc)
    {
        var dyes = AcidBaseDiagram.Dyes;
        var left = 96.0;
        var plot = new Rect(left, 6, Math.Max(10, ActualWidth - left - 10), Math.Max(10, ActualHeight - 28));
        var row = plot.Height / dyes.Length;
        for (var i = 0; i < dyes.Length; i++)
        {
            var strip = new Rect(plot.Left, plot.Top + (i * row) + 3, plot.Width, row - 6);
            var stops = new List<(Color, double)>();
            for (var p = 0.0; p <= 14.001; p += 0.5) stops.Add((AcidBaseModel.LiquidColor(dyes[i], p, 50), p / 14));
            dc.DrawRoundedRectangle(SpaceDrawing.LinearGradient(0, [.. stops]), dyes[i] == ScaleIndicator ? GoldPen : Frame, strip, 3, 3);
            Label(dc, Loc(AcidBaseDiagram.IndicatorShortKey(dyes[i])), new Point(plot.Left - 6, strip.Top + (strip.Height / 2)), dyes[i] == ScaleIndicator ? Gold : DimColor, right: true);
        }

        for (var p = 0; p <= 14; p += 2)
        {
            Label(dc, p.ToString(System.Globalization.CultureInfo.InvariantCulture), new Point(plot.Left + (p / 14.0 * plot.Width), plot.Bottom + 10), DimColor, center: true);
        }

        var x = plot.Left + (Math.Clamp(Ph, 0, 14) / 14 * plot.Width);
        dc.DrawLine(CursorPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
    }

    /// <summary>What is in the flask in millimoles: the acid not yet used, the salt made, the base left over.</summary>
    private void DrawAmounts(PaintContext dc)
    {
        var acid = AcidBaseModel.FlaskAcidMl * Molar;
        var added = BaseMl * Molar;
        var left = Math.Max(0, acid - added);
        var salt = Math.Min(acid, added);
        var extra = Math.Max(0, added - acid);
        var rows = new (string Key, double Amount, Color Color)[]
        {
            (Acid == TitrationAcid.Strong ? "Acid_Chart_AcidLeftStrong" : "Acid_Chart_AcidLeftWeak", left, Acid == TitrationAcid.Strong ? Red : Orange),
            (Acid == TitrationAcid.Strong ? "Acid_Chart_SaltStrong" : "Acid_Chart_SaltWeak", salt, Green),
            ("Acid_Chart_BaseExtra", extra, Violet),
        };

        var labelWidth = 118.0;
        var plot = new Rect(labelWidth, 10, Math.Max(10, ActualWidth - labelWidth - 54), Math.Max(10, ActualHeight - 34));
        var full = acid * 1.0;
        var row = plot.Height / rows.Length;
        for (var i = 0; i < rows.Length; i++)
        {
            var (key, amount, color) = rows[i];
            var y = plot.Top + (i * row);
            var bar = new Rect(plot.Left, y + (row * 0.22), Math.Max(1, plot.Width * Math.Min(1, amount / full)), row * 0.56);
            dc.DrawRectangle(Solid(40, 255, 255, 255), null, new Rect(plot.Left, bar.Top, plot.Width, bar.Height));
            if (amount > 1e-9) dc.DrawRectangle(Solid(200, color.R, color.G, color.B), null, bar);
            Label(dc, Loc(key), new Point(plot.Left - 8, bar.Top + (bar.Height / 2)), color, right: true);
            Label(dc, LocalizationService.Instance.Format("Acid_Value_Mmol", AcidBaseDiagram.Number(amount, "0.00")), new Point(plot.Right + 6, bar.Top + (bar.Height / 2)), TextColor);
        }

        Label(dc, Loc("Acid_Chart_Note"), new Point(plot.Left, plot.Bottom + 12), DimColor);
    }

    #endregion

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (Mode != AcidBaseMode.Scale) return;

        // Clicking a strip uses that indicator.
        var y = e.GetPosition(this).Y;
        var plotHeight = Math.Max(10, ActualHeight - 28);
        var index = (int)Math.Floor((y - 6) / (plotHeight / AcidBaseDiagram.Dyes.Length));
        if (index < 0 || index >= AcidBaseDiagram.Dyes.Length) return;
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        SetCurrentValue(ScaleIndicatorProperty, AcidBaseDiagram.Dyes[index]);
        e.Handled = true;
    }

    private void Label(PaintContext dc, string text, Point at, Color color, bool right = false, bool center = false)
    {
        var formatted = _labels.Text(text, 10.5, color);
        var x = right ? at.X - formatted.Width : center ? at.X - (formatted.Width / 2) : at.X;
        dc.DrawText(formatted, new Point(x, at.Y - (formatted.Height / 2)));
    }

    private static string Superscript(int value)
    {
        const string digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var text = Math.Abs(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return (value < 0 ? "⁻" : string.Empty) + string.Concat(text.Select(c => digits[c - '0']));
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);
}
