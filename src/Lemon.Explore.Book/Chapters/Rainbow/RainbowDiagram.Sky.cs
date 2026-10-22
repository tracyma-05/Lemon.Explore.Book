using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Rainbow;

public sealed partial class RainbowDiagram
{
    private readonly BowSkyRenderer _sky = new();

    /// <summary>Falling rain: where across (0–1), where in its fall (0–1), how fast, how long and how bright.</summary>
    private static readonly (double X, double Phase, double Speed, double Length, double Alpha)[] Streaks = MakeStreaks();

    private static (double, double, double, double, double)[] MakeStreaks()
    {
        var random = new Random(42);
        return Enumerable.Range(0, 240)
            .Select(_ => (random.NextDouble(), random.NextDouble(), 0.7 + (0.6 * random.NextDouble()), 10 + (16 * random.NextDouble()),
                0.06 + (0.14 * random.NextDouble())))
            .ToArray();
    }

    /// <summary>The sky view: the horizon low in the stage, wide enough for both bows with the Sun on the horizon.</summary>
    private SkyView SkyLayout(Rect stage)
    {
        var horizon = stage.Bottom - (stage.Height * 0.17);
        var scale = Math.Max(2, Math.Min((ActualWidth - 40) / 126, (horizon - stage.Top + 40) / 57));
        return new SkyView(scale, horizon, ActualWidth / 2);
    }

    private void DrawSky(PaintContext dc, Rect stage, double width, double height)
    {
        var view = SkyLayout(stage);
        var sun = Math.Clamp(SunAltitude, 0, 60);
        var image = _sky.Render(width, height, view, sun, DropSize, ShowSecondary);
        SpaceDrawing.DrawSmooth(dc, image, new Rect(0, 0, Math.Ceiling(width / BowSkyRenderer.Pixel) * BowSkyRenderer.Pixel,
            Math.Ceiling(height / BowSkyRenderer.Pixel) * BowSkyRenderer.Pixel));

        // Your shadow on the ground: its head is the point opposite the Sun.
        var antisolar = view.Project(-sun, 0);
        if (sun > BowSkyRenderer.HillAltitude(0) + 0.5) DrawShadow(dc, antisolar, view.Scale);

        DrawRain(dc, width, height);

        if (ShowGuides) DrawSkyGuides(dc, stage, view, sun, antisolar);

        var where = LocFormat("Rain_Diagram_SunBehind", Number(sun, "0"));
        var text = _labels.Text(where, 12, Ink.Gold);
        _labels.DrawTextPill(dc, where, new Point(stage.Right - text.Width - 16, stage.Top + 4), 12, Ink.Gold);
    }

    /// <summary>The head and shoulders of your shadow, its head on the point opposite the Sun.</summary>
    private static void DrawShadow(PaintContext dc, Point head, double scale)
    {
        var size = Math.Clamp(scale * 1.6, 7, 16);
        dc.DrawEllipse(ShadowBrush, null, head, size * 0.62, size * 0.72);
        dc.DrawEllipse(ShadowBrush, null, head + new Vector(0, size * 1.6), size * 1.5, size * 0.9);
    }

    private void DrawRain(PaintContext dc, double width, double height)
    {
        foreach (var (x, phase, speed, length, _) in Streaks)
        {
            var y = (((phase + (_time * speed * 0.55)) % 1) * (height + 80)) - 40;
            var top = new Point((x * (width + 60)) - 30 + (y * 0.1), y);
            dc.DrawLine(RainPen, top, top + new Vector(length * 0.1, length));
        }
    }

    /// <summary>The point opposite the Sun, the circles the bows lie on, and what to see where.</summary>
    private void DrawSkyGuides(PaintContext dc, Rect stage, SkyView view, double sun, Point antisolar)
    {
        var bounds = new Rect(0, stage.Top, ActualWidth, stage.Height);
        var n = RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.YellowNm);
        var primary = RainbowOptics.RainbowAngle(1, n);
        var secondary = RainbowOptics.RainbowAngle(2, n);

        DrawCircle(dc, view, primary, sun, GuidePrimary);
        if (ShowSecondary) DrawCircle(dc, view, secondary, sun, GuideSecondary);

        // The point opposite the Sun, with a cross, and a radius out to each bow.
        if (antisolar.Y < ActualHeight - 4)
        {
            dc.DrawLine(GuideCross, antisolar + new Vector(-9, 0), antisolar + new Vector(9, 0));
            dc.DrawLine(GuideCross, antisolar + new Vector(0, -9), antisolar + new Vector(0, 9));
            DrawPill(dc, bounds, Loc("Rain_Diagram_Antisolar"), antisolar + new Vector(0, 26), Ink.Text, 11, "antisolar");
            _targets.Add("antisolar", antisolar, 12);
        }

        foreach (var (radius, around, color, show) in new[] { (primary, 62.0, Ink.Gold, true), (secondary, 50.0, Ink.Violet, ShowSecondary) })
        {
            if (!show) continue;
            var (alt, az) = SkyView.OnCircle(radius, around, sun);
            var end = view.Project(alt, az);
            dc.DrawLine(MakePen(new SolidColorBrush(WithAlpha(color, 0.75)), 1.2, dashed: true), antisolar, end);
            var (midAlt, midAz) = SkyView.OnCircle(radius * 0.72, around, sun);
            DrawPill(dc, bounds, $"{Number(radius, "0")}°", view.Project(midAlt, midAz), color, 11.5, "bowangle");
        }

        // Labels at the top of each bow, or where it meets the horizon if its top is down.
        LabelOnCircle(dc, bounds, view, primary - 4, sun, Loc("Rain_Diagram_PrimaryBow"), Ink.Gold, "primary");
        if (ShowSecondary)
        {
            LabelOnCircle(dc, bounds, view, secondary + 3.2, sun, Loc("Rain_Diagram_SecondaryBow"), Ink.Violet, "secondary");
            LabelOnCircle(dc, bounds, view, (primary + secondary) / 2, sun, Loc("Rain_Diagram_DarkBand"), Ink.Dim, "darkband", -1);
        }

        LabelOnCircle(dc, bounds, view, primary - 16, sun, Loc("Rain_Diagram_BrightInside"), Ink.Text, "brightinside");
    }

    /// <summary>A circle round the point opposite the Sun: solid above the land, dashed below it.</summary>
    private static void DrawCircle(PaintContext dc, SkyView view, double radius, double sun, IPen pen)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var around = 0; around <= 360; around += 3)
            {
                var (alt, az) = SkyView.OnCircle(radius, around, sun);
                var point = view.Project(alt, az);
                if (around == 0) context.BeginFigure(point, false);
                else context.LineTo(point);
            }

            context.EndFigure(false);
        }
        dc.DrawGeometry(null, pen, geometry);
    }

    /// <summary>
    /// A label on a circle round the point opposite the Sun: at its top if that is in the sky, else on the side
    /// (<paramref name="side"/> +1 right, −1 left) just above the hills; a target named <paramref name="id"/>.
    /// </summary>
    private void LabelOnCircle(PaintContext dc, Rect bounds, SkyView view, double radius, double sun, string text, Color color, string id,
        int side = 1)
    {
        var top = RainbowOptics.TopAltitude(radius, sun);
        double around;
        if (top > 6)
        {
            around = 90;
        }
        else
        {
            // Find where the circle is 4° up on the chosen side.
            around = double.NaN;
            for (var a = 90.0; a >= -90; a -= 1)
            {
                var (alt, _) = SkyView.OnCircle(radius, side > 0 ? a : 180 - a, sun);
                if (alt <= 4)
                {
                    around = side > 0 ? a : 180 - a;
                    break;
                }
            }

            if (double.IsNaN(around) || top < 3) return;
        }

        var (altitude, azimuth) = SkyView.OnCircle(radius, around, sun);
        DrawPill(dc, bounds, text, view.Project(altitude, azimuth), color, 11, id);
    }

    private static readonly IBrush ShadowBrush = Solid(120, 0, 0, 0);
    private static readonly IPen RainPen = MakePen(Solid(46, 0xDD, 0xE6, 0xF0), 1);
    private static readonly IPen GuidePrimary = MakePen(Solid(120, 0xFF, 0xD3, 0x6B), 1, dashed: true);
    private static readonly IPen GuideSecondary = MakePen(Solid(110, 0xB3, 0x9D, 0xFF), 1, dashed: true);
    private static readonly IPen GuideCross = MakePen(Solid(220, 0xEE, 0xF1, 0xF6), 1.6);
}
