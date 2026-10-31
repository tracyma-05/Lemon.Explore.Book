using System.Globalization;

namespace Lemon.Explore.Book.Chapters.PeriodicTable;

/// <summary>The families the periodic table is coloured by.</summary>
public enum ElementCategory
{
    AlkaliMetal,
    AlkalineEarth,
    Transition,
    PostTransition,
    Metalloid,
    Nonmetal,
    Halogen,
    NobleGas,
    Lanthanide,
    Actinide,
}

/// <summary>What an element is at room temperature (25 °C); Unknown for those never made in bulk.</summary>
public enum ElementState
{
    Solid,
    Liquid,
    Gas,
    Unknown,
}

/// <summary>
/// One element: its atomic number, symbol, standard atomic weight (or the mass number of its longest-lived isotope),
/// family, group (0 for the two rows printed under the table), state and electrons per shell. Shells past 103 are
/// predictions.
/// </summary>
public sealed record Element(int Number, string Symbol, double Mass, ElementCategory Category, int Group, ElementState State, IReadOnlyList<int> Shells)
{
    /// <summary>
    /// The row: the shell count, except for palladium, which keeps its fifth-period place with no fifth-shell electron
    /// (its shells are 2, 8, 18, 18). Each period ends with a noble gas.
    /// </summary>
    public int Period => Number switch
    {
        <= 2 => 1,
        <= 10 => 2,
        <= 18 => 3,
        <= 36 => 4,
        <= 54 => 5,
        <= 86 => 6,
        _ => 7,
    };

    /// <summary>Electrons in the shell of its period: the outer shell, and none for palladium, whose fifth shell is empty.</summary>
    public int Outer => Shells.Count == Period ? Shells[^1] : 0;

    /// <summary>The lanthanides and actinides, printed as two rows under the table.</summary>
    public bool IsFBlock => Group == 0;

    public string NameKey => "Atom_El_" + Symbol;

    /// <summary>No stable isotope at all: technetium, promethium and everything from polonium on.</summary>
    public bool Radioactive => Number is 43 or 61 or >= 84;

    /// <summary>Too short-lived for a standard atomic weight: <see cref="Mass"/> is its longest-lived isotope's mass number.</summary>
    public bool MassIsMassNumber => Number is 43 or 61 or (>= 84 and <= 89) or >= 93;

    /// <summary>Where it is printed: column 1 to 18, row 1 to 7, or 9 and 10 for the lanthanides and actinides.</summary>
    public (int Column, int Row) Cell => Group != 0
        ? (Group, Period)
        : Number <= 71 ? (3 + Number - 57, 9) : (3 + Number - 89, 10);
}

/// <summary>The 118 elements, and the stable isotopes of the ones light enough to build by hand.</summary>
public static class ElementData
{
    // Number|symbol|mass|family|group|state|shells. Families: A alkali metal, E alkaline earth, T transition metal,
    // P post-transition metal, M metalloid, N nonmetal, H halogen, G noble gas, L lanthanide, C actinide.
    // States: s, l, g, u (unknown).
    private const string Table = """
        1|H|1.008|N|1|g|1
        2|He|4.0026|G|18|g|2
        3|Li|6.94|A|1|s|2,1
        4|Be|9.0122|E|2|s|2,2
        5|B|10.81|M|13|s|2,3
        6|C|12.011|N|14|s|2,4
        7|N|14.007|N|15|g|2,5
        8|O|15.999|N|16|g|2,6
        9|F|18.998|H|17|g|2,7
        10|Ne|20.180|G|18|g|2,8
        11|Na|22.990|A|1|s|2,8,1
        12|Mg|24.305|E|2|s|2,8,2
        13|Al|26.982|P|13|s|2,8,3
        14|Si|28.085|M|14|s|2,8,4
        15|P|30.974|N|15|s|2,8,5
        16|S|32.06|N|16|s|2,8,6
        17|Cl|35.45|H|17|g|2,8,7
        18|Ar|39.95|G|18|g|2,8,8
        19|K|39.098|A|1|s|2,8,8,1
        20|Ca|40.078|E|2|s|2,8,8,2
        21|Sc|44.956|T|3|s|2,8,9,2
        22|Ti|47.867|T|4|s|2,8,10,2
        23|V|50.942|T|5|s|2,8,11,2
        24|Cr|51.996|T|6|s|2,8,13,1
        25|Mn|54.938|T|7|s|2,8,13,2
        26|Fe|55.845|T|8|s|2,8,14,2
        27|Co|58.933|T|9|s|2,8,15,2
        28|Ni|58.693|T|10|s|2,8,16,2
        29|Cu|63.546|T|11|s|2,8,18,1
        30|Zn|65.38|T|12|s|2,8,18,2
        31|Ga|69.723|P|13|s|2,8,18,3
        32|Ge|72.630|M|14|s|2,8,18,4
        33|As|74.922|M|15|s|2,8,18,5
        34|Se|78.971|N|16|s|2,8,18,6
        35|Br|79.904|H|17|l|2,8,18,7
        36|Kr|83.798|G|18|g|2,8,18,8
        37|Rb|85.468|A|1|s|2,8,18,8,1
        38|Sr|87.62|E|2|s|2,8,18,8,2
        39|Y|88.906|T|3|s|2,8,18,9,2
        40|Zr|91.224|T|4|s|2,8,18,10,2
        41|Nb|92.906|T|5|s|2,8,18,12,1
        42|Mo|95.95|T|6|s|2,8,18,13,1
        43|Tc|98|T|7|s|2,8,18,13,2
        44|Ru|101.07|T|8|s|2,8,18,15,1
        45|Rh|102.91|T|9|s|2,8,18,16,1
        46|Pd|106.42|T|10|s|2,8,18,18
        47|Ag|107.87|T|11|s|2,8,18,18,1
        48|Cd|112.41|T|12|s|2,8,18,18,2
        49|In|114.82|P|13|s|2,8,18,18,3
        50|Sn|118.71|P|14|s|2,8,18,18,4
        51|Sb|121.76|M|15|s|2,8,18,18,5
        52|Te|127.60|M|16|s|2,8,18,18,6
        53|I|126.90|H|17|s|2,8,18,18,7
        54|Xe|131.29|G|18|g|2,8,18,18,8
        55|Cs|132.91|A|1|s|2,8,18,18,8,1
        56|Ba|137.33|E|2|s|2,8,18,18,8,2
        57|La|138.91|L|0|s|2,8,18,18,9,2
        58|Ce|140.12|L|0|s|2,8,18,19,9,2
        59|Pr|140.91|L|0|s|2,8,18,21,8,2
        60|Nd|144.24|L|0|s|2,8,18,22,8,2
        61|Pm|145|L|0|s|2,8,18,23,8,2
        62|Sm|150.36|L|0|s|2,8,18,24,8,2
        63|Eu|151.96|L|0|s|2,8,18,25,8,2
        64|Gd|157.25|L|0|s|2,8,18,25,9,2
        65|Tb|158.93|L|0|s|2,8,18,27,8,2
        66|Dy|162.50|L|0|s|2,8,18,28,8,2
        67|Ho|164.93|L|0|s|2,8,18,29,8,2
        68|Er|167.26|L|0|s|2,8,18,30,8,2
        69|Tm|168.93|L|0|s|2,8,18,31,8,2
        70|Yb|173.05|L|0|s|2,8,18,32,8,2
        71|Lu|174.97|L|0|s|2,8,18,32,9,2
        72|Hf|178.49|T|4|s|2,8,18,32,10,2
        73|Ta|180.95|T|5|s|2,8,18,32,11,2
        74|W|183.84|T|6|s|2,8,18,32,12,2
        75|Re|186.21|T|7|s|2,8,18,32,13,2
        76|Os|190.23|T|8|s|2,8,18,32,14,2
        77|Ir|192.22|T|9|s|2,8,18,32,15,2
        78|Pt|195.08|T|10|s|2,8,18,32,17,1
        79|Au|196.97|T|11|s|2,8,18,32,18,1
        80|Hg|200.59|T|12|l|2,8,18,32,18,2
        81|Tl|204.38|P|13|s|2,8,18,32,18,3
        82|Pb|207.2|P|14|s|2,8,18,32,18,4
        83|Bi|208.98|P|15|s|2,8,18,32,18,5
        84|Po|209|P|16|s|2,8,18,32,18,6
        85|At|210|H|17|u|2,8,18,32,18,7
        86|Rn|222|G|18|g|2,8,18,32,18,8
        87|Fr|223|A|1|u|2,8,18,32,18,8,1
        88|Ra|226|E|2|s|2,8,18,32,18,8,2
        89|Ac|227|C|0|s|2,8,18,32,18,9,2
        90|Th|232.04|C|0|s|2,8,18,32,18,10,2
        91|Pa|231.04|C|0|s|2,8,18,32,20,9,2
        92|U|238.03|C|0|s|2,8,18,32,21,9,2
        93|Np|237|C|0|s|2,8,18,32,22,9,2
        94|Pu|244|C|0|s|2,8,18,32,24,8,2
        95|Am|243|C|0|s|2,8,18,32,25,8,2
        96|Cm|247|C|0|s|2,8,18,32,25,9,2
        97|Bk|247|C|0|s|2,8,18,32,27,8,2
        98|Cf|251|C|0|s|2,8,18,32,28,8,2
        99|Es|252|C|0|s|2,8,18,32,29,8,2
        100|Fm|257|C|0|u|2,8,18,32,30,8,2
        101|Md|258|C|0|u|2,8,18,32,31,8,2
        102|No|259|C|0|u|2,8,18,32,32,8,2
        103|Lr|266|C|0|u|2,8,18,32,32,8,3
        104|Rf|267|T|4|u|2,8,18,32,32,10,2
        105|Db|268|T|5|u|2,8,18,32,32,11,2
        106|Sg|269|T|6|u|2,8,18,32,32,12,2
        107|Bh|270|T|7|u|2,8,18,32,32,13,2
        108|Hs|269|T|8|u|2,8,18,32,32,14,2
        109|Mt|278|T|9|u|2,8,18,32,32,15,2
        110|Ds|281|T|10|u|2,8,18,32,32,16,2
        111|Rg|282|T|11|u|2,8,18,32,32,17,2
        112|Cn|285|T|12|u|2,8,18,32,32,18,2
        113|Nh|286|P|13|u|2,8,18,32,32,18,3
        114|Fl|289|P|14|u|2,8,18,32,32,18,4
        115|Mc|290|P|15|u|2,8,18,32,32,18,5
        116|Lv|293|P|16|u|2,8,18,32,32,18,6
        117|Ts|294|H|17|u|2,8,18,32,32,18,7
        118|Og|294|G|18|u|2,8,18,32,32,18,8
        """;

    /// <summary>The elements, by atomic number: <c>Elements[0]</c> is hydrogen.</summary>
    public static IReadOnlyList<Element> Elements { get; } = Parse();

    public static Element Of(int number) => Elements[Math.Clamp(number, 1, Elements.Count) - 1];

    /// <summary>
    /// The neutron counts of the nuclei that last, for hydrogen to krypton. A few that decay so slowly that most made
    /// with the Earth are still here (half-lives over 10¹⁷ years, such as calcium-48) count as stable; potassium-40,
    /// with a half-life of about a billion years, does not.
    /// </summary>
    public static IReadOnlyDictionary<int, int[]> StableNeutrons { get; } = new Dictionary<int, int[]>
    {
        [1] = [0, 1],
        [2] = [1, 2],
        [3] = [3, 4],
        [4] = [5],
        [5] = [5, 6],
        [6] = [6, 7],
        [7] = [7, 8],
        [8] = [8, 9, 10],
        [9] = [10],
        [10] = [10, 11, 12],
        [11] = [12],
        [12] = [12, 13, 14],
        [13] = [14],
        [14] = [14, 15, 16],
        [15] = [16],
        [16] = [16, 17, 18, 20],
        [17] = [18, 20],
        [18] = [18, 20, 22],
        [19] = [20, 22],
        [20] = [20, 22, 23, 24, 26, 28],
        [21] = [24],
        [22] = [24, 25, 26, 27, 28],
        [23] = [27, 28],
        [24] = [26, 28, 29, 30],
        [25] = [30],
        [26] = [28, 30, 31, 32],
        [27] = [32],
        [28] = [30, 32, 33, 34, 36],
        [29] = [34, 36],
        [30] = [34, 36, 37, 38, 40],
        [31] = [38, 40],
        [32] = [38, 40, 41, 42, 44],
        [33] = [42],
        [34] = [40, 42, 43, 44, 46, 48],
        [35] = [44, 46],
        [36] = [42, 44, 46, 47, 48, 50],
    };

    private static List<Element> Parse()
    {
        var elements = new List<Element>();
        foreach (var line in Table.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var f = line.Split('|');
            elements.Add(new Element(
                int.Parse(f[0], CultureInfo.InvariantCulture),
                f[1],
                double.Parse(f[2], CultureInfo.InvariantCulture),
                f[3] switch
                {
                    "A" => ElementCategory.AlkaliMetal,
                    "E" => ElementCategory.AlkalineEarth,
                    "T" => ElementCategory.Transition,
                    "P" => ElementCategory.PostTransition,
                    "M" => ElementCategory.Metalloid,
                    "N" => ElementCategory.Nonmetal,
                    "H" => ElementCategory.Halogen,
                    "G" => ElementCategory.NobleGas,
                    "L" => ElementCategory.Lanthanide,
                    _ => ElementCategory.Actinide,
                },
                int.Parse(f[4], CultureInfo.InvariantCulture),
                f[5] switch
                {
                    "g" => ElementState.Gas,
                    "l" => ElementState.Liquid,
                    "u" => ElementState.Unknown,
                    _ => ElementState.Solid,
                },
                f[6].Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray()));
        }

        return elements;
    }
}
