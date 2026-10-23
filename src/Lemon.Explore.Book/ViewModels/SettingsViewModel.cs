using System.ComponentModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.Infrastructures.Theming;
using Lemon.Explore.Book.Updates;

namespace Lemon.Explore.Book.ViewModels;

/// <summary>
/// Everything the reader can set, in one place (the settings button at the top right): language, theme and colour,
/// observer location, narration, page turning and reading progress. Each choice is saved on this device as soon as it
/// is made.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly INarrationService _narration;
    private readonly ReadingProgress _progress;
    private bool _animatesTurns;
    private BookThemeMode _themeMode;
    private AccentColor _accent;

    public SettingsViewModel(BookServices services, UpdateViewModel? update = null)
    {
        Update = update;
        _store = services.Settings;
        _narration = services.Narration;
        _animatesTurns = _store.Get(SettingKeys.AnimatesTurns) != bool.FalseString;
        _progress = services.Progress;
        _themeMode = BookTheme.LoadMode(_store);
        _accent = BookTheme.LoadAccent(_store);
        Accents = BookTheme.Accents.Select(a => new AccentChoice(a, this)).ToList();
        Speeds = NarrationSettings.Speeds.Select(s => new SpeedChoice(s, this)).ToList();
        _progress.Changed += (_, _) => OnPropertyChanged(nameof(ProgressSummary));
        Location = new ExploreLocationPickerViewModel(services.Location);
        _narration.SettingsChanged += (_, _) => RaiseNarration();
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    /// <summary>The update address and checking (desktop); null when the settings are built alone (tests).</summary>
    public UpdateViewModel? Update { get; }

    /// <summary>e.g. "Version 0.2.0".</summary>
    public string VersionText => LocalizationService.Instance.Format("Update_Version", Update?.CurrentVersion ?? string.Empty);

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
        OnPropertyChanged(nameof(ProgressSummary));
        OnPropertyChanged(nameof(VersionText));
        foreach (var accent in Accents) accent.RefreshName();
    }

    #endregion

    #region theme and colour

    public bool IsLightTheme
    {
        get => _themeMode == BookThemeMode.Light;
        set
        {
            if (value) SetThemeMode(BookThemeMode.Light);
        }
    }

    public bool IsDarkTheme
    {
        get => _themeMode == BookThemeMode.Dark;
        set
        {
            if (value) SetThemeMode(BookThemeMode.Dark);
        }
    }

    /// <summary>Light or dark as the system is set.</summary>
    public bool IsSystemTheme
    {
        get => _themeMode == BookThemeMode.System;
        set
        {
            if (value) SetThemeMode(BookThemeMode.System);
        }
    }

    public BookThemeMode ThemeMode => _themeMode;

    /// <summary>The accent colours to choose from, as swatches.</summary>
    public IReadOnlyList<AccentChoice> Accents { get; }

    public AccentColor Accent
    {
        get => _accent;
        set
        {
            if (_accent == value) return;
            _accent = value;
            _store.Set(BookTheme.AccentSetting, value.Id);
            ApplyTheme();
            OnPropertyChanged();
            foreach (var accent in Accents) accent.RefreshSelected();
        }
    }

    private void SetThemeMode(BookThemeMode mode)
    {
        if (_themeMode == mode) return;
        _themeMode = mode;
        _store.Set(BookTheme.ThemeSetting, mode.ToString());
        ApplyTheme();
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(ThemeMode));
    }

    private void ApplyTheme() => BookTheme.Apply(Application.Current, _themeMode, _accent);

    #endregion

    #region reading progress

    /// <summary>e.g. "5 chapters opened, 2 finished".</summary>
    public string ProgressSummary
    {
        get
        {
            var (opened, complete) = _progress.Summary();
            return LocalizationService.Instance.Format("Settings_Progress_Summary", opened, complete);
        }
    }

    /// <summary>Forgets which chapters were opened and which walkthroughs were heard (bookmarks stay).</summary>
    [RelayCommand]
    private void ClearProgress() => _progress.Clear();

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

    /// <summary>How fast the narration reads; the chips offer <see cref="NarrationSettings.Speeds"/>.</summary>
    public double Speed
    {
        get => _narration.Settings.Speed;
        set => _narration.Update(_narration.Settings with { Speed = value });
    }

    public IReadOnlyList<SpeedChoice> Speeds { get; }

    private void RaiseNarration()
    {
        OnPropertyChanged(nameof(VoiceEnabled));
        OnPropertyChanged(nameof(VolumePercent));
        OnPropertyChanged(nameof(ShowSubtitles));
        OnPropertyChanged(nameof(SpeakOnClick));
        OnPropertyChanged(nameof(Speed));
        foreach (var speed in Speeds) speed.RefreshSelected();
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

/// <summary>One accent colour as a swatch in the settings.</summary>
/// <summary>One of the narration speed chips: 0.75×, 1×, 1.25×, 1.5×.</summary>
public sealed class SpeedChoice(double speed, SettingsViewModel settings) : ObservableObject
{
    public double Speed { get; } = speed;

    public string Label { get; } = speed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "×";

    public bool IsSelected
    {
        get => Math.Abs(settings.Speed - Speed) < 0.001;
        set
        {
            if (value) settings.Speed = Speed;
        }
    }

    internal void RefreshSelected() => OnPropertyChanged(nameof(IsSelected));
}

public sealed class AccentChoice(AccentColor color, SettingsViewModel settings) : ObservableObject
{
    public AccentColor Color { get; } = color;

    public Avalonia.Media.IBrush Swatch { get; } = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(color.OnLight);

    public string Name => LocalizationService.Instance.GetString(Color.NameKey);

    public bool IsSelected
    {
        get => settings.Accent == Color;
        set
        {
            if (value) settings.Accent = Color;
        }
    }

    internal void RefreshSelected() => OnPropertyChanged(nameof(IsSelected));

    internal void RefreshName() => OnPropertyChanged(nameof(Name));
}
