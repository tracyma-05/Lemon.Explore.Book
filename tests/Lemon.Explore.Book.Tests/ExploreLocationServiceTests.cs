using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class ExploreLocationServiceTests
{
    [Fact]
    public void BeijingIsTheDefault()
    {
        var service = new ExploreLocationService(new MemorySettingsStore());

        Assert.Equal("Beijing", service.Current.Id);
        Assert.False(service.Current.IsCustom);
    }

    [Fact]
    public void APreset_IsSavedAndRaisesChanged_Once()
    {
        var settings = new MemorySettingsStore();
        var service = new ExploreLocationService(settings);
        var changes = 0;
        service.Changed += (_, _) => changes++;

        service.Select("Tokyo");
        service.Select("Tokyo");
        service.Select("Nowhere");

        Assert.Equal(1, changes);
        Assert.Equal("Tokyo", new ExploreLocationService(settings).Current.Id);
    }

    [Fact]
    public void ACustomPoint_IsKept_WhileAPresetIsChosen()
    {
        var settings = new MemorySettingsStore();
        var service = new ExploreLocationService(settings);

        service.SelectCustom(-12.5, 200);
        Assert.True(service.Current.IsCustom);
        Assert.Equal(-160, service.Current.Longitude, 6);

        service.Select("London");
        Assert.Equal((-12.5, -160.0), service.CustomPoint);

        service.Select(ExplorePlaces.CustomId);
        var reloaded = new ExploreLocationService(settings);
        Assert.True(reloaded.Current.IsCustom);
        Assert.Equal(-12.5, reloaded.Current.Latitude, 6);
    }

    [Fact]
    public void ABrokenSetting_FallsBackToTheDefault()
    {
        var settings = new MemorySettingsStore();
        settings.Set(ExploreLocationService.SettingKey, "{not json");

        Assert.Equal(ExplorePlaces.DefaultId, new ExploreLocationService(settings).Current.Id);
    }
}
