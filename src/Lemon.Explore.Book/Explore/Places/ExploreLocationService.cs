using System.Text.Json;
using Lemon.Explore.Book.Infrastructures.Settings;

namespace Lemon.Explore.Book.Explore.Places;

/// <summary>The saved observer location: a preset's id, or <see cref="ExplorePlaces.CustomId"/> with its own point.</summary>
public sealed record ExploreLocationSettings
{
    public string PlaceId { get; init; } = ExplorePlaces.DefaultId;

    public double CustomLatitude { get; init; } = ExplorePlaces.Default.Latitude;

    public double CustomLongitude { get; init; } = ExplorePlaces.Default.Longitude;
}

/// <summary>
/// The one observer location shared by the chapters that depend on where you are (day and night, tides, …). Chosen
/// from the location chip on those pages; a chapter follows <see cref="Changed"/>.
/// </summary>
public interface IExploreLocationService
{
    ExplorePlace Current { get; }

    /// <summary>The last custom point, kept while a preset is chosen so the picker can go back to it.</summary>
    (double Latitude, double Longitude) CustomPoint { get; }

    event EventHandler? Changed;

    /// <summary>Picks a preset by id; an unknown id is ignored.</summary>
    void Select(string id);

    void SelectCustom(double latitude, double longitude);
}

/// <summary>Kept as one JSON setting in the reader's settings store.</summary>
public sealed class ExploreLocationService : IExploreLocationService
{
    internal const string SettingKey = "explore.location";

    private readonly ISettingsStore _store;
    private ExploreLocationSettings _settings;

    public ExploreLocationService(ISettingsStore store)
    {
        _store = store;
        _settings = Load(store);
        Current = Resolve(_settings);
    }

    public ExplorePlace Current { get; private set; }

    public (double Latitude, double Longitude) CustomPoint => (_settings.CustomLatitude, _settings.CustomLongitude);

    public event EventHandler? Changed;

    public void Select(string id)
    {
        if (id == ExplorePlaces.CustomId)
        {
            SelectCustom(_settings.CustomLatitude, _settings.CustomLongitude);
            return;
        }

        if (ExplorePlaces.Find(id) is null || Current.Id == id)
        {
            return;
        }

        Apply(_settings with { PlaceId = id });
    }

    public void SelectCustom(double latitude, double longitude)
    {
        var place = ExplorePlaces.Custom(latitude, longitude);
        if (Current == place)
        {
            return;
        }

        Apply(_settings with { PlaceId = ExplorePlaces.CustomId, CustomLatitude = place.Latitude, CustomLongitude = place.Longitude });
    }

    private void Apply(ExploreLocationSettings settings)
    {
        _settings = settings;
        _store.Set(SettingKey, JsonSerializer.Serialize(settings, BookJson.Default.ExploreLocationSettings));
        Current = Resolve(settings);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static ExploreLocationSettings Load(ISettingsStore store)
    {
        try
        {
            return store.Get(SettingKey) is { } json
                ? JsonSerializer.Deserialize(json, BookJson.Default.ExploreLocationSettings) ?? new ExploreLocationSettings()
                : new ExploreLocationSettings();
        }
        catch (JsonException)
        {
            return new ExploreLocationSettings();
        }
    }

    private static ExplorePlace Resolve(ExploreLocationSettings settings) => settings.PlaceId == ExplorePlaces.CustomId
        ? ExplorePlaces.Custom(settings.CustomLatitude, settings.CustomLongitude)
        : ExplorePlaces.Find(settings.PlaceId) ?? ExplorePlaces.Default;
}
