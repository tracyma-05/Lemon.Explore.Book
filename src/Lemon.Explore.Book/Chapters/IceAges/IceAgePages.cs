using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.IceAges;

/// <summary>The Ice ages chapter's left page: the time, the view's settings, its chart, and what is happening.</summary>
public sealed class IceAgeTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, IceAgeViewModel ice)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public IceAgeViewModel Ice { get; } = ice;

    public IChapterContent Content => Ice;
}

/// <summary>The Ice ages chapter's right page: Earth's orbit and tilt, summer sunshine and the ice, or an ice sheet in section.</summary>
public sealed class IceAgeDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, IceAgeViewModel ice)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public IceAgeViewModel Ice { get; } = ice;

    public IChapterContent Content => Ice;
}

public partial class IceAgeTextPageView : ChapterPageView
{
    public IceAgeTextPageView()
    {
        InitializeComponent();
    }
}

public partial class IceAgeDiagramPageView : ChapterPageView
{
    public IceAgeDiagramPageView()
    {
        InitializeComponent();
    }
}
