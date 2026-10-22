using System.ComponentModel;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.StarLife;

/// <summary>
/// How big the three kinds of remains are. Left: a white dwarf beside Earth, about the same size. Right, a thousand
/// times closer in: a neutron star about as wide as a city, and a black hole's event horizon a few tens of kilometres
/// across. The one the chosen star leaves is lit, at its own size.
/// </summary>
public sealed class RemnantView : Control
{
    static RemnantView()
    {
        AffectsRender<RemnantView>(MassProperty);
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

    public static readonly StyledProperty<double> MassProperty =
        AvaloniaProperty.Register<RemnantView, double>(nameof(Mass), 1.0);

    public double Mass
    {
        get => GetValue(MassProperty);
        set => SetValue(MassProperty, value);
    }

    private readonly LabelCache _labels;

    public RemnantView()
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
        if (width < 240 || height < 150) return;

        var kind = StarModel.ClassOf(Mass);
        var half = width / 2;
        dc.DrawLine(Paint.Divider, new Point(half, 12), new Point(half, height - 12));

        // A white dwarf and Earth, on one scale: this star's white dwarf, or a typical one.
        var whiteMass = kind is StarClass.RedDwarf or StarClass.SunLike ? StarModel.WhiteDwarfMass(Mass) : 0.6;
        var whiteKm = StarModel.WhiteDwarfRadiusKm(whiteMass);
        var scale = (half * 0.2) / Math.Max(whiteKm, StarModel.EarthRadiusKm);
        var y = height * 0.44;
        var white = kind is StarClass.RedDwarf or StarClass.SunLike;
        var earthAt = new Point(half * 0.3, y);
        var dwarfAt = new Point(half * 0.72, y);
        dc.DrawEllipse(Paint.Earth, null, earthAt, StarModel.EarthRadiusKm * scale, StarModel.EarthRadiusKm * scale);
        dc.DrawEllipse(Paint.WhiteDwarf, white ? Paint.Lit : null, dwarfAt, whiteKm * scale, whiteKm * scale);
        Caption(dc, "SolarSystem_Earth_Name", earthAt, StarModel.EarthRadiusKm * scale, false, null);
        Caption(dc, "Star_Stage_WhiteDwarf", dwarfAt, whiteKm * scale, white, Km(whiteKm * 2));

        // A neutron star and a black hole, on a scale a thousand times larger.
        var holeKm = StarModel.SchwarzschildRadiusKm(kind == StarClass.VeryMassive ? StarModel.BlackHoleMass(Mass) : 10);
        var near = (half * 0.2) / Math.Max(holeKm, StarModel.NeutronStarRadiusKm);
        var neutronAt = new Point(half + (half * 0.28), y);
        var holeAt = new Point(half + (half * 0.7), y);
        dc.DrawEllipse(Paint.Neutron, kind == StarClass.Massive ? Paint.Lit : null, neutronAt, StarModel.NeutronStarRadiusKm * near, StarModel.NeutronStarRadiusKm * near);
        dc.DrawEllipse(Brushes.Black, kind == StarClass.VeryMassive ? Paint.Lit : Paint.Horizon, holeAt, holeKm * near, holeKm * near);
        Caption(dc, "Star_Stage_NeutronStar", neutronAt, StarModel.NeutronStarRadiusKm * near, kind == StarClass.Massive, Km(StarModel.NeutronStarRadiusKm * 2));
        Caption(dc, "Star_Stage_BlackHole", holeAt, holeKm * near, kind == StarClass.VeryMassive, Km(holeKm * 2));

        // Scale bars: 10,000 km on the left, 20 km on the right.
        ScaleBar(dc, 10, height - 18, 10_000 * scale, Km(10_000));
        ScaleBar(dc, half + 10, height - 18, 20 * near, Km(20));
    }

    private void Caption(PaintContext dc, string key, Point at, double radius, bool lit, string? size)
    {
        var name = _labels.Get(key, 10.5, lit ? StarDrawing.Gold : StarDrawing.Text, lit);
        var top = at.Y + radius + 6;
        dc.DrawText(name, new Point(at.X - (name.Width / 2), top));
        if (size is null) return;
        var text = _labels.Text(size, 9.5, StarDrawing.DimText);
        dc.DrawText(text, new Point(at.X - (text.Width / 2), top + name.Height));
    }

    private void ScaleBar(PaintContext dc, double x, double y, double length, string text)
    {
        dc.DrawLine(Paint.Scale, new Point(x, y), new Point(x + length, y));
        dc.DrawLine(Paint.Scale, new Point(x, y - 3), new Point(x, y + 3));
        dc.DrawLine(Paint.Scale, new Point(x + length, y - 3), new Point(x + length, y + 3));
        var label = _labels.Text(text, 9, StarDrawing.DimText);
        dc.DrawText(label, new Point(x + length + 5, y - (label.Height / 2)));
    }

    /// <summary>A width in kilometres, e.g. "24 km across".</summary>
    private static string Km(double km) =>
        LocalizationService.Instance.Format("Star_Km", StarFormat.Km(km));

    private static class Paint
    {
        public static readonly IPen Divider = MakePen(Solid(30, 255, 255, 255), 1);
        public static readonly IBrush Earth = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x6F, 0xB4, 0xFF)), new ImmutableGradientStop(1, Color.FromRgb(0x1D, 0x4E, 0x89))],
            gradientOrigin: new RelativePoint(0.4, 0.4, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.5, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.5, RelativeUnit.Relative));
        public static readonly IBrush WhiteDwarf = SpaceDrawing.RadialGradient((Color.FromRgb(0xFF, 0xFF, 0xFF), 0), (Color.FromRgb(0xB8, 0xC8, 0xFF), 1));
        public static readonly IBrush Neutron = SpaceDrawing.RadialGradient((Color.FromRgb(0xF0, 0xF6, 0xFF), 0), (Color.FromRgb(0x80, 0xA0, 0xFF), 1));
        public static readonly IPen Lit = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 2);
        public static readonly IPen Horizon = MakePen(Solid(170, 0xFF, 0xE6, 0xC0), 1.2);
        public static readonly IPen Scale = MakePen(Solid(170, 255, 255, 255), 1);
    }
}
