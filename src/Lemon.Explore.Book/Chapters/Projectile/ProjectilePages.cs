using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.ViewModels.Pages;

namespace Lemon.Explore.Book.Chapters.Projectile;

/// <summary>The Projectile chapter's left page: where and what is thrown, the shot, its charts, and what is happening.</summary>
public sealed class ProjectileTextPageViewModel(int index, IBookNavigator navigator, Chapter chapter, int number, ProjectileViewModel projectile)
    : ChapterTextPageViewModel(index, navigator, chapter, number), IChapterPage
{
    public ProjectileViewModel Projectile { get; } = projectile;

    public IChapterContent Content => Projectile;
}

/// <summary>The Projectile chapter's right page: a shot, the best angle, or two things falling together, with playback.</summary>
public sealed class ProjectileDiagramPageViewModel(int index, IBookNavigator navigator, Chapter chapter, ProjectileViewModel projectile)
    : ChapterDiagramPageViewModel(index, navigator, chapter), IChapterPage
{
    public ProjectileViewModel Projectile { get; } = projectile;

    public IChapterContent Content => Projectile;
}

public partial class ProjectileTextPageView : ChapterPageView
{
    public ProjectileTextPageView()
    {
        InitializeComponent();
    }
}

public partial class ProjectileDiagramPageView : ChapterPageView
{
    public ProjectileDiagramPageView()
    {
        InitializeComponent();
    }
}
