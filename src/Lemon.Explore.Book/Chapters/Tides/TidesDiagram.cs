using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Chapters.MoonPhases;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Infrastructures.Localization;

namespace Lemon.Explore.Book.Chapters.Tides;

/// <summary>
/// The animated diagram of the tides page. Bulges mode: Earth from above the North Pole with the Moon, the tidal force
/// and the two bulges of water, Earth turning under them (<c>TidesDiagram.Bulges.cs</c>). Spring and neap mode: the
/// Sun's bulges added to the Moon's through the month (<c>TidesDiagram.SpringNeap.cs</c>). Coast mode: a harbour from
/// the side, the water rising and falling under the Sun and the Moon (<c>TidesDiagram.Coast.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the animation clock (an animation frame per screen refresh while it is on screen) and pushes
/// <see cref="Day"/> back to its binding, which the rest of the page follows. Dragging sideways moves the time (a day
/// across the view), in spring and neap mode it moves the Moon along its orbit; either pauses playback. Clicking a
/// thing explains it (<c>TidesDiagram.Objects.cs</c>).
/// </remarks>
public sealed partial class TidesDiagram : Control
{
    static TidesDiagram()
    {
        AffectsRender<TidesDiagram>(CoastLatitudeProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DayProperty)
        {
            OnDayChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Bulges and coast: hours per second of playback, a tide in about eight seconds.</summary>
    private const double HoursPerSecond = 1.5;

    /// <summary>Spring and neap: days per second, a month in about 15 seconds.</summary>
    private const double DaysPerSecond = 2;

    #region dependency properties

    public static readonly StyledProperty<TidesMode> ModeProperty =
        AvaloniaProperty.Register<TidesDiagram, TidesMode>(nameof(Mode), TidesMode.Bulges);

    /// <summary>Days since a new moon at 00:00 (see <see cref="TideGeometry"/>), 0…<see cref="MoonPhaseGeometry.SynodicMonth"/>.</summary>
    public static readonly StyledProperty<double> DayProperty =
        AvaloniaProperty.Register<TidesDiagram, double>(nameof(Day), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<TidesDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Bulges mode: arrows of the tidal force round Earth.</summary>
    public static readonly StyledProperty<bool> ShowForcesProperty =
        AvaloniaProperty.Register<TidesDiagram, bool>(nameof(ShowForces), true);

    /// <summary>Coast mode: the latitude whose sky is drawn (the shared observer location's), degrees north.</summary>
    public static readonly StyledProperty<double> CoastLatitudeProperty =
        AvaloniaProperty.Register<TidesDiagram, double>(nameof(CoastLatitude), 30.0);

    public double CoastLatitude
    {
        get => GetValue(CoastLatitudeProperty);
        set => SetValue(CoastLatitudeProperty, value);
    }

    public TidesMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Day
    {
        get => GetValue(DayProperty);
        set => SetValue(DayProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public bool ShowForces
    {
        get => GetValue(ShowForcesProperty);
        set => SetValue(ShowForcesProperty, value);
    }

    #endregion

    private readonly Stopwatch _clock = new();
    private readonly StarBackdrop _stars = new();
    private readonly LabelCache _labels;
    private readonly PlanetRenderer _earthTop = MoonDrawing.TopRenderer("Earth");
    private readonly PlanetRenderer _moonTop = MoonDrawing.TopRenderer("Moon");

    private double _lastSeconds;
    private bool _pushing;
    private double _day;
    private bool _dragging;
    private double _dragStartX;
    private double _dragStartDay;

    public TidesDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

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

    private void OnDayChanged(double value)
    {
        if (!_pushing) _day = value;
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
            PushDay(_day + (Mode == TidesMode.SpringNeap ? dt * DaysPerSecond : dt * HoursPerSecond / 24));
        }

        InvalidateVisual();
    }

    private void PushDay(double day)
    {
        _day = MoonPhaseGeometry.Wrap(day);
        _pushing = true;
        try
        {
            SetCurrentValue(DayProperty, _day);
        }
        finally
        {
            _pushing = false;
        }
    }

    #endregion

    #region dragging

    private void DragTo(Point point)
    {
        if (Mode == TidesMode.SpringNeap)
        {
            if (DayAt(point) is { } day) PushDay(day);
            return;
        }

        // Across the whole width is a day.
        var width = Math.Max(ActualWidth, 1);
        PushDay(_dragStartDay + ((point.X - _dragStartX) / width));
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
        _targets.Clear();

        switch (Mode)
        {
            case TidesMode.Coast:
                DrawCoast(dc, dpi, now);
                break;

            case TidesMode.SpringNeap:
                _stars.Draw(dc, width, height, dpi, now, 0.8);
                DrawSpringNeap(dc, dpi, now);
                break;

            default:
                _stars.Draw(dc, width, height, dpi, now, 0.8);
                DrawBulges(dc, dpi, now);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
    }

    #region shared by the views from above

    // View transform of the views from above: Earth's centre on screen and DIPs per unit.
    private Point _origin;
    private double _scale = 1;

    /// <summary>Screen point at <paramref name="angle"/>° (counter-clockwise from the Sun, which is on the left) and <paramref name="radius"/> from Earth.</summary>
    private Point TopPoint(double angle, double radius)
    {
        var (sin, cos) = Math.SinCos(SeasonsGeometry.ToRadians(angle));
        return new Point(_origin.X - (cos * radius * _scale), _origin.Y + (sin * radius * _scale));
    }

    /// <summary>The angle of <paramref name="point"/> round Earth, the same way as <see cref="TopPoint"/>.</summary>
    private double AngleOf(Point point)
    {
        var v = point - _origin;
        return SeasonsGeometry.Normalize360(SeasonsGeometry.ToDegrees(Math.Atan2(v.Y, -v.X)));
    }

    /// <summary>
    /// The water round Earth as a closed shape: <paramref name="radius"/> plus <paramref name="bulge"/> times the
    /// height of the water at each angle.
    /// </summary>
    private StreamGeometry WaterShape(double radius, double bulge, Func<double, double> heightAt)
    {
        const int steps = 144;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < steps; i++)
            {
                var angle = 360.0 * i / steps;
                var point = TopPoint(angle, radius + (bulge * heightAt(angle)));
                if (i == 0) ctx.BeginFigure(point, true);
                else ctx.LineTo(point);
            }

            ctx.EndFigure(true);
        }
        return geometry;
    }

    #endregion

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);
}
