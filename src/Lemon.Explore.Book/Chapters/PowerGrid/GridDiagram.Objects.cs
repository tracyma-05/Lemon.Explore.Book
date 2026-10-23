using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.PowerGrid;

/// <summary>
/// The things in the generator-and-grid diagram that can be clicked for an explanation (the generator's parts, the line
/// from the power station to the town and the transformer's, the grid's stations, frequency and city) and the
/// walkthrough's highlight. Clicking the lamp still switches it on or off, clicking the transformer's supply swaps AC and
/// DC; dragging sideways still turns the coil or moves through the day.
/// </summary>
public sealed partial class GridDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<GridDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<GridDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<GridDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<GridDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<GridDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<GridDiagram>(TitleHeightProperty);
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

    /// <summary>The target id of one of the three phases' coils (0 = A, 1 = B, 2 = C).</summary>
    internal static string PhaseId(int phase) => "phase-" + phase;

    /// <summary>
    /// The thing a target id stands for, which names its title (<c>Grid_Obj_&lt;name&gt;</c>) and its narration clip
    /// (<see cref="ObjectClipKey"/>).
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        null => null,
        "phase-0" => "PhaseA",
        "phase-1" => "PhaseB",
        "phase-2" => "PhaseC",
        "north" => "North",
        "south" => "South",
        "field" => "Field",
        "coil" => "Coil",
        "rings" => "Rings",
        "commutator" => "Commutator",
        "brushes" => "Brushes",
        "lamp" => "Lamp",
        "lamps" => "Lamps",
        "scope" => "Scope",
        "crank" => "Crank",
        "pushback" => "PushBack",
        "rotor" => "Rotor",
        "stator" => "Stator",
        "station" => "Station",
        "stepup" => "StepUp",
        "line" => "Line",
        "pylon" => "Pylon",
        "heat" => "Heat",
        "substation" => "Substation",
        "pole" => "Pole",
        "town" => "Town",
        "core" => "Core",
        "primary" => "Primary",
        "secondary" => "Secondary",
        "supply" => "Supply",
        "nuclear" => "Nuclear",
        "coal" => "Coal",
        "gas" => "Gas",
        "hydro" => "Hydro",
        "pumped" => "Pumped",
        "wind" => "Wind",
        "solar" => "Solar",
        "import" => "Import",
        "bus" => "Bus",
        "dial" => "Dial",
        "flywheel" => "Flywheel",
        "city" => "City",
        "trace" => "Trace",
        _ => null,
    };

    /// <summary>The resource key of a target's title, <c>Grid_Obj_&lt;name&gt;</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Grid_Obj_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Grid_Obj_" + name : null;

    /// <summary>The line's wires and the grid's bus are glowed along their length; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "line" or "bus";

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

    /// <summary>The coil can be turned by hand, and the day scrubbed, by dragging; the line has nothing to drag.</summary>
    private bool CanDrag => Mode switch
    {
        GridMode.Generator => true,
        GridMode.Balance => !EventRunning,
        _ => false,
    };

    #region clicking and dragging

    /// <summary>
    /// Pressing on a thing waits: moving more than a few pixels turns the coil (or moves through the day) as a drag, coming
    /// up over the thing picks it. Pressing anywhere else starts dragging at once.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragStart = e.GetPosition(this);
        _dragMoved = false;
        _pressed = _targets.Find(_dragStart)?.Id;
        if (_pressed is null && !CanDrag) return;
        e.Pointer.Capture(this);

        if (_pressed is null) StartDrag();
        e.Handled = true;
    }

    private void StartDrag()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        _dragging = true;
        _dragStartValue = Mode == GridMode.Generator ? _angle : _hour;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_pressed is not null)
        {
            if (!CanDrag || ((Vector)(point - _dragStart)).Length <= 4) return;
            _pressed = null;
            StartDrag();
        }

        if (!_dragging)
        {
            var hover = _targets.Find(point)?.Id;
            _targets.Hover = hover;
            Cursor = hover is not null ? new Cursor(StandardCursorType.Hand) : CanDrag ? new Cursor(StandardCursorType.SizeWestEast) : null;
            return;
        }

        var dx = point.X - _dragStart.X;
        if (Math.Abs(dx) > 2) _dragMoved = true;
        if (Mode == GridMode.Generator)
        {
            // About one turn across most of the picture's width.
            _angle = Wrap360(_dragStartValue + (dx * 360 / Math.Max(300, ActualWidth * 0.8)));
            Push(AngleProperty, Math.Round(_angle, 3));
        }
        else
        {
            // The whole day across the picture.
            _hour = GridDayModel.Wrap(_dragStartValue + (dx * 24 / Math.Max(300, ActualWidth * 0.9)));
            Push(HourProperty, Math.Round(_hour, 4));
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

    /// <summary>A click on a thing: show (and say) what it is. The lamp also switches; the transformer's supply swaps AC and DC.</summary>
    private void Pick(string id)
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);

        switch (id)
        {
            case "lamp" or "lamps":
                SetCurrentValue(LampOnProperty, !LampOn);
                break;

            case "supply":
                SetCurrentValue(FeedDcProperty, !FeedDc);
                break;
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
