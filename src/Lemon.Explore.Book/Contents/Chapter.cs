namespace Lemon.Explore.Book.Contents;

/// <summary>
/// One chapter of the book: a topic that gets a text page (left) and an interactive diagram page (right) as
/// one two-page spread.
/// </summary>
/// <param name="Slug">Stable id, also used for file names and resource keys (e.g. <c>Solar-System</c>).</param>
/// <param name="Volume">The part of the book the chapter belongs to.</param>
public sealed record Chapter(string Slug, Volume Volume)
{
    /// <summary>Resource key of the chapter title: the slug with every non-alphanumeric character as <c>_</c>.</summary>
    public string TitleKey => "Chapter_" + ToKeySegment(Slug);

    internal static string ToKeySegment(string value) =>
        string.Concat(value.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
}
