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

namespace Lemon.Explore.Book.Chapters.Sound;

/// <summary>
/// The side panel's chart, one per view. Wave: what a young ear hears, frequency against level, from the threshold of
/// hearing to the threshold of pain, with the speaker's tone as a dot (drag it to change both). Strings: how strong each
/// harmonic of the pluck is (the ones the pluck cannot start crossed out), or the pipe's allowed resonances. Timbre: the
/// frequency of every key on the keyboard, doubling every octave.
/// </summary>
public sealed class SoundChart : Control
{
    static SoundChart()
    {
        AffectsRender<SoundChart>(ModeProperty, FrequencyProperty, LoudnessProperty, ResonatorProperty, PluckPointProperty, SoloHarmonicProperty, StringFrequencyProperty, PipeModeProperty, PipeFrequencyProperty, NoteMidiProperty);
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

    public static readonly StyledProperty<SoundMode> ModeProperty =
        AvaloniaProperty.Register<SoundChart, SoundMode>(nameof(Mode), SoundMode.Wave);

    public static readonly StyledProperty<double> FrequencyProperty =
        AvaloniaProperty.Register<SoundChart, double>(nameof(Frequency), 440.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> LoudnessProperty =
        AvaloniaProperty.Register<SoundChart, double>(nameof(Loudness), 60.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<Resonator> ResonatorProperty =
        AvaloniaProperty.Register<SoundChart, Resonator>(nameof(Resonator), Resonator.String);

    public static readonly StyledProperty<double> PluckPointProperty =
        AvaloniaProperty.Register<SoundChart, double>(nameof(PluckPoint), 0.2);

    public static readonly StyledProperty<int> SoloHarmonicProperty =
        AvaloniaProperty.Register<SoundChart, int>(nameof(SoloHarmonic), 0);

    public static readonly StyledProperty<double> StringFrequencyProperty =
        AvaloniaProperty.Register<SoundChart, double>(nameof(StringFrequency), 110.0);

    public static readonly StyledProperty<int> PipeModeProperty =
        AvaloniaProperty.Register<SoundChart, int>(nameof(PipeMode), 1);

    public static readonly StyledProperty<double> PipeFrequencyProperty =
        AvaloniaProperty.Register<SoundChart, double>(nameof(PipeFrequency), 286.0);

    public static readonly StyledProperty<int> NoteMidiProperty =
        AvaloniaProperty.Register<SoundChart, int>(nameof(NoteMidi), 69);

    /// <summary>Run when the viewer starts dragging the dot (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<SoundChart, ICommand?>(nameof(InteractionCommand));


    public SoundMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Frequency
    {
        get => GetValue(FrequencyProperty);
        set => SetValue(FrequencyProperty, value);
    }

    public double Loudness
    {
        get => GetValue(LoudnessProperty);
        set => SetValue(LoudnessProperty, value);
    }

    public Resonator Resonator
    {
        get => GetValue(ResonatorProperty);
        set => SetValue(ResonatorProperty, value);
    }

    public double PluckPoint
    {
        get => GetValue(PluckPointProperty);
        set => SetValue(PluckPointProperty, value);
    }

    public int SoloHarmonic
    {
        get => GetValue(SoloHarmonicProperty);
        set => SetValue(SoloHarmonicProperty, value);
    }

    public double StringFrequency
    {
        get => GetValue(StringFrequencyProperty);
        set => SetValue(StringFrequencyProperty, value);
    }

    public int PipeMode
    {
        get => GetValue(PipeModeProperty);
        set => SetValue(PipeModeProperty, value);
    }

    public double PipeFrequency
    {
        get => GetValue(PipeFrequencyProperty);
        set => SetValue(PipeFrequencyProperty, value);
    }

    public int NoteMidi
    {
        get => GetValue(NoteMidiProperty);
        set => SetValue(NoteMidiProperty, value);
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

    private const double MinHz = 20;
    private const double MaxHz = 20_000;
    private const double MinDb = -10;
    private const double MaxDb = 140;

    private readonly LabelCache _labels;
    private bool _dragging;

    public SoundChart()
    {
        _labels = new LabelCache(this);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    private Rect Plot => new(42, 12, Math.Max(10, ActualWidth - 54), Math.Max(10, ActualHeight - 40));

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 80 || height < 60) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
        switch (Mode)
        {
            case SoundMode.Strings:
                if (Resonator == Resonator.String) DrawHarmonics(dc, Plot);
                else DrawPipeLadder(dc, Plot);
                break;

            case SoundMode.Timbre:
                DrawKeys(dc, Plot);
                break;

            default:
                DrawHearing(dc, Plot);
                break;
        }
    }

    #region hearing

    private static double HzX(Rect plot, double hertz) => plot.Left + (plot.Width * Math.Log(hertz / MinHz) / Math.Log(MaxHz / MinHz));

    private static double DbY(Rect plot, double decibels) => plot.Bottom - (plot.Height * (decibels - MinDb) / (MaxDb - MinDb));

    private void DrawHearing(PaintContext dc, Rect plot)
    {
        foreach (var hz in new[] { 20.0, 100, 1000, 10_000 })
        {
            var x = HzX(plot, hz);
            dc.DrawLine(Grid, new Point(x, plot.Top), new Point(x, plot.Bottom));
            Label(dc, hz >= 1000 ? Format("Sound_Value_KHz", Num(hz / 1000, "0")) : Format("Sound_Value_Hz", Num(hz, "0")), new Point(x, plot.Bottom + 9), DimColor, center: true);
        }

        foreach (var db in new[] { 0.0, 40, 80, 120 })
        {
            var y = DbY(plot, db);
            dc.DrawLine(Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            Label(dc, Format("Sound_Value_Db", Num(db, "0")), new Point(plot.Left - 4, y), DimColor, right: true);
        }

        // Below the threshold: too quiet to hear.
        var threshold = new List<Point>();
        for (var i = 0; i <= 120; i++)
        {
            var hz = MinHz * Math.Pow(MaxHz / MinHz, i / 120.0);
            threshold.Add(new Point(HzX(plot, hz), DbY(plot, Math.Clamp(SoundModel.HearingThreshold(hz), MinDb, MaxDb))));
        }

        var quiet = new List<Point>(threshold) { new(plot.Right, plot.Bottom), new(plot.Left, plot.Bottom) };
        dc.DrawGeometry(Solid(60, 0, 0, 0), null, Shape(quiet, true));
        dc.DrawGeometry(null, MakePen(Solid(230, Cyan.R, Cyan.G, Cyan.B), 1.6), Shape(threshold, false));

        var pain = DbY(plot, SoundModel.PainThreshold);
        dc.DrawLine(MakePen(Solid(220, Red.R, Red.G, Red.B), 1.2, dashed: true), new Point(plot.Left, pain), new Point(plot.Right, pain));
        Label(dc, Get("Sound_Chart_Pain"), new Point(plot.Right - 2, pain - 8), Red, right: true);
        Label(dc, Get("Sound_Chart_Silent"), new Point(HzX(plot, 1000), DbY(plot, -4)), DimColor, center: true);

        // Where speech sits.
        var speech = new Rect(new Point(HzX(plot, 250), DbY(plot, 70)), new Point(HzX(plot, 4000), DbY(plot, 40)));
        dc.DrawRoundedRectangle(Solid(24, Green.R, Green.G, Green.B), MakePen(Solid(90, Green.R, Green.G, Green.B), 1), speech, 6, 6);
        Label(dc, Get("Sound_Chart_Speech"), new Point(speech.Left + (speech.Width / 2), speech.Top + (speech.Height / 2)), Green, center: true);

        var heard = SoundModel.IsAudible(Frequency, Loudness);
        var dot = new Point(HzX(plot, Math.Clamp(Frequency, MinHz, MaxHz)), DbY(plot, Math.Clamp(Loudness, MinDb, MaxDb)));
        var color = heard ? Gold : Red;
        dc.DrawEllipse(Solid(70, color.R, color.G, color.B), null, dot, 10, 10);
        dc.DrawEllipse(Solid(255, color.R, color.G, color.B), MakePen(Brushes.White, 1), dot, 5, 5);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (Mode != SoundMode.Wave) return;
        e.Pointer.Capture(this);
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        _dragging = true;
        MoveDot(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Cursor = Mode == SoundMode.Wave ? new Cursor(StandardCursorType.Cross) : null;
        if (_dragging) MoveDot(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    /// <summary>The tone set from a point on the chart: frequency from x (log), level from y.</summary>
    private void MoveDot(Point point)
    {
        var plot = Plot;
        var fx = Math.Clamp((point.X - plot.Left) / plot.Width, 0, 1);
        var fy = Math.Clamp((plot.Bottom - point.Y) / plot.Height, 0, 1);
        var hz = MinHz * Math.Pow(MaxHz / MinHz, fx);
        SetCurrentValue(FrequencyProperty, Math.Round(hz, hz < 100 ? 1 : 0));
        SetCurrentValue(LoudnessProperty, Math.Round(Math.Clamp(MinDb + ((MaxDb - MinDb) * fy), 0, 120)));
    }

    #endregion

    #region strings and pipes

    private void DrawHarmonics(PaintContext dc, Rect plot)
    {
        const int Count = 12;
        var largest = 1e-6;
        for (var n = 1; n <= Count; n++) largest = Math.Max(largest, Math.Abs(SoundModel.PluckAmplitude(n, PluckPoint)));
        dc.DrawLine(Axis, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));
        foreach (var level in new[] { 0.5, 1.0 })
        {
            var y = plot.Bottom - (plot.Height * level);
            dc.DrawLine(Grid, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        Label(dc, "1", new Point(plot.Left - 4, plot.Top), DimColor, right: true);
        var slot = plot.Width / Count;
        for (var n = 1; n <= Count; n++)
        {
            var cx = plot.Left + ((n - 0.5) * slot);
            var value = SoloHarmonic > 0 ? (n == SoloHarmonic ? 1 : 0) : Math.Abs(SoundModel.PluckAmplitude(n, PluckPoint)) / largest;
            var color = n == 1 ? Gold : Cyan;
            var h = value * plot.Height;
            dc.DrawRoundedRectangle(Solid(220, color.R, color.G, color.B), null, new Rect(cx - (slot * 0.3), plot.Bottom - h, slot * 0.6, Math.Max(1, h)), 2, 2);
            Label(dc, n.ToString(CultureInfo.InvariantCulture), new Point(cx, plot.Bottom + 9), DimColor, center: true);
            if (SoloHarmonic == 0 && SoundModel.IsSilenced(n, PluckPoint)) Label(dc, "×", new Point(cx, plot.Bottom - 10), Red, center: true);
        }

        Label(dc, Format("Sound_Chart_HarmonicHz", Format("Sound_Value_Hz", Num(StringFrequency, StringFrequency < 100 ? "0.0" : "0"))), new Point(plot.Right, plot.Top + 4), DimColor, right: true);
    }

    private void DrawPipeLadder(PaintContext dc, Rect plot)
    {
        const int Count = 10;
        var closed = Resonator == Resonator.ClosedPipe;
        var chosen = SoundModel.PipeHarmonic(PipeMode, closed);
        var unit = PipeFrequency / chosen;
        dc.DrawLine(Axis, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));
        var slot = plot.Width / Count;
        for (var n = 1; n <= Count; n++)
        {
            var cx = plot.Left + ((n - 0.5) * slot);
            var allowed = !closed || n % 2 == 1;
            var h = plot.Height * n / Count;
            var rect = new Rect(cx - (slot * 0.3), plot.Bottom - h, slot * 0.6, h);
            if (allowed)
            {
                var color = n == chosen ? Gold : Green;
                dc.DrawRoundedRectangle(Solid((byte)(n == chosen ? 240 : 150), color.R, color.G, color.B), null, rect, 2, 2);
            }
            else
            {
                dc.DrawRoundedRectangle(null, MakePen(Solid(90, DimColor.R, DimColor.G, DimColor.B), 1, dashed: true), rect, 2, 2);
            }

            Label(dc, n.ToString(CultureInfo.InvariantCulture), new Point(cx, plot.Bottom + 9), allowed ? DimColor : Color.FromRgb(0x55, 0x5E, 0x6C), center: true);
        }

        Label(dc, Format("Sound_Chart_PipeUnit", Format("Sound_Value_Hz", Num(unit, unit < 100 ? "0.0" : "0"))), new Point(plot.Left + 2, plot.Top + 4), DimColor);
        Label(dc, Get(closed ? "Sound_Chart_OddOnly" : "Sound_Chart_AllHarmonics"), new Point(plot.Left + 2, plot.Top + 20), closed ? Gold : Green);
    }

    #endregion

    #region keys

    private void DrawKeys(PaintContext dc, Rect plot)
    {
        const int Low = 60;
        const int High = 83;
        var top = SoundModel.NoteFrequency(High);
        foreach (var hz in new[] { 250.0, 500, 750, 1000 })
        {
            var y = plot.Bottom - (plot.Height * hz / 1000);
            dc.DrawLine(Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            Label(dc, Num(hz, "0"), new Point(plot.Left - 4, y), DimColor, right: true);
        }

        var slot = plot.Width / (High - Low + 1);
        for (var m = Low; m <= High; m++)
        {
            var f = SoundModel.NoteFrequency(m);
            var h = plot.Height * f / 1000;
            var cx = plot.Left + ((m - Low + 0.5) * slot);
            var color = m == NoteMidi ? Gold : SoundModel.IsBlack(m) ? Violet : Cyan;
            dc.DrawRectangle(Solid((byte)(m == NoteMidi ? 255 : 170), color.R, color.G, color.B), null, new Rect(cx - (slot * 0.36), plot.Bottom - h, slot * 0.72, h));
            if (m % 12 == 0) Label(dc, SoundModel.NoteName(m), new Point(cx, plot.Bottom + 9), DimColor, center: true);
        }

        var c4 = SoundModel.NoteFrequency(60);
        var c5 = SoundModel.NoteFrequency(72);
        Label(dc, Format("Sound_Chart_Doubling", Num(c4, "0"), Num(c5, "0"), Num(top, "0")), new Point(plot.Left + 2, plot.Top + 4), DimColor);
        var chosen = SoundModel.NoteFrequency(NoteMidi);
        Label(dc, $"{SoundModel.NoteName(NoteMidi)} · {Format("Sound_Value_Hz", Num(chosen, "0.0"))}", new Point(plot.Left + 2, plot.Top + 20), Gold);
        dc.DrawLine(Axis, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));
    }

    #endregion

    private static StreamGeometry Shape(IReadOnlyList<Point> points, bool filled)
    {
        var geometry = new StreamGeometry();
        using (var c = geometry.Open())
        {
            c.BeginFigure(points[0], filled);
            for (var i = 1; i < points.Count; i++) c.LineTo(points[i]);
            c.EndFigure(filled);
        }
        return geometry;
    }

    private void Label(PaintContext dc, string text, Point at, Color color, bool center = false, bool right = false)
    {
        var formatted = _labels.Text(text, 10.5, color);
        var x = center ? at.X - (formatted.Width / 2) : right ? at.X - formatted.Width : at.X;
        dc.DrawText(formatted, new Point(x, at.Y - (formatted.Height / 2)));
    }

    private static string Num(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Get(string key) => LocalizationService.Instance.GetString(key);

    private static string Format(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);
}
