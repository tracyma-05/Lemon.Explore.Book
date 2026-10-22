using System.Globalization;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.WaterCycle;

/// <summary>
/// Volumes and times of the water-cycle page in the reader's language: big volumes in millions (English) or 亿
/// (Chinese), times from minutes to thousands of years. The unit sizes come from the strings, so each language groups
/// digits its own way.
/// </summary>
public static class WaterFormat
{
    /// <summary>"1,338 million km³", "176 thousand km³" or "12,900 km³" (or 亿 / 万 in Chinese).</summary>
    public static string Volume(double cubicKilometres)
    {
        var big = Unit("Water_Unit_Big", 1e6);
        var mid = Unit("Water_Unit_Mid", 1e3);
        if (cubicKilometres >= big) return Format("Water_Volume_Big", Short(cubicKilometres / big));
        if (cubicKilometres >= mid * 10) return Format("Water_Volume_Mid", Short(cubicKilometres / mid));
        return Format("Water_Volume_Small", Number(cubicKilometres, "N0"));
    }

    /// <summary>A volume each year.</summary>
    public static string Flux(double cubicKilometresPerYear) => Format("Water_Flux", Volume(cubicKilometresPerYear));

    /// <summary>A stay or a journey: minutes, hours, days, months or years.</summary>
    public static string Duration(double days)
    {
        if (days < 1.0 / 24) return Format("Water_Time_Minutes", Number(Math.Max(1, days * 24 * 60), "0"));
        if (days < 2) return Format("Water_Time_Hours", Number(days * 24, "0"));
        if (days < 60) return Format("Water_Time_Days", Number(days, days < 15 ? "0.#" : "0"));
        if (days < 730) return Format("Water_Time_Months", Number(days / 30.44, "0"));
        var years = days / 365.25;
        return Format("Water_Time_Years", Number(years, "N0"));
    }

    /// <summary>Three significant figures, grouped: 1,338 / 24.1 / 1.39.</summary>
    public static string Short(double value) => value >= 100 ? Number(value, "N0") : value >= 10 ? Number(value, "0.#") : Number(value, "0.##");

    public static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static double Unit(string key, double fallback) =>
        double.TryParse(LocalizationService.Instance.GetString(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var unit) && unit > 0 ? unit : fallback;

    private static string Format(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);
}
