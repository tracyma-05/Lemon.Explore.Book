using Lemon.Explore.Book.Chapters.MoonPhases;
using Xunit;

namespace Lemon.Explore.Book.Tests.Chapters;

public sealed class MoonPhaseGeometryTests
{
    private const double Month = MoonPhaseGeometry.SynodicMonth;

    [Theory]
    [InlineData(0.0, MoonPhase.New, 0.0)]
    [InlineData(Month / 4, MoonPhase.FirstQuarter, 0.5)]
    [InlineData(Month / 2, MoonPhase.Full, 1.0)]
    [InlineData(Month * 3 / 4, MoonPhase.LastQuarter, 0.5)]
    public void PrincipalPhases_AreLitAsExpected(double age, MoonPhase phase, double lit)
    {
        Assert.Equal(phase, MoonPhaseGeometry.PhaseOf(age));
        Assert.Equal(lit, MoonPhaseGeometry.Illumination(age), 9);
    }

    [Fact]
    public void PhaseOf_NamesTheQuartersForADayEitherSide_AndTheCrescentsAndGibbousPhasesBetween()
    {
        foreach (var phase in Enum.GetValues<MoonPhase>())
        {
            Assert.Equal(phase, MoonPhaseGeometry.PhaseOf(MoonPhaseGeometry.AgeOf(phase)));
            if (!MoonPhaseGeometry.IsPrincipal(phase)) continue;

            var exact = MoonPhaseGeometry.AgeOf(phase);
            var before = (MoonPhase)(((int)phase + 7) % 8);
            var after = (MoonPhase)(((int)phase + 1) % 8);
            Assert.Equal(phase, MoonPhaseGeometry.PhaseOf(exact + 0.95));
            Assert.Equal(phase, MoonPhaseGeometry.PhaseOf(exact - 0.95 + Month));
            Assert.Equal(after, MoonPhaseGeometry.PhaseOf(exact + 1.05));
            Assert.Equal(before, MoonPhaseGeometry.PhaseOf(exact - 1.05 + Month));
        }

        // Half a day before the next new moon is already a new moon.
        Assert.Equal(MoonPhase.New, MoonPhaseGeometry.PhaseOf(Month - 0.5));
        Assert.True(MoonPhaseGeometry.IsWaxing(Month / 4));
        Assert.False(MoonPhaseGeometry.IsWaxing(Month * 3 / 4));
    }

    [Fact]
    public void TheMoonRisesLaterEachDay_TheFullMoonAtSunset()
    {
        Assert.Equal(6, MoonPhaseGeometry.MoonriseHour(0), 9);
        Assert.Equal(12, MoonPhaseGeometry.MoonriseHour(Month / 4), 9);
        Assert.Equal(18, MoonPhaseGeometry.MoonriseHour(Month / 2), 9);
        Assert.Equal(0, MoonPhaseGeometry.MoonriseHour(Month * 3 / 4), 9);
        Assert.Equal(0, MoonPhaseGeometry.TransitHour(Month / 2), 9);

        // About 49 minutes later each day.
        var later = MoonPhaseGeometry.MoonriseHour(1) - MoonPhaseGeometry.MoonriseHour(0);
        Assert.InRange(later * 60, 48, 50);
    }

    [Fact]
    public void TheFirstQuarterMoon_IsDueSouthAtSunset()
    {
        var moon = MoonPhaseGeometry.MoonDirection(40, Month / 4, 18);
        Assert.Equal(180, MoonPhaseGeometry.Azimuth(moon), 6);
        Assert.Equal(50, MoonPhaseGeometry.Altitude(moon), 6);

        // The Sun is setting in the west.
        var sun = MoonPhaseGeometry.SunDirection(40, 18);
        Assert.Equal(0, MoonPhaseGeometry.Altitude(sun), 6);
        Assert.Equal(270, MoonPhaseGeometry.Azimuth(sun), 6);
    }

    [Fact]
    public void ASynodicMonth_IsASiderealMonthPlusEarthsShareOfItsOrbit()
    {
        // After one synodic month the Moon has gone round once against the Sun: once against the stars and as far
        // again as Earth has moved round the Sun.
        var stars = MoonPhaseGeometry.SiderealAngle(Month);
        var sun = MoonPhaseGeometry.EarthOrbitAngle(Month);
        Assert.Equal(360, stars - sun, 2);
        Assert.InRange(sun, 29, 29.2);
    }

    [Theory]
    [InlineData(2024, 1, 4, 3, 30, MoonPhase.LastQuarter)]
    [InlineData(2024, 1, 11, 11, 57, MoonPhase.New)]
    [InlineData(2024, 1, 18, 3, 53, MoonPhase.FirstQuarter)]
    [InlineData(2024, 1, 25, 17, 54, MoonPhase.Full)]
    [InlineData(2024, 4, 8, 18, 21, MoonPhase.New)]
    [InlineData(2025, 9, 7, 18, 9, MoonPhase.Full)]
    public void NextPhase_MatchesPublishedTimes(int year, int month, int day, int hour, int minute, MoonPhase phase)
    {
        var expected = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);

        var actual = MoonPhaseGeometry.NextPhase(expected.AddDays(-3), phase);

        Assert.InRange((actual - expected).TotalMinutes, -10, 10);
    }

    [Fact]
    public void AgeAt_CountsFromTheLastNewMoon()
    {
        var newMoon = new DateTime(2024, 4, 8, 18, 21, 0, DateTimeKind.Utc);

        Assert.InRange(MoonPhaseGeometry.AgeAt(newMoon.AddDays(3)), 2.99, 3.01);
        Assert.InRange((MoonPhaseGeometry.PreviousNewMoon(newMoon.AddHours(1)) - newMoon).TotalMinutes, -10, 10);
    }
}
