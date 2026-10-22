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
/// How long each stage of the star's life really lasts, on a logarithmic scale from a month to a trillion years:
/// almost all of it on the main sequence, the supernova over in months. The stage the star is in is lit.
/// </summary>
public sealed class DurationChart : Control
{
    static DurationChart()
    {
        AffectsRender<DurationChart>(MassProperty, StageIndexProperty);
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
        AvaloniaProperty.Register<DurationChart, double>(nameof(Mass), 1.0);

    public static readonly StyledProperty<int> StageIndexProperty =
        AvaloniaProperty.Register<DurationChart, int>(nameof(StageIndex), 0);

    public double Mass
    {
        get => GetValue(MassProperty);
        set => SetValue(MassProperty, value);
    }

    /// <summary>The stage the star is in, by its place in the track.</summary>
    public int StageIndex
    {
        get => GetValue(StageIndexProperty);
        set => SetValue(StageIndexProperty, value);
    }

    private const double MinLog = -1;
    private const double MaxLog = 13;

    private readonly LabelCache _labels;

    public DurationChart()
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
        if (width < 200 || height < 100) return;

        var track = StarModel.Track(Mass);
        var names = track.Select(s => _labels.Get(StarDiagram.StageKey(s.Stage), 10.5, StarDrawing.DimText)).ToList();
        var left = Math.Min(width * 0.4, names.Max(n => n.Width) + 16);
        var right = width - 12;
        var top = 10.0;
        var bottom = height - 24;
        var row = (bottom - top) / track.Count;
        double X(double years) => left + ((right - left) * (Math.Log10(years) - MinLog) / (MaxLog - MinLog));

        foreach (var (text, years) in new[] { ("1", 1.0), ("10³", 1e3), ("10⁶", 1e6), ("10⁹", 1e9), ("10¹²", 1e12) })
        {
            var x = X(years);
            dc.DrawLine(Paint.Grid, new Point(x, top), new Point(x, bottom));
            var label = _labels.Text(text, 9.5, StarDrawing.DimText);
            dc.DrawText(label, new Point(x - (label.Width / 2), bottom + 4));
        }

        var unit = _labels.Get("Star_Duration_Axis", 9.5, StarDrawing.DimText);
        dc.DrawText(unit, new Point(left - unit.Width - 8, bottom + 4));

        for (var i = 0; i < track.Count; i++)
        {
            var span = track[i];
            var y = top + (row * i);
            var current = i == StageIndex;
            var name = current ? _labels.Get(StarDiagram.StageKey(span.Stage), 10.5, StarDrawing.Text, true) : names[i];
            dc.DrawText(name, new Point(left - name.Width - 8, y + ((row - name.Height) / 2)));

            var color = StarDiagram.StageColor(span, Mass);
            var forever = double.IsInfinity(span.Years);
            var end = forever ? right : X(Math.Max(span.Years, 0.1));
            var bar = new Rect(left, y + (row * 0.22), Math.Max(2, end - left), row * 0.56);
            dc.DrawRoundedRectangle(new SolidColorBrush(WithAlpha(color, current ? 0.85 : 0.45)), current ? Paint.Current : null, bar, 3, 3);

            var text = _labels.Text(StarFormat.Years(span.Years), 9.5, current ? StarDrawing.Text : StarDrawing.DimText);
            var textX = forever || bar.Right + text.Width + 6 > width ? bar.Right - text.Width - 6 : bar.Right + 6;
            dc.DrawText(text, new Point(textX, y + ((row - text.Height) / 2)));
        }
    }

    private static class Paint
    {
        public static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
        public static readonly IPen Current = MakePen(Solid(230, 255, 255, 255), 1.2);
    }
}
