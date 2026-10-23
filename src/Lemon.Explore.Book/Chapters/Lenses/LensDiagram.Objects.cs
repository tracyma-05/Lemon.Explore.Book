using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Lenses;

/// <summary>
/// The things in the lenses diagram that can be clicked for an explanation, the walkthrough's highlight, and the
/// hands-on parts: on the bench the candle, its flame, the screen and the focal points drag; in the eye the chart drags
/// along its track; in the camera the focus mark drags and a click on the photo focuses there.
/// </summary>
public sealed partial class LensDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<LensDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<LensDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<LensDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<LensDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<LensDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<LensDiagram>(TitleHeightProperty);
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
        Candle,
        Flame,
        Screen,
        Focal,
        Chart,
        FocusMark,
    }

    private DragKind _drag;

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    private Point _dragStart;
    private bool _dragMoved;

    /// <summary>The thing a target id stands for (<c>subject2</c> → <c>Obj_Subject</c>). It names the title and the narration clip.</summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "lens" => "Obj_Lens",
        "axis" => "Obj_Axis",
        "focus" => "Obj_Focus",
        "twof" => "Obj_TwoF",
        "centre" => "Obj_Centre",
        "object" => "Obj_Candle",
        "image" => "Obj_RealImage",
        "virtual" => "Obj_VirtualImage",
        "ray1" => "Obj_Ray1",
        "ray2" => "Obj_Ray2",
        "ray3" => "Obj_Ray3",
        "screen" => "Obj_Screen",
        "screenview" => "Obj_ScreenView",
        "aberration" => "Obj_Aberration",
        "chromatic" => "Obj_Chromatic",
        "cornea" => "Obj_Cornea",
        "iris" => "Obj_Iris",
        "eyelens" => "Obj_EyeLens",
        "ciliary" => "Obj_Ciliary",
        "retina" => "Obj_Retina",
        "fovea" => "Obj_Fovea",
        "blindspot" => "Obj_BlindSpot",
        "retinalimage" => "Obj_RetinalImage",
        "vitreous" => "Obj_Vitreous",
        "glasses" => "Obj_Glasses",
        "nearpoint" => "Obj_NearPoint",
        "farpoint" => "Obj_FarPoint",
        "chart" => "Obj_Chart",
        "view" => "Obj_View",
        "camlens" => "Obj_CameraLens",
        "aperture" => "Obj_Aperture",
        "shutter" => "Obj_Shutter",
        "sensor" => "Obj_Sensor",
        "blurcircle" => "Obj_BlurCircle",
        "dof" => "Obj_Dof",
        "focusmark" => "Obj_FocusMark",
        "subject0" or "subject1" or "subject2" or "subject3" => "Obj_Subject",
        "photo" => "Obj_Photo",
        "ball" => "Obj_Ball",
        "meter" => "Obj_Meter",
        "fov" => "Obj_Fov",
        _ => null,
    };

    /// <summary>The resource key of a target's title: <c>Lens_Obj_Lens</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Lens_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Lens_" + name : null;

    /// <summary>Lines and curves are glowed along their length by the diagram; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "axis" or "ray1" or "ray2" or "ray3" or "retina" or "eyelens";

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
                Interacted();
                break;
        }

        DragTo(point);
        InvalidateVisual();
    }

    /// <summary>What a press at <paramref name="point"/> drags once it moves.</summary>
    private DragKind DragAt(Point point)
    {
        switch (Mode)
        {
            case LensMode.Bench:
            {
                var v = _bench;
                if (RayStyle != RayStyle.Sunlight)
                {
                    var candle = CandleBox(v.P(-ObjectDistance, 0), ObjectHeight * v.Sy, CandleWidth(ObjectHeight, v));
                    candle = candle.Inflate(new Thickness(8, 6));
                    if (candle.Contains(point)) return point.Y < v.P(0, ObjectHeight * 0.68).Y ? DragKind.Flame : DragKind.Candle;
                }

                if (ShowScreen && Math.Abs(point.X - v.P(ScreenDistance, 0).X) < 12 && Math.Abs(v.CmUp(point.Y)) < ScreenHalf + 3) return DragKind.Screen;
                foreach (var side in new[] { -1, 1 })
                {
                    if (((Vector)(point - v.P(side * FocalLength, 0))).Length < 14) return DragKind.Focal;
                }

                return DragKind.None;
            }

            case LensMode.Eye:
            {
                var v = _eye;
                return point.X < v.TrackRight + 20 && point.Y > v.Y0 - 60 && point.Y < v.Y0 + 26 ? DragKind.Chart : DragKind.None;
            }

            default:
            {
                var v = _camera;
                return point.X < v.TrackRight + 20 && point.Y > v.Y0 - 50 && point.Y < v.Y0 + 46 ? DragKind.FocusMark : DragKind.None;
            }
        }
    }

    private void DragTo(Point point)
    {
        switch (_drag)
        {
            case DragKind.Candle:
                SetCurrentValue(ObjectDistanceProperty, Math.Round(Math.Clamp(-_bench.Cm(point.X), 3, 68) * 2) / 2);
                break;

            case DragKind.Flame:
                SetCurrentValue(ObjectDistanceProperty, Math.Round(Math.Clamp(-_bench.Cm(point.X), 3, 68) * 2) / 2);
                SetCurrentValue(ObjectHeightProperty, Math.Round(Math.Clamp(_bench.CmUp(point.Y), 1.5, 5) * 10) / 10);
                break;

            case DragKind.Screen:
                SetCurrentValue(ScreenDistanceProperty, Math.Round(Math.Clamp(_bench.Cm(point.X), 3, 88) * 2) / 2);
                break;

            case DragKind.Focal:
                SetCurrentValue(FocalLengthProperty, Math.Round(Math.Clamp(Math.Abs(_bench.Cm(point.X)), 10, 25) * 2) / 2);
                break;

            case DragKind.Chart:
                SetCurrentValue(DistanceProperty, Tidy(_eye.TrackDistance(point.X)));
                break;

            case DragKind.FocusMark:
                SetCurrentValue(FocusDistanceProperty, Tidy(_camera.TrackDistance(point.X)));
                break;
        }
    }

    /// <summary>A distance rounded to two significant figures (infinity kept).</summary>
    private static double Tidy(double metres)
    {
        if (double.IsPositiveInfinity(metres)) return metres;
        var digits = Math.Max(0, 1 - (int)Math.Floor(Math.Log10(metres)));
        return Math.Round(metres, Math.Min(digits, 6));
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
        var drag = DragAt(point);
        Cursor = drag switch
        {
            DragKind.Flame => new Cursor(StandardCursorType.SizeAll),
            DragKind.Candle or DragKind.Screen or DragKind.Focal => new Cursor(StandardCursorType.SizeWestEast),
            _ when hover is not null => new Cursor(StandardCursorType.Hand),
            DragKind.Chart or DragKind.FocusMark => new Cursor(StandardCursorType.SizeWestEast),
            _ when Mode == LensMode.Camera && _camera.Photo.Contains(point) => new Cursor(StandardCursorType.Cross),
            _ => null,
        };
    }

    /// <summary>A click on nothing in particular: move the candle or the screen there, the chart along its track, or focus there.</summary>
    private void ClickEmpty(Point point)
    {
        switch (Mode)
        {
            case LensMode.Bench:
            {
                var v = _bench;
                if (!v.Stage.Contains(point) || Math.Abs(v.CmUp(point.Y)) > 9) return;
                var x = v.Cm(point.X);
                if (x < -2 && RayStyle != RayStyle.Sunlight)
                {
                    Interacted();
                    SetCurrentValue(ObjectDistanceProperty, Math.Round(Math.Clamp(-x, 3, 68) * 2) / 2);
                }
                else if (x > 2 && ShowScreen)
                {
                    Interacted();
                    SetCurrentValue(ScreenDistanceProperty, Math.Round(Math.Clamp(x, 3, 88) * 2) / 2);
                }

                break;
            }

            case LensMode.Eye:
                if (point.X < _eye.TrackRight + 20 && Math.Abs(point.Y - _eye.Y0) < 40)
                {
                    Interacted();
                    SetCurrentValue(DistanceProperty, Tidy(_eye.TrackDistance(point.X)));
                }

                break;

            case LensMode.Camera:
                if (_camera.Photo.Contains(point))
                {
                    var layer = _photo.LayerAt(_camera.PhotoPixel(point));
                    Pick("subject" + (char)('0' + layer));
                }
                else if (point.X < _camera.TrackRight + 20 && Math.Abs(point.Y - _camera.Y0) < 40)
                {
                    Interacted();
                    SetCurrentValue(FocusDistanceProperty, Tidy(_camera.TrackDistance(point.X)));
                }

                break;
        }
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>A click on a thing: show (and say) what it is; a subject in the camera's view is also focused on.</summary>
    private void Pick(string id)
    {
        Interacted();
        if (id.StartsWith("subject", StringComparison.Ordinal) && int.TryParse(id.AsSpan(7), out var n) && n is >= 0 and < 4)
        {
            SetCurrentValue(FocusDistanceProperty, CameraPhoto.Distances[n]);
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
