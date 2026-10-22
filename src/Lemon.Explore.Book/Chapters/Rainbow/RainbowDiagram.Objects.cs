using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Rainbow;

/// <summary>
/// The things in the rainbow diagram that can be clicked for an explanation (the lamp, the angles, the share reflected,
/// the prism, the drop, the light leaking out, the bows…), the walkthrough's highlight, and telling a click from the
/// drag that sets the view's main value.
/// </summary>
public sealed partial class RainbowDiagram
{
    /// <summary>
    /// What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>) or a line (bowray, the
    /// drop's rainbow ray).
    /// </summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<RainbowDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<RainbowDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer starts dragging or clicks a thing, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<RainbowDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<RainbowDiagram, bool>(nameof(IsLessonActive), false);

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
        "lamp" => "Lamp",
        "normal" => "Normal",
        "index" => "Index",
        "crests" => "Crests",
        "incidence" => "Incidence",
        "refracted" => "Refracted",
        "reflected" => "Reflected",
        "through" => "Through",
        "total" => "Total",
        "critical" => "Critical",
        "prism" => "Prism",
        "spectrum" => "Spectrum",
        "turn" => "Turn",
        "drop" => "Drop",
        "entry" => "Entry",
        "exitangle" => "ExitAngle",
        "crowd" => "Crowd",
        "out" => "Out",
        "losses" => "Losses",
        "antisolar" => "Antisolar",
        "bowangle" => "BowAngle",
        "primary" => "Primary",
        "secondary" => "Secondary",
        "darkband" => "DarkBand",
        "brightinside" => "BrightInside",
        _ => null,
    };

    /// <summary>The resource key of a target's title, <c>Rain_Obj_&lt;name&gt;</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Rain_Obj_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Rain_Obj_" + name : null;

    /// <summary>Highlights the diagram draws as a glow along a line rather than a ring round a target.</summary>
    private static bool IsLineHighlight(string id) => id is "bowray";

    private bool Glows(string id) => Highlight == id;

    /// <summary>Seconds of the page's clock for this frame, for the walkthrough's glows.</summary>
    private double Now => _clock.Elapsed.TotalSeconds;

    /// <summary>A label drawn with its top left at <paramref name="topLeft"/>, as a target.</summary>
    private void AddTextTarget(string id, FormattedText text, Point topLeft) =>
        _targets.AddBox(id, new Rect(topLeft.X - 2, topLeft.Y - 1, text.Width + 4, text.Height + 2));

    /// <summary>A text pill drawn by <c>DrawTextPill</c> with its top left at <paramref name="topLeft"/>, as a target.</summary>
    private void AddPillTarget(string id, FormattedText text, Point topLeft) =>
        _targets.AddBox(id, new Rect(topLeft.X, topLeft.Y, text.Width + 12, text.Height + 4));

    #region dragging and clicking

    /// <summary>
    /// Pressing on a thing waits to see whether it is a click, which explains it, or the start of a drag. Pressing
    /// anywhere else in the picture drags at once, as before.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(this);
        var target = _targets.Find(point)?.Id;
        if (target is null && !Stage().Contains(point)) return;
        e.Pointer.Capture(this);

        _pressPoint = point;
        _pressed = target;
        if (_pressed is null) BeginDrag(point);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_pressed is not null && ((Vector)(point - _pressPoint)).Length > 4)
        {
            _pressed = null;
            if (Stage().Contains(_pressPoint)) BeginDrag(_pressPoint);
        }

        if (_dragging)
        {
            DragTo(point);
            return;
        }

        if (_pressed is not null) return;
        var hover = _targets.Find(point)?.Id;
        _targets.Hover = hover;
        Cursor = hover is not null ? new Cursor(StandardCursorType.Hand)
            : Stage().Contains(point) ? Mode == RainbowMode.Refraction ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.SizeNorthSouth)
            : null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var picked = _pressed;
        _pressed = null;
        _dragging = false;
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
    }

    /// <summary>Starts dragging from <paramref name="point"/>: the sweep stops and the value follows the pointer.</summary>
    private void BeginDrag(Point point)
    {
        Interacted();
        _dragging = true;
        _dragStartY = point.Y;
        _dragStartSun = SunAltitude;
        SetCurrentValue(IsSweepingProperty, false);
        DragTo(point);
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
