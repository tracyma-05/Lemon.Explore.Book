using Avalonia;
using Avalonia.Media;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Seasons;

/// <summary>
/// Earth cut through the noon meridian, sunlight coming in from the left: the axis leans towards or away from the Sun
/// by the Sun's declination, and the ray that reaches the observer meets their horizon at the noon altitude.
/// </summary>
public sealed class NoonSunlightView : SeasonFigure
{
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<NoonSunlightView, double>(nameof(Latitude), 40);

    public static readonly StyledProperty<double> DeclinationProperty =
        AvaloniaProperty.Register<NoonSunlightView, double>(nameof(Declination));

    static NoonSunlightView()
    {
        AffectsRender<NoonSunlightView>(LatitudeProperty, DeclinationProperty);
    }

    public double Latitude
    {
        get => GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    public double Declination
    {
        get => GetValue(DeclinationProperty);
        set => SetValue(DeclinationProperty, value);
    }

    public override void Render(DrawingContext dc)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width < 1 || height < 1)
        {
            return;
        }

        var radius = Math.Min(height * 0.36, width * 0.27);
        var center = new Point(width * 0.6, height / 2);
        var declination = SeasonsGeometry.ToRadians(Declination);
        var latitude = SeasonsGeometry.ToRadians(Math.Clamp(Latitude, -90, 90));

        // Screen directions (y down): north along the axis, and along the equator away from the Sun.
        var north = new Vector(-Math.Sin(declination), -Math.Cos(declination));
        var east = new Vector(Math.Cos(declination), -Math.Sin(declination));

        // Parallel sunlight from the left, stopping where it meets the globe.
        for (var i = -3; i <= 3; i++)
        {
            var y = center.Y + (i * radius * 0.3);
            var dy = y - center.Y;
            var end = Math.Abs(dy) < radius ? center.X - Math.Sqrt((radius * radius) - (dy * dy)) : width;
            dc.DrawLine(Paint.Ray, new Point(8, y), new Point(end - 3, y));
            if (Math.Abs(dy) < radius)
            {
                DrawArrowHead(dc, Paint.RayHead, new Point(end - 2, y), new Vector(1, 0), 5);
            }
        }

        Labels.DrawPill(dc, "Seasons_Label_Sunlight", new Point(6, 6), 10.5, Paint.Text);

        // The globe: day side towards the Sun, night side away.
        dc.DrawEllipse(Paint.Globe, null, center, radius, radius);
        using (dc.PushClip(new Rect(center.X, 0, width, height)))
        {
            dc.DrawEllipse(Paint.Night, null, center, radius, radius);
        }

        dc.DrawEllipse(null, Paint.Rim, center, radius, radius);

        // Axis, equator and the observer's parallel.
        dc.DrawLine(Paint.Axis, center - (north * radius * 1.22), center + (north * radius * 1.22));
        dc.DrawLine(Paint.Equator, center - (east * radius), center + (east * radius));
        var n = Labels.Get("Seasons_Label_North", 10.5, Paint.Text, true);
        var tip = center + (north * radius * 1.22);
        dc.DrawText(n, new Point(tip.X - (n.Width / 2), tip.Y - n.Height - 1));

        var middle = center + (north * radius * Math.Sin(latitude));
        var half = east * radius * Math.Cos(latitude);
        dc.DrawLine(Paint.Latitude, middle - half, middle + half);

        // The overhead Sun: where the horizontal ray through the centre meets the globe.
        var overhead = new Point(center.X - radius, center.Y);
        dc.DrawEllipse(Paint.OverheadFill, null, overhead, 3, 3);

        // The observer, on the side facing the Sun.
        var observer = middle - half;
        Vector up = observer - center;
        up /= up.Length;
        var altitude = SeasonsGeometry.NoonAltitude(Latitude, Declination);
        if (altitude > 0)
        {
            // The ray that reaches the observer, their horizon and the noon altitude between them.
            dc.DrawLine(Paint.ObserverRay, new Point(8, observer.Y), observer);
            var along = new Vector(up.Y, -up.X);
            if (along.X > 0)
            {
                along = -along;
            }

            dc.DrawLine(Paint.Horizon, observer - (along * 22), observer + (along * 22));
            DrawAngle(dc, observer, along);
            var text = Labels.Text($"{altitude:0}°", 11.5, Paint.Gold, true);
            dc.DrawText(text, new Point(observer.X - text.Width - 26, observer.Y + (along.Y > 0 ? 2 : -text.Height - 2)));
        }

        dc.DrawEllipse(Paint.ObserverFill, Paint.ObserverPen, observer, 4, 4);

        // Inside the globe, clear of the incoming ray and the angle.
        var you = Labels.Get("Seasons_Label_Observer", 10.5, Paint.Text, true);
        var label = observer - (up * 15);
        dc.DrawText(you, new Point(label.X - (you.Width / 2), label.Y - (you.Height / 2)));
    }

    /// <summary>The angle from the horizon (pointing towards the Sun) up to the incoming ray, which comes from −x.</summary>
    private static void DrawAngle(DrawingContext dc, Point at, Vector horizon)
    {
        const double r = 16;
        var start = at + (horizon * r);
        var end = at + new Vector(-r, 0);

        // Turning from the horizon to (−1, 0) is clockwise on screen (y down) when the horizon points downwards.
        var sweep = horizon.Y > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;
        var arc = new StreamGeometry();
        using (var ctx = arc.Open())
        {
            ctx.BeginFigure(start, false);
            ctx.ArcTo(end, new Size(r, r), 0, false, sweep);
            ctx.EndFigure(false);
        }

        dc.DrawGeometry(null, Paint.AngleArc, arc);
    }

    private static class Paint
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);

        public static readonly IPen Ray = MakePen(Solid(110, 0xFF, 0xE6, 0xA8), 1);
        public static readonly IBrush RayHead = Solid(160, 0xFF, 0xE6, 0xA8);
        public static readonly IBrush Globe = new RadialGradientBrush
        {
            GradientOrigin = new RelativePoint(0.3, 0.5, RelativeUnit.Relative),
            Center = new RelativePoint(0.35, 0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x4C, 0x8F, 0xD0), 0),
                new GradientStop(Color.FromRgb(0x1E, 0x4F, 0x86), 1),
            },
        }.ToImmutable();

        public static readonly IBrush Night = Solid(190, 2, 4, 12);
        public static readonly IPen Rim = MakePen(Solid(90, 255, 255, 255), 1);
        public static readonly IPen Axis = MakePen(Solid(230, 255, 255, 255), 1.4);
        public static readonly IPen Equator = MakePen(Solid(110, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Latitude = MakePen(Solid(240, 0xFF, 0xD3, 0x6B), 1.5);
        public static readonly IBrush OverheadFill = Solid(255, 0xFF, 0xF1, 0xC4);
        public static readonly IPen ObserverRay = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 1.6);
        public static readonly IPen Horizon = MakePen(Solid(220, 0xC9, 0xE7, 0xA8), 1.6);
        public static readonly IPen AngleArc = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 1.2);
        public static readonly IBrush ObserverFill = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IPen ObserverPen = MakePen(Solid(255, 20, 26, 40), 1.2);
    }
}
