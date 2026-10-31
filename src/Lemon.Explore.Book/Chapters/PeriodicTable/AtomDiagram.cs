using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Book.Shell.Infrastructures.Localization;
using Lemon.Book.Shell.Infrastructures.Timing;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.PeriodicTable;

/// <summary>
/// The animated diagram of the atoms page. Build: an atom's nucleus of protons and neutrons, its electrons going round
/// in shells, the element they make on a tile, and a tray of each particle to add or take away
/// (<c>AtomDiagram.Build.cs</c>). Table: the 118 elements in their table, coloured by family, state or outer
/// electrons, with the picked one's tile and shells in the gap at the top (<c>AtomDiagram.Table.cs</c>).
/// </summary>
/// <remarks>
/// The atom and the element picked belong to the view model; the diagram only draws and animates them, and changes
/// them through its two-way properties when a tray's button or a cell is clicked. The mouse and the clickable things
/// are in <c>AtomDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class AtomDiagram : Control
{
    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<AtomMode> ModeProperty =
        AvaloniaProperty.Register<AtomDiagram, AtomMode>(nameof(Mode), AtomMode.Build);

    public static readonly StyledProperty<int> ProtonsProperty =
        AvaloniaProperty.Register<AtomDiagram, int>(nameof(Protons), 1, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> NeutronsProperty =
        AvaloniaProperty.Register<AtomDiagram, int>(nameof(Neutrons), 0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> ElectronsProperty =
        AvaloniaProperty.Register<AtomDiagram, int>(nameof(Electrons), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Table: the element picked, by atomic number.</summary>
    public static readonly StyledProperty<int> ElementZProperty =
        AvaloniaProperty.Register<AtomDiagram, int>(nameof(ElementZ), 1, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<TableColouring> ColouringProperty =
        AvaloniaProperty.Register<AtomDiagram, TableColouring>(nameof(Colouring), TableColouring.Category);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<AtomDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<AtomDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<AtomDiagram>(ModeProperty, ProtonsProperty, NeutronsProperty, ElectronsProperty, ElementZProperty, ColouringProperty,
            ShowLabelsProperty, ToolbarHeightProperty);
        return true;
    }

    public AtomMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public int Protons
    {
        get => GetValue(ProtonsProperty);
        set => SetValue(ProtonsProperty, value);
    }

    public int Neutrons
    {
        get => GetValue(NeutronsProperty);
        set => SetValue(NeutronsProperty, value);
    }

    public int Electrons
    {
        get => GetValue(ElectronsProperty);
        set => SetValue(ElectronsProperty, value);
    }

    public int ElementZ
    {
        get => GetValue(ElementZProperty);
        set => SetValue(ElementZProperty, value);
    }

    public TableColouring Colouring
    {
        get => GetValue(ColouringProperty);
        set => SetValue(ColouringProperty, value);
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

    private readonly BookStopwatch _clock = new();
    private readonly LabelCache _labels;
    private double _lastSeconds;

    /// <summary>Seconds the diagram has been animating, for the electrons going round.</summary>
    private double _drift;

    public AtomDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            _flights.Clear();
        }
        else if (change.Property == ProtonsProperty || change.Property == NeutronsProperty || change.Property == ElectronsProperty)
        {
            OnParticleCountChanged(change);
        }
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

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        _drift += dt;
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
        if (Mode == AtomMode.Table) DrawTableMode(dc, now);
        else DrawBuildMode(dc, now);

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
        if (Mode == AtomMode.Table) DrawCellHover(dc);
    }

    #region shared pictures

    private IBrush? _protonBrush;
    private IBrush? _neutronBrush;
    private IBrush? _electronBrush;

    /// <summary>A proton, a neutron or an electron as a small shaded ball; a proton carries a "+" when big enough to read.</summary>
    private void DrawParticle(PaintContext dc, Particle kind, Point at, double radius)
    {
        var brush = kind switch
        {
            Particle.Proton => _protonBrush ??= RadialGradient((Color.FromRgb(0xFF, 0x9A, 0x8C), 0), (Ink.Proton, 0.55), (Color.FromRgb(0x9E, 0x24, 0x2C), 1)),
            Particle.Neutron => _neutronBrush ??= RadialGradient((Color.FromRgb(0xE4, 0xEA, 0xF2), 0), (Ink.Neutron, 0.55), (Color.FromRgb(0x4E, 0x58, 0x66), 1)),
            _ => _electronBrush ??= RadialGradient((Color.FromRgb(0xE8, 0xFB, 0xFF), 0), (Ink.Electron, 0.55), (Color.FromRgb(0x1E, 0x6E, 0x96), 1)),
        };
        dc.DrawEllipse(brush, Ink.ParticleRim, at, radius, radius);
        if (radius < 6) return;
        var mark = kind switch
        {
            Particle.Proton => "+",
            Particle.Electron => "−",
            _ => null,
        };
        if (mark is not null) DrawText(dc, mark, at, Colors.White, radius * 1.3, TextAnchor.Center, bold: true);
    }

    /// <summary>
    /// An element's tile: its atomic number at the top left, its symbol big in the middle (with the mass number and the
    /// charge beside it when given), its name under it.
    /// </summary>
    private void DrawTile(PaintContext dc, Rect tile, Element? element, Color color, int? massNumber = null, int charge = 0, bool glow = false, double now = 0)
    {
        dc.DrawRoundedRectangle(FadedBrush(color, 0.26), FadedPen(color, 0.9, 2), tile, 10, 10);
        if (glow) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(now), tile.Inflate(3), 12, 12);
        var size = tile.Height;
        if (element is null)
        {
            DrawText(dc, "?", new Point(tile.Center.X, tile.Center.Y - (size * 0.08)), Ink.Dim, size * 0.42, TextAnchor.Center, bold: true);
            DrawWrapped(dc, Loc("Atom_Label_NoElement"), new Point(tile.Center.X, tile.Bottom - (size * 0.16)), Ink.Dim, Math.Max(9, size * 0.09), tile.Width - 10);
            return;
        }

        DrawText(dc, Number(element.Number), new Point(tile.Left + (size * 0.08), tile.Top + (size * 0.13)), Ink.Text, Math.Max(9, size * 0.12));

        // The symbol with its mass number up on the left and its charge up on the right, centred as one.
        var small = Math.Max(8.5, size * 0.12);
        var mass = massNumber is { } a ? _labels.Text(Number(a), small, Ink.Gold) : null;
        var suffix = charge != 0 ? _labels.Text(AtomModel.ChargeSuffix(charge), small * 1.15, charge > 0 ? Ink.Red : Ink.Violet, bold: true) : null;
        var symbolText = _labels.Text(element.Symbol, size * 0.36, Colors.White, bold: true);
        var width = (mass?.Width + 2 ?? 0) + symbolText.Width + (suffix?.Width + 2 ?? 0);
        var left = tile.Center.X - (width / 2);
        var symbolTop = tile.Center.Y - (symbolText.Height / 2) + (mass is null ? -size * 0.06 : size * 0.01);
        var symbolLeft = left + (mass?.Width + 2 ?? 0);
        dc.DrawText(symbolText, new Point(symbolLeft, symbolTop));
        var raised = symbolTop + (symbolText.Height * 0.2);
        if (mass is not null) dc.DrawText(mass, new Point(left, raised));
        if (suffix is not null) dc.DrawText(suffix, new Point(symbolLeft + symbolText.Width + 2, raised - 2));
        DrawWrapped(dc, Loc(element.NameKey), new Point(tile.Center.X, tile.Bottom - (size * 0.15)), Ink.Text, Math.Max(9, size * 0.1), tile.Width - 8);
    }

    /// <summary>
    /// Electron shells drawn small: a red dot of a nucleus and up to seven rings with their electrons, turning slowly.
    /// Used for the table's picked element.
    /// </summary>
    private void DrawMiniShells(PaintContext dc, Point center, double radius, IReadOnlyList<int> shells, double now)
    {
        var count = Math.Max(1, shells.Count);
        dc.DrawEllipse(FadedBrush(Ink.Proton, 1), null, center, Math.Max(2.5, radius * 0.08), Math.Max(2.5, radius * 0.08));
        for (var k = 0; k < shells.Count; k++)
        {
            var r = radius * (0.22 + (0.78 * (k + 1) / count));
            dc.DrawEllipse(null, Ink.Shell, center, r, r);
            var n = shells[k];
            var dot = Math.Clamp(Math.Min(r * Math.PI / Math.Max(n, 1) * 0.32, radius * 0.05), 1.3, 3.6);
            var phase = (_drift * 0.5 / (k + 1)) + (k * 0.7);
            for (var i = 0; i < n; i++)
            {
                var angle = phase + (2 * Math.PI * i / n);
                dc.DrawEllipse(FadedBrush(Ink.Electron, 1), null, new Point(center.X + (r * Math.Cos(angle)), center.Y + (r * Math.Sin(angle))), dot, dot);
            }
        }
    }

    #endregion

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

    /// <summary>Centred text, made smaller until it fits <paramref name="maxWidth"/> (down to two thirds of its size).</summary>
    private Rect DrawWrapped(PaintContext dc, string text, Point center, Color color, double size, double maxWidth)
    {
        var fitted = size;
        while (fitted > size * 0.67 && _labels.Text(text, fitted, color).Width > maxWidth) fitted -= 0.5;
        return DrawText(dc, text, center, color, fitted, TextAnchor.Center);
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
        pen = new ImmutablePen(brush, key.Item3, dashed ? new ImmutableDashStyle([4, 3], 0) : null, PenLineCap.Round, PenLineJoin.Round);
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

    internal static string Number(double value) => value.ToString("0", CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    #endregion

    /// <summary>The three particles an atom is made of.</summary>
    internal enum Particle
    {
        Proton,
        Neutron,
        Electron,
    }

    internal static class Ink
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Green = Color.FromRgb(0x7C, 0xFF, 0x9E);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9A, 0x4A);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x7A, 0x7F);
        public static readonly Color Violet = Color.FromRgb(0xB0, 0x8C, 0xFF);
        public static readonly Color Proton = Color.FromRgb(0xF0, 0x4E, 0x52);
        public static readonly Color Neutron = Color.FromRgb(0x9C, 0xA8, 0xB8);
        public static readonly Color Electron = Color.FromRgb(0x6F, 0xD8, 0xFF);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x10, 0x14, 0x22)), new ImmutableGradientStop(1, Color.FromRgb(0x04, 0x05, 0x0A))],
            center: new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.9, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.9, RelativeUnit.Relative));

        public static readonly IBrush Panel = Solid(185, 8, 11, 20);
        public static readonly IPen Frame = MakePen(Solid(46, 255, 255, 255), 1);
        public static readonly IPen Faint = MakePen(Solid(60, 255, 255, 255), 1);
        public static readonly IPen Shell = MakePen(Solid(70, 0x6F, 0xD8, 0xFF), 1.2);
        public static readonly IPen ShellNext = MakePen(Solid(45, 0x6F, 0xD8, 0xFF), 1, dashed: true);
        public static readonly IPen ParticleRim = MakePen(Solid(90, 0, 0, 0), 0.8);
        public static readonly IPen Selected = MakePen(Solid(255, 255, 255, 255), 2.2);
        public static readonly IBrush NucleusGlow = RadialGradient((Color.FromArgb(70, 0xFF, 0x9A, 0x6B), 0), (Color.FromArgb(0, 0xFF, 0x9A, 0x6B), 1));
    }
}

/// <summary>The colours the table and its chart share: by family, by state, by outer electrons.</summary>
internal static class AtomColours
{
    public static Color Of(Element element, TableColouring colouring) => colouring switch
    {
        TableColouring.State => State(element.State),
        TableColouring.Outer => Outer(element.Outer),
        _ => Category(element.Category),
    };

    public static Color Category(ElementCategory category) => category switch
    {
        ElementCategory.AlkaliMetal => Color.FromRgb(0xFF, 0x6B, 0x6B),
        ElementCategory.AlkalineEarth => Color.FromRgb(0xFF, 0xA9, 0x4D),
        ElementCategory.Transition => Color.FromRgb(0xFF, 0xD4, 0x3B),
        ElementCategory.PostTransition => Color.FromRgb(0x8C, 0xE9, 0x9A),
        ElementCategory.Metalloid => Color.FromRgb(0x38, 0xD9, 0xA9),
        ElementCategory.Nonmetal => Color.FromRgb(0x4D, 0xAB, 0xF7),
        ElementCategory.Halogen => Color.FromRgb(0x74, 0xC0, 0xFC),
        ElementCategory.NobleGas => Color.FromRgb(0xB1, 0x97, 0xFC),
        ElementCategory.Lanthanide => Color.FromRgb(0xF7, 0x83, 0xAC),
        _ => Color.FromRgb(0xE5, 0x99, 0xF7),
    };

    public static Color State(ElementState state) => state switch
    {
        ElementState.Gas => Color.FromRgb(0xFF, 0xD3, 0x6B),
        ElementState.Liquid => Color.FromRgb(0x4D, 0xAB, 0xF7),
        ElementState.Unknown => Color.FromRgb(0x6B, 0x73, 0x80),
        _ => Color.FromRgb(0xC9, 0xD2, 0xDE),
    };

    /// <summary>One to eight outer electrons, red through to violet; grey for none (palladium).</summary>
    public static Color Outer(int outer) => Math.Clamp(outer, 0, 8) switch
    {
        0 => Color.FromRgb(0x6B, 0x73, 0x80),
        1 => Color.FromRgb(0xFF, 0x6B, 0x6B),
        2 => Color.FromRgb(0xFF, 0xA9, 0x4D),
        3 => Color.FromRgb(0xFF, 0xD4, 0x3B),
        4 => Color.FromRgb(0xA9, 0xE3, 0x4B),
        5 => Color.FromRgb(0x38, 0xD9, 0xA9),
        6 => Color.FromRgb(0x4D, 0xAB, 0xF7),
        7 => Color.FromRgb(0x91, 0xA7, 0xFF),
        _ => Color.FromRgb(0xD0, 0x8C, 0xFF),
    };
}
