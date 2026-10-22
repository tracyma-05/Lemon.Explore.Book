using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.BlackHole;

/// <summary>
/// The things in the black-hole diagram that can be clicked for an explanation, the walkthrough's highlight, and the
/// hands-on parts: dragging up and down tilts the up-close view; dragging the probe moves its start and dragging its arrow
/// sets its launch; dragging (or clicking) in the lensing view moves the hole in front of the sky.
/// </summary>
public sealed partial class BlackHoleDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<BlackHoleDiagram>(TitleHeightProperty);
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

        /// <summary>Tilting the up-close view.</summary>
        Tilt,

        /// <summary>Moving the probe's start.</summary>
        Start,

        /// <summary>Setting the probe's launch speed and direction.</summary>
        Arrow,

        /// <summary>Moving the hole in front of the sky.</summary>
        Lens,
    }

    private DragKind _drag;

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    private Point _dragStart;
    private bool _dragMoved;
    private double _dragFrom;

    /// <summary>The thing a target id stands for (<c>shadow</c> → <c>Obj_Shadow</c>). It names the title and the narration clip.</summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "shadow" => "Obj_Shadow",
        "horizon" => "Obj_Horizon",
        "photonring" => "Obj_PhotonRing",
        "hat" => "Obj_Hat",
        "below" => "Obj_Below",
        "coming" => "Obj_Coming",
        "going" => "Obj_Going",
        "inner" => "Obj_Inner",
        "stars" => "Obj_Stars",
        "scale" => "Obj_Scale",
        "photonsphere" => "Obj_PhotonSphere",
        "isco" => "Obj_Isco",
        "probe" => "Obj_Probe",
        "trail" => "Obj_Trail",
        "newton" => "Obj_Newton",
        "start" => "Obj_Start",
        "arrow" => "Obj_Arrow",
        "caught" => "Obj_Caught",
        "bent" => "Obj_Bent",
        "critical" => "Obj_Critical",
        "clocks" => "Obj_Clocks",
        "lens" => "Obj_Lens",
        "ring" => "Obj_Ring",
        "true" => "Obj_True",
        "image1" => "Obj_Image1",
        "image2" => "Obj_Image2",
        "arc" => "Obj_Arc",
        "track" => "Obj_Track",
        "sideview" => "Obj_SideView",
        _ => null,
    };

    /// <summary>The resource key of a target's title: <c>Hole_Obj_Shadow</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Hole_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Hole_" + name : null;

    /// <summary>Lines and rings the diagram glows itself; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "photonsphere" or "isco" or "trail" or "track" or "ring" or "critical" or "caught" or "bent";

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
        switch (Mode)
        {
            case BlackHoleMode.Shadow:
                return stage.Contains(point) ? DragKind.Tilt : DragKind.None;

            case BlackHoleMode.Orbits:
                return pressed switch
                {
                    "arrow" => DragKind.Arrow,
                    "start" or "probe" => DragKind.Start,
                    _ => DragKind.None,
                };

            default:
                return stage.Contains(point) && pressed is not ("sideview" or "ring" or "true" or "image1" or "image2" or "arc") || pressed == "lens"
                    ? DragKind.Lens
                    : DragKind.None;
        }
    }

    private void StartDrag(DragKind drag)
    {
        _hold = 0;
        _dragFrom = drag == DragKind.Tilt ? Inclination : 0;
        if (drag == DragKind.Lens) SetCurrentValue(IsPlayingProperty, false);
    }

    private void DragTo(Point point)
    {
        switch (_drag)
        {
            case DragKind.Tilt:
                SetCurrentValue(InclinationProperty, Math.Round(Math.Clamp(_dragFrom - ((point.Y - _dragStart.Y) * TiltPerPixel), 0, 90), 1));
                break;

            case DragKind.Start:
            case DragKind.Arrow:
            {
                var (centre, scale) = OrbitLayout(Stage());
                var m = new Point((point.X - centre.X) / scale, (centre.Y - point.Y) / scale);
                if (_drag == DragKind.Start) SetStartRadius(m.X);
                else SetLaunchTip(m);
                break;
            }

            case DragKind.Lens:
                MoveLensTo(point);
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
        if (hover is not null)
        {
            Cursor = Mode == BlackHoleMode.Orbits && hover is "arrow" or "start" or "probe" ? new Cursor(StandardCursorType.SizeAll) : new Cursor(StandardCursorType.Hand);
            return;
        }

        Cursor = DragAt(point, null) switch
        {
            DragKind.Tilt => new Cursor(StandardCursorType.SizeNorthSouth),
            DragKind.Lens => new Cursor(StandardCursorType.SizeAll),
            _ => null,
        };
    }

    /// <summary>A click on nothing in particular: in the lensing view, the hole moves there.</summary>
    private void ClickEmpty(Point point)
    {
        if (Mode != BlackHoleMode.Lensing || DragAt(point, null) != DragKind.Lens) return;
        Interacted();
        SetCurrentValue(IsPlayingProperty, false);
        MoveLensTo(point);
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>A click on a thing: show (and say) what it is.</summary>
    private void Pick(string id)
    {
        Interacted();

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
