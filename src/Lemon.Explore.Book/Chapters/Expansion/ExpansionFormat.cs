using System.Globalization;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Expansion;

/// <summary>
/// Numbers of the expanding-universe page in the viewer's language: light-years and years in millions and billions
/// (万 and 亿 in Chinese; each <c>_Mid</c> / <c>_Big</c> key's <c>_Unit</c> says how many it is), seconds to days for
/// the first moments, speeds, and temperatures as powers of ten when they are huge.
/// </summary>
internal static class ExpansionFormat
{
    /// <summary>A distance given in megaparsecs, in light-years.</summary>
    public static string Distance(double mpc) => Scaled(mpc * Universe.GlyPerMpc * 1e9, "Expand_Ly");

    /// <summary>A distance given in billions of light-years.</summary>
    public static string DistanceGly(double gly) => Scaled(gly * 1e9, "Expand_Ly");

    /// <summary>A span of time given in billions of years; below a year in days, hours, minutes or seconds.</summary>
    public static string Time(double gyr)
    {
        var years = gyr * 1e9;
        if (years >= 1) return Scaled(years, "Expand_Yr");
        var seconds = gyr * Universe.SecondsPerGyr;
        if (seconds >= 86_400) return Format("Expand_Day", Short(seconds / 86_400));
        if (seconds >= 3_600) return Format("Expand_Hour", Short(seconds / 3_600));
        if (seconds >= 60) return Format("Expand_Min", Short(seconds / 60));
        return Format("Expand_Sec", seconds >= 0.01 ? Short(seconds) : Power(seconds));
    }

    /// <summary>A speed in km/s.</summary>
    public static string Speed(double kms) => Format("Expand_Kms", Grouped(Math.Abs(kms) >= 1000 ? Sig(kms, 3) : Math.Round(kms)));

    /// <summary>A temperature in kelvin; a power of ten from a million up.</summary>
    public static string Kelvin(double kelvin) => Format("Expand_K", kelvin >= 1e6 ? Power(kelvin) : kelvin >= 100 ? Grouped(Math.Round(kelvin)) : Short(kelvin));

    /// <summary>A size compared with today's: "1/1100" when smaller, "2.4×" when bigger.</summary>
    public static string Size(double a) => a >= 0.1
        ? Format("Expand_Size_Bigger", a < 1 ? Sig(a, 3).ToString("0.###", CultureInfo.InvariantCulture) : Short(a))
        : Format("Expand_Size_Smaller", a > 0 ? Short(1 / a) : "∞");

    /// <summary>A value rounded for reading: three significant digits at most, no separators below 10 000, a power of ten from ten million.</summary>
    public static string Short(double value)
    {
        var v = Math.Abs(value);
        if (v >= 1e7) return Power(v);
        if (v >= 10_000) return Grouped(Sig(value, 3));
        if (v >= 100) return Sig(value, 3).ToString("0", CultureInfo.InvariantCulture);
        if (v >= 10) return value.ToString("0.#", CultureInfo.InvariantCulture);
        if (v >= 1) return value.ToString("0.##", CultureInfo.InvariantCulture);
        return Sig(value, 2).ToString("0.######", CultureInfo.InvariantCulture);
    }

    /// <summary>A number rounded to a number of significant digits.</summary>
    public static double Sig(double value, int digits)
    {
        if (value == 0 || double.IsNaN(value) || double.IsInfinity(value)) return value;
        var scale = Math.Pow(10, digits - 1 - (int)Math.Floor(Math.Log10(Math.Abs(value))));
        return Math.Round(value * scale) / scale;
    }

    /// <summary>A whole number with thousands separators from 10 000 up.</summary>
    public static string Grouped(double value) => Math.Abs(value) >= 10_000
        ? value.ToString("#,0", CultureInfo.InvariantCulture)
        : value.ToString("0", CultureInfo.InvariantCulture);

    /// <summary>"2.7 × 10⁹" (or "10⁻⁴" when the leading digit is 1).</summary>
    public static string Power(double value)
    {
        if (value <= 0) return "0";
        var exponent = (int)Math.Floor(Math.Log10(value));
        var mantissa = Math.Round(value / Math.Pow(10, exponent), 1);
        if (mantissa >= 10)
        {
            mantissa /= 10;
            exponent++;
        }

        var power = "10" + Superscript(exponent);
        return Math.Abs(mantissa - 1) < 0.05 ? power : mantissa.ToString("0.#", CultureInfo.InvariantCulture) + " × " + power;
    }

    private static string Superscript(int n)
    {
        const string digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var text = Math.Abs(n).ToString(CultureInfo.InvariantCulture);
        var chars = text.Select(c => digits[c - '0']);
        return (n < 0 ? "⁻" : string.Empty) + string.Concat(chars);
    }

    /// <summary>A count of years or light-years: plain, or in the language's middle or big unit when it reaches it.</summary>
    private static string Scaled(double value, string prefix)
    {
        var big = Unit(prefix + "_Big");
        if (Math.Abs(value) >= big) return Format(prefix + "_Big", Short(value / big));
        var mid = Unit(prefix + "_Mid");
        if (Math.Abs(value) >= mid) return Format(prefix + "_Mid", Short(value / mid));
        return Format(prefix + "_Plain", Grouped(Sig(value, 2)));
    }

    private static double Unit(string key) =>
        double.TryParse(LocalizationService.Instance.GetString(key + "_Unit"), NumberStyles.Float, CultureInfo.InvariantCulture, out var unit) && unit > 0
            ? unit
            : 1e9;

    private static string Format(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);
}
