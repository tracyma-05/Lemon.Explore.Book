using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.WaterCycle;

/// <summary>
/// The things in the water-cycle diagram that can be clicked for an explanation (every way water moves, the freezing
/// level, the rain shadow, the rising parcel, the cloud base, the balls of water, the yearly flows…) and the
/// walkthrough's highlight. Clicking a way of moving still lights it up (<see cref="SelectedProcess"/>).
/// </summary>
public sealed partial class WaterCycleDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks a thing, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, bool>(nameof(IsLessonActive), false);

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

    /// <summary>The target id of a way of moving's label.</summary>
    internal static string ProcessId(WaterProcess process) => "p-" + process;

    /// <summary>
    /// The thing a target id stands for, which names its narration clip (<see cref="ObjectClipKey"/>): a way of moving
    /// (<c>p-Evaporation</c>), a ball of water (<c>b-All</c>), a yearly flow (<c>f-Runoff</c>), or a thing of its own.
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        null => null,
        _ when id.StartsWith("p-", StringComparison.Ordinal) && Enum.TryParse<WaterProcess>(id[2..], out var p) && p != WaterProcess.None => p.ToString(),
        "b-All" => "BallAll",
        "b-FreshLiquid" => "BallFreshLiquid",
        "b-LakesRivers" => "BallLakesRivers",
        "f-OceanEvaporation" => "FlowOceanEvaporation",
        "f-OceanPrecipitation" => "FlowOceanPrecipitation",
        "f-VapourToLand" => "FlowVapourToLand",
        "f-LandPrecipitation" => "FlowLandPrecipitation",
        "f-LandEvaporation" => "FlowLandEvaporation",
        "f-Runoff" => "FlowRunoff",
        "sun" => "Sun",
        "freezing" => "Freezing",
        "snowline" => "SnowLine",
        "rainshadow" => "RainShadow",
        "drop" => "Drop",
        "thermal" => "Thermal",
        "parcel" => "Parcel",
        "cloudbase" => "CloudBase",
        "wind" => "Wind",
        "leeward" => "Leeward",
        "front" => "Front",
        "earth" => "Earth",
        "atmosphere" => "Atmosphere",
        "oceans" => "Oceans",
        "land" => "Land",
        _ => null,
    };

    /// <summary>
    /// The resource key of a target's title: the page's own name for a way of moving, a ball, a flow or a store, else
    /// <c>Water_Obj_&lt;name&gt;</c>.
    /// </summary>
    internal static string? ObjectTitleKey(string? id)
    {
        if (ObjectName(id) is not { } name) return null;
        return id![..2] switch
        {
            "p-" => "Water_Process_" + id[2..],
            "b-" => "Water_Ball_" + id[2..],
            "f-" => "Water_Flow_" + id[2..],
            _ when id is "atmosphere" or "oceans" or "land" => "Water_Store_" + name,
            _ => "Water_Obj_" + name,
        };
    }

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Water_Obj_" + name : null;

    /// <summary>Nothing here is glowed along a line: every highlight is a ring.</summary>
    private static bool IsLineHighlight(string id) => false;

    /// <summary>A text or pill already drawn in <paramref name="box"/>, as a target.</summary>
    private void AddBoxTarget(string id, Rect box)
    {
        box = box.Inflate(new Thickness(2, 1));
        _targets.AddBox(id, box);
    }

    #region clicking

    /// <summary>Pressing on a thing picks it when the button comes up over it.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var target = _targets.Find(e.GetPosition(this))?.Id;
        if (target is null) return;
        e.Pointer.Capture(this);
        _pressed = target;
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pressed is not null) return;
        var hover = _targets.Find(e.GetPosition(this))?.Id;
        _targets.Hover = hover;
        Cursor = hover is not null ? new Cursor(StandardCursorType.Hand) : null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var picked = _pressed;
        _pressed = null;
        e.Pointer.Capture(null);
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
        _pressed = null;
    }

    /// <summary>
    /// A click on a thing: show (and say) what it is. A way of moving also lights up, and clicking the lit one again puts
    /// all of them back, as before.
    /// </summary>
    private void Pick(string id)
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);

        if (id.StartsWith("p-", StringComparison.Ordinal) && Enum.TryParse<WaterProcess>(id[2..], out var process))
        {
            var again = process == SelectedProcess;
            Push(SelectedProcessProperty, again ? WaterProcess.None : process);
            if (again)
            {
                SetCurrentValue(SelectedObjectProperty, null);
                return;
            }
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
