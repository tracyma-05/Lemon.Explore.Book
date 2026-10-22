using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.StarTrails;

/// <summary>
/// The things in the star-trails diagram that can be clicked for an explanation (named stars, constellations, the pole,
/// the horizon, the sphere's circles and caps, the Sun, Earth and the sidereal day) and the walkthrough's highlight.
/// Dragging still turns the camera or the sphere, or moves Earth round its orbit.
/// </summary>
public sealed partial class TrailsDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<TrailsDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<TrailsDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<TrailsDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<TrailsDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<TrailsDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<TrailsDiagram>(TitleHeightProperty);
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

    /// <summary>The target id of a named star.</summary>
    internal static string StarId(string star) => "s-" + star;

    /// <summary>The target id of a constellation.</summary>
    internal static string FigureId(string figure) => "c-" + figure;

    private static readonly HashSet<string> FigureIds = StarCatalog.Figures.Select(f => f.Id).ToHashSet();

    /// <summary>
    /// The thing a target id stands for: a named star (<c>s-Vega</c> → <c>Star_Vega</c>), a constellation (<c>c-Orion</c>
    /// → <c>Con_Orion</c>), or a thing of its own (<c>Obj_Pole</c>). It names the title and the narration clip.
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        null => null,
        _ when id.StartsWith("s-", StringComparison.Ordinal) && StarCatalog.Find(id[2..]) is { Named: true } => "Star_" + id[2..],
        _ when id.StartsWith("c-", StringComparison.Ordinal) && FigureIds.Contains(id[2..]) => "Con_" + id[2..],
        "pole" => "Obj_Pole",
        "arc" => "Obj_Arc",
        "horizon" => "Obj_Horizon",
        "compass" => "Obj_Compass",
        "shutter" => "Obj_Shutter",
        "observer" => "Obj_Observer",
        "ncp" => "Obj_NorthPole",
        "scp" => "Obj_SouthPole",
        "zenith" => "Obj_Zenith",
        "equator" => "Obj_Equator",
        "circumpolar" => "Obj_Circumpolar",
        "never" => "Obj_NeverRises",
        "risers" => "Obj_Risers",
        "sun" => "Obj_Sun",
        "earth" => "Obj_Earth",
        "night" => "Obj_Night",
        "sidereal" => "Obj_Sidereal",
        "panorama" => "Obj_Panorama",
        _ => null,
    };

    /// <summary>The resource key of a target's title: <c>Trails_Star_Vega</c>, <c>Trails_Con_Orion</c>, <c>Trails_Obj_Pole</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Trails_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Trails_" + name : null;

    /// <summary>The sphere's equator is glowed along its circle; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "equator";

    /// <summary>Whether the walkthrough points at <paramref name="id"/>, which the diagram glows itself.</summary>
    private bool Glows(string id) => Highlight == id;

    /// <summary>A text, a pill or a part already drawn in <paramref name="box"/>, as a target.</summary>
    private void AddBoxTarget(string id, Rect box)
    {
        _targets.AddBox(id, box.Inflate(new Thickness(2, 1)));
    }

    /// <summary>
    /// The title sits over the top of the picture; in a narrow window, where the title wraps across the whole width, the
    /// picture starts lower, and never above the title's last line (its top margin is 22).
    /// </summary>
    private double TitleSpace => Math.Max(Math.Clamp(118 + ((1050 - ActualWidth) * 0.3), 118, 190), 22 + TitleHeight + 12);

    #region clicking and dragging

    /// <summary>
    /// Pressing on a thing waits: moving more than a few pixels drags (turns the camera or the sphere, or moves Earth),
    /// coming up over the thing picks it. Pressing anywhere else starts dragging at once.
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
        _dragStartValue = Mode switch
        {
            TrailsMode.Camera => (ViewAzimuth, ViewAltitude),
            TrailsMode.Sphere => (_sphereAzimuth, _sphereElevation),
            _ => (_day, 0),
        };
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
            Cursor = hover is not null ? new Cursor(StandardCursorType.Hand) : Mode == TrailsMode.Year ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.SizeAll);
            return;
        }

        Vector delta = point - _dragStart;
        if (delta.Length > 2) _dragMoved = true;
        switch (Mode)
        {
            case TrailsMode.Camera:
                // Grab the sky: dragging right turns the camera left, dragging down tilts it up.
                var perPixel = CameraFov / Math.Max(300, ActualWidth);
                SetCurrentValue(ViewAzimuthProperty, Math.Round(SkyModel.Wrap360(_dragStartValue.A - (delta.X * perPixel)), 2));
                SetCurrentValue(ViewAltitudeProperty, Math.Round(Math.Clamp(_dragStartValue.B + (delta.Y * perPixel), 0, 90), 2));
                break;

            case TrailsMode.Sphere:
                _sphereAzimuth = SkyModel.Wrap360(_dragStartValue.A - (delta.X * 0.4));
                _sphereElevation = Math.Clamp(_dragStartValue.B + (delta.Y * 0.3), 4, 80);
                break;

            default:
                // Earth follows the pointer round the Sun.
                if (_orbitRadius > 0)
                {
                    var v = point - _orbitCenter;
                    var angle = Math.Atan2(-v.Y, v.X) * 180 / Math.PI;
                    _day = DayForEarthAngle(angle);
                    Push(DayProperty, Math.Round(_day, 3));
                }

                break;
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

        // Moving Earth by hand holds the year where it was left.
        if (dragged && Mode == TrailsMode.Year) SetCurrentValue(IsPlayingProperty, false);
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
