using CommunityToolkit.Mvvm.ComponentModel;

namespace Lemon.Explore.Book.Explore.Lessons;

// Shared by many chapters' view models, under the names the original app gave them (so a ported view model needs no
// edits): a row of a "what is happening" card, and one choice out of a row of chips.

/// <summary>A row of the "what is happening" card: a name and its value.</summary>
public sealed record OscillationStat(string Name, string Value);

/// <summary>
/// One of a few chips, by id: bind a chip's <c>IsChecked</c> to <c>Choice[id]</c>; <see cref="Selected"/> is the chosen id.
/// </summary>
public sealed class OscillationChoice(string selected) : ObservableObject
{
    private string _selected = selected;

    public string Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;

            // Avalonia's indexer bindings listen for the indexer's own name, not WPF's "Item[]".
            OnPropertyChanged("Item");
        }
    }

    public bool this[string id]
    {
        get => _selected == id;
        set
        {
            if (value) Selected = id;
        }
    }
}
