using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.BlueSky;

/// <summary>
/// How much air sunlight crosses against the Sun's altitude (on a log scale, 1 to 40 times the air overhead), with the
/// Sun now marked: hardly changing while the Sun is high, then shooting up in its last few degrees.
/// </summary>
public sealed class AirMassChart : Control
{
    static AirMassChart()
    {
        AffectsRender<AirMassChart>(AltitudeProperty, LatitudeProperty);
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

    public static readonly StyledProperty<double> AltitudeProperty =
        AvaloniaProperty.Register<AirMassChart, double>(nameof(Altitude), 30.0);

    public double Altitude
    {
        get => GetValue(AltitudeProperty);
        set => SetValue(AltitudeProperty, value);
    }

    /// <summary>The day's latitude, for the noon line.</summary>
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<AirMassChart, double>(nameof(Latitude), SkyOptics.DefaultLatitude);

    public double Latitude
    {
        get => GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    private const double MaxAltitude = 90;
    private const double MaxAirMass = 40;

    private readonly LabelCache _labels;

    public AirMassChart()
    {
        _labels = new LabelCache(this);
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
        if (width < 80 || height < 80) return;

        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
        var plot = new Rect(34, 16, width - 48, height - 50);

        // Grid: air mass on a log scale, altitude every 15°.
        foreach (var mass in new[] { 1, 2, 5, 10, 20, 40 })
        {
            var y = Y(plot, mass);
            dc.DrawLine(Ink.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            var text = _labels.Plain($"×{mass}", 10, Ink.Dim);
            dc.DrawText(text, new Point(plot.Left - text.Width - 5, y - (text.Height / 2)));
        }

        for (var altitude = 0; altitude <= 90; altitude += 15)
        {
            var x = X(plot, altitude);
            dc.DrawLine(Ink.Grid, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var text = _labels.Plain(altitude.ToString(CultureInfo.InvariantCulture) + "°", 10, Ink.Dim);
            dc.DrawText(text, new Point(x - (text.Width / 2), plot.Bottom + 4));
        }

        var axis = _labels.Get("Sky_Chart_Altitude", 10.5, Ink.Dim);
        dc.DrawText(axis, new Point(plot.Right - axis.Width, plot.Bottom + 18));

        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(new Point(X(plot, 0), Y(plot, SkyOptics.AirMass(0))), false);
            for (var altitude = 0.25; altitude <= MaxAltitude; altitude += altitude < 10 ? 0.25 : 1)
            {
                figure.LineTo(new Point(X(plot, altitude), Y(plot, SkyOptics.AirMass(altitude))));
            }

            figure.EndFigure(false);
        }
        dc.DrawGeometry(null, Ink.Curve, geometry);

        // Today's highest Sun, and the Sun now.
        var noon = X(plot, SkyOptics.NoonAltitude(Latitude));
        dc.DrawLine(Ink.Noon, new Point(noon, plot.Top), new Point(noon, plot.Bottom));
        var noonText = _labels.Get("Sky_Chart_Noon", 10, Ink.Dim);
        dc.DrawText(noonText, new Point(noon + 4, plot.Top + 2));

        var now = Math.Max(0, Altitude);
        var point = new Point(X(plot, now), Y(plot, SkyOptics.AirMass(now)));
        dc.DrawEllipse(Ink.NowHalo, null, point, 9, 9);
        dc.DrawEllipse(Ink.Now, null, point, 4.5, 4.5);
        var label = Altitude > 0
            ? $"×{SkyOptics.AirMass(Altitude).ToString(SkyOptics.AirMass(Altitude) < 10 ? "0.0" : "0", CultureInfo.InvariantCulture)}"
            : LocalizationService.Instance.GetString("Sky_Chart_SunDown");
        var text2 = _labels.Text(label, 11.5, Ink.Gold, true);
        var at = new Point(point.X + 10, point.Y - text2.Height - 4);
        if (at.X + text2.Width > plot.Right) at = at.WithX(point.X - text2.Width - 10);
        at = at.WithY(Math.Max(plot.Top, at.Y));
        dc.DrawText(text2, at);
    }

    private static double X(Rect plot, double altitude) => plot.Left + (altitude / MaxAltitude * plot.Width);

    private static double Y(Rect plot, double mass) =>
        plot.Bottom - (Math.Log(Math.Clamp(mass, 1, MaxAirMass)) / Math.Log(MaxAirMass) * plot.Height);

    private static class Ink
    {
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);
        public static readonly IPen Curve = MakePen(Solid(230, 0xFF, 0x9F, 0x5A), 2);
        public static readonly IPen Noon = MakePen(Solid(80, 255, 255, 255), 1, dashed: true);
        public static readonly IBrush Now = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IBrush NowHalo = Solid(70, 0xFF, 0xD3, 0x6B);
    }
}
