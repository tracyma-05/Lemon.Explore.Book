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
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.AcidsBases;

/// <summary>How the burette's tap is turned.</summary>
public enum TapState
{
    Closed,

    /// <summary>A drop at a time, about 0.4 mL a second.</summary>
    Drip,

    /// <summary>A thin stream, 2 mL a second.</summary>
    Stream,
}

/// <summary>
/// The animated diagram of the acids and bases page. Particles: a beaker of water, four bottles (strong and weak acid,
/// strong and weak base) and a jug of water to drip in, a pH meter, and a magnifier on the particles in the beaker
/// (<c>AcidBaseDiagram.Particles.cs</c>). Scale: the pH scale from 0 to 14 with everyday liquids on it, ten times more
/// H₃O⁺ at each step down, and a sample of the chosen one in a test tube under each indicator
/// (<c>AcidBaseDiagram.Scale.cs</c>). Neutralise: sodium hydroxide from a burette into a flask of acid, with its
/// indicator, its thermometer, the particles meeting, and the titration curve drawn as it goes
/// (<c>AcidBaseDiagram.Neutralize.cs</c>).
/// </summary>
/// <remarks>
/// The chemistry is <see cref="AcidBaseModel"/>'s; the page's state (what is in the beaker, the substance, the base
/// added) belongs to the view model, and the diagram only animates it: drops falling from a clicked bottle are given to
/// <see cref="DropCommand"/> when they land, and an open tap runs <see cref="BaseMl"/> up. The mouse and the clickable
/// things are in <c>AcidBaseDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class AcidBaseDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<AcidBaseMode> ModeProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, AcidBaseMode>(nameof(Mode), AcidBaseMode.Particles);

    /// <summary>Particles: what is in the beaker.</summary>
    public static readonly StyledProperty<AcidBaseModel.Beaker> BeakerProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, AcidBaseModel.Beaker>(nameof(Beaker), AcidBaseModel.Beaker.Water());

    /// <summary>Particles: the indicator in the beaker's water (None for clear).</summary>
    public static readonly StyledProperty<Indicator> BeakerIndicatorProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, Indicator>(nameof(BeakerIndicator), Indicator.None);

    /// <summary>Particles: run with a <see cref="Reagent"/> when a drop lands in the beaker (Water: watered down ten times).</summary>
    public static readonly StyledProperty<ICommand?> DropCommandProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, ICommand?>(nameof(DropCommand));

    /// <summary>Scale: the liquid chosen (a <see cref="Substance.Id"/>).</summary>
    public static readonly StyledProperty<string?> SubstanceIdProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, string?>(nameof(SubstanceId), "Lemon", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Scale: how many times the chosen liquid has been watered down ten-fold.</summary>
    public static readonly StyledProperty<int> DilutionsProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, int>(nameof(Dilutions), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Scale: the indicator the scale and the big test tube are coloured with.</summary>
    public static readonly StyledProperty<Indicator> ScaleIndicatorProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, Indicator>(nameof(ScaleIndicator), Indicator.Universal, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Neutralise: the acid in the flask.</summary>
    public static readonly StyledProperty<TitrationAcid> AcidProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, TitrationAcid>(nameof(Acid), TitrationAcid.Strong);

    /// <summary>Neutralise: the indicator in the flask.</summary>
    public static readonly StyledProperty<Indicator> FlaskIndicatorProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, Indicator>(nameof(FlaskIndicator), Indicator.Phenolphthalein);

    /// <summary>Neutralise: the base run in from the burette, mL (0 to 50); an open tap runs it up.</summary>
    public static readonly StyledProperty<double> BaseMlProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, double>(nameof(BaseMl), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Neutralise: the burette's tap; it closes itself when the burette is empty.</summary>
    public static readonly StyledProperty<TapState> TapProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, TapState>(nameof(Tap), TapState.Closed, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Neutralise: the strength of the acid and of the base, mol/L.</summary>
    public static readonly StyledProperty<double> MolarProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, double>(nameof(Molar), AcidBaseModel.BottleMolar);

    /// <summary>Neutralise: the other acid's curve drawn faintly for comparison.</summary>
    public static readonly StyledProperty<bool> ShowCompareProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, bool>(nameof(ShowCompare), false);

    /// <summary>Particles and neutralise: the magnifier on the particles.</summary>
    public static readonly StyledProperty<bool> ShowParticlesProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, bool>(nameof(ShowParticles), true);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<AcidBaseDiagram>(ModeProperty, BeakerProperty, BeakerIndicatorProperty, SubstanceIdProperty, DilutionsProperty, ScaleIndicatorProperty, AcidProperty, FlaskIndicatorProperty, BaseMlProperty, TapProperty, MolarProperty, ShowCompareProperty, ShowParticlesProperty, ShowLabelsProperty, ToolbarHeightProperty);
        return true;
    }


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

    public Indicator BeakerIndicator
    {
        get => GetValue(BeakerIndicatorProperty);
        set => SetValue(BeakerIndicatorProperty, value);
    }

    public ICommand? DropCommand
    {
        get => GetValue(DropCommandProperty);
        set => SetValue(DropCommandProperty, value);
    }

    public string? SubstanceId
    {
        get => GetValue(SubstanceIdProperty);
        set => SetValue(SubstanceIdProperty, value);
    }

    public int Dilutions
    {
        get => GetValue(DilutionsProperty);
        set => SetValue(DilutionsProperty, value);
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

    public Indicator FlaskIndicator
    {
        get => GetValue(FlaskIndicatorProperty);
        set => SetValue(FlaskIndicatorProperty, value);
    }

    public double BaseMl
    {
        get => GetValue(BaseMlProperty);
        set => SetValue(BaseMlProperty, value);
    }

    public TapState Tap
    {
        get => GetValue(TapProperty);
        set => SetValue(TapProperty, value);
    }

    public double Molar
    {
        get => GetValue(MolarProperty);
        set => SetValue(MolarProperty, value);
    }

    public bool ShowCompare
    {
        get => GetValue(ShowCompareProperty);
        set => SetValue(ShowCompareProperty, value);
    }

    public bool ShowParticles
    {
        get => GetValue(ShowParticlesProperty);
        set => SetValue(ShowParticlesProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
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

    /// <summary>Seconds since the page started, for bubbles, swirls and drops that move whatever the state.</summary>
    private double _drift;

    public AcidBaseDiagram()
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
        _drops.Clear();
        _pour = 0;
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        _drift += dt;
        switch (Mode)
        {
            case AcidBaseMode.Particles:
                StepParticles(dt);
                break;

            case AcidBaseMode.Neutralize:
                StepNeutralize(dt);
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
        return new Rect(16, top, Math.Max(100, ActualWidth - 32), Math.Max(200, ActualHeight - top - bottom));
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        _targets.Clear();
        var now = _clock.Elapsed.TotalSeconds;
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        switch (Mode)
        {
            case AcidBaseMode.Scale:
                DrawScaleMode(dc);
                break;

            case AcidBaseMode.Neutralize:
                DrawNeutralizeMode(dc, now);
                break;

            default:
                DrawParticlesMode(dc, now);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
    }

    #region shared pictures

    /// <summary>
    /// A magnifier: a dark lens on the particles of <paramref name="box"/>, its rim, and a line to the spot it looks at.
    /// Registered as the target <paramref name="id"/>.
    /// </summary>
    private void DrawMagnifier(PaintContext dc, ParticleBox box, Point center, double radius, Point? lookingAt, string id, double now)
    {
        if (lookingAt is { } spot)
        {
            Vector toward = spot - center;
            if (toward.Length > radius + 6)
            {
                toward = toward.Normalize();
                dc.DrawLine(Ink.Faint, center + (toward * radius), spot);
                dc.DrawEllipse(null, Ink.Faint, spot, 7, 7);
            }
        }

        dc.DrawEllipse(Ink.Lens, null, center, radius, radius);
        dc.PushClip(SpaceDrawing.Ellipse(center, radius, radius));
        box.Draw(dc, center, radius);
        dc.Pop();
        dc.DrawEllipse(null, Glows(id) ? ExploreTargets.GlowPen(now) : Ink.LensRim, center, radius, radius);
        _targets.Add(id, center, radius);
    }

    /// <summary>The particles of a magnifier, by kind, in a row of small glyphs with their names under or beside the lens.</summary>
    private Rect DrawLegend(PaintContext dc, IEnumerable<ParticleKind> kinds, Point topLeft, double maxWidth)
    {
        var x = topLeft.X;
        var y = topLeft.Y;
        var left = x;
        foreach (var kind in kinds)
        {
            var name = Loc(ParticleNameKey(kind));
            var text = _labels.Text(name, 10.5, ParticleBox.ColorOf(kind));
            var width = 22 + text.Width + 12;
            if (x + width > left + maxWidth && x > left)
            {
                x = left;
                y += 22;
            }

            ParticleBox.DrawGlyph(dc, kind, new Point(x + 9, y + 9), 4.2, -0.4);
            dc.DrawText(text, new Point(x + 22, y + 9 - (text.Height / 2)));
            AddBoxTarget(ParticleTargetId(kind), new Rect(x, y, width - 8, 18));
            x += width;
        }

        return new Rect(left, topLeft.Y, maxWidth, y + 18 - topLeft.Y);
    }

    /// <summary>The resource key of a particle kind's name: <c>Acid_Particle_Hydronium</c>.</summary>
    internal static string ParticleNameKey(ParticleKind kind) => kind switch
    {
        ParticleKind.Hydronium => "Acid_Particle_Hydronium",
        ParticleKind.Hydroxide => "Acid_Particle_Hydroxide",
        ParticleKind.Chloride => "Acid_Particle_Chloride",
        ParticleKind.Sodium => "Acid_Particle_Sodium",
        ParticleKind.AcidMolecule => "Acid_Particle_AcidMolecule",
        ParticleKind.AcidIon => "Acid_Particle_AcidIon",
        ParticleKind.BaseMolecule => "Acid_Particle_BaseMolecule",
        ParticleKind.BaseIon => "Acid_Particle_BaseIon",
        _ => "Acid_Particle_Water",
    };

    /// <summary>The kinds present in a solution (more than half a drawn particle), commonest first, water last.</summary>
    private static List<ParticleKind> KindsIn(Species s, double unit)
    {
        var amounts = new (ParticleKind Kind, double Amount)[]
        {
            (ParticleKind.Hydronium, s.Hydronium), (ParticleKind.Hydroxide, s.Hydroxide), (ParticleKind.Chloride, s.Chloride),
            (ParticleKind.Sodium, s.Sodium), (ParticleKind.AcidMolecule, s.AcidMolecules), (ParticleKind.AcidIon, s.AcidIons),
            (ParticleKind.BaseMolecule, s.BaseMolecules), (ParticleKind.BaseIon, s.BaseIons),
        };
        var kinds = amounts.Where(a => a.Amount >= unit * 0.5).OrderByDescending(a => a.Amount).Select(a => a.Kind).ToList();
        kinds.Add(ParticleKind.Water);
        return kinds;
    }

    /// <summary>A pH strip from 0 to 14 in the colours of <paramref name="indicator"/>, with its numbers under it.</summary>
    private void DrawPhStrip(PaintContext dc, Rect strip, Indicator indicator, bool numbers = true, bool everyNumber = true)
    {
        var brush = StripBrush(indicator);
        dc.DrawRoundedRectangle(brush, Ink.Frame, strip, 4, 4);
        if (!numbers) return;
        for (var p = 0; p <= 14; p++)
        {
            if (!everyNumber && p % 2 == 1) continue;
            var x = PhX(strip, p);
            dc.DrawLine(Ink.Faint, new Point(x, strip.Bottom), new Point(x, strip.Bottom + 4));
            DrawText(dc, Number(p, "0"), new Point(x, strip.Bottom + 12), p == 7 ? Ink.Green : Ink.Dim, 10.5, TextAnchor.Center);
        }
    }

    private static double PhX(Rect strip, double ph) => strip.Left + (Math.Clamp(ph, 0, 14) / 14 * strip.Width);

    private static double XPh(Rect strip, double x) => Math.Clamp((x - strip.Left) / strip.Width * 14, 0, 14);

    private readonly Dictionary<Indicator, IBrush> _stripBrushes = [];

    /// <summary>A left-to-right gradient of the indicator's colours (a dark backing shows through where it has none).</summary>
    private IBrush StripBrush(Indicator indicator)
    {
        if (_stripBrushes.TryGetValue(indicator, out var brush)) return brush;
        var stops = new List<(Color, double)>();
        for (var p = 0.0; p <= 14.001; p += 0.25)
        {
            var c = AcidBaseModel.LiquidColor(indicator, p, 60);
            stops.Add((c, p / 14));
        }

        brush = SpaceDrawing.LinearGradient(0, [.. stops]);
        _stripBrushes[indicator] = brush;
        return brush;
    }

    /// <summary>A pointer under or over a pH strip at <paramref name="ph"/> with the value on a pill.</summary>
    private void DrawPhPointer(PaintContext dc, Rect strip, double ph, string id, bool above = true)
    {
        var x = PhX(strip, ph);
        var tip = above ? strip.Top - 2 : strip.Bottom + 2;
        var dir = above ? -1 : 1;
        dc.DrawGeometry(Brushes.White, null, Polygon(new Point(x, tip), new Point(x - 6, tip + (dir * 9)), new Point(x + 6, tip + (dir * 9))));
        dc.DrawLine(FadedPen(Ink.Text, 0.85, 1.5), new Point(x, strip.Top), new Point(x, strip.Bottom));
        DrawTargetPill(dc, LocFormat("Acid_Value_Ph", Number(ph, "0.0")), new Point(x, tip + (dir * 22)), Ink.Text, id, 12);
    }

    /// <summary>A glass vessel's outline and shine, from a closed outline geometry.</summary>
    private static void DrawGlass(PaintContext dc, Geometry outline)
    {
        dc.DrawGeometry(Ink.GlassFill, Ink.GlassPen, outline);
    }

    #endregion

    #region drawing helpers

    private static Rect Inflated(Rect rect, double x, double y)
    {
        rect = rect.Inflate(new Thickness(x, y));
        return rect;
    }

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

    private readonly Dictionary<(Color Color, int Alpha, double Thickness, bool Dashed, bool Flat), IPen> _fadedPens = [];
    private readonly Dictionary<(Color Color, int Alpha), IBrush> _fadedBrushes = [];

    /// <summary>A frozen pen of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IPen FadedPen(Color color, double alpha, double thickness, bool dashed = false, bool flat = false)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20), Math.Round(thickness, 1), dashed, flat);
        if (_fadedPens.TryGetValue(key, out var pen)) return pen;
        var brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        var cap = flat ? PenLineCap.Flat : PenLineCap.Round;
        pen = new ImmutablePen(((IBrush)brush).ToImmutable(), key.Item3, dashed ? new ImmutableDashStyle([4, 3], 0) : null, cap, PenLineJoin.Round);
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

    private readonly Dictionary<Color, IBrush> _liquidBrushes = [];

    /// <summary>A liquid of <paramref name="color"/> (alpha included), a little darker at the bottom; kept per colour.</summary>
    private IBrush LiquidBrush(Color color)
    {
        if (_liquidBrushes.TryGetValue(color, out var brush)) return brush;
        var deep = Color.FromArgb((byte)Math.Min(255, color.A + 40), (byte)(color.R * 0.78), (byte)(color.G * 0.78), (byte)(color.B * 0.78));
        brush = SpaceDrawing.LinearGradient(color, deep, 90);
        if (_liquidBrushes.Count > 400) _liquidBrushes.Clear();
        _liquidBrushes[color] = brush;
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

    /// <summary>An arrow from <paramref name="from"/> to <paramref name="to"/> with its head at the end.</summary>
    private void DrawArrow(PaintContext dc, Point from, Point to, Color color, double thickness = 2.5, double alpha = 1, bool dashed = false)
    {
        Vector v = to - from;
        if (v.Length < 2) return;
        var direction = v / v.Length;
        var head = Math.Min(Math.Max(7, thickness * 3.4), v.Length * 0.6);
        dc.DrawLine(FadedPen(color, alpha, thickness, dashed), from, to - (direction * head * 0.7));
        DrawArrowHead(dc, FadedBrush(color, alpha), to, direction, head);
    }

    /// <summary>A value in scientific notation: "1.0 × 10⁻⁷".</summary>
    internal static string Scientific(double value)
    {
        if (value <= 0) return "0";
        var exponent = (int)Math.Floor(Math.Log10(value) + 1e-9);
        var mantissa = value / Math.Pow(10, exponent);
        if (mantissa >= 9.95)
        {
            mantissa /= 10;
            exponent++;
        }

        return exponent == 0 ? Number(mantissa, "0.0") : Number(mantissa, "0.0") + " × 10" + Superscript(exponent);
    }

    private static string Superscript(int value)
    {
        const string digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var text = Math.Abs(value).ToString(CultureInfo.InvariantCulture);
        return (value < 0 ? "⁻" : string.Empty) + string.Concat(text.Select(c => digits[c - '0']));
    }

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
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xA8, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xB0, 0x8C, 0xFF);
        public static readonly Color Steel = Color.FromRgb(0xC9, 0xD2, 0xDE);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x10, 0x14, 0x22)), new ImmutableGradientStop(1, Color.FromRgb(0x04, 0x05, 0x0A))],
            center: new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.9, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.9, RelativeUnit.Relative));

        public static readonly IBrush Panel = Solid(185, 8, 11, 20);
        public static readonly IBrush PlotBack = Solid(150, 6, 8, 16);
        public static readonly IPen Frame = MakePen(Solid(46, 255, 255, 255), 1);
        public static readonly IPen Faint = MakePen(Solid(60, 255, 255, 255), 1);
        public static readonly IPen Grid = MakePen(Solid(22, 255, 255, 255), 1);
        public static readonly IBrush Lens = Solid(255, 0x07, 0x0B, 0x16);
        public static readonly IPen LensRim = MakePen(Solid(150, 0xC9, 0xD2, 0xDE), 2.5);
        public static readonly IBrush GlassFill = Solid(22, 0xC8, 0xE4, 0xFF);
        public static readonly IPen GlassPen = MakePen(Solid(170, 0xD8, 0xE6, 0xF4), 2);
        public static readonly IPen GlassThin = MakePen(Solid(110, 0xD8, 0xE6, 0xF4), 1);
        public static readonly IBrush Shine = Solid(34, 255, 255, 255);
        public static readonly IBrush Wood = SpaceDrawing.LinearGradient(Color.FromRgb(0x6A, 0x4E, 0x36), Color.FromRgb(0x3E, 0x2C, 0x1E), 90);
        public static readonly IBrush Metal = SpaceDrawing.LinearGradient(Color.FromRgb(0x9C, 0xA6, 0xB4), Color.FromRgb(0x4A, 0x52, 0x5E), 0);
        public static readonly IBrush MeterBody = SpaceDrawing.LinearGradient(Color.FromRgb(0x2C, 0x34, 0x44), Color.FromRgb(0x16, 0x1A, 0x24), 90);
        public static readonly IBrush Screen = Solid(255, 0x0E, 0x1E, 0x16);
        public static readonly IBrush Bench = SpaceDrawing.LinearGradient(Color.FromArgb(200, 0x2A, 0x30, 0x3C), Color.FromArgb(0, 0x2A, 0x30, 0x3C), 90);
    }
}
