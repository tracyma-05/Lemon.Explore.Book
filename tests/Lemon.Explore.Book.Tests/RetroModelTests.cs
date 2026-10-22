using Lemon.Explore.Book.Chapters.Retrograde;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class RetroModelTests
{
    /// <summary>
    /// Published retrograde periods (stations and the opposition or inferior conjunction, geocentric, UT dates). The model
    /// is geometric and uses Earth–Moon barycentre elements, so a station may land a day or two off.
    /// </summary>
    [Theory]
    [InlineData(RetroPlanet.Mars, "2024-12-06", "2025-02-24", "2025-01-16")]
    [InlineData(RetroPlanet.Mars, "2022-10-30", "2023-01-12", "2022-12-08")]
    [InlineData(RetroPlanet.Mars, "2020-09-09", "2020-11-13", "2020-10-13")]
    [InlineData(RetroPlanet.Jupiter, "2024-10-09", "2025-02-04", "2024-12-07")]
    [InlineData(RetroPlanet.Saturn, "2025-07-13", "2025-11-28", "2025-09-21")]
    [InlineData(RetroPlanet.Venus, "2025-03-02", "2025-04-13", "2025-03-23")]
    [InlineData(RetroPlanet.Mercury, "2025-03-15", "2025-04-07", "2025-03-24")]
    public void FindsPublishedRetrogradePeriods(RetroPlanet planet, string start, string end, string middle)
    {
        var period = RetroModel.Apparition(planet, RetroModel.DayOf(DateTime.Parse(middle)));
        AssertNear(start, period.Start, 2.5);
        AssertNear(end, period.End, 2.5);
        AssertNear(middle, period.Middle, 1.5);
    }

    [Theory]
    [InlineData(RetroPlanet.Mercury)]
    [InlineData(RetroPlanet.Venus)]
    [InlineData(RetroPlanet.Mars)]
    [InlineData(RetroPlanet.Jupiter)]
    [InlineData(RetroPlanet.Saturn)]
    public void LoopsComeOncePerSynodicPeriod(RetroPlanet planet)
    {
        var periods = RetroModel.Periods(planet);
        var synodic = RetroModel.SynodicDays(planet);
        Assert.InRange(periods.Count, (RetroModel.MaxDay - RetroModel.MinDay) / synodic - 2, (RetroModel.MaxDay - RetroModel.MinDay) / synodic + 1);
        foreach (var (a, b) in periods.Zip(periods.Skip(1)))
        {
            // Mercury's and Mars's gaps vary with their eccentric orbits.
            Assert.InRange(b.Middle - a.Middle, synodic * 0.85, synodic * 1.15);
        }
    }

    [Theory]
    [InlineData(RetroPlanet.Mercury, 18, 26)]
    [InlineData(RetroPlanet.Venus, 39, 44)]
    [InlineData(RetroPlanet.Mars, 58, 82)]
    [InlineData(RetroPlanet.Jupiter, 116, 125)]
    [InlineData(RetroPlanet.Saturn, 132, 143)]
    public void LoopsLastAsLongAsPublished(RetroPlanet planet, double shortest, double longest)
    {
        var periods = RetroModel.Periods(planet);
        var (min, max) = (periods.Min(p => p.Duration), periods.Max(p => p.Duration));
        Assert.True(min >= shortest && max <= longest, $"{min:0.0} – {max:0.0} days");
        Assert.All(periods, p => Assert.InRange(p.Middle, p.Start, p.End));
    }

    [Theory]
    [InlineData(RetroPlanet.Mars)]
    [InlineData(RetroPlanet.Jupiter)]
    [InlineData(RetroPlanet.Saturn)]
    public void AnOuterPlanetGoesBackOnlyOppositeTheSun(RetroPlanet planet)
    {
        foreach (var period in RetroModel.Periods(planet).Take(10))
        {
            for (var day = period.Start + 1; day < period.End; day += 3)
            {
                Assert.True(RetroModel.Rate(planet, day) < 0);
                Assert.True(Math.Abs(RetroModel.Elongation(planet, day)) > 100, $"{RetroModel.DateOf(day):d}");
            }

            Assert.Equal(180, Math.Abs(RetroModel.Elongation(planet, period.Middle)), 1);
            Assert.True(RetroModel.Rate(planet, period.Start - 5) > 0);
            Assert.True(RetroModel.Rate(planet, period.End + 5) > 0);
        }
    }

    [Theory]
    [InlineData(RetroPlanet.Mercury)]
    [InlineData(RetroPlanet.Venus)]
    public void AnInnerPlanetGoesBackWhenItPassesBetweenUsAndTheSun(RetroPlanet planet)
    {
        foreach (var period in RetroModel.Periods(planet).Take(10))
        {
            Assert.Equal(0, RetroModel.Elongation(planet, period.Middle), 1);
            Assert.True(RetroModel.Geo(planet, period.Middle).Distance < 1, "inferior, not superior conjunction");
        }
    }

    [Fact]
    public void TheRateIsZeroAtTheStations()
    {
        foreach (var planet in Enum.GetValues<RetroPlanet>())
        {
            var period = RetroModel.Periods(planet)[5];
            Assert.Equal(0, RetroModel.Rate(planet, period.Start), 3);
            Assert.Equal(0, RetroModel.Rate(planet, period.End), 3);
        }
    }

    [Fact]
    public void VenusComesBackToTheSamePlaceAfterEightYears()
    {
        var day = RetroModel.DayOf(new DateTime(2025, 3, 23));
        var drift = RetroModel.Wrap180(RetroModel.Geo(RetroPlanet.Venus, day + 2920).Longitude - RetroModel.Geo(RetroPlanet.Venus, day).Longitude);
        Assert.InRange(Math.Abs(drift), 0, 4);
    }

    [Fact]
    public void MarsIsClosestNearOpposition()
    {
        var period = RetroModel.Apparition(RetroPlanet.Mars, RetroModel.DayOf(new DateTime(2025, 1, 16)));
        var closest = Enumerable.Range(-60, 121).Select(d => period.Middle + d).MinBy(d => RetroModel.Geo(RetroPlanet.Mars, d).Distance);
        Assert.InRange(closest - period.Middle, -8, 0);

        // 2025 January 12: 96 million km.
        Assert.InRange(RetroModel.Geo(RetroPlanet.Mars, closest).Distance * RetroModel.KmPerAu / 1e6, 94, 98);
    }

    [Theory]
    [InlineData(10.1395, 11.967, 149.8, 0.46)]
    [InlineData(13.4199, -11.161, 203.8, -2.05)]
    [InlineData(4.5987, 16.509, 69.8, -5.47)]
    public void StarsGoOverToEclipticCoordinates(double ra, double dec, double longitude, double latitude)
    {
        var (lon, lat) = RetroModel.Ecliptic(ra, dec);
        Assert.Equal(longitude, lon, 0.3);
        Assert.Equal(latitude, lat, 0.2);
    }

    [Theory]
    [InlineData(0, "Pisces")]
    [InlineData(355, "Pisces")]
    [InlineData(30, "Aries")]
    [InlineData(149.8, "Leo")]
    [InlineData(203.8, "Virgo")]
    [InlineData(250, "Ophiuchus")]
    [InlineData(340, "Aquarius")]
    public void KnowsTheConstellationOnTheEcliptic(double longitude, string id) =>
        Assert.Equal(id, ZodiacSky.ConstellationAt(longitude));

    [Fact]
    public void TheSunGoesRoundTheEclipticInAYear()
    {
        // The March equinox 2025 (March 20, 09:01 UT): the Sun at longitude 0, give or take precession since J2000.
        var day = RetroModel.DayOf(new DateTime(2025, 3, 20, 9, 0, 0));
        Assert.InRange(RetroModel.Wrap180(RetroModel.SunLongitude(day)), -0.6, 0.1);
        Assert.Equal(0.9856, RetroModel.Wrap180(RetroModel.SunLongitude(day + 1) - RetroModel.SunLongitude(day)), 1);
    }

    [Fact]
    public void EarthIsFasterThanMars()
    {
        var day = RetroModel.DayOf(new DateTime(2025, 1, 16));
        Assert.InRange(RetroModel.SpeedKmS(null, day), 29.5, 30.6);
        Assert.InRange(RetroModel.SpeedKmS(RetroPlanet.Mars, day), 21.9, 26.5);
    }

    private static void AssertNear(string expected, double day, double tolerance)
    {
        var miss = (RetroModel.DateOf(day) - DateTime.Parse(expected).AddHours(12)).TotalDays;
        Assert.True(Math.Abs(miss) <= tolerance, $"expected {expected}, got {RetroModel.DateOf(day):yyyy-MM-dd HH:mm} ({miss:+0.0;-0.0} d)");
    }
}
