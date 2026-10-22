using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Lemon.Explore.Book.Explore.Places;

namespace Lemon.Explore.Book.Controls.Explore;

/// <summary>
/// The shared observer location on a chapter's left page. It makes its own picker over <see cref="Service"/> while
/// it is on screen, so a page only places it and binds the service; the chapter follows the service.
/// </summary>
public partial class LocationChip : UserControl
{
    public static readonly StyledProperty<IExploreLocationService?> ServiceProperty =
        AvaloniaProperty.Register<LocationChip, IExploreLocationService?>(nameof(Service));

    private ExploreLocationPickerViewModel? _picker;

    public LocationChip()
    {
        InitializeComponent();
    }

    public IExploreLocationService? Service
    {
        get => GetValue(ServiceProperty);
        set => SetValue(ServiceProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ServiceProperty && this.IsAttachedToVisualTree())
        {
            Attach();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Chip.Flyout?.Hide();
        Detach();
    }

    private void Attach()
    {
        Detach();
        if (Service is { } service)
        {
            _picker = new ExploreLocationPickerViewModel(service);
            Chip.DataContext = _picker;
        }
    }

    private void Detach()
    {
        Chip.DataContext = null;
        _picker?.Dispose();
        _picker = null;
    }
}
