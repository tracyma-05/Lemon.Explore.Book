using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Lemon.Explore.Book.Explore.Narration;
using Xunit;

namespace Lemon.Explore.Book.Tests;

/// <summary>
/// The manifest built into the app must match the current narration text in both languages: a <c>Narr_*</c> string
/// edited without rendering again would play audio that says something else. Run
/// <c>python -I scripts/narration/generate-narration.py</c> after editing one. The audio is not in the repository, so
/// only the manifest is checked here.
/// </summary>
public sealed class NarrationManifestTests
{
    private static readonly string Resources =
        Path.Combine(LocalizationCoverageTests.FindRepositoryRoot(), "src", "Lemon.Explore.Book", "Resources");

    [Theory]
    [InlineData("en", "AppStrings.resx")]
    [InlineData("zh-CN", "AppStrings.zh-CN.resx")]
    public void EveryClip_IsRenderedFromItsCurrentText(string culture, string file)
    {
        var entry = NarrationManifest.Bundled.Culture(culture);
        Assert.NotNull(entry);

        var problems = new List<string>();
        foreach (var (key, text) in Clips(file))
        {
            if (!entry.Clips.TryGetValue(key, out var clip))
            {
                problems.Add($"{culture}/{key}: not rendered");
            }
            else if (clip.Hash != Hash(entry, text))
            {
                problems.Add($"{culture}/{key}: text changed since it was rendered");
            }
        }

        Assert.Empty(problems);
    }

    [Theory]
    [InlineData("en", "AppStrings.resx")]
    [InlineData("zh-CN", "AppStrings.zh-CN.resx")]
    public void TheManifest_HasNoClipsForRemovedKeys(string culture, string file)
    {
        var keys = Clips(file).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);

        var orphans = NarrationManifest.Bundled.Culture(culture)!.Clips.Keys.Except(keys).ToList();

        Assert.Empty(orphans);
    }

    [Fact]
    public void AClipPath_CarriesTheHash()
    {
        Assert.Equal("zh-CN/Narr_Seasons_Obj_Sun.0123456789abcdef.mp3",
            NarrationManifest.ClipPath("zh-CN", "Narr_Seasons_Obj_Sun", "0123456789abcdef"));
    }

    private static IEnumerable<(string Key, string Text)> Clips(string file) =>
        XDocument.Load(Path.Combine(Resources, file))
            .Root!.Elements("data")
            .Select(d => (Key: (string)d.Attribute("name")!, Text: (string?)d.Element("value") ?? string.Empty))
            .Where(c => c.Key.StartsWith("Narr_", StringComparison.Ordinal));

    // The generator's hash: the first 16 hex digits of SHA-256 over "voice|rate|pitch|text".
    private static string Hash(NarrationManifest.CultureEntry entry, string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{entry.Voice}|{entry.Rate}|{entry.Pitch}|{text}")))[..16];
}
