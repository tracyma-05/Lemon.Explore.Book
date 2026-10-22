using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.BlueSky;

/// <summary>A row of the "what is happening" card: a name and its value.</summary>
public sealed record SkyStat(string Name, string Value);

/// <summary>
/// The blue sky page: which view is shown, the time of day (which sets the Sun's altitude and so how much air its light
/// crosses), how hazy the air is, and what that does to the light. The diagram (<see cref="SkyDiagram"/>) moves
/// <see cref="Hour"/>; everything else follows it. <see cref="Lesson"/> is the spoken walkthrough of the current view, and
/// clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class BlueSkyViewModel : ObservableObject, IChapterContent, IDisposable
{
    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;
    private readonly IExploreLocationService _location;

    public IExploreLocationService Location => _location;

    public BlueSkyViewModel(IExploreLocationService location, INarrationService narration)
    {
        _location = location;
        Lesson = new LessonPlayer(narration, () => BlueSkyLessons.For(this));
        Card = new ObjectCard(narration, Lesson, SkyDiagram.ObjectTitleKey, SkyDiagram.ObjectClipKey);
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        location.Changed += OnLocationChanged;
        Refresh();
    }

    /// <summary>A view of the chapter came on screen.</summary>
    public void ViewAttached() => _views++;

    /// <summary>The last view of the chapter left the screen: the walkthrough and any explanation being read stop.</summary>
    public void ViewDetached()
    {
        _views = Math.Max(0, _views - 1);
        if (_views > 0)
        {
            return;
        }

        Lesson.Stop();
        Card.Close();
    }

    /// <summary>
    /// The day's latitude: the shared observer location's, kept within 75° so the equinox Sun still climbs well clear
    /// of the horizon.
    /// </summary>
    public double Latitude => Math.Clamp(_location.Current.Latitude, -75, 75);

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Latitude));
        OnPropertyChanged(nameof(Altitude));
        OnPropertyChanged(nameof(ScaleNote));
        Refresh();
    }

    #region mode and options

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSky), nameof(IsScattering), nameof(IsPath), nameof(Subtitle), nameof(ScaleNote),
        nameof(DragHint), nameof(Facts))]
    private SkyMode _mode;

    public bool IsSky
    {
        get => Mode == SkyMode.Sky;
        set
        {
            if (value) Mode = SkyMode.Sky;
        }
    }

    public bool IsScattering
    {
        get => Mode == SkyMode.Scattering;
        set
        {
            if (value) Mode = SkyMode.Scattering;
        }
    }

    public bool IsPath
    {
        get => Mode == SkyMode.Path;
        set
        {
            if (value) Mode = SkyMode.Path;
        }
    }

    public string Subtitle => _localization.GetString(Mode switch
    {
        SkyMode.Scattering => "Sky_Scattering_Subtitle",
        SkyMode.Path => "Sky_Path_Subtitle",
        _ => "Sky_Sky_Subtitle",
    });

    public string ScaleNote => Mode switch
    {
        SkyMode.Scattering => _localization.GetString("Sky_Scattering_Note"),
        SkyMode.Path => _localization.GetString("Sky_Path_Note"),
        _ => _localization.Format("Sky_Sky_Note", _location.Current.Name, ExplorePlace.LatitudeText(Latitude)),
    };

    public string DragHint => _localization.GetString(Mode switch
    {
        SkyMode.Scattering => "Sky_DragHint_Scattering",
        SkyMode.Path => "Sky_DragHint_Path",
        _ => "Sky_DragHint_Sky",
    });

    /// <summary>Sky mode: face away from the Sun.</summary>
    [ObservableProperty]
    private bool _lookAway;

    partial void OnModeChanged(SkyMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
    }

    #endregion

    #region walkthrough and clicked things

    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports a drag or click before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    #endregion

    #region time and haze

    [ObservableProperty]
    private bool _isPlaying = true;

    /// <summary>Local solar time on an equinox day at <see cref="Latitude"/>; starts on a clear afternoon heading for sunset.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Altitude))]
    private double _hour = 16.5;

    public double FirstHour => SkyOptics.FirstHour;

    public double LastHour => SkyOptics.LastHour;

    public double Altitude => SkyOptics.SunAltitude(Hour, Latitude);

    /// <summary>Haze, dust and droplets in the air, 0 (very clean) to 1 (thick haze).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HazeText))]
    private double _haze = 0.3;

    public string HazeText => _localization.GetString(Haze switch
    {
        < 0.15 => "Sky_Haze_Clean",
        < 0.55 => "Sky_Haze_Normal",
        _ => "Sky_Haze_Hazy",
    });

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    /// <summary>Jumps to a moment of the day, by name, and pauses.</summary>
    [RelayCommand]
    private void SetTime(string? moment)
    {
        Hour = moment switch
        {
            "Noon" => 12,
            "Afternoon" => 15.5,
            "Golden" => SkyOptics.HourAt(4, true, Latitude),
            "Sunset" => 18,
            "Twilight" => SkyOptics.HourAt(-4, true, Latitude),
            "Sunrise" => 6,
            _ => Hour,
        };
        IsPlaying = false;
    }

    [RelayCommand]
    private void SetHaze(string? haze)
    {
        if (double.TryParse(haze, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) Haze = value;
    }

    partial void OnHourChanged(double value) => Refresh();

    partial void OnHazeChanged(double value) => Refresh();

    #endregion

    #region what is happening

    private string _clockText = string.Empty;
    private string _phaseTitle = string.Empty;
    private string _phaseDescription = string.Empty;
    private IBrush _phaseBrush = Brushes.White;
    private IReadOnlyList<SkyStat> _stats = [];

    /// <summary>Beside the timeline: the time and the Sun's altitude.</summary>
    public string ClockText
    {
        get => _clockText;
        private set => SetProperty(ref _clockText, value);
    }

    public string PhaseTitle
    {
        get => _phaseTitle;
        private set => SetProperty(ref _phaseTitle, value);
    }

    public string PhaseDescription
    {
        get => _phaseDescription;
        private set => SetProperty(ref _phaseDescription, value);
    }

    public IBrush PhaseBrush
    {
        get => _phaseBrush;
        private set => SetProperty(ref _phaseBrush, value);
    }

    public IReadOnlyList<SkyStat> Stats
    {
        get => _stats;
        private set => SetProperty(ref _stats, value);
    }

    public IReadOnlyList<string> Facts => (Mode switch
    {
        SkyMode.Scattering => new[] { "Sky_Scattering_Fact1", "Sky_Scattering_Fact2", "Sky_Scattering_Fact3", "Sky_Scattering_Fact4", "Sky_Scattering_Fact5" },
        SkyMode.Path => ["Sky_Path_Fact1", "Sky_Path_Fact2", "Sky_Path_Fact3", "Sky_Path_Fact4", "Sky_Path_Fact5"],
        _ => ["Sky_Sky_Fact1", "Sky_Sky_Fact2", "Sky_Sky_Fact3", "Sky_Sky_Fact4", "Sky_Sky_Fact5"],
    }).Select(_localization.GetString).ToList();

    private void Refresh()
    {
        var altitude = Altitude;
        ClockText = _localization.Format("Sky_Clock", SkyClock(Hour), SkyDiagram.Format(altitude));

        var phase = SkyOptics.PhaseAt(Hour, Latitude);
        PhaseTitle = _localization.GetString($"Sky_Phase_{phase}_Title");
        PhaseDescription = _localization.GetString($"Sky_Phase_{phase}_Description");
        var brush = new ImmutableSolidColorBrush(PhaseColour(phase));
        PhaseBrush = brush;

        var stats = new List<SkyStat>
        {
            new(_localization.GetString("Sky_Stat_Time"), SkyClock(Hour)),
            new(_localization.GetString("Sky_Stat_Altitude"), SkyDiagram.Format(altitude) + "°"),
        };

        if (altitude > 0)
        {
            var airMass = SkyOptics.AirMass(altitude);
            stats.Add(new SkyStat(_localization.GetString("Sky_Stat_AirMass"),
                _localization.Format("Sky_Stat_AirMass_Value", airMass.ToString(airMass < 10 ? "0.0" : "0", CultureInfo.InvariantCulture))));
            stats.Add(new SkyStat(_localization.GetString("Sky_Stat_Blue"), Percent(SkyOptics.TransmittanceAt(SkyOptics.BlueNm, altitude, Haze))));
            stats.Add(new SkyStat(_localization.GetString("Sky_Stat_Red"), Percent(SkyOptics.TransmittanceAt(SkyOptics.RedNm, altitude, Haze))));
            stats.Add(new SkyStat(_localization.GetString("Sky_Stat_SunColour"), _localization.GetString(SunColourKey(SkyOptics.BlueToRed(altitude, Haze)))));
        }
        else
        {
            stats.Add(new SkyStat(_localization.GetString("Sky_Stat_AirMass"), _localization.GetString("Sky_Stat_SunDown")));
        }

        stats.Add(new SkyStat(_localization.GetString("Sky_Stat_Haze"), HazeText));
        Stats = stats;
    }

    private static string Percent(double share) =>
        (share * 100).ToString(share < 0.1 ? "0.0" : "0", CultureInfo.InvariantCulture) + "%";

    /// <summary>The Sun's colour by how much of its blue comes through against its red.</summary>
    private static string SunColourKey(double blueToRed) => blueToRed switch
    {
        > 0.8 => "Sky_SunColour_White",
        > 0.6 => "Sky_SunColour_YellowWhite",
        > 0.35 => "Sky_SunColour_Yellow",
        > 0.1 => "Sky_SunColour_Orange",
        _ => "Sky_SunColour_Red",
    };

    private static Color PhaseColour(SkyPhase phase) => phase switch
    {
        SkyPhase.Day => Color.FromRgb(0x7F, 0xB8, 0xFF),
        SkyPhase.GoldenHour => Color.FromRgb(0xFF, 0xC8, 0x61),
        SkyPhase.Sunrise or SkyPhase.Sunset => Color.FromRgb(0xFF, 0x8A, 0x4C),
        SkyPhase.Twilight => Color.FromRgb(0xB3, 0x9D, 0xFF),
        SkyPhase.NauticalTwilight => Color.FromRgb(0x7C, 0x8C, 0xE0),
        _ => Color.FromRgb(0x9A, 0xA5, 0xB5),
    };

    /// <summary>Local solar time as hours and minutes, e.g. 17:24.</summary>
    private static string SkyClock(double hour)
    {
        var minutes = (int)Math.Round(hour * 60);
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(DragHint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(HazeText));
        Refresh();
    }

    public void Dispose()
    {
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        _location.Changed -= OnLocationChanged;
        Lesson.Dispose();
        Card.Dispose();
        GC.SuppressFinalize(this);
    }
}
