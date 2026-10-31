using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Lemon.Book.Shell.Infrastructures.Localization;
using Lemon.Explore.Book.Controls.Space;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.PeriodicTable;

/// <summary>
/// The side panel's chart, one per view. Build: every stable nucleus from hydrogen to krypton, neutrons against protons,
/// drifting above the N = Z line as they get heavier, with the atom being built marked (click a dot to build it).
/// Table: the outer electrons of all 118 elements as bars, climbing to eight and starting again each period, with the
/// picked element marked (click a bar to pick it).
/// </summary>
public sealed class AtomChart : Control
{
    static AtomChart()
    {
        AffectsRender<AtomChart>(ModeProperty, ProtonsProperty, NeutronsProperty, ElementZProperty);
    }

    public static readonly StyledProperty<AtomMode> ModeProperty =
        AvaloniaProperty.Register<AtomChart, AtomMode>(nameof(Mode), AtomMode.Build);

    public static readonly StyledProperty<int> ProtonsProperty =
        AvaloniaProperty.Register<AtomChart, int>(nameof(Protons), 1);

    public static readonly StyledProperty<int> NeutronsProperty =
        AvaloniaProperty.Register<AtomChart, int>(nameof(Neutrons), 0);

    public static readonly StyledProperty<int> ElementZProperty =
        AvaloniaProperty.Register<AtomChart, int>(nameof(ElementZ), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Build: run with the <see cref="Nucleus"/> of a clicked dot.</summary>
    public static readonly StyledProperty<ICommand?> PickNucleusCommandProperty =
        AvaloniaProperty.Register<AtomChart, ICommand?>(nameof(PickNucleusCommand));

    /// <summary>Run when the viewer clicks (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<AtomChart, ICommand?>(nameof(InteractionCommand));

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

    public int ElementZ
    {
        get => GetValue(ElementZProperty);
        set => SetValue(ElementZProperty, value);
    }

    public ICommand? PickNucleusCommand
    {
        get => GetValue(PickNucleusCommandProperty);
        set => SetValue(PickNucleusCommandProperty, value);
    }

    public ICommand? InteractionCommand
    {
        get => GetValue(InteractionCommandProperty);
        set => SetValue(InteractionCommandProperty, value);
    }

    private readonly LabelCache _labels;

    public AtomChart()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
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

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    /// <summary>The plotting area inside the axes' labels.</summary>
    private Rect Plot() => new(34, 12, Math.Max(40, Bounds.Width - 46), Math.Max(40, Bounds.Height - 40));

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width < 1 || Bounds.Height < 1) return;
        using var dc = new PaintContext(context);
        if (Mode == AtomMode.Table) DrawOuter(dc);
        else DrawNuclei(dc);
    }

    #region build: stable nuclei

    private const int MaxZ = AtomModel.MaxProtons;
    private const int MaxN = AtomModel.MaxNeutrons;

    private static Point NucleusPoint(Rect plot, double z, double n) =>
        new(plot.Left + (z / MaxZ * plot.Width), plot.Bottom - (n / MaxN * plot.Height));

    private void DrawNuclei(PaintContext dc)
    {
        var plot = Plot();
        for (var z = 0; z <= MaxZ; z += 6)
        {
            var x = NucleusPoint(plot, z, 0).X;
            dc.DrawLine(Grid, new Point(x, plot.Top), new Point(x, plot.Bottom));
            Label(dc, Number(z), new Point(x, plot.Bottom + 9), Dim, center: true);
        }

        for (var n = 0; n <= MaxN; n += 10)
        {
            var y = NucleusPoint(plot, 0, n).Y;
            dc.DrawLine(Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            Label(dc, Number(n), new Point(plot.Left - 6, y), Dim, right: true);
        }

        Label(dc, Loc("Atom_Chart_Z"), new Point(plot.Center.X, plot.Bottom + 22), Dim, center: true);
        Label(dc, Loc("Atom_Chart_N"), new Point(plot.Left + 4, plot.Top + 6), Dim);

        // N = Z: light nuclei sit on it, heavy ones above it.
        dc.DrawLine(EqualPen, NucleusPoint(plot, 0, 0), NucleusPoint(plot, MaxZ, MaxZ));
        Label(dc, Loc("Atom_Chart_Equal"), NucleusPoint(plot, MaxZ * 0.86, MaxZ * 0.86) + new Vector(4, 10), Dim);

        foreach (var (z, neutrons) in ElementData.StableNeutrons)
        {
            foreach (var n in neutrons)
            {
                dc.DrawEllipse(StableBrush, null, NucleusPoint(plot, z, n), 2.6, 2.6);
            }
        }

        if (Protons is < 1 or > MaxZ) return;
        var at = NucleusPoint(plot, Protons, Math.Min(Neutrons, MaxN));
        var stable = AtomModel.IsStable(Protons, Neutrons);
        dc.DrawEllipse(null, MakePen(Solid(stable ? Green : Orange), 2), at, 6.5, 6.5);
        var element = ElementData.Of(Protons);
        var text = $"{element.Symbol}-{Protons + Neutrons}";
        Label(dc, text, at + new Vector(at.X > plot.Right - 50 ? -10 : 10, -10), stable ? Green : Orange, right: at.X > plot.Right - 50);
    }

    #endregion

    #region table: outer electrons

    private static double BarX(Rect plot, int z) => plot.Left + ((z - 0.5) / ElementData.Elements.Count * plot.Width);

    private void DrawOuter(PaintContext dc)
    {
        var plot = Plot();
        for (var outer = 0; outer <= 8; outer += 2)
        {
            var y = plot.Bottom - (outer / 8.0 * plot.Height);
            dc.DrawLine(Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            Label(dc, Number(outer), new Point(plot.Left - 6, y), Dim, right: true);
        }

        Label(dc, Loc("Atom_Chart_Outer"), new Point(plot.Left + 4, plot.Top + 6), Dim);
        Label(dc, Loc("Atom_Chart_Number"), new Point(plot.Center.X, plot.Bottom + 22), Dim, center: true);

        var width = Math.Max(1, (plot.Width / ElementData.Elements.Count) - 0.6);
        foreach (var element in ElementData.Elements)
        {
            var x = BarX(plot, element.Number);
            var top = plot.Bottom - (element.Outer / 8.0 * plot.Height);
            var color = AtomColours.Outer(element.Outer);
            dc.DrawRectangle(Solid(WithAlpha(color, element.Number == ElementZ ? 1 : 0.7)), null, new Rect(x - (width / 2), top, width, plot.Bottom - top));

            // Each period starts again at one: mark where with its first element's symbol.
            if (element.Category == ElementCategory.AlkaliMetal || element.Number == 1)
            {
                Label(dc, element.Symbol, new Point(x, plot.Bottom + 9), Dim, center: true);
            }
        }

        var picked = ElementData.Of(ElementZ);
        var px = BarX(plot, picked.Number);
        var py = plot.Bottom - (picked.Outer / 8.0 * plot.Height);
        dc.DrawLine(MakePen(Solid(255, 255, 255, 255), 1.2), new Point(px, plot.Top), new Point(px, plot.Bottom));
        var right = px > plot.Right - 60;
        Label(dc, $"{picked.Symbol}: {picked.Outer}", new Point(px + (right ? -5 : 5), Math.Max(plot.Top + 16, py - 8)), Colors.White, right: right);
    }

    #endregion

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(this);
        var plot = Plot();
        if (InteractionCommand is { } interaction && interaction.CanExecute(null)) interaction.Execute(null);
        e.Handled = true;

        if (Mode == AtomMode.Table)
        {
            var z = (int)Math.Floor((point.X - plot.Left) / plot.Width * ElementData.Elements.Count) + 1;
            SetCurrentValue(ElementZProperty, Math.Clamp(z, 1, ElementData.Elements.Count));
            return;
        }

        // The stable nucleus nearest the click, if it is close.
        Nucleus? nearest = null;
        var best = 12.0;
        foreach (var (z, neutrons) in ElementData.StableNeutrons)
        {
            foreach (var n in neutrons)
            {
                var distance = ((Vector)(NucleusPoint(plot, z, n) - point)).Length;
                if (distance >= best) continue;
                best = distance;
                nearest = new Nucleus(z, n);
            }
        }

        if (nearest is not null && PickNucleusCommand is { } command && command.CanExecute(nearest)) command.Execute(nearest);
    }

    private void Label(PaintContext dc, string text, Point at, Color color, bool center = false, bool right = false)
    {
        var formatted = _labels.Text(text, 10, color);
        var x = center ? at.X - (formatted.Width / 2) : right ? at.X - formatted.Width : at.X;
        dc.DrawText(formatted, new Point(x, at.Y - (formatted.Height / 2)));
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
    private static readonly Color Green = Color.FromRgb(0x7C, 0xFF, 0x9E);
    private static readonly Color Orange = Color.FromRgb(0xFF, 0x9A, 0x4A);
    private static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
    private static readonly IPen EqualPen = MakePen(Solid(90, 255, 255, 255), 1, dashed: true);
    private static readonly IBrush StableBrush = Solid(220, 0x7C, 0xFF, 0x9E);
}
