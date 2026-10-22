using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Contents;

namespace Lemon.Explore.Book.ViewModels.Pages;

/// <summary>Front cover, alone on the right like a closed book.</summary>
public sealed partial class CoverPageViewModel(int index, IBookNavigator navigator) : PageViewModel(index, navigator)
{
    public override bool HasFolio => false;

    public override string Caption => Infrastructures.Localization.LocalizationService.Instance.GetString("Page_Caption_Cover");

    [RelayCommand]
    private void Open() => Navigator.GoToPage(BookContents.FirstContentsPage);
}

/// <summary>Inside the cover: how to turn pages, explore and listen.</summary>
public sealed class HowToUsePageViewModel(int index, IBookNavigator navigator) : PageViewModel(index, navigator)
{
    public override bool HasFolio => false;

    public override string Caption => Infrastructures.Localization.LocalizationService.Instance.GetString("Page_Caption_HowToUse");
}

/// <summary>Title page: title, edition and licence.</summary>
public sealed class TitlePageViewModel(int index, IBookNavigator navigator) : PageViewModel(index, navigator)
{
    public override bool HasFolio => false;

    public override string Caption => Infrastructures.Localization.LocalizationService.Instance.GetString("Page_Caption_Title");
}

/// <summary>Inside the back cover: licences and sources.</summary>
public sealed class CreditsPageViewModel(int index, IBookNavigator navigator) : PageViewModel(index, navigator)
{
    public override string Caption => Infrastructures.Localization.LocalizationService.Instance.GetString("Credits_Title");
}

/// <summary>Back cover.</summary>
public sealed class BackCoverPageViewModel(int index, IBookNavigator navigator) : PageViewModel(index, navigator)
{
    public override bool HasFolio => false;

    public override string Caption => Infrastructures.Localization.LocalizationService.Instance.GetString("Page_Caption_BackCover");
}
