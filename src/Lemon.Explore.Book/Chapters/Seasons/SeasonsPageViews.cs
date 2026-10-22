using Avalonia;
using Avalonia.Controls;

namespace Lemon.Explore.Book.Chapters.Seasons;

/// <summary>
/// Tells the chapter when its pages come on and leave the screen, so a walkthrough stops when the reader turns away.
/// </summary>
public abstract class SeasonsPageView : UserControl
{
    private SeasonsViewModel? _shown;

    private protected abstract SeasonsViewModel? Seasons { get; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _shown = Seasons;
        _shown?.ViewAttached();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _shown?.ViewDetached();
        _shown = null;
    }
}

public partial class SeasonsTextPageView : SeasonsPageView
{
    public SeasonsTextPageView()
    {
        InitializeComponent();
    }

    private protected override SeasonsViewModel? Seasons => (DataContext as SeasonsTextPageViewModel)?.Seasons;
}

public partial class SeasonsDiagramPageView : SeasonsPageView
{
    public SeasonsDiagramPageView()
    {
        InitializeComponent();
    }

    private protected override SeasonsViewModel? Seasons => (DataContext as SeasonsDiagramPageViewModel)?.Seasons;
}
