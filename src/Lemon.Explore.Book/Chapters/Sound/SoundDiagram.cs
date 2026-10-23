using System.ComponentModel;
using System.Diagnostics;
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

/// <summary>A change to one bar of the timbre view's overtones: which (0 = the fundamental), its new height, and whether the drag ended.</summary>
public readonly record struct HarmonicEdit(int Index, double Value, bool Done);

/// <summary>Where the string was let go: a fraction of its length from the nut, and how far it was pulled (−1…1 of the most).</summary>
public readonly record struct PluckRequest(double Point, double Height);

/// <summary>
/// The animated diagram of the sound page. Wave: a loudspeaker pushing the air, the dots of air bunching and spreading on
/// their way to an ear, the pressure along the way and a ruler (<c>SoundDiagram.Wave.cs</c>). Strings: a guitar string
/// plucked and its harmonics, or the air column of an open or closed pipe and its modes (<c>SoundDiagram.Strings.cs</c>).
/// Timbre: a note's waveform, its overtones (which can be dragged), its loudness over time and a keyboard
/// (<c>SoundDiagram.Timbre.cs</c>).
/// </summary>
/// <remarks>
/// Everything is slowed down a great deal and the air's movement exaggerated (the page says by how much). The diagram owns
/// the clock; it plays no sound itself but asks the page to (<see cref="PluckCommand"/>, <see cref="PlayNoteCommand"/>).
/// The mouse and the clickable things are in <c>SoundDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class SoundDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == MediumProperty)
        {
            ResetAir();
        }
        else if (change.Property == PluckCountProperty)
        {
            RestartString();
        }
        else if (change.Property == SoloHarmonicProperty)
        {
            RestartString();
        }
        else if (change.Property == NoteCountProperty)
        {
            OnNotePlayed();
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<SoundMode> ModeProperty =
        AvaloniaProperty.Register<SoundDiagram, SoundMode>(nameof(Mode), SoundMode.Wave);

    /// <summary>Wave: the loudspeaker's frequency, Hz.</summary>
    public static readonly StyledProperty<double> FrequencyProperty =
        AvaloniaProperty.Register<SoundDiagram, double>(nameof(Frequency), 440.0);

    /// <summary>Wave: the sound's level, dB.</summary>
    public static readonly StyledProperty<double> LoudnessProperty =
        AvaloniaProperty.Register<SoundDiagram, double>(nameof(Loudness), 60.0);

    public static readonly StyledProperty<SoundMedium> MediumProperty =
        AvaloniaProperty.Register<SoundDiagram, SoundMedium>(nameof(Medium), SoundMedium.Air);

    /// <summary>The air's temperature, °C (wave and pipes).</summary>
    public static readonly StyledProperty<double> TemperatureProperty =
        AvaloniaProperty.Register<SoundDiagram, double>(nameof(Temperature), 20.0);

    public static readonly StyledProperty<Resonator> ResonatorProperty =
        AvaloniaProperty.Register<SoundDiagram, Resonator>(nameof(Resonator), Resonator.String);

    /// <summary>Strings: the plucked string's fundamental now (fret and tension included), Hz.</summary>
    public static readonly StyledProperty<double> StringFrequencyProperty =
        AvaloniaProperty.Register<SoundDiagram, double>(nameof(StringFrequency), 110.0);

    /// <summary>Strings: the fret held down (0 for the open string); a click on the neck sets it.</summary>
    public static readonly StyledProperty<int> FretProperty =
        AvaloniaProperty.Register<SoundDiagram, int>(nameof(Fret), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Strings: where the string is plucked, a fraction of the vibrating length from the nut (or fret).</summary>
    public static readonly StyledProperty<double> PluckPointProperty =
        AvaloniaProperty.Register<SoundDiagram, double>(nameof(PluckPoint), 0.2);

    /// <summary>Strings: how far the string was pulled when let go (−1…1 of the most).</summary>
    public static readonly StyledProperty<double> PluckHeightProperty =
        AvaloniaProperty.Register<SoundDiagram, double>(nameof(PluckHeight), 0.8);

    /// <summary>Strings: changed (any new value) when the string is plucked again.</summary>
    public static readonly StyledProperty<int> PluckCountProperty =
        AvaloniaProperty.Register<SoundDiagram, int>(nameof(PluckCount), 0);

    /// <summary>Strings: one harmonic sounding alone (1–6), or 0 for the plucked mix; a click on a row sets it.</summary>
    public static readonly StyledProperty<int> SoloHarmonicProperty =
        AvaloniaProperty.Register<SoundDiagram, int>(nameof(SoloHarmonic), 0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowHarmonicsProperty =
        AvaloniaProperty.Register<SoundDiagram, bool>(nameof(ShowHarmonics), true);

    /// <summary>Pipes: the tube's length, m.</summary>
    public static readonly StyledProperty<double> PipeLengthProperty =
        AvaloniaProperty.Register<SoundDiagram, double>(nameof(PipeLength), 0.6);

    /// <summary>Pipes: which resonance sounds (1 the lowest); a click on a row sets it.</summary>
    public static readonly StyledProperty<int> PipeModeProperty =
        AvaloniaProperty.Register<SoundDiagram, int>(nameof(PipeMode), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Timbre: the key played, as a MIDI number (60 = C4); a click on the keyboard sets it.</summary>
    public static readonly StyledProperty<int> NoteMidiProperty =
        AvaloniaProperty.Register<SoundDiagram, int>(nameof(NoteMidi), 69, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<Instrument> InstrumentProperty =
        AvaloniaProperty.Register<SoundDiagram, Instrument>(nameof(Instrument), Instrument.Fork);

    /// <summary>Timbre: the overtones' amplitudes, the fundamental first.</summary>
    public static readonly StyledProperty<double[]> HarmonicsProperty =
        AvaloniaProperty.Register<SoundDiagram, double[]>(nameof(Harmonics), SoundModel.Harmonics(Instrument.Fork));

    public static readonly StyledProperty<SecondNote> SecondNoteProperty =
        AvaloniaProperty.Register<SoundDiagram, SecondNote>(nameof(SecondNote), SecondNote.None);

    /// <summary>Timbre: how far the beating note is off, Hz.</summary>
    public static readonly StyledProperty<double> DetuneProperty =
        AvaloniaProperty.Register<SoundDiagram, double>(nameof(Detune), 3.0);

    /// <summary>Timbre: changed (any new value) when a note is played, to run the loudness curve's marker and light the keys.</summary>
    public static readonly StyledProperty<int> NoteCountProperty =
        AvaloniaProperty.Register<SoundDiagram, int>(nameof(NoteCount), 0);

    /// <summary>Run with a <see cref="PluckRequest"/> when the viewer lets go of the string.</summary>
    public static readonly StyledProperty<ICommand?> PluckCommandProperty =
        AvaloniaProperty.Register<SoundDiagram, ICommand?>(nameof(PluckCommand));

    /// <summary>Run (after <see cref="NoteMidi"/> is set) when the viewer clicks a key.</summary>
    public static readonly StyledProperty<ICommand?> PlayNoteCommandProperty =
        AvaloniaProperty.Register<SoundDiagram, ICommand?>(nameof(PlayNoteCommand));

    /// <summary>Run with a <see cref="HarmonicEdit"/> while an overtone bar is dragged.</summary>
    public static readonly StyledProperty<ICommand?> EditHarmonicCommandProperty =
        AvaloniaProperty.Register<SoundDiagram, ICommand?>(nameof(EditHarmonicCommand));

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<SoundDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>Playback speed: 1 is the normal (already slowed) pace.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<SoundDiagram, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<SoundDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<SoundDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<SoundDiagram>(ModeProperty, FrequencyProperty, LoudnessProperty, MediumProperty, TemperatureProperty, ResonatorProperty, StringFrequencyProperty, FretProperty, PluckPointProperty, PluckHeightProperty, PluckCountProperty, SoloHarmonicProperty, ShowHarmonicsProperty, PipeLengthProperty, PipeModeProperty, NoteMidiProperty, InstrumentProperty, HarmonicsProperty, SecondNoteProperty, DetuneProperty, NoteCountProperty, ShowLabelsProperty, SpeedProperty, ToolbarHeightProperty);
        return true;
    }


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

    public SoundMedium Medium
    {
        get => GetValue(MediumProperty);
        set => SetValue(MediumProperty, value);
    }

    public double Temperature
    {
        get => GetValue(TemperatureProperty);
        set => SetValue(TemperatureProperty, value);
    }

    public Resonator Resonator
    {
        get => GetValue(ResonatorProperty);
        set => SetValue(ResonatorProperty, value);
    }

    public double StringFrequency
    {
        get => GetValue(StringFrequencyProperty);
        set => SetValue(StringFrequencyProperty, value);
    }

    public int Fret
    {
        get => GetValue(FretProperty);
        set => SetValue(FretProperty, value);
    }

    public double PluckPoint
    {
        get => GetValue(PluckPointProperty);
        set => SetValue(PluckPointProperty, value);
    }

    public double PluckHeight
    {
        get => GetValue(PluckHeightProperty);
        set => SetValue(PluckHeightProperty, value);
    }

    public int PluckCount
    {
        get => GetValue(PluckCountProperty);
        set => SetValue(PluckCountProperty, value);
    }

    public int SoloHarmonic
    {
        get => GetValue(SoloHarmonicProperty);
        set => SetValue(SoloHarmonicProperty, value);
    }

    public bool ShowHarmonics
    {
        get => GetValue(ShowHarmonicsProperty);
        set => SetValue(ShowHarmonicsProperty, value);
    }

    public double PipeLength
    {
        get => GetValue(PipeLengthProperty);
        set => SetValue(PipeLengthProperty, value);
    }

    public int PipeMode
    {
        get => GetValue(PipeModeProperty);
        set => SetValue(PipeModeProperty, value);
    }

    public int NoteMidi
    {
        get => GetValue(NoteMidiProperty);
        set => SetValue(NoteMidiProperty, value);
    }

    public Instrument Instrument
    {
        get => GetValue(InstrumentProperty);
        set => SetValue(InstrumentProperty, value);
    }

    public double[] Harmonics
    {
        get => (double[])GetValue(HarmonicsProperty);
        set => SetValue(HarmonicsProperty, value);
    }

    public SecondNote SecondNote
    {
        get => GetValue(SecondNoteProperty);
        set => SetValue(SecondNoteProperty, value);
    }

    public double Detune
    {
        get => GetValue(DetuneProperty);
        set => SetValue(DetuneProperty, value);
    }

    public int NoteCount
    {
        get => GetValue(NoteCountProperty);
        set => SetValue(NoteCountProperty, value);
    }

    public ICommand? PluckCommand
    {
        get => GetValue(PluckCommandProperty);
        set => SetValue(PluckCommandProperty, value);
    }

    public ICommand? PlayNoteCommand
    {
        get => GetValue(PlayNoteCommandProperty);
        set => SetValue(PlayNoteCommandProperty, value);
    }

    public ICommand? EditHarmonicCommand
    {
        get => GetValue(EditHarmonicCommandProperty);
        set => SetValue(EditHarmonicCommandProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public double ToolbarHeight
    {
        get => GetValue(ToolbarHeightProperty);
        set => SetValue(ToolbarHeightProperty, value);
    }

    #endregion

    private readonly Stopwatch _clock = new();
    private readonly LabelCache _labels;
    private double _lastSeconds;

    public SoundDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
    }

    #region lifecycle

    private bool _attached;

    private void RequestFrame()
    {
        if (_attached)
        {
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
        }
    }

    private void OnFrame(TimeSpan time)
    {
        if (!_attached)
        {
            return;
        }

        OnRendering();
        RequestFrame();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _clock.Start();
        _lastSeconds = _clock.Elapsed.TotalSeconds;
        RequestFrame();
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        _clock.Stop();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => _labels.Clear();

    private void OnModeChanged()
    {
        _drag = DragKind.None;
        if (Mode == SoundMode.Wave) ResetAir();
        if (Mode == SoundMode.Strings) RestartString();
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        var step = IsPlaying ? dt * Math.Clamp(Speed, 0.05, 4) : 0;
        switch (Mode)
        {
            case SoundMode.Wave:
                // A cone pushed by hand moves the air even while paused.
                StepAir(_drag == DragKind.Cone && step == 0 ? dt * Math.Clamp(Speed, 0.05, 4) : step);
                break;

            case SoundMode.Strings:
                if (_drag != DragKind.String) _stringTime += step;
                _pipeTime += step;
                break;

        }

        InvalidateVisual();
    }

    #endregion

    /// <summary>The room for the picture: below the title, above the toolbar (and a walkthrough's subtitles).</summary>
    private Rect Stage()
    {
        var top = TitleSpace;
        var bottom = Math.Max(ToolbarSpace, ToolbarHeight + 18 + 16) + _reserve.Current;
        return new Rect(0, top, Math.Max(100, ActualWidth), Math.Max(200, ActualHeight - top - bottom));
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        _targets.Clear();
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        var now = _clock.Elapsed.TotalSeconds;
        switch (Mode)
        {
            case SoundMode.Strings:
                if (Resonator == Resonator.String) DrawStringMode(dc, now);
                else DrawPipeMode(dc, now);
                break;

            case SoundMode.Timbre:
                DrawTimbreMode(dc, now);
                break;

            default:
                DrawWaveMode(dc, now);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
    }

    #region drawing helpers

    /// <summary>Already localized text on a dark pill, centred on <paramref name="center"/> and kept inside the control.</summary>
    private Rect DrawPill(PaintContext dc, string text, Point center, Color color, double size = 11.5)
    {
        var formatted = _labels.Text(text, size, color);
        var x = Math.Clamp(center.X - (formatted.Width / 2) - 6, 2, Math.Max(2, ActualWidth - formatted.Width - 14));
        var y = Math.Clamp(center.Y - (formatted.Height / 2) - 2, 2, Math.Max(2, ActualHeight - formatted.Height - 6));
        _labels.DrawTextPill(dc, text, new Point(x, y), size, color);
        return new Rect(x, y, formatted.Width + 12, formatted.Height + 4);
    }

    /// <summary>A pill that is also a target for <paramref name="id"/>.</summary>
    private Rect DrawTargetPill(PaintContext dc, string text, Point center, Color color, string id, double size = 11.5)
    {
        var box = DrawPill(dc, text, center, color, size);
        AddBoxTarget(id, box);
        return box;
    }

    /// <summary>Text with its left edge (or its centre, or its right edge) at a point, its middle at the point's height.</summary>
    private Rect DrawText(PaintContext dc, string text, Point at, Color color, double size = 11, TextAnchor anchor = TextAnchor.Left, bool bold = false)
    {
        var formatted = _labels.Text(text, size, color, bold);
        var x = anchor switch
        {
            TextAnchor.Center => at.X - (formatted.Width / 2),
            TextAnchor.Right => at.X - formatted.Width,
            _ => at.X,
        };
        var topLeft = new Point(x, at.Y - (formatted.Height / 2));
        dc.DrawText(formatted, topLeft);
        return new Rect(topLeft, new Size(formatted.Width, formatted.Height));
    }

    private enum TextAnchor
    {
        Left,
        Center,
        Right,
    }

    /// <summary>A double-headed arrow from <paramref name="a"/> to <paramref name="b"/> (a span such as a wavelength).</summary>
    private static void DrawSpan(PaintContext dc, Point a, Point b, Color color, double thickness = 1.4)
    {
        Vector v = b - a;
        if (v.Length < 8) return;
        var direction = v / v.Length;
        var brush = Solid(255, color.R, color.G, color.B);
        dc.DrawLine(MakePen(brush, thickness), a + (direction * 4), b - (direction * 4));
        DrawArrowHead(dc, brush, b, direction, 7);
        DrawArrowHead(dc, brush, a, -direction, 7);
    }

    private readonly Dictionary<(Color Color, int Alpha, double Thickness, bool Dashed, bool Flat), IPen> _fadedPens = [];
    private readonly Dictionary<(Color Color, int Alpha), IBrush> _fadedBrushes = [];

    /// <summary>A frozen pen of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IPen FadedPen(Color color, double alpha, double thickness, bool dashed = false, bool flat = false)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20), Math.Round(thickness, 1), dashed, flat);
        if (_fadedPens.TryGetValue(key, out var pen)) return pen;
        var brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        pen = new ImmutablePen(((IBrush)brush).ToImmutable(), key.Item3, dashed ? new ImmutableDashStyle([4, 4], 0) : null, flat ? PenLineCap.Flat : PenLineCap.Round, PenLineJoin.Round);
        _fadedPens[key] = pen;
        return pen;
    }

    /// <summary>A frozen brush of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IBrush FadedBrush(Color color, double alpha)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20));
        if (_fadedBrushes.TryGetValue(key, out var brush)) return brush;
        brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        _fadedBrushes[key] = brush;
        return brush;
    }

    /// <summary>A line through the points, as one figure.</summary>
    private static StreamGeometry Line(IEnumerable<Point> points, bool closed = false, bool filled = false)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = true;
            foreach (var point in points)
            {
                if (first) context.BeginFigure(point, filled);
                else context.LineTo(point);
                first = false;
            }

            if (!first) context.EndFigure(closed);
        }
        return geometry;
    }

    /// <summary>A rounded frame for a panel inside the picture (the scope, the overtones).</summary>
    private static void DrawFrame(PaintContext dc, Rect box) => dc.DrawRoundedRectangle(Ink.Panel, Ink.Frame, box, 12, 12);

    /// <summary>A frequency with a sensible number of digits: 82.4 Hz, 440 Hz, 4.0 kHz.</summary>
    internal static string Hertz(double hertz) => hertz switch
    {
        >= 9_995 => LocFormat("Sound_Value_KHz", Number(hertz / 1000, "0.#")),
        >= 1_000 => LocFormat("Sound_Value_KHz", Number(hertz / 1000, "0.00")),
        >= 100 => LocFormat("Sound_Value_Hz", Number(hertz, "0")),
        _ => LocFormat("Sound_Value_Hz", Number(hertz, "0.0")),
    };

    /// <summary>A length in the handiest unit: mm, cm or m.</summary>
    internal static string Metres(double metres) => metres switch
    {
        < 0.01 => LocFormat("Sound_Value_Mm", Number(metres * 1000, "0.0")),
        < 1 => LocFormat("Sound_Value_Cm", Number(metres * 100, metres < 0.1 ? "0.0" : "0")),
        < 10 => LocFormat("Sound_Value_M", Number(metres, "0.00")),
        _ => LocFormat("Sound_Value_M", Number(metres, "0.0")),
    };

    internal static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    private static double Smooth(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - (2 * t));
    }

    #endregion

    private static class Ink
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Green = Color.FromRgb(0x7C, 0xFF, 0x9E);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9A, 0x4A);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x5A, 0x5F);
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xA8, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xB0, 0x8C, 0xFF);
        public static readonly Color Wood = Color.FromRgb(0x5A, 0x3A, 0x24);
        public static readonly Color WoodLight = Color.FromRgb(0xC8, 0x92, 0x5A);
        public static readonly Color Steel = Color.FromRgb(0xD8, 0xDE, 0xE8);
        public static readonly Color Ivory = Color.FromRgb(0xF2, 0xEA, 0xD8);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x12, 0x17, 0x26)), new ImmutableGradientStop(1, Color.FromRgb(0x05, 0x07, 0x0E))],
            center: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.85, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.85, RelativeUnit.Relative));

        public static readonly IBrush Panel = Solid(150, 8, 12, 22);
        public static readonly IPen Frame = MakePen(Solid(46, 255, 255, 255), 1);
        public static readonly IPen Grid = MakePen(Solid(20, 255, 255, 255), 1);
        public static readonly IPen Axis = MakePen(Solid(70, 255, 255, 255), 1);
    }
}
