using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Spectra;

/// <summary>
/// The things in the spectra diagram that can be clicked for an explanation, the walkthrough's highlight, and the hands-on
/// parts: a click on a level of the atom lifts the electron there (above the top sets it free); a click on a spectrum picks
/// the line there; the star's curve peak drags its temperature and its spectrum drags its speed.
/// </summary>
public sealed partial class SpectraDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<SpectraDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<SpectraDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<SpectraDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<SpectraDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<SpectraDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<SpectraDiagram>(TitleHeightProperty);
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

        /// <summary>The button is down; moving far enough starts one of the drags below, else it is a click.</summary>
        Pending,
        Peak,
        StarSpectrum,
    }

    private DragKind _drag;

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    private Point _dragStart;
    private bool _dragMoved;
    private double _dragVelocity;

    /// <summary>The thing a target id stands for (<c>level3</c> → <c>Obj_Level</c>). It names the title and the narration clip.</summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "nucleus" => "Obj_Nucleus",
        "electron" => "Obj_Electron",
        "orbit" => "Obj_Orbit",
        "level1" => "Obj_Ground",
        "level2" or "level3" or "level4" or "level5" or "level6" => "Obj_Level",
        "continuum" => "Obj_Ionise",
        "photonin" => "Obj_PhotonIn",
        "photonout" => "Obj_PhotonOut",
        "lamp" => "Obj_PhotonLamp",
        "hspectrum" => "Obj_HSpectrum",
        "uv" => "Obj_Ultraviolet",
        "ir" => "Obj_Infrared",
        "cloud" => "Obj_Cloud",
        "energy" => "Obj_Energy",
        "gaslamp" => "Obj_GasLamp",
        "bulb" => "Obj_Bulb",
        "sun" => "Obj_SunLight",
        "coolgas" => "Obj_CoolGas",
        "slit" => "Obj_Slit",
        "prism" => "Obj_Prism",
        "line" => "Obj_Line",
        "darkline" => "Obj_DarkLine",
        "trace" => "Obj_Trace",
        "ruler" => "Obj_Nanometre",
        "compare" => "Obj_Compare",
        "zoom" => "Obj_Zoom",
        "mystery" => "Obj_Mystery",
        "star" => "Obj_Star",
        "peak" => "Obj_Peak",
        "curve" => "Obj_Planck",
        "visible" => "Obj_Visible",
        "starspectrum" => "Obj_StarSpectrum",
        "balmer" => "Obj_BalmerDark",
        "calcium" => "Obj_Calcium",
        "helium" => "Obj_Helium",
        "bands" => "Obj_Molecules",
        "reference" => "Obj_Reference",
        "shift" => "Obj_Shift",
        _ => null,
    };

    /// <summary>The resource key of a target's title: <c>Spec_Obj_Nucleus</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Spec_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Spec_" + name : null;

    /// <summary>Lines and curves are glowed along their length by the diagram; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "orbit" or "curve";

    /// <summary>Whether the walkthrough points at <paramref name="id"/>, which the diagram glows itself.</summary>
    private bool Glows(string id) => Highlight == id;

    /// <summary>A text, a pill or a part already drawn in <paramref name="box"/>, as a target.</summary>
    private void AddBoxTarget(string id, Rect box)
    {
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
        _pressed = _targets.Find(_dragStart)?.Id;
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
                _pressed = null;
                _drag = DragAt(_dragStart);
                if (_drag == DragKind.None) return;
                _dragVelocity = Velocity;
                Interacted();
                break;
        }

        DragTo(point);
        InvalidateVisual();
    }

    /// <summary>What a press at <paramref name="point"/> drags once it moves.</summary>
    private DragKind DragAt(Point point)
    {
        if (Mode != SpectraMode.Stars) return DragKind.None;
        var v = _stars;
        if (v.Chart.Inflate(new Thickness(4, 4)).Contains(point)) return DragKind.Peak;
        if (v.StarStrip.Inflate(new Thickness(0, 6)).Contains(point)) return DragKind.StarSpectrum;
        return DragKind.None;
    }

    private void DragTo(Point point)
    {
        switch (_drag)
        {
            case DragKind.Peak:
            {
                // The curve's peak follows the mouse: Wien's law gives the temperature.
                var nm = _stars.ChartNm(point.X);
                SetCurrentValue(KelvinProperty, Math.Round(Math.Clamp(SpectraModel.PeakWavelength(1) / nm, 2_500, 40_000) / 10) * 10);
                break;
            }

            case DragKind.StarSpectrum:
            {
                // Dragging the spectrum sideways shifts it: the speed that moves its middle that far.
                var middle = (SpectraModel.VisibleFrom + SpectraModel.VisibleTo) / 2;
                var seen = middle + ((point.X - _dragStart.X) / _stars.StarStrip.Width * (SpectraModel.VisibleTo - SpectraModel.VisibleFrom));
                var start = SpectraModel.Shift(middle, _dragVelocity);
                var speed = SpectraModel.SpeedFromShift(middle, seen + (start - middle));
                SetCurrentValue(VelocityProperty, Math.Round(Math.Clamp(speed, -30_000, 30_000) / 50) * 50);
                break;
            }
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

        if (picked is not null)
        {
            if (_targets.Find(point)?.Id == picked) Pick(picked);
            return;
        }

        ClickEmpty(point);
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
        Cursor = hover is not null ? new Cursor(StandardCursorType.Hand)
            : DragAt(point) != DragKind.None ? new Cursor(StandardCursorType.SizeWestEast)
            : Mode == SpectraMode.Lamps && _lamps.Strip.Inflate(new Thickness(0, 8)).Contains(point) ? new Cursor(StandardCursorType.Hand)
            : null;
    }

    /// <summary>A click on nothing in particular: on a lamp's spectrum, pick the line (or the colour) there.</summary>
    private void ClickEmpty(Point point)
    {
        if (Mode != SpectraMode.Lamps || !_lamps.Strip.Inflate(new Thickness(0, 8)).Contains(point)) return;
        var nm = _lamps.Nm(point.X);
        var lines = ShownLines();
        if (lines.Count > 0)
        {
            var nearest = lines.MinBy(l => Math.Abs(l.Nm - nm));
            if (Math.Abs(_lamps.X(nearest.Nm) - point.X) < 10) nm = nearest.Nm;
        }

        Interacted();
        SetCurrentValue(SelectedLineProperty, Math.Round(nm, 2));
        Pick(Source is LightSource.Lamp ? "line" : "darkline");
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>A click on a thing: show (and say) what it is; a level of the atom also lifts the electron there.</summary>
    private void Pick(string id)
    {
        Interacted();
        if (id.StartsWith("level", StringComparison.Ordinal) && int.TryParse(id.AsSpan(5), out var n) && n != Level && _level >= 1)
        {
            SetCurrentValue(LevelProperty, n);
        }
        else if (id == "continuum" && _level >= 1)
        {
            SetCurrentValue(LevelProperty, 0);
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
