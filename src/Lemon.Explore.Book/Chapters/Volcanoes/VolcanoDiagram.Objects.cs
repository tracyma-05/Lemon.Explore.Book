using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Volcanoes;

/// <summary>
/// The things in the volcanoes and rocks diagram that can be clicked for an explanation, the walkthrough's highlight, and
/// the hands-on parts: the vent erupts now and the chamber fills again; a crystal, a grain or a flake under the magnifier
/// says what it is; the river pours mud into the tank; dragging sideways runs time back and forth, and dragging the press
/// or the rock piled on the layers buries them deeper.
/// </summary>
public sealed partial class VolcanoDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<VolcanoDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<VolcanoDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<VolcanoDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<VolcanoDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<VolcanoDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<VolcanoDiagram>(TitleHeightProperty);
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

        /// <summary>Running time back and forth: the round, the journey or the cooling.</summary>
        Scrub,

        /// <summary>Pushing the press (squeezing) or the rock on the layers (settling) up and down: the depth.</summary>
        Depth,
    }

    private DragKind _drag;

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    private Point _dragStart;
    private double _dragStartValue;
    private bool _dragMoved;

    /// <summary>Target ids and the names they stand for (which name their title <c>Volc_Obj_X</c> and clip <c>Narr_Volc_Obj_X</c>).</summary>
    private static readonly Dictionary<string, string> Names = new()
    {
        ["chamber"] = "Chamber",
        ["conduit"] = "Conduit",
        ["bubbles"] = "Bubbles",
        ["satlevel"] = "SatLevel",
        ["fraglevel"] = "FragLevel",
        ["vent"] = "Vent",
        ["cone"] = "Cone",
        ["lavaflow"] = "LavaFlow",
        ["fountain"] = "Fountain",
        ["bombs"] = "Bombs",
        ["column"] = "Column",
        ["umbrella"] = "Umbrella",
        ["ashfall"] = "AshFall",
        ["pyroflow"] = "PyroFlow",
        ["dome"] = "Dome",
        ["gauge"] = "Gauge",
        ["quakes"] = "Quakes",
        ["proc_weathering"] = "Weathering",
        ["proc_erosion"] = "Erosion",
        ["proc_deposition"] = "Deposition",
        ["proc_lithification"] = "Lithification",
        ["proc_uplift"] = "Uplift",
        ["proc_metamorphism"] = "Metamorphism",
        ["proc_melting"] = "Melting",
        ["proc_crystallization"] = "Crystallization",
        ["proc_eruption"] = "EruptionStep",
        ["rock_granite"] = "Granite",
        ["rock_basalt"] = "Basalt",
        ["rock_sandstone"] = "Sandstone",
        ["rock_shale"] = "Shale",
        ["rock_limestone"] = "Limestone",
        ["rock_marble"] = "Marble",
        ["rock_schist"] = "Schist",
        ["rock_magma"] = "Magma",
        ["rock_sediment"] = "Sediment",
        ["grain"] = "Grain",
        ["min_plagioclase"] = "Plagioclase",
        ["min_biotite"] = "Biotite",
        ["min_kfeldspar"] = "KFeldspar",
        ["min_quartz"] = "Quartz",
        ["min_olivine"] = "Olivine",
        ["min_pyroxene"] = "Pyroxene",
        ["glass"] = "Glass",
        ["vesicle"] = "Vesicle",
        ["melt"] = "Melt",
        ["thermo"] = "Thermometer",
        ["river"] = "River",
        ["g_gravel"] = "Gravel",
        ["g_sand"] = "SandGrain",
        ["g_silt"] = "Silt",
        ["g_clay"] = "Clay",
        ["layers"] = "Layers",
        ["overburden"] = "Overburden",
        ["press"] = "Press",
        ["mica"] = "Mica",
        ["garnet"] = "Garnet",
        ["bands"] = "Bands",
        ["meltpocket"] = "MeltPocket",
        ["grains"] = "Grains",
        ["fossil"] = "Fossil",
    };

    /// <summary>Every target id the diagram can register.</summary>
    internal static IEnumerable<string> TargetIds => Names.Keys;

    /// <summary>The thing a target id stands for (<c>min_quartz</c> → <c>Obj_Quartz</c>). It names the title and the narration clip.</summary>
    internal static string? ObjectName(string? id) => id is not null && Names.TryGetValue(id, out var name) ? "Obj_" + name : null;

    /// <summary>The resource key of a target's title: <c>Volc_Obj_Quartz</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Volc_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Volc_" + name : null;

    /// <summary>The target id of a mineral: <c>min_quartz</c>.</summary>
    internal static string MineralId(Mineral mineral) => "min_" + mineral.Id.ToLowerInvariant();

    /// <summary>The target id of a grain size in the tank: <c>g_sand</c>.</summary>
    internal static string GrainId(byte size) => size switch
    {
        SedimentSim.Gravel => "g_gravel",
        SedimentSim.Sand => "g_sand",
        SedimentSim.Silt => "g_silt",
        _ => "g_clay",
    };

    /// <summary>The target id of a rock-cycle step: <c>proc_weathering</c>.</summary>
    internal static string ProcessId(RockProcess process) => "proc_" + process.ToString().ToLowerInvariant();

    /// <summary>Things the diagram glows itself; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "chamber" or "conduit" or "cone" or "lavaflow" or "pyroflow" or "river" or "press" or "column";

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
        _dragMoved = false;
        _pressed = _targets.Find(_dragStart)?.Id ?? PickInPicture(_dragStart);
        e.Pointer.Capture(this);

        e.Handled = true;
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
                _dragStartValue = _drag == DragKind.Depth ? DepthValue() : Progress;
                break;
        }

        var stage = Stage();
        if (_drag == DragKind.Scrub)
        {
            var span = Mode == VolcanoMode.Eruption ? stage.Width : stage.Width * 0.8;
            var value = _dragStartValue + ((point.X - _dragStart.X) / span);
            SetCurrentValue(ProgressProperty, Mode == VolcanoMode.Eruption ? value - Math.Floor(value) : Math.Clamp(value, 0, 1));
        }
        else if (_drag == DragKind.Depth)
        {
            var dy = point.Y - _dragStart.Y;
            if (Process == MagnifierProcess.Squeezing) SetCurrentValue(SqueezeDepthKmProperty, Math.Clamp(_dragStartValue + (dy / stage.Height * 40), 0, 30));
            else SetCurrentValue(BurialKmProperty, Math.Clamp(_dragStartValue + (dy / stage.Height * 6), 0, 4));
        }

        InvalidateVisual();
    }

    private double DepthValue() => Process == MagnifierProcess.Squeezing ? SqueezeDepthKm : BurialKm;

    /// <summary>What a press at <paramref name="point"/> drags once it moves.</summary>
    private DragKind DragAt(Point point)
    {
        switch (Mode)
        {
            case VolcanoMode.Eruption:
                return DragKind.Scrub;

            case VolcanoMode.Cycle:
                return JourneyId is null ? DragKind.None : DragKind.Scrub;

            default:
                if (Process == MagnifierProcess.Cooling) return DragKind.Scrub;
                if (Process == MagnifierProcess.Squeezing) return _squeezeSample.Contains(point) || _pressPlate.Contains(point) ? DragKind.Depth : DragKind.None;
                return _overburden.Contains(point) && _overburden != default ? DragKind.Depth : DragKind.None;
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
        if (drag != DragKind.Pending || _dragMoved || picked is null) return;

        var now = _targets.Find(point)?.Id ?? PickInPicture(point);
        if (now == picked) Pick(picked);
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
        if (hover is not null || PickInPicture(point) is not null)
        {
            Cursor = new Cursor(StandardCursorType.Hand);
            return;
        }

        Cursor = DragAt(point) switch
        {
            DragKind.Scrub => new Cursor(StandardCursorType.SizeWestEast),
            DragKind.Depth => new Cursor(StandardCursorType.SizeNorthSouth),
            _ => null,
        };
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>
    /// A thing inside a picture that is not a target of its own: the crystal, glass or bubble under the mouse in the cooling
    /// magma, the grain size of the bed in the tank, a garnet, a band or the flakes of the squeezed rock.
    /// </summary>
    private string? PickInPicture(Point point)
    {
        if (Mode != VolcanoMode.Magnifier) return null;
        return Process switch
        {
            MagnifierProcess.Cooling => CoolingThingAt(point),
            MagnifierProcess.Settling => BedThingAt(point),
            _ => SqueezeThingAt(point),
        };
    }

    /// <summary>
    /// A click on a thing: show (and say) what it is, and do what it does: the vent erupts now, the chamber fills again
    /// once the eruption is over, the river pours a load of mud into the tank.
    /// </summary>
    private void Pick(string id)
    {
        Interacted();
        switch (id)
        {
            case "vent" when VolcanoModel.PhaseAt(Progress) is EruptionPhase.Recharge or EruptionPhase.Rising:
                SetCurrentValue(ProgressProperty, VolcanoModel.EruptionStart);
                SetCurrentValue(IsPlayingProperty, true);
                break;

            case "chamber" when VolcanoModel.PhaseAt(Progress) == EruptionPhase.Calm:
                SetCurrentValue(ProgressProperty, 0.0);
                SetCurrentValue(IsPlayingProperty, true);
                break;

            case "river" when Mode == VolcanoMode.Magnifier:
                Tank?.Pour();
                SetCurrentValue(IsPlayingProperty, true);

                // More pours just pour; what the river is was said the first time.
                if (SelectedObject == id) return;
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
