using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Timing;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Projectile;

/// <summary>
/// The animated diagram of the projectile page, drawn to scale (a metre is as long across as up). Launch mode: a cannon
/// fires one ball, a strobe image every moment, its speed split into across and up, shadows on the ground and on a post
/// (<c>ProjectileDiagram.Launch.cs</c>). Best-angle mode: seven balls at once from 15° to 75°
/// (<c>ProjectileDiagram.BestAngle.cs</c>). Together mode: a dropped and a thrown ball, or the monkey and the hunter
/// (<c>ProjectileDiagram.Together.cs</c>).
/// </summary>
/// <remarks>
/// Every flight is worked out whole when a setting changes (<see cref="ProjectilePhysics.Fly"/>) and played back by
/// <see cref="Time"/>, which the diagram moves while playing: real time, sped up for long flights
/// (<see cref="ProjectilePhysics.PlaybackBoost"/>). After landing it waits a moment and starts again. Dragging from the
/// cannon aims it: the angle and the speed are pushed back to the page.
/// </remarks>
public sealed partial class ProjectileDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AngleProperty)
        {
            OnSettingChanged();
        }
        else if (change.Property == LaunchSpeedProperty)
        {
            OnSettingChanged();
        }
        else if (change.Property == ClearTrailsCountProperty)
        {
            _trails.Clear();
        }
        else if (change.Property == TimeProperty)
        {
            OnTimeChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the title and the notes under it.</summary>
    private const double TitleSpace = 34;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    /// <summary>Real seconds a landed flight stays on screen before it starts again.</summary>
    private const double HoldSeconds = 2.2;

    /// <summary>Pixels of arrow per metre per second, for the launch arrow and the velocity arrows.</summary>
    private const double ArrowPerSpeed = 3.2;

    private const double Deg = Math.PI / 180;

    #region dependency properties

    public static readonly StyledProperty<ProjectileMode> ModeProperty =
        AvaloniaProperty.Register<ProjectileDiagram, ProjectileMode>(nameof(Mode), ProjectileMode.Launch);
    public static readonly StyledProperty<ProjectilePlace> PlaceProperty =
        AvaloniaProperty.Register<ProjectileDiagram, ProjectilePlace>(nameof(Place), ProjectilePlace.Earth);
    /// <summary>Gravity on Earth here, m/s² (the shared observer location's).</summary>
    public static readonly StyledProperty<double> EarthGravityProperty =
        AvaloniaProperty.Register<ProjectileDiagram, double>(nameof(EarthGravity), ProjectilePhysics.EarthGravity);
    public static readonly StyledProperty<ProjectileBall> BallProperty =
        AvaloniaProperty.Register<ProjectileDiagram, ProjectileBall>(nameof(Ball), ProjectileBall.Baseball);
    public static readonly StyledProperty<bool> AirDragProperty =
        AvaloniaProperty.Register<ProjectileDiagram, bool>(nameof(AirDrag), false);

    /// <summary>Launch angle above level, degrees (0–90). Aiming the cannon sets it.</summary>
    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<ProjectileDiagram, double>(nameof(Angle), 45.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Launch speed, m/s. Aiming the cannon sets it.</summary>
    public static readonly StyledProperty<double> LaunchSpeedProperty =
        AvaloniaProperty.Register<ProjectileDiagram, double>(nameof(LaunchSpeed), 20.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the cliff the cannon stands on, metres.</summary>
    public static readonly StyledProperty<double> CliffHeightProperty =
        AvaloniaProperty.Register<ProjectileDiagram, double>(nameof(CliffHeight), 0.0);

    public static readonly StyledProperty<bool> ShowVectorsProperty =
        AvaloniaProperty.Register<ProjectileDiagram, bool>(nameof(ShowVectors), true);
    public static readonly StyledProperty<bool> ShowShadowsProperty =
        AvaloniaProperty.Register<ProjectileDiagram, bool>(nameof(ShowShadows), false);
    public static readonly StyledProperty<bool> ShowStrobeProperty =
        AvaloniaProperty.Register<ProjectileDiagram, bool>(nameof(ShowStrobe), true);

    /// <summary>Launch mode: earlier shots stay as faint paths to compare with.</summary>
    public static readonly StyledProperty<bool> KeepTrailsProperty =
        AvaloniaProperty.Register<ProjectileDiagram, bool>(nameof(KeepTrails), true);

    /// <summary>Goes up each time the page clears the kept paths.</summary>
    public static readonly StyledProperty<int> ClearTrailsCountProperty =
        AvaloniaProperty.Register<ProjectileDiagram, int>(nameof(ClearTrailsCount), 0);

    public static readonly StyledProperty<TogetherScene> SceneProperty =
        AvaloniaProperty.Register<ProjectileDiagram, TogetherScene>(nameof(Scene), TogetherScene.DropAndThrow);

    /// <summary>Drop and throw: how high the table is, metres.</summary>
    public static readonly StyledProperty<double> TowerHeightProperty =
        AvaloniaProperty.Register<ProjectileDiagram, double>(nameof(TowerHeight), 20.0);

    /// <summary>Drop and throw: how fast the second ball is thrown sideways, m/s.</summary>
    public static readonly StyledProperty<double> ThrowSpeedProperty =
        AvaloniaProperty.Register<ProjectileDiagram, double>(nameof(ThrowSpeed), 8.0);

    /// <summary>The hunter's dart speed, m/s.</summary>
    public static readonly StyledProperty<double> DartSpeedProperty =
        AvaloniaProperty.Register<ProjectileDiagram, double>(nameof(DartSpeed), 25.0);

    /// <summary>Whether the monkey lets go of the branch the moment the dart is fired.</summary>
    public static readonly StyledProperty<bool> MonkeyLetsGoProperty =
        AvaloniaProperty.Register<ProjectileDiagram, bool>(nameof(MonkeyLetsGo), true);

    /// <summary>Playback speed: 1 is real time (long flights are sped up on top of it).</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<ProjectileDiagram, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<ProjectileDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Seconds since launch.</summary>
    public static readonly StyledProperty<double> TimeProperty =
        AvaloniaProperty.Register<ProjectileDiagram, double>(nameof(Time), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Goes up each time the page asks to fire again.</summary>
    public static readonly StyledProperty<int> LaunchCountProperty =
        AvaloniaProperty.Register<ProjectileDiagram, int>(nameof(LaunchCount), 0);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<ProjectileDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<ProjectileDiagram>(ToolbarHeightProperty);
        return true;
    }


    public ProjectileMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double EarthGravity
    {
        get => GetValue(EarthGravityProperty);
        set => SetValue(EarthGravityProperty, value);
    }

    public ProjectilePlace Place
    {
        get => GetValue(PlaceProperty);
        set => SetValue(PlaceProperty, value);
    }

    public ProjectileBall Ball
    {
        get => GetValue(BallProperty);
        set => SetValue(BallProperty, value);
    }

    public bool AirDrag
    {
        get => GetValue(AirDragProperty);
        set => SetValue(AirDragProperty, value);
    }

    public double Angle
    {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    public double LaunchSpeed
    {
        get => GetValue(LaunchSpeedProperty);
        set => SetValue(LaunchSpeedProperty, value);
    }

    public double CliffHeight
    {
        get => GetValue(CliffHeightProperty);
        set => SetValue(CliffHeightProperty, value);
    }

    public bool ShowVectors
    {
        get => GetValue(ShowVectorsProperty);
        set => SetValue(ShowVectorsProperty, value);
    }

    public bool ShowShadows
    {
        get => GetValue(ShowShadowsProperty);
        set => SetValue(ShowShadowsProperty, value);
    }

    public bool ShowStrobe
    {
        get => GetValue(ShowStrobeProperty);
        set => SetValue(ShowStrobeProperty, value);
    }

    public bool KeepTrails
    {
        get => GetValue(KeepTrailsProperty);
        set => SetValue(KeepTrailsProperty, value);
    }

    public int ClearTrailsCount
    {
        get => GetValue(ClearTrailsCountProperty);
        set => SetValue(ClearTrailsCountProperty, value);
    }

    public TogetherScene Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public double TowerHeight
    {
        get => GetValue(TowerHeightProperty);
        set => SetValue(TowerHeightProperty, value);
    }

    public double ThrowSpeed
    {
        get => GetValue(ThrowSpeedProperty);
        set => SetValue(ThrowSpeedProperty, value);
    }

    public double DartSpeed
    {
        get => GetValue(DartSpeedProperty);
        set => SetValue(DartSpeedProperty, value);
    }

    public bool MonkeyLetsGo
    {
        get => GetValue(MonkeyLetsGoProperty);
        set => SetValue(MonkeyLetsGoProperty, value);
    }

    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public double Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    public int LaunchCount
    {
        get => GetValue(LaunchCountProperty);
        set => SetValue(LaunchCountProperty, value);
    }

    public double ToolbarHeight
    {
        get => GetValue(ToolbarHeightProperty);
        set => SetValue(ToolbarHeightProperty, value);
    }

    #endregion

    /// <summary>World to screen: where the launch point's foot is, and pixels per metre (the same across and up).</summary>
    private readonly record struct View(Rect Stage, Point Origin, double Scale)
    {
        public Point P(double x, double y) => new(Origin.X + (x * Scale), Origin.Y - (y * Scale));

        public Point P(FlightPoint point) => P(point.X, point.Y);

        /// <summary>The post on the left that measures height (and catches the ball's shadow).</summary>
        public double PostX => Origin.X - 64;
    }

    /// <summary>An earlier shot kept to compare with.</summary>
    private sealed record Trail(Flight Flight, double Angle, double Speed, string Key, Color Color);

    /// <summary>A path of the best-angle fan.</summary>
    private sealed record FanPath(double Angle, Flight Flight, Color Color, bool IsBest);

    private readonly BookStopwatch _clock = new();
    private readonly StarBackdrop _stars = new();
    private readonly LabelCache _labels;
    private readonly List<Trail> _trails = [];
    private readonly List<FanPath> _fan = [];

    private double _lastSeconds;
    private bool _pushing;
    private bool _dragging;

    /// <summary>Seconds since launch, and real seconds since it landed.</summary>
    private double _time;
    private double _held;

    /// <summary>Launch mode: the shot, the same shot without air (when there is air), and whether it has landed yet.</summary>
    private Flight _flight = ProjectilePhysics.Fly(20, 45, 0, 9.81, 0);
    private Flight? _airless;
    private string _flightKey = string.Empty;
    private bool _landedOnce;

    /// <summary>How long the current scene runs, seconds.</summary>
    private double _duration;

    /// <summary>Pixels per metre now, easing toward the scale that fits the scene; 0 to snap to it.</summary>
    private double _scale;

    public ProjectileDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
        Rebuild();
    }

    private double Gravity => ProjectilePhysics.Gravity(Place, EarthGravity);

    private double Drag => ProjectilePhysics.DragFactor(Ball, Place, AirDrag);

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

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _stars.OnSizeChanged(_clock.Elapsed.TotalSeconds);
        _scale = 0;
    }

    private void OnSettingChanged(bool snap = false)
    {
        if (_pushing && !_dragging) return;
        if (snap) _scale = 0;
        Rebuild();
    }

    private void OnTimeChanged(double value)
    {
        if (_pushing) return;
        _time = Math.Clamp(value, 0, _duration);
        _held = 0;
    }

    /// <summary>Works out the scene's flights again and fires from the start.</summary>
    private void Rebuild()
    {
        var gravity = Gravity;
        var drag = Drag;
        switch (Mode)
        {
            case ProjectileMode.BestAngle:
                BuildFan(gravity, drag);
                _duration = _fan.Count == 0 ? 0 : _fan.Max(p => p.Flight.Duration);
                break;

            case ProjectileMode.Together:
                _duration = TogetherDuration(gravity);
                break;

            default:
            {
                var key = string.Create(CultureInfo.InvariantCulture, $"{Place}|{Ball}|{AirDrag}|{Angle:0.#}|{LaunchSpeed:0.#}|{CliffHeight:0.#}");
                if (key != _flightKey)
                {
                    // The shot it replaces stays as a faint path, once it has landed.
                    if (KeepTrails && _landedOnce && _flightKey.Length > 0) KeepTrail();
                    _flightKey = key;
                    _flight = ProjectilePhysics.Fly(LaunchSpeed, Angle, CliffHeight, gravity, drag);
                    _airless = drag > 0 ? ProjectilePhysics.Fly(LaunchSpeed, Angle, CliffHeight, gravity, 0) : null;
                    _landedOnce = false;
                }

                _duration = _flight.Duration;
                break;
            }
        }

        _time = 0;
        _held = 0;
        Push(TimeProperty, 0.0);
    }

    private void KeepTrail()
    {
        if (_trails.Any(t => t.Key == _flightKey) || _flight.Duration <= 0) return;
        _trails.Add(new Trail(_flight, _flight.Launch.Vy >= 0 ? Math.Atan2(_flight.Launch.Vy, _flight.Launch.Vx) / Deg : 0,
            _flight.Launch.Speed, _flightKey, TrailColors[_trails.Count % TrailColors.Length]));
        if (_trails.Count > 6) _trails.RemoveAt(0);
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;
        _reserve.Step(IsLessonActive, dt);

        if (IsPlaying && !_dragging && dt > 0)
        {
            if (_time < _duration)
            {
                var boost = Mode == ProjectileMode.Together ? 1 : ProjectilePhysics.PlaybackBoost(_duration);
                _time = Math.Min(_duration, _time + (dt * Math.Clamp(Speed, 0.05, 8) * boost));
                Push(TimeProperty, _time);
            }
            else
            {
                if (Mode == ProjectileMode.Launch) _landedOnce = true;
                _held += dt;
                if (_held >= HoldSeconds)
                {
                    _time = 0;
                    _held = 0;
                    Push(TimeProperty, 0.0);
                }
            }
        }
        else if (_time >= _duration && Mode == ProjectileMode.Launch)
        {
            _landedOnce = true;
        }

        // Ease the scale toward the one that fits, so a longer shot zooms out smoothly.
        var target = TargetScale();
        _scale = _scale <= 0 ? target : _scale + ((target - _scale) * (1 - Math.Exp(-dt * 9)));

        InvalidateVisual();
    }

    private void Push(AvaloniaProperty property, object value)
    {
        if (Equals(GetValue(property), value)) return;
        _pushing = true;
        try
        {
            SetCurrentValue(property, value);
        }
        finally
        {
            _pushing = false;
        }
    }

    #endregion

    #region layout

    /// <summary>The room for the picture: below the title, above the toolbar and, while a walkthrough runs, its subtitles.</summary>
    private Rect Stage()
    {
        var top = Math.Min(TitleSpace, ActualHeight * 0.26);
        var bottom = Math.Max(ToolbarSpace, ToolbarHeight + 18 + 16) + _reserve.Current;
        return new Rect(24, top, Math.Max(100, ActualWidth - 48), Math.Max(120, ActualHeight - top - bottom));
    }

    /// <summary>Where the launch point stands: past the height post on the left, the ground near the bottom.</summary>
    private static Point OriginIn(Rect stage) => new(stage.Left + 112, stage.Bottom - 30);

    /// <summary>How far across and how high the scene reaches, metres.</summary>
    private (double Across, double Up) Extent()
    {
        switch (Mode)
        {
            case ProjectileMode.BestAngle:
            {
                var across = _fan.Count == 0 ? 1 : _fan.Max(p => p.Flight.Range);
                var up = _fan.Count == 0 ? 1 : _fan.Max(p => p.Flight.Peak);
                if (!AirDrag || Drag <= 0)
                {
                    across = Math.Max(across, ProjectilePhysics.IdealMaxRange(LaunchSpeed, CliffHeight, Gravity));
                    up = Math.Max(up, ProjectilePhysics.Envelope(0, LaunchSpeed, CliffHeight, Gravity));
                }

                return (across, up);
            }

            case ProjectileMode.Together:
                return TogetherExtent();

            default:
            {
                var across = Math.Max(_flight.Range, _airless?.Range ?? 0);
                var up = Math.Max(_flight.Peak, _airless?.Peak ?? 0);
                foreach (var trail in _trails.Where(_ => KeepTrails))
                {
                    across = Math.Max(across, trail.Flight.Range);
                    up = Math.Max(up, trail.Flight.Peak);
                }

                // Room for the launch arrow too.
                return (Math.Max(across, 2), Math.Max(up, 1));
            }
        }
    }

    private double TargetScale()
    {
        var stage = Stage();
        var origin = OriginIn(stage);
        var (across, up) = Extent();
        var width = Math.Max(60, stage.Right - 56 - origin.X);
        var height = Math.Max(60, origin.Y - stage.Top - 40);
        return Math.Max(0.05, Math.Min(width / Math.Max(across, 0.5), height / Math.Max(up, 0.5)));
    }

    private View Layout()
    {
        var stage = Stage();
        if (_scale <= 0) _scale = TargetScale();
        return new View(stage, CentredOrigin(stage, _scale), _scale);
    }

    /// <summary>
    /// The launch point for the picture at <paramref name="scale"/>. A page is taller than it is wide, so a scene fitted
    /// to the width leaves room above it: the ground rises to centre the scene in the stage instead of leaving it under
    /// a tall empty sky.
    /// </summary>
    private Point CentredOrigin(Rect stage, double scale)
    {
        var origin = OriginIn(stage);
        var (_, up) = Extent();
        var spare = stage.Height - ((up * scale) + 40 + 30);
        return spare > 0 ? new Point(origin.X, origin.Y - (spare / 2)) : origin;
    }

    #endregion

    #region aiming the cannon

    private Point ArrowTip(Point axle) => axle + (AimDirection * (BarrelLength + (LaunchSpeed * ArrowPerSpeed)));

    /// <summary>Points the cannon at the mouse: the direction is the angle, the distance the speed.</summary>
    private void AimAt(Point point)
    {
        var muzzle = Layout().P(0, CliffHeight);
        Vector arrow = point - muzzle;
        var angle = Math.Clamp(Math.Atan2(-arrow.Y, arrow.X) / Deg, 0, 90);
        if (arrow.X < 0 && arrow.Y < 0) angle = 90;
        else if (arrow.Y > 0) angle = 0;
        var speed = Math.Clamp((arrow.Length - BarrelLength) / ArrowPerSpeed, 2, 60);
        Push(AngleProperty, Math.Round(angle));
        Push(LaunchSpeedProperty, Math.Round(speed * 2) / 2);
    }

    #endregion

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        _targets.Clear();
        var view = Layout();
        DrawBackdrop(dc, view, width, height);
        switch (Mode)
        {
            case ProjectileMode.BestAngle:
                DrawBestAngle(dc, view);
                break;

            case ProjectileMode.Together:
                DrawTogether(dc, view);
                break;

            default:
                DrawLaunch(dc, view);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, Now, width);
    }

    #region drawing helpers

    /// <summary>The sky and the ground of the place, a ruler along the ground and a post measuring height.</summary>
    private void DrawBackdrop(PaintContext dc, View view, double width, double height)
    {
        dc.DrawRectangle(Place switch
        {
            ProjectilePlace.Moon => Ink.MoonSky,
            ProjectilePlace.Mars => Ink.MarsSky,
            _ => Ink.EarthSky,
        }, null, new Rect(0, 0, width, height));
        if (Place == ProjectilePlace.Moon) _stars.Draw(dc, width, height, RenderScale(), _clock.Elapsed.TotalSeconds, 0.6);

        var ground = view.Origin.Y;
        dc.DrawRectangle(Place switch
        {
            ProjectilePlace.Moon => Ink.MoonGround,
            ProjectilePlace.Mars => Ink.MarsGround,
            _ => Ink.EarthGround,
        }, null, new Rect(0, ground, width, Math.Max(0, height - ground)));
        dc.DrawLine(Ink.GroundLine, new Point(0, ground), new Point(width, ground));

        // Along the ground: a tick every nice step, labelled.
        var across = (view.Stage.Right - view.Origin.X) / view.Scale;
        var step = NiceStep(across / 7);
        for (var x = 0.0; x <= across + 1e-9; x += step)
        {
            var at = view.P(x, 0);
            dc.DrawLine(Ink.Tick, at, at + new Vector(0, 6));
            var text = _labels.Text($"{Number(x, step < 1 ? "0.#" : "0")} m", 10.5, Ink.Dim);
            dc.DrawText(text, new Point(at.X - (text.Width / 2), at.Y + 8));
        }

        // The height post.
        var up = (view.Origin.Y - view.Stage.Top - 8) / view.Scale;
        var top = view.P(0, up);
        var post = view.PostX;
        dc.DrawLine(Ink.Post, new Point(post, ground), new Point(post, top.Y));
        var rise = NiceStep(up / 6);
        for (var y = rise; y <= up + 1e-9; y += rise)
        {
            var at = new Point(post, view.P(0, y).Y);
            dc.DrawLine(Ink.Tick, at, at + new Vector(-6, 0));
            var text = _labels.Text($"{Number(y, rise < 1 ? "0.#" : "0")} m", 10.5, Ink.Dim);
            dc.DrawText(text, new Point(at.X - text.Width - 9, at.Y - (text.Height / 2)));
        }
    }

    /// <summary>A rocky cliff (or table) whose top right corner is the launch point, <paramref name="height"/> metres up.</summary>
    private static void DrawCliff(PaintContext dc, View view, double height, double width, bool table = false)
    {
        if (height <= 0) return;
        var top = view.P(0, height);
        var rect = new Rect(top.X - width, top.Y, width, view.Origin.Y - top.Y);
        if (table)
        {
            dc.DrawRectangle(Ink.TableTop, null, new Rect(rect.Left - 6, rect.Top, rect.Width + 6, 7));
            dc.DrawRectangle(Ink.TableLeg, null, new Rect(rect.Left + 4, rect.Top + 7, 7, rect.Height - 7));
            dc.DrawRectangle(Ink.TableLeg, null, new Rect(rect.Right - 11, rect.Top + 7, 7, rect.Height - 7));
            return;
        }

        dc.DrawRectangle(Ink.Cliff, Ink.CliffEdge, rect);
        for (var y = rect.Top + 14; y < rect.Bottom - 4; y += 18)
        {
            dc.DrawLine(Ink.CliffLayer, new Point(rect.Left + 3, y), new Point(rect.Right - 3, y + 3));
        }
    }

    /// <summary>The path of a flight up to <paramref name="time"/> (all of it when it is past the landing).</summary>
    private static StreamGeometry PathGeometry(View view, Flight flight, double time)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(view.P(flight.Launch), false);
            var last = view.P(flight.Launch);
            foreach (var point in flight.Points)
            {
                if (point.Time > time) break;
                var screen = view.P(point);
                if (((Vector)(screen - last)).SquaredLength < 4) continue;
                context.LineTo(screen);
                last = screen;
            }

            if (time > 0) context.LineTo(view.P(flight.At(time)));

            context.EndFigure(false);
        }
        return geometry;
    }

    /// <summary>A shaded ball: a bright spot up and to the left, darker toward the rim.</summary>
    private static void DrawBall(PaintContext dc, Point center, double radius, Color color, double opacity = 1)
    {
        if (opacity < 1) dc.PushOpacity(opacity);
        var brush = new ImmutableRadialGradientBrush(
        [
            new ImmutableGradientStop(0, Lerp(color, Colors.White, 0.7)),
            new ImmutableGradientStop(0.5, color),
            new ImmutableGradientStop(1, Lerp(color, Colors.Black, 0.55)),
        ],
            center: new RelativePoint(0.42, 0.38, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.35, 0.3, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.65, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.65, RelativeUnit.Relative));
        dc.DrawEllipse(brush, Ink.BallRim, center, radius, radius);
        if (opacity < 1) dc.Pop();
    }

    /// <summary>The thrown ball (a shuttlecock flies cork first, along its velocity).</summary>
    private void DrawProjectile(PaintContext dc, Point center, Vector velocity, double opacity = 1)
    {
        if (Ball != ProjectileBall.Shuttlecock)
        {
            DrawBall(dc, center, BallRadius, BallColor, opacity);
            return;
        }

        var forward = velocity.Length > 1e-6 ? velocity / velocity.Length : new Vector(1, 0);
        var side = new Vector(-forward.Y, forward.X);
        if (opacity < 1) dc.PushOpacity(opacity);
        var cork = center + (forward * 4);
        var back = center - (forward * 9);
        dc.DrawGeometry(Ink.Feathers, Ink.FeatherEdge, Polygon(cork + (side * 3), back + (side * 8), back - (side * 8), cork - (side * 3)));
        dc.DrawEllipse(Ink.Cork, null, cork, 4, 4);
        if (opacity < 1) dc.Pop();
    }

    private double BallRadius => Ball switch
    {
        ProjectileBall.ShotPut or ProjectileBall.Soccer => 9,
        ProjectileBall.PingPong => 6,
        _ => 7,
    };

    private Color BallColor => Ball switch
    {
        ProjectileBall.ShotPut => Color.FromRgb(0x9A, 0xA3, 0xB0),
        ProjectileBall.Soccer => Color.FromRgb(0xF2, 0xF4, 0xF7),
        ProjectileBall.PingPong => Color.FromRgb(0xFF, 0xA2, 0x4A),
        _ => Color.FromRgb(0xF4, 0xEC, 0xDC),
    };

    /// <summary>
    /// An arrow from <paramref name="from"/> along <paramref name="vector"/>, with a label at its tip; a target named
    /// <paramref name="id"/> (the label, or the tip) if given.
    /// </summary>
    private void DrawArrow(PaintContext dc, Point from, Vector vector, IBrush brush, Color labelColor, string? label, double thickness = 2.2,
        string? id = null)
    {
        if (vector.Length < 3) return;
        var pen = MakePen(brush, thickness);
        var direction = vector / vector.Length;
        var tip = from + vector;
        dc.DrawLine(pen, from, tip - (direction * 6));
        DrawArrowHead(dc, brush, tip, direction, 10);
        if (label is null)
        {
            if (id is not null) _targets.Add(id, tip, 9);
            return;
        }

        var text = _labels.Text(label, 11.5, labelColor, true);
        var at = tip + (direction * 7);
        var topLeft = new Point(direction.X < -0.3 ? at.X - text.Width : direction.X > 0.3 ? at.X : at.X - (text.Width / 2),
            direction.Y < -0.3 ? at.Y - text.Height : direction.Y > 0.3 ? at.Y : at.Y - (text.Height / 2));
        dc.DrawText(text, topLeft);
        if (id is not null) _targets.AddBox(id, new Rect(topLeft.X - 2, topLeft.Y - 1, text.Width + 4, text.Height + 2).Union(new Rect(tip, tip)));
    }

    /// <summary>
    /// Already localized text on a dark pill, centred on <paramref name="center"/> and kept inside the stage; a target
    /// named <paramref name="id"/> if given.
    /// </summary>
    private void DrawPill(PaintContext dc, View view, string text, Point center, Color color, double size = 11.5, string? id = null)
    {
        var formatted = _labels.Text(text, size, color);
        var x = Math.Clamp(center.X - (formatted.Width / 2) - 6, view.Stage.Left + 2, Math.Max(view.Stage.Left + 2, view.Stage.Right - formatted.Width - 14));
        var y = Math.Clamp(center.Y - (formatted.Height / 2) - 2, view.Stage.Top + 2, Math.Max(view.Stage.Top + 2, view.Stage.Bottom - formatted.Height - 4));
        _labels.DrawTextPill(dc, text, new Point(x, y), size, color);
        if (id is not null) AddPillTarget(id, formatted, new Point(x, y));
    }

    /// <summary>A mark on the ground where a flight landed.</summary>
    private static void DrawLandingMark(PaintContext dc, Point at, IBrush brush)
    {
        dc.DrawLine(MakePen(brush, 2), at + new Vector(-5, -5), at + new Vector(5, 5));
        dc.DrawLine(MakePen(brush, 2), at + new Vector(-5, 5), at + new Vector(5, -5));
    }

    /// <summary>1, 2 or 5 times a power of ten, at least <paramref name="raw"/>.</summary>
    internal static double NiceStep(double raw)
    {
        if (raw <= 0 || double.IsNaN(raw)) return 1;
        var power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var mantissa = raw / power;
        return (mantissa <= 1 ? 1 : mantissa <= 2 ? 2 : mantissa <= 5 ? 5 : 10) * power;
    }

    internal static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>"12.3 m" or "123 m".</summary>
    internal static string Metres(double value) => $"{Number(value, Math.Abs(value) < 100 ? "0.0" : "0")} m";

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    /// <summary>A colour for a launch angle: cyan when flat, gold at 45°, pink when steep.</summary>
    internal static Color AngleColor(double angle)
    {
        var t = Math.Clamp(angle / 90, 0, 1);
        return t < 0.5
            ? Lerp(Color.FromRgb(0x6F, 0xD8, 0xFF), Color.FromRgb(0xFF, 0xD3, 0x6B), t * 2)
            : Lerp(Color.FromRgb(0xFF, 0xD3, 0x6B), Color.FromRgb(0xFF, 0x8F, 0xB8), (t - 0.5) * 2);
    }

    private static readonly Color[] TrailColors =
    [
        Color.FromRgb(0x6F, 0xD8, 0xFF),
        Color.FromRgb(0xFF, 0x8F, 0xB8),
        Color.FromRgb(0x8B, 0xE0, 0x8F),
        Color.FromRgb(0xB3, 0x9D, 0xFF),
        Color.FromRgb(0xFF, 0x9F, 0x5A),
        Color.FromRgb(0xC9, 0xD1, 0xDC),
    ];

    #endregion

    private static class Ink
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Green = Color.FromRgb(0x8B, 0xE0, 0x8F);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x7A, 0x6B);
        public static readonly Color Pink = Color.FromRgb(0xFF, 0x8F, 0xB8);
        public static readonly Color Violet = Color.FromRgb(0xB3, 0x9D, 0xFF);

        public static readonly IBrush EarthSky = Vertical(Color.FromRgb(0x0B, 0x16, 0x28), Color.FromRgb(0x22, 0x37, 0x52));
        public static readonly IBrush MoonSky = Vertical(Color.FromRgb(0x02, 0x03, 0x06), Color.FromRgb(0x0E, 0x10, 0x16));
        public static readonly IBrush MarsSky = Vertical(Color.FromRgb(0x17, 0x0D, 0x0A), Color.FromRgb(0x46, 0x26, 0x18));
        public static readonly IBrush EarthGround = Vertical(Color.FromRgb(0x1D, 0x33, 0x22), Color.FromRgb(0x0E, 0x18, 0x11));
        public static readonly IBrush MoonGround = Vertical(Color.FromRgb(0x3A, 0x3C, 0x42), Color.FromRgb(0x1C, 0x1D, 0x21));
        public static readonly IBrush MarsGround = Vertical(Color.FromRgb(0x5C, 0x31, 0x1E), Color.FromRgb(0x2C, 0x17, 0x0E));

        public static readonly IBrush WhiteBrush = Solid(240, 0xEE, 0xF1, 0xF6);
        public static readonly IBrush GoldBrush = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IBrush CyanBrush = Solid(255, 0x6F, 0xD8, 0xFF);
        public static readonly IBrush GreenBrush = Solid(255, 0x8B, 0xE0, 0x8F);
        public static readonly IBrush RedBrush = Solid(255, 0xFF, 0x7A, 0x6B);
        public static readonly IBrush PinkBrush = Solid(255, 0xFF, 0x8F, 0xB8);
        public static readonly IBrush ShadowBrush = Solid(150, 0, 0, 0);

        public static readonly IPen GroundLine = MakePen(Solid(120, 0xC9, 0xD1, 0xDC), 1.2);
        public static readonly IPen Tick = MakePen(Solid(150, 0xC9, 0xD1, 0xDC), 1);
        public static readonly IPen Post = MakePen(Solid(150, 0xC9, 0xD1, 0xDC), 2);
        public static readonly IPen Guide = MakePen(Solid(90, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Airless = MakePen(Solid(150, 0xEE, 0xF1, 0xF6), 1.4, dashed: true);
        public static readonly IPen Path = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 2.4);
        public static readonly IPen Preview = MakePen(Solid(200, 0xFF, 0xD3, 0x6B), 1.6, dashed: true);
        public static readonly IPen Envelope = MakePen(Solid(170, 0xB3, 0x9D, 0xFF), 1.5, dashed: true);
        public static readonly IPen Bracket = MakePen(Solid(210, 0x8B, 0xE0, 0x8F), 1.3);
        public static readonly IPen BallRim = MakePen(Solid(90, 0, 0, 0), 1);

        public static readonly IBrush Cliff = Vertical(Color.FromRgb(0x5A, 0x50, 0x46), Color.FromRgb(0x33, 0x2C, 0x26));
        public static readonly IPen CliffEdge = MakePen(Solid(160, 0x8A, 0x7E, 0x70), 1);
        public static readonly IPen CliffLayer = MakePen(Solid(60, 0, 0, 0), 1.5);
        public static readonly IBrush TableTop = Solid(255, 0x9C, 0x74, 0x4A);
        public static readonly IBrush TableLeg = Solid(255, 0x6E, 0x50, 0x33);

        public static readonly IBrush CannonBody = Vertical(Color.FromRgb(0x6A, 0x74, 0x84), Color.FromRgb(0x33, 0x39, 0x44));
        public static readonly IBrush Wheel = Solid(255, 0x7A, 0x5A, 0x3A);
        public static readonly IPen WheelRim = MakePen(Solid(255, 0x3A, 0x2A, 0x1A), 2);

        public static readonly IBrush Trunk = Solid(255, 0x6B, 0x4A, 0x2E);
        public static readonly IBrush Leaves = Solid(235, 0x2E, 0x5A, 0x34);
        public static readonly IBrush MonkeyFur = Solid(255, 0x8A, 0x5A, 0x34);
        public static readonly IBrush MonkeyFace = Solid(255, 0xE0, 0xB8, 0x8A);
        public static readonly IBrush Mound = Solid(255, 0x2A, 0x40, 0x2C);

        public static readonly IBrush Feathers = Solid(235, 0xF4, 0xF6, 0xF8);
        public static readonly IPen FeatherEdge = MakePen(Solid(120, 0x80, 0x88, 0x94), 1);
        public static readonly IBrush Cork = Solid(255, 0xC8, 0x9B, 0x62);

        private static IBrush Vertical(Color top, Color bottom) => SpaceDrawing.LinearGradient(top, bottom, 90);
    }
}
