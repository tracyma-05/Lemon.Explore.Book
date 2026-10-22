using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.SeaBreeze;

/// <summary>
/// The things in the breeze diagram that can be clicked for an explanation (the sea and the land, the arrows of the
/// circulation, highs and lows, the flag, the smoke, the front; on the map the cities, the pressure centres and the
/// monsoons) and the walkthrough's highlight. Clicking a city on the map still picks it (<see cref="City"/>).
/// </summary>
public sealed partial class BreezeDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<BreezeDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<BreezeDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks a thing, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<BreezeDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<BreezeDiagram, bool>(nameof(IsLessonActive), false);

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

    /// <summary>The target id of a city on the map.</summary>
    internal static string CityId(MonsoonCity city) => "c-" + city;

    /// <summary>The target id of a pressure centre on the map, by its <see cref="PressureSystem.Key"/>.</summary>
    internal static string SystemId(string key) => "s-" + key;

    /// <summary>The target id of a named monsoon arrow on the map (<c>SW</c>, <c>NE</c>, <c>Australia</c>…).</summary>
    internal static string MonsoonId(string name) => "m-" + name;

    /// <summary>
    /// The thing a target id stands for, which names its narration clip (<see cref="ObjectClipKey"/>): a city
    /// (<c>c-Mumbai</c>), a pressure centre (<c>s-Siberian</c>), a monsoon (<c>m-SW</c>), or a thing of its own.
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        null => null,
        _ when id.StartsWith("c-", StringComparison.Ordinal) && Enum.TryParse<MonsoonCity>(id[2..], out var city) => city.ToString(),
        "s-Siberian" => "Siberian",
        "s-AsianLow" => "AsianLow",
        "s-Mascarene" => "Mascarene",
        "s-Australia" => "Australia",
        "s-PacificRidge" => "PacificRidge",
        "m-SW" => "MonsoonSW",
        "m-SE" => "MonsoonSE",
        "m-NW" => "MonsoonNW",
        "m-NE" => "MonsoonNE",
        "m-Australia" => "MonsoonAustralia",
        "sea" => "Sea",
        "land" => "Land",
        "seabreeze" => "SeaBreeze",
        "landbreeze" => "LandBreeze",
        "rising" => "Rising",
        "sinking" => "Sinking",
        "return" => "Return",
        "calm" => "Calm",
        "isobars" => "Isobars",
        "high" => "High",
        "low" => "Low",
        "sun" => "Sun",
        "cooling" => "Cooling",
        "front" => "Front",
        "flag" => "Flag",
        "smoke" => "Smoke",
        "boat" => "Boat",
        "scale" => "Scale",
        "shower" => "Shower",
        "trough" => "Trough",
        "equator" => "Equator",
        _ => null,
    };

    /// <summary>
    /// The resource key of a target's title: the page's own name where it has one (a city, a named high or low, a monsoon,
    /// the labels of the cell), else <c>Breeze_Obj_&lt;name&gt;</c>.
    /// </summary>
    internal static string? ObjectTitleKey(string? id)
    {
        if (ObjectName(id) is not { } name) return null;
        return id switch
        {
            _ when id!.StartsWith("c-", StringComparison.Ordinal) => "Breeze_City_" + name,
            _ when id.StartsWith("m-", StringComparison.Ordinal) => "Breeze_Monsoon_" + id[2..],
            "s-Siberian" => "Breeze_System_Siberian_High",
            "s-AsianLow" => "Breeze_System_AsianLow_Low",
            "s-Mascarene" => "Breeze_System_Mascarene_High",
            "s-PacificRidge" => "Breeze_System_PacificRidge_High",
            "sea" => "Breeze_Sea",
            "land" => "Breeze_Land",
            "seabreeze" => "Breeze_SeaBreeze",
            "landbreeze" => "Breeze_LandBreeze",
            "rising" => "Breeze_Rising",
            "sinking" => "Breeze_Sinking",
            "return" => "Breeze_Return",
            "calm" => "Breeze_Calm",
            "isobars" => "Breeze_Isobar",
            "trough" => "Breeze_Trough",
            "equator" => "Breeze_Equator",
            _ => "Breeze_Obj_" + name,
        };
    }

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Breeze_Obj_" + name : null;

    /// <summary>The pressure surfaces are glowed along their lines; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id == "isobars";

    /// <summary>Whether the walkthrough points at <paramref name="id"/>, which the diagram glows itself.</summary>
    private bool Glows(string id) => Highlight == id;

    /// <summary>A text or pill already drawn in <paramref name="box"/>, as a target.</summary>
    private void AddBoxTarget(string id, Rect box)
    {
        box = box.Inflate(new Thickness(2, 1));
        _targets.AddBox(id, box);
    }

    /// <summary>A curved arrow's middle (the quadratic's point at one half), for its target.</summary>
    private static Point CurveMiddle(Point from, Point control, Point to) =>
        new((0.25 * from.X) + (0.5 * control.X) + (0.25 * to.X), (0.25 * from.Y) + (0.5 * control.Y) + (0.25 * to.Y));

    /// <summary>Over the finished frame: the selected, hovered and highlighted things.</summary>
    private void DrawObjectOverlays(PaintContext dc, double width) =>
        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, _clock.Elapsed.TotalSeconds, width);

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

    /// <summary>A click on a thing: show (and say) what it is. A city is also picked for the rain chart, as before.</summary>
    private void Pick(string id)
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);

        if (id.StartsWith("c-", StringComparison.Ordinal) && Enum.TryParse<MonsoonCity>(id[2..], out var city))
        {
            SetCurrentValue(CityProperty, city);
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
