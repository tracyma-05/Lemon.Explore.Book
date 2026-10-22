using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.ViewModels;

/// <summary>The whole book: its pages in order, the reading page, and the reader's language.</summary>
public sealed partial class MainViewModel : ObservableObject, IBookNavigator
{
    private readonly ISettingsStore _settings;

    [ObservableProperty]
    private int _pageIndex;

    public MainViewModel(ISettingsStore settings)
    {
        _settings = settings;
        Pages = BuildPages(this);

        var saved = int.TryParse(settings.Get(SettingKeys.PageIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out var page)
            ? page
            : 0;
        _pageIndex = Math.Clamp(saved, 0, Pages.Count - 1);
        AnimatesTurns = settings.Get(SettingKeys.AnimatesTurns) != bool.FalseString;
    }

    public IReadOnlyList<PageViewModel> Pages { get; }

    public bool AnimatesTurns { get; }

    public void GoToPage(int pageIndex) => PageIndex = Math.Clamp(pageIndex, 0, Pages.Count - 1);

    [RelayCommand]
    private void GoToContents() => GoToPage(BookContents.FirstContentsPage);

    /// <summary>Flips between English and Chinese; the choice is remembered on this device.</summary>
    [RelayCommand]
    private void SwitchLanguage()
    {
        var localization = LocalizationService.Instance;
        var next = localization.SupportedCultures.First(c => !c.Equals(localization.CurrentCulture));
        localization.SetCulture(next);
        _settings.Set(SettingKeys.Culture, next.Name);
    }

    partial void OnPageIndexChanged(int value) =>
        _settings.Set(SettingKeys.PageIndex, value.ToString(CultureInfo.InvariantCulture));

    internal static IReadOnlyList<PageViewModel> BuildPages(IBookNavigator navigator)
    {
        var pages = new List<PageViewModel>(BookContents.PageCount)
        {
            new CoverPageViewModel(0, navigator),
            new HowToUsePageViewModel(1, navigator),
            new TitlePageViewModel(2, navigator),
        };

        foreach (var volume in BookContents.Volumes)
        {
            pages.Add(new ContentsPageViewModel(pages.Count, navigator, volume));
        }

        for (var i = 0; i < BookContents.Chapters.Count; i++)
        {
            var chapter = BookContents.Chapters[i];
            pages.Add(new ChapterTextPageViewModel(pages.Count, navigator, chapter, i + 1));
            pages.Add(new ChapterDiagramPageViewModel(pages.Count, navigator, chapter));
        }

        pages.Add(new CreditsPageViewModel(pages.Count, navigator));
        pages.Add(new BackCoverPageViewModel(pages.Count, navigator));
        return pages;
    }
}
