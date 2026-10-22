using System.Text.RegularExpressions;
using System.Xml.Linq;
using Lemon.Explore.Book.Contents;
using Xunit;

namespace Lemon.Explore.Book.Tests;

/// <summary>
/// Every string ships in English and Chinese, and the language switches at run time. These tests catch the two
/// ways that breaks: a key added to one resx file only, and a key used in code that neither file defines.
/// </summary>
public partial class LocalizationCoverageTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly string Resources = Path.Combine(Root, "src", "Lemon.Explore.Book", "Resources");

    [Fact]
    public void BothLanguages_DefineTheSameKeys()
    {
        var english = Keys("AppStrings.resx");
        var chinese = Keys("AppStrings.zh-CN.resx");

        Assert.Empty(english.Except(chinese));
        Assert.Empty(chinese.Except(english));
    }

    [Fact]
    public void NoValue_IsEmpty()
    {
        foreach (var file in new[] { "AppStrings.resx", "AppStrings.zh-CN.resx" })
        {
            var empty = Values(file).Where(pair => string.IsNullOrWhiteSpace(pair.Value)).Select(pair => pair.Key);
            Assert.Empty(empty);
        }
    }

    [Fact]
    public void EveryKeyUsedInSource_IsDefined()
    {
        var defined = Keys("AppStrings.resx");
        var used = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".axaml", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal))
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in XamlKey().Matches(text).Concat(CodeKey().Matches(text)))
            {
                used.Add(match.Groups["key"].Value);
            }
        }

        // Keys built at run time.
        used.UnionWith(BookContents.Chapters.Select(c => c.TitleKey));
        used.UnionWith(BookContents.Volumes.Select(v => "Volume_" + v));

        Assert.NotEmpty(used);
        Assert.Empty(used.Except(defined));
    }

    private static SortedSet<string> Keys(string file) =>
        new(Values(file).Select(pair => pair.Key), StringComparer.Ordinal);

    private static IEnumerable<KeyValuePair<string, string>> Values(string file) =>
        XDocument.Load(Path.Combine(Resources, file))
            .Root!.Elements("data")
            .Select(d => new KeyValuePair<string, string>((string)d.Attribute("name")!, (string?)d.Element("value") ?? string.Empty));

    internal static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Lemon.Explore.Book.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [GeneratedRegex(@"\{loc:Localize (?<key>[A-Za-z0-9_]+)\}")]
    private static partial Regex XamlKey();

    // A literal key only: "Volume_" + volume is a prefix, covered by the run-time keys above.
    [GeneratedRegex(@"(?:GetString|Format)\(\s*""(?<key>[A-Za-z0-9_]+)""\s*[,)]")]
    private static partial Regex CodeKey();
}
