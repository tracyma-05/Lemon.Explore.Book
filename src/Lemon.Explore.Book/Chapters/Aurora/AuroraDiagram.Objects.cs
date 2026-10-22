using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Aurora;

/// <summary>
/// The things in the aurora diagram that can be clicked for an explanation, and the walkthrough's highlight. Dragging
/// scrubs the Dungey cycle (shield), turns the view round Earth (particles) or runs the night's hours (sky).
/// </summary>
public sealed partial class AuroraDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<AuroraDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<AuroraDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<AuroraDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<AuroraDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<AuroraDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<AuroraDiagram>(TitleHeightProperty);
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

    private bool _dragging;
    private bool _dragMoved;
    private Point _dragStart;
    private double _dragStartPhase;
    private double _dragStartHour;
    private double _dragStartAzimuth;
    private double _dragStartElevation;

    /// <summary>
    /// The thing a target id stands for (<c>bowshock</c> → <c>Obj_BowShock</c>). It names the title and the narration clip.
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "sun" => "Obj_Sun",
        "wind" => "Obj_Wind",
        "bowshock" => "Obj_BowShock",
        "sheath" => "Obj_Sheath",
        "magnetopause" => "Obj_Magnetopause",
        "tail" => "Obj_Tail",
        "plasmasheet" => "Obj_PlasmaSheet",
        "lobe" => "Obj_Lobe",
        "belts" => "Obj_Belts",
        "cusp" => "Obj_Cusp",
        "earth" => "Obj_Earth",
        "aurora" => "Obj_Aurora",
        "imf" => "Obj_Imf",
        "xday" => "Obj_DayX",
        "xtail" => "Obj_TailX",
        "openline" => "Obj_OpenLine",
        "closedline" => "Obj_ClosedLine",
        "stripped" => "Obj_Stripped",
        "particle" => "Obj_Particle",
        "spiral" => "Obj_Spiral",
        "mirror" => "Obj_Mirror",
        "line" => "Obj_Line",
        "shell" => "Obj_Shell",
        "foot" => "Obj_Foot",
        "inner" => "Obj_Inner",
        "outer" => "Obj_Outer",
        "ring" => "Obj_Ring",
        "cone" => "Obj_LossCone",
        "oval" => "Obj_Oval",
        "you" => "Obj_You",
        "pole" => "Obj_Pole",
        "meridian" => "Obj_Meridian",
        "curtain" => "Obj_Curtain",
        "green" => "Obj_Green",
        "red" => "Obj_Red",
        "violet" => "Obj_Violet",
        "horizon" => "Obj_Horizon",
        "sight" => "Obj_Sight",
        "view" => "Obj_View",
        "night" => "Obj_Night",
        "seen" => "Obj_Seen",
        _ => null,
    };

    /// <summary>The resource key of a target's title: <c>Aurora_Obj_BowShock</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Aurora_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Aurora_" + name : null;

    /// <summary>Lines and curves are glowed along their length by the diagram; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "bowshock" or "magnetopause" or "imf" or "openline" or "closedline"
        or "spiral" or "line" or "shell" or "meridian" or "sight" or "horizon" or "oval" or "seen";

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
    /// Pressing on a thing waits: moving more than a few pixels drags, coming up over the thing picks it. Pressing anywhere
    /// else starts dragging at once.
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
        _dragStartPhase = _phase;
        _dragStartHour = _hour;
        _dragStartAzimuth = _azimuth;
        _dragStartElevation = _elevation;
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
            Cursor = hover is not null ? new Cursor(StandardCursorType.Hand) : Mode == AuroraMode.Particles ? new Cursor(StandardCursorType.SizeAll) : new Cursor(StandardCursorType.SizeWestEast);
            return;
        }

        Vector delta = point - _dragStart;
        if (delta.Length > 2) _dragMoved = true;
        switch (Mode)
        {
            case AuroraMode.Shield:
                // Across the whole width is one cycle.
                _phase = ((_dragStartPhase + (delta.X / Math.Max(300, ActualWidth))) % 1 + 1) % 1;
                Push(CyclePhaseProperty, Math.Round(_phase, 4));
                break;

            case AuroraMode.Particles:
                _azimuth = _dragStartAzimuth - (delta.X * 0.35);
                _elevation = Math.Clamp(_dragStartElevation + (delta.Y * 0.25), -15, 70);
                break;

            default:
                // Across the whole width is twelve hours.
                _hour = Math.Clamp(_dragStartHour + (delta.X / Math.Max(300, ActualWidth) * 12), 0, 24);
                Push(SkyHourProperty, Math.Round(_hour, 4));
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

        // A cycle or an hour dragged to by hand stays there; turning the view leaves the particle running.
        if (dragged && Mode != AuroraMode.Particles) SetCurrentValue(IsPlayingProperty, false);
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
