namespace Lemon.Explore.Book.Chapters.PeriodicTable;

/// <summary>The atoms page's two views.</summary>
public enum AtomMode
{
    /// <summary>An atom built a particle at a time: protons and neutrons in the nucleus, electrons in shells.</summary>
    Build,

    /// <summary>The 118 elements in their table, and the atom of the one picked.</summary>
    Table,
}

/// <summary>What the periodic table's cells are coloured by.</summary>
public enum TableColouring
{
    Category,
    State,
    Outer,
}

/// <summary>A nucleus picked on the chart of stable nuclei.</summary>
public sealed record Nucleus(int Protons, int Neutrons);

/// <summary>
/// The atom (or ion, or bare nucleus) of the build view: how many protons, neutrons and electrons it has, and what
/// follows from them.
/// </summary>
public readonly record struct AtomBuild(int Protons, int Neutrons, int Electrons)
{
    public int MassNumber => Protons + Neutrons;

    public int Charge => Protons - Electrons;

    /// <summary>The element its protons make, or null with none.</summary>
    public Element? Element => Protons >= 1 ? ElementData.Of(Protons) : null;

    public bool IsStable => AtomModel.IsStable(Protons, Neutrons);

    /// <summary>Electrons per shell, innermost first (empty with none).</summary>
    public IReadOnlyList<int> Shells => AtomModel.ShellsFor(Electrons);
}

/// <summary>The rules of the build view.</summary>
public static class AtomModel
{
    /// <summary>The heaviest atom built by hand: krypton.</summary>
    public const int MaxProtons = 36;

    public const int MaxNeutrons = 50;

    /// <summary>Electrons beyond the protons an ion can hold here (N³⁻ is the most an atom takes in salts).</summary>
    public const int MaxExtraElectrons = 3;

    /// <summary>Whether a nucleus lasts: it must have a proton, and be one of the stable isotopes.</summary>
    public static bool IsStable(int protons, int neutrons) =>
        ElementData.StableNeutrons.TryGetValue(protons, out var stable) && stable.Contains(neutrons);

    /// <summary>
    /// How <paramref name="electrons"/> electrons sit in shells: as in the neutral atom with that many, which holds for
    /// the ions built here (Na⁺ has neon's shells, Cl⁻ argon's).
    /// </summary>
    public static IReadOnlyList<int> ShellsFor(int electrons) => electrons <= 0 ? [] : ElementData.Of(electrons).Shells;

    public static int MaxElectrons(int protons) => Math.Min(MaxProtons, protons + MaxExtraElectrons);

    /// <summary>
    /// What the build is, for the card: "Nothing", "NoProton", else its charge ("Neutral", "Positive", "Negative") and
    /// whether its nucleus lasts ("Stable", "Unstable"), joined by an underscore.
    /// </summary>
    public static string StateName(AtomBuild atom)
    {
        if (atom.Protons == 0) return atom.Neutrons + atom.Electrons == 0 ? "Nothing" : "NoProton";
        var charge = atom.Charge switch
        {
            0 => "Neutral",
            > 0 => "Positive",
            _ => "Negative",
        };
        return charge + (atom.IsStable ? "_Stable" : "_Unstable");
    }

    /// <summary>Elements whose negative ion has its own English name (chloride, oxide…): <c>Atom_Anion_Cl</c>.</summary>
    public static bool HasAnionName(string symbol) => symbol is "H" or "N" or "O" or "F" or "P" or "S" or "Cl" or "Se" or "Br";

    /// <summary>A charge as written after a symbol: "", "+", "2+", "−", "3−".</summary>
    public static string ChargeSuffix(int charge) => charge switch
    {
        0 => string.Empty,
        1 => "+",
        -1 => "−",
        > 0 => charge + "+",
        _ => -charge + "−",
    };

    /// <summary>A charge as a signed number: "0", "+1", "−2".</summary>
    public static string SignedCharge(int charge) => charge switch
    {
        0 => "0",
        > 0 => "+" + charge,
        _ => "−" + -charge,
    };
}
