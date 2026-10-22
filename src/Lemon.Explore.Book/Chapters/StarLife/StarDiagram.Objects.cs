using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.StarLife;

/// <summary>
/// The things in the star diagram that can be clicked for an explanation: the star at each stage (<c>stage-RedGiant</c>),
/// the stages of the strip, the kinds of star in the fates view (<c>class-SunLike</c>), the named stars and regions of
/// the Hertzsprung–Russell diagram (<c>known-Vega</c>, <c>giants</c>). Also the walkthrough's highlight, and telling a
/// click from dragging along the strip.
/// </summary>
public sealed partial class StarDiagram
{
    /// <summary>
    /// What the walkthrough points at: a clickable thing's id (see <see cref="ObjectTitleKey"/>) or a line (track,
    /// strip).
    /// </summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<StarDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<StarDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer starts dragging or clicks a thing, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<StarDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<StarDiagram, bool>(nameof(IsLessonActive), false);

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

    private const string StagePrefix = "stage-";
    private const string ClassPrefix = "class-";
    private const string KnownPrefix = "known-";

    private readonly ExploreTargets _targets = new();
    private readonly LessonReserve _reserve = new();
    private string? _pressed;
    private Point _pressPoint;

    /// <summary>The target id of the star at a stage, e.g. <c>stage-RedGiant</c>.</summary>
    internal static string StageTarget(StarStage stage) => StagePrefix + stage;

    private static StarStage? StageOfTarget(string? id) =>
        id is not null && id.StartsWith(StagePrefix, StringComparison.Ordinal) && Enum.TryParse<StarStage>(id[StagePrefix.Length..], out var stage) ? stage : null;

    private static StarClass? ClassOfTarget(string? id) =>
        id is not null && id.StartsWith(ClassPrefix, StringComparison.Ordinal) && Enum.TryParse<StarClass>(id[ClassPrefix.Length..], out var kind) ? kind : null;

    private static string? KnownOfTarget(string? id) =>
        id is not null && id.StartsWith(KnownPrefix, StringComparison.Ordinal) && StarModel.KnownStars.Any(s => s.Id == id[KnownPrefix.Length..])
            ? id[KnownPrefix.Length..]
            : null;

    /// <summary>The resource key of a target's title: the stage's, kind's or star's own name, or the region's label.</summary>
    internal static string? ObjectTitleKey(string? id)
    {
        if (StageOfTarget(id) is { } stage) return StageKey(stage);
        if (ClassOfTarget(id) is { } kind) return $"Star_Class_{kind}";
        if (KnownOfTarget(id) is { } known) return $"Star_Known_{known}";
        return id switch
        {
            "sun" => "Star_Label_SunToday",
            "orbits" => "Star_Obj_Orbits",
            "mainsequence" => "Star_Hr_MainSequence",
            "giants" => "Star_Hr_Giants",
            "supergiants" => "Star_Hr_Supergiants",
            "whitedwarfs" => "Star_Hr_WhiteDwarfs",
            _ => null,
        };
    }

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id)
    {
        var name = StageOfTarget(id)?.ToString()
            ?? (ClassOfTarget(id) is { } kind ? "Class" + kind : null)
            ?? (KnownOfTarget(id) is { } known ? "Known" + known : null)
            ?? id switch
            {
                "sun" => "SunToday",
                "orbits" => "Orbits",
                "mainsequence" => "MainSequenceBand",
                "giants" => "Giants",
                "supergiants" => "Supergiants",
                "whitedwarfs" => "WhiteDwarfs",
                _ => null,
            };
        return name is null ? null : "Narr_Star_Obj_" + name;
    }

    /// <summary>Highlights the diagram draws as a glow along a line rather than a ring round a target.</summary>
    private static bool IsLineHighlight(string id) => id is "track" or "strip";

    private bool Glows(string id) => Highlight == id;

    /// <summary>A pill drawn with <see cref="LabelCache.DrawTextPill"/> at <paramref name="topLeft"/>, as a target.</summary>
    private void AddPillTarget(string id, string text, Point topLeft, double size, Color color)
    {
        var formatted = _labels.Text(text, size, color);
        _targets.AddBox(id, new Rect(topLeft.X, topLeft.Y, formatted.Width + 12, formatted.Height + 4));
    }

    /// <summary>A label drawn with its top left at <paramref name="topLeft"/>, as a target.</summary>
    private void AddTextTarget(string id, FormattedText text, Point topLeft) =>
        _targets.AddBox(id, new Rect(topLeft.X - 2, topLeft.Y - 1, text.Width + 4, text.Height + 2));

    #region dragging and clicking

    /// <summary>Dragging along the strip of stages moves through the life (life and diagram views).</summary>
    private bool OnStrip(Point point) => Mode != StarMode.Fates && StripHitArea().Contains(point);

    /// <summary>
    /// Pressing on a thing waits to see whether it is a click, which explains it, or the start of a drag along the
    /// strip; pressing elsewhere on the strip starts dragging at once.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(this);
        var target = _targets.Find(point)?.Id;
        if (target is null && !OnStrip(point)) return;
        e.Pointer.Capture(this);

        _pressPoint = point;
        _pressed = target;
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
            if (OnStrip(_pressPoint)) BeginDrag(_pressPoint);
        }

        if (_dragging)
        {
            PushStory(StoryAtStrip(point.X));
            return;
        }

        if (_pressed is not null) return;
        var hover = _targets.Find(point)?.Id;
        _targets.Hover = hover;
        Cursor = hover is not null || OnStrip(point) ? new Cursor(StandardCursorType.Hand) : null;
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
        SetCurrentValue(IsPlayingProperty, false);
        PushStory(StoryAtStrip(point.X));
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>
    /// A click on a thing: a stage on the strip goes to its start, a row of the fates view picks a star of that kind;
    /// then show (and say) what it is.
    /// </summary>
    private void Pick(string id)
    {
        Interacted();
        if (OnStrip(_pressPoint) && StageOfTarget(id) is { } stage)
        {
            var index = Enumerable.Range(0, Track.Count).FirstOrDefault(i => Track[i].Stage == stage);
            SetCurrentValue(IsPlayingProperty, false);
            PushStory(StarModel.StoryStart(Track, index));
        }

        if (ClassOfTarget(id) is { } kind && StarModel.ClassOf(Mass) != kind)
        {
            SetCurrentValue(MassProperty, FateRows.First(r => r.Class == kind).Mass);
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
