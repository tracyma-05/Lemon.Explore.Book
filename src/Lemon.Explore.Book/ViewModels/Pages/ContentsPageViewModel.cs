using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Infrastructures.Localization;

namespace Lemon.Explore.Book.ViewModels.Pages;

/// <summary>One table-of-contents page: a volume and its chapters.</summary>
public sealed class ContentsPageViewModel : PageViewModel
{
    public ContentsPageViewModel(int index, IBookNavigator navigator, Volume volume, ReadingProgress progress)
        : base(index, navigator)
    {
        Volume = volume;
        Entries = BookContents.Chapters
            .Select((chapter, chapterIndex) => (chapter, chapterIndex))
            .Where(c => c.chapter.Volume == volume)
            .Select(c => new ContentsEntry(c.chapter, c.chapterIndex + 1, BookContents.ChapterPage(c.chapterIndex), navigator, progress))
            .ToArray();
    }

    public Volume Volume { get; }

    public IReadOnlyList<ContentsEntry> Entries { get; }

    public string PartLabel => LocalizationService.Instance.Format("Volume_Part", (int)Volume + 1);

    public string VolumeTitle => LocalizationService.Instance.GetString("Volume_" + Volume);

    public override string Caption => LocalizationService.Instance.Format("Page_Caption_Contents", VolumeTitle);

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

/// <summary>A contents line: chapter number, title, how far the reader got, and page; clickable.</summary>
public sealed partial class ContentsEntry : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private readonly Chapter _chapter;
    private readonly IBookNavigator _navigator;

    private readonly ReadingProgress _progress;

    public ContentsEntry(Chapter chapter, int number, int pageIndex, IBookNavigator navigator, ReadingProgress progress)
    {
        _chapter = chapter;
        _navigator = navigator;
        _progress = progress;
        Number = number;
        PageIndex = pageIndex;
        progress.Changed += (_, _) => RefreshProgress();
    }

    /// <summary>Empty, "read", "walkthroughs 1/3" or "done".</summary>
    public string ProgressText
    {
        get
        {
            var progress = _progress.For(_chapter.Slug);
            var localization = LocalizationService.Instance;
            return progress switch
            {
                { IsComplete: true } => localization.GetString("Progress_Complete"),
                { WalkthroughsDone: > 0 } => localization.Format("Progress_Walkthroughs", progress.WalkthroughsDone, progress.WalkthroughCount),
                { Opened: true } => localization.GetString("Progress_Opened"),
                _ => string.Empty,
            };
        }
    }

    public bool IsComplete => _progress.For(_chapter.Slug).IsComplete;

    private void RefreshProgress()
    {
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(IsComplete));
    }

    public int Number { get; }

    public int PageIndex { get; }

    public string Title => LocalizationService.Instance.GetString(_chapter.TitleKey);

    public string PageText => PageIndex.ToString(LocalizationService.Instance.CurrentCulture);

    internal void RefreshText()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(ProgressText));
    }

    [RelayCommand]
    private void Open() => _navigator.GoToPage(PageIndex);
}
