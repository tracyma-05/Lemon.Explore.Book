using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace Lemon.Explore.Book.Infrastructures.Localization;

/// <summary>
/// XAML shorthand for a live-updating localized string: <c>Text="{loc:Localize Cover_Open}"</c>.
/// </summary>
/// <remarks>
/// Returns a reflection binding to the <see cref="LocalizationService"/> indexer rather than the string itself,
/// so switching language refreshes it without reloading the page. Reflection rather than compiled: the source
/// is fixed and the key is only known at run time.
/// </remarks>
public sealed class LocalizeExtension : MarkupExtension
{
    public LocalizeExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => CreateBinding(Key);

    internal static ReflectionBinding CreateBinding(string key) => new($"[{key}]")
    {
        Source = LocalizationService.Instance,
        Mode = BindingMode.OneWay,
    };
}
