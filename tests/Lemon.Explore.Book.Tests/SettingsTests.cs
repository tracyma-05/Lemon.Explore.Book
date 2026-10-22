using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.ViewModels;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class SettingsTests
{
    [AvaloniaFact]
    public void NarrationChoices_GoToTheNarrationService_AndAreSaved()
    {
        var store = new MemorySettingsStore();
        var settings = new SettingsViewModel(BookServices.Create(store));

        settings.VoiceEnabled = false;
        settings.VolumePercent = 40;
        settings.ShowSubtitles = false;
        settings.SpeakOnClick = false;

        var reloaded = BookServices.Create(store).Narration.Settings;
        Assert.False(reloaded.VoiceEnabled);
        Assert.Equal(0.4, reloaded.Volume, 6);
        Assert.False(reloaded.ShowSubtitles);
        Assert.False(reloaded.SpeakOnClick);
    }

    [AvaloniaFact]
    public void PageTurning_IsSaved_AndReachesTheBook()
    {
        var store = new MemorySettingsStore();
        var model = new MainViewModel(store);
        var window = new Window { Width = 1360, Height = 860, Content = new Views.MainView { DataContext = model } };
        window.Show();
        Frames(3);
        var book = window.FindDescendantOfType<BookView>()!;
        Assert.True(book.AnimatesTurns);

        model.Settings.AnimatesTurns = false;

        Assert.False(book.AnimatesTurns);
        Assert.False(new SettingsViewModel(BookServices.Create(store)).AnimatesTurns);
        window.Close();
    }

    [AvaloniaFact]
    public void TheLanguage_IsChosenAndSaved()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        try
        {
            localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
            var store = new MemorySettingsStore();
            var settings = new SettingsViewModel(BookServices.Create(store));
            Assert.True(settings.IsChinese);

            settings.IsEnglish = true;

            Assert.Equal("en", localization.CurrentCulture.TwoLetterISOLanguageName);
            Assert.False(settings.IsChinese);
            Assert.Equal("en", store.Get(SettingKeys.Culture));
        }
        finally
        {
            localization.SetCulture(original);
        }
    }

    [AvaloniaFact]
    public void TheLocationInTheSettings_IsTheOneTheChaptersFollow()
    {
        var (window, model) = OpenAt("Day-Night");
        var day = ((DayNightTextPageViewModel)model.Pages[TextPageOf("Day-Night")]).Day;

        model.Settings.Location.SelectedPlace = model.Settings.Location.Places.First(p => p.Id == "London");

        Assert.Equal("London", day.PlaceCity);
        window.Close();
    }

    [AvaloniaFact]
    public void TheSettingsPanel_OpensFromTheTopRight()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
        try
        {
            var (window, _) = OpenAt("Day-Night");
            var button = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "SettingsButton");
            Assert.True(button.TranslatePoint(default, window)!.Value.X > window.Width * 0.75);

            button.Flyout!.ShowAt(button);
            Frames(5);
            Snapshot(window, "settings-zh");

            // The narration and reading sections, further down.
            var panel = TopLevel.GetTopLevel(window)!.GetVisualDescendants().OfType<Views.SettingsView>().FirstOrDefault()
                ?? Avalonia.Controls.Primitives.OverlayLayer.GetOverlayLayer(window)!.GetVisualDescendants().OfType<Views.SettingsView>().First();
            var scroller = panel.GetVisualDescendants().OfType<ScrollViewer>().First();
            scroller.Offset = new Vector(0, scroller.Extent.Height);
            Frames(5);
            Snapshot(window, "settings-bottom-zh");
            button.Flyout.Hide();
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
