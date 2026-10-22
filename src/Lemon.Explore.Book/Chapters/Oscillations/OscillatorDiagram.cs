using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Oscillations;

/// <summary>
/// The animated diagram of the oscillations page. Pendulum mode: a bob on a light rod with a pen tracing its swing on
/// paper running down beneath it (<c>OscillatorDiagram.Pendulum.cs</c>). Spring mode: a mass bouncing on a spring, the
/// paper running beside it and the circle whose shadow moves the same way (<c>OscillatorDiagram.Spring.cs</c>). Wave
/// mode: fifteen pendulums falling in and out of step, from the side and from above (<c>OscillatorDiagram.Wave.cs</c>).
/// </summary>
/// <remarks>
/// The pendulum and the spring are simulated (Runge–Kutta steps of a few milliseconds), started again whenever a setting
/// that shapes them changes or <see cref="ReleaseCount"/> goes up. The diagram pushes back what the page shows about
/// them: <see cref="Stage"/>, <see cref="Energy"/> and the <see cref="MeasuredPeriod"/> timed between turns on the paper.
/// The wave is worked out from <see cref="WaveTime"/>, which the diagram moves while playing. Dragging the bob or the mass
/// holds it aside; letting go releases it from there. Clicking a thing explains it (<c>OscillatorDiagram.Objects.cs</c>).
/// </remarks>
public sealed partial class OscillatorDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            Release();
        }
        else if (change.Property == PlaceProperty)
        {
            OnPlaceChanged();
        }
        else if (change.Property == EarthGravityProperty)
        {
            OnPlaceChanged();
        }
        else if (change.Property == LengthProperty)
        {
            Release();
        }
        else if (change.Property == AmplitudeProperty)
        {
            OnReleaseSettingChanged();
        }
        else if (change.Property == MassProperty)
        {
            Release();
        }
        else if (change.Property == StiffnessProperty)
        {
            Release();
        }
        else if (change.Property == PullProperty)
        {
            OnReleaseSettingChanged();
        }
        else if (change.Property == WaveTimeProperty)
        {
            OnWaveTimeChanged(change.GetNewValue<double>());
        }
        else if (change.Property == ReleaseCountProperty)
        {
            Release();
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Seconds of motion a paper trace keeps.</summary>
    private const double TraceSeconds = 8;

    /// <summary>Longest simulation step, in seconds.</summary>
    private const double MaxStep = 1.0 / 500;

    private const double Deg = Math.PI / 180;

    #region dependency properties

    public static readonly StyledProperty<OscillationMode> ModeProperty =
        AvaloniaProperty.Register<OscillatorDiagram, OscillationMode>(nameof(Mode), OscillationMode.Pendulum);

    public static readonly StyledProperty<OscillatorPlace> PlaceProperty =
        AvaloniaProperty.Register<OscillatorDiagram, OscillatorPlace>(nameof(Place), OscillatorPlace.Earth);

    /// <summary>Gravity on Earth here, m/s² (the shared observer location's).</summary>
    public static readonly StyledProperty<double> EarthGravityProperty =
        AvaloniaProperty.Register<OscillatorDiagram, double>(nameof(EarthGravity), OscillatorPhysics.EarthGravity);

    /// <summary>Length of the pendulum, metres.</summary>
    public static readonly StyledProperty<double> LengthProperty =
        AvaloniaProperty.Register<OscillatorDiagram, double>(nameof(Length), 1.0);

    /// <summary>How far the pendulum is let go, degrees (2–90). Dragging the bob sets it.</summary>
    public static readonly StyledProperty<double> AmplitudeProperty =
        AvaloniaProperty.Register<OscillatorDiagram, double>(nameof(Amplitude), 20.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Mass on the spring, kilograms.</summary>
    public static readonly StyledProperty<double> MassProperty =
        AvaloniaProperty.Register<OscillatorDiagram, double>(nameof(Mass), 0.5);

    /// <summary>Stiffness of the spring, newtons per metre.</summary>
    public static readonly StyledProperty<double> StiffnessProperty =
        AvaloniaProperty.Register<OscillatorDiagram, double>(nameof(Stiffness), 20.0);

    /// <summary>How far the mass is pulled from where it rests, metres. Dragging the mass sets it.</summary>
    public static readonly StyledProperty<double> PullProperty =
        AvaloniaProperty.Register<OscillatorDiagram, double>(nameof(Pull), 0.12, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the air (and, for the spring, its own losses) slow the motion down.</summary>
    public static readonly StyledProperty<bool> AirDragProperty =
        AvaloniaProperty.Register<OscillatorDiagram, bool>(nameof(AirDrag), false);

    public static readonly StyledProperty<bool> ShowForcesProperty =
        AvaloniaProperty.Register<OscillatorDiagram, bool>(nameof(ShowForces), false);

    /// <summary>Pendulum mode: a faint twin swinging exactly as the formula 2π√(L/g) says.</summary>
    public static readonly StyledProperty<bool> ShowFormulaProperty =
        AvaloniaProperty.Register<OscillatorDiagram, bool>(nameof(ShowFormula), false);

    /// <summary>Spring mode: a motor shakes the top of the spring up and down.</summary>
    public static readonly StyledProperty<bool> ShakeProperty =
        AvaloniaProperty.Register<OscillatorDiagram, bool>(nameof(Shake), false);

    /// <summary>How often the motor shakes the top of the spring, hertz.</summary>
    public static readonly StyledProperty<double> DriveFrequencyProperty =
        AvaloniaProperty.Register<OscillatorDiagram, double>(nameof(DriveFrequency), 1.0);

    /// <summary>Playback speed: 1 is real time.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<OscillatorDiagram, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<OscillatorDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Wave mode: seconds since all the pendulums were let go together.</summary>
    public static readonly StyledProperty<double> WaveTimeProperty =
        AvaloniaProperty.Register<OscillatorDiagram, double>(nameof(WaveTime), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Goes up each time the page asks to let go again.</summary>
    public static readonly StyledProperty<int> ReleaseCountProperty =
        AvaloniaProperty.Register<OscillatorDiagram, int>(nameof(ReleaseCount), 0);

    /// <summary>Out: where in its swing the pendulum or the mass is.</summary>
    public static readonly StyledProperty<SwingStage> StageProperty =
        AvaloniaProperty.Register<OscillatorDiagram, SwingStage>(nameof(Stage), SwingStage.Turning, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Out: how its energy is shared out now.</summary>
    public static readonly StyledProperty<EnergySplit> EnergyProperty =
        AvaloniaProperty.Register<OscillatorDiagram, EnergySplit>(nameof(Energy), EnergySplit.None, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Out: the last period timed between two turns at the same end, seconds; NaN until there is one.</summary>
    public static readonly StyledProperty<double> MeasuredPeriodProperty =
        AvaloniaProperty.Register<OscillatorDiagram, double>(nameof(MeasuredPeriod), double.NaN, defaultBindingMode: BindingMode.TwoWay);

    public OscillationMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double EarthGravity
    {
        get => GetValue(EarthGravityProperty);
        set => SetValue(EarthGravityProperty, value);
    }

    public OscillatorPlace Place
    {
        get => GetValue(PlaceProperty);
        set => SetValue(PlaceProperty, value);
    }

    public double Length
    {
        get => GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }

    public double Amplitude
    {
        get => GetValue(AmplitudeProperty);
        set => SetValue(AmplitudeProperty, value);
    }

    public double Mass
    {
        get => GetValue(MassProperty);
        set => SetValue(MassProperty, value);
    }

    public double Stiffness
    {
        get => GetValue(StiffnessProperty);
        set => SetValue(StiffnessProperty, value);
    }

    public double Pull
    {
        get => GetValue(PullProperty);
        set => SetValue(PullProperty, value);
    }

    public bool AirDrag
    {
        get => GetValue(AirDragProperty);
        set => SetValue(AirDragProperty, value);
    }

    public bool ShowForces
    {
        get => GetValue(ShowForcesProperty);
        set => SetValue(ShowForcesProperty, value);
    }

    public bool ShowFormula
    {
        get => GetValue(ShowFormulaProperty);
        set => SetValue(ShowFormulaProperty, value);
    }

    public bool Shake
    {
        get => GetValue(ShakeProperty);
        set => SetValue(ShakeProperty, value);
    }

    public double DriveFrequency
    {
        get => GetValue(DriveFrequencyProperty);
        set => SetValue(DriveFrequencyProperty, value);
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

    public double WaveTime
    {
        get => GetValue(WaveTimeProperty);
        set => SetValue(WaveTimeProperty, value);
    }

    public int ReleaseCount
    {
        get => GetValue(ReleaseCountProperty);
        set => SetValue(ReleaseCountProperty, value);
    }

    public SwingStage Stage
    {
        get => GetValue(StageProperty);
        set => SetValue(StageProperty, value);
    }

    public EnergySplit Energy
    {
        get => GetValue(EnergyProperty);
        set => SetValue(EnergyProperty, value);
    }

    public double MeasuredPeriod
    {
        get => GetValue(MeasuredPeriodProperty);
        set => SetValue(MeasuredPeriodProperty, value);
    }

    #endregion

    /// <summary>A moment of the paper trace: the time, the offset drawn and the formula twin's offset.</summary>
    private readonly record struct TracePoint(double Time, double Offset, double Twin);

    private readonly Stopwatch _clock = new();
    private readonly StarBackdrop _stars = new();
    private readonly LabelCache _labels;
    private readonly List<TracePoint> _trace = [];

    /// <summary>Times the pendulum or mass turned at its right (top) end, latest last.</summary>
    private readonly List<double> _turns = [];

    private double _lastSeconds;
    private bool _pushing;

    /// <summary>Simulated seconds since it was let go.</summary>
    private double _time;

    // Pendulum: angle from straight down (positive to the right) and how fast it turns, and the angle it was let go at.
    private double _angle;
    private double _angularVelocity;
    private double _releaseAngle;

    // Spring: height of the mass above where it rests, its velocity, the height it was let go at, and the motor's phase.
    private double _height;
    private double _velocity;
    private double _releaseHeight;
    private double _drivePhase;

    /// <summary>Which side the next release is from: +1 right (up), −1 left (down).</summary>
    private int _releaseSide = 1;

    private double _waveTime;
    private bool _dragging;

    public OscillatorDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
        Release();
    }

    private double Gravity => OscillatorPhysics.Gravity(Place, EarthGravity);

    private double Damping => Mode switch
    {
        OscillationMode.Spring when Shake => OscillatorPhysics.ShakenDamping(SpringOmega, AirDrag ? OscillatorPhysics.SpringDamping : 0),
        OscillationMode.Spring => AirDrag ? OscillatorPhysics.SpringDamping : 0,
        _ => AirDrag ? OscillatorPhysics.PendulumAirDamping : 0,
    };

    private double SpringOmega => OscillatorPhysics.SpringOmega(Mass, Stiffness);

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
    }

    private void OnPlaceChanged()
    {
        // The wave keeps its place in the cycle; the pendulum and the spring start again.
        var cycle = OscillatorPhysics.WaveCycle(Gravity);
        if (_waveTime > cycle) PushWaveTime(_waveTime % cycle);
        Release();
    }

    private void OnReleaseSettingChanged()
    {
        if (_pushing) return;
        _releaseSide = 1;
        Release();
    }

    private void OnWaveTimeChanged(double value)
    {
        if (_pushing) return;
        _waveTime = Math.Max(0, value);
    }

    /// <summary>Lets the pendulum or the mass go again from its release point, with a clean sheet of paper.</summary>
    private void Release()
    {
        _time = 0;
        _drivePhase = 0;
        _trace.Clear();
        _turns.Clear();
        _releaseAngle = _releaseSide * Math.Clamp(Amplitude, 1, 90) * Deg;
        _angle = _releaseAngle;
        _angularVelocity = 0;
        _releaseHeight = _releaseSide * Math.Clamp(Pull, 0.005, MaxPull);
        _height = _releaseHeight;
        _velocity = 0;
        Push(MeasuredPeriodProperty, double.NaN);
        Record();
        PushReadings();
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
            var simulated = dt * Math.Clamp(Speed, 0.05, 8);
            if (Mode == OscillationMode.Wave)
            {
                var cycle = OscillatorPhysics.WaveCycle(Gravity);
                var next = _waveTime + simulated;
                PushWaveTime(next >= cycle ? next - cycle : next);
            }
            else
            {
                Advance(simulated);
            }
        }

        InvalidateVisual();
    }

    private void PushWaveTime(double value)
    {
        _waveTime = value;
        Push(WaveTimeProperty, value);
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

    #region simulation

    /// <summary>Runs the pendulum or the spring on by <paramref name="seconds"/> in small steps, timing its turns.</summary>
    private void Advance(double seconds)
    {
        var steps = Math.Max(1, (int)Math.Ceiling(seconds / MaxStep));
        var dt = seconds / steps;
        var damping = Damping;
        var gravity = Gravity;
        var length = Math.Max(0.05, Length);
        var omega = SpringOmega;
        var driveOmega = 2 * Math.PI * DriveFrequency;
        var shake = Shake;

        for (var i = 0; i < steps; i++)
        {
            double before;
            double after;
            if (Mode == OscillationMode.Spring)
            {
                var phase = _drivePhase;
                var start = _time;
                double Support(double t) => shake ? OscillatorPhysics.DriveAmplitude * Math.Sin(phase + (driveOmega * (t - start))) : 0;

                before = _velocity;
                (_height, _velocity) = OscillatorPhysics.StepSpring(_height, _velocity, omega, damping, Support, _time, dt);
                after = _velocity;
                if (shake) _drivePhase = (_drivePhase + (driveOmega * dt)) % (2 * Math.PI);
            }
            else
            {
                before = _angularVelocity;
                (_angle, _angularVelocity) = OscillatorPhysics.StepPendulum(_angle, _angularVelocity, length, gravity, damping, dt);
                after = _angularVelocity;
            }

            // Turning at the right (top) end: the velocity goes from positive to negative.
            if (before > 0 && after <= 0) OnTurn(_time + (dt * before / (before - after)));
            _time += dt;
            if (_trace.Count == 0 || _time - _trace[^1].Time >= 1.0 / 90) Record();
        }

        var keepFrom = _time - TraceSeconds - 1;
        var drop = 0;
        while (drop < _trace.Count && _trace[drop].Time < keepFrom) drop++;
        if (drop > 0) _trace.RemoveRange(0, drop);
        _turns.RemoveAll(t => t < keepFrom);

        PushReadings();
    }

    private void OnTurn(double time)
    {
        if (_turns.Count > 0)
        {
            var period = time - _turns[^1];
            if (period > 0.05) Push(MeasuredPeriodProperty, Math.Round(period, 4));
        }

        _turns.Add(time);
    }

    private void Record()
    {
        var offset = Mode == OscillationMode.Spring ? _height : _angle;
        _trace.Add(new TracePoint(_time, offset, TwinAngle(_time)));
    }

    /// <summary>The formula twin: let go with the pendulum, swinging at 2π√(L/g) and slowed alike by the air.</summary>
    private double TwinAngle(double time)
    {
        var omega = Math.Sqrt(Gravity / Math.Max(0.05, Length));
        var damping = Damping;
        return _releaseAngle * Math.Exp(-damping * time / 2) * Math.Cos(omega * time);
    }

    /// <summary>Pushes where it is in its swing and how its energy is shared out.</summary>
    private void PushReadings()
    {
        if (Mode == OscillationMode.Wave) return;

        if (Mode == OscillationMode.Spring)
        {
            var omega = SpringOmega;
            Push(EnergyProperty, OscillatorPhysics.SpringEnergy(_height, _velocity, omega, _releaseHeight));
            if (!_dragging)
            {
                Push(StageProperty, OscillatorPhysics.Stage(_height, _velocity,
                    OscillatorPhysics.SpringAmplitude(_height, _velocity, omega), 0.003));
            }

            return;
        }

        var length = Math.Max(0.05, Length);
        Push(EnergyProperty, OscillatorPhysics.PendulumEnergy(_angle, _angularVelocity, length, Gravity, _releaseAngle));
        if (!_dragging)
        {
            Push(StageProperty, OscillatorPhysics.Stage(_angle, _angularVelocity,
                OscillatorPhysics.PendulumAmplitude(_angle, _angularVelocity, length, Gravity), 0.6 * Deg));
        }
    }

    #endregion

    #region dragging the bob or the mass

    private bool IsOnHandle(Point point) => Mode switch
    {
        OscillationMode.Pendulum => ((Vector)(point - PendulumLayout().Bob(_angle, Length))).Length < 34,
        OscillationMode.Spring => ((Vector)(point - SpringLayout().MassRect(_height).Center())).Length < 44,
        _ => false,
    };

    private void DragTo(Point point)
    {
        if (Mode == OscillationMode.Spring)
        {
            var layout = SpringLayout();
            _height = Math.Clamp((layout.EquilibriumY - point.Y) / layout.Scale, -MaxPull, Math.Min(MaxPull, layout.HighestHeight));
            _velocity = 0;
        }
        else
        {
            var layout = PendulumLayout();
            var angle = Math.Atan2(point.X - layout.Pivot.X, point.Y - layout.Pivot.Y);
            _angle = Math.Clamp(angle, -90 * Deg, 90 * Deg);
            _angularVelocity = 0;
        }

        _trace.Clear();
        _turns.Clear();
        _time = 0;
    }

    /// <summary>Lets go from where it was dragged: that becomes the amplitude (or pull) and the side it starts on.</summary>
    private void LetGo()
    {
        if (Mode == OscillationMode.Spring)
        {
            _releaseSide = _height < 0 ? -1 : 1;
            Push(PullProperty, Math.Round(Math.Clamp(Math.Abs(_height), 0.01, MaxPull), 3));
        }
        else
        {
            _releaseSide = _angle < 0 ? -1 : 1;
            Push(AmplitudeProperty, Math.Round(Math.Clamp(Math.Abs(_angle) / Deg, 2, 90), 1));
        }

        Release();
    }

    #endregion

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        DrawBackground(dc, width, height, now);
        _targets.Clear();
        switch (Mode)
        {
            case OscillationMode.Spring:
                DrawSpring(dc, now);
                break;

            case OscillationMode.Wave:
                DrawWave(dc, now);
                break;

            default:
                DrawPendulum(dc, now);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
    }

    /// <summary>A dark backdrop tinted by the place: blue-grey on Earth, black and starry on the Moon, rust on Mars…</summary>
    private void DrawBackground(PaintContext dc, double width, double height, double now)
    {
        dc.DrawRectangle(Place switch
        {
            OscillatorPlace.Moon => Ink.MoonSky,
            OscillatorPlace.Mars => Ink.MarsSky,
            OscillatorPlace.Jupiter => Ink.JupiterSky,
            _ => Ink.EarthSky,
        }, null, new Rect(0, 0, width, height));
        if (Place == OscillatorPlace.Moon) _stars.Draw(dc, width, height, RenderScale(), now, 0.7);
    }

    /// <summary>"Earth · g = 9.81 m/s²", on a pill.</summary>
    private void DrawPlaceBadge(PaintContext dc, Point center)
    {
        var text = LocFormat("Osc_Diagram_PlaceBadge", Loc($"Osc_Place_{Place}"), Gravity.ToString("0.00", CultureInfo.InvariantCulture));
        DrawCenteredPill(dc, text, center, 12, Ink.Text);
        var formatted = _labels.Text(text, 12, Ink.Text);
        _targets.AddBox("place", new Rect(center.X - (formatted.Width / 2) - 6, center.Y - (formatted.Height / 2) - 2, formatted.Width + 12, formatted.Height + 4));
    }

    /// <summary>Already localized text on a dark pill, centred on <paramref name="center"/>.</summary>
    private void DrawCenteredPill(PaintContext dc, string text, Point center, double size, Color color)
    {
        var formatted = _labels.Text(text, size, color);
        _labels.DrawTextPill(dc, text, new Point(center.X - (formatted.Width / 2) - 6, center.Y - (formatted.Height / 2) - 2), size, color);
    }

    /// <summary>An arrow from <paramref name="from"/> along <paramref name="vector"/>, with an optional label at its tip.</summary>
    private void DrawArrow(PaintContext dc, Point from, Vector vector, IBrush brush, Color labelColor, string? labelKey)
    {
        if (vector.Length < 3) return;
        var pen = MakePen(brush, 2.4);
        var direction = vector / vector.Length;
        var tip = from + vector;
        dc.DrawLine(pen, from, tip - (direction * 7));
        DrawArrowHead(dc, brush, tip, direction, 11);
        if (labelKey is null) return;

        var text = _labels.Get(labelKey, 11.5, labelColor);
        var at = tip + (direction * 8);
        dc.DrawText(text, new Point(direction.X < -0.3 ? at.X - text.Width : direction.X > 0.3 ? at.X : at.X - (text.Width / 2),
            direction.Y < -0.3 ? at.Y - text.Height : direction.Y > 0.3 ? at.Y : at.Y - (text.Height / 2)));
    }

    /// <summary>A shaded ball: a bright spot up and to the left, darker toward the rim.</summary>
    private static void DrawBall(PaintContext dc, Point center, double radius, Color color, double opacity = 1)
    {
        if (opacity < 1) dc.PushOpacity(opacity);
        var brush = new ImmutableRadialGradientBrush(
        [
            new ImmutableGradientStop(0, Lerp(color, Colors.White, 0.75)),
            new ImmutableGradientStop(0.45, color),
            new ImmutableGradientStop(1, Lerp(color, Colors.Black, 0.6)),
        ],
            center: new RelativePoint(0.42, 0.38, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.35, 0.3, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.65, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.65, RelativeUnit.Relative));
        dc.DrawEllipse(brush, Ink.BallRim, center, radius, radius);
        if (opacity < 1) dc.Pop();
    }

    internal static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    private static class Ink
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x7A, 0x6B);
        public static readonly Color Green = Color.FromRgb(0x8B, 0xE0, 0x8F);
        public static readonly Color Brass = Color.FromRgb(0xD9, 0xA4, 0x41);
        public static readonly Color Steel = Color.FromRgb(0xA9, 0xB6, 0xC6);

        public static readonly IBrush EarthSky = Vertical(Color.FromRgb(0x0C, 0x13, 0x22), Color.FromRgb(0x1A, 0x27, 0x3B));
        public static readonly IBrush MoonSky = Vertical(Color.FromRgb(0x02, 0x03, 0x06), Color.FromRgb(0x10, 0x12, 0x17));
        public static readonly IBrush MarsSky = Vertical(Color.FromRgb(0x17, 0x0C, 0x0A), Color.FromRgb(0x36, 0x1A, 0x11));
        public static readonly IBrush JupiterSky = Vertical(Color.FromRgb(0x16, 0x11, 0x0B), Color.FromRgb(0x35, 0x27, 0x15));

        public static readonly IBrush WhiteBrush = Solid(235, 0xEE, 0xF1, 0xF6);
        public static readonly IBrush GoldBrush = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IBrush CyanBrush = Solid(255, 0x6F, 0xD8, 0xFF);
        public static readonly IBrush RedBrush = Solid(255, 0xFF, 0x7A, 0x6B);
        public static readonly IBrush GreenBrush = Solid(255, 0x8B, 0xE0, 0x8F);

        public static readonly IPen BallRim = MakePen(Solid(90, 0, 0, 0), 1);
        public static readonly IPen Rod = MakePen(Solid(230, 0xC9, 0xD1, 0xDC), 2);
        public static readonly IPen TwinRod = MakePen(Solid(90, 0xC9, 0xD1, 0xDC), 1.5, dashed: true);
        public static readonly IPen Guide = MakePen(Solid(70, 255, 255, 255), 1, dashed: true);
        public static readonly IPen EquilibriumLine = MakePen(Solid(150, 0xFF, 0xD3, 0x6B), 1.2, dashed: true);
        public static readonly IPen Arc = MakePen(Solid(200, 0xFF, 0xD3, 0x6B), 1.5);
        public static readonly IPen Trace = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 2.2);
        public static readonly IPen TwinTrace = MakePen(Solid(150, 0xC9, 0xD1, 0xDC), 1.5, dashed: true);
        public static readonly IPen PaperGrid = MakePen(Solid(22, 255, 255, 255), 1);
        public static readonly IPen PaperCentre = MakePen(Solid(55, 255, 255, 255), 1);
        public static readonly IPen PaperEdge = MakePen(Solid(60, 255, 255, 255), 1);
        public static readonly IPen PenArm = MakePen(Solid(120, 0xFF, 0xD3, 0x6B), 1, dashed: true);
        public static readonly IPen Bracket = MakePen(Solid(200, 0x8B, 0xE0, 0x8F), 1.3);
        public static readonly IPen Beam = MakePen(Solid(200, 0x5A, 0x66, 0x78), 1);
        public static readonly IPen Hatch = MakePen(Solid(90, 0xA9, 0xB6, 0xC6), 1);
        public static readonly IPen Ruler = MakePen(Solid(150, 0xC9, 0xD1, 0xDC), 1.2);

        public static readonly IBrush Paper = Solid(30, 255, 255, 255);
        public static readonly IBrush BeamFill = Vertical(Color.FromRgb(0x4A, 0x55, 0x66), Color.FromRgb(0x2A, 0x31, 0x3C));
        public static readonly IBrush Pivot = Solid(255, 0xEE, 0xF1, 0xF6);

        private static IBrush Vertical(Color top, Color bottom) => SpaceDrawing.LinearGradient(top, bottom, 90);
    }
}

internal static class RectExtensions
{
    public static Point Center(this Rect rect) => new(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
}
