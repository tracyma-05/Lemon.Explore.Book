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
    /// <summary>A page narrower than this (a phone's) gets slimmer margins than the book's.</summary>
    internal const double NarrowPageWidth = 520;

    /// <summary>The slimmest margins a narrow page keeps on each side: left, top, right, bottom.</summary>
    private static readonly Thickness NarrowMargin = new(12, 14, 12, 10);

    private IChapterContent? _shown;
    private Thickness? _bookMargin;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty || change.Property == ContentProperty)
        {
            FitMargin();
        }
    }

    /// <summary>
    /// The page views set the book's generous margins on their root panel. On a phone they would take a sixth of the
    /// width from the diagram, so a narrow page trims each to <see cref="NarrowMargin"/>, and a wide one puts them back.
    /// </summary>
    private void FitMargin()
    {
        if (Content is not Control root || Bounds.Width <= 0)
        {
            return;
        }

        _bookMargin ??= root.Margin;
        var book = _bookMargin.Value;
        root.Margin = Bounds.Width >= NarrowPageWidth
            ? book
            : new Thickness(
                Math.Min(book.Left, NarrowMargin.Left),
                Math.Min(book.Top, NarrowMargin.Top),
                Math.Min(book.Right, NarrowMargin.Right),
                Math.Min(book.Bottom, NarrowMargin.Bottom));
    }

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
