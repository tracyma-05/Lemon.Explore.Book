using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Eclipses;

/// <summary>
/// The things in the eclipse diagram that can be clicked for an explanation (the Sun, the Moon, the umbra, the line of
/// nodes…), the walkthrough's highlight, and dragging: the Moon round its orbit, or (seasons view) through the year.
/// </summary>
public sealed partial class EclipseDiagram
{
    /// <summary>
    /// What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>) or a line (orbit, nodeline,
    /// season, moonorbit).
    /// </summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<EclipseDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<EclipseDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer starts dragging or clicks a thing, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<EclipseDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<EclipseDiagram, bool>(nameof(IsLessonActive), false);

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
    private bool _dragging;
    private double _dragStartX;
    private double _dragStartDay;

    /// <summary>
    /// The thing a target id stands for, which names its resource keys (<see cref="ObjectTitleKey"/>,
    /// <see cref="ObjectClipKey"/>).
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "sun" => "Sun",
        "earth" => "Earth",
        "moon" => "Moon",
        "umbra" => "Umbra",
        "penumbra" => "Penumbra",
        "antumbra" => "Antumbra",
        "observer" => "Observer",
        "newmoon" => "NewMoon",
        "fullmoon" => "FullMoon",
        "ecliptic" => "Ecliptic",
        "season" => "Season",
        "nodeline" => "NodeLine",
        "moonorbit" => "MoonOrbit",
        _ => null,
    };

    /// <summary>The resource key of a target's title, <c>Eclipse_Obj_&lt;name&gt;</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Eclipse_Obj_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Eclipse_Obj_" + name : null;

    /// <summary>Highlights the diagram draws as a glow along a line rather than a ring round a target.</summary>
    private static bool IsLineHighlight(string id) => id is "orbit" or "nodeline" or "season" or "moonorbit";

    private bool Glows(string id) => Highlight == id;

    /// <summary>A label drawn with its top left at <paramref name="topLeft"/>, as a target.</summary>
    private void AddTextTarget(string id, FormattedText text, Point topLeft) =>
        _targets.AddBox(id, new Rect(topLeft.X - 2, topLeft.Y - 1, text.Width + 4, text.Height + 2));

    /// <summary>A pill drawn with <see cref="LabelCache.DrawPill"/> at <paramref name="topLeft"/>, as a target.</summary>
    private void AddPillTarget(string id, string key, Point topLeft, double size, Color color)
    {
        var text = _labels.Get(key, size, color);
        _targets.AddBox(id, new Rect(topLeft.X, topLeft.Y, text.Width + 12, text.Height + 4));
    }

    #region dragging and clicking

    /// <summary>
    /// Pressing on a thing waits to see whether it is a click, which explains it, or the start of a drag; pressing
    /// anywhere else starts dragging at once.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(this);

        var point = e.GetPosition(this);
        _pressPoint = point;
        _pressed = _targets.Find(point)?.Id;
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
            BeginDrag(_pressPoint);
        }

        if (_dragging) DragTo(point);
        else if (_pressed is null) _targets.Hover = _targets.Find(point)?.Id;
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
        _dragStartX = point.X;
        _dragStartDay = _day;
        SetCurrentValue(IsPlayingProperty, false);
        DragTo(point);
    }

    /// <summary>
    /// Solar and lunar view: the Moon goes to the point of its orbit in the direction of the mouse as seen from Earth.
    /// Seasons view: across the whole width is a year.
    /// </summary>
    private void DragTo(Point point)
    {
        if (Mode == EclipseMode.Seasons)
        {
            PushDay(Math.Max(0, _dragStartDay + ((point.X - _dragStartX) / Math.Max(ActualWidth, 1) * EclipseSeasons.Year)));
            return;
        }

        var x = (point.X - _origin.X) / _scale;
        var y = (_origin.Y - point.Y) / _scale;
        if ((x * x) + (y * y) < 4) return;

        // The Moon sits at (−r cos a, −r sin a): new moon (0°) towards the Sun on the left.
        PushAngle(Math.Atan2(-y, -x) * 180 / Math.PI);
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>A click on a thing: go to its moment where it has one, and show (and say) what it is.</summary>
    private void Pick(string id)
    {
        Interacted();
        switch (id)
        {
            case "newmoon":
                GoToAngle(0);
                break;
            case "fullmoon":
                GoToAngle(180);
                break;
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    private void GoToAngle(double degrees)
    {
        SetCurrentValue(IsPlayingProperty, false);
        PushAngle(degrees);
    }

    private void PushAngle(double degrees)
    {
        _angle = degrees - (360 * Math.Floor(degrees / 360));
        _pushing = true;
        try
        {
            SetCurrentValue(MoonAngleProperty, _angle);
        }
        finally
        {
            _pushing = false;
        }
    }

    private void PushDay(double day)
    {
        _day = day;
        _pushing = true;
        try
        {
            SetCurrentValue(SeasonDayProperty, _day);
        }
        finally
        {
            _pushing = false;
        }
    }

    #endregion
}
