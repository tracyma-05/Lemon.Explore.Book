using System.ComponentModel;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.MoonPhases;

/// <summary>
/// The Moon as seen from Earth at its current age: its lit part with the bright limb towards the Sun, the rest of the
/// disc outlined. Upside down (lit on the other side) in the southern hemisphere.
/// </summary>
public sealed class MoonDiscView : Control
{
    static MoonDiscView()
    {
        AffectsRender<MoonDiscView>(AgeProperty, IsSouthernProperty);
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
        AvaloniaProperty.Register<MoonDiscView, double>(nameof(Age), 0.0);

    public static readonly StyledProperty<bool> IsSouthernProperty =
        AvaloniaProperty.Register<MoonDiscView, bool>(nameof(IsSouthern), false);

    /// <summary>Days since new moon.</summary>
    public double Age
    {
        get => GetValue(AgeProperty);
        set => SetValue(AgeProperty, value);
    }

    public bool IsSouthern
    {
        get => GetValue(IsSouthernProperty);
        set => SetValue(IsSouthernProperty, value);
    }

    private readonly LabelCache _labels;
    private PlanetRenderer? _renderer;
    private bool _rendererSouthern;

    public MoonDiscView()
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
        if (width < 20 || height < 20) return;

        if (_renderer is null || _rendererSouthern != IsSouthern)
        {
            _renderer = MoonDrawing.DiscRenderer(IsSouthern);
            _rendererSouthern = IsSouthern;
        }

        var center = new Point(width / 2, (height / 2) - 6);
        var radius = Math.Min(width, height - 30) * 0.4;
        var elongation = MoonPhaseGeometry.Elongation(Age);
        var litSide = MoonDrawing.LitSide(elongation, IsSouthern);

        // The whole disc, faintly, so even the new moon has a place; then the lit part.
        dc.DrawEllipse(Paint.Dark, Paint.Outline, center, radius, radius);
        var lit = MoonDrawing.LitShape(center, radius, elongation, litSide);
        if (lit is not null)
        {
            dc.PushClip(lit);
            DrawSphere(dc, _renderer, center, radius, MoonDrawing.DiscLight(elongation, litSide), RenderScale());
            dc.Pop();
        }

        // Which side the Sun is on.
        var sun = _labels.Get("MoonPhases_Label_SunThisWay", 10.5, MoonDrawing.Gold);
        var arrowY = height - 16;
        var tip = new Point(center.X + (litSide.X * (radius + 6)), arrowY);
        var tail = new Point(center.X + (litSide.X * (radius - 30)), arrowY);
        dc.DrawLine(Paint.Arrow, tail, tip);
        DrawArrowHead(dc, Paint.ArrowHead, tip, tip - tail, 6);
        var textX = litSide.X > 0 ? tail.X - sun.Width - 6 : tail.X + 6;
        dc.DrawText(sun, new Point(textX, arrowY - (sun.Height / 2)));
    }

    private static class Paint
    {
        public static readonly IBrush Dark = Solid(255, 0x16, 0x19, 0x20);
        public static readonly IPen Outline = MakePen(Solid(70, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Arrow = MakePen(Solid(200, 0xFF, 0xD3, 0x6B), 1.2);
        public static readonly IBrush ArrowHead = Solid(220, 0xFF, 0xD3, 0x6B);
    }
}
