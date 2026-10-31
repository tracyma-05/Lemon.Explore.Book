using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Lemon.Book.Shell.Controls.Explore;
using Lemon.Explore.Book.Controls.Explore;

namespace Lemon.Explore.Book.Chapters.PeriodicTable;

/// <summary>
/// The things in the atoms diagram that can be clicked for an explanation, the walkthrough's highlight, and the
/// hands-on parts: a tray's + and − add and take away a particle, and a cell of the table picks its element.
/// </summary>
public sealed partial class AtomDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<AtomDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<AtomDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<AtomDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<AtomDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<AtomDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<AtomDiagram>(TitleHeightProperty, HighlightProperty, SelectedObjectProperty);
        return true;
    }

    public string? Highlight
    {
        get => GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    public string? SelectedObject
    {
        get => GetValue(SelectedObjectProperty);
        set => SetValue(SelectedObjectProperty, value);
    }

    public ICommand? InteractionCommand
    {
        get => GetValue(InteractionCommandProperty);
        set => SetValue(InteractionCommandProperty, value);
    }

    public bool IsLessonActive
    {
        get => GetValue(IsLessonActiveProperty);
        set => SetValue(IsLessonActiveProperty, value);
    }

    public double TitleHeight
    {
        get => GetValue(TitleHeightProperty);
        set => SetValue(TitleHeightProperty, value);
    }

    private readonly ExploreTargets _targets = new();
    private readonly LessonReserve _reserve = new();

    /// <summary>The thing the button went down on; a click if it comes up there.</summary>
    private string? _pressed;

    /// <summary>The ids of the build view's trays and their buttons, by particle.</summary>
    internal static readonly IReadOnlyList<(Particle Kind, string Tray, string Add, string Remove)> Trays =
    [
        (Particle.Proton, "bin_p", "add_p", "rem_p"),
        (Particle.Neutron, "bin_n", "add_n", "rem_n"),
        (Particle.Electron, "bin_e", "add_e", "rem_e"),
    ];

    /// <summary>The target id of an element's cell: <c>el_26</c>.</summary>
    internal static string CellId(int number) => "el_" + number.ToString(CultureInfo.InvariantCulture);

    /// <summary>The element a cell's id stands for, or null.</summary>
    internal static int? CellNumber(string? id) =>
        id is not null && id.StartsWith("el_", StringComparison.Ordinal)
        && int.TryParse(id.AsSpan(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
        && number >= 1 && number <= ElementData.Elements.Count
            ? number
            : null;

    /// <summary>
    /// The thing a target id stands for (<c>cat_Halogen</c> → <c>Obj_Cat_Halogen</c>). It names the title and the
    /// narration clip. A tray's buttons stand for the tray; an element's cell picks the element and has no card.
    /// </summary>
    internal static string? ObjectName(string? id)
    {
        if (id is null) return null;
        if (id.StartsWith("cat_", StringComparison.Ordinal))
        {
            return Enum.TryParse<ElementCategory>(id[4..], out _) ? "Obj_Cat_" + id[4..] : null;
        }

        if (id.StartsWith("state_", StringComparison.Ordinal))
        {
            return Enum.TryParse<ElementState>(id[6..], out _) ? "Obj_State_" + id[6..] : null;
        }

        return id switch
        {
            "nucleus" => "Obj_Nucleus",
            "proton" => "Obj_Proton",
            "neutron" => "Obj_Neutron",
            "electron" => "Obj_Electron",
            "shell" => "Obj_Shell",
            "tile" => "Obj_Tile",
            "massnumber" => "Obj_MassNumber",
            "charge" => "Obj_Charge",
            "stability" => "Obj_Stability",
            "bin_p" or "add_p" or "rem_p" => "Obj_ProtonTray",
            "bin_n" or "add_n" or "rem_n" => "Obj_NeutronTray",
            "bin_e" or "add_e" or "rem_e" => "Obj_ElectronTray",
            "periods" => "Obj_Periods",
            "groups" => "Obj_Groups",
            "fblock" => "Obj_FBlock",
            "detail" => "Obj_Detail",
            "minishells" => "Obj_MiniShells",
            "outer" => "Obj_Outer",
            _ => null,
        };
    }

    /// <summary>The resource key of a target's title: <c>Atom_Obj_Nucleus</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Atom_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Atom_" + name : null;

    /// <summary>Things the diagram glows itself; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "shell" or "groups" or "periods" or "fblock";

    /// <summary>Whether the walkthrough points at <paramref name="id"/>, which the diagram glows itself.</summary>
    private bool Glows(string id) => Highlight == id;

    /// <summary>A text, a pill or a part already drawn in <paramref name="box"/>, as a target.</summary>
    private void AddBoxTarget(string id, Rect box)
    {
        if (box == default) return;
        box = box.Inflate(new Thickness(2, 1));
        _targets.AddBox(id, box);
    }

    /// <summary>The title sits on the left page: the picture starts below a top margin (and any title block over it).</summary>
    private double TitleSpace => 22 + TitleHeight + 14;

    #region clicking

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pressed = _targets.Find(e.GetPosition(this))?.Id;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pressed is not null) return;
        var hover = _targets.Find(e.GetPosition(this))?.Id;
        _targets.Hover = hover;
        Cursor = hover is not null ? new Cursor(StandardCursorType.Hand) : null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var picked = _pressed;
        _pressed = null;
        e.Pointer.Capture(null);
        if (picked is not null && _targets.Find(e.GetPosition(this))?.Id == picked) Pick(picked);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _targets.Hover = null;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = null;
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>
    /// A click on a thing: a cell picks its element; a tray's button adds or takes a particle (and explains the tray the
    /// first time); anything else shows (and says) what it is.
    /// </summary>
    private void Pick(string id)
    {
        Interacted();
        if (CellNumber(id) is { } number)
        {
            SetCurrentValue(ElementZProperty, number);
            return;
        }

        foreach (var (kind, tray, add, remove) in Trays)
        {
            if (id != add && id != remove) continue;
            Step(kind, id == add ? 1 : -1);
            if (SelectedObject != tray) Select(tray);
            return;
        }

        Select(id);
    }

    /// <summary>One particle more or fewer, within what an atom here can hold.</summary>
    private void Step(Particle kind, int by)
    {
        switch (kind)
        {
            case Particle.Proton:
                SetCurrentValue(ProtonsProperty, Math.Clamp(Protons + by, 0, AtomModel.MaxProtons));
                break;

            case Particle.Neutron:
                SetCurrentValue(NeutronsProperty, Math.Clamp(Neutrons + by, 0, AtomModel.MaxNeutrons));
                break;

            default:
                SetCurrentValue(ElectronsProperty, Math.Clamp(Electrons + by, 0, AtomModel.MaxElectrons(Protons)));
                break;
        }
    }

    /// <summary>Shows a thing's explanation; cleared first so clicking the same thing again says it again.</summary>
    private void Select(string id)
    {
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
