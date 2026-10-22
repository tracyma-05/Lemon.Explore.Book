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

/// <summary>What a <see cref="SpectrumChart"/> shows.</summary>
public enum SpectrumKind
{
    /// <summary>Sunlight in space against what of it comes straight through the air to the ground.</summary>
    Sunlight,

    /// <summary>How strongly each colour is scattered by the air's molecules, as 1/λ⁴, and the light of the sky that makes.</summary>
    Scattering,
}

/// <summary>A small chart of light across the rainbow, 400…700 nm, for the side panel of the blue sky page.</summary>
public sealed class SpectrumChart : Control
{
    static SpectrumChart()
    {
        AffectsRender<SpectrumChart>(KindProperty, AltitudeProperty, HazeProperty);
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

    public static readonly StyledProperty<SpectrumKind> KindProperty =
        AvaloniaProperty.Register<SpectrumChart, SpectrumKind>(nameof(Kind), SpectrumKind.Sunlight);

    /// <summary>The Sun's altitude, degrees.</summary>
    public static readonly StyledProperty<double> AltitudeProperty =
        AvaloniaProperty.Register<SpectrumChart, double>(nameof(Altitude), 30.0);

    public static readonly StyledProperty<double> HazeProperty =
        AvaloniaProperty.Register<SpectrumChart, double>(nameof(Haze), 0.3);

    public SpectrumKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double Altitude
    {
        get => GetValue(AltitudeProperty);
        set => SetValue(AltitudeProperty, value);
    }

    public double Haze
    {
        get => GetValue(HazeProperty);
        set => SetValue(HazeProperty, value);
    }

    private const double Shortest = 400;
    private const double Longest = 700;

    private readonly LabelCache _labels;

    public SpectrumChart()
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
        var plot = new Rect(14, 34, width - 28, height - 74);

        // The rainbow along the bottom, with its wavelengths.
        for (var x = 0.0; x < plot.Width; x += 2)
        {
            var nm = Shortest + ((Longest - Shortest) * x / plot.Width);
            dc.DrawRectangle(new SolidColorBrush(SkyOptics.SpectralColour(nm)), null, new Rect(plot.Left + x, plot.Bottom + 4, 2.4, 8));
        }

        foreach (var nm in new[] { 400, 500, 600, 700 })
        {
            var text = _labels.Plain(nm.ToString(CultureInfo.InvariantCulture), 10, Ink.Dim);
            var x = Math.Clamp(X(plot, nm) - (text.Width / 2), plot.Left - 4, plot.Right - text.Width + 4);
            dc.DrawText(text, new Point(x, plot.Bottom + 15));
        }

        var unit = _labels.Get("Sky_Chart_Nm", 10, Ink.Dim);
        dc.DrawText(unit, new Point(plot.Right - unit.Width, plot.Bottom + 27));
        dc.DrawLine(Ink.Axis, plot.BottomLeft, plot.BottomRight);

        if (Kind == SpectrumKind.Scattering) DrawScattering(dc, plot);
        else DrawSunlight(dc, plot);
    }

    /// <summary>Sunlight in space (dashed) and, filled with its colours, what of it reaches the ground.</summary>
    private void DrawSunlight(PaintContext dc, Rect plot)
    {
        var through = SkyOptics.Transmittance(Altitude, Haze);
        var peak = SkyOptics.Wavelengths.Max(SkyOptics.SunSpectrum);
        var bands = SkyOptics.Wavelengths;
        var step = (Longest - Shortest) / (bands.Length - 1);
        for (var b = 0; b < bands.Length; b++)
        {
            var value = SkyOptics.SunSpectrum(bands[b]) * through[b] / peak;
            if (value <= 0) continue;
            var left = X(plot, Math.Max(Shortest, bands[b] - (step / 2)));
            var right = X(plot, Math.Min(Longest, bands[b] + (step / 2)));
            var top = plot.Bottom - (value * plot.Height);
            dc.DrawRectangle(new SolidColorBrush(WithAlpha(SkyOptics.SpectralColour(bands[b]), 0.8)), null,
                new Rect(left, top, Math.Max(1, right - left - 1), plot.Bottom - top));
        }

        dc.DrawGeometry(null, Ink.Space, Curve(plot, nm => SkyOptics.SunSpectrum(nm) / peak));

        // The colour the light that comes through looks.
        var light = SkyOptics.ToRgb(through);
        Legend(dc, plot, "Sky_Chart_Space", "Sky_Chart_Ground", light.Luminance > 0 ? SkyDiagram.SunColour(light) : (Color?)null);
        if (light.Luminance <= 0)
        {
            var text = _labels.Get("Sky_Chart_SunDown", 12, Ink.Text);
            dc.DrawText(text, new Point(plot.Left + ((plot.Width - text.Width) / 2), plot.Top + (plot.Height / 2) - 8));
        }
    }

    /// <summary>How strongly each colour scatters, as bars against red, and (dashed) the light of the sky.</summary>
    private void DrawScattering(PaintContext dc, Rect plot)
    {
        var bands = SkyOptics.Wavelengths;
        var most = SkyOptics.Rayleigh(Shortest);
        var step = (Longest - Shortest) / (bands.Length - 1);
        for (var b = 0; b < bands.Length; b++)
        {
            var value = SkyOptics.Rayleigh(bands[b]) / most;
            var left = X(plot, Math.Max(Shortest, bands[b] - (step / 2)));
            var right = X(plot, Math.Min(Longest, bands[b] + (step / 2)));
            var top = plot.Bottom - (value * plot.Height);
            dc.DrawRectangle(new SolidColorBrush(WithAlpha(SkyOptics.SpectralColour(bands[b]), 0.8)), null,
                new Rect(left + 1, top, Math.Max(1, right - left - 2), plot.Bottom - top));
        }

        // Sunlight times scattering: what the sky sends you, peaking in the blue.
        var sky = SkyOptics.Wavelengths.Max(nm => SkyOptics.SunSpectrum(nm) * SkyOptics.Rayleigh(nm));
        dc.DrawGeometry(null, Ink.Space, Curve(plot, nm => SkyOptics.SunSpectrum(nm) * SkyOptics.Rayleigh(nm) / sky));

        var times = (SkyOptics.Rayleigh(Shortest) / SkyOptics.Rayleigh(Longest)).ToString("0.0", CultureInfo.InvariantCulture);
        var text = _labels.Text(LocalizationService.Instance.Format("Sky_Chart_Times", times), 11, Ink.Text);
        dc.DrawText(text, new Point(X(plot, Shortest) + 22, plot.Top + 2));

        var skyLight = SkyOptics.ToRgb(SkyOptics.Wavelengths.Select(SkyOptics.Rayleigh).ToArray());
        Legend(dc, plot, "Sky_Chart_SkyLight", "Sky_Chart_Scatter", SkyOptics.Hue(skyLight));
    }

    private void Legend(PaintContext dc, Rect plot, string dashedKey, string filledKey, Color? swatch)
    {
        var x = plot.Left;
        dc.DrawLine(Ink.Space, new Point(x, 12), new Point(x + 16, 12));
        var dashed = _labels.Get(dashedKey, 11, Ink.Dim);
        dc.DrawText(dashed, new Point(x + 21, 12 - (dashed.Height / 2)));
        x += 21 + dashed.Width + 14;
        dc.DrawRectangle(Ink.FilledKey, null, new Rect(x, 7, 14, 10));
        var filled = _labels.Get(filledKey, 11, Ink.Dim);
        dc.DrawText(filled, new Point(x + 19, 12 - (filled.Height / 2)));

        if (swatch is { } colour)
        {
            var centre = new Point(plot.Right - 10, 12);
            dc.DrawEllipse(new SolidColorBrush(colour), Ink.SwatchEdge, centre, 9, 9);
        }
    }

    private static double X(Rect plot, double nm) => plot.Left + ((nm - Shortest) / (Longest - Shortest) * plot.Width);

    private static StreamGeometry Curve(Rect plot, Func<double, double> value)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var nm = Shortest; nm <= Longest + 1e-9; nm += 5)
            {
                var point = new Point(X(plot, nm), plot.Bottom - (Math.Clamp(value(nm), 0, 1.05) * plot.Height));
                if (nm == Shortest) context.BeginFigure(point, false);
                else context.LineTo(point);
            }

            context.EndFigure(false);
        }
        return geometry;
    }

    private static class Ink
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly IPen Axis = MakePen(Solid(70, 255, 255, 255), 1);
        public static readonly IPen Space = MakePen(Solid(220, 255, 255, 255), 1.5, dashed: true);
        public static readonly IBrush FilledKey = SpaceDrawing.LinearGradient(Color.FromRgb(0x7F, 0x6B, 0xFF), Color.FromRgb(0xFF, 0x6B, 0x4A), 0);
        public static readonly IPen SwatchEdge = MakePen(Solid(200, 255, 255, 255), 1.2);
    }
}
