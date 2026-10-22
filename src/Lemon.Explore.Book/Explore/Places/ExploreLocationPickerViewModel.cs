using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Infrastructures.Localization;

namespace Lemon.Explore.Book.Explore.Places;

/// <summary>A preset in the location picker; checking it picks the place.</summary>
public sealed partial class ExplorePlaceOption(ExplorePlace place, IExploreLocationService service) : ObservableObject
{
    public string Id => place.Id;

    public string Name => place.Name;

    public string CoordinatesText => place.CoordinatesText;

    public bool IsSelected
    {
        get => service.Current.Id == place.Id;
        set
        {
            if (value) service.Select(place.Id);
        }
    }

    internal void Refresh()
    {
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(CoordinatesText));
    }
}

/// <summary>
/// The observer-location picker behind the location chip: the presets, a custom point, and a summary of the chosen
/// place. Each chip makes one over the shared <see cref="IExploreLocationService"/> and disposes it.
/// </summary>
public sealed partial class ExploreLocationPickerViewModel : ObservableObject, IDisposable
{
    private readonly IExploreLocationService _service;

    public ExploreLocationPickerViewModel(IExploreLocationService service)
    {
        _service = service;
        Places = ExplorePlaces.All.Select(p => new ExplorePlaceOption(p, service)).ToList();
        (_customLatitude, _customLongitude) = service.CustomPoint;
        service.Changed += OnLocationChanged;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    public IReadOnlyList<ExplorePlaceOption> Places { get; }

    public ExplorePlace Current => _service.Current;

    /// <summary>The chosen preset for a list box, or null while a custom point is used.</summary>
    public ExplorePlaceOption? SelectedPlace
    {
        get => Places.FirstOrDefault(p => p.IsSelected);
        set
        {
            if (value is not null) _service.Select(value.Id);
        }
    }

    /// <summary>e.g. 北京.</summary>
    public string Name => Current.Name;

    /// <summary>e.g. 北纬 39.9°，东经 116.4°.</summary>
    public string CoordinatesText => Current.CoordinatesText;

    /// <summary>The short form on the chip, e.g. 北京 · 北纬 39.9°.</summary>
    public string ChipText => LocalizationService.Instance.Format("Location_Chip", Name, ExplorePlace.LatitudeText(Current.Latitude));

    /// <summary>Its time zone and gravity, e.g. UTC+8 · g = 9.792 m/s².</summary>
    public string DetailsText => LocalizationService.Instance.Format(
        "Location_Details",
        WorldCities.OffsetText(Current.TimeZone.GetUtcOffset(DateTime.UtcNow)),
        Current.Gravity.ToString("0.000", CultureInfo.InvariantCulture));

    public bool IsCustom
    {
        get => Current.IsCustom;
        set
        {
            if (value) _service.SelectCustom(CustomLatitude, CustomLongitude);
        }
    }

    /// <summary>The custom point being edited; <see cref="UseCustomCommand"/> makes it the location.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomText))]
    private double _customLatitude;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomText))]
    private double _customLongitude;

    public string CustomText => LocalizationService.Instance.Format(
        "Location_Coordinates",
        ExplorePlace.LatitudeText(CustomLatitude),
        ExplorePlace.LongitudeText(CustomLongitude));

    [RelayCommand]
    private void UseCustom() => _service.SelectCustom(CustomLatitude, CustomLongitude);

    /// <summary>Starts the custom point from the chosen place, so it can be nudged from there.</summary>
    [RelayCommand]
    private void CopyCurrent()
    {
        CustomLatitude = Current.Latitude;
        CustomLongitude = Current.Longitude;
    }

    partial void OnCustomLatitudeChanged(double value)
    {
        var clamped = Math.Round(Math.Clamp(value, -90, 90), 2);
        if (clamped != value) CustomLatitude = clamped;
    }

    partial void OnCustomLongitudeChanged(double value)
    {
        var normalized = Math.Round(ExplorePlaces.NormalizeLongitude(value), 2);
        if (normalized != value) CustomLongitude = normalized;
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (Current.IsCustom) (CustomLatitude, CustomLongitude) = (Current.Latitude, Current.Longitude);
        RaiseAll();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => RaiseAll();

    private void RaiseAll()
    {
        foreach (var place in Places) place.Refresh();
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(SelectedPlace));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(CoordinatesText));
        OnPropertyChanged(nameof(ChipText));
        OnPropertyChanged(nameof(DetailsText));
        OnPropertyChanged(nameof(IsCustom));
        OnPropertyChanged(nameof(CustomText));
    }

    public void Dispose()
    {
        _service.Changed -= OnLocationChanged;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
    }
}
