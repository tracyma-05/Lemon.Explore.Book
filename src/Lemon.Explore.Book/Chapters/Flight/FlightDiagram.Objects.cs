using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Flight;

/// <summary>
/// The things in the flight diagram that can be clicked for an explanation, the walkthrough's highlight, and the
/// hands-on parts: in the wind tunnel the wing turns when dragged and a drag anywhere else trails smoke (a click puffs
/// it); the flying plane's nose drags up and down and its throttle lever slides; the 3D plane banks when dragged
/// sideways, and clicking a control surface shows what it does.
/// </summary>
public sealed partial class FlightDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<FlightDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<FlightDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<FlightDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<FlightDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<FlightDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<FlightDiagram>(TitleHeightProperty);
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

        /// <summary>Turning the wing in the tunnel.</summary>
        Wing,

        /// <summary>Trailing smoke from the pointer, like a smoke wand in a real wind tunnel.</summary>
        Wand,

        /// <summary>Pitching the flying plane's nose up or down.</summary>
        Plane,
        Throttle,

        /// <summary>Banking the 3D plane (moving the stick sideways).</summary>
        Bank,
    }

    private DragKind _drag;

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    private Point _dragStart;
    private bool _dragMoved;

    /// <summary>The value being dragged when the drag started (angle of attack, bank).</summary>
    private double _dragFrom;

    /// <summary>The pointer's angle round the wing's pivot when the drag started, rad.</summary>
    private double _dragAngle;

    /// <summary>The thing a target id stands for (<c>lift</c> → <c>Obj_Lift</c>). It names the title and the narration clip.</summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "wing" => "Obj_Wing",
        "leadingedge" => "Obj_LeadingEdge",
        "trailingedge" => "Obj_TrailingEdge",
        "chord" => "Obj_Chord",
        "aoa" => "Obj_Aoa",
        "camber" => "Obj_Camber",
        "upper" => "Obj_Upper",
        "lower" => "Obj_Lower",
        "stagnation" => "Obj_Stagnation",
        "streamline" => "Obj_Streamline",
        "wlift" => "Obj_WingLift",
        "wdrag" => "Obj_WingDrag",
        "downwash" => "Obj_Downwash",
        "timeline" => "Obj_Timeline",
        "wake" => "Obj_Wake",
        "wind" => "Obj_Wind",
        "arrows" => "Obj_Arrows",
        "plane" => "Obj_Plane",
        "lift" => "Obj_Lift",
        "weight" => "Obj_Weight",
        "thrust" => "Obj_Thrust",
        "drag" => "Obj_Drag",
        "path" => "Obj_Path",
        "planeaoa" => "Obj_PlaneAoa",
        "propeller" => "Obj_Propeller",
        "balance" => "Obj_Balance",
        "throttle" => "Obj_Throttle",
        "airspeed" => "Obj_Airspeed",
        "altimeter" => "Obj_Altimeter",
        "vario" => "Obj_Vario",
        "stallwarn" => "Obj_StallWarn",
        "runway" => "Obj_Runway",
        "aileron" => "Obj_Aileron",
        "elevator" => "Obj_Elevator",
        "rudder" => "Obj_Rudder",
        "rollaxis" => "Obj_RollAxis",
        "pitchaxis" => "Obj_PitchAxis",
        "yawaxis" => "Obj_YawAxis",
        "liftvector" => "Obj_LiftVector",
        "rearview" => "Obj_RearView",
        "centripetal" => "Obj_Centripetal",
        "loadfactor" => "Obj_LoadFactor",
        "topview" => "Obj_TopView",
        _ => null,
    };

    /// <summary>The resource key of a target's title: <c>Flight_Obj_Lift</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Flight_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Flight_" + name : null;

    /// <summary>Lines and curves are glowed along their length by the diagram; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "chord" or "camber" or "streamline" or "path" or "rollaxis" or "pitchaxis" or "yawaxis";

    /// <summary>Whether the walkthrough points at <paramref name="id"/>, which the diagram glows itself.</summary>
    private bool Glows(string id) => Highlight == id;

    /// <summary>A text, a pill or a part already drawn in <paramref name="box"/>, as a target.</summary>
    private void AddBoxTarget(string id, Rect box)
    {
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
                _pressed = null;
                _drag = DragAt(_dragStart);
                if (_drag == DragKind.None) return;
                Interacted();
                StartDrag(_drag, _dragStart);
                break;
        }

        DragTo(point);
        InvalidateVisual();
    }

    /// <summary>What a press at <paramref name="point"/> drags once it moves.</summary>
    private DragKind DragAt(Point point)
    {
        switch (Mode)
        {
            case FlightMode.Wing:
                if (NearWing(point)) return DragKind.Wing;
                return Stage().Contains(point) ? DragKind.Wand : DragKind.None;

            case FlightMode.Forces:
                if (ThrottleSlot().Contains(point)) return DragKind.Throttle;
                return ((Vector)(point - _planeScreen)).Length < 95 ? DragKind.Plane : DragKind.None;

            default:
                return Stage().Contains(point) && !RearViewBox().Contains(point) && !TopViewBox().Contains(point) ? DragKind.Bank : DragKind.None;
        }
    }

    private void StartDrag(DragKind drag, Point start)
    {
        switch (drag)
        {
            case DragKind.Wing:
                _dragFrom = Alpha;
                _dragAngle = PointerAngle(start);
                break;

            case DragKind.Plane:
                _dragFrom = PlaneAlpha;
                break;

            case DragKind.Bank:
                _dragFrom = Bank;
                if (Demo != TurnDemo.Turn) SetCurrentValue(DemoProperty, TurnDemo.Turn);
                break;
        }
    }

    private void DragTo(Point point)
    {
        switch (_drag)
        {
            case DragKind.Wing:
            {
                // Turning the nose up turns the wing clockwise on screen (the nose is on the left).
                var turned = (PointerAngle(point) - _dragAngle) * 180 / Math.PI;
                SetCurrentValue(AlphaProperty, Math.Round(Math.Clamp(_dragFrom - turned, -10, 25) * 2) / 2);
                break;
            }

            case DragKind.Wand:
                Puff(point, 3);
                break;

            case DragKind.Plane:
                SetCurrentValue(PlaneAlphaProperty, Math.Round(Math.Clamp(_dragFrom - ((point.Y - _dragStart.Y) * 0.08), -5, 22) * 10) / 10);
                break;

            case DragKind.Throttle:
            {
                var slot = ThrottleSlot();
                SetCurrentValue(ThrottleProperty, Math.Round(Math.Clamp((slot.Bottom - 8 - point.Y) / (slot.Height - 16), 0, 1) * 100) / 100);
                break;
            }

            case DragKind.Bank:
                SetCurrentValue(BankProperty, Math.Round(Math.Clamp(_dragFrom + ((point.X - _dragStart.X) * 0.25), -70, 70)));
                break;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetPosition(this);
        var drag = _drag;
        var picked = _pressed;
        _drag = DragKind.None;
        _pressed = null;
        e.Pointer.Capture(null);
        if (drag != DragKind.Pending || _dragMoved) return;

        if (picked is not null)
        {
            if (_targets.Find(point)?.Id == picked) Pick(picked);
            return;
        }

        ClickEmpty(point);
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
    }

    private void UpdateHover(Point point)
    {
        var hover = _targets.Find(point)?.Id;
        _targets.Hover = hover;
        var drag = DragAt(point);
        Cursor = drag switch
        {
            DragKind.Throttle or DragKind.Plane => new Cursor(StandardCursorType.SizeNorthSouth),
            _ when hover is not null => new Cursor(StandardCursorType.Hand),
            DragKind.Wing => new Cursor(StandardCursorType.SizeAll),
            DragKind.Bank => new Cursor(StandardCursorType.SizeWestEast),
            DragKind.Wand => new Cursor(StandardCursorType.Cross),
            _ => null,
        };
    }

    /// <summary>A click on nothing in particular: in the tunnel, a puff of smoke there; the throttle slot moves its lever.</summary>
    private void ClickEmpty(Point point)
    {
        switch (Mode)
        {
            case FlightMode.Wing:
                if (Stage().Contains(point) && !NearWing(point))
                {
                    Interacted();
                    Puff(point, 24);
                }

                break;

            case FlightMode.Forces:
                if (ThrottleSlot().Contains(point))
                {
                    Interacted();
                    _drag = DragKind.Throttle;
                    DragTo(point);
                    _drag = DragKind.None;
                }

                break;
        }
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>A click on a thing: show (and say) what it is; a control surface on the 3D plane also shows what it does.</summary>
    private void Pick(string id)
    {
        Interacted();
        var demo = id switch
        {
            "aileron" or "rollaxis" => TurnDemo.Roll,
            "elevator" or "pitchaxis" => TurnDemo.Pitch,
            "rudder" or "yawaxis" => TurnDemo.Yaw,
            "liftvector" or "rearview" or "centripetal" or "loadfactor" or "topview" => TurnDemo.Turn,
            _ => (TurnDemo?)null,
        };
        if (Mode == FlightMode.Turn && demo is { } d && d != Demo) SetCurrentValue(DemoProperty, d);

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
