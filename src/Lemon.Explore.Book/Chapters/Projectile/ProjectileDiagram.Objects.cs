using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Projectile;

/// <summary>
/// The things in the projectile diagram that can be clicked for an explanation (the cannon, the ball, its strobe images,
/// the velocity arrows, the shadows, the fan's launcher, the monkey…), the walkthrough's highlight, and telling a click
/// on the cannon from aiming it.
/// </summary>
public sealed partial class ProjectileDiagram
{
    /// <summary>
    /// What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>) or a line (path, envelope).
    /// </summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<ProjectileDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<ProjectileDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer starts aiming or clicks a thing, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<ProjectileDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<ProjectileDiagram, bool>(nameof(IsLessonActive), false);

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

    /// <summary>
    /// The thing a target id stands for, which names its resource keys (<see cref="ObjectTitleKey"/>,
    /// <see cref="ObjectClipKey"/>).
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "cannon" => "Cannon",
        "ball" => "Ball",
        "strobe" => "Strobe",
        "velocity" => "Velocity",
        "vx" => "Vx",
        "vy" => "Vy",
        "weight" => "Weight",
        "drag" => "Drag",
        "peak" => "Peak",
        "range" => "Range",
        "airless" => "Airless",
        "groundshadow" => "GroundShadow",
        "postshadow" => "PostShadow",
        "launcher" => "Launcher",
        "farthest" => "Farthest",
        "envelope" => "Envelope",
        "landing" => "Landing",
        "dropped" => "Dropped",
        "thrown" => "Thrown",
        "oddnumbers" => "OddNumbers",
        "monkey" => "Monkey",
        "dart" => "Dart",
        "hunter" => "Hunter",
        "aim" => "Aim",
        "fall" => "Fall",
        _ => null,
    };

    /// <summary>The resource key of a target's title, <c>Proj_Obj_&lt;name&gt;</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Proj_Obj_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Proj_Obj_" + name : null;

    /// <summary>Highlights the diagram draws as a glow along a line rather than a ring round a target.</summary>
    private static bool IsLineHighlight(string id) => id is "path" or "envelope";

    private bool Glows(string id) => Highlight == id;

    /// <summary>Seconds of the page's clock for this frame, for the walkthrough's glows.</summary>
    private double Now => _clock.Elapsed.TotalSeconds;

    /// <summary>A label drawn with its top left at <paramref name="topLeft"/>, as a target.</summary>
    private void AddTextTarget(string id, FormattedText text, Point topLeft) =>
        _targets.AddBox(id, new Rect(topLeft.X - 2, topLeft.Y - 1, text.Width + 4, text.Height + 2));

    /// <summary>A text pill drawn by <c>DrawTextPill</c> with its top left at <paramref name="topLeft"/>, as a target.</summary>
    private void AddPillTarget(string id, FormattedText text, Point topLeft) =>
        _targets.AddBox(id, new Rect(topLeft.X, topLeft.Y, text.Width + 12, text.Height + 4));

    #region aiming and clicking

    /// <summary>
    /// Pressing on a thing waits to see whether it is a click, which explains it, or (on the cannon) the start of aiming
    /// it. Pressing on the tip of the launch arrow aims at once, as before.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(this);
        var target = _targets.Find(point)?.Id;
        var handle = IsOnCannon(point);
        if (target is null && !handle) return;
        e.Pointer.Capture(this);

        _pressPoint = point;
        _pressed = target;
        if (_pressed is null) BeginAim(point);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_pressed is not null && ((Vector)(point - _pressPoint)).Length > 4)
        {
            _pressed = null;
            if (IsOnCannon(_pressPoint)) BeginAim(point);
        }

        if (_dragging)
        {
            AimAt(point);
            return;
        }

        if (_pressed is not null) return;
        var hover = _targets.Find(point)?.Id;
        _targets.Hover = hover;
        Cursor = hover is not null || IsOnCannon(point) ? new Cursor(StandardCursorType.Hand) : null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var picked = _pressed;
        var aimed = _dragging;
        _pressed = null;
        _dragging = false;
        e.Pointer.Capture(null);
        if (aimed) Push(IsPlayingProperty, true);
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
    }

    /// <summary>On the cannon, or on the tip of its launch arrow.</summary>
    private bool IsOnCannon(Point point)
    {
        if (Mode != ProjectileMode.Launch) return false;
        var view = Layout();
        var muzzle = view.P(0, CliffHeight);
        return ((Vector)(point - muzzle)).Length < 40 || ((Vector)(point - ArrowTip(muzzle))).Length < 18;
    }

    private void BeginAim(Point point)
    {
        Interacted();
        _dragging = true;
        AimAt(point);
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
