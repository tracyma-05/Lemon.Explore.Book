using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.ViewModels;

/// <summary>The whole book: its pages in order, the reading page, and the reader's settings.</summary>
public sealed partial class MainViewModel : ObservableObject, IBookNavigator
{
    private readonly ISettingsStore _settings;
    private readonly ReadingProgress _progress;

    [ObservableProperty]
    private int _pageIndex;

    public MainViewModel(ISettingsStore settings)
        : this(BookServices.Create(settings))
    {
    }

    public MainViewModel(BookServices services)
    {
        var settings = services.Settings;
        _settings = settings;
        Pages = BuildPages(this, services);

        var saved = int.TryParse(settings.Get(SettingKeys.PageIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out var page)
            ? page
            : 0;
        _pageIndex = Math.Clamp(saved, 0, Pages.Count - 1);
        Settings = new SettingsViewModel(services);
        Bookmarks = new BookmarksViewModel(services.Bookmarks, Pages, this, _pageIndex);
        _progress = services.Progress;
        MarkOpened(_pageIndex);
    }

    public IReadOnlyList<PageViewModel> Pages { get; }

    /// <summary>Language, observer location, narration and page turning (the settings button).</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>The bookmarks button and the ribbons on marked pages.</summary>
    public BookmarksViewModel Bookmarks { get; }

    public void GoToPage(int pageIndex) => PageIndex = Math.Clamp(pageIndex, 0, Pages.Count - 1);

    [RelayCommand]
    private void GoToContents() => GoToPage(BookContents.FirstContentsPage);

    /// <summary>Flips between English and Chinese; the choice is remembered on this device.</summary>
    [RelayCommand]
    private void SwitchLanguage() => Settings.SwitchLanguage();

    partial void OnPageIndexChanged(int value)
    {
        _settings.Set(SettingKeys.PageIndex, value.ToString(CultureInfo.InvariantCulture));
        Bookmarks.SetCurrent(value);
        MarkOpened(value);
    }

    /// <summary>Turning to a chapter's pages counts as opening it, for the progress in the contents.</summary>
    private void MarkOpened(int page)
    {
        var chapter = Pages[page] switch
        {
            ChapterTextPageViewModel text => text.Chapter,
            ChapterDiagramPageViewModel diagram => diagram.Chapter,
            _ => null,
        };
        if (chapter is not null) _progress.MarkOpened(chapter.Slug);
    }

    internal static IReadOnlyList<PageViewModel> BuildPages(IBookNavigator navigator, BookServices services)
    {
        var pages = new List<PageViewModel>(BookContents.PageCount)
        {
            new CoverPageViewModel(0, navigator),
            new HowToUsePageViewModel(1, navigator),
            new TitlePageViewModel(2, navigator),
        };

        foreach (var volume in BookContents.Volumes)
        {
            pages.Add(new ContentsPageViewModel(pages.Count, navigator, volume, services.Progress));
        }

        for (var i = 0; i < BookContents.Chapters.Count; i++)
        {
            var (text, diagram) = ChapterRegistry.CreatePages(BookContents.Chapters[i], i + 1, pages.Count, navigator, services);
            pages.Add(text);
            pages.Add(diagram);
        }

        pages.Add(new CreditsPageViewModel(pages.Count, navigator));
        pages.Add(new BackCoverPageViewModel(pages.Count, navigator));
        return pages;
    }
}
