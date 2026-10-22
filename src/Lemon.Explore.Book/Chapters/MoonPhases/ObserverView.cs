using System.ComponentModel;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.MoonPhases;

/// <summary>
/// Earth from above the North Pole with sunlight from the left, the observer carried round by Earth's turning (noon
/// facing the Sun, midnight facing away) and the Moon on its orbit. The Moon is up when it is on the observer's side
/// of their horizon.
/// </summary>
public sealed class ObserverView : Control
{
    static ObserverView()
    {
        AffectsRender<ObserverView>(AgeProperty, TimeOfDayProperty);
    }

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

    public static readonly StyledProperty<double> AgeProperty =
        AvaloniaProperty.Register<ObserverView, double>(nameof(Age), 0.0);

    /// <summary>Local solar time in hours.</summary>
    public static readonly StyledProperty<double> TimeOfDayProperty =
        AvaloniaProperty.Register<ObserverView, double>(nameof(TimeOfDay), 20.0);

    public double Age
    {
        get => GetValue(AgeProperty);
        set => SetValue(AgeProperty, value);
    }

    public double TimeOfDay
    {
        get => GetValue(TimeOfDayProperty);
        set => SetValue(TimeOfDayProperty, value);
    }

    private readonly LabelCache _labels;
    private readonly PlanetRenderer _earth = MoonDrawing.TopRenderer("Earth");

    public ObserverView()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 40 || height < 40) return;

        var center = new Point((width / 2) + 14, height / 2);
        var orbit = Math.Min(width / 2 - 30, (height / 2) - 18);
        var earthRadius = orbit * 0.36;

        // Sunlight from the left.
        for (var k = -3; k <= 3; k++)
        {
            var y = center.Y + (k * orbit * 0.32);
            dc.DrawLine(Paint.Ray, new Point(6, y), new Point(32, y));
            DrawArrowHead(dc, Paint.RayArrow, new Point(36, y), new Vector(1, 0), 5);
        }

        var sunlight = _labels.Get("MoonPhases_Label_Sunlight", 10, MoonDrawing.Gold);
        dc.DrawText(sunlight, new Point(6, 4));

        // The observer: noon faces the Sun, and Earth turns counter-clockwise, so 18:00 is at the bottom.
        var angle = SeasonsGeometry.ToRadians(180 + (15 * (TimeOfDay - 12)));
        var up = new Vector(Math.Cos(angle), -Math.Sin(angle));
        var you = center + (up * earthRadius);
        var along = new Vector(-up.Y, up.X);

        // Their sky: the half of space above their horizon.
        var sky = orbit + 14;
        dc.DrawGeometry(Paint.Sky, null, Polygon(
            you + (along * sky), you + (along * sky) + (up * sky), you - (along * sky) + (up * sky), you - (along * sky)));
        dc.DrawLine(Paint.Horizon, you - (along * sky), you + (along * sky));

        dc.DrawEllipse(null, Paint.Orbit, center, orbit, orbit);
        DrawSphere(dc, _earth, center, earthRadius, new LightVector(-1, 0, 0.05), RenderScale(), 1.2);

        // Times of day round Earth; each makes way for the observer's own label as they pass.
        foreach (var (hours, key) in new[] { (12.0, "MoonPhases_Clock_Noon"), (18.0, "MoonPhases_Clock_Evening"), (0.0, "MoonPhases_Clock_Midnight"), (6.0, "MoonPhases_Clock_Morning") })
        {
            var gap = Math.Abs(SeasonsGeometry.Normalize180(15 * (hours - TimeOfDay)));
            var visibility = Noise.SmoothStep(14, 36, gap);
            if (visibility < 0.01) continue;

            var a = SeasonsGeometry.ToRadians(180 + (15 * (hours - 12)));
            var direction = new Vector(Math.Cos(a), -Math.Sin(a));
            var text = _labels.Get(key, 9.5, MoonDrawing.DimText);
            var at = center + (direction * (earthRadius + 4 + (text.Height / 2)));
            if (Math.Abs(direction.X) > 0.5) at += new Vector(direction.X * text.Width / 2, 0);
            dc.PushOpacity(visibility);
            dc.DrawText(text, new Point(at.X - (text.Width / 2), at.Y - (text.Height / 2)));
            dc.Pop();
        }

        dc.DrawEllipse(Paint.YouFill, Paint.YouOutline, you, 4, 4);
        var youText = _labels.Get("Seasons_Label_Observer", 10.5, MoonDrawing.Gold, true);
        var youAt = you + (up * 12);
        dc.DrawText(youText, new Point(youAt.X - (youText.Width / 2), youAt.Y - (youText.Height / 2)));

        // The Moon, lit from the left; ringed when it is above the observer's horizon.
        var elongation = MoonPhaseGeometry.Elongation(Age);
        var (sin, cos) = Math.SinCos(SeasonsGeometry.ToRadians(elongation));
        var moon = new Point(center.X - (cos * orbit), center.Y + (sin * orbit));
        var visible = Vector.Dot(moon - center, up) > 0;
        MoonDrawing.DrawPhaseIcon(dc, moon, 7, 90, new Vector(-1, 0), visible ? MoonDrawing.CurrentRing : null);
    }

    private static class Paint
    {
        public static readonly IPen Ray = MakePen(Solid(120, 255, 226, 150), 1);
        public static readonly IBrush RayArrow = Solid(150, 255, 226, 150);
        public static readonly IPen Orbit = MakePen(Solid(80, 255, 255, 255), 1, dashed: true);
        public static readonly IBrush Sky = Solid(34, 0x8E, 0xC5, 0xFF);
        public static readonly IPen Horizon = MakePen(Solid(150, 0x8E, 0xC5, 0xFF), 1.1);
        public static readonly IBrush YouFill = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IPen YouOutline = MakePen(Solid(255, 20, 26, 40), 1.2);
    }
}
