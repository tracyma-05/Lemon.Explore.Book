using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Chapters.BlueSky;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Spectra;

/// <summary>
/// The animated diagram of the spectra page. Atom: hydrogen's electron on its levels, photons in and out, the energy
/// ladder and the lines given out so far (<c>SpectraDiagram.Atom.cs</c>). Lamps: a light through a slit and a prism and
/// the spectrum it makes (<c>SpectraDiagram.Lamps.cs</c>). Stars: a star's colour, its black-body curve and its dark
/// lines, shifted by its motion against a lab lamp (<c>SpectraDiagram.Stars.cs</c>).
/// </summary>
/// <remarks>The mouse and the clickable things are in <c>SpectraDiagram.Objects.cs</c>.</remarks>
public sealed partial class SpectraDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == LevelProperty)
        {
            OnLevelChanged(change.GetOldValue<int>(), change.GetNewValue<int>());
        }
        else if (change.Property == FireCountProperty)
        {
            FirePhoton();
        }
        else if (change.Property == ClearCountProperty)
        {
            ClearLines();
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<SpectraMode> ModeProperty =
        AvaloniaProperty.Register<SpectraDiagram, SpectraMode>(nameof(Mode), SpectraMode.Atom);

    /// <summary>Atom: the electron's level (1 the ground; 0 set free). Set from outside it excites or drops the electron; the atom pushes its falls back.</summary>
    public static readonly StyledProperty<int> LevelProperty =
        AvaloniaProperty.Register<SpectraDiagram, int>(nameof(Level), 1, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<FallMode> FallModeProperty =
        AvaloniaProperty.Register<SpectraDiagram, FallMode>(nameof(FallMode), FallMode.Chance);

    /// <summary>Atom: the electron stays up instead of falling by itself.</summary>
    public static readonly StyledProperty<bool> HoldProperty =
        AvaloniaProperty.Register<SpectraDiagram, bool>(nameof(Hold), false);

    /// <summary>Atom: the wavelength of the photons the lamp fires, nm.</summary>
    public static readonly StyledProperty<double> PhotonNmProperty =
        AvaloniaProperty.Register<SpectraDiagram, double>(nameof(PhotonNm), 656.28, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Atom: changed (any new value) to fire a photon.</summary>
    public static readonly StyledProperty<int> FireCountProperty =
        AvaloniaProperty.Register<SpectraDiagram, int>(nameof(FireCount), 0);

    /// <summary>Atom: changed (any new value) to clear the lines collected so far.</summary>
    public static readonly StyledProperty<int> ClearCountProperty =
        AvaloniaProperty.Register<SpectraDiagram, int>(nameof(ClearCount), 0);

    public static readonly StyledProperty<bool> ShowCloudProperty =
        AvaloniaProperty.Register<SpectraDiagram, bool>(nameof(ShowCloud), false);

    /// <summary>Atom: the cloud's shape, l (0 s, 1 p, 2 d), kept below the level.</summary>
    public static readonly StyledProperty<int> CloudShapeProperty =
        AvaloniaProperty.Register<SpectraDiagram, int>(nameof(CloudShape), 0);

    /// <summary>Atom: what last happened, pushed out: "emit 3 2", "absorb 2 3", "free 2", "pass 640.0", "capture 5".</summary>
    public static readonly StyledProperty<string?> LastEventProperty =
        AvaloniaProperty.Register<SpectraDiagram, string?>(nameof(LastEvent), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<LightSource> SourceProperty =
        AvaloniaProperty.Register<SpectraDiagram, LightSource>(nameof(Source), LightSource.Lamp);

    public static readonly StyledProperty<Gas> GasProperty =
        AvaloniaProperty.Register<SpectraDiagram, Gas>(nameof(Gas), Gas.Hydrogen);

    /// <summary>Lamps: the lamp's gas is hidden (the guessing game).</summary>
    public static readonly StyledProperty<bool> MysteryProperty =
        AvaloniaProperty.Register<SpectraDiagram, bool>(nameof(Mystery), false);

    /// <summary>Lamps: a second gas shown under the spectrum to compare with, or null.</summary>
    public static readonly StyledProperty<Gas?> CompareGasProperty =
        AvaloniaProperty.Register<SpectraDiagram, Gas?>(nameof(CompareGas));

    /// <summary>Lamps: the line picked by a click, nm (NaN for none).</summary>
    public static readonly StyledProperty<double> SelectedLineProperty =
        AvaloniaProperty.Register<SpectraDiagram, double>(nameof(SelectedLine), double.NaN, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Lamps: a magnified look at a few nanometres round the picked line.</summary>
    public static readonly StyledProperty<bool> ZoomProperty =
        AvaloniaProperty.Register<SpectraDiagram, bool>(nameof(Zoom), false);

    /// <summary>Stars: the star's surface temperature, K. Dragging the curve's peak sets it.</summary>
    public static readonly StyledProperty<double> KelvinProperty =
        AvaloniaProperty.Register<SpectraDiagram, double>(nameof(Kelvin), SpectraModel.SunKelvin, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Stars: how fast the star moves away from us, km/s (negative: towards). Dragging its spectrum sets it.</summary>
    public static readonly StyledProperty<double> VelocityProperty =
        AvaloniaProperty.Register<SpectraDiagram, double>(nameof(Velocity), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<SpectraDiagram, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<SpectraDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<SpectraDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<SpectraDiagram>(ModeProperty, LevelProperty, FallModeProperty, HoldProperty, PhotonNmProperty, FireCountProperty, ClearCountProperty, ShowCloudProperty, CloudShapeProperty, SourceProperty, GasProperty, MysteryProperty, CompareGasProperty, SelectedLineProperty, ZoomProperty, KelvinProperty, VelocityProperty, ShowLabelsProperty, ToolbarHeightProperty);
        return true;
    }


    public SpectraMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public int Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public FallMode FallMode
    {
        get => GetValue(FallModeProperty);
        set => SetValue(FallModeProperty, value);
    }

    public bool Hold
    {
        get => GetValue(HoldProperty);
        set => SetValue(HoldProperty, value);
    }

    public double PhotonNm
    {
        get => GetValue(PhotonNmProperty);
        set => SetValue(PhotonNmProperty, value);
    }

    public int FireCount
    {
        get => GetValue(FireCountProperty);
        set => SetValue(FireCountProperty, value);
    }

    public int ClearCount
    {
        get => GetValue(ClearCountProperty);
        set => SetValue(ClearCountProperty, value);
    }

    public bool ShowCloud
    {
        get => GetValue(ShowCloudProperty);
        set => SetValue(ShowCloudProperty, value);
    }

    public int CloudShape
    {
        get => GetValue(CloudShapeProperty);
        set => SetValue(CloudShapeProperty, value);
    }

    public string? LastEvent
    {
        get => GetValue(LastEventProperty);
        set => SetValue(LastEventProperty, value);
    }

    public LightSource Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
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

    public Gas? CompareGas
    {
        get => GetValue(CompareGasProperty);
        set => SetValue(CompareGasProperty, value);
    }

    public double SelectedLine
    {
        get => GetValue(SelectedLineProperty);
        set => SetValue(SelectedLineProperty, value);
    }

    public bool Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double Kelvin
    {
        get => GetValue(KelvinProperty);
        set => SetValue(KelvinProperty, value);
    }

    public double Velocity
    {
        get => GetValue(VelocityProperty);
        set => SetValue(VelocityProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
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

    public SpectraDiagram()
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

    private void OnModeChanged() => _drag = DragKind.None;

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        if (Mode == SpectraMode.Atom) StepAtom(dt);
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
            case SpectraMode.Lamps:
                DrawLampsMode(dc, now);
                break;

            case SpectraMode.Stars:
                DrawStarsMode(dc, now);
                break;

            default:
                DrawAtomMode(dc, now);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
    }

    #region spectra as pictures

    /// <summary>
    /// A strip of spectrum, one column per pixel: <paramref name="light"/> gives each column's light (linear) for its
    /// wavelength. Drawn from a bitmap so even a thousand columns are one image.
    /// </summary>
    private void DrawStrip(PaintContext dc, Rect box, Func<double, double> wavelengthAt, Func<double, LinearRgb> light, ref WriteableBitmap? bitmap, ref byte[] pixels)
    {
        var columns = Math.Max(2, (int)box.Width);
        if (bitmap is null || bitmap.PixelSize.Width != columns)
        {
            bitmap = new WriteableBitmap(new PixelSize(columns, 1), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            pixels = new byte[columns * 4];
        }

        for (var i = 0; i < columns; i++)
        {
            var c = light(wavelengthAt(box.X + i + 0.5));
            pixels[(i * 4) + 0] = SkyOptics.Encode(c.B);
            pixels[(i * 4) + 1] = SkyOptics.Encode(c.G);
            pixels[(i * 4) + 2] = SkyOptics.Encode(c.R);
            pixels[(i * 4) + 3] = 255;
        }

        SpaceDrawing.WritePixels(bitmap, pixels, columns, 1);
        SpaceDrawing.DrawWhole(dc, bitmap, box);
    }

    /// <summary>Emission lines as light: each a narrow glow at least <paramref name="minWidth"/> nm wide.</summary>
    private static LinearRgb EmissionLight(IEnumerable<SpectralLine> lines, double nm, double width, double gain)
    {
        var total = default(LinearRgb);
        foreach (var line in lines)
        {
            var d = (nm - line.Nm) / width;
            if (Math.Abs(d) > 4) continue;
            total += SpectraModel.LineLight(line.Nm, gain * line.Strength * Math.Exp(-0.5 * d * d));
        }

        return total;
    }

    /// <summary>A colour to draw a line of light in: its own colour in the visible, lilac for ultraviolet, maroon for infrared.</summary>
    internal static Color PhotonColour(double nm) => nm switch
    {
        < SpectraModel.VisibleFrom => Color.FromRgb(0xC0, 0x9C, 0xFF),
        > SpectraModel.VisibleTo => Color.FromRgb(0xC8, 0x5A, 0x50),
        _ => SkyOptics.Hue(SpectraModel.SpectrumColour(nm)),
    };

    #endregion

    #region drawing helpers

    /// <summary>Already localized text on a dark pill, centred on <paramref name="center"/> and kept inside the control.</summary>
    private Rect DrawPill(PaintContext dc, string text, Point center, Color color, double size = 11.5)
    {
        var formatted = _labels.Text(text, size, color);
        // (kept clear of the plate's rounded corners)
        var x = Math.Clamp(center.X - (formatted.Width / 2) - 6, 8, Math.Max(8, ActualWidth - formatted.Width - 20));
        var y = Math.Clamp(center.Y - (formatted.Height / 2) - 2, 2, Math.Max(2, ActualHeight - formatted.Height - 6));
        _labels.DrawTextPill(dc, text, new Point(x, y), size, color);
        return new Rect(x, y, formatted.Width + 12, formatted.Height + 4);
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

    private readonly Dictionary<(Color Color, int Alpha, double Thickness, bool Dashed), IPen> _fadedPens = [];
    private readonly Dictionary<(Color Color, int Alpha), IBrush> _fadedBrushes = [];

    /// <summary>A frozen pen of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IPen FadedPen(Color color, double alpha, double thickness, bool dashed = false)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20), Math.Round(thickness, 1), dashed);
        if (_fadedPens.TryGetValue(key, out var pen)) return pen;
        var brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        pen = new ImmutablePen(((IBrush)brush).ToImmutable(), key.Item3, dashed ? new ImmutableDashStyle([4, 3], 0) : null, PenLineCap.Round, PenLineJoin.Round);
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

    /// <summary>A rounded frame for a panel inside the picture.</summary>
    private static void DrawFrame(PaintContext dc, Rect box) => dc.DrawRoundedRectangle(Ink.Panel, Ink.Frame, box, 12, 12);

    /// <summary>A wavelength: 656.3 nm, 1875 nm.</summary>
    internal static string Nanometres(double nm) => LocFormat("Spec_Value_Nm", Number(nm, nm < 1000 ? "0.0" : "0"));

    internal static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

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
        public static readonly Color Violet = Color.FromRgb(0xB0, 0x8C, 0xFF);
        public static readonly Color Glass = Color.FromRgb(0x9F, 0xE3, 0xFF);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x12, 0x17, 0x26)), new ImmutableGradientStop(1, Color.FromRgb(0x05, 0x07, 0x0E))],
            center: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.85, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.85, RelativeUnit.Relative));

        public static readonly IBrush Panel = Solid(150, 8, 12, 22);
        public static readonly IPen Frame = MakePen(Solid(46, 255, 255, 255), 1);
        public static readonly IPen Axis = MakePen(Solid(80, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Faint = MakePen(Solid(40, 255, 255, 255), 1);
        public static readonly IPen Tick = MakePen(Solid(110, 255, 255, 255), 1);
    }
}
