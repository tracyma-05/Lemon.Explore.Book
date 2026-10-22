using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Oscillations;

/// <summary>
/// The things in the oscillations diagram that can be clicked for an explanation (the bob, the paper, the reference
/// circle, the motor…), the walkthrough's highlight, and telling a click on the bob or the mass from dragging it aside.
/// </summary>
public sealed partial class OscillatorDiagram
{
    /// <summary>
    /// What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>) or a line (trace, snake).
    /// </summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<OscillatorDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<OscillatorDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer starts dragging or clicks a thing, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<OscillatorDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<OscillatorDiagram, bool>(nameof(IsLessonActive), false);

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
        "bob" => "Bob",
        "length" => "Length",
        "angle" => "Angle",
        "paper" => "Paper",
        "place" => "Place",
        "formula" => "Formula",
        "mass" => "Mass",
        "spring" => "Spring",
        "rest" => "Rest",
        "unstretched" => "Unstretched",
        "stretch" => "Stretch",
        "circle" => "Circle",
        "motor" => "Motor",
        "longest" => "Longest",
        "shortest" => "Shortest",
        "topview" => "TopView",
        _ => null,
    };

    /// <summary>The resource key of a target's title, <c>Osc_Obj_&lt;name&gt;</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Osc_Obj_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Osc_Obj_" + name : null;

    /// <summary>Highlights the diagram draws as a glow along a line rather than a ring round a target.</summary>
    private static bool IsLineHighlight(string id) => id is "trace" or "snake";

    private bool Glows(string id) => Highlight == id;

    /// <summary>A label drawn with its top left at <paramref name="topLeft"/>, as a target.</summary>
    private void AddTextTarget(string id, FormattedText text, Point topLeft) =>
        _targets.AddBox(id, new Rect(topLeft.X - 2, topLeft.Y - 1, text.Width + 4, text.Height + 2));

    /// <summary>The height the picture fits into: less the walkthrough's subtitles while one runs.</summary>
    private double PictureHeight => Math.Max(200, ActualHeight - _reserve.Current);

    #region dragging and clicking

    /// <summary>
    /// Pressing on a thing waits to see whether it is a click, which explains it, or (on the bob or the mass) the start
    /// of dragging it aside.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(this);
        var target = _targets.Find(point)?.Id;
        var handle = Mode != OscillationMode.Wave && IsOnHandle(point);
        if (target is null && !handle) return;
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
            if (Mode != OscillationMode.Wave && IsOnHandle(_pressPoint)) BeginDrag(_pressPoint);
        }

        if (_dragging)
        {
            DragTo(point);
            return;
        }

        if (_pressed is not null) return;
        var hover = _targets.Find(point)?.Id;
        _targets.Hover = hover;
        Cursor = hover is not null || (Mode != OscillationMode.Wave && IsOnHandle(point)) ? new Cursor(StandardCursorType.Hand) : null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var picked = _pressed;
        var dragged = _dragging;
        _pressed = null;
        _dragging = false;
        e.Pointer.Capture(null);
        if (dragged) LetGo();
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
        if (!_dragging) return;
        _dragging = false;
        LetGo();
    }

    private void BeginDrag(Point point)
    {
        Interacted();
        _dragging = true;
        Push(StageProperty, SwingStage.Held);
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
