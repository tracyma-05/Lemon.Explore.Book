using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Greenhouse;

/// <summary>
/// The things in the greenhouse diagram that can be clicked for an explanation (the Sun, the ground, the gases, the
/// energy flows, the molecule's wiggles, the spectrum, the greenhouse and the car) and the walkthrough's highlight.
/// Clicking a wiggle on the spectrum also shows it; clicking the vents or the car's window still opens or shuts them;
/// dragging sideways still turns the glass-house day.
/// </summary>
public sealed partial class GreenhouseDiagram
{
    private void OnPropertyChangedObjects(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == TitleHeightProperty)
        {
            _planetLayout = null;
        }
    }

    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<GreenhouseDiagram>(TitleHeightProperty);
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

    /// <summary>The bottom room last laid out with, so the planet's cached layout follows the walkthrough's reserve.</summary>
    private double _laidOutReserve;

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    /// <summary>The target id of a gas (a molecule in the air, or the big one).</summary>
    internal static string GasId(Gas gas) => "gas-" + gas;

    /// <summary>The target id of a way of wiggling (a <see cref="Vibration.Id"/>).</summary>
    internal static string VibrationTargetId(string vibration) => "vib-" + vibration;

    /// <summary>The target id of an energy flow of the budget.</summary>
    internal static string BudgetId(string flow) => "b-" + flow;

    private static readonly HashSet<string> Vibrations =
        ["Stretch", "SymmetricStretch", "AsymmetricStretch", "Bend", "SymmetricBend", "AsymmetricBend", "Spin"];

    private static readonly HashSet<string> Flows =
        ["In", "Surface", "Reflected", "AirSun", "Glow", "Back", "Window", "FromAir", "Rising"];

    /// <summary>
    /// The thing a target id stands for, which names its narration clip (<see cref="ObjectClipKey"/>): a gas
    /// (<c>gas-CO2</c>), a wiggle (<c>vib-Bend</c>), an energy flow (<c>b-Back</c>), or a thing of its own.
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        null => null,
        _ when id.StartsWith("gas-", StringComparison.Ordinal) && Enum.TryParse<Gas>(id[4..], out var gas) && gas != Gas.O3 => "Gas" + gas,
        _ when id.StartsWith("vib-", StringComparison.Ordinal) && Vibrations.Contains(id[4..]) => "Vib" + id[4..],
        _ when id.StartsWith("b-", StringComparison.Ordinal) && Flows.Contains(id[2..]) => "Budget" + id[2..],
        "sun" => "Sun",
        "space" => "Space",
        "air" => "Air",
        "clouds" => "Clouds",
        "ground" => "Ground",
        "emission" => "Emission",
        "profile" => "Profile",
        "sunlight" => "Sunlight",
        "infrared" => "Infrared",
        "dipole" => "Dipole",
        "groundrays" => "GroundRays",
        "spectrum" => "Spectrum",
        "window" => "Window",
        "greenhouse" => "Greenhouse",
        "vents" => "Vents",
        "car" => "Car",
        "carwindow" => "CarWindow",
        "outside" => "Outside",
        "pane" => "Pane",
        "floor" => "Floor",
        "warmair" => "WarmAir",
        _ => null,
    };

    /// <summary>The resource key of a target's title: the page's own name for a gas, else <c>Green_Obj_&lt;name&gt;</c>.</summary>
    internal static string? ObjectTitleKey(string? id)
    {
        if (ObjectName(id) is not { } name) return null;
        return id!.StartsWith("gas-", StringComparison.Ordinal) ? "Green_Gas_" + id[4..] : "Green_Obj_" + name;
    }

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Green_Obj_" + name : null;

    /// <summary>Nothing here is glowed along a line: every highlight is a ring.</summary>
    private static bool IsLineHighlight(string id) => false;

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
    /// Pressing on a thing waits: moving more than a few pixels turns the glass-house day as before, coming up over the thing
    /// picks it. Pressing anywhere else in the glass house starts turning the day at once, as before.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragStart = e.GetPosition(this);
        _dragMoved = false;
        _pressed = _targets.Find(_dragStart)?.Id;
        if (_pressed is null && Mode != GreenhouseMode.GlassHouse) return;
        e.Pointer.Capture(this);

        if (_pressed is null) StartDrag();
        e.Handled = true;
    }

    private void StartDrag()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        _dragging = true;
        _dragStartHour = _hour;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_pressed is not null)
        {
            if (Mode != GreenhouseMode.GlassHouse || ((Vector)(point - _dragStart)).Length <= 4) return;
            _pressed = null;
            StartDrag();
        }

        if (!_dragging)
        {
            var hover = _targets.Find(point)?.Id;
            _targets.Hover = hover;
            Cursor = hover is not null ? new Cursor(StandardCursorType.Hand) : Mode == GreenhouseMode.GlassHouse ? new Cursor(StandardCursorType.SizeWestEast) : null;
            return;
        }

        var dx = point.X - _dragStart.X;
        if (Math.Abs(dx) > 2) _dragMoved = true;

        // A whole day across most of the picture.
        _hour = (((_dragStartHour + (dx * 24 / Math.Max(300, ActualWidth * 0.9))) % 24) + 24) % 24;
        Push(HourProperty, Math.Round(_hour, 4));
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

        // A drag holds the day where it was left.
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

    /// <summary>
    /// A click on a thing: show (and say) what it is. A wiggle on the spectrum is also shown; the vents and the car's
    /// window open or shut, as before.
    /// </summary>
    private void Pick(string id)
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);

        if (id.StartsWith("vib-", StringComparison.Ordinal) && Mode == GreenhouseMode.Molecules)
        {
            SetCurrentValue(VibrationIdProperty, id[4..]);
        }
        else if (id == "vents")
        {
            SetCurrentValue(VentsOpenProperty, !VentsOpen);
        }
        else if (id == "carwindow")
        {
            SetCurrentValue(CarWindowOpenProperty, !CarWindowOpen);
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
