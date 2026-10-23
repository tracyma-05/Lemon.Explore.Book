using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.AcidsBases;

/// <summary>
/// The things in the acids and bases diagram that can be clicked for an explanation, the walkthrough's highlight, and the
/// hands-on parts: pressing a bottle drips it into the beaker (held, drop after drop) and the jug waters it down; a
/// liquid's name picks it and a small tube picks its indicator; the tap turns, the flask takes one drop, and dragging
/// along the titration curve sets how much base has gone in.
/// </summary>
public sealed partial class AcidBaseDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<AcidBaseDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<AcidBaseDiagram>(TitleHeightProperty);
        return true;
    }

    public string? Highlight
    {
        get => GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    public string? SelectedObject
    {
        get => GetValue(SelectedObjectProperty);
        set => SetValue(SelectedObjectProperty, value);
    }

    public ICommand? InteractionCommand
    {
        get => GetValue(InteractionCommandProperty);
        set => SetValue(InteractionCommandProperty, value);
    }

    public bool IsLessonActive
    {
        get => GetValue(IsLessonActiveProperty);
        set => SetValue(IsLessonActiveProperty, value);
    }

    public double TitleHeight
    {
        get => GetValue(TitleHeightProperty);
        set => SetValue(TitleHeightProperty, value);
    }

    private readonly ExploreTargets _targets = new();
    private readonly LessonReserve _reserve = new();

    /// <summary>What the mouse is doing now.</summary>
    private enum DragKind
    {
        None,

        /// <summary>The button is down; moving far enough starts a drag, else it is a click.</summary>
        Pending,

        /// <summary>A bottle is held down and drips until the button comes up.</summary>
        Holding,

        /// <summary>Following the mouse along the titration curve: the base under it.</summary>
        Scrub,
    }

    private DragKind _drag;

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    private Point _dragStart;
    private bool _dragMoved;

    /// <summary>The thing a target id stands for (<c>sub_Lemon</c> → <c>Obj_Lemon</c>). It names the title and the narration clip.</summary>
    internal static string? ObjectName(string? id)
    {
        if (id is null) return null;
        if (id.StartsWith("sub_", StringComparison.Ordinal))
        {
            var substance = id[4..];
            return AcidBaseModel.Substances.Any(s => s.Id == substance) ? "Obj_" + substance : null;
        }

        return id switch
        {
            "beaker" => "Obj_Beaker",
            "meter" => "Obj_Meter",
            "phvalue" => "Obj_PhValue",
            "phstrip" => "Obj_PhStrip",
            "magnifier" => "Obj_Magnifier",
            "swap" => "Obj_Swap",
            "hcl" => "Obj_Hcl",
            "acetic" => "Obj_Acetic",
            "naoh" => "Obj_Naoh",
            "ammonia" => "Obj_AmmoniaBottle",
            "jug" => "Obj_Jug",
            "p_water" => "Obj_WaterMolecule",
            "p_hydronium" => "Obj_Hydronium",
            "p_hydroxide" => "Obj_Hydroxide",
            "p_chloride" => "Obj_Chloride",
            "p_sodium" => "Obj_Sodium",
            "p_acidmolecule" => "Obj_AcidMolecule",
            "p_acidion" => "Obj_AcidIon",
            "p_basemolecule" => "Obj_BaseMolecule",
            "p_baseion" => "Obj_BaseIon",
            "scale" => "Obj_Scale",
            "neutral" => "Obj_Neutral",
            "steps" => "Obj_Steps",
            "tube" => "Obj_Tube",
            "dilute" => "Obj_Dilute",
            "ind_universal" => "Obj_Universal",
            "ind_cabbage" => "Obj_Cabbage",
            "ind_litmus" => "Obj_Litmus",
            "ind_phenol" => "Obj_Phenol",
            "ind_methyl" => "Obj_Methyl",
            "ind_bromothymol" => "Obj_Bromothymol",
            "burette" => "Obj_Burette",
            "tap" => "Obj_Tap",
            "flask" => "Obj_Flask",
            "indicator" => "Obj_FlaskIndicator",
            "thermometer" => "Obj_Thermometer",
            "reaction" => "Obj_Reaction",
            "curve" => "Obj_Curve",
            "band" => "Obj_Band",
            "equivalence" => "Obj_Equivalence",
            "jump" => "Obj_Jump",
            "halfway" => "Obj_Halfway",
            _ => null,
        };
    }

    /// <summary>The resource key of a target's title: <c>Acid_Obj_Hydronium</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Acid_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Acid_" + name : null;

    /// <summary>The target id of a particle kind in the legend: <c>p_hydronium</c>.</summary>
    internal static string ParticleTargetId(ParticleKind kind) => "p_" + kind.ToString().ToLowerInvariant();

    /// <summary>Things the diagram glows itself; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "beaker" or "meter" or "magnifier" or "reaction" or "hcl" or "acetic" or "naoh"
        or "ammonia" or "jug" or "scale" or "steps" or "tube" or "burette" or "tap" or "flask" or "thermometer" or "curve" or "equivalence";

    /// <summary>Whether the walkthrough points at <paramref name="id"/>, which the diagram glows itself.</summary>
    private bool Glows(string id) => Highlight == id;

    /// <summary>A text, a pill or a part already drawn in <paramref name="box"/>, as a target.</summary>
    private void AddBoxTarget(string id, Rect box)
    {
        if (box == default) return;
        box = box.Inflate(new Thickness(2, 1));
        _targets.AddBox(id, box);
    }

    /// <summary>The title sits over the top of the picture: the picture starts below its last line (its top margin is 22), however it wraps.</summary>
    // The book puts the title on the left page: only a margin at the top, not the original's title band.
    private double TitleSpace => 22 + TitleHeight + 14;

    #region clicking and dragging

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragStart = e.GetPosition(this);
        _dragMoved = false;
        _pressed = _targets.Find(_dragStart)?.Id;
        e.Pointer.Capture(this);

        e.Handled = true;

        // A bottle drips at once and goes on dripping while held; the jug pours.
        if (Mode == AcidBaseMode.Particles && ReagentOf(_pressed) is { } reagent)
        {
            Interacted();
            StartDropping(reagent, hold: true);

            // Said the first time; more drops from the same bottle just drip.
            if (SelectedObject != _pressed) Select(_pressed!);
            _pressed = null;
            _drag = DragKind.Holding;
            return;
        }

        _drag = DragKind.Pending;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        switch (_drag)
        {
            case DragKind.None:
                UpdateHover(point);
                return;

            case DragKind.Holding:
                return;

            case DragKind.Pending:
                if (((Vector)(point - _dragStart)).Length <= 4) return;
                _dragMoved = true;
                _drag = DragAt(_dragStart);
                _pressed = null;
                if (_drag == DragKind.None) return;
                Interacted();
                SetCurrentValue(TapProperty, TapState.Closed);
                break;
        }

        if (_drag == DragKind.Scrub) SetCurrentValue(BaseMlProperty, Math.Round(PlotMl(point) * 20) / 20);
        InvalidateVisual();
    }

    /// <summary>What a press at <paramref name="point"/> drags once it moves: only the titration curve can be dragged along.</summary>
    private DragKind DragAt(Point point)
    {
        if (Mode != AcidBaseMode.Neutralize) return DragKind.None;
        var plot = LayOutNeutralize().Plot;
        plot = plot.Inflate(new Thickness(10, 10));
        return plot.Contains(point) ? DragKind.Scrub : DragKind.None;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetPosition(this);
        var drag = _drag;
        var picked = _pressed;
        _drag = DragKind.None;
        _pressed = null;
        _holding = null;
        e.Pointer.Capture(null);
        if (drag != DragKind.Pending || _dragMoved) return;

        if (picked is not null && _targets.Find(point)?.Id == picked) Pick(picked, point);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _targets.Hover = null;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _drag = DragKind.None;
        _pressed = null;
        _holding = null;
    }

    private void UpdateHover(Point point)
    {
        var hover = _targets.Find(point)?.Id;
        _targets.Hover = hover;
        Cursor = hover is not null ? new Cursor(StandardCursorType.Hand) : DragAt(point) == DragKind.None ? null : new Cursor(StandardCursorType.SizeWestEast);
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>
    /// A click on a thing: show (and say) what it is, and do what it does: a liquid's name picks it, a small tube its
    /// indicator, the water pill waters it down, the tap turns, the flask takes a drop, the curve goes to that much base.
    /// </summary>
    private void Pick(string id, Point point)
    {
        Interacted();
        if (id.StartsWith("sub_", StringComparison.Ordinal)) SetCurrentValue(SubstanceIdProperty, id[4..]);
        if (IndicatorOf(id) is { } dye) SetCurrentValue(ScaleIndicatorProperty, dye);
        switch (id)
        {
            case "dilute":
                SetCurrentValue(DilutionsProperty, Math.Min(MaxDilutions, Dilutions + 1));
                break;

            case "tap":
                SetCurrentValue(TapProperty, Tap switch
                {
                    TapState.Closed when BaseMl < AcidBaseModel.BuretteMl => TapState.Drip,
                    TapState.Drip => TapState.Stream,
                    _ => TapState.Closed,
                });
                break;

            case "flask":
                AddOneDrop();
                break;

            case "curve":
                SetCurrentValue(TapProperty, TapState.Closed);
                SetCurrentValue(BaseMlProperty, Math.Round(PlotMl(point) * 20) / 20);

                // Clicking along the curve again only moves along it; its explanation is already showing.
                if (SelectedObject == id) return;
                break;
        }

        // The water pill and a drop in the flask say what they are only the first time.
        if (id is "dilute" or "flask" or "tap" && SelectedObject == id) return;
        Select(id);
    }

    /// <summary>Shows a thing's explanation; cleared first so clicking the same thing again says it again.</summary>
    private void Select(string id)
    {
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
