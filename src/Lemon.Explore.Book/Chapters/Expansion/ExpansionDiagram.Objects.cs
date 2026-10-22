using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Expansion;

/// <summary>
/// The things in the expansion diagram that can be clicked for an explanation, the walkthrough's highlight, and the
/// hands-on parts: clicking a galaxy in the Hubble view makes it home, and dragging sideways runs time back and forth;
/// dragging along the strip of space moves the light on its journey; dragging along the horn moves the moment looked at,
/// and clicking a moment of history jumps there.
/// </summary>
public sealed partial class ExpansionDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<ExpansionDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<ExpansionDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<ExpansionDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<ExpansionDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<ExpansionDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<ExpansionDiagram>(TitleHeightProperty);
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

        /// <summary>Running the Hubble view's time back and forth.</summary>
        Time,

        /// <summary>Moving the light along its journey.</summary>
        Journey,

        /// <summary>Moving the moment looked at along the horn.</summary>
        Cursor,
    }

    private DragKind _drag;

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    private Point _dragStart;
    private bool _dragMoved;

    /// <summary>The value being dragged when the drag started (progress, journey).</summary>
    private double _dragFrom;

    /// <summary>The thing a target id stands for (<c>home</c> → <c>Obj_Home</c>). It names the title and the narration clip.</summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "home" => "Obj_Home",
        "galaxy" => "Obj_Galaxy",
        "arrow" => "Obj_Arrow",
        "ruler" => "Obj_Ruler",
        "ghost" => "Obj_Ghost",
        "grid" => "Obj_Grid",
        "law" => "Obj_Law",
        "clock" => "Obj_Clock",
        "source" => "Obj_Source",
        "us" => "Obj_Us",
        "light" => "Obj_Light",
        "space" => "Obj_Space",
        "distnow" => "Obj_DistNow",
        "distthen" => "Obj_DistThen",
        "travel" => "Obj_Travel",
        "drift" => "Obj_Drift",
        "rclock" => "Obj_RedshiftClock",
        "lab" => "Obj_Lab",
        "arriving" => "Obj_Arriving",
        "line" => "Obj_Line",
        "visible" => "Obj_Visible",
        "infrared" => "Obj_Infrared",
        "jwst" => "Obj_Jwst",
        "size" => "Obj_Size",
        "cursor" => "Obj_Cursor",
        "patch" => "Obj_Patch",
        "axis" => "Obj_Axis",
        "bigbang" => "Obj_BigBang",
        "protons" => "Obj_Protons",
        "nuclei" => "Obj_Nuclei",
        "cmb" => "Obj_Cmb",
        "firststars" => "Obj_FirstStars",
        "jades" => "Obj_Jades",
        "noon" => "Obj_Noon",
        "speedup" => "Obj_Speedup",
        "sun" => "Obj_Sun",
        "now" => "Obj_Now",
        "andromeda" => "Obj_Andromeda",
        "turnaround" => "Obj_Turnaround",
        "crunch" => "Obj_Crunch",
        _ => null,
    };

    /// <summary>The resource key of a target's title: <c>Expand_Obj_Home</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Expand_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Expand_" + name : null;

    /// <summary>Lines are glowed along their length by the diagram; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "grid" or "space";

    /// <summary>Whether the walkthrough points at <paramref name="id"/>, which the diagram glows itself.</summary>
    private bool Glows(string id) => Highlight == id;

    /// <summary>A text, a pill or a part already drawn in <paramref name="box"/>, as a target.</summary>
    private void AddBoxTarget(string id, Rect box)
    {
        box = box.Inflate(new Thickness(2, 1));
        _targets.AddBox(id, box);
    }

    /// <summary>The title sits over the top of the picture: the picture starts below its last line (its top margin is 22), however it wraps.</summary>
    private double TitleSpace => Math.Max(140, 22 + TitleHeight + 14);

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
                StartDrag(_drag);
                break;
        }

        DragTo(point);
        InvalidateVisual();
    }

    /// <summary>What a press at <paramref name="point"/> drags once it moves.</summary>
    private DragKind DragAt(Point point)
    {
        var stage = Stage();
        switch (Mode)
        {
            case ExpansionMode.Hubble:
                return stage.Contains(point) ? DragKind.Time : DragKind.None;

            case ExpansionMode.Redshift:
            {
                var (strip, _) = RedshiftLayout(stage);
                strip = strip.Inflate(new Thickness(0, 40));
                return strip.Contains(point) ? DragKind.Journey : DragKind.None;
            }

            default:
            {
                var layout = HistoryLayout(stage);
                var band = new Rect(layout.Left - 10, stage.Top, layout.Width + 20, layout.AxisY + 12 - stage.Top);
                return band.Contains(point) ? DragKind.Cursor : DragKind.None;
            }
        }
    }

    private void StartDrag(DragKind drag)
    {
        // Scrubbing takes over from the animation until play is pressed again.
        SetCurrentValue(IsPlayingProperty, false);
        _hold = 0;
        _dragFrom = drag switch
        {
            DragKind.Time => HubbleProgress,
            DragKind.Journey => Journey,
            _ => 0,
        };
    }

    private void DragTo(Point point)
    {
        var stage = Stage();
        switch (_drag)
        {
            case DragKind.Time:
                SetCurrentValue(HubbleProgressProperty, Math.Clamp(_dragFrom + ((point.X - _dragStart.X) / (stage.Width * 0.7)), 0, 1));
                break;

            case DragKind.Journey:
                SetCurrentValue(JourneyProperty, Math.Clamp(_dragFrom + ((point.X - _dragStart.X) / (stage.Width * 0.7)), 0, 1));
                break;

            case DragKind.Cursor:
                MoveCursorTo(point.X);
                break;
        }
    }

    /// <summary>Puts the history view's moment under an x on the screen.</summary>
    private void MoveCursorTo(double x)
    {
        var layout = HistoryLayout(Stage());
        var time = HistoryTimeAt(layout, Math.Clamp(x, layout.Left, layout.Right));
        SetCurrentValue(CosmicTimeProperty, Math.Clamp(time, CosmicHistory.Earliest(Axis), HistoryEnd));
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
        Cursor = hover is not null
            ? new Cursor(StandardCursorType.Hand)
            : DragAt(point) == DragKind.None ? null : new Cursor(StandardCursorType.SizeWestEast);
    }

    /// <summary>A click on nothing in particular: on the horn, the moment looked at moves there.</summary>
    private void ClickEmpty(Point point)
    {
        if (Mode != ExpansionMode.History || DragAt(point) != DragKind.Cursor) return;
        Interacted();
        SetCurrentValue(IsPlayingProperty, false);
        MoveCursorTo(point.X);
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>
    /// A click on a thing: show (and say) what it is. A galaxy becomes home; a moment of history (or the Big Bang, or
    /// today) moves the history view there and holds it.
    /// </summary>
    private void Pick(string id)
    {
        Interacted();
        if (Mode == ExpansionMode.Hubble && id == "galaxy" && GalaxyAt(_dragStart) is { } galaxy && galaxy.Cell != Home)
        {
            SetCurrentValue(HomeProperty, galaxy.Cell);
        }

        if (Mode == ExpansionMode.History && JumpTime(id) is { } time)
        {
            SetCurrentValue(IsPlayingProperty, false);
            SetCurrentValue(CosmicTimeProperty, Math.Min(time, HistoryEnd));
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    /// <summary>The time a clicked moment of history stands for, or null.</summary>
    private double? JumpTime(string id)
    {
        var universe = Cosmos;
        switch (id)
        {
            case "bigbang":
                return CosmicHistory.Earliest(Axis);

            case "turnaround":
                return universe.TurnaroundTime;

            case "crunch":
                return universe.CrunchTime is { } crunch ? crunch - 0.05 : null;
        }

        var match = CosmicEvent.All.FirstOrDefault(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        return match?.TimeIn(universe);
    }

    #endregion
}
