using Lemon.Book.Shell.Controls.Explore;
using Lemon.Book.Shell.ViewModels.Pages;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.StatesOfMatter;

/// <summary>The States of matter chapter's left page: the substance, temperature and pressure or the heating's settings, their chart, and what is happening.</summary>
public sealed class StatesOfMatterTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, MatterViewModel matter)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public MatterViewModel Matter { get; } = matter;

    public IChapterContent Content => Matter;
}

/// <summary>The States of matter chapter's right page: the cylinder of molecules, the heating curve, or the phase diagram.</summary>
public sealed class StatesOfMatterDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, MatterViewModel matter)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public MatterViewModel Matter { get; } = matter;

    public IChapterContent Content => Matter;
}

public partial class StatesOfMatterTextPageView : ChapterPageView
{
    public StatesOfMatterTextPageView()
    {
        InitializeComponent();
    }
}

public partial class StatesOfMatterDiagramPageView : ChapterPageView
{
    public StatesOfMatterDiagramPageView()
    {
        InitializeComponent();
    }
}
