using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Waves;

/// <summary>
/// The things in the waves diagram that can be clicked for an explanation (the dippers, the probe, the still lines, the
/// slits, the screen, the source, the listener…), the walkthrough's highlight, and telling a click from a drag: the probe
/// and the listener move, a dipper sets the distance between the dippers, the source steps through its pass.
/// </summary>
public sealed partial class WavesDiagram
{
    /// <summary>
    /// What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>) or a line (quiet, loud,
    /// paths, cone).
    /// </summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<WavesDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<WavesDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer starts dragging or clicks a thing, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<WavesDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<WavesDiagram, bool>(nameof(IsLessonActive), false);

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

    private readonly ExploreTargets _targets = new();
    private readonly LessonReserve _reserve = new();
    private string? _pressed;
    private Point _pressPoint;

    /// <summary>What a drag moves: the probe (or the listener), a dipper, or the source.</summary>
    private string? _dragHandle;

    /// <summary>Seconds of the page's clock for this frame, for the walkthrough's glows.</summary>
    private double _now;

    /// <summary>
    /// The thing a target id stands for, which names its resource keys (<see cref="ObjectTitleKey"/>,
    /// <see cref="ObjectClipKey"/>).
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "dippera" => "DipperA",
        "dipperb" => "DipperB",
        "probe" => "Probe",
        "difference" => "Difference",
        "quiet" => "Quiet",
        "loud" => "Loud",
        "tank" => "Tank",
        "laser" => "Laser",
        "slits" => "Slits",
        "spacing" => "Spacing",
        "screen" => "Screen",
        "order" => "Order",
        "tape" => "Tape",
        "source" => "Source",
        "listener" => "Listener",
        "bunched" => "Bunched",
        "stretched" => "Stretched",
        "cone" => "Cone",
        "heard" => "Heard",
        _ => null,
    };

    /// <summary>The resource key of a target's title, <c>Waves_Obj_&lt;name&gt;</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Waves_Obj_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Waves_Obj_" + name : null;

    /// <summary>Highlights the diagram draws as a glow along a line rather than a ring round a target.</summary>
    private static bool IsLineHighlight(string id) => id is "quiet" or "loud" or "paths" or "cone";

    private bool Glows(string id) => Highlight == id;

    /// <summary>A label drawn with its top left at <paramref name="topLeft"/>, as a target.</summary>
    private void AddTextTarget(string id, FormattedText text, Point topLeft) =>
        _targets.AddBox(id, new Rect(topLeft.X - 2, topLeft.Y - 1, text.Width + 4, text.Height + 2));

    /// <summary>A text pill drawn by <c>DrawTextPill</c> with its top left at <paramref name="topLeft"/>, as a target.</summary>
    private void AddPillTarget(string id, FormattedText text, Point topLeft) =>
        _targets.AddBox(id, new Rect(topLeft.X, topLeft.Y, text.Width + 12, text.Height + 4));

    #region dragging and clicking

    /// <summary>
    /// Pressing on a thing waits to see whether it is a click, which explains it, or the start of a drag. Pressing on
    /// open water (or the open road) moves the probe (or the listener) there at once, as before.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(this);
        var target = _targets.Find(point)?.Id;
        if (target is null && !IsOnStage(point)) return;
        e.Pointer.Capture(this);

        _pressPoint = point;
        _pressed = target;
        if (_pressed is null) BeginDrag(StageHandle, point);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_pressed is not null && ((Vector)(point - _pressPoint)).Length > 4)
        {
            var handle = HandleOf(_pressed) ?? (IsOnStage(_pressPoint) ? StageHandle : null);
            _pressed = null;
            if (handle is not null) BeginDrag(handle, point);
        }

        if (_dragging)
        {
            DragTo(point);
            return;
        }

        if (_pressed is not null) return;
        var hover = _targets.Find(point)?.Id;
        _targets.Hover = hover;
        Cursor = hover is not null || IsOnStage(point) ? new Cursor(StandardCursorType.Hand) : null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var picked = _pressed;
        _pressed = null;
        _dragging = false;
        _dragHandle = null;
        e.Pointer.Capture(null);
        if (picked is not null) Pick(picked);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _targets.Hover = null;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = null;
        _dragging = false;
        _dragHandle = null;
    }

    /// <summary>What pressing on open space drags: the probe, or in the Doppler view the listener.</summary>
    private string StageHandle => Mode == WaveMode.Doppler ? "listener" : "probe";

    /// <summary>What dragging a thing moves, or null if it does not move.</summary>
    private string? HandleOf(string id) => id switch
    {
        "probe" or "listener" or "source" => id,
        "dippera" or "dipperb" => "dipper",
        _ => null,
    };

    /// <summary>Whether a click there moves the probe or the listener.</summary>
    private bool IsOnStage(Point point) => Mode switch
    {
        WaveMode.Ripple => RippleLayout().Tank.Contains(point),
        WaveMode.Slits => SlitLayout() is var layout && layout.Field.Contains(point) && point.X > layout.BarrierX + 6,
        _ => DopplerLayout().Scene.Contains(point),
    };

    private void BeginDrag(string handle, Point point)
    {
        Interacted();
        _dragging = true;
        _dragHandle = handle;
        DragTo(point);
    }

    private void DragTo(Point point)
    {
        switch (_dragHandle)
        {
            case "dipper":
            {
                // The dippers stay either side of the middle: the distance is twice how far out the pointer is.
                var x = RippleLayout().ToWorld(point).X;
                Push(SeparationProperty, Math.Clamp(Math.Round(Math.Abs(x) * 20) / 10, 2, 12));
                break;
            }

            case "source":
            {
                // Where along the road the source is says how far through the pass it is.
                var mach = Mach;
                if (mach < 0.005) break;
                var x = DopplerLayout().ToWorld(point).X;
                var seconds = (x / (mach * WavePhysics.SpeedOfSound)) + (WavePhysics.PassDuration(mach) / 2);
                PushTime(Math.Clamp(seconds, 0, WavePhysics.LoopDuration(mach, Listener)));
                break;
            }

            case "listener":
            {
                var layout = DopplerLayout();
                var scene = layout.Scene;
                var clamped = new Point(Math.Clamp(point.X, scene.Left + 10, scene.Right - 10), Math.Clamp(point.Y, scene.Top + 10, scene.Bottom - 10));
                Push(ListenerProperty, Round(layout.ToWorld(clamped), 1));
                break;
            }

            case "probe" when Mode == WaveMode.Slits:
            {
                var layout = SlitLayout();
                var world = layout.ToWorld(point);
                var half = layout.Field.Height / 2 / layout.Scale;
                Push(SlitProbeProperty, Round(new Point(Math.Clamp(world.X, 1, SlitScreen), Math.Clamp(world.Y, -half + 0.3, half - 0.3)), 100));
                break;
            }

            case "probe":
            {
                var layout = RippleLayout();
                var tank = layout.Tank;
                var clamped = new Point(Math.Clamp(point.X, tank.Left + 4, tank.Right - 4), Math.Clamp(point.Y, tank.Top + 4, tank.Bottom - 4));
                Push(ProbeProperty, Round(layout.ToWorld(clamped), 10));
                break;
            }
        }
    }

    private static Point Round(Point point, double steps) =>
        new(Math.Round(point.X * steps) / steps, Math.Round(point.Y * steps) / steps);

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
