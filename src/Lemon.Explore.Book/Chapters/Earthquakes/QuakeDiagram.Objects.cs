using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Earthquakes;

/// <summary>
/// The things in the earthquake diagram that can be clicked for an explanation, the walkthrough's highlight, and the
/// hands-on parts: on the map a click on a boundary picks it, on a plate picks the plate, on a star picks that quake; in
/// the sections dragging scrubs the ridge's or India's time and tilts the sinking plate; inside the Earth the quake can be
/// dragged round the rim and a click on the rim puts the station there.
/// </summary>
public sealed partial class QuakeDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<QuakeDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<QuakeDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<QuakeDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<QuakeDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<QuakeDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<QuakeDiagram>(TitleHeightProperty);
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

        /// <summary>Sideways along the ridge or the collision section: time goes back or forward.</summary>
        Scrub,

        /// <summary>Up and down on the sinking plate: steeper or shallower.</summary>
        Dip,

        /// <summary>The quake round the rim of the Earth.</summary>
        Quake,
    }

    private DragKind _drag;

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    private Point _dragStart;
    private bool _dragMoved;
    private double _dragValue;

    /// <summary>The boundary under the mouse on the map, −1 for none.</summary>
    private int _hoverBoundary = -1;

    /// <summary>The thing a target id stands for (<c>plate:PA</c> → <c>Obj_Plate</c>). It names the narration clip.</summary>
    internal static string? ObjectName(string? id)
    {
        if (id is null) return null;
        if (id.StartsWith("plate:", StringComparison.Ordinal) || id.StartsWith("group:", StringComparison.Ordinal)) return "Obj_Plate";
        return id switch
        {
            "ridge" => "Obj_Ridge",
            "trench" => "Obj_Trench",
            "collision" => "Obj_Collision",
            "transform" => "Obj_Transform",
            "rift" => "Obj_Rift",
            "diffuse" => "Obj_Diffuse",
            "depth" => "Obj_Depth",
            "hawaii" => "Obj_Hawaii",
            "place" => "Obj_Place",
            "chile1960" => "Obj_Chile1960",
            "alaska1964" => "Obj_Alaska1964",
            "sumatra2004" => "Obj_Sumatra2004",
            "tohoku2011" => "Obj_Tohoku2011",
            "bolivia1994" => "Obj_Bolivia1994",
            "sanfrancisco1906" => "Obj_SanFrancisco1906",
            "wenchuan2008" => "Obj_Wenchuan2008",
            "nepal2015" => "Obj_Nepal2015",
            "turkey2023" => "Obj_Turkey2023",
            "tangshan1976" => "Obj_Tangshan1976",
            "axis" => "Obj_Axis",
            "magma" => "Obj_Magma",
            "smoker" => "Obj_Smoker",
            "stripes" => "Obj_Stripes",
            "oldfloor" => "Obj_OldFloor",
            "lithosphere" => "Obj_Lithosphere",
            "asthenosphere" => "Obj_Asthenosphere",
            "field" => "Obj_Field",
            "oceanplate" => "Obj_OceanPlate",
            "upperplate" => "Obj_UpperPlate",
            "slab" => "Obj_Slab",
            "benioff" => "Obj_Benioff",
            "arcvolcano" => "Obj_ArcVolcano",
            "melt" => "Obj_Melt",
            "wedge" => "Obj_Wedge",
            "locked" => "Obj_Locked",
            "tsunami" => "Obj_Tsunami",
            "india" => "Obj_India",
            "asia" => "Obj_Asia",
            "tethys" => "Obj_Tethys",
            "himalaya" => "Obj_Himalaya",
            "tibet" => "Obj_Tibet",
            "suture" => "Obj_Suture",
            "slabbreak" => "Obj_SlabBreak",
            "gangdese" => "Obj_Gangdese",
            "fault" => "Obj_Fault",
            "fence" or "road" => "Obj_Fence",
            "epicentre" => "Obj_Epicentre",
            "farfield" => "Obj_FarField",
            "stress" => "Obj_Stress",
            "quake" => "Obj_Focus",
            "station" => "Obj_Station",
            "pwave" => "Obj_PWave",
            "swave" => "Obj_SWave",
            "surfacewave" => "Obj_SurfaceWave",
            "shadowp" => "Obj_PShadow",
            "shadows" => "Obj_SShadow",
            "crust" => "Obj_Crust",
            "mantle" => "Obj_Mantle",
            "outercore" => "Obj_OuterCore",
            "innercore" => "Obj_InnerCore",
            "gutenberg" => "Obj_Gutenberg",
            "moho" => "Obj_Moho",
            "pcurve" => "Obj_PCurve",
            "scurve" => "Obj_SCurve",
            _ => null,
        };
    }

    /// <summary>The resource key of a target's title: <c>Quake_Obj_Ridge</c>; a plate's title is its own name.</summary>
    internal static string? ObjectTitleKey(string? id)
    {
        if (id is not null && id.StartsWith("plate:", StringComparison.Ordinal)) return PlateKey(id[6..]);
        if (id is not null && id.StartsWith("group:", StringComparison.Ordinal)) return GroupKey(id[6..]);
        return ObjectName(id) is { } name ? "Quake_" + name : null;
    }

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Quake_" + name : null;

    /// <summary>Lines are glowed along their length by the diagram; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "gutenberg" or "pcurve" or "scurve";

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
                var pressed = _pressed;
                _pressed = null;
                _drag = DragAt(_dragStart, pressed);
                if (_drag == DragKind.None) return;
                _dragValue = _drag switch
                {
                    DragKind.Scrub => SectionTime,
                    DragKind.Dip => Dip,
                    _ => QuakeAngle,
                };
                Interacted();
                break;
        }

        DragTo(point);
        InvalidateVisual();
    }

    /// <summary>What a press at <paramref name="point"/> drags once it moves.</summary>
    private DragKind DragAt(Point point, string? pressed)
    {
        switch (Mode)
        {
            case QuakeMode.Boundaries when _section.Contains(point):
                return Section switch
                {
                    SectionKind.Ridge or SectionKind.Collision => DragKind.Scrub,
                    SectionKind.Subduction when point.X > SlabX(0) => DragKind.Dip,
                    _ => DragKind.None,
                };

            case QuakeMode.Inside:
                return pressed == "quake" || Math.Abs(((Vector)(point - _earthCentre)).Length - _earthRadius) < 16 ? DragKind.Quake : DragKind.None;

            default:
                return DragKind.None;
        }
    }

    private void DragTo(Point point)
    {
        switch (_drag)
        {
            case DragKind.Scrub:
            {
                // Dragging right moves towards today.
                var span = Section == SectionKind.Ridge ? 12.0 : 80;
                var max = Section == SectionKind.Ridge ? 12.0 : 80;
                var value = _dragValue - ((point.X - _dragStart.X) / _section.Width * span);
                SetCurrentValue(SectionTimeProperty, Math.Round(Math.Clamp(value, 0, max), 2));
                SetCurrentValue(IsPlayingProperty, false);
                break;
            }

            case DragKind.Dip:
            {
                // Down makes it steeper.
                var value = _dragValue + ((point.Y - _dragStart.Y) / 4);
                SetCurrentValue(DipProperty, Math.Round(Math.Clamp(value, 20, 70)));
                break;
            }

            case DragKind.Quake:
            {
                var v = point - _earthCentre;
                var angle = Math.Atan2(v.X, -v.Y) * 180 / Math.PI;
                SetCurrentValue(QuakeAngleProperty, Math.Round(PlateModel.Normalize360(angle)));
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
        _hoverBoundary = -1;
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
        _hoverBoundary = hover is null && Mode == QuakeMode.Plates ? BoundaryAt(point) : -1;
        Cursor = hover is not null || _hoverBoundary >= 0 ? new Cursor(StandardCursorType.Hand)
            : DragAt(point, null) switch
            {
                DragKind.Scrub => new Cursor(StandardCursorType.SizeWestEast),
                DragKind.Dip => new Cursor(StandardCursorType.SizeNorthSouth),
                DragKind.Quake => new Cursor(StandardCursorType.Hand),
                _ => Mode == QuakeMode.Plates && _map.Contains(point) ? new Cursor(StandardCursorType.Hand) : null,
            };
    }

    /// <summary>The boundary drawn within a few pixels of a point on the map, or −1.</summary>
    private int BoundaryAt(Point point)
    {
        if (!_map.Contains(point)) return -1;
        var best = -1;
        var bestDistance = 6.0;
        foreach (var (index, _, a, b) in _boundarySegments)
        {
            if (!IsMainBoundary(PlateModel.Boundaries[index]) && PlateModel.Boundaries[index].Kind != BoundaryKind.Rift) continue;
            if (Math.Abs(b.X - a.X) > _map.Width / 2) continue;
            var d = DistanceToSegment(point, a, b);
            if (d >= bestDistance) continue;
            bestDistance = d;
            best = index;
        }

        return best;
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        Vector ab = b - a;
        var length = ab.SquaredLength;
        var t = length < 1e-9 ? 0 : Math.Clamp(Vector.Dot((Vector)(p - a), ab) / length, 0, 1);
        return ((Vector)(p - (a + (ab * t)))).Length;
    }

    /// <summary>A click on nothing in particular: a boundary or a plate on the map, a place on the rim of the Earth.</summary>
    private void ClickEmpty(Point point)
    {
        switch (Mode)
        {
            case QuakeMode.Plates when _map.Contains(point):
            {
                var boundary = BoundaryAt(point);
                if (boundary >= 0)
                {
                    Interacted();
                    SetCurrentValue(SelectedPlateProperty, null);
                    SetCurrentValue(SelectedBoundaryProperty, boundary);
                    Pick(KindTarget(PlateModel.Boundaries[boundary].Kind));
                    return;
                }

                var geo = FromMap(point);
                var plate = PlateModel.PlateAt(geo.Latitude, geo.Longitude);
                Interacted();
                SetCurrentValue(SelectedBoundaryProperty, -1);
                SetCurrentValue(SelectedPlateProperty, plate.Id);
                Pick(SixPlates ? "group:" + plate.Group : "plate:" + plate.Id);
                return;
            }

            case QuakeMode.Inside when Math.Abs(((Vector)(point - _earthCentre)).Length - _earthRadius) < 22:
            {
                // A station where the rim was clicked, on whichever side of the quake.
                var v = point - _earthCentre;
                var angle = PlateModel.Normalize360((Math.Atan2(v.X, -v.Y) * 180 / Math.PI) - QuakeAngle);
                _stationSide = angle <= 180 ? 1 : -1;
                Interacted();
                SetCurrentValue(StationNameProperty, null);
                SetCurrentValue(StationDistanceProperty, Math.Round(angle <= 180 ? angle : 360 - angle));
                Pick("station");
                return;
            }
        }
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>A click on a thing: show (and say) what it is; a plate's name or a famous quake also picks it.</summary>
    private void Pick(string id)
    {
        Interacted();
        if (id.StartsWith("plate:", StringComparison.Ordinal))
        {
            SetCurrentValue(SelectedBoundaryProperty, -1);
            SetCurrentValue(SelectedPlateProperty, id[6..]);
        }
        else if (id.StartsWith("group:", StringComparison.Ordinal))
        {
            var group = id[6..];
            SetCurrentValue(SelectedBoundaryProperty, -1);
            SetCurrentValue(SelectedPlateProperty, PlateModel.Plates.First(p => p.Group == group).Id);
        }
        else if (PlateModel.Famous.FirstOrDefault(q => FamousTargetId(q.Id) == id) is { } famous)
        {
            SetCurrentValue(FamousProperty, famous.Id);
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
