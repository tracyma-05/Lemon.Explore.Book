using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Retrograde;

/// <summary>
/// The things in the retrograde diagram that can be clicked for an explanation (the planet, the stations, the opposition,
/// the ecliptic, Earth, the Sun, the orbits, the lines of sight, the deferent and the epicycle) and the walkthrough's
/// highlight. Dragging scrubs the date; from above it moves Earth round its orbit.
/// </summary>
public sealed partial class RetroDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<RetroDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<RetroDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<RetroDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<RetroDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<RetroDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<RetroDiagram>(TitleHeightProperty);
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

    /// <summary>The target id of a planet: <c>p-Mars</c>.</summary>
    internal static string PlanetId(RetroPlanet planet) => "p-" + planet;

    /// <summary>
    /// The thing a target id stands for: a planet (<c>p-Mars</c> → <c>Planet_Mars</c>) or a thing of its own
    /// (<c>Obj_StationRetro</c>). It names the title and the narration clip.
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        null => null,
        _ when id.StartsWith("p-", StringComparison.Ordinal) && Enum.TryParse<RetroPlanet>(id[2..], out var planet) => "Obj_" + planet,
        "sun" => "Obj_Sun",
        "earth" => "Obj_Earth",
        "ecliptic" => "Obj_Ecliptic",
        "station-r" => "Obj_StationRetro",
        "station-d" => "Obj_StationDirect",
        "opposition" => "Obj_Opposition",
        "conjunction" => "Obj_Conjunction",
        "retro" => "Obj_Retro",
        "zodiac" => "Obj_Zodiac",
        "scale" => "Obj_Scale",
        "east" => "Obj_East",
        "sightline" => "Obj_SightLine",
        "stars" => "Obj_Stars",
        "earthorbit" => "Obj_EarthOrbit",
        "planetorbit" => "Obj_PlanetOrbit",
        "marks" => "Obj_Marks",
        "fan" => "Obj_Fan",
        "speed" => "Obj_Speed",
        "loop" => "Obj_Loop",
        "deferent" => "Obj_Deferent",
        "epicycle" => "Obj_Epicycle",
        "frame" => "Obj_Frame",
        _ => null,
    };

    /// <summary>The resource key of a target's title: <c>Retro_Obj_Mars</c>, <c>Retro_Obj_StationRetro</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Retro_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Retro_" + name : null;

    /// <summary>Lines and curves are glowed along their length; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "ecliptic" or "retro" or "sightline" or "earthorbit" or "planetorbit" or "deferent" or "epicycle" or "stars" or "fan";

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
    private double TitleSpace => Math.Max(Math.Clamp(118 + ((1050 - ActualWidth) * 0.3), 118, 190), 22 + TitleHeight + 12);

    #region clicking and dragging

    /// <summary>
    /// Pressing on a thing waits: moving more than a few pixels drags (scrubs the date), coming up over the thing picks it.
    /// Pressing anywhere else starts dragging at once.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragStart = e.GetPosition(this);
        _dragMoved = false;
        _pressed = _targets.Find(_dragStart)?.Id;
        e.Pointer.Capture(this);

        if (_pressed is null) StartDrag();
        e.Handled = true;
    }

    private void StartDrag()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        _dragging = true;
        _dragStartDay = _day;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_pressed is not null)
        {
            if (((Vector)(point - _dragStart)).Length <= 4) return;
            _pressed = null;
            StartDrag();
        }

        if (!_dragging)
        {
            var hover = _targets.Find(point)?.Id;
            _targets.Hover = hover;
            Cursor = hover is not null ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.SizeWestEast);
            return;
        }

        Vector delta = point - _dragStart;
        if (delta.Length > 2) _dragMoved = true;
        _day = Mode switch
        {
            // Dragging towards the east runs time forward through the loop.
            RetroMode.Sky => Math.Clamp(_dragStartDay + (delta.X * (EastLeft ? -1 : 1) / Math.Max(200, _skyArea.Width) * (WindowEnd - WindowStart)), WindowStart, WindowEnd),
            RetroMode.Above => DayForEarthAt(point),
            _ => Math.Clamp(_dragStartDay + (delta.X / Math.Max(300, ActualWidth) * PatternDays(Planet) * 0.5), RetroModel.MinDay, RetroModel.MaxDay),
        };
        Push(DayProperty, Math.Round(_day, 3));
        InvalidateVisual();
    }

    /// <summary>From above: the day in the loop being shown (nearest the current one) when Earth is in the pointer's direction from the Sun.</summary>
    private double DayForEarthAt(Point point)
    {
        if (_aboveScale <= 0) return _day;
        var v = point - _aboveCenter;
        var target = Math.Atan2(-v.Y, v.X) * 180 / Math.PI;
        var day = _day;
        for (var i = 0; i < 3; i++)
        {
            var earth = RetroModel.Earth(day);
            var angle = Math.Atan2(earth.Y, earth.X) * 180 / Math.PI;
            day += RetroModel.Wrap180(target - angle) / 0.9856;
        }

        // The same place a whole year earlier or later may be the one inside the loop's days.
        var candidates = Enumerable.Range(-2, 5).Select(k => day + (k * 365.256)).ToList();
        var inside = candidates.Where(d => d >= WindowStart && d <= WindowEnd).ToList();
        return (inside.Count > 0 ? inside : candidates).MinBy(d => Math.Abs(d - _day));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var picked = _pressed;
        var dragged = _dragging && _dragMoved;
        _pressed = null;
        _dragging = false;
        e.Pointer.Capture(null);

        // A date dragged to by hand stays there.
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

    /// <summary>A click on a thing: show (and say) what it is.</summary>
    private void Pick(string id)
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
