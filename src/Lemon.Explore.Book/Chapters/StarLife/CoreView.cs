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
/// Inside the star at its current stage, cut in half and not to scale: which layers it has and which of them are
/// burning. Hydrogen in the core on the main sequence; a helium core with a burning shell in a giant; the onion of
/// shells round an iron core just before a supernova.
/// </summary>
public sealed class CoreView : Control
{
    static CoreView()
    {
        AffectsRender<CoreView>(StageProperty, MassProperty);
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

    public static readonly StyledProperty<StarStage> StageProperty =
        AvaloniaProperty.Register<CoreView, StarStage>(nameof(Stage), StarStage.MainSequence);

    public static readonly StyledProperty<double> MassProperty =
        AvaloniaProperty.Register<CoreView, double>(nameof(Mass), 1.0);

    public StarStage Stage
    {
        get => GetValue(StageProperty);
        set => SetValue(StageProperty, value);
    }

    /// <summary>For a red dwarf's white dwarf, made of helium rather than carbon and oxygen.</summary>
    public double Mass
    {
        get => GetValue(MassProperty);
        set => SetValue(MassProperty, value);
    }

    private static readonly Color Gas = Color.FromRgb(0xC8, 0x70, 0x40);
    private static readonly Color Envelope = Color.FromRgb(0xE0, 0x90, 0x50);
    private static readonly Color Fusion = Color.FromRgb(0xFF, 0xF0, 0xA0);
    private static readonly Color Helium = Color.FromRgb(0x9A, 0x9A, 0xA8);
    private static readonly Color Carbon = Color.FromRgb(0x70, 0x78, 0x90);

    private readonly LabelCache _labels;

    public CoreView()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    /// <summary>The layers from the outside in: name, colour, outer radius (1 is the star's edge), and whether it is burning.</summary>
    private (string Key, Color Color, double Radius, bool Burning)[] Layers() => Stage switch
    {
        StarStage.Protostar => [("Star_Layer_Gas", Gas, 1, false), ("Star_Layer_HotCore", Color.FromRgb(0xFF, 0xA0, 0x60), 0.35, false)],
        StarStage.MainSequence or StarStage.BlueDwarf => [("Star_Layer_Envelope", Envelope, 1, false), ("Star_Layer_HCore", Fusion, 0.3, true)],
        StarStage.Subgiant or StarStage.RedGiant => [("Star_Layer_Envelope", Envelope, 1, false), ("Star_Layer_HShell", Fusion, 0.26, true), ("Star_Layer_HeCore", Helium, 0.19, false)],
        StarStage.HeliumBurning or StarStage.Supergiant or StarStage.RedSupergiant =>
            [("Star_Layer_Envelope", Envelope, 1, false), ("Star_Layer_HShell", Fusion, 0.32, true), ("Star_Layer_HeCore", Helium, 0.26, false), ("Star_Layer_HeBurning", Color.FromRgb(0xFF, 0xC8, 0x80), 0.15, true)],
        StarStage.Agb => [("Star_Layer_Envelope", Envelope, 1, false), ("Star_Layer_HShell", Fusion, 0.34, true), ("Star_Layer_HeShell", Color.FromRgb(0xFF, 0xC8, 0x80), 0.26, true), ("Star_Layer_CoCore", Carbon, 0.17, false)],
        StarStage.PlanetaryNebula => [("Star_Layer_Ejected", StarDrawing.Nebula, 1, false), ("Star_Layer_Space", Color.FromRgb(6, 8, 15), 0.78, false), ("Star_Layer_CoCore", Color.FromRgb(0xE8, 0xF0, 0xFF), 0.14, false)],
        StarStage.WhiteDwarf => [(Mass < 0.5 ? "Star_Layer_HeDegenerate" : "Star_Layer_CoDegenerate", Color.FromRgb(0xD8, 0xE4, 0xFF), 1, false)],
        StarStage.LateBurning =>
        [
            ("Star_Layer_H", Envelope, 1, false), ("Star_Layer_He", Color.FromRgb(0xF0, 0xB0, 0x60), 0.66, true),
            ("Star_Layer_C", Color.FromRgb(0xE8, 0xC8, 0x70), 0.52, true), ("Star_Layer_Ne", Color.FromRgb(0xC8, 0xD8, 0x80), 0.41, true),
            ("Star_Layer_O", Color.FromRgb(0x90, 0xC8, 0xD0), 0.31, true), ("Star_Layer_Si", Color.FromRgb(0xA0, 0xA8, 0xE0), 0.22, true),
            ("Star_Layer_Fe", Color.FromRgb(0x80, 0x70, 0x78), 0.13, false),
        ],
        StarStage.Supernova => [("Star_Layer_Shock", StarDrawing.Blast, 1, false), ("Star_Layer_Infall", Color.FromRgb(0x90, 0x50, 0x40), 0.7, false), ("Star_Layer_Collapse", Color.FromRgb(0xC8, 0xDC, 0xFF), 0.12, false)],
        StarStage.NeutronStar => [("Star_Layer_Crust", Color.FromRgb(0x90, 0xA0, 0xC0), 1, false), ("Star_Layer_Neutrons", Color.FromRgb(0xB8, 0xC8, 0xFF), 0.88, false)],
        _ => [("Star_Layer_Horizon", Color.FromRgb(0, 0, 0), 1, false)],
    };

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 200 || height < 120) return;

        var radius = Math.Min((height / 2) - 14, width * 0.27);
        var center = new Point(16 + radius, height / 2);
        var layers = Layers();

        // A half star cut open: each layer a filled disc, the inner ones on top, burning ones glowing.
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, center.X, height)));
        foreach (var (_, color, r, burning) in layers)
        {
            IBrush fill = burning
                ? SpaceDrawing.RadialGradient((Lerp(color, Colors.White, 0.4), 0), (color, 1))
                : SpaceDrawing.Solid(color);
            dc.DrawEllipse(fill, Paint.Edge, center, radius * r, radius * r);
        }

        dc.Pop();
        dc.DrawLine(Paint.Cut, new Point(center.X, center.Y - radius), new Point(center.X, center.Y + radius));
        if (Stage == StarStage.BlackHole) dc.DrawEllipse(null, Paint.Horizon, center, radius, radius);

        // The names on the right, joined to their layers.
        var columnX = center.X + 26;
        var rowHeight = Math.Min(24, (height - 16) / layers.Length);
        var firstY = (height / 2) - (rowHeight * (layers.Length - 1) / 2);
        for (var i = 0; i < layers.Length; i++)
        {
            var (key, color, r, burning) = layers[i];
            var inner = i + 1 < layers.Length ? layers[i + 1].Radius : 0;
            var mid = radius * (r + inner) / 2;
            var y = firstY + (i * rowHeight);
            var dy = Math.Clamp(y - center.Y, -mid * 0.95, mid * 0.95);
            var anchor = new Point(center.X - Math.Sqrt(Math.Max(0, (mid * mid) - (dy * dy))) * 0.35, center.Y + dy);
            var label = _labels.Get(key, 10.5, burning ? StarDrawing.Gold : StarDrawing.Text, burning);
            var labelAt = new Point(columnX, y - (label.Height / 2));
            dc.DrawLine(Paint.Leader, anchor, new Point(columnX - 4, y));
            dc.DrawEllipse(Paint.Dot, null, anchor, 2, 2);
            dc.DrawText(label, labelAt);
        }
    }

    private static class Paint
    {
        public static readonly IPen Edge = MakePen(Solid(70, 0, 0, 0), 1);
        public static readonly IPen Cut = MakePen(Solid(140, 255, 255, 255), 1);
        public static readonly IPen Horizon = MakePen(Solid(200, 0xFF, 0xE6, 0xC0), 1.4);
        public static readonly IPen Leader = MakePen(Solid(120, 255, 255, 255), 1);
        public static readonly IBrush Dot = Solid(220, 255, 255, 255);
    }
}
