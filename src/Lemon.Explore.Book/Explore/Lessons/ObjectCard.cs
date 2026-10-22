using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Infrastructures.Localization;

namespace Lemon.Explore.Book.Explore.Lessons;

/// <summary>
/// The thing last clicked in an Explore diagram and its explanation card: <see cref="Id"/> is bound two-way to the
/// diagram's <c>SelectedObject</c>; the card shows the title and the narration clip's text, and the clip is read aloud
/// when the narration settings say so. While the viewer is doing a walkthrough's "your turn" task, the card is enough.
/// </summary>
public sealed partial class ObjectCard : ObservableObject, IDisposable
{
    private readonly INarrationService _narration;
    private readonly LessonPlayer _lesson;
    private readonly Func<string?, string?> _titleKey;
    private readonly Func<string?, string?> _clipKey;

    /// <param name="titleKey">A target id's title key (the diagram's <c>ObjectTitleKey</c>), null if it has none.</param>
    /// <param name="clipKey">A target id's clip key (the diagram's <c>ObjectClipKey</c>), null if it has none.</param>
    public ObjectCard(INarrationService narration, LessonPlayer lesson, Func<string?, string?> titleKey, Func<string?, string?> clipKey)
    {
        _narration = narration;
        _lesson = lesson;
        _titleKey = titleKey;
        _clipKey = clipKey;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasObject), nameof(Title), nameof(Text))]
    private string? _id;

    private bool _quiet;

    public bool HasObject => _clipKey(Id) is not null;

    public string Title => _titleKey(Id) is { } key ? LocalizationService.Instance.GetString(key) : string.Empty;

    /// <summary>The explanation shown is the clip's own text.</summary>
    public string Text => _clipKey(Id) is { } key ? LocalizationService.Instance.GetString(key) : string.Empty;

    partial void OnIdChanged(string? value)
    {
        if (value is not null && !_quiet && _narration.Settings.SpeakOnClick && !_lesson.IsTryIt) Listen();
    }

    /// <summary>Shows a thing without reading it aloud: a walkthrough moved there and is speaking itself.</summary>
    public void ShowQuietly(string? id)
    {
        _quiet = true;
        try
        {
            Id = id;
        }
        finally
        {
            _quiet = false;
        }
    }

    [RelayCommand]
    private void Listen()
    {
        if (_clipKey(Id) is not { } key) return;
        _lesson.UserInteracted();
        _narration.Play(key, this);
    }

    [RelayCommand]
    public void Close()
    {
        _narration.Stop(this);
        Id = null;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Text));
    }

    public void Dispose()
    {
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        _narration.Stop(this);
    }
}
