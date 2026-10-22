using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.StarLife;

/// <summary>Colours and the glowing star shared by the life of a star views.</summary>
internal static class StarDrawing
{
    public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
    public static readonly Color DimText = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);
    public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    public static readonly Color Track = Color.FromRgb(0xFF, 0xD3, 0x6B);
    public static readonly Color Nebula = Color.FromRgb(0x6F, 0xE3, 0xC0);
    public static readonly Color Pink = Color.FromRgb(0xFF, 0x7E, 0xB6);
    public static readonly Color Blast = Color.FromRgb(0xFF, 0x9A, 0x5A);

    public static readonly IPen DotOutline = MakePen(Solid(255, 10, 14, 24), 1.2);
    public static readonly IPen Guide = MakePen(Solid(110, 255, 255, 255), 1, dashed: true);

    /// <summary>
    /// A star as a glowing disc: a halo that grows with its luminosity, a body brighter in the middle and darker at the
    /// limb, and for a swollen giant a slowly boiling outline.
    /// </summary>
    public static void DrawStar(PaintContext dc, Point center, double radius, double temperature, double luminosity, double now, bool boiling)
    {
        var color = StarModel.ColorOf(temperature);
        radius = Math.Max(radius, 1.5);

        var halo = radius * Math.Clamp(1.7 + (0.22 * Math.Log10(Math.Max(luminosity, 1e-4) + 1)), 1.5, 3.2);
        var haloBrush = SpaceDrawing.RadialGradient(
            (WithAlpha(Lerp(color, Colors.White, 0.3), 0.55), 0),
            (WithAlpha(color, 0.22), 0.45),
            (WithAlpha(color, 0), 1));
        dc.DrawEllipse(haloBrush, null, center, halo, halo);

        var body = new ImmutableRadialGradientBrush(
        [
            new ImmutableGradientStop(0, Lerp(color, Colors.White, 0.5)),
            new ImmutableGradientStop(0.55, Lerp(color, Colors.White, 0.12)),
            new ImmutableGradientStop(0.85, color),
            new ImmutableGradientStop(1, Lerp(color, Color.FromRgb(40, 10, 0), 0.35)),
        ],
        gradientOrigin: new RelativePoint(0.45, 0.42, RelativeUnit.Relative),
        radiusX: new RelativeScalar(0.5, RelativeUnit.Relative),
        radiusY: new RelativeScalar(0.5, RelativeUnit.Relative));
        if (!boiling)
        {
            dc.DrawEllipse(body, null, center, radius, radius);
            return;
        }

        // Convection cells as big as the star itself make a giant's edge bulge and churn.
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            const int steps = 120;
            for (var i = 0; i < steps; i++)
            {
                var a = 2 * Math.PI * i / steps;
                var r = radius * (1 + (0.006 * Math.Sin((5 * a) + (now * 0.35))) + (0.004 * Math.Sin((9 * a) - (now * 0.55))) + (0.0025 * Math.Sin((13 * a) + now)));
                var point = new Point(center.X + (r * Math.Cos(a)), center.Y + (r * Math.Sin(a)));
                if (i == 0) ctx.BeginFigure(point, true);
                else ctx.LineTo(point);
            }

            ctx.EndFigure(true);
        }

        // The churning rim as a flat colour, the round body with its limb darkening on top.
        var rim = new SolidColorBrush(Lerp(color, Color.FromRgb(40, 10, 0), 0.3));
        dc.DrawGeometry(rim, null, geometry);
        dc.DrawEllipse(body, null, center, radius * 0.985, radius * 0.985);

        // A few darker cells drifting across the face.
        for (var k = 0; k < 6; k++)
        {
            var a = (k * 1.9) + (now * 0.04 * (k % 2 == 0 ? 1 : -1));
            var d = radius * (0.2 + (0.18 * (k % 3)));
            var cell = new Point(center.X + (Math.Cos(a) * d), center.Y + (Math.Sin(a) * d * 0.9));
            var size = radius * (0.22 + (0.03 * Math.Sin(now * 0.3 + k)));
            dc.DrawEllipse(CellBrush, null, cell, size, size * 0.85);
        }
    }

    private static readonly IBrush CellBrush = SpaceDrawing.RadialGradient((Color.FromArgb(24, 60, 10, 0), 0), (Color.FromArgb(0, 60, 10, 0), 1));
}

/// <summary>Numbers as the life of a star page writes them, shared by the views and the view model.</summary>
public static class StarFormat
{
    /// <summary>
    /// A length of time in the units the language uses for big numbers: billions and millions in English, 亿 and 万 in
    /// Chinese (each key's <c>_Unit</c> says how many years it is); days below a year; "for ever" for a remnant.
    /// </summary>
    public static string Years(double years)
    {
        var loc = LocalizationService.Instance;
        if (double.IsInfinity(years)) return loc.GetString("Star_Forever");
        if (years < 1) return loc.Format("Star_Days", Math.Max(1, Math.Round(years * 365.25)));

        foreach (var key in new[] { "Star_Years_Huge", "Star_Years_Big", "Star_Years_Mid" })
        {
            var unit = Unit(key);
            if (years >= unit) return loc.Format(key, Round(years / unit));
        }

        return loc.Format("Star_Years", years >= 1000 ? Significant(years, 2) : Math.Round(years));
    }

    /// <summary>Times the Sun's: 1,200, 25, 0.86, 0.0017.</summary>
    public static string Times(double value)
    {
        var culture = LocalizationService.Instance.CurrentCulture;
        return value switch
        {
            >= 1000 => Significant(value, 3).ToString("N0", culture),
            >= 100 => Math.Round(value).ToString("N0", culture),
            >= 10 => value.ToString("0.#", culture),
            >= 0.01 => value.ToString("0.##", culture),
            _ => value.ToString("0.#####", culture),
        };
    }

    /// <summary>A temperature in kelvin, rounded to a sensible step.</summary>
    public static string Kelvin(double kelvin) =>
        RoundTo(kelvin, kelvin >= 1e5 ? 10_000 : kelvin >= 1e4 ? 100 : 10).ToString("N0", LocalizationService.Instance.CurrentCulture);

    /// <summary>A distance in kilometres as a number, to the nearest hundred above a thousand: 24, 8,700.</summary>
    public static string Km(double km) =>
        RoundTo(km, km >= 1000 ? 100 : 1).ToString("N0", LocalizationService.Instance.CurrentCulture);

    private static double RoundTo(double value, double step) => Math.Round(value / step) * step;

    private static double Significant(double value, int digits) =>
        RoundTo(value, Math.Pow(10, Math.Floor(Math.Log10(value)) - digits + 1));

    /// <summary>Two or three significant figures, e.g. 4.6, 46, 120.</summary>
    private static double Round(double value) => value >= 100 ? Math.Round(value) : value >= 10 ? Math.Round(value, 1) : Math.Round(value, 2);

    private static double Unit(string key) =>
        double.TryParse(LocalizationService.Instance.GetString(key + "_Unit"), NumberStyles.Float, CultureInfo.InvariantCulture, out var unit) && unit > 0
            ? unit
            : 1e9;
}
