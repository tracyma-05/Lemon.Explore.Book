namespace Lemon.Explore.Book.Contents;

/// <summary>
/// The fixed running order of the book: front matter, one table-of-contents page per volume, the chapters,
/// then the back matter.
/// </summary>
public static class BookContents
{
    public static IReadOnlyList<Chapter> Chapters { get; } =
    [
        new("Solar-System", Volume.Sky),
        new("Eclipses", Volume.Sky),
        new("Seasons", Volume.Sky),
        new("Moon-Phases", Volume.Sky),
        new("Day-Night", Volume.Sky),
        new("Tides", Volume.Sky),
        new("Kepler", Volume.Sky),
        new("Star-Trails", Volume.Sky),
        new("Retrograde", Volume.Sky),
        new("Star-Life", Volume.Sky),
        new("Black-Hole", Volume.Sky),
        new("Expansion", Volume.Sky),

        new("Water-Cycle", Volume.Earth),
        new("Sea-Breeze", Volume.Earth),
        new("Greenhouse", Volume.Earth),
        new("Typhoon", Volume.Earth),
        new("Lightning", Volume.Earth),
        new("Blue-Sky", Volume.Earth),
        new("Rainbow", Volume.Earth),
        new("Aurora", Volume.Earth),
        new("Earthquakes", Volume.Earth),
        new("Volcanoes", Volume.Earth),
        new("Ice-Ages", Volume.Earth),

        new("Oscillations", Volume.Physics),
        new("Waves", Volume.Physics),
        new("Sound", Volume.Physics),
        new("Projectile", Volume.Physics),
        new("Flight", Volume.Physics),
        new("Lenses", Volume.Physics),
        new("Spectra", Volume.Physics),
        new("Heat-Engines", Volume.Physics),
        new("Power-Grid", Volume.Physics),
        new("Relativity", Volume.Physics),

        new("Acids-Bases", Volume.Chemistry),
    ];

    public static IReadOnlyList<Volume> Volumes { get; } = Enum.GetValues<Volume>();

    /// <summary>Pages before the table of contents: cover, how-to-use, title page.</summary>
    internal const int FrontMatterPages = 3;

    /// <summary>Index of the first table-of-contents page.</summary>
    public static int FirstContentsPage => FrontMatterPages;

    public static int ContentsPage(Volume volume) => FrontMatterPages + (int)volume;

    /// <summary>
    /// Index of a chapter's text page; its diagram page follows. The front matter plus one contents page per
    /// volume is an odd count (3 + 4), so every chapter opens on a left page and fills exactly one spread.
    /// </summary>
    public static int ChapterPage(int chapterIndex) => FrontMatterPages + Volumes.Count + (chapterIndex * 2);

    /// <summary>Index of the text page of the chapter with this slug, e.g. "Moon-Phases".</summary>
    public static int ChapterPageOf(string slug)
    {
        var index = Chapters.ToList().FindIndex(c => c.Slug == slug);
        return index >= 0 ? ChapterPage(index) : throw new ArgumentException($"No chapter '{slug}'.", nameof(slug));
    }

    /// <summary>Total pages: front matter, contents, two per chapter, credits and the back cover.</summary>
    public static int PageCount => ChapterPage(Chapters.Count) + 2;
}
