namespace Lemon.Explore.Book.Infrastructures.Settings;

/// <summary>
/// A few reader preferences (language, reading page), kept per device. Each head supplies its own store:
/// a JSON file on the desktop, <c>localStorage</c> in the browser.
/// </summary>
public interface ISettingsStore
{
    string? Get(string key);

    /// <summary>Saves a value; <see langword="null"/> removes it.</summary>
    void Set(string key, string? value);
}

/// <summary>Setting names shared by every store.</summary>
public static class SettingKeys
{
    public const string Culture = "culture";
    public const string PageIndex = "pageIndex";
    public const string AnimatesTurns = "animatesTurns";
    public const string Narration = "narration";
}
