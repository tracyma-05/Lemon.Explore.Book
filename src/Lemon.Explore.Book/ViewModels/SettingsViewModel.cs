using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;

namespace Lemon.Explore.Book.ViewModels;

/// <summary>
/// Everything the reader can set, in one place (the settings button at the top right): language, observer location,
/// narration and page turning. Each choice is saved on this device as soon as it is made.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly INarrationService _narration;
    private bool _animatesTurns;

    public SettingsViewModel(BookServices services)
    {
        _store = services.Settings;
        _narration = services.Narration;
        _animatesTurns = _store.Get(SettingKeys.AnimatesTurns) != bool.FalseString;
        Location = new ExploreLocationPickerViewModel(services.Location);
        _narration.SettingsChanged += (_, _) => RaiseNarration();
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    /// <summary>The observer location picker, the same one a chapter's location chip opens.</summary>
    public ExploreLocationPickerViewModel Location { get; }

    #region language

    public bool IsChinese
    {
        get => LocalizationService.Instance.CurrentCulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value) SetCulture("zh-CN");
        }
    }

    public bool IsEnglish
    {
        get => !IsChinese;
        set
        {
            if (value) SetCulture("en");
        }
    }

    /// <summary>Flips between English and Chinese.</summary>
    public void SwitchLanguage()
    {
        if (IsChinese) IsEnglish = true;
        else IsChinese = true;
    }

    private void SetCulture(string name)
    {
        var localization = LocalizationService.Instance;
        var culture = localization.ResolveSupportedCulture(name);
        if (culture.Equals(localization.CurrentCulture)) return;
        localization.SetCulture(culture);
        _store.Set(SettingKeys.Culture, culture.Name);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsChinese));
        OnPropertyChanged(nameof(IsEnglish));
    }

    #endregion

    #region narration

    public bool VoiceEnabled
    {
        get => _narration.Settings.VoiceEnabled;
        set => _narration.Update(_narration.Settings with { VoiceEnabled = value });
    }

    /// <summary>0 to 100, for the slider.</summary>
    public double VolumePercent
    {
        get => Math.Round(_narration.Settings.Volume * 100);
        set => _narration.Update(_narration.Settings with { Volume = Math.Clamp(value, 0, 100) / 100 });
    }

    public bool ShowSubtitles
    {
        get => _narration.Settings.ShowSubtitles;
        set => _narration.Update(_narration.Settings with { ShowSubtitles = value });
    }

    public bool SpeakOnClick
    {
        get => _narration.Settings.SpeakOnClick;
        set => _narration.Update(_narration.Settings with { SpeakOnClick = value });
    }

    private void RaiseNarration()
    {
        OnPropertyChanged(nameof(VoiceEnabled));
        OnPropertyChanged(nameof(VolumePercent));
        OnPropertyChanged(nameof(ShowSubtitles));
        OnPropertyChanged(nameof(SpeakOnClick));
    }

    #endregion

    /// <summary>Pages curl over when turned; off, they change at once (also kinder to motion-sensitive readers).</summary>
    public bool AnimatesTurns
    {
        get => _animatesTurns;
        set
        {
            if (!SetProperty(ref _animatesTurns, value)) return;
            _store.Set(SettingKeys.AnimatesTurns, value ? null : bool.FalseString);
        }
    }
}
