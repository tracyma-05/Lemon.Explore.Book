using System.ComponentModel;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Kepler;

/// <summary>
/// What the eccentricity measures. Above: the orbit's shape with its centre, the two foci, a and c, and e = c / a.
/// Below: a scale from 0 (a circle) to 1 with real bodies on it: the planets near 0, Halley's comet near 1.
/// </summary>
public sealed class EccentricityView : Control
{
    static EccentricityView()
    {
        AffectsRender<EccentricityView>(EccentricityProperty);
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

    public static readonly StyledProperty<double> EccentricityProperty =
        AvaloniaProperty.Register<EccentricityView, double>(nameof(Eccentricity), 0.5);

    public double Eccentricity
    {
        get => GetValue(EccentricityProperty);
        set => SetValue(EccentricityProperty, value);
    }

    /// <summary>Bodies on the scale, and whether their label goes above it (else below), so neighbours do not overlap.</summary>
    private static readonly (string Id, bool Above)[] Marks =
        [("Earth", true), ("Mars", false), ("Mercury", true), ("Pluto", false), ("Halley", true)];

    private readonly LabelCache _labels;

    public EccentricityView()
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
        if (width < 200 || height < 160) return;

        var e = Math.Clamp(Eccentricity, 0, KeplerGeometry.MaxEccentricity);

        // The shape, as wide as fits while b stays clear of the scale: a, then b and c from e.
        const double maxB = 52;
        var ratio = Math.Sqrt(1 - (e * e));
        var a = Math.Min((width - 40) / 2, maxB / Math.Max(ratio, 1e-3));
        var b = a * ratio;
        var c = a * e;
        var center = new Point(width / 2, 30 + maxB);
        dc.DrawLine(KeplerDrawing.AxisPen, new Point(center.X - a, center.Y), new Point(center.X + a, center.Y));
        dc.DrawEllipse(null, KeplerDrawing.OrbitPen, center, a, Math.Max(b, 0.5));
        dc.DrawEllipse(Paint.Sun, null, new Point(center.X + c, center.Y), 4.5, 4.5);
        dc.DrawEllipse(Paint.Focus, KeplerDrawing.DotOutline, new Point(center.X - c, center.Y), 3, 3);
        dc.DrawEllipse(Paint.Centre, null, center, 2, 2);

        // a above the axis from the centre to the end, c below it from the centre to the Sun.
        var aY = center.Y - Math.Max(b, 10) - 8;
        DrawBracket(dc, center.X, center.X + a, aY, "a", KeplerDrawing.Text, above: true);
        if (c > 6) DrawBracket(dc, center.X, center.X + c, center.Y + 8, "c", KeplerDrawing.Gold, above: false);

        var formula = LocalizationService.Instance.Format("Kepler_Ecc_Formula", e.ToString("0.00"));
        var text = _labels.Text(formula, 12, KeplerDrawing.Text, true);
        dc.DrawText(text, new Point((width - text.Width) / 2, center.Y + maxB + 8));

        // The scale from 0 to 1.
        const double left = 18;
        var right = width - 18;
        var y = height - 40;
        double X(double value) => left + ((right - left) * value);
        dc.DrawRectangle(Paint.Track, null, new Rect(left, y - 2, right - left, 4));
        foreach (var (value, key) in new[] { (0.0, "Kepler_Ecc_Circle"), (1.0, "Kepler_Ecc_Open") })
        {
            var end = _labels.Get(key, 9.5, KeplerDrawing.DimText);
            var x = Math.Clamp(X(value) - (end.Width / 2), 2, width - end.Width - 2);
            dc.DrawText(end, new Point(x, y + 20));
        }

        foreach (var (id, above) in Marks)
        {
            var body = KeplerGeometry.Body(id);
            var x = X(body.Eccentricity);
            dc.DrawLine(Paint.Tick, new Point(x, y - 6), new Point(x, y + 6));
            var name = _labels.Get(KeplerDiagram.BodyNameKey(id), 9.5, KeplerDrawing.DimText);
            var left2 = Math.Clamp(x - (name.Width / 2), 2, width - name.Width - 2);
            dc.DrawText(name, new Point(left2, above ? y - name.Height - 6 : y + 6));
        }

        var now = X(e);
        dc.DrawEllipse(Paint.Marker, KeplerDrawing.DotOutline, new Point(now, y), 6, 6);
    }

    private void DrawBracket(PaintContext dc, double from, double to, double y, string name, Color color, bool above)
    {
        var pen = MakePen(SpaceDrawing.Solid(WithAlpha(color, 0.7)), 1);
        dc.DrawLine(pen, new Point(from, y), new Point(to, y));
        dc.DrawLine(pen, new Point(from, y - 3), new Point(from, y + 3));
        dc.DrawLine(pen, new Point(to, y - 3), new Point(to, y + 3));
        var text = _labels.Text(name, 11, color, true);
        dc.DrawText(text, new Point(((from + to) / 2) - (text.Width / 2), above ? y - text.Height - 1 : y + 1));
    }

    private static class Paint
    {
        public static readonly IBrush Sun = SpaceDrawing.Solid(KeplerDrawing.Gold);
        public static readonly IBrush Focus = SpaceDrawing.Solid(KeplerDrawing.Cyan);
        public static readonly IBrush Centre = Solid(200, 255, 255, 255);
        public static readonly IBrush Track = SpaceDrawing.LinearGradient(WithAlpha(KeplerDrawing.Slow, 0.5), WithAlpha(KeplerDrawing.Fast, 0.5), 0);
        public static readonly IPen Tick = MakePen(Solid(200, 255, 255, 255), 1.2);
        public static readonly IBrush Marker = SpaceDrawing.Solid(KeplerDrawing.Gold);
    }
}
