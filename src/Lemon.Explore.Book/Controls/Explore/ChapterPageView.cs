using Avalonia;
using Avalonia.Controls;

namespace Lemon.Explore.Book.Controls.Explore;

/// <summary>A ported chapter's shared state, told when its pages come on and leave the screen.</summary>
public interface IChapterContent
{
    /// <summary>A view of the chapter came on screen (its pages are shown or about to be turned to).</summary>
    void ViewAttached();

    /// <summary>
    /// A view of the chapter left the screen. Once none is left the reader has turned away, so the walkthrough and any
    /// explanation being read stop rather than talking over the next chapter.
    /// </summary>
    void ViewDetached();
}

/// <summary>A page of a ported chapter: either of the two pages of its spread.</summary>
public interface IChapterPage
{
    IChapterContent Content { get; }
}

/// <summary>Base of a ported chapter's page views: reports them coming on and off screen to the chapter.</summary>
public abstract class ChapterPageView : UserControl
{
    private IChapterContent? _shown;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _shown = (DataContext as IChapterPage)?.Content;
        _shown?.ViewAttached();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _shown?.ViewDetached();
        _shown = null;
    }
}
