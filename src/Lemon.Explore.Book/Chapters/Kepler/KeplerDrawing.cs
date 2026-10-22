using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Kepler;

/// <summary>Colours and pens shared by the Kepler's laws views.</summary>
internal static class KeplerDrawing
{
    public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
    public static readonly Color DimText = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);
    public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    public static readonly Color Blue = Color.FromRgb(0x5A, 0xA9, 0xFF);
    public static readonly Color Cyan = Color.FromRgb(0x7F, 0xE0, 0xFF);
    public static readonly Color Orbit = Color.FromRgb(0xB8, 0xC7, 0xFF);
    public static readonly Color Fast = Color.FromRgb(0xFF, 0x8A, 0x65);
    public static readonly Color Slow = Color.FromRgb(0x6F, 0xC3, 0xFF);
    public static readonly Color You = Color.FromRgb(0x6F, 0xE3, 0xC0);

    /// <summary>The two colours the swept sectors alternate between.</summary>
    public static readonly Color SectorA = Color.FromRgb(0x5A, 0xA9, 0xFF);

    public static readonly Color SectorB = Color.FromRgb(0xFF, 0xD3, 0x6B);

    public static readonly IPen OrbitPen = MakePen(SpaceDrawing.Solid(WithAlpha(Orbit, 0.75)), 1.6);
    public static readonly IPen FaintOrbitPen = MakePen(SpaceDrawing.Solid(WithAlpha(Orbit, 0.32)), 1.1);
    public static readonly IPen AxisPen = MakePen(Solid(90, 255, 255, 255), 1, dashed: true);
    public static readonly IPen CirclePen = MakePen(Solid(110, 0x6F, 0xE3, 0xC0), 1.2, dashed: true);
    public static readonly IPen GoldPen = MakePen(SpaceDrawing.Solid(Gold), 2);
    public static readonly IPen CyanPen = MakePen(SpaceDrawing.Solid(Cyan), 2);
    public static readonly IPen FastPen = MakePen(SpaceDrawing.Solid(Fast), 2.2);
    public static readonly IBrush FastBrush = SpaceDrawing.Solid(Fast);
    public static readonly IBrush GoldBrush = SpaceDrawing.Solid(Gold);
    public static readonly IBrush CyanBrush = SpaceDrawing.Solid(Cyan);
    public static readonly IBrush YouBrush = SpaceDrawing.Solid(You);
    public static readonly IPen YouPen = MakePen(SpaceDrawing.Solid(WithAlpha(You, 0.85)), 1.6);
    public static readonly IPen DotOutline = MakePen(Solid(255, 10, 14, 24), 1.2);

    public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(200, 255, 236, 190), 0.2),
            (Color.FromArgb(70, 255, 200, 120), 0.45),
            (Color.FromArgb(0, 255, 170, 80), 1));
}

/// <summary>Numbers as the Kepler's laws page writes them, shared by the views and the view model.</summary>
public static class KeplerFormat
{
    /// <summary>A distance in AU with as many decimals as reads well: 0.387, 1.52, 39.5.</summary>
    public static string Au(double au) => au.ToString(au < 1 ? "0.###" : au < 10 ? "0.##" : "0.#", LocalizationService.Instance.CurrentCulture);

    /// <summary>A number of Earth years: 0.241, 1.88, 11.9, 248.</summary>
    public static string Years(double years) => years.ToString(years < 1 ? "0.###" : years < 10 ? "0.##" : years < 100 ? "0.#" : "0", LocalizationService.Instance.CurrentCulture);

    /// <summary>A length of time in days below two years, else in years, e.g. "88 days" / "11.9 years".</summary>
    public static string Duration(double years) => Duration(years, years);

    /// <summary>
    /// <paramref name="years"/> in the unit <see cref="Duration(double)"/> would pick for <paramref name="unitOf"/>, so
    /// a time and the period it is part of read alike: "1.4 / 75.3 years", not "511 days / 75.3 years".
    /// </summary>
    public static string Duration(double years, double unitOf) => unitOf < 2
        ? LocalizationService.Instance.Format("Kepler_Days", Math.Round(years * KeplerGeometry.DaysPerYear))
        : LocalizationService.Instance.Format("Kepler_Years", Years(years));
}
