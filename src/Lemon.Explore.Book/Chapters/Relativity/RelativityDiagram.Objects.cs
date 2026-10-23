using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Relativity;

/// <summary>
/// The things in the relativity diagram that can be clicked for an explanation, the walkthrough's highlight, and the
/// hands-on parts: dragging sideways in the light-clock and train views changes the speed; clicking the "seen from" pill
/// switches whose view it is; clicking the lamp flashes it again; dragging up and down in the spacetime diagram moves
/// through the trip, and dragging the turn corner changes where and how fast the traveller goes.
/// </summary>
public sealed partial class RelativityDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<RelativityDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<RelativityDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<RelativityDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<RelativityDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<RelativityDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<RelativityDiagram>(TitleHeightProperty);
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

        /// <summary>The button is down; moving far enough starts one of the drags below, else it is a click.</summary>
        Pending,

        /// <summary>Changing the speed by dragging sideways.</summary>
        Speed,

        /// <summary>Moving through the twins' trip by dragging up and down.</summary>
        Scrub,

        /// <summary>Moving the trip's turn corner: how far the star is and how fast the ship goes.</summary>
        Turn,
    }

    private DragKind _drag;

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    private Point _dragStart;
    private bool _dragMoved;
    private double _dragFrom;

    /// <summary>The thing a target id stands for (<c>zigzag</c> → <c>Obj_Zigzag</c>). It names the title and the narration clip.</summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "shipclock" => "Obj_ShipClock",
        "groundclock" => "Obj_GroundClock",
        "flash" => "Obj_Flash",
        "zigzag" => "Obj_Zigzag",
        "triangle" => "Obj_Triangle",
        "mirror" => "Obj_Mirror",
        "ship" => "Obj_Ship",
        "ground" => "Obj_Ground",
        "counter" => "Obj_Counter",
        "frame" => "Obj_Frame",
        "gamma" => "Obj_Gamma",
        "observer" => "Obj_Observer",
        "lamp" => "Obj_Lamp",
        "origin" => "Obj_Origin",
        "rear" => "Obj_Rear",
        "front" => "Obj_Front",
        "train" => "Obj_Train",
        "platform" => "Obj_Platform",
        "watcher" => "Obj_Watcher",
        "light" => "Obj_Light",
        "trainlength" => "Obj_TrainLength",
        "platformlength" => "Obj_PlatformLength",
        "trainclocks" => "Obj_TrainClocks",
        "platformclock" => "Obj_PlatformClock",
        "time" => "Obj_Time",
        "lightline" => "Obj_LightLine",
        "earthline" => "Obj_EarthLine",
        "shipline" => "Obj_ShipLine",
        "turn" => "Obj_Turn",
        "now" => "Obj_Now",
        "earthmarks" => "Obj_EarthMarks",
        "shipmarks" => "Obj_ShipMarks",
        "shipaxes" => "Obj_ShipAxes",
        "jump" => "Obj_Jump",
        "shipnow" => "Obj_ShipNow",
        "signals" => "Obj_Signals",
        "strip" => "Obj_Strip",
        "star" => "Obj_Star",
        "earthtwin" => "Obj_EarthTwin",
        "traveller" => "Obj_Traveller",
        "younger" => "Obj_Younger",
        "grid" => "Obj_Grid",
        _ => null,
    };

    /// <summary>The resource key of a target's title: <c>Rel_Obj_Zigzag</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Rel_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Rel_" + name : null;

    /// <summary>Lines the diagram glows itself; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "zigzag" or "triangle" or "lightline" or "earthline" or "shipline" or "shipnow" or "jump";

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

            case DragKind.Pending:
                if (((Vector)(point - _dragStart)).Length <= 4) return;
                _dragMoved = true;
                _drag = DragAt(_dragStart, _pressed);
                _pressed = null;
                if (_drag == DragKind.None) return;
                Interacted();
                StartDrag(_drag);
                break;
        }

        DragTo(point);
        InvalidateVisual();
    }

    /// <summary>What a press at <paramref name="point"/> (on <paramref name="pressed"/>) drags once it moves.</summary>
    private DragKind DragAt(Point point, string? pressed)
    {
        var stage = Stage();
        if (Mode != RelativityMode.Twins) return stage.Contains(point) ? DragKind.Speed : DragKind.None;
        if (pressed == "turn") return DragKind.Turn;
        var layout = LayOutTwins(Trip());
        var plot = layout.Plot;
        plot = plot.Inflate(new Thickness(10, 10));
        return plot.Contains(point) ? DragKind.Scrub : DragKind.None;
    }

    private void StartDrag(DragKind drag)
    {
        _dragFrom = drag == DragKind.Speed ? Beta : 0;
        if (drag is DragKind.Scrub or DragKind.Turn) SetCurrentValue(IsPlayingProperty, false);
    }

    private void DragTo(Point point)
    {
        switch (_drag)
        {
            case DragKind.Speed:
                SetCurrentValue(BetaProperty, Math.Round(Math.Clamp(_dragFrom + ((point.X - _dragStart.X) / 500), 0, 0.99), 2));
                break;

            case DragKind.Scrub:
            {
                var trip = Trip();
                var layout = LayOutTwins(trip);
                var years = (layout.Origin.Y - point.Y) / layout.Scale;
                SetCurrentValue(TwinTimeProperty, Math.Clamp(years, 0, trip.EarthYears));
                break;
            }

            case DragKind.Turn:
            {
                // The layout is kept from the start of the drag, so the diagram does not rescale under the mouse.
                var layout = _turnLayout ??= LayOutTwins(Trip());
                var distance = Math.Clamp(Math.Round((point.X - layout.Origin.X) / layout.Scale, 1), 1, 10);
                var years = Math.Max(0.1, (layout.Origin.Y - point.Y) / layout.Scale);
                SetCurrentValue(DistanceProperty, distance);
                SetCurrentValue(BetaProperty, Math.Round(Math.Clamp(distance / years, SlowestTrip, 0.99), 2));
                SetCurrentValue(TwinTimeProperty, RelativityModel.Trip(distance, TripSpeed(Beta)).Turn);
                break;
            }
        }
    }

    private TwinLayout? _turnLayout;

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetPosition(this);
        var drag = _drag;
        var picked = _pressed;
        _drag = DragKind.None;
        _pressed = null;
        _turnLayout = null;
        e.Pointer.Capture(null);
        if (drag != DragKind.Pending || _dragMoved) return;

        if (picked is not null && _targets.Find(point)?.Id == picked) Pick(picked);
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
        _turnLayout = null;
    }

    private void UpdateHover(Point point)
    {
        var hover = _targets.Find(point)?.Id;
        _targets.Hover = hover;
        if (hover is not null)
        {
            Cursor = Mode == RelativityMode.Twins && hover == "turn" ? new Cursor(StandardCursorType.SizeAll) : new Cursor(StandardCursorType.Hand);
            return;
        }

        Cursor = DragAt(point, null) switch
        {
            DragKind.Speed => new Cursor(StandardCursorType.SizeWestEast),
            DragKind.Scrub => new Cursor(StandardCursorType.SizeNorthSouth),
            _ => null,
        };
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>A click on a thing: show (and say) what it is. The "seen from" pill also switches the view; the lamp flashes again.</summary>
    private void Pick(string id)
    {
        Interacted();
        if (id == "frame") SetCurrentValue(FrameProperty, Frame == RelativityFrame.Ground ? RelativityFrame.Ship : RelativityFrame.Ground);
        if (id == "lamp" && double.IsNaN(TrainHold)) RestartTrain();

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
