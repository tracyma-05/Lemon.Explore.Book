using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using System.Windows.Input;
using Lemon.Book.Shell.Controls.Explore;
using Lemon.Explore.Book.Controls.Explore;

namespace Lemon.Explore.Book.Chapters.StatesOfMatter;

/// <summary>
/// The things in the states of matter diagram that can be clicked for an explanation, the walkthrough's highlight, and
/// the hands-on parts: dragging the thermometer warms or cools the cylinder and dragging the piston squeezes it; the
/// burner's flame turns up, the place pill goes to the next place, and dragging along the heating curve sets how much
/// heat has gone in; on the phase diagram the dot is dragged anywhere, and a marked place moves it there.
/// </summary>
public sealed partial class MatterDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<MatterDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<MatterDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<MatterDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<MatterDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<MatterDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<MatterDiagram>(TitleHeightProperty);
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

        /// <summary>The button is down; moving far enough starts a drag, else it is a click.</summary>
        Pending,

        /// <summary>The thermometer's column follows the mouse up and down: the temperature.</summary>
        Thermometer,

        /// <summary>The piston follows the mouse: up lets the pressure off, down squeezes.</summary>
        Piston,

        /// <summary>Following the mouse along the heating curve: the heat under it.</summary>
        Scrub,

        /// <summary>The phase diagram's dot follows the mouse.</summary>
        Dot,
    }

    private DragKind _drag;

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    private Point _dragStart;
    private double _dragStartLog;
    private bool _dragMoved;

    /// <summary>Every id the diagram can register, for the tests and the walkthrough's highlights.</summary>
    internal static readonly string[] TargetIds =
    [
        "cylinder", "piston", "heater", "thermometer", "gauge", "molecule", "state",
        "beaker", "flame", "window", "curve", "place", "melt", "boil", "sublime",
        "plot", "solid", "liquid", "gas", "supercritical", "meltline", "boilline", "subline", "triple", "critical", "atm", "dot",
        "lm_everest", "lm_cooker", "lm_mars", "lm_dryice", "lm_marspole", "lm_extinguisher",
    ];

    /// <summary>The thing a target id stands for (<c>lm_everest</c> → <c>Obj_Everest</c>). It names the title and the narration clip.</summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "cylinder" => "Obj_Cylinder",
        "piston" => "Obj_Piston",
        "heater" => "Obj_Heater",
        "thermometer" => "Obj_Thermometer",
        "gauge" => "Obj_Gauge",
        "molecule" => "Obj_Molecule",
        "state" => "Obj_State",
        "beaker" => "Obj_Beaker",
        "flame" => "Obj_Flame",
        "window" => "Obj_Window",
        "curve" => "Obj_Curve",
        "place" => "Obj_Place",
        "melt" => "Obj_Melt",
        "boil" => "Obj_Boil",
        "sublime" => "Obj_Sublime",
        "plot" => "Obj_Plot",
        "solid" => "Obj_Solid",
        "liquid" => "Obj_Liquid",
        "gas" => "Obj_Gas",
        "supercritical" => "Obj_Supercritical",
        "meltline" => "Obj_MeltLine",
        "boilline" => "Obj_BoilLine",
        "subline" => "Obj_SubLine",
        "triple" => "Obj_Triple",
        "critical" => "Obj_Critical",
        "atm" => "Obj_Atm",
        "dot" => "Obj_Dot",
        "lm_everest" => "Obj_Everest",
        "lm_cooker" => "Obj_Cooker",
        "lm_mars" => "Obj_Mars",
        "lm_dryice" => "Obj_DryIce",
        "lm_marspole" => "Obj_MarsPole",
        "lm_extinguisher" => "Obj_Extinguisher",
        _ => null,
    };

    /// <summary>The resource key of a target's title: <c>Matter_Obj_Piston</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Matter_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Matter_" + name : null;

    /// <summary>Things the diagram glows itself; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "cylinder" or "thermometer" or "window" or "curve" or "plot"
        or "meltline" or "boilline" or "subline" or "atm";

    /// <summary>Whether the walkthrough points at <paramref name="id"/>, which the diagram glows itself.</summary>
    private bool Glows(string id) => Highlight == id;

    /// <summary>A text, a pill or a part already drawn in <paramref name="box"/>, as a target.</summary>
    private void AddBoxTarget(string id, Rect box)
    {
        if (box == default) return;
        box = box.Inflate(new Thickness(2, 1));
        _targets.AddBox(id, box);
    }

    /// <summary>The title sits over the top of the picture: the picture starts below its last line (its top margin is 22), however it wraps.</summary>
    // The book puts the title on the left page: only a margin at the top, not the original's title band.
    private double TitleSpace => 22 + TitleHeight + 14;

    #region clicking and dragging

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragStart = e.GetPosition(this);
        _dragStartLog = PressureLog;
        _dragMoved = false;
        _pressed = _targets.Find(_dragStart)?.Id;
        e.Pointer.Capture(this);
        e.Handled = true;

        // The dot jumps to where the plot is pressed and follows from there; a marked place is a click.
        if (Mode == MatterMode.Diagram && _pressed is null or "dot" or "plot" && PlotContains(_dragStart))
        {
            Interacted();
            _drag = DragKind.Dot;
            _pressed = null;
            MoveDot(_dragStart);
            return;
        }

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
                _drag = DragAt(_dragStart);
                _pressed = null;
                if (_drag == DragKind.None) return;
                Interacted();
                if (_drag == DragKind.Scrub) SetCurrentValue(HeatProperty, HeatSetting.Off);
                break;
        }

        switch (_drag)
        {
            case DragKind.Thermometer:
                SetCurrentValue(CelsiusProperty, Math.Round(ThermometerCelsius(point.Y)));
                break;

            case DragKind.Piston:
                // Forty pixels down squeezes ten times harder.
                SetCurrentValue(PressureLogProperty, Math.Clamp(Math.Round((_dragStartLog + ((point.Y - _dragStart.Y) / 40)) * 20) / 20, 0, 8));
                break;

            case DragKind.Scrub:
                SetCurrentValue(HeatKjProperty, Math.Max(0, CurveKj(point)));
                break;

            case DragKind.Dot:
                MoveDot(point);
                break;
        }

        InvalidateVisual();
    }

    /// <summary>What a press at <paramref name="point"/> drags once it moves.</summary>
    private DragKind DragAt(Point point)
    {
        switch (Mode)
        {
            case MatterMode.Particles:
            {
                var layout = LayOutParticles();
                if (layout.Thermometer.Inflate(new Thickness(14, 8)).Contains(point)) return DragKind.Thermometer;
                return layout.PistonGrip.Inflate(new Thickness(10, 10)).Contains(point) ? DragKind.Piston : DragKind.None;
            }

            case MatterMode.Heating:
                return LayOutHeating().Plot.Inflate(new Thickness(10, 10)).Contains(point) ? DragKind.Scrub : DragKind.None;

            default:
                return PlotContains(point) ? DragKind.Dot : DragKind.None;
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

        if (picked is not null && _targets.Find(point)?.Id == picked) Pick(picked, point);
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
        var drag = DragAt(point);
        Cursor = drag switch
        {
            DragKind.Thermometer or DragKind.Piston => new Cursor(StandardCursorType.SizeNorthSouth),
            DragKind.Scrub => new Cursor(StandardCursorType.SizeWestEast),
            DragKind.Dot => new Cursor(StandardCursorType.Cross),
            _ => hover is not null ? new Cursor(StandardCursorType.Hand) : null,
        };
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>
    /// A click on a thing: show (and say) what it is, and do what it does: the flame turns up (or out), the place pill
    /// goes to the next place, the curve goes to that much heat, a marked place on the phase diagram takes the dot there.
    /// </summary>
    private void Pick(string id, Point point)
    {
        Interacted();
        switch (id)
        {
            case "flame":
                SetCurrentValue(HeatProperty, Heat switch
                {
                    HeatSetting.Off when HeatKj < MatterModel.TotalKj(Curve) => HeatSetting.Low,
                    HeatSetting.Low => HeatSetting.High,
                    _ => HeatSetting.Off,
                });
                if (SelectedObject == id) return;
                break;

            case "place":
                SetCurrentValue(PlaceProperty, (HeatingPlace)(((int)Place + 1) % Enum.GetValues<HeatingPlace>().Length));
                break;

            case "curve":
                SetCurrentValue(HeatProperty, HeatSetting.Off);
                SetCurrentValue(HeatKjProperty, Math.Max(0, CurveKj(point)));

                // Clicking along the curve again only moves along it; its explanation is already showing.
                if (SelectedObject == id) return;
                break;

            default:
                if (LandmarkState(id) is { } at)
                {
                    SetCurrentValue(CelsiusProperty, at.Celsius);
                    SetCurrentValue(PressureLogProperty, Math.Log10(at.Pascals));
                }

                break;
        }

        Select(id);
    }

    /// <summary>Shows a thing's explanation; cleared first so clicking the same thing again says it again.</summary>
    private void Select(string id)
    {
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
