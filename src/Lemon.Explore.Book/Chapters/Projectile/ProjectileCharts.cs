using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Projectile;

/// <summary>A small chart of the projectile side panel: redrawn when a property changes or the language does.</summary>
public abstract class ProjectileChart : Control
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    private protected readonly LabelCache Labels;

    protected ProjectileChart()
    {
        Labels = new LabelCache(this);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        Labels.Clear();
        InvalidateVisual();
    }


    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 80 || ActualHeight < 60) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        Draw(dc, ActualWidth, ActualHeight);
    }

    protected abstract void Draw(PaintContext dc, double width, double height);

    private protected void AxisLabel(PaintContext dc, string value, Point at, bool alignRight = false, bool center = false)
    {
        var text = Labels.Plain(value, 10, ChartInk.Dim);
        var x = alignRight ? at.X - text.Width : center ? at.X - (text.Width / 2) : at.X;
        dc.DrawText(text, new Point(x, at.Y - (text.Height / 2)));
    }

    /// <summary>A value label beside a marked point, kept inside the plot.</summary>
    private protected void PointLabel(PaintContext dc, string value, Point point, Rect plot, Color color, bool below = false)
    {
        var text = Labels.Text(value, 11, color, true);
        var at = new Point(point.X + 8, below ? point.Y + 4 : point.Y - text.Height - 3);
        if (at.X + text.Width > plot.Right) at = at.WithX(point.X - text.Width - 8);
        at = at.WithY(Math.Clamp(at.Y, plot.Top - 4, plot.Bottom - text.Height));
        dc.DrawText(text, at);
    }

    private protected static void Marker(PaintContext dc, Point point, Color color, double radius = 4.5)
    {
        dc.DrawEllipse(Solid(70, color.R, color.G, color.B), null, point, radius * 2, radius * 2);
        dc.DrawEllipse(Solid(255, color.R, color.G, color.B), null, point, radius, radius);
    }

    private protected static StreamGeometry Curve(IEnumerable<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = true;
            foreach (var point in points)
            {
                if (first) context.BeginFigure(point, false);
                else context.LineTo(point);
                first = false;
            }

            if (!first) context.EndFigure(false);
        }
        return geometry;
    }

    /// <summary>Grid lines and labels up the left, every nice step from 0 (or −max) to max.</summary>
    private protected void ValueAxis(PaintContext dc, Rect plot, double low, double high, double step, string unit)
    {
        for (var value = Math.Ceiling(low / step) * step; value <= high + 1e-9; value += step)
        {
            var y = plot.Bottom - ((value - low) / (high - low) * plot.Height);
            dc.DrawLine(Math.Abs(value) < 1e-9 && low < 0 ? ChartInk.Zero : ChartInk.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            AxisLabel(dc, $"{Number(value, step < 1 ? "0.#" : "0")}{unit}", new Point(plot.Left - 5, y), alignRight: true);
        }
    }

    private protected static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private protected static class ChartInk
    {
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Green = Color.FromRgb(0x8B, 0xE0, 0x8F);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9F, 0x5A);
        public static readonly Color Pink = Color.FromRgb(0xFF, 0x8F, 0xB8);
        public static readonly Color Brown = Color.FromRgb(0xC8, 0x8A, 0x5A);
        public static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);
        public static readonly IPen Zero = MakePen(Solid(80, 255, 255, 255), 1);
        public static readonly IPen Guide = MakePen(Solid(90, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Airless = MakePen(Solid(170, 0xEE, 0xF1, 0xF6), 1.4, dashed: true);
    }
}

/// <summary>
/// How far a throw goes against its angle, at the page's speed and height: without air a hill peaking at 45° (lower from
/// up high), with air a lower hill leaning to flatter angles.
/// </summary>
public sealed class RangeChart : ProjectileChart
{
    static RangeChart()
    {
        AffectsRender<RangeChart>(PlaceProperty, EarthGravityProperty, BallProperty, AirDragProperty, LaunchSpeedProperty, CliffHeightProperty, AngleProperty);
    }

    public static readonly StyledProperty<ProjectilePlace> PlaceProperty =
        AvaloniaProperty.Register<RangeChart, ProjectilePlace>(nameof(Place), ProjectilePlace.Earth);
    /// <summary>Gravity on Earth here, m/s² (the shared observer location's).</summary>
    public static readonly StyledProperty<double> EarthGravityProperty =
        AvaloniaProperty.Register<RangeChart, double>(nameof(EarthGravity), ProjectilePhysics.EarthGravity);
    public static readonly StyledProperty<ProjectileBall> BallProperty =
        AvaloniaProperty.Register<RangeChart, ProjectileBall>(nameof(Ball), ProjectileBall.Baseball);
    public static readonly StyledProperty<bool> AirDragProperty =
        AvaloniaProperty.Register<RangeChart, bool>(nameof(AirDrag), false);
    public static readonly StyledProperty<double> LaunchSpeedProperty =
        AvaloniaProperty.Register<RangeChart, double>(nameof(LaunchSpeed), 20.0);
    public static readonly StyledProperty<double> CliffHeightProperty =
        AvaloniaProperty.Register<RangeChart, double>(nameof(CliffHeight), 0.0);

    /// <summary>The angle to mark, degrees; NaN to mark the fan's angles instead.</summary>
    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<RangeChart, double>(nameof(Angle), double.NaN);

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

    public double Angle
    {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(44, 16, width - 58, height - 46);
        var gravity = ProjectilePhysics.Gravity(Place, EarthGravity);
        var drag = ProjectilePhysics.DragFactor(Ball, Place, AirDrag);
        var speed = LaunchSpeed;
        var cliff = CliffHeight;

        var angles = Enumerable.Range(0, 46).Select(i => i * 2.0).ToList();
        var airless = angles.Select(a => ProjectilePhysics.IdealRange(speed, a, cliff, gravity)).ToList();
        var withAir = drag > 0 ? angles.Select(a => ProjectilePhysics.Range(speed, a, cliff, gravity, drag)).ToList() : null;

        // Scaled to the throws as they are: with air, the airless curve runs off the top.
        var top = withAir?.Max() ?? airless.Max();
        var max = ProjectileDiagram.NiceStep(top / 4) * 4;
        while (max / 2 >= top * 1.02) max /= 2;
        var step = ProjectileDiagram.NiceStep(max / 4);
        Point P(double angle, double range) => new(plot.Left + (angle / 90 * plot.Width), plot.Bottom - (range / max * plot.Height));

        ValueAxis(dc, plot, 0, max, step, " m");
        for (var angle = 0; angle <= 90; angle += 15)
        {
            dc.DrawLine(ChartInk.Grid, P(angle, 0), P(angle, max));
            AxisLabel(dc, $"{angle}°", P(angle, 0) + new Vector(0, 10), center: true);
        }

        var bestAirless = ProjectilePhysics.IdealBestAngle(speed, cliff, gravity);
        if (withAir is null)
        {
            dc.DrawGeometry(null, MakePen(Solid(255, ChartInk.Gold.R, ChartInk.Gold.G, ChartInk.Gold.B), 2.2),
                Curve(angles.Select((a, i) => P(a, airless[i]))));
        }
        else
        {
            dc.PushClip(new RectangleGeometry(new Rect(plot.Left, plot.Top - 2, plot.Width, plot.Height + 4)));
            dc.DrawGeometry(null, ChartInk.Airless, Curve(angles.Select((a, i) => P(a, airless[i]))));
            dc.Pop();
            dc.DrawGeometry(null, MakePen(Solid(255, ChartInk.Orange.R, ChartInk.Orange.G, ChartInk.Orange.B), 2.2),
                Curve(angles.Select((a, i) => P(a, withAir[i]))));

            // "without air" where its curve leaves the top, or above its peak.
            var name = Labels.Get("Proj_Chart_WithoutAir", 10, ChartInk.Dim);
            var exit = airless.FindIndex(r => r > max);
            var at = exit >= 0
                ? new Point(P(angles[exit], max).X + 6, plot.Top - 2)
                : P(bestAirless, ProjectilePhysics.IdealMaxRange(speed, cliff, gravity)) + new Vector(10, -name.Height + 2);
            dc.DrawText(name, new Point(Math.Min(at.X, plot.Right - name.Width), Math.Max(plot.Top - 6, at.Y)));
        }

        // The best angle, and the page's angle (or the fan's).
        var best = ProjectilePhysics.BestAngle(speed, cliff, gravity, drag);
        var bestRange = ProjectilePhysics.Range(speed, best, cliff, gravity, drag);
        var bestPoint = P(best, bestRange);
        dc.DrawLine(ChartInk.Guide, bestPoint, P(best, 0));
        Marker(dc, bestPoint, ChartInk.Green, 3.5);
        var bestText = Labels.Text(LocalizationService.Instance.Format("Proj_Chart_Best", Number(best, "0")), 10.5, ChartInk.Green, true);
        dc.DrawText(bestText, new Point(Math.Clamp(bestPoint.X - (bestText.Width / 2), plot.Left, plot.Right - bestText.Width),
            plot.Bottom - bestText.Height - 3));

        if (double.IsNaN(Angle))
        {
            foreach (var angle in ProjectilePhysics.FanAngles)
            {
                var color = ProjectileDiagram.AngleColor(angle);
                dc.DrawEllipse(Solid(255, color.R, color.G, color.B), null, P(angle, ProjectilePhysics.Range(speed, angle, cliff, gravity, drag)), 3.5, 3.5);
            }

            return;
        }

        var current = Math.Clamp(Angle, 0, 90);
        var range = ProjectilePhysics.Range(speed, current, cliff, gravity, drag);
        var point = P(current, range);
        Marker(dc, point, ChartInk.Gold);
        PointLabel(dc, ProjectileDiagram.Metres(range), point, plot, ChartInk.Gold, below: point.Y < plot.Top + 16);
    }
}

/// <summary>
/// The two parts of the velocity through the flight: across (flat without air, sagging with it) and up (falling by g every
/// second, through zero at the top), with the moment shown.
/// </summary>
public sealed class VelocityChart : ProjectileChart
{
    static VelocityChart()
    {
        AffectsRender<VelocityChart>(PlaceProperty, EarthGravityProperty, BallProperty, AirDragProperty, LaunchSpeedProperty, AngleProperty, CliffHeightProperty, TimeProperty);
    }

    public static readonly StyledProperty<ProjectilePlace> PlaceProperty =
        AvaloniaProperty.Register<VelocityChart, ProjectilePlace>(nameof(Place), ProjectilePlace.Earth);
    /// <summary>Gravity on Earth here, m/s² (the shared observer location's).</summary>
    public static readonly StyledProperty<double> EarthGravityProperty =
        AvaloniaProperty.Register<VelocityChart, double>(nameof(EarthGravity), ProjectilePhysics.EarthGravity);
    public static readonly StyledProperty<ProjectileBall> BallProperty =
        AvaloniaProperty.Register<VelocityChart, ProjectileBall>(nameof(Ball), ProjectileBall.Baseball);
    public static readonly StyledProperty<bool> AirDragProperty =
        AvaloniaProperty.Register<VelocityChart, bool>(nameof(AirDrag), false);
    public static readonly StyledProperty<double> LaunchSpeedProperty =
        AvaloniaProperty.Register<VelocityChart, double>(nameof(LaunchSpeed), 20.0);
    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<VelocityChart, double>(nameof(Angle), 45.0);
    public static readonly StyledProperty<double> CliffHeightProperty =
        AvaloniaProperty.Register<VelocityChart, double>(nameof(CliffHeight), 0.0);
    public static readonly StyledProperty<double> TimeProperty =
        AvaloniaProperty.Register<VelocityChart, double>(nameof(Time), 0.0);

    private Flight? _flight;
    private string _key = string.Empty;

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

    public double LaunchSpeed
    {
        get => GetValue(LaunchSpeedProperty);
        set => SetValue(LaunchSpeedProperty, value);
    }

    public double Angle
    {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    public double CliffHeight
    {
        get => GetValue(CliffHeightProperty);
        set => SetValue(CliffHeightProperty, value);
    }

    public double Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    private Flight Flight()
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"{Place}|{Ball}|{AirDrag}|{LaunchSpeed}|{Angle}|{CliffHeight}");
        if (_flight is null || key != _key)
        {
            _key = key;
            _flight = ProjectilePhysics.Fly(LaunchSpeed, Angle, CliffHeight, ProjectilePhysics.Gravity(Place, EarthGravity),
                ProjectilePhysics.DragFactor(Ball, Place, AirDrag));
        }

        return _flight;
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var flight = Flight();
        var plot = new Rect(44, 12, width - 58, height - 40);
        var duration = Math.Max(flight.Duration, 0.1);
        var top = Math.Max(1, flight.Points.Max(p => Math.Max(Math.Abs(p.Vx), Math.Abs(p.Vy))));
        var step = ProjectileDiagram.NiceStep(top / 2);
        var max = Math.Ceiling(top / step) * step;
        Point P(double time, double value) => new(plot.Left + (time / duration * plot.Width), plot.Bottom - ((value + max) / (2 * max) * plot.Height));

        ValueAxis(dc, plot, -max, max, step, "");
        var timeStep = ProjectileDiagram.NiceStep(duration / 5);
        for (var t = 0.0; t <= duration + 1e-9; t += timeStep)
        {
            dc.DrawLine(ChartInk.Grid, P(t, -max), P(t, max));
            AxisLabel(dc, $"{Number(t, timeStep < 1 ? "0.#" : "0")} s", P(t, -max) + new Vector(0, 10), center: true);
        }

        var unit = Labels.Plain("m/s", 10, ChartInk.Dim);
        dc.DrawText(unit, new Point(2, plot.Top - 10));

        var greenPen = MakePen(Solid(255, ChartInk.Green.R, ChartInk.Green.G, ChartInk.Green.B), 2.2);
        var cyanPen = MakePen(Solid(255, ChartInk.Cyan.R, ChartInk.Cyan.G, ChartInk.Cyan.B), 2.2);
        dc.DrawGeometry(null, greenPen, Curve(flight.Points.Select(p => P(p.Time, p.Vx))));
        dc.DrawGeometry(null, cyanPen, Curve(flight.Points.Select(p => P(p.Time, p.Vy))));

        var vx = Labels.Text("vx", 10.5, ChartInk.Green, true);
        var vy = Labels.Text("vy", 10.5, ChartInk.Cyan, true);
        dc.DrawText(vx, P(0, flight.Launch.Vx) + new Vector(4, flight.Launch.Vx >= flight.Launch.Vy ? -vx.Height - 1 : 1));
        dc.DrawText(vy, P(0, flight.Launch.Vy) + new Vector(4, flight.Launch.Vy > flight.Launch.Vx ? -vy.Height - 1 : 1));

        // The top, where the upward part is zero.
        if (flight.PeakPoint.Time > 0)
        {
            var peak = P(flight.PeakPoint.Time, 0);
            dc.DrawLine(ChartInk.Guide, new Point(peak.X, plot.Top), new Point(peak.X, plot.Bottom));
            var text = Labels.Get("Proj_Chart_Top", 10, ChartInk.Dim);
            dc.DrawText(text, new Point(peak.X + 3, plot.Top));
        }

        var now = flight.At(Time);
        Marker(dc, P(now.Time, now.Vx), ChartInk.Green, 3.5);
        Marker(dc, P(now.Time, now.Vy), ChartInk.Cyan, 3.5);
    }
}

/// <summary>
/// Height against time for the "fall together" scenes: the dropped and the thrown ball on one curve, or the dart and the
/// monkey closing in on each other at a steady rate.
/// </summary>
public sealed class FallChart : ProjectileChart
{
    static FallChart()
    {
        AffectsRender<FallChart>(SceneProperty, PlaceProperty, EarthGravityProperty, TowerHeightProperty, DartSpeedProperty, MonkeyLetsGoProperty, TimeProperty);
    }

    public static readonly StyledProperty<TogetherScene> SceneProperty =
        AvaloniaProperty.Register<FallChart, TogetherScene>(nameof(Scene), TogetherScene.DropAndThrow);
    public static readonly StyledProperty<ProjectilePlace> PlaceProperty =
        AvaloniaProperty.Register<FallChart, ProjectilePlace>(nameof(Place), ProjectilePlace.Earth);

    /// <summary>Gravity on Earth here, m/s² (the shared observer location's).</summary>
    public static readonly StyledProperty<double> EarthGravityProperty =
        AvaloniaProperty.Register<FallChart, double>(nameof(EarthGravity), ProjectilePhysics.EarthGravity);
    public static readonly StyledProperty<double> TowerHeightProperty =
        AvaloniaProperty.Register<FallChart, double>(nameof(TowerHeight), 20.0);
    public static readonly StyledProperty<double> DartSpeedProperty =
        AvaloniaProperty.Register<FallChart, double>(nameof(DartSpeed), 25.0);
    public static readonly StyledProperty<bool> MonkeyLetsGoProperty =
        AvaloniaProperty.Register<FallChart, bool>(nameof(MonkeyLetsGo), true);
    public static readonly StyledProperty<double> TimeProperty =
        AvaloniaProperty.Register<FallChart, double>(nameof(Time), 0.0);

    public TogetherScene Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
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

    public double TowerHeight
    {
        get => GetValue(TowerHeightProperty);
        set => SetValue(TowerHeightProperty, value);
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

    public double Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var gravity = ProjectilePhysics.Gravity(Place, EarthGravity);
        var plot = new Rect(44, 14, width - 58, height - 42);
        var monkey = Scene == TogetherScene.MonkeyHunter;
        var duration = monkey
            ? ProjectilePhysics.MonkeyEnd(DartSpeed, gravity, MonkeyLetsGo).Time
            : ProjectilePhysics.FallTime(TowerHeight, gravity);
        duration = Math.Max(duration, 0.1);
        var top = monkey
            ? Math.Max(ProjectilePhysics.MonkeyHeight, Enumerable.Range(0, 41).Max(i => ProjectilePhysics.Dart(DartSpeed, gravity, duration * i / 40).Y))
            : TowerHeight;
        var step = ProjectileDiagram.NiceStep(top / 4);
        var max = Math.Ceiling(top / step) * step;
        Point P(double time, double y) => new(plot.Left + (time / duration * plot.Width), plot.Bottom - (Math.Max(0, y) / max * plot.Height));

        ValueAxis(dc, plot, 0, max, step, " m");
        var timeStep = ProjectileDiagram.NiceStep(duration / 5);
        for (var t = 0.0; t <= duration + 1e-9; t += timeStep)
        {
            dc.DrawLine(ChartInk.Grid, P(t, 0), P(t, max));
            AxisLabel(dc, $"{Number(t, timeStep < 1 ? "0.#" : "0")} s", P(t, 0) + new Vector(0, 10), center: true);
        }

        var times = Enumerable.Range(0, 81).Select(i => duration * i / 80).ToList();
        var time = Math.Clamp(Time, 0, duration);
        if (monkey)
        {
            var dart = times.Select(t => P(t, ProjectilePhysics.Dart(DartSpeed, gravity, t).Y)).ToList();
            var ape = times.Select(t => P(t, ProjectilePhysics.MonkeyY(gravity, t, MonkeyLetsGo))).ToList();
            dc.DrawGeometry(null, MakePen(Solid(255, ChartInk.Gold.R, ChartInk.Gold.G, ChartInk.Gold.B), 2.2), Curve(dart));
            dc.DrawGeometry(null, MakePen(Solid(255, ChartInk.Brown.R, ChartInk.Brown.G, ChartInk.Brown.B), 2.2), Curve(ape));

            // The gap between them closes at a steady rate: a straight line would join their tops.
            var dartNow = P(time, ProjectilePhysics.Dart(DartSpeed, gravity, time).Y);
            var apeNow = P(time, ProjectilePhysics.MonkeyY(gravity, time, MonkeyLetsGo));
            dc.DrawLine(ChartInk.Guide, dartNow, apeNow);
            Marker(dc, dartNow, ChartInk.Gold, 3.5);
            Marker(dc, apeNow, ChartInk.Brown, 3.5);

            var dartName = Labels.Get("Proj_Chart_Dart", 10.5, ChartInk.Gold, true);
            var apeName = Labels.Get("Proj_Chart_Monkey", 10.5, ChartInk.Brown, true);
            dc.DrawText(apeName, ape[0] + new Vector(4, 2));
            dc.DrawText(dartName, dart[8] + new Vector(4, -dartName.Height - 2));
            return;
        }

        var fall = times.Select(t => P(t, TowerHeight - ProjectilePhysics.FallDistance(t, gravity))).ToList();
        dc.DrawGeometry(null, MakePen(Solid(255, ChartInk.Pink.R, ChartInk.Pink.G, ChartInk.Pink.B), 5), Curve(fall));
        dc.DrawGeometry(null, MakePen(Solid(255, ChartInk.Cyan.R, ChartInk.Cyan.G, ChartInk.Cyan.B), 1.8, dashed: true), Curve(fall));
        var now = P(time, TowerHeight - ProjectilePhysics.FallDistance(time, gravity));
        Marker(dc, now, ChartInk.Gold, 3.5);
        var both = Labels.Get("Proj_Chart_BothBalls", 10.5, ChartInk.Text);
        dc.DrawText(both, new Point(plot.Right - both.Width - 2, plot.Top));
    }
}
