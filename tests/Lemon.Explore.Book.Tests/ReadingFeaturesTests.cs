using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Chapters.Tides;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls;
using Lemon.Explore.Book.Explore.Lessons;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.Infrastructures.Theming;
using Lemon.Explore.Book.ViewModels;
using Lemon.Explore.Book.ViewModels.Pages;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

/// <summary>Bookmarks, reading progress, the light and dark themes, and the preset places.</summary>
public sealed class ReadingFeaturesTests
{
    [Fact]
    public void Changsha_IsAPresetPlace_WithANameInBothLanguages()
    {
        var changsha = ExplorePlaces.Find("Changsha");
        Assert.NotNull(changsha);
        Assert.Equal(28.23, changsha.Latitude, 2);
        Assert.Equal(ExplorePlaces.DefaultId, ExplorePlaces.All[0].Id);
        foreach (var keys in BothLanguages())
        {
            Assert.Contains(changsha.NameKey, keys);
        }
    }

    [AvaloniaFact]
    public void Bookmarks_AreToggled_Listed_AndSaved()
    {
        var store = new MemorySettingsStore();
        var model = new MainViewModel(store);
        var page = TextPageOf("Tides");
        model.GoToPage(page);

        model.Bookmarks.ToggleCurrentCommand.Execute(null);
        Assert.True(model.Bookmarks.IsCurrentMarked);
        Assert.Contains(page, model.Bookmarks.MarkedPages);
        var entry = Assert.Single(model.Bookmarks.Items);
        Assert.Contains(LocalizationService.Instance.GetString(BookContents.Chapters.First(c => c.Slug == "Tides").TitleKey), entry.Caption);

        model.GoToPage(0);
        Assert.False(model.Bookmarks.IsCurrentMarked);
        entry.OpenCommand.Execute(null);
        Assert.Equal(page, model.PageIndex);

        // Saved: a new book on the same device has it.
        var reopened = new MainViewModel(store);
        Assert.Contains(page, reopened.Bookmarks.MarkedPages);
        reopened.Bookmarks.Items[0].RemoveCommand.Execute(null);
        Assert.Empty(new MainViewModel(store).Bookmarks.Items);
    }

    [Fact]
    public void BrokenOrOutOfRangeBookmarks_AreDropped()
    {
        var store = new MemorySettingsStore();
        store.Set(Bookmarks.SettingKey, "[{\"Page\":5,\"Added\":\"2026-10-22T00:00:00Z\"},{\"Page\":99999,\"Added\":\"2026-10-22T00:00:00Z\"}]");
        Assert.Equal([5], new Bookmarks(store).Items.Select(b => b.Page));

        store.Set(Bookmarks.SettingKey, "not json");
        Assert.Empty(new Bookmarks(store).Items);
    }

    [AvaloniaFact]
    public void Progress_CountsOpenedChapters_AndFinishedWalkthroughs()
    {
        var store = new MemorySettingsStore();
        var model = new MainViewModel(store);
        var entry = ContentsEntryFor(model, "Tides");
        Assert.Equal(string.Empty, entry.ProgressText);

        model.GoToPage(TextPageOf("Tides"));
        Assert.Equal(LocalizationService.Instance.GetString("Progress_Opened"), entry.ProgressText);

        // Run the bulges walkthrough to its end.
        var tides = ((TidesTextPageViewModel)model.Pages[TextPageOf("Tides")]).Tides;
        tides.Mode = TidesMode.Bulges;
        FinishWalkthrough(tides.Lesson);
        Assert.Equal(LocalizationService.Instance.Format("Progress_Walkthroughs", 1, 3), entry.ProgressText);

        // Saved, and cleared from the settings.
        var reopened = new MainViewModel(store);
        Assert.Equal(1, BookServices.Create(store).Progress.Summary().Opened);
        reopened.Settings.ClearProgressCommand.Execute(null);
        Assert.Equal(string.Empty, ContentsEntryFor(reopened, "Tides").ProgressText);
        Assert.Equal(default, new ReadingProgress(store).For("Tides") with { WalkthroughCount = 0 });
    }

    [AvaloniaFact]
    public void TheTheme_SwitchesThePaper_AndTheAccentFollowsTheChoice()
    {
        var store = new MemorySettingsStore();
        var settings = new SettingsViewModel(BookServices.Create(store));
        var app = Application.Current!;
        try
        {
            settings.IsDarkTheme = true;
            Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);
            Assert.Equal(nameof(BookThemeMode.Dark), store.Get(BookTheme.ThemeSetting));

            settings.Accents.First(a => a.Color.Id == "Pine").IsSelected = true;
            Assert.True(app.TryFindResource("AccentBrush", ThemeVariant.Dark, out var accent));
            Assert.Equal(BookTheme.Accents.First(a => a.Id == "Pine").OnDark, ((ISolidColorBrush)accent!).Color);
            Assert.Equal("Pine", BookTheme.LoadAccent(store).Id);
        }
        finally
        {
            BookTheme.Apply(app, BookThemeMode.Light, BookTheme.DefaultAccent);
        }
    }

    [AvaloniaFact]
    public void TheBook_Renders_InLightAndDark_WithARibbonOnABookmarkedPage()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
        var app = Application.Current!;
        try
        {
            var (window, model) = OpenAt("Tides");
            model.Bookmarks.ToggleCurrentCommand.Execute(null);
            ((TidesTextPageViewModel)model.Pages[TextPageOf("Tides")]).Tides.IsPlaying = false;
            Frames(10);
            Assert.Contains(TextPageOf("Tides"), window.FindDescendantOfType<BookView>()!.BookmarkedPages!);
            Snapshot(window, "tides-bookmarked-light-zh");

            BookTheme.Apply(app, BookThemeMode.Dark, BookTheme.Accents.First(a => a.Id == "Indigo"));
            Frames(10);
            Snapshot(window, "tides-bookmarked-dark-zh");

            model.GoToPage(BookContents.ContentsPage(Volume.Sky));
            Frames(10);
            Snapshot(window, "contents-dark-zh");

            var bookmarks = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "BookmarksButton");
            bookmarks.Flyout!.ShowAt(bookmarks);
            Frames(5);
            Snapshot(window, "bookmarks-dark-zh");
            bookmarks.Flyout.Hide();

            var settingsButton = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "SettingsButton");
            settingsButton.Flyout!.ShowAt(settingsButton);
            Frames(5);
            Snapshot(window, "settings-dark-zh");
            settingsButton.Flyout.Hide();
            window.Close();
        }
        finally
        {
            BookTheme.Apply(app, BookThemeMode.Light, BookTheme.DefaultAccent);
            localization.SetCulture(original);
        }
    }

    [AvaloniaFact]
    public void OpeningTheBook_AddsNoBookmark()
    {
        var store = new MemorySettingsStore();
        store.Set(SettingKeys.PageIndex, TextPageOf("Tides").ToString(CultureInfo.InvariantCulture));
        var model = new MainViewModel(store);
        var window = new Window { Width = 1300, Height = 860, Content = new Views.MainView { DataContext = model } };
        window.Show();
        Frames(10);
        window.Close();

        Assert.Null(store.Get(Bookmarks.SettingKey));
    }

    private static ContentsEntry ContentsEntryFor(MainViewModel model, string slug) =>
        model.Pages.OfType<ContentsPageViewModel>().SelectMany(p => p.Entries)
            .First(e => e.PageIndex == TextPageOf(slug));

    private static void FinishWalkthrough(LessonPlayer lesson)
    {
        lesson.Start();
        for (var guard = 0; guard < 400 && lesson.State != LessonState.Done; guard++)
        {
            lesson.NextCommand.Execute(null);
        }

        Assert.Equal(LessonState.Done, lesson.State);
        lesson.Stop();
    }
}
