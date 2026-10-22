using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Infrastructures.Localization;

namespace Lemon.Explore.Book.ViewModels.Pages;

/// <summary>One table-of-contents page: a volume and its chapters.</summary>
public sealed class ContentsPageViewModel : PageViewModel
{
    public ContentsPageViewModel(int index, IBookNavigator navigator, Volume volume)
        : base(index, navigator)
    {
        Volume = volume;
        Entries = BookContents.Chapters
            .Select((chapter, chapterIndex) => (chapter, chapterIndex))
            .Where(c => c.chapter.Volume == volume)
            .Select(c => new ContentsEntry(c.chapter, c.chapterIndex + 1, BookContents.ChapterPage(c.chapterIndex), navigator))
            .ToArray();
    }

    public Volume Volume { get; }

    public IReadOnlyList<ContentsEntry> Entries { get; }

    public string PartLabel => LocalizationService.Instance.Format("Volume_Part", (int)Volume + 1);

    public string VolumeTitle => LocalizationService.Instance.GetString("Volume_" + Volume);

    protected override void OnCultureChanged()
    {
        OnPropertyChanged(nameof(PartLabel));
        OnPropertyChanged(nameof(VolumeTitle));
        foreach (var entry in Entries)
        {
            entry.RefreshText();
        }
    }
}

/// <summary>A contents line: chapter number, title and page, clickable.</summary>
public sealed partial class ContentsEntry : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private readonly Chapter _chapter;
    private readonly IBookNavigator _navigator;

    public ContentsEntry(Chapter chapter, int number, int pageIndex, IBookNavigator navigator)
    {
        _chapter = chapter;
        _navigator = navigator;
        Number = number;
        PageIndex = pageIndex;
    }

    public int Number { get; }

    public int PageIndex { get; }

    public string Title => LocalizationService.Instance.GetString(_chapter.TitleKey);

    public string PageText => PageIndex.ToString(LocalizationService.Instance.CurrentCulture);

    internal void RefreshText()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PageText));
    }

    [RelayCommand]
    private void Open() => _navigator.GoToPage(PageIndex);
}
