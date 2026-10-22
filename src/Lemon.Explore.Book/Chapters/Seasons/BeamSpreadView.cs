using Avalonia;
using Avalonia.Media;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Seasons;

/// <summary>
/// One beam of sunlight reaching the ground at the noon altitude: the lower the Sun, the longer the patch it lights
/// and the dimmer each part of it. A dashed bracket shows the patch the same beam lights with the Sun overhead.
/// </summary>
public sealed class BeamSpreadView : SeasonFigure
{
    public static readonly StyledProperty<double> AltitudeProperty =
        AvaloniaProperty.Register<BeamSpreadView, double>(nameof(Altitude), 45);

    static BeamSpreadView()
    {
        AffectsRender<BeamSpreadView>(AltitudeProperty);
    }

    /// <summary>The Sun's altitude in degrees; at or below 0 no light reaches the ground.</summary>
    public double Altitude
    {
        get => GetValue(AltitudeProperty);
        set => SetValue(AltitudeProperty, value);
    }

    public override void Render(DrawingContext dc)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width < 1 || height < 1)
        {
            return;
        }

        const double beam = 30;
        var ground = height - 24;
        var hit = width * 0.62;
        dc.FillRectangle(Paint.Ground, new Rect(0, ground, width, height - ground));
        dc.DrawLine(Paint.GroundLine, new Point(0, ground), new Point(width, ground));

        // With the Sun overhead the beam would light a patch exactly as wide as itself.
        var overheadLeft = hit - (beam / 2);
        var overheadRight = hit + (beam / 2);
        var bracketY = ground + 12;
        dc.DrawLine(Paint.Bracket, new Point(overheadLeft, bracketY), new Point(overheadRight, bracketY));
        dc.DrawLine(Paint.Bracket, new Point(overheadLeft, bracketY - 4), new Point(overheadLeft, bracketY + 4));
        dc.DrawLine(Paint.Bracket, new Point(overheadRight, bracketY - 4), new Point(overheadRight, bracketY + 4));
        var overhead = Labels.Get("Seasons_Label_Overhead", 10, Paint.DimText);
        dc.DrawText(overhead, new Point(overheadRight + 6, bracketY - (overhead.Height / 2)));

        var altitude = Altitude;
        if (altitude <= 0.2)
        {
            var below = Labels.Get("Seasons_Label_NoSunlight", 11, Paint.DimText);
            dc.DrawText(below, new Point((width - below.Width) / 2, (ground - below.Height) / 2));
            return;
        }

        // The beam comes down from the upper left at the Sun's altitude; its footprint is beam / sin(altitude) long.
        var radians = SeasonsGeometry.ToRadians(altitude);
        var direction = new Vector(Math.Cos(radians), Math.Sin(radians));
        var footprint = Math.Min(beam / Math.Sin(radians), width * 3);
        var left = new Point(hit - (footprint / 2), ground);
        var right = new Point(hit + (footprint / 2), ground);
        var back = direction * ((ground + 10) / direction.Y);
        var intensity = Math.Sin(radians);

        using (dc.PushOpacity(0.35 + (0.65 * intensity)))
        {
            dc.DrawGeometry(Paint.Beam, null, Polygon(left - back, left, right, right - back));
        }

        dc.DrawLine(Paint.BeamEdge, left - back, left);
        dc.DrawLine(Paint.BeamEdge, right - back, right);

        // The lit patch: the same light, spread thinner the longer it is.
        using (dc.PushOpacity(0.25 + (0.75 * intensity)))
        {
            dc.FillRectangle(Paint.Patch, new Rect(left.X, ground - 2, right.X - left.X, 5));
        }

        var text = Labels.Text($"{altitude:0}°", 11, Paint.Gold, true);
        dc.DrawText(text, new Point(Math.Max(4, left.X - text.Width - 6), ground - text.Height - 3));
    }

    private static class Paint
    {
        public static readonly Color DimText = Color.FromArgb(0xD0, 0xC9, 0xD1, 0xDC);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);

        public static readonly IBrush Ground = LinearGradient(Color.FromRgb(0x3C, 0x52, 0x2C), Color.FromRgb(0x1C, 0x26, 0x15), 90);
        public static readonly IPen GroundLine = MakePen(Solid(200, 0xC9, 0xE7, 0xA8), 1.2);
        public static readonly IPen Bracket = MakePen(Solid(200, 255, 255, 255), 1, dashed: true);
        public static readonly IBrush Beam = LinearGradient(Color.FromArgb(40, 255, 226, 150), Color.FromArgb(150, 255, 214, 120), 45);
        public static readonly IPen BeamEdge = MakePen(Solid(150, 0xFF, 0xD3, 0x6B), 1);
        public static readonly IBrush Patch = Solid(255, 0xFF, 0xD3, 0x6B);
    }
}
