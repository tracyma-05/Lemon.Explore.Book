using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.SolarSystem;

/// <summary>
/// The Solar System chapter: playback (date, speed, direction), display toggles and the body shown in the detail
/// panel. The scene itself (camera, animation) lives in <see cref="SolarSystemCanvas"/>. <see cref="Lesson"/> is the
/// spoken tour, and the body chosen (<see cref="Card"/>) is read aloud. One instance serves both pages of the spread.
/// </summary>
public partial class SolarSystemViewModel : ObservableObject, IChapterContent, IDisposable
{
    /// <summary>Playback speeds in simulated days per real second, with the label that names each one.</summary>
    private static readonly (double Days, string Key)[] Speeds =
    [
        (1 / 24.0, "SolarSystem_Speed_Hour"),
        (1, "SolarSystem_Speed_Day"),
        (7, "SolarSystem_Speed_Week"),
        (30, "SolarSystem_Speed_Month"),
        (365.25, "SolarSystem_Speed_Year"),
    ];

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private DateTime _shownDate;
    private int _views;

    public SolarSystemViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => SolarSystemLessons.Tour(this));
        Card = new ObjectCard(narration, Lesson, BodyTitleKey, BodyClipKey);
        _shownDate = SimulationTime.ToLocalTime().Date;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    /// <summary>A view of the chapter came on screen.</summary>
    public void ViewAttached() => _views++;

    /// <summary>The last view of the chapter left the screen: the tour and any body being read out stop.</summary>
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

    public IReadOnlyList<CelestialBody> Bodies => SolarSystemCatalog.Bodies;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailOpen), nameof(Facts), nameof(ChipSelection), nameof(SystemBody),
        nameof(Satellites), nameof(HasSatellites), nameof(SatelliteSelection), nameof(IsMoonSelected), nameof(BackToText),
        nameof(MoonsOfText), nameof(HasNoSelection))]
    private CelestialBody? _selectedBody;

    public bool IsDetailOpen => SelectedBody is not null;

    public bool HasNoSelection => SelectedBody is null;

    /// <summary>The link from a moon back to its planet, e.g. "Back to Jupiter".</summary>
    public string BackToText => _localization.Format("SolarSystem_BackTo", SelectedBody?.Parent?.Name);

    /// <summary>The heading over the moon chips, e.g. "Moons of Saturn".</summary>
    public string MoonsOfText => _localization.Format("SolarSystem_MoonsOf", SystemBody?.Name);

    partial void OnSelectedBodyChanged(CelestialBody? value)
    {
        // A body the viewer picks is read aloud; one the tour flies to is not, the tour is speaking.
        if (Lesson.IsApplying) Card.ShowQuietly(value?.Id);
        else Card.Id = value?.Id;
    }

    public bool IsMoonSelected => SelectedBody?.IsMoon == true;

    /// <summary>The planet whose system is shown: the selected planet, or the planet of the selected moon.</summary>
    public CelestialBody? SystemBody => SelectedBody?.Parent ?? SelectedBody;

    /// <summary>Major moons of <see cref="SystemBody"/>, listed in the detail panel.</summary>
    public IReadOnlyList<CelestialBody> Satellites => SystemBody?.Satellites ?? [];

    public bool HasSatellites => Satellites.Count > 0;

    /// <summary>
    /// Selection of the Sun/planet chips: a moon highlights its planet. Null coming back from the list (when it
    /// cannot show the selection) is ignored, so it never clears the selected body.
    /// </summary>
    public CelestialBody? ChipSelection
    {
        get => SystemBody;
        set
        {
            if (value is not null && value != SystemBody) SelectedBody = value;
        }
    }

    /// <summary>Selection of the moon chips in the detail panel; null while a planet is selected.</summary>
    public CelestialBody? SatelliteSelection
    {
        get => IsMoonSelected ? SelectedBody : null;
        set
        {
            if (value is not null) SelectedBody = value;
        }
    }

    [ObservableProperty]
    private DateTime _simulationTime = DateTime.UtcNow;

    /// <summary>Simulated date in the interface language's long date format.</summary>
    public string DateText => _shownDate.ToString("D", _localization.CurrentCulture);

    [ObservableProperty]
    private bool _isPlaying = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DaysPerSecond), nameof(SpeedText))]
    private int _speedIndex = 2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DaysPerSecond))]
    private bool _isReversed;

    [ObservableProperty]
    private bool _showLabels = true;

    [ObservableProperty]
    private bool _showOrbits = true;

    [ObservableProperty]
    private bool _isRealScale;

    public int MaxSpeedIndex => Speeds.Length - 1;

    public double DaysPerSecond => Speeds[SpeedIndex].Days * (IsReversed ? -1 : 1);

    public string SpeedText => _localization.GetString(Speeds[SpeedIndex].Key);

    /// <summary>Key figures of the selected body, formatted for the interface language.</summary>
    public IReadOnlyList<BodyFact> Facts => SelectedBody is null ? [] : BuildFacts(SelectedBody);

    partial void OnSimulationTimeChanged(DateTime value)
    {
        // The scene moves the time every frame; the date label only needs to change when the day does.
        var date = value.ToLocalTime().Date;
        if (date == _shownDate) return;
        _shownDate = date;
        OnPropertyChanged(nameof(DateText));
    }

    [RelayCommand]
    private void TogglePlay() => IsPlaying = !IsPlaying;

    #region tour and spoken bodies

    public LessonPlayer Lesson { get; }

    /// <summary>The body chosen, read aloud; the detail panel shows its text.</summary>
    public ObjectCard Card { get; }

    /// <summary>The scene reports a drag or click before it changes anything: the tour being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    /// <summary>The body already chosen was clicked again: say it again.</summary>
    [RelayCommand]
    private void Reselected(CelestialBody? body)
    {
        if (body is not null && body == SelectedBody) Card.ListenCommand.Execute(null);
    }

    /// <summary>Asks the view to show the whole system again (the camera is the view's).</summary>
    internal event EventHandler? ViewResetRequested;

    /// <summary>For the tour: no body chosen, the whole system in view.</summary>
    internal void ResetView()
    {
        SelectedBody = null;
        ViewResetRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A body's name, as the detail panel and the chips show it.</summary>
    internal static string? BodyTitleKey(string? id) => FindBody(id) is { } body ? $"SolarSystem_{body.Id}_Name" : null;

    /// <summary>The spoken explanation of a body.</summary>
    internal static string? BodyClipKey(string? id) => FindBody(id) is { } body ? "Narr_SolarSystem_Obj_" + body.Id : null;

    private static CelestialBody? FindBody(string? id) => id is null ? null : SolarSystemCatalog.AllBodies.FirstOrDefault(b => b.Id == id);

    #endregion

    [RelayCommand]
    private void Today() => SimulationTime = DateTime.UtcNow;

    [RelayCommand]
    private void SelectBody(CelestialBody? body) => SelectedBody = body;

    [RelayCommand]
    private void CloseDetail() => SelectedBody = null;

    [RelayCommand]
    private void SelectParent() => SelectedBody = SelectedBody?.Parent ?? SelectedBody;

    [RelayCommand]
    private void NextBody() => Step(1);

    [RelayCommand]
    private void PreviousBody() => Step(-1);

    /// <summary>Previous / next body: among the Sun and planets, or among the moons of the same planet.</summary>
    private void Step(int direction)
    {
        var list = SelectedBody?.Parent?.Satellites ?? Bodies;
        var index = SelectedBody is null ? -1 : IndexOf(list, SelectedBody);
        var count = list.Count;
        SelectedBody = list[(((index + direction) % count) + count) % count];
    }

    private static int IndexOf(IReadOnlyList<CelestialBody> list, CelestialBody body)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == body) return i;
        }

        return -1;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(SpeedText));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(BackToText));
        OnPropertyChanged(nameof(MoonsOfText));
    }

    private List<BodyFact> BuildFacts(CelestialBody body)
    {
        var culture = _localization.CurrentCulture;
        var facts = new List<BodyFact>();

        void Add(string labelKey, string value) => facts.Add(new BodyFact(_localization.GetString(labelKey), value));

        if (body.IsStar)
        {
            Add("SolarSystem_Fact_Type", _localization.GetString("SolarSystem_Sun_Type"));
            Add("SolarSystem_Fact_Age", _localization.GetString("SolarSystem_Sun_Age"));
        }

        if (body.Parent is { } parent)
        {
            Add("SolarSystem_Fact_Parent", parent.Name);
        }

        Add("SolarSystem_Fact_Diameter", _localization.Format("SolarSystem_Value_Km", body.DiameterKm));
        Add("SolarSystem_Fact_Mass", _localization.Format("SolarSystem_Value_Mass", body.MassMantissa, Superscript(body.MassExponent)));
        if (body.MassEarths is { } earths)
        {
            Add("SolarSystem_Fact_MassEarths", FormatQuantity(earths, culture));
        }

        if (body.MoonOrbit is { } moonOrbit)
        {
            Add("SolarSystem_Fact_PlanetDistance", _localization.Format("SolarSystem_Value_Km", moonOrbit.DistanceKm));
            var period = moonOrbit.PeriodDays < 1
                ? _localization.Format("SolarSystem_Value_Hours", moonOrbit.PeriodDays * 24)
                : _localization.Format("SolarSystem_Value_Days", moonOrbit.PeriodDays);
            if (moonOrbit.Retrograde)
            {
                period += _localization.GetString("SolarSystem_Value_RetrogradeOrbit");
            }

            Add("SolarSystem_Fact_OrbitalPeriod", period);
            Add("SolarSystem_Fact_Rotation", _localization.GetString("SolarSystem_Value_Synchronous"));
        }

        if (body.DistanceMillionKm is { } millionKm && body.DistanceAu is { } au)
        {
            // Each language picks its own unit (million km, 万千米); the resource says how many km one unit is.
            var unitKm = double.Parse(_localization.GetString("SolarSystem_Value_DistanceUnitKm"), CultureInfo.InvariantCulture);
            Add("SolarSystem_Fact_Distance", _localization.Format("SolarSystem_Value_Distance", millionKm * 1_000_000 / unitKm, au));
        }

        if (body.OrbitalPeriodDays is { } days)
        {
            Add("SolarSystem_Fact_OrbitalPeriod", days < 1000
                ? _localization.Format("SolarSystem_Value_Days", days)
                : _localization.Format("SolarSystem_Value_Years", days / 365.25));
        }

        if (!body.IsTidallyLocked)
        {
            var hours = Math.Abs(body.RotationHours);
            var rotation = hours < 100
                ? _localization.Format("SolarSystem_Value_Hours", hours)
                : _localization.Format("SolarSystem_Value_Days", hours / 24);
            if (body.RotationHours < 0)
            {
                rotation += _localization.GetString("SolarSystem_Value_Retrograde");
            }

            Add("SolarSystem_Fact_Rotation", rotation);
            Add("SolarSystem_Fact_AxialTilt", body.AxialTiltDegrees.ToString("0.##", culture) + "°");
        }

        if (body.Moons is { } moons)
        {
            Add("SolarSystem_Fact_Moons", moons.ToString("N0", culture));
        }

        if (body.IsStar)
        {
            Add("SolarSystem_Fact_SurfaceTemperature", _localization.Format("SolarSystem_Value_Celsius", body.MeanTemperatureC));
            Add("SolarSystem_Fact_CoreTemperature", _localization.GetString("SolarSystem_Sun_CoreTemperature"));
        }
        else if (body.MeanTemperatureC is { } temperature)
        {
            Add("SolarSystem_Fact_Temperature", _localization.Format("SolarSystem_Value_Celsius", temperature));
        }

        // Small moons have gravity in the thousandths of a m/s²; keep enough digits to show it.
        var gravityFormat = body.SurfaceGravity >= 1 ? "0.#" : "0.0##";
        Add("SolarSystem_Fact_Gravity", body.SurfaceGravity.ToString(gravityFormat, culture) + " m/s²");
        return facts;
    }

    private static string FormatQuantity(double value, CultureInfo culture) =>
        value.ToString(value >= 100 ? "N0" : value >= 10 ? "0.#" : "0.###", culture);

    private static string Superscript(int value)
    {
        const string digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var text = value.ToString(CultureInfo.InvariantCulture);
        return string.Concat(text.Select(c => c == '-' ? '⁻' : digits[c - '0']));
    }

    public void Dispose()
    {
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        Lesson.Dispose();
        Card.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>One row of the detail panel's key figures.</summary>
public sealed record BodyFact(string Label, string Value);
