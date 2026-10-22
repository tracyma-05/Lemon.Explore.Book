using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.MoonPhases;

/// <summary>Drawing helpers shared by the moon-phases views.</summary>
internal static class MoonDrawing
{
    public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
    public static readonly Color DimText = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);
    public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    public static readonly Color MoonColor = Color.FromRgb(0xD9, 0xDC, 0xE3);

    public static readonly IBrush LitBrush = SpaceDrawing.RadialGradient((Color.FromRgb(0xF2, 0xF3, 0xF6), 0), (Color.FromRgb(0xB9, 0xBD, 0xC6), 1));
    public static readonly IBrush DarkBrush = Solid(255, 0x23, 0x27, 0x30);
    public static readonly IPen DarkOutline = MakePen(Solid(110, 255, 255, 255), 0.8);
    public static readonly IPen CurrentRing = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 1.6);

    public static string Loc(string key) => LocalizationService.Instance.GetString(key);

    public static string PhaseKey(MoonPhase phase) => $"MoonPhases_Phase_{phase}";

    /// <summary>
    /// The lit part of the Moon's disc seen from Earth, centred on <paramref name="center"/>, with the bright limb
    /// towards <paramref name="litDirection"/> (screen coordinates, y down). Empty at new moon.
    /// </summary>
    public static Geometry? LitShape(Point center, double radius, double elongation, Vector litDirection)
    {
        var e = SeasonsGeometry.ToRadians(SeasonsGeometry.Normalize360(elongation));
        var cos = Math.Cos(e);
        if (cos > 0.9995) return null;
        if (litDirection.Length < 1e-9) litDirection = new Vector(1, 0);
        litDirection /= litDirection.Length;

        // Built with the lit limb towards +x and turned into place: the limb is a half circle, the terminator half an
        // ellipse whose width is |cos E| of the radius, bulging towards the limb for a crescent and away for a gibbous.
        const int steps = 40;
        var points = new Point[(2 * steps) + 2];
        for (var i = 0; i <= steps; i++)
        {
            var y = -1 + (2.0 * i / steps);
            var x = Math.Sqrt(Math.Max(0, 1 - (y * y)));
            points[i] = new Point(x, y);
            points[(2 * steps) + 1 - i] = new Point(x * cos, y);
        }

        var normal = new Vector(-litDirection.Y, litDirection.X);
        for (var i = 0; i < points.Length; i++)
        {
            var p = points[i];
            points[i] = center + (((litDirection * p.X) + (normal * p.Y)) * radius);
        }

        return Polygon(points);
    }

    /// <summary>A small flat picture of the phase: the dark disc with its lit part over it.</summary>
    public static void DrawPhaseIcon(PaintContext dc, Point center, double radius, double elongation, Vector litDirection, IPen? ring = null)
    {
        dc.DrawEllipse(DarkBrush, DarkOutline, center, radius, radius);
        if (LitShape(center, radius, elongation, litDirection) is { } lit)
        {
            dc.DrawGeometry(LitBrush, null, lit);
        }

        if (ring is not null) dc.DrawEllipse(null, ring, center, radius + 3, radius + 3);
    }

    /// <summary>
    /// Which way the lit limb points for an observer looking at the Moon with north up: to the right while it waxes
    /// in the northern hemisphere, to the left while it wanes, and the other way round in the southern hemisphere.
    /// </summary>
    public static Vector LitSide(double elongation, bool southern)
    {
        var waxing = SeasonsGeometry.Normalize360(elongation) < 180;
        return new Vector(waxing ^ southern ? 1 : -1, 0);
    }

    /// <summary>Light for a renderer showing the Moon as seen from Earth, its lit limb towards <paramref name="litDirection"/> (screen, y down).</summary>
    public static LightVector DiscLight(double elongation, Vector litDirection)
    {
        var (sin, cos) = Math.SinCos(SeasonsGeometry.ToRadians(elongation));
        if (litDirection.Length < 1e-9) litDirection = new Vector(1, 0);
        litDirection /= litDirection.Length;
        var side = Math.Abs(sin);
        return LightVector.Normalize(litDirection.X * side, -litDirection.Y * side, -cos);
    }

    /// <summary>The Moon's renderer as seen from Earth; in the southern hemisphere it is upside down.</summary>
    public static PlanetRenderer DiscRenderer(bool southern)
    {
        // The Moon's axis is nearly upright: pole straight up (or down), leaning a little towards the viewer.
        var lean = 0.12;
        return new PlanetRenderer(Body("Moon"), LightVector.Normalize(0, southern ? -1 : 1, lean));
    }

    /// <summary>A renderer for a body seen from above its north pole, as in the orbit views.</summary>
    public static PlanetRenderer TopRenderer(string id) => new(Body(id), new LightVector(0, 0, 1));
}
