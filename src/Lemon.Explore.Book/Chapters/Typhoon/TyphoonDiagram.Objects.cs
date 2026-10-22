using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Typhoon;

/// <summary>
/// The things in the typhoon diagram that can be clicked for an explanation (the storm's eye, eyewall, bands, isobars and
/// probe; the slice's flows, warm core, sea and coast; the map's high, warm-sea edge, steering winds, track, days,
/// landfalls and the storm) and the walkthrough's highlight. Clicking the eye or the eyewall also moves the probe there;
/// clicking a day or a landfall jumps the track there; clicking where the storm formed starts it again. Clicking or
/// dragging anywhere else still moves the probe, turns the hours, or starts a storm on the open sea.
/// </summary>
public sealed partial class TyphoonDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<TyphoonDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<TyphoonDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<TyphoonDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<TyphoonDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<TyphoonDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<TyphoonDiagram>(TitleHeightProperty);
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

    /// <summary>The target id of the dot at the end of a day of the track (1 is the first day's end).</summary>
    internal static string DayId(int day) => "day-" + day;

    /// <summary>The target id of the storm's n-th landfall (0 is the first).</summary>
    internal static string LandfallId(int index) => "landfall-" + index;

    /// <summary>
    /// The thing a target id stands for, which names its narration clip (<see cref="ObjectClipKey"/>): every day dot is
    /// <c>Day</c>, every landfall <c>Landfall</c>; the eye, the eyewall and the bands are the same in both pictures.
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        null => null,
        _ when id.StartsWith("day-", StringComparison.Ordinal) => "Day",
        _ when id.StartsWith("landfall-", StringComparison.Ordinal) => "Landfall",
        "eye" => "Eye",
        "low" => "Low",
        "eyewall" => "Eyewall",
        "bands" => "Bands",
        "isobars" => "Isobars",
        "probe" => "Probe",
        "push" => "Push",
        "motion" => "Motion",
        "strongside" => "StrongSide",
        "turn" => "Turn",
        "scale" => "Scale",
        "outflow" => "Outflow",
        "inflow" => "Inflow",
        "warmcore" => "WarmCore",
        "sea" => "Sea",
        "wind" => "Wind",
        "coast" => "Coast",
        "showers" => "Showers",
        "start" => "Start",
        "warmedge" => "WarmEdge",
        "high" => "High",
        "edge588" => "Edge588",
        "steering" => "Steering",
        "track" => "Track",
        "storm" => "StormNow",
        "place" => "Place",
        "pacific" => "Pacific",
        "scs" => "SouthChinaSea",
        _ => null,
    };

    /// <summary>The resource key of a target's title: the page's own word where it has one, else <c>Typhoon_Obj_&lt;name&gt;</c>.</summary>
    internal static string? ObjectTitleKey(string? id)
    {
        if (ObjectName(id) is not { } name) return null;
        return id switch
        {
            "isobars" => "Typhoon_Option_Isobars",
            "probe" => "Typhoon_Stat_Probe",
            "wind" => "Typhoon_Stat_MaxWind",
            "pacific" => "Typhoon_Track_Pacific",
            "scs" => "Typhoon_Track_SouthChinaSea",
            _ => "Typhoon_Obj_" + name,
        };
    }

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Typhoon_Obj_" + name : null;

    /// <summary>The isobars, the track, the high's edge, the warm-sea edge and the steering winds are glowed along their lines.</summary>
    private static bool IsLineHighlight(string id) => id is "isobars" or "track" or "edge588" or "warmedge" or "steering";

    /// <summary>Whether the walkthrough points at <paramref name="id"/>, which the diagram glows itself.</summary>
    private bool Glows(string id) => Highlight == id;

    /// <summary>The glowing pen for a line the walkthrough points at.</summary>
    private IPen Glow => ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds);

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
    private double TitleSpace => Math.Max(Math.Clamp(146 + ((1050 - ActualWidth) * 0.3), 146, 220), 22 + TitleHeight + 12);

    #region clicking and dragging

    /// <summary>
    /// Pressing on a thing waits: moving more than a few pixels drags as before (the probe, the hours), coming up over the
    /// thing picks it. Pressing anywhere else acts at once, as before.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragStart = e.GetPosition(this);
        _dragMoved = false;
        _pressed = _targets.Find(_dragStart)?.Id;
        if (_pressed is null && Mode == TyphoonMode.Engine && !Landfall) return;
        e.Pointer.Capture(this);

        if (_pressed is null) StartDrag(_dragStart);
        e.Handled = true;
    }

    private void StartDrag(Point at)
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        switch (Mode)
        {
            case TyphoonMode.Storm:
                MoveProbe(at);
                break;

            case TyphoonMode.Engine:
                if (!Landfall) return;
                _dragStartValue = _landfallHours;
                break;

            default:
                _dragStartValue = _trackHour;
                break;
        }

        _dragging = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_pressed is not null)
        {
            if (((Vector)(point - _dragStart)).Length <= 4) return;
            _pressed = null;
            StartDrag(_dragStart);
        }

        if (!_dragging)
        {
            var hover = _targets.Find(point)?.Id;
            _targets.Hover = hover;
            Cursor = hover is not null ? new Cursor(StandardCursorType.Hand) : Mode switch
            {
                TyphoonMode.Storm => new Cursor(StandardCursorType.Cross),
                TyphoonMode.Engine => Landfall ? new Cursor(StandardCursorType.SizeWestEast) : null,
                _ => new Cursor(StandardCursorType.SizeWestEast),
            };
            return;
        }

        var dx = point.X - _dragStart.X;
        if (Math.Abs(dx) > 3 || Math.Abs(point.Y - _dragStart.Y) > 3) _dragMoved = true;
        var across = Math.Max(300, ActualWidth * 0.9);
        switch (Mode)
        {
            case TyphoonMode.Storm:
                MoveProbe(point);
                break;

            case TyphoonMode.Engine:
                _landfallHours = Math.Clamp(_dragStartValue + (dx * LandfallSpan / across), 0, LandfallSpan);
                Push(LandfallHoursProperty, Math.Round(_landfallHours, 3));
                break;

            default:
                _trackHour = Math.Clamp(_dragStartValue + (dx * CurrentTrack[^1].Hour / across), 0, CurrentTrack[^1].Hour);
                Push(TrackHourProperty, Math.Round(_trackHour, 3));
                break;
        }

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var picked = _pressed;
        var dragging = _dragging;
        var moved = _dragMoved;
        _pressed = null;
        _dragging = false;
        e.Pointer.Capture(null);

        if (picked is not null)
        {
            if (_targets.Find(e.GetPosition(this))?.Id == picked) Pick(picked);
            return;
        }

        if (!dragging) return;
        if (Mode == TyphoonMode.Track && !moved)
        {
            // A click on the open sea starts a new storm there.
            StartStormAt(e.GetPosition(this));
            return;
        }

        // A drag of a timeline holds the picture where it was left.
        if (moved && Mode != TyphoonMode.Storm) SetCurrentValue(IsPlayingProperty, false);
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
    /// A click on a thing: show (and say) what it is. The eye and the eyewall also take the probe there; a day or a
    /// landfall is jumped to and held; where the storm formed starts it again.
    /// </summary>
    private void Pick(string id)
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);

        if (Mode == TyphoonMode.Storm && id is "eye" or "eyewall")
        {
            // Into the calm middle, or out to the strongest wind (on the side away from the labels).
            var r = id == "eye" ? 0 : StormModel.RadiusOfMaxWind(CentralPressure, StormLatitude);
            SetCurrentValue(ProbeXProperty, Math.Round(r, 1));
            SetCurrentValue(ProbeYProperty, 0.0);
        }
        else if (id.StartsWith("day-", StringComparison.Ordinal) && int.TryParse(id[4..], out var day))
        {
            JumpTrack(day * 24);
        }
        else if (id.StartsWith("landfall-", StringComparison.Ordinal) && int.TryParse(id[9..], out var index)
                 && TrackModel.Landfalls(CurrentTrack) is { } falls && index < falls.Count)
        {
            JumpTrack(CurrentTrack[falls[index].Index].Hour);
        }
        else if (id == "start")
        {
            _trackRest = 0;
            SetCurrentValue(TrackHourProperty, 0.0);
            SetCurrentValue(IsPlayingProperty, true);
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    private void JumpTrack(double hour)
    {
        _trackRest = 0;
        SetCurrentValue(TrackHourProperty, Math.Min(hour, CurrentTrack[^1].Hour));
        SetCurrentValue(IsPlayingProperty, false);
    }

    #endregion
}
