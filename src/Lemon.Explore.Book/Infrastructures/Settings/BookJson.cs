using System.Text.Json.Serialization;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Explore.Places;

namespace Lemon.Explore.Book.Infrastructures.Settings;

/// <summary>
/// Source-generated JSON for everything the book saves, so saving works without reflection (trimmed and
/// native AOT builds turn reflection-based serialization off).
/// </summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(NarrationSettings))]
[JsonSerializable(typeof(ExploreLocationSettings))]
[JsonSerializable(typeof(List<BookmarkRecord>))]
[JsonSerializable(typeof(Dictionary<string, ChapterProgressRecord>))]
internal sealed partial class BookJson : JsonSerializerContext;
