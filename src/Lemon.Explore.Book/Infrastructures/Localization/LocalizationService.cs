using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace Lemon.Explore.Book.Infrastructures.Localization;

/// <summary>
/// Resource-backed implementation over <c>Resources/AppStrings.resx</c> and its satellite cultures.
/// </summary>
/// <remarks>
/// Ported from the Lemon desktop templates. Exposed as a singleton via <see cref="Instance"/> because XAML
/// markup needs an ambient binding source (see <see cref="LocalizeExtension"/>).
/// </remarks>
public sealed class LocalizationService : ILocalizationService
{
    private static readonly ResourceManager Resources =
        new("Lemon.Explore.Book.Resources.AppStrings", typeof(LocalizationService).Assembly);

    private static readonly CultureInfo[] Cultures =
    [
        CultureInfo.GetCultureInfo("en"),
        CultureInfo.GetCultureInfo("zh-CN"),
    ];

    private CultureInfo _currentCulture = Cultures[0];

    /// <summary>
    /// Property name that invalidates every indexer binding (<c>[Key]</c>) on this source. Avalonia listens for
    /// the indexer's own name, <c>"Item"</c>, not WPF's <c>"Item[]"</c>.
    /// </summary>
    internal const string IndexerPropertyName = "Item";

    public static LocalizationService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<CultureInfo> SupportedCultures => Cultures;

    public CultureInfo CurrentCulture => _currentCulture;

    public string this[string key] => GetString(key);

    public string GetString(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        // Missing keys surface as [Key] rather than an empty label, so a gap is visible instead of blank.
        return Resources.GetString(key, _currentCulture) ?? $"[{key}]";
    }

    public string Format(string key, params object?[] args) =>
        string.Format(_currentCulture, GetString(key), args);

    public void SetCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (_currentCulture.Equals(culture))
        {
            return;
        }

        _currentCulture = culture;

        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;

        // The indexer notification tells Avalonia every [Key] binding on this source is stale, which is what
        // makes the whole book re-read its strings without a restart.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(IndexerPropertyName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
    }

    /// <summary>
    /// Resolves a persisted culture name to a supported culture; with none saved, follows the system language.
    /// </summary>
    public CultureInfo ResolveSupportedCulture(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return Match(CultureInfo.CurrentUICulture);
        }

        try
        {
            return Match(CultureInfo.GetCultureInfo(cultureName));
        }
        catch (CultureNotFoundException)
        {
            return Cultures[0];
        }
    }

    private static CultureInfo Match(CultureInfo requested) =>
        Cultures.FirstOrDefault(c => c.Equals(requested))
        ?? Cultures.FirstOrDefault(c => c.TwoLetterISOLanguageName == requested.TwoLetterISOLanguageName)
        ?? Cultures[0];
}
