using Lemon.Explore.Book.Chapters.MoonPhases;
using Lemon.Explore.Book.Chapters.Tides;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class TideGeometryTests
{
    private const double Month = MoonPhaseGeometry.SynodicMonth;

    [Fact]
    public void TheSunRaisesAbout46PercentOfTheMoonsTide()
    {
        Assert.InRange(TideGeometry.SolarRatio, 0.45, 0.47);

        // ...although its pull on Earth is about 178 times the Moon's.
        var pull = TideGeometry.Gravity(TideGeometry.SunMassKg, TideGeometry.SunDistanceKm)
                   / TideGeometry.Gravity(TideGeometry.MoonMassKg, TideGeometry.MoonDistanceKm);
        Assert.InRange(pull, 170, 185);

        var tide = TideGeometry.TidalAcceleration(TideGeometry.SunMassKg, TideGeometry.SunDistanceKm)
                   / TideGeometry.TidalAcceleration(TideGeometry.MoonMassKg, TideGeometry.MoonDistanceKm);
        Assert.InRange(tide, 0.43, 0.47);
    }

    [Fact]
    public void TheMoonsTidalAcceleration_IsAboutOneTenMillionthOfEarthsGravity()
    {
        var near = TideGeometry.TidalAcceleration(TideGeometry.MoonMassKg, TideGeometry.MoonDistanceKm);
        var far = TideGeometry.TidalAcceleration(TideGeometry.MoonMassKg, TideGeometry.MoonDistanceKm, farSide: true);
        var approx = TideGeometry.TidalAccelerationApprox(TideGeometry.MoonMassKg, TideGeometry.MoonDistanceKm);

        Assert.InRange(approx, 1.08e-6, 1.12e-6);
        Assert.InRange(near / approx, 1.0, 1.06);
        Assert.InRange(far / approx, 0.94, 1.0);
        Assert.InRange(approx / 9.81, 1.0e-7, 1.2e-7);
    }

    [Fact]
    public void TheEquilibriumTide_IsHalfAMetreFromTheMoonAndAQuarterFromTheSun()
    {
        Assert.InRange(TideGeometry.EquilibriumRange(TideGeometry.MoonMassKg, TideGeometry.MoonDistanceKm), 0.52, 0.56);
        Assert.InRange(TideGeometry.EquilibriumRange(TideGeometry.SunMassKg, TideGeometry.SunDistanceKm), 0.23, 0.27);
    }

    [Fact]
    public void TheTidalForce_PushesOutUnderTheMoonAndOppositeIt_AndInHalfwayBetween()
    {
        Assert.Equal((2.0, 0.0), Round(TideGeometry.TidalForce(0)));
        Assert.Equal((2.0, 0.0), Round(TideGeometry.TidalForce(180)));
        Assert.Equal((-1.0, 0.0), Round(TideGeometry.TidalForce(90)));

        // At 45° it is mostly along the surface, back towards the point under the Moon.
        var (radial, tangential) = TideGeometry.TidalForce(45);
        Assert.Equal(0.5, radial, 9);
        Assert.Equal(-1.5, tangential, 9);

        static (double, double) Round((double R, double T) f) => (Math.Round(f.R, 9), Math.Round(f.T, 9) + 0.0);
    }

    [Fact]
    public void ALunarDay_Is24Hours50Minutes_AndHighTidesComeAbout12Hours25MinutesApart()
    {
        Assert.InRange((TideGeometry.LunarDayHours - 24) * 60, 50, 51);
        Assert.InRange(TideGeometry.TidePeriodHours, 12.41, 12.43);
    }

    [Fact]
    public void AtNewMoonMidnight_TheMoonIsUnderfoot_AndTheWaterIsAtItsHighest()
    {
        Assert.Equal(180, Math.Abs(TideGeometry.MoonHourAngle(0)), 9);
        Assert.Equal(1 + TideGeometry.SolarRatio, TideGeometry.Height(0), 9);
        Assert.Equal(TideStage.High, TideGeometry.StageAt(0));

        // Six hours later it is low water, the Moon and the Sun both on the horizon.
        Assert.Equal(TideStage.Low, TideGeometry.StageAt(0.25));
        Assert.Equal(TideStage.Falling, TideGeometry.StageAt(0.125));
    }

    [Fact]
    public void TheRange_IsGreatestAtNewAndFullMoon_AndLeastAtTheQuarters()
    {
        Assert.Equal(TideGeometry.SpringRange, TideGeometry.Range(0), 9);
        Assert.Equal(TideGeometry.SpringRange, TideGeometry.Range(Month / 2), 9);
        Assert.Equal(TideGeometry.NeapRange, TideGeometry.Range(Month / 4), 9);
        Assert.Equal(TideGeometry.NeapRange, TideGeometry.Range(Month * 3 / 4), 9);

        // Spring tides are about 2.7 times the neap tides.
        Assert.InRange(TideGeometry.SpringRange / TideGeometry.NeapRange, 2.6, 2.8);
        Assert.Equal(1, TideGeometry.SpringFraction(0), 9);
        Assert.Equal(0, TideGeometry.SpringFraction(Month / 4), 9);

        Assert.False(TideGeometry.IsRangeGrowing(Month / 8));
        Assert.True(TideGeometry.IsRangeGrowing(Month * 3 / 8));
    }

    [Fact]
    public void TheModelsDailyRange_MatchesTheHighsAndLowsItProduces()
    {
        foreach (var day in new[] { 0.0, 3, Month / 4, 11, Month / 2 })
        {
            var turns = TideGeometry.Turns(day, day + 1.2);
            var highs = turns.Where(t => t.IsHigh).Select(t => t.Height).ToList();
            var lows = turns.Where(t => !t.IsHigh).Select(t => t.Height).ToList();
            var range = TideGeometry.Range(day + 0.6);
            Assert.InRange(highs.Average() - lows.Average(), range - 0.08, range + 0.08);
        }
    }

    [Fact]
    public void TheTurns_AlternateHighAndLow_TwiceADay()
    {
        var turns = TideGeometry.Turns(0.1, Month + 0.1);
        for (var i = 1; i < turns.Count; i++) Assert.NotEqual(turns[i - 1].IsHigh, turns[i].IsHigh);

        // Two highs per lunar day: one fewer pair per month than there are solar days.
        var highs = turns.Where(t => t.IsHigh).ToList();
        Assert.InRange(highs.Count, 56, 58);

        // On average 12 h 25 min apart; with the Sun's tide added, a little more or less.
        var gaps = highs.Zip(highs.Skip(1), (a, b) => (b.Day - a.Day) * 24).ToList();
        Assert.Equal(TideGeometry.TidePeriodHours, gaps.Average(), 1);
        Assert.InRange(gaps.Min(), 11.5, 12.42);
        Assert.InRange(gaps.Max(), 12.42, 13.5);
    }

    [Fact]
    public void WithTheMoonAlone_HighTideIsWhenTheMoonIsHighestOrUnderfoot_50MinutesLaterEachDay()
    {
        var highs = TideGeometry.Turns(2, 4, includeSun: false).Where(t => t.IsHigh).ToList();
        foreach (var high in highs)
        {
            var hourAngle = Math.Abs(TideGeometry.MoonHourAngle(high.Day));
            Assert.True(hourAngle < 0.01 || hourAngle > 179.99, $"hour angle {hourAngle}");
            Assert.Equal(1, high.Height, 9);
        }

        var later = (highs[2].Day - highs[0].Day - 1) * 1440;
        Assert.InRange(later, 50, 51);
    }

    [Fact]
    public void Next_FindsTheComingHighAndLow()
    {
        var high = TideGeometry.Next(0.01, high: true);
        var low = TideGeometry.Next(0.01, high: false);
        Assert.True(high.IsHigh);
        Assert.False(low.IsHigh);
        Assert.InRange(low.Day * 24, 5.9, 6.3);
        Assert.InRange(high.Day * 24, 12, 12.6);
    }

    [Fact]
    public void CoastHeights_RunFromZeroAtTheLowestLowWater()
    {
        Assert.Equal(2 * (1 + TideGeometry.SolarRatio), TideGeometry.CoastHeight(0), 9);
        Assert.InRange(TideGeometry.CoastHeight(0.25), 0, 0.02);
        Assert.Equal(TideGeometry.CoastHeight(1.3), TideGeometry.ToCoastMetres(TideGeometry.Height(1.3)), 9);
    }
}
