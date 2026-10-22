using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.IceAges;

/// <summary>
/// The things in the ice-age diagram that can be clicked for an explanation, the walkthrough's highlight, and the hands-on
/// parts: dragging along the time strip or a chart moves to that time; dragging sideways anywhere else moves through time
/// more finely; clicking a gauge in the orbit view shows that cycle alone; clicking a numbered end of a glacial, today's
/// line or the future jumps there.
/// </summary>
public sealed partial class IceAgeDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<IceAgeDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<IceAgeDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<IceAgeDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<IceAgeDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<IceAgeDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<IceAgeDiagram>(TitleHeightProperty);
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

        /// <summary>Following the mouse along a time axis (the strip or a chart): the time under it.</summary>
        Scrub,

        /// <summary>Dragging sideways elsewhere: through time, 4× finer than the strip.</summary>
        Nudge,
    }

    private DragKind _drag;

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    private Point _dragStart;
    private bool _dragMoved;
    private double _dragFrom;

    /// <summary>The thing a target id stands for (<c>ring65</c> → <c>Obj_Ring65</c>). It names the title and the narration clip.</summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "sun" => "Obj_Sun",
        "earth" => "Obj_Earth",
        "orbit" => "Obj_Orbit",
        "perihelion" => "Obj_Perihelion",
        "aphelion" => "Obj_Aphelion",
        "axis" => "Obj_Axis",
        "summerhalf" => "Obj_SummerHalf",
        "eccentricity" => "Obj_Eccentricity",
        "tilt" => "Obj_Tilt",
        "polestar" => "Obj_PoleStar",
        "strip" => "Obj_Strip",
        "globe" => "Obj_Globe",
        "ring65" => "Obj_Ring65",
        "laurentide" => "Obj_Laurentide",
        "fennoscandian" => "Obj_Fennoscandian",
        "greenland" => "Obj_Greenland",
        "seaice" => "Obj_SeaIce",
        "sunmeter" => "Obj_SunMeter",
        "sunshinechart" => "Obj_SunshineChart",
        "icechart" => "Obj_IceChart",
        "terminations" => "Obj_Terminations",
        "now" => "Obj_Now",
        "future" => "Obj_Future",
        "sheet" => "Obj_Sheet",
        "snowline" => "Obj_SnowLine",
        "snowfall" => "Obj_Snowfall",
        "melt" => "Obj_Melt",
        "sunrays" => "Obj_SunRays",
        "reflect" => "Obj_Reflect",
        "ground" => "Obj_Ground",
        "coast" => "Obj_Coast",
        "hudson" => "Obj_Hudson",
        "co2" => "Obj_Co2",
        _ => null,
    };

    /// <summary>The resource key of a target's title: <c>Ice_Obj_Ring65</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Ice_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Ice_" + name : null;

    /// <summary>Lines the diagram glows itself; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "orbit" or "summerhalf" or "axis" or "ring65" or "sheet" or "snowline";

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

    /// <summary>The time axes in the view: the strip in the orbit and ice-sheet views, both charts in the sunshine view.</summary>
    private IEnumerable<Rect> TimeAxes()
    {
        var stage = Stage();
        if (Mode == IceAgeMode.Sunshine)
        {
            var layout = LayOutSunshine();
            yield return layout.Sun;
            yield return layout.Ice;
            yield break;
        }

        yield return StripRect(stage);
    }

    /// <summary>The time axis under <paramref name="point"/> (a little slack around it), if any.</summary>
    private Rect? AxisAt(Point point)
    {
        foreach (var axis in TimeAxes())
        {
            var slack = axis;
            slack = slack.Inflate(new Thickness(8, 10));
            if (slack.Contains(point)) return axis;
        }

        return null;
    }

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
                _drag = DragAt(_dragStart);
                _pressed = null;
                if (_drag == DragKind.None) return;
                Interacted();
                _dragFrom = TimeKa;
                SetCurrentValue(IsPlayingProperty, false);
                break;
        }

        DragTo(point);
        InvalidateVisual();
    }

    /// <summary>What a press at <paramref name="point"/> drags once it moves: the time under it on an axis, else time by nudging.</summary>
    private DragKind DragAt(Point point)
    {
        if (AxisAt(point) is not null) return DragKind.Scrub;
        return Stage().Contains(point) ? DragKind.Nudge : DragKind.None;
    }

    private void DragTo(Point point)
    {
        switch (_drag)
        {
            case DragKind.Scrub:
                SetCurrentValue(TimeKaProperty, Math.Round(XTime(AxisAt(_dragStart) ?? TimeAxes().First(), point.X), 1));
                break;

            case DragKind.Nudge:
            {
                var strip = TimeAxes().First();
                var kyr = _dragFrom + ((point.X - _dragStart.X) / strip.Width * (IceRecord.End - IceRecord.Start) / 4);
                SetCurrentValue(TimeKaProperty, Math.Round(Math.Clamp(kyr, IceRecord.Start, IceRecord.End), 1));
                break;
            }
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

        if (picked is not null && _targets.Find(point)?.Id == picked) Pick(picked, point);
        else if (AxisAt(point) is { } axis)
        {
            // A click on an axis with nothing on it: go to that time.
            Interacted();
            SetCurrentValue(IsPlayingProperty, false);
            SetCurrentValue(TimeKaProperty, Math.Round(XTime(axis, point.X), 1));
        }
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
        Cursor = hover is not null ? new Cursor(StandardCursorType.Hand) : DragAt(point) == DragKind.None ? null : new Cursor(StandardCursorType.SizeWestEast);
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>
    /// A click on a thing: show (and say) what it is. A gauge also shows its cycle alone (clicked again: all three); a
    /// glacial's numbered end, today's line and the future jump there; a chart or the strip goes to the time clicked.
    /// </summary>
    private void Pick(string id, Point point)
    {
        Interacted();
        var cycle = id switch
        {
            "eccentricity" => IceAgeCycle.Eccentricity,
            "tilt" => IceAgeCycle.Tilt,
            "polestar" => IceAgeCycle.Precession,
            _ => (IceAgeCycle?)null,
        };
        if (cycle is { } chosen) SetCurrentValue(CycleProperty, Cycle == chosen ? IceAgeCycle.All : chosen);

        double? jump = id switch
        {
            "terminations" => IceRecord.Instance.Terminations.MinBy(t => Math.Abs(XAt(point) - t)),
            "now" => 0,
            "future" => 50,
            "strip" or "sunshinechart" or "icechart" => XAt(point),
            _ => null,
        };
        if (jump is { } kyr)
        {
            SetCurrentValue(IsPlayingProperty, false);
            SetCurrentValue(TimeKaProperty, Math.Round(kyr, 1));

            // Clicking along a time axis again only moves the time; its explanation is already showing.
            if (id is "strip" or "sunshinechart" or "icechart" && SelectedObject == id) return;
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    /// <summary>The time under a point's x on the view's (first) time axis.</summary>
    private double XAt(Point point) => XTime(AxisAt(point) ?? TimeAxes().First(), point.X);

    #endregion
}
