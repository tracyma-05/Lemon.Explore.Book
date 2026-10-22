using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Lightning;

/// <summary>
/// The things in the lightning diagram that can be clicked for an explanation (the bench's rod, cloth, paper, water, ball
/// and spark; the cloud's updraft, grains, temperature levels, charges and field meter; the strike's leader, streamers,
/// strokes, the things on the ground, the protected zone and the thunder strip) and the walkthrough's highlight. Clicking
/// the cloth, the paper, the water or the ball also sends the demonstrating hand there; clicking the thunder strip plays
/// the thunder. Dragging the rod, clicking the ground for the meter and dragging the strike's time work as before.
/// </summary>
public sealed partial class LightningDiagram
{
    private void OnPropertyChangedObjects(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == HandSceneProperty)
        {
            OnHandSceneChanged(change.GetNewValue<HandScene>());
        }
        else if (change.Property == FlashRequestProperty)
        {
            OnFlashRequested(change.GetNewValue<FlashKind>());
        }
    }

    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<LightningDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<LightningDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<LightningDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<LightningDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<LightningDiagram, double>(nameof(TitleHeight), 0.0);

    /// <summary>Static: the part of its round the demonstrating hand keeps repeating (all of it by default).</summary>
    public static readonly StyledProperty<HandScene> HandSceneProperty =
        AvaloniaProperty.Register<LightningDiagram, HandScene>(nameof(HandScene), HandScene.Round, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Cloud: set to a kind to make the storm flash now; the diagram sets it back to none.</summary>
    public static readonly StyledProperty<FlashKind> FlashRequestProperty =
        AvaloniaProperty.Register<LightningDiagram, FlashKind>(nameof(FlashRequest), FlashKind.None, defaultBindingMode: BindingMode.TwoWay);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<LightningDiagram>(TitleHeightProperty);
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

    public HandScene HandScene
    {
        get => GetValue(HandSceneProperty);
        set => SetValue(HandSceneProperty, value);
    }

    public FlashKind FlashRequest
    {
        get => GetValue(FlashRequestProperty);
        set => SetValue(FlashRequestProperty, value);
    }

    private readonly ExploreTargets _targets = new();
    private readonly LessonReserve _reserve = new();

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    /// <summary>
    /// The thing a target id stands for, which names its narration clip (<see cref="ObjectClipKey"/>). The bench's rod is
    /// <c>rod</c>, the house's lightning rod <c>lrod</c>.
    /// </summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "cloth" => "Cloth",
        "paper" => "Paper",
        "water" => "Water",
        "ball" => "Ball",
        "earth" => "Earth",
        "rod" => "Rod",
        "humidity" => "Humidity",
        "spark" => "Spark",
        "updraft" => "Updraft",
        "anvil" => "Anvil",
        "top" => "Top",
        "graupel" => "Graupel",
        "ice" => "Ice",
        "rain" => "Rain",
        "freezing" => "Freezing",
        "reversal" => "Reversal",
        "glaciation" => "Glaciation",
        "upper" => "Upper",
        "main" => "Main",
        "lower" => "Lower",
        "meter" => "Meter",
        "corona" => "Corona",
        "field" => "Field",
        "tree" => "Tree",
        "house" => "House",
        "lrod" => "LightningRod",
        "person" => "Person",
        "cloudbase" => "CloudBase",
        "slow" => "Slow",
        "leader" => "Leader",
        "streamer" => "Streamer",
        "stroke" => "Stroke",
        "later" => "Later",
        "dart" => "Dart",
        "hit" => "Hit",
        "zone" => "Zone",
        "sphere" => "Sphere",
        "strip" => "Strip",
        "squeezed" => "Squeezed",
        _ => null,
    };

    /// <summary>The resource key of a target's title: the page's own word where it has one, else <c>Lightning_Obj_&lt;name&gt;</c>.</summary>
    internal static string? ObjectTitleKey(string? id)
    {
        if (ObjectName(id) is not { } name) return null;
        return id switch
        {
            "paper" => "Lightning_Static_Paper",
            "water" => "Lightning_Static_Water",
            "ball" => "Lightning_Static_Ball",
            "updraft" => "Lightning_Cloud_Updraft",
            "anvil" => "Lightning_Cloud_Anvil",
            "graupel" => "Lightning_Cloud_Graupel",
            "ice" => "Lightning_Cloud_Ice",
            "rain" => "Lightning_Cloud_Rain",
            "lrod" => "Lightning_Option_Rod",
            "sphere" => "Lightning_Option_Sphere",
            "leader" => "Lightning_Strike_Label_Leader",
            "streamer" => "Lightning_Strike_Label_Streamer",
            "stroke" => "Lightning_Strike_Label_Stroke",
            "later" => "Lightning_Strike_Label_Later",
            "dart" => "Lightning_Strike_Label_Dart",
            _ => "Lightning_Obj_" + name,
        };
    }

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Lightning_Obj_" + name : null;

    /// <summary>The temperature levels, the protected zone's edge and the rolling sphere are glowed along their lines.</summary>
    private static bool IsLineHighlight(string id) => id is "freezing" or "reversal" or "glaciation" or "zone" or "sphere";

    /// <summary>Whether the walkthrough points at <paramref name="id"/>, which the diagram glows itself.</summary>
    private bool Glows(string id) => Highlight == id;

    /// <summary>The glowing pen for a line the walkthrough points at.</summary>
    private IPen GlowLine => ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds);

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

    private void OnFlashRequested(FlashKind kind)
    {
        if (kind == FlashKind.None) return;
        _pendingFlash = kind;
        SetCurrentValue(FlashRequestProperty, FlashKind.None);
    }

    #region clicking and dragging

    /// <summary>
    /// Pressing on a thing waits: moving more than a few pixels drags as before (the rod, the meter, the strike's time),
    /// coming up over the thing picks it. Pressing anywhere else acts at once, as before.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragStart = e.GetPosition(this);
        _dragMoved = false;
        _pressed = _targets.Find(_dragStart)?.Id;
        e.Pointer.Capture(this);

        if (_pressed is null) StartDrag();
        e.Handled = true;
    }

    private void StartDrag()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        switch (Mode)
        {
            case LightningMode.Static:
                GrabRod(_dragStart);
                break;

            case LightningMode.Cloud:
                MoveMeter(_dragStart);
                break;

            default:
                _dragStartValue = _strikeTime;
                break;
        }

        _dragging = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_pressed is not null)
        {
            if (((Vector)(point - _dragStart)).Length <= 4) return;
            _pressed = null;
            StartDrag();
        }

        if (!_dragging)
        {
            var hover = _targets.Find(point)?.Id;
            _targets.Hover = hover;
            Cursor = hover is not null ? new Cursor(StandardCursorType.Hand) : Mode switch
            {
                LightningMode.Static => new Cursor(StandardCursorType.Hand),
                LightningMode.Cloud => new Cursor(StandardCursorType.Cross),
                _ => new Cursor(StandardCursorType.SizeWestEast),
            };
            return;
        }

        if (Mode == LightningMode.Static) Cursor = new Cursor(StandardCursorType.SizeAll);
        var dx = point.X - _dragStart.X;
        if (Math.Abs(dx) > 3 || Math.Abs(point.Y - _dragStart.Y) > 3) _dragMoved = true;
        switch (Mode)
        {
            case LightningMode.Static:
                DragRod(point);
                break;

            case LightningMode.Cloud:
                MoveMeter(point);
                break;

            default:
                var across = Math.Max(300, ActualWidth * 0.9);
                var length = StrikeLength;
                _strikeTime = Math.Clamp(_dragStartValue + (dx * length / across), 0, length);
                Push(StrikeTimeProperty, Math.Round(_strikeTime, 3));
                break;
        }

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

        // A drag of the strike's timeline holds the picture where it was left.
        if (dragged && Mode == LightningMode.Strike) SetCurrentValue(IsPlayingProperty, false);
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
    /// A click on a thing: show (and say) what it is. The cloth, the paper, the water and the ball also send the hand there
    /// to show it; the thunder strip plays the thunder from the flash on.
    /// </summary>
    private void Pick(string id)
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);

        if (Mode == LightningMode.Static && id is "cloth" or "paper" or "water" or "ball")
        {
            SetCurrentValue(AutoHandProperty, true);
            SetCurrentValue(HandSceneProperty, id switch
            {
                "cloth" => HandScene.Rub,
                "paper" => HandScene.Paper,
                "water" => HandScene.Stream,
                _ => HandScene.Ball,
            });
            SetCurrentValue(IsPlayingProperty, true);
        }
        else if (id == "strip")
        {
            SetCurrentValue(StrikeTimeProperty, StrikeTimeline.FlashStory(Strokes));
            SetCurrentValue(IsPlayingProperty, true);
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
