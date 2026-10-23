using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.HeatEngines;

/// <summary>
/// The things in the engine-and-fridge diagram that can be clicked for an explanation (the engine's parts and strokes,
/// the fridge's parts, the cycle's two sides, flows, steps and loop) and the walkthrough's highlight. Clicking a stroke
/// on the dial or a step of the loop also jumps there; clicking the fridge's door still opens or shuts it; dragging
/// sideways still turns the crank or moves round the cycle.
/// </summary>
public sealed partial class HeatDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<HeatDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<HeatDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<HeatDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<HeatDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<HeatDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<HeatDiagram>(TitleHeightProperty);
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

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    /// <summary>The target id of a stroke's name on the dial.</summary>
    internal static string StrokeId(EngineStroke stroke) => "s-" + stroke;

    /// <summary>The target id of a step of the cycle (0…4 in the engine's order).</summary>
    internal static string StepId(int step) => "step-" + step;

    /// <summary>
    /// The thing a target id stands for, which names its narration clip (<see cref="ObjectClipKey"/>): a stroke
    /// (<c>s-Power</c>), a step of the cycle (<c>step-2</c>), or a thing of its own.
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        null => null,
        _ when id.StartsWith("s-", StringComparison.Ordinal) && Enum.TryParse<EngineStroke>(id[2..], out var stroke) => "Stroke" + stroke,
        "step-0" or "step-1" or "step-2" or "step-3" => "Step" + id[5..],
        "intakevalve" => "IntakeValve",
        "exhaustvalve" => "ExhaustValve",
        "camshaft" => "Camshaft",
        "coolant" => "Coolant",
        "rod" => "Rod",
        "crank" => "Crank",
        "sparkplug" => "SparkPlug",
        "piston" => "Piston",
        "oil" => "Oil",
        "intake" => "IntakePort",
        "exhaust" => "ExhaustPort",
        "gas" => "Gas",
        "dial" => "Dial",
        "spark" => "Spark",
        "compressor" => "Compressor",
        "condenser" => "Condenser",
        "capillary" => "Capillary",
        "evaporator" => "Evaporator",
        "insulation" => "Insulation",
        "door" => "Door",
        "heatout" => "HeatOut",
        "freezer" => "Freezer",
        "fridge" => "FridgeInside",
        "room" => "Room",
        "hot" => "HotSide",
        "cold" => "ColdSide",
        "machine" => "Machine",
        "hotflow" => "HotFlow",
        "coldflow" => "ColdFlow",
        "work" => "Work",
        "loop" => "Loop",
        "isotherm" => "Isotherm",
        _ => null,
    };

    /// <summary>
    /// The resource key of a target's title: the page's own name for an engine part, a stroke or the insulation, else
    /// <c>Heat_Obj_&lt;name&gt;</c>.
    /// </summary>
    internal static string? ObjectTitleKey(string? id)
    {
        if (ObjectName(id) is not { } name) return null;
        return id switch
        {
            _ when id!.StartsWith("s-", StringComparison.Ordinal) => "Heat_Stroke_" + id[2..],
            "intakevalve" or "exhaustvalve" or "camshaft" or "coolant" or "rod" or "crank" or "sparkplug" or "piston" or "oil" => "Heat_Part_" + name,
            "insulation" => "Heat_Fridge_Label_Insulation",
            _ => "Heat_Obj_" + name,
        };
    }

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Heat_Obj_" + name : null;

    /// <summary>The cycle's loop is glowed along its outline; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id == "loop";

    /// <summary>Whether the walkthrough points at <paramref name="id"/>, which the diagram glows itself.</summary>
    private bool Glows(string id) => Highlight == id;

    /// <summary>A text, a pill or a part already drawn in <paramref name="box"/>, as a target.</summary>
    private void AddBoxTarget(string id, Rect box)
    {
        box = box.Inflate(new Thickness(2, 1));
        _targets.AddBox(id, box);
    }

    /// <summary>
    /// The title sits over the top of the picture; in a narrow window, where the title wraps across the whole width, the
    /// picture starts lower, and never above the title's last line (its top margin is 22).
    /// </summary>
    // The book puts the title on the left page: only a margin at the top, not the original's title band.
    private double TitleSpace => 22 + TitleHeight + 12;

    #region clicking and dragging

    /// <summary>
    /// Pressing on a thing waits: moving more than a few pixels turns the crank (or moves round the cycle) as before, coming
    /// up over the thing picks it. Pressing anywhere else starts turning at once, as before.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragStart = e.GetPosition(this);
        _dragMoved = false;
        _pressed = _targets.Find(_dragStart)?.Id;
        if (_pressed is null && Mode == HeatMode.Fridge) return;
        e.Pointer.Capture(this);

        if (_pressed is null) StartDrag();
        e.Handled = true;
    }

    private void StartDrag()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        _dragging = true;
        _dragStartValue = Mode == HeatMode.Engine ? _angle : _phase;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_pressed is not null)
        {
            if (Mode == HeatMode.Fridge || ((Vector)(point - _dragStart)).Length <= 4) return;
            _pressed = null;
            StartDrag();
        }

        if (!_dragging)
        {
            var hover = _targets.Find(point)?.Id;
            _targets.Hover = hover;
            Cursor = hover is not null ? new Cursor(StandardCursorType.Hand) : Mode == HeatMode.Fridge ? null : new Cursor(StandardCursorType.SizeWestEast);
            return;
        }

        var dx = point.X - _dragStart.X;
        if (Math.Abs(dx) > 2) _dragMoved = true;
        if (Mode == HeatMode.Engine)
        {
            // About two turns of the crank across the width of the picture.
            _angle = EngineModel.Wrap(_dragStartValue + (dx * 720 / Math.Max(300, ActualWidth * 0.9)));
            Push(AngleProperty, Math.Round(_angle, 3));
        }
        else
        {
            _phase = (((_dragStartValue + (dx * 4 / Math.Max(300, ActualWidth * 0.9))) % 4) + 4) % 4;
            Push(PhaseProperty, Math.Round(_phase, 4));
        }

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var picked = _pressed;
        var dragged = _dragging && _dragMoved;
        _pressed = null;
        _dragging = false;
        e.Pointer.Capture(null);

        // A drag holds the picture where it was left.
        if (dragged) SetCurrentValue(IsPlayingProperty, false);
        if (picked is not null && _targets.Find(e.GetPosition(this))?.Id == picked) Pick(picked);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _targets.Hover = null;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
        _pressed = null;
    }

    /// <summary>
    /// A click on a thing: show (and say) what it is. A stroke or a step is also jumped to and held; the fridge's door opens
    /// or shuts, as before.
    /// </summary>
    private void Pick(string id)
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);

        if (id.StartsWith("s-", StringComparison.Ordinal) && Enum.TryParse<EngineStroke>(id[2..], out var stroke))
        {
            SetCurrentValue(AngleProperty, ((int)stroke * 180) + 90.0);
            SetCurrentValue(IsPlayingProperty, false);
        }
        else if (id.StartsWith("step-", StringComparison.Ordinal) && int.TryParse(id[5..], out var step))
        {
            SetCurrentValue(PhaseProperty, step + 0.5);
            SetCurrentValue(IsPlayingProperty, false);
        }
        else if (id == "door")
        {
            SetCurrentValue(DoorOpenProperty, !DoorOpen);
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
