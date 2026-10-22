using Lemon.Explore.Book.Chapters.Eclipses;
using Xunit;

namespace Lemon.Explore.Book.Tests.Chapters;

public sealed class EclipseGeometryTests
{
    private const double NewMoon = 0;
    private const double FullMoon = Math.PI;

    [Fact]
    public void OverlapArea_CoversDisjointNestedAndPartialDiscs()
    {
        Assert.Equal(0, EclipseGeometry.OverlapArea(1, 1, 2.5));
        Assert.Equal(Math.PI * 0.25, EclipseGeometry.OverlapArea(2, 0.5, 0.3), 9);

        // Two unit discs one radius apart share 2π/3 − √3/2.
        Assert.Equal((2 * Math.PI / 3) - (Math.Sqrt(3) / 2), EclipseGeometry.OverlapArea(1, 1, 1), 9);
    }

    [Fact]
    public void SunAndMoon_LookAlmostTheSameSizeFromEarth()
    {
        var near = EclipseGeometry.Solar(NewMoon, moonNear: true, 0);
        var far = EclipseGeometry.Solar(NewMoon, moonNear: false, 0);

        Assert.InRange(near.MoonRadius / near.SunRadius, 1.0, 1.15);
        Assert.InRange(far.MoonRadius / far.SunRadius, 0.8, 1.0);
    }

    [Fact]
    public void NewMoon_OnTheCentreLine_IsTotalWhenNearAndAnnularWhenFar()
    {
        var total = EclipseGeometry.Solar(NewMoon, moonNear: true, 0);
        Assert.Equal(EclipseStage.SolarTotal, total.Stage);
        Assert.Equal(1, total.Coverage, 6);

        var annular = EclipseGeometry.Solar(NewMoon, moonNear: false, 0);
        Assert.Equal(EclipseStage.SolarAnnular, annular.Stage);
        Assert.InRange(annular.Coverage, 0.6, 0.95);
    }

    [Fact]
    public void ObserverAwayFromTheCentreLine_SeesLessOfTheSunCovered()
    {
        var near = EclipseGeometry.Solar(NewMoon, moonNear: true, 0.4);
        var far = EclipseGeometry.Solar(NewMoon, moonNear: true, 1);

        Assert.Equal(EclipseStage.SolarPartial, near.Stage);
        Assert.True(far.Coverage < near.Coverage);
        Assert.True(far.Coverage < 0.2);
    }

    [Fact]
    public void Solar_ThePartialPhaseComesBeforeAndAfterTotality_AndTheMoonMovesRightToLeft()
    {
        var before = EclipseGeometry.Solar(-8 * Math.PI / 180, moonNear: true, 0);
        var after = EclipseGeometry.Solar(8 * Math.PI / 180, moonNear: true, 0);

        Assert.Equal(EclipseStage.SolarPartial, before.Stage);
        Assert.Equal(EclipseStage.SolarPartial, after.Stage);
        Assert.True(before.MoonX > 0);
        Assert.True(after.MoonX < 0);
        Assert.Equal(EclipseStage.None, EclipseGeometry.Solar(Math.PI / 2, moonNear: true, 0).Stage);
    }

    [Fact]
    public void MoonShadow_ReachesEarthWhenNear_AndEndsShortWhenFar()
    {
        var near = EclipseGeometry.MoonShadowOnEarth(NewMoon, moonNear: true);
        var far = EclipseGeometry.MoonShadowOnEarth(NewMoon, moonNear: false);

        Assert.True(near.Reaches);
        Assert.True(near.Umbra > 0);
        Assert.True(far.Umbra < 0);
        Assert.False(EclipseGeometry.MoonShadowOnEarth(FullMoon, moonNear: true).Reaches);
    }

    [Theory]
    [InlineData(LunarPath.Central, EclipseStage.LunarTotal)]
    [InlineData(LunarPath.OffCentre, EclipseStage.LunarPartial)]
    [InlineData(LunarPath.Grazing, EclipseStage.LunarPenumbral)]
    public void FullMoon_PathThroughTheShadow_DecidesTheKindOfLunarEclipse(LunarPath path, EclipseStage expected)
    {
        Assert.Equal(expected, EclipseGeometry.Lunar(FullMoon, EclipseGeometry.LunarPathHeight(path)).Stage);
    }

    [Fact]
    public void Lunar_NothingHappensAwayFromFullMoon()
    {
        Assert.Equal(EclipseStage.None, EclipseGeometry.Lunar(NewMoon, 0).Stage);
        Assert.Equal(EclipseStage.None, EclipseGeometry.Lunar(Math.PI / 2, 0).Stage);
        Assert.Equal(1, EclipseGeometry.Lunar(Math.PI / 2, 0).Light);
    }

    [Fact]
    public void EarthsUmbra_HoldsTheMoonWithRoomToSpare()
    {
        var umbra = EclipseGeometry.EarthShadow.Umbra(EclipseGeometry.MoonOrbitNear);
        Assert.InRange(umbra / EclipseGeometry.MoonRadius, 1.6, 3);
    }

    [Fact]
    public void Playback_SlowsDownThroughTheEclipse()
    {
        Assert.True(EclipseGeometry.PlaybackRate(EclipseMode.Solar, 0) < EclipseGeometry.PlaybackRate(EclipseMode.Solar, 10));
        Assert.True(EclipseGeometry.PlaybackRate(EclipseMode.Solar, 10) < EclipseGeometry.PlaybackRate(EclipseMode.Solar, 90));
        Assert.True(EclipseGeometry.PlaybackRate(EclipseMode.Lunar, 180) < EclipseGeometry.PlaybackRate(EclipseMode.Lunar, 90));
        Assert.Equal(EclipseGeometry.PlaybackRate(EclipseMode.Solar, 355), EclipseGeometry.PlaybackRate(EclipseMode.Solar, -5), 9);
    }
}

public sealed class EclipseSeasonsTests
{
    [Fact]
    public void Events_AlternateNewAndFullMoonsHalfAMonthApart()
    {
        var events = EclipseSeasons.Events(-100, 400).ToList();

        Assert.All(events.Zip(events.Skip(1)), pair =>
        {
            Assert.NotEqual(pair.First.IsNewMoon, pair.Second.IsNewMoon);
            Assert.Equal(EclipseSeasons.SynodicMonth / 2, pair.Second.Day - pair.First.Day, 1e-6);
        });
        Assert.All(events.Where(e => e.IsNewMoon), e => Assert.Equal(0, ElongationAt(e.Day), 1e-6));
    }

    [Fact]
    public void SolarEclipses_HappenOnlyInEclipseSeasons_AtLeastTwiceAYear()
    {
        for (var year = 0; year < 20; year++)
        {
            var from = year * EclipseSeasons.Year;
            var events = EclipseSeasons.Events(from, from + EclipseSeasons.Year).ToList();
            var solar = events.Where(e => e.Kind == SeasonEventKind.SolarEclipse).ToList();

            Assert.InRange(solar.Count, 2, 5);
            Assert.All(solar, e => Assert.True(EclipseSeasons.InSeason(e.Day)));

            // Most new moons miss.
            Assert.True(events.Count(e => e.IsNewMoon && e.Kind == SeasonEventKind.None) >= 7);
        }
    }

    [Fact]
    public void Classify_UsesTheEclipseLimits()
    {
        Assert.Equal(SeasonEventKind.SolarEclipse, EclipseSeasons.Classify(isNewMoon: true, 1.2));
        Assert.Equal(SeasonEventKind.None, EclipseSeasons.Classify(isNewMoon: true, -2));
        Assert.Equal(SeasonEventKind.LunarEclipse, EclipseSeasons.Classify(isNewMoon: false, -0.5));
        Assert.Equal(SeasonEventKind.PenumbralLunarEclipse, EclipseSeasons.Classify(isNewMoon: false, 1.3));
        Assert.Equal(SeasonEventKind.None, EclipseSeasons.Classify(isNewMoon: false, 3));
    }

    [Fact]
    public void NextSeasonStart_FindsTheStartOfTheNextSeason()
    {
        var start = EclipseSeasons.NextSeasonStart(0);

        Assert.True(start > 0);
        Assert.True(EclipseSeasons.InSeason(start));
        Assert.False(EclipseSeasons.InSeason(start - 0.5));
        Assert.True(EclipseSeasons.NextSeasonStart(start + 1) > start + 100);
    }

    private static double ElongationAt(double day)
    {
        var elongation = EclipseSeasons.MoonLongitude(day) - EclipseSeasons.SunLongitude(day);
        return elongation - (360 * Math.Round(elongation / 360));
    }
}
