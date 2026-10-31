using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Book.Shell.Controls.Explore;
using Lemon.Book.Shell.Explore.Lessons;
using Lemon.Book.Shell.Explore.Narration;
using Lemon.Book.Shell.Infrastructures.Localization;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.PeriodicTable;

/// <summary>An element in the side panel's picker: "1 · H · Hydrogen".</summary>
public sealed class ElementOption(Element element) : ObservableObject
{
    public Element Element { get; } = element;

    public string Label => $"{Element.Number} · {Element.Symbol} · {LocalizationService.Instance.GetString(Element.NameKey)}";

    /// <summary>The language changed.</summary>
    public void Refresh() => OnPropertyChanged(nameof(Label));

    public override string ToString() => Label;
}

/// <summary>
/// The atoms page: which view is shown; the protons, neutrons and electrons of the atom being built; the element picked
/// on the table and what the table is coloured by; and what is happening. The diagram (<see cref="AtomDiagram"/>) adds and
/// takes particles and picks elements, and reports back. <see cref="Lesson"/> is the spoken walkthrough of the current
/// view, and clicking a thing in the diagram (<see cref="Card"/>) explains it.
/// </summary>
public partial class AtomViewModel : ObservableObject, IChapterContent, IDisposable
{
    private static readonly IBrush GoldBrush = Frozen(0xFF, 0xD3, 0x6B);
    private static readonly IBrush CyanBrush = Frozen(0x6F, 0xD8, 0xFF);
    private static readonly IBrush GreenBrush = Frozen(0x7C, 0xFF, 0x9E);
    private static readonly IBrush RedBrush = Frozen(0xFF, 0x5A, 0x5F);
    private static readonly IBrush OrangeBrush = Frozen(0xFF, 0x9A, 0x4A);
    private static readonly IBrush VioletBrush = Frozen(0xB0, 0x8C, 0xFF);

    private readonly ILocalizationService _localization = LocalizationService.Instance;
    private int _views;

    public AtomViewModel(INarrationService narration)
    {
        Lesson = new LessonPlayer(narration, () => AtomLessons.For(this));
        Card = new ObjectCard(narration, Lesson, AtomDiagram.ObjectTitleKey, AtomDiagram.ObjectClipKey);
        ColouringChoice.PropertyChanged += OnColouringChosen;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        ElementOptions = ElementData.Elements.Select(e => new ElementOption(e)).ToList();
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

    #region mode

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBuild), nameof(IsTable), nameof(Subtitle), nameof(ScaleNote), nameof(Hint), nameof(Facts),
        nameof(ChartCaption), nameof(ChartNote))]
    private AtomMode _mode;

    public bool IsBuild
    {
        get => Mode == AtomMode.Build;
        set
        {
            if (value) Mode = AtomMode.Build;
        }
    }

    public bool IsTable
    {
        get => Mode == AtomMode.Table;
        set
        {
            if (value) Mode = AtomMode.Table;
        }
    }

    public string Subtitle => _localization.GetString($"Atom_{Mode}_Subtitle");

    public string ScaleNote => _localization.GetString($"Atom_{Mode}_Note");

    public string Hint => _localization.GetString($"Atom_Hint_{Mode}");

    public string ChartCaption => _localization.GetString($"Atom_Chart_{Mode}_Caption");

    public string ChartNote => _localization.GetString($"Atom_Chart_{Mode}_Note");

    public IReadOnlyList<string> Facts => Enumerable.Range(1, 5)
        .Select(i => _localization.GetString($"Atom_{Mode}_Fact{i}"))
        .ToList();

    partial void OnModeChanged(AtomMode value)
    {
        // Switching views by hand ends a walkthrough of the old view; a walkthrough switching views itself goes on.
        if (!Lesson.IsApplying) Lesson.Stop();
        Card.Id = null;
        Refresh();
    }

    /// <summary>The spoken walkthrough of the current view.</summary>
    public LessonPlayer Lesson { get; }

    /// <summary>The thing clicked in the diagram and its explanation.</summary>
    public ObjectCard Card { get; }

    /// <summary>The diagram reports a click before it changes anything: a walkthrough being spoken pauses.</summary>
    [RelayCommand]
    private void UserInteracted() => Lesson.UserInteracted();

    [ObservableProperty]
    private bool _showLabels = true;

    #endregion

    #region the atom

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Build))]
    private int _protons = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Build))]
    private int _neutrons;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Build))]
    private int _electrons = 1;

    public AtomBuild Build => new(Protons, Neutrons, Electrons);

    partial void OnProtonsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, AtomModel.MaxProtons);
        if (clamped != value)
        {
            Protons = clamped;
            return;
        }

        // An atom holds at most a few electrons more than it has protons.
        if (Electrons > AtomModel.MaxElectrons(value)) Electrons = AtomModel.MaxElectrons(value);
        Refresh();
    }

    partial void OnNeutronsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, AtomModel.MaxNeutrons);
        if (clamped != value)
        {
            Neutrons = clamped;
            return;
        }

        Refresh();
    }

    partial void OnElectronsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, AtomModel.MaxElectrons(Protons));
        if (clamped != value)
        {
            Electrons = clamped;
            return;
        }

        Refresh();
    }

    /// <summary>Sets the whole atom at once (presets, the chart, the walkthrough).</summary>
    public void SetAtom(int protons, int neutrons, int electrons)
    {
        Protons = protons;
        Neutrons = neutrons;
        Electrons = electrons;
    }

    [RelayCommand]
    private void AddProton() => Protons = Math.Min(AtomModel.MaxProtons, Protons + 1);

    [RelayCommand]
    private void RemoveProton() => Protons = Math.Max(0, Protons - 1);

    [RelayCommand]
    private void AddNeutron() => Neutrons = Math.Min(AtomModel.MaxNeutrons, Neutrons + 1);

    [RelayCommand]
    private void RemoveNeutron() => Neutrons = Math.Max(0, Neutrons - 1);

    [RelayCommand]
    private void AddElectron() => Electrons = Math.Min(AtomModel.MaxElectrons(Protons), Electrons + 1);

    [RelayCommand]
    private void RemoveElectron() => Electrons = Math.Max(0, Electrons - 1);

    /// <summary>Nothing at all: no protons, neutrons or electrons.</summary>
    [RelayCommand]
    public void ClearAtom() => SetAtom(0, 0, 0);

    /// <summary>A ready-made atom from the side panel, by name.</summary>
    [RelayCommand]
    private void Preset(string name)
    {
        var (p, n, e) = name switch
        {
            "Helium" => (2, 2, 2),
            "Carbon12" => (6, 6, 6),
            "Carbon14" => (6, 8, 6),
            "Oxygen" => (8, 8, 8),
            "Sodium" => (11, 12, 10),
            "Chloride" => (17, 18, 18),
            "Iron" => (26, 30, 26),
            _ => (1, 0, 1),
        };
        SetAtom(p, n, e);
    }

    /// <summary>A nucleus clicked on the chart: that atom, neutral.</summary>
    [RelayCommand]
    private void PickNucleus(Nucleus? nucleus)
    {
        if (nucleus is null) return;
        SetAtom(nucleus.Protons, nucleus.Neutrons, nucleus.Protons);
    }

    #endregion

    #region the table

    /// <summary>The element picked on the table, by atomic number.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Element), nameof(ElementIndex))]
    private int _elementZ = 1;

    [ObservableProperty]
    private TableColouring _colouring = TableColouring.Category;

    public OscillationChoice ColouringChoice { get; } = new(nameof(TableColouring.Category));

    public Element Element => ElementData.Of(ElementZ);

    /// <summary>The element list's index (hydrogen is 0), for the side panel's picker.</summary>
    public int ElementIndex
    {
        get => ElementZ - 1;
        set
        {
            if (value >= 0) ElementZ = value + 1;
        }
    }

    /// <summary>One per element, for the side panel's picker. The list stays; only the words change language.</summary>
    public IReadOnlyList<ElementOption> ElementOptions { get; }

    partial void OnElementZChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, ElementData.Elements.Count);
        if (clamped != value)
        {
            ElementZ = clamped;
            return;
        }

        Refresh();
    }

    partial void OnColouringChanged(TableColouring value) => ColouringChoice.Selected = value.ToString();

    private void OnColouringChosen(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OscillationChoice.Selected) && Enum.TryParse<TableColouring>(ColouringChoice.Selected, out var colouring)) Colouring = colouring;
    }

    [RelayCommand]
    private void PreviousElement() => ElementZ = ElementZ <= 1 ? ElementData.Elements.Count : ElementZ - 1;

    [RelayCommand]
    private void NextElement() => ElementZ = ElementZ >= ElementData.Elements.Count ? 1 : ElementZ + 1;

    #endregion

    #region what is happening

    private string _phaseTitle = string.Empty;
    private string _phaseDescription = string.Empty;
    private IBrush _phaseBrush = GoldBrush;
    private IReadOnlyList<OscillationStat> _stats = [];

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

    public IReadOnlyList<OscillationStat> Stats
    {
        get => _stats;
        private set => SetProperty(ref _stats, value);
    }

    private void Refresh()
    {
        if (Mode == AtomMode.Table) RefreshTable();
        else RefreshBuild();
    }

    private void RefreshBuild()
    {
        var atom = Build;
        var state = AtomModel.StateName(atom);
        PhaseTitle = BuildTitle(atom);
        PhaseDescription = _localization.GetString($"Atom_Build_{state}_Text");
        PhaseBrush = state switch
        {
            "Nothing" or "NoProton" => CyanBrush,
            _ when !atom.IsStable => OrangeBrush,
            "Positive_Stable" => RedBrush,
            "Negative_Stable" => VioletBrush,
            _ => GreenBrush,
        };

        var element = atom.Element;
        Stats =
        [
            new(_localization.GetString("Atom_Stat_Element"), element is null
                ? _localization.GetString("Atom_Value_NoElement")
                : _localization.Format("Atom_Value_ElementName", _localization.GetString(element.NameKey), element.Symbol)),
            new(_localization.GetString("Atom_Stat_Protons"), Number(atom.Protons)),
            new(_localization.GetString("Atom_Stat_Neutrons"), Number(atom.Neutrons)),
            new(_localization.GetString("Atom_Stat_Electrons"), Number(atom.Electrons)),
            new(_localization.GetString("Atom_Stat_MassNumber"), _localization.Format("Atom_Value_MassNumber", Number(atom.MassNumber), Number(atom.Protons), Number(atom.Neutrons))),
            new(_localization.GetString("Atom_Stat_Charge"), AtomModel.SignedCharge(atom.Charge)),
            new(_localization.GetString("Atom_Stat_Shells"), atom.Electrons == 0 ? "—" : ShellsText(atom.Shells)),
            new(_localization.GetString("Atom_Stat_Nucleus"), atom.Protons + atom.Neutrons == 0
                ? "—"
                : _localization.GetString(atom.IsStable ? "Atom_Value_Stable" : "Atom_Value_Radioactive")),
        ];
    }

    /// <summary>The card's title for a build: "Carbon-14", "Sodium ion (Na+)", "Chloride ion (Cl−)", or what is missing.</summary>
    internal string BuildTitle(AtomBuild atom)
    {
        if (atom.Element is not { } element) return _localization.GetString($"Atom_Build_{AtomModel.StateName(atom)}_Title");
        var name = _localization.GetString(element.NameKey);
        var symbol = element.Symbol + AtomModel.ChargeSuffix(atom.Charge);
        return atom.Charge switch
        {
            0 => _localization.Format("Atom_Isotope", name, Number(atom.MassNumber)),
            > 0 => _localization.Format("Atom_Ion_Positive", name, symbol),
            _ when AtomModel.HasAnionName(element.Symbol) => _localization.Format("Atom_Ion_Named", _localization.GetString("Atom_Anion_" + element.Symbol), symbol),
            _ => _localization.Format("Atom_Ion_Negative", name, symbol),
        };
    }

    private void RefreshTable()
    {
        var element = Element;
        PhaseTitle = _localization.Format("Atom_Value_ElementName", _localization.GetString(element.NameKey), element.Symbol);
        PhaseDescription = _localization.GetString($"Atom_Cat_{element.Category}_Text");
        PhaseBrush = element.Category switch
        {
            ElementCategory.AlkaliMetal or ElementCategory.AlkalineEarth => RedBrush,
            ElementCategory.Transition or ElementCategory.PostTransition => GoldBrush,
            ElementCategory.Metalloid => GreenBrush,
            ElementCategory.Nonmetal or ElementCategory.Halogen => CyanBrush,
            ElementCategory.NobleGas => VioletBrush,
            _ => OrangeBrush,
        };

        Stats =
        [
            new(_localization.GetString("Atom_Stat_Number"), Number(element.Number)),
            new(_localization.GetString("Atom_Stat_Mass"), MassText(element)),
            new(_localization.GetString("Atom_Stat_Kind"), _localization.GetString($"Atom_Cat_{element.Category}")),
            new(_localization.GetString("Atom_Stat_Group"), element.IsFBlock ? _localization.GetString("Atom_Value_FBlock") : Number(element.Group)),
            new(_localization.GetString("Atom_Stat_Period"), Number(element.Period)),
            new(_localization.GetString("Atom_Stat_Shells"), ShellsText(element.Shells)),
            new(_localization.GetString("Atom_Stat_Outer"), Number(element.Outer)),
            new(_localization.GetString("Atom_Stat_State"), _localization.GetString($"Atom_State_{element.State}")),
        ];
    }

    /// <summary>"12.011", or "[294] (its longest-lived isotope)".</summary>
    internal static string MassText(Element element) => element.MassIsMassNumber
        ? LocalizationService.Instance.Format("Atom_Value_MassBracket", Number(element.Mass))
        : element.Mass.ToString("0.###", CultureInfo.InvariantCulture);

    internal static string ShellsText(IReadOnlyList<int> shells) => string.Join(" · ", shells.Select(Number));

    #endregion

    #region formatting

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Number(double value) => value.ToString("0", CultureInfo.InvariantCulture);

    private static IBrush Frozen(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    #endregion

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ScaleNote));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(Facts));
        OnPropertyChanged(nameof(ChartCaption));
        OnPropertyChanged(nameof(ChartNote));

        foreach (var option in ElementOptions) option.Refresh();
        Refresh();
    }

    public void Dispose()
    {
        Lesson.Dispose();
        Card.Dispose();
        ColouringChoice.PropertyChanged -= OnColouringChosen;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
