using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;

namespace Lemon.Explore.Book.Chapters.Seasons;

/// <summary>
/// Base of the small drawn figures beside the seasons diagram: a label cache that is dropped (and the figure redrawn)
/// when the language changes while the figure is on screen.
/// </summary>
public abstract class SeasonFigure : Control
{
    private protected SeasonFigure()
    {
        Labels = new LabelCache(this);
        ClipToBounds = true;
    }

    private protected LabelCache Labels { get; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        Labels.Clear();
        InvalidateVisual();
    }
}
