using System.Runtime.InteropServices.JavaScript;
using Lemon.Explore.Book.Infrastructures.Settings;

namespace Lemon.Explore.Book.Browser;

/// <summary>Reader preferences in the browser's <c>localStorage</c>, under a per-app prefix.</summary>
internal sealed partial class LocalStorageSettingsStore : ISettingsStore
{
    private const string Prefix = "explore-book:";

    public string? Get(string key)
    {
        try
        {
            return GetItem(Prefix + key);
        }
        catch (JSException)
        {
            // Storage can be blocked (private mode, site data disabled); the book then simply forgets.
            return null;
        }
    }

    public void Set(string key, string? value)
    {
        try
        {
            if (value is null)
            {
                RemoveItem(Prefix + key);
            }
            else
            {
                SetItem(Prefix + key, value);
            }
        }
        catch (JSException)
        {
            // See Get: a preference that cannot be stored still applies for this visit.
        }
    }

    [JSImport("globalThis.localStorage.getItem")]
    private static partial string? GetItem(string key);

    [JSImport("globalThis.localStorage.setItem")]
    private static partial void SetItem(string key, string value);

    [JSImport("globalThis.localStorage.removeItem")]
    private static partial void RemoveItem(string key);
}
