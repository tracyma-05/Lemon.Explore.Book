using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Infrastructures.Localization;

namespace Lemon.Explore.Book.ViewModels.Pages;

/// <summary>A chapter's left-hand page: its number, title and (once ported) the lesson text.</summary>
public class ChapterTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number)
    : PageViewModel(index, navigator)
{
    public Chapter Chapter { get; } = chapter;

    public int Number { get; } = number;

    public string NumberLabel => LocalizationService.Instance.Format("Chapter_Number", Number);

    public string Title => LocalizationService.Instance.GetString(Chapter.TitleKey);

    public string VolumeTitle => LocalizationService.Instance.GetString("Volume_" + Chapter.Volume);

    protected override void OnCultureChanged()
    {
        OnPropertyChanged(nameof(NumberLabel));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(VolumeTitle));
    }
}

/// <summary>A chapter's right-hand page: the interactive diagram (a placeholder until the chapter is ported).</summary>
public class ChapterDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter)
    : PageViewModel(index, navigator)
{
    public Chapter Chapter { get; } = chapter;

    public Volume Volume => Chapter.Volume;
}
