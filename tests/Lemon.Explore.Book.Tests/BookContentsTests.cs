using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.ViewModels;
using Lemon.Explore.Book.ViewModels.Pages;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public class BookContentsTests
{
    [Fact]
    public void AllThirtyFourTopics_AreChapters_Once()
    {
        Assert.Equal(34, BookContents.Chapters.Count);
        Assert.Equal(BookContents.Chapters.Count, BookContents.Chapters.Select(c => c.Slug).Distinct().Count());
    }

    [Fact]
    public void EveryChapter_OpensOnALeftPage_SoItFillsOneSpread()
    {
        for (var i = 0; i < BookContents.Chapters.Count; i++)
        {
            var page = BookContents.ChapterPage(i);
            var spread = SpreadLayout.SpreadOf(page);

            Assert.Equal(page, SpreadLayout.LeftPage(spread, BookContents.PageCount));
            Assert.Equal(page + 1, SpreadLayout.RightPage(spread, BookContents.PageCount));
        }
    }

    [Fact]
    public void BuiltPages_MatchTheRunningOrder()
    {
        var pages = MainViewModel.BuildPages(new NullNavigator());

        Assert.Equal(BookContents.PageCount, pages.Count);
        Assert.Equal(Enumerable.Range(0, pages.Count), pages.Select(page => page.Index));
        Assert.IsType<CoverPageViewModel>(pages[0]);
        Assert.IsType<BackCoverPageViewModel>(pages[^1]);

        foreach (var volume in BookContents.Volumes)
        {
            Assert.Equal(volume, Assert.IsType<ContentsPageViewModel>(pages[BookContents.ContentsPage(volume)]).Volume);
        }

        for (var i = 0; i < BookContents.Chapters.Count; i++)
        {
            var text = Assert.IsType<ChapterTextPageViewModel>(pages[BookContents.ChapterPage(i)]);
            var diagram = Assert.IsType<ChapterDiagramPageViewModel>(pages[BookContents.ChapterPage(i) + 1]);
            Assert.Same(BookContents.Chapters[i], text.Chapter);
            Assert.Same(BookContents.Chapters[i], diagram.Chapter);
        }
    }

    [Fact]
    public void Contents_ListEveryChapter_WithItsPage()
    {
        var pages = MainViewModel.BuildPages(new NullNavigator());
        var entries = pages.OfType<ContentsPageViewModel>().SelectMany(p => p.Entries).ToArray();

        Assert.Equal(BookContents.Chapters.Count, entries.Length);
        Assert.Equal(Enumerable.Range(1, entries.Length), entries.Select(e => e.Number));
        Assert.All(entries, e => Assert.IsType<ChapterTextPageViewModel>(pages[e.PageIndex]));
    }

    [Fact]
    public void ContentsLink_MovesTheReadingPage_AndRemembersIt()
    {
        var settings = new MemorySettingsStore();
        var book = new MainViewModel(settings);
        var entry = book.Pages.OfType<ContentsPageViewModel>().First().Entries[2];

        entry.OpenCommand.Execute(null);

        Assert.Equal(entry.PageIndex, book.PageIndex);
        Assert.Equal(entry.PageIndex, new MainViewModel(settings).PageIndex);
    }

    [Theory]
    [InlineData(0, 0, -1, 0)]
    [InlineData(1, 1, 1, 2)]
    [InlineData(2, 1, 1, 2)]
    [InlineData(7, 4, 7, 8)]
    public void Spreads_PutTheCoverAloneOnTheRight(int page, int spread, int left, int right)
    {
        Assert.Equal(spread, SpreadLayout.SpreadOf(page));
        Assert.Equal(left, SpreadLayout.LeftPage(spread, BookContents.PageCount));
        Assert.Equal(right, SpreadLayout.RightPage(spread, BookContents.PageCount));
    }

    [Fact]
    public void LastSpread_HoldsCreditsAndBackCover()
    {
        var last = SpreadLayout.LastSpread(BookContents.PageCount);

        Assert.Equal(BookContents.PageCount - 2, SpreadLayout.LeftPage(last, BookContents.PageCount));
        Assert.Equal(BookContents.PageCount - 1, SpreadLayout.RightPage(last, BookContents.PageCount));
    }

    private sealed class NullNavigator : IBookNavigator
    {
        public void GoToPage(int pageIndex)
        {
        }
    }
}
