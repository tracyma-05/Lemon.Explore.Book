using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Timing;

namespace Lemon.Explore.Book.Chapters.SolarSystem;

/// <summary>A large, slowly rotating close-up of one body, lit from the upper left, for the detail panel.</summary>
public sealed class PlanetPreview : Control
{
    public static readonly StyledProperty<CelestialBody?> BodyProperty =
        AvaloniaProperty.Register<PlanetPreview, CelestialBody?>(nameof(Body));

    private static readonly LightVector Light = LightVector.Normalize(-0.62, 0.38, 0.69);

    private readonly BookStopwatch _clock = BookStopwatch.StartNew();
    private PlanetRenderer? _renderer;
    private double _appearedAt;
    private bool _attached;

    public CelestialBody? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BodyProperty)
        {
            var body = change.GetNewValue<CelestialBody?>();
            _renderer = body is null ? null : new PlanetRenderer(body);
            _appearedAt = _clock.Elapsed.TotalSeconds;
            InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        RequestFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
    }

    private void RequestFrame()
    {
        if (_attached)
        {
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(_ =>
            {
                if (IsEffectivelyVisible && _renderer is not null)
                {
                    InvalidateVisual();
                }

                RequestFrame();
            });
        }
    }

    public override void Render(DrawingContext context)
    {
        var body = Body;
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (_renderer is null || body is null || width < 1 || height < 1)
        {
            return;
        }

        var now = _clock.Elapsed.TotalSeconds;
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var center = new Point(width / 2, height / 2);

        // Ease in when a new body is shown: grow from 85% and fade in.
        var appear = Math.Clamp((now - _appearedAt) / 0.6, 0, 1);
        var ease = 1 - Math.Pow(1 - appear, 3);
        var fit = Math.Min(width, height) / 2 * 0.92;
        var radius = fit / _renderer.Extent * (0.85 + (0.15 * ease));
        if (body.IsStar)
        {
            radius *= 0.62;
        }

        using (context.PushOpacity(ease))
        {
            var glow = SpaceDrawing.RadialGradient(
                (Color.FromArgb(body.IsStar ? (byte)190 : (byte)70, body.AccentColor.R, body.AccentColor.G, body.AccentColor.B), 0.3),
                (Color.FromArgb(0, body.AccentColor.R, body.AccentColor.G, body.AccentColor.B), 1));
            var glowRadius = radius * (body.IsStar ? 1.6 : 1.45);
            context.DrawEllipse(glow, null, center, glowRadius, glowRadius);

            var size = _renderer.SizeFor(radius * scale);
            _renderer.Render(size, body.IsStar ? new LightVector(0, 0, 1) : Light, body.SpinAngle(now) * 0.6);
            var edge = size / scale;
            context.DrawImage(_renderer.Bitmap!, new Rect(center.X - (edge / 2), center.Y - (edge / 2), edge, edge));
        }
    }
}
