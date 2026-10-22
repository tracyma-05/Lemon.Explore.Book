using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.BlueSky;

/// <summary>
/// The things in the blue sky diagram that can be clicked for an explanation (the Sun, the box of air, the two eyes, the
/// ray of sunlight…), the walkthrough's highlight, and telling a click on one of them from dragging the Sun.
/// </summary>
public sealed partial class SkyDiagram
{
    /// <summary>
    /// What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>) or a line (sunpath, beam,
    /// ray).
    /// </summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<SkyDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<SkyDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer starts dragging or clicks a thing, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<SkyDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<SkyDiagram, bool>(nameof(IsLessonActive), false);

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
        "sun" => "Sun",
        "earthshadow" => "EarthShadow",
        "beltofvenus" => "BeltOfVenus",
        "sunbehind" => "SunBehind",
        "box" => "Box",
        "sideeye" => "SideEye",
        "endeye" => "EndEye",
        "you" => "You",
        "overhead" => "Overhead",
        "ray" => "Ray",
        "air" => "Air",
        _ => null,
    };

    /// <summary>The resource key of a target's title, <c>Sky_Obj_&lt;name&gt;</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Sky_Obj_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Sky_Obj_" + name : null;

    /// <summary>Highlights the diagram draws as a glow along a line rather than a ring round a target.</summary>
    private static bool IsLineHighlight(string id) => id is "sunpath" or "beam" or "ray";

    private bool Glows(string id) => Highlight == id;

    /// <summary>A pill drawn with <see cref="LabelCache.DrawTextPill"/> at <paramref name="topLeft"/>, as a target.</summary>
    private void AddPillTarget(string id, string text, Point topLeft, double size, Color color)
    {
        var formatted = _labels.Text(text, size, color);
        _targets.AddBox(id, new Rect(topLeft.X, topLeft.Y, formatted.Width + 12, formatted.Height + 4));
    }

    /// <summary>A pill drawn centred on <paramref name="center"/> with <see cref="DrawCenteredPill"/>, as a target.</summary>
    private void AddCenteredPillTarget(string id, string text, Point center, double size, Color color)
    {
        var formatted = _labels.Text(text, size, color);
        AddPillTarget(id, text, new Point(center.X - (formatted.Width / 2) - 6, center.Y - (formatted.Height / 2) - 2), size, color);
    }

    #region dragging and clicking

    /// <summary>The Sun can be dragged in the sky and path views, above the toolbar.</summary>
    private bool CanDrag(Point point) => Mode != SkyMode.Scattering && point.Y <= ActualHeight - 100;

    /// <summary>
    /// Pressing on a thing waits to see whether it is a click, which explains it, or the start of a drag; pressing
    /// anywhere else in the sky (or round the observer) starts dragging the Sun at once.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(this);
        var target = _targets.Find(point)?.Id;
        if (target is null && !CanDrag(point)) return;
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
            if (CanDrag(_pressPoint)) BeginDrag(_pressPoint);
        }

        if (_dragging)
        {
            DragTo(point);
            return;
        }

        if (_pressed is not null) return;
        var hover = _targets.Find(point)?.Id;
        _targets.Hover = hover;
        Cursor = hover is not null || (Mode == SkyMode.Path && CanDrag(point)) ? new Cursor(StandardCursorType.Hand) : CanDrag(point) ? new Cursor(StandardCursorType.SizeNorthSouth) : null;
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
        _dragging = false;
        _pressed = null;
    }

    private void BeginDrag(Point point)
    {
        Interacted();
        _dragging = true;
        SetCurrentValue(IsPlayingProperty, false);
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
