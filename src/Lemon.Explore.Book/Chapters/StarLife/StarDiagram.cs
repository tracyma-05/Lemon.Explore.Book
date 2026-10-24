using System.ComponentModel;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Timing;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.StarLife;

/// <summary>
/// The animated diagram of the life of a star page. Life mode: the star drawn big through its life, from a collapsing
/// cloud to its remnant (<c>StarDiagram.Life.cs</c>). Diagram mode: its track on the Hertzsprung–Russell diagram with
/// real stars (<c>StarDiagram.Hr.cs</c>). Fates mode: the four ways a life can go, by mass (<c>StarDiagram.Fates.cs</c>).
/// </summary>
/// <remarks>
/// Time is a <c>story</c>: playback seconds into the star's track (<see cref="StarModel.Track"/>), every stage getting
/// its turn however short it really is. The diagram owns the animation clock and pushes <see cref="Story"/> back to its
/// binding; at the end it holds for a moment and starts again. The strip of stages along the bottom (life and diagram
/// modes) jumps through the life when clicked or dragged; in fates mode a click on a row picks a star of that kind.
/// Clicking a thing also explains it (<c>StarDiagram.Objects.cs</c>).
/// </remarks>
public sealed partial class StarDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MassProperty)
        {
            _track = null;
        }
        else if (change.Property == StoryProperty)
        {
            OnStoryChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Seconds playback holds on the end before starting again.</summary>
    private const double EndPause = 2.5;

    #region dependency properties

    public static readonly StyledProperty<StarMode> ModeProperty =
        AvaloniaProperty.Register<StarDiagram, StarMode>(nameof(Mode), StarMode.Life);

    /// <summary>The star's initial mass, Suns.</summary>
    public static readonly StyledProperty<double> MassProperty =
        AvaloniaProperty.Register<StarDiagram, double>(nameof(Mass), 1.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Playback seconds into the star's track, 0…<see cref="StarModel.StoryLength"/>.</summary>
    public static readonly StyledProperty<double> StoryProperty =
        AvaloniaProperty.Register<StarDiagram, double>(nameof(Story), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<StarDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Life mode: today's Sun as a dashed circle, for scale.</summary>
    public static readonly StyledProperty<bool> ShowSunProperty =
        AvaloniaProperty.Register<StarDiagram, bool>(nameof(ShowSun), true);

    /// <summary>Life mode, Sun-like stars: the orbits of Mercury, Venus, Earth and Mars.</summary>
    public static readonly StyledProperty<bool> ShowOrbitsProperty =
        AvaloniaProperty.Register<StarDiagram, bool>(nameof(ShowOrbits), true);

    /// <summary>Diagram mode: the Sun and well-known stars.</summary>
    public static readonly StyledProperty<bool> ShowKnownStarsProperty =
        AvaloniaProperty.Register<StarDiagram, bool>(nameof(ShowKnownStars), true);

    /// <summary>Diagram mode: lines along which stars are the same size.</summary>
    public static readonly StyledProperty<bool> ShowRadiusLinesProperty =
        AvaloniaProperty.Register<StarDiagram, bool>(nameof(ShowRadiusLines), true);

    public StarMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Mass
    {
        get => GetValue(MassProperty);
        set => SetValue(MassProperty, value);
    }

    public double Story
    {
        get => GetValue(StoryProperty);
        set => SetValue(StoryProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public bool ShowSun
    {
        get => GetValue(ShowSunProperty);
        set => SetValue(ShowSunProperty, value);
    }

    public bool ShowOrbits
    {
        get => GetValue(ShowOrbitsProperty);
        set => SetValue(ShowOrbitsProperty, value);
    }

    public bool ShowKnownStars
    {
        get => GetValue(ShowKnownStarsProperty);
        set => SetValue(ShowKnownStarsProperty, value);
    }

    public bool ShowRadiusLines
    {
        get => GetValue(ShowRadiusLinesProperty);
        set => SetValue(ShowRadiusLinesProperty, value);
    }

    #endregion

    private readonly BookStopwatch _clock = new();
    private readonly StarBackdrop _stars = new();
    private readonly LabelCache _labels;

    private IReadOnlyList<StarStageSpan>? _track;
    private double _lastSeconds;
    private bool _pushing;
    private double _story;

    /// <summary>Playback runs a little past the end to hold there; this is the time since the end, else 0.</summary>
    private double _overrun;

    private bool _dragging;

    public StarDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
    }

    /// <summary>The track of the current mass, worked out again when the mass changes.</summary>
    private IReadOnlyList<StarStageSpan> Track => _track ??= StarModel.Track(Mass);

    private double StoryLength => StarModel.StoryLength(Track);

    #region lifecycle

    private bool _attached;

    private void RequestFrame()
    {
        if (_attached)
        {
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
        }
    }

    private void OnFrame(TimeSpan time)
    {
        if (!_attached)
        {
            return;
        }

        OnRendering();
        RequestFrame();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _clock.Start();
        _lastSeconds = _clock.Elapsed.TotalSeconds;
        RequestFrame();
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        _clock.Stop();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => _labels.Clear();

    private void OnStoryChanged(double value)
    {
        if (_pushing) return;
        _story = value;
        _overrun = 0;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _stars.OnSizeChanged(_clock.Elapsed.TotalSeconds);
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.1);
        _lastSeconds = now;
        _reserve.Step(IsLessonActive, dt);

        if (IsPlaying && !_dragging && dt > 0)
        {
            var length = StoryLength;
            if (_story >= length)
            {
                // Hold on the end for a moment, then start the life again.
                _overrun += dt;
                if (_overrun > EndPause) PushStory(0);
            }
            else
            {
                PushStory(Math.Min(length, _story + dt));
            }
        }

        InvalidateVisual();
    }

    private void PushStory(double story)
    {
        _story = Math.Clamp(story, 0, StoryLength);
        _overrun = 0;
        _pushing = true;
        try
        {
            SetCurrentValue(StoryProperty, _story);
        }
        finally
        {
            _pushing = false;
        }
    }

    #endregion

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        var dpi = RenderScale();
        var now = _clock.Elapsed.TotalSeconds;
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
        _stars.Draw(dc, width, height, dpi, now, Mode == StarMode.Diagram ? 0.35 : 0.8);

        var track = Track;
        var state = StarModel.At(track, Mass, Math.Min(_story, StarModel.StoryLength(track)));
        _targets.Clear();
        if (Mode != StarMode.Fates) AddStripTargets(track);
        switch (Mode)
        {
            case StarMode.Fates:
                DrawFates(dc, state, now);
                break;

            case StarMode.Diagram:
                DrawHr(dc, track, state, now);
                DrawStrip(dc, track, state, now);
                break;

            default:
                DrawLife(dc, track, state, now);
                DrawStrip(dc, track, state, now);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
    }

    #region the strip of stages

    private const double StripHeight = 22;

    /// <summary>The bar of stages along the bottom, above the toolbar: each as wide as its share of playback.</summary>
    private Rect StripRect()
    {
        var left = 40.0;
        var right = Math.Max(left + 100, ActualWidth - 40);
        return new Rect(left, ActualHeight - 150 - _reserve.Current, right - left, StripHeight);
    }

    /// <summary>Clicks a little above and below the bar still count, for the labels.</summary>
    private Rect StripHitArea()
    {
        var strip = StripRect();
        strip = strip.Inflate(new Thickness(0, 16));
        return strip;
    }

    private double StoryAtStrip(double x)
    {
        var strip = StripRect();
        return Math.Clamp((x - strip.Left) / strip.Width, 0, 1) * StoryLength;
    }

    /// <summary>
    /// The strip's stages as targets, added before the star's own so the walkthrough's ring for a stage goes round the
    /// star rather than its piece of the strip.
    /// </summary>
    private void AddStripTargets(IReadOnlyList<StarStageSpan> track)
    {
        var strip = StripRect();
        var length = StarModel.StoryLength(track);
        var x = strip.Left;
        foreach (var span in track)
        {
            var w = strip.Width * span.Seconds / length;
            _targets.AddBox(StageTarget(span.Stage), new Rect(x + 1, strip.Top, Math.Max(1, w - 2), strip.Height));
            x += w;
        }
    }

    private void DrawStrip(PaintContext dc, IReadOnlyList<StarStageSpan> track, StarState state, double now)
    {
        var strip = StripRect();
        if (Glows("strip"))
        {
            var glow = strip;
            glow = glow.Inflate(new Thickness(4, 4));
            dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(now), glow, 8, 8);
        }

        var length = StarModel.StoryLength(track);
        var x = strip.Left;
        for (var i = 0; i < track.Count; i++)
        {
            var span = track[i];
            var w = strip.Width * span.Seconds / length;
            var rect = new Rect(x + 1, strip.Top, Math.Max(1, w - 2), strip.Height);
            var color = StageColor(span, Mass);
            var current = i == state.Index;
            dc.DrawRoundedRectangle(new SolidColorBrush(WithAlpha(color, current ? 0.5 : 0.2)), current ? Paint.StripCurrent : Paint.StripEdge, rect, 5, 5);

            // The name inside, how long it lasts underneath, where there is room.
            var name = _labels.Get(StageKey(span.Stage), 11, current ? StarDrawing.Text : StarDrawing.DimText, current);
            if (name.Width < rect.Width - 6)
            {
                dc.DrawText(name, new Point(rect.X + ((rect.Width - name.Width) / 2), rect.Y + ((rect.Height - name.Height) / 2)));
            }

            var years = _labels.Text(StarFormat.Years(span.Years), 10, StarDrawing.DimText);
            if (years.Width < rect.Width - 4)
            {
                dc.DrawText(years, new Point(rect.X + ((rect.Width - years.Width) / 2), rect.Bottom + 3));
            }

            x += w;
        }

        var marker = strip.Left + (strip.Width * Math.Min(_story, length) / length);
        dc.DrawLine(Paint.Marker, new Point(marker, strip.Top - 6), new Point(marker, strip.Bottom + 6));
        DrawArrowHead(dc, Paint.MarkerBrush, new Point(marker, strip.Top - 1), new Vector(0, 1), 7);
    }

    #endregion

    /// <summary>A stage's colour: the star's colour midway through it, or the remnant's own.</summary>
    internal static Color StageColor(StarStageSpan span, double mass) => span.Stage switch
    {
        StarStage.Supernova => StarDrawing.Blast,
        StarStage.NeutronStar => Color.FromRgb(0xA8, 0xC8, 0xFF),
        StarStage.BlackHole => Color.FromRgb(0x9A, 0x8C, 0xC8),
        StarStage.PlanetaryNebula => StarDrawing.Nebula,
        _ => StarModel.ColorOf(Math.Pow(10, (span.LogT0 + span.LogT1) / 2)),
    };

    internal static string StageKey(StarStage stage) => $"Star_Stage_{stage}";

    private void DrawCenteredPill(PaintContext dc, string text, Point center, double size, Color color)
    {
        var formatted = _labels.Text(text, size, color);
        _labels.DrawTextPill(dc, text, new Point(center.X - (formatted.Width / 2) - 6, center.Y - (formatted.Height / 2) - 2), size, color);
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    private static class Paint
    {
        public static readonly IPen StripEdge = MakePen(Solid(60, 255, 255, 255), 1);
        public static readonly IPen StripCurrent = MakePen(Solid(220, 255, 255, 255), 1.4);
        public static readonly IPen Marker = MakePen(Solid(240, 0xFF, 0xD3, 0x6B), 2);
        public static readonly IBrush MarkerBrush = Solid(255, 0xFF, 0xD3, 0x6B);
    }
}
