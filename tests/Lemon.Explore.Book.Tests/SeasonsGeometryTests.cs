using Lemon.Explore.Book.Chapters.Seasons;
using Xunit;

namespace Lemon.Explore.Book.Tests.Chapters;

public sealed class SeasonsGeometryTests
{
    private const double Tilt = SeasonsGeometry.EarthTilt;

    [Theory]
    [InlineData(SeasonEvent.MarchEquinox, 3, 20)]
    [InlineData(SeasonEvent.JuneSolstice, 6, 21)]
    [InlineData(SeasonEvent.SeptemberEquinox, 9, 23)]
    [InlineData(SeasonEvent.DecemberSolstice, 12, 21)]
    public void EquinoxesAndSolstices_FallOnTheirCalendarDates(SeasonEvent e, int month, int day)
    {
        var date = SeasonsGeometry.DateOf(SeasonsGeometry.EventDay(e));

        Assert.Equal(month, date.Month);
        Assert.InRange(date.Day, day - 1, day + 1);
    }

    [Fact]
    public void Declination_IsTheTiltAtTheSolsticesAndZeroAtTheEquinoxes()
    {
        Assert.Equal(Tilt, SeasonsGeometry.Declination(SeasonsGeometry.EventDay(SeasonEvent.JuneSolstice), Tilt), 2);
        Assert.Equal(-Tilt, SeasonsGeometry.Declination(SeasonsGeometry.EventDay(SeasonEvent.DecemberSolstice), Tilt), 2);
        Assert.Equal(0, SeasonsGeometry.Declination(SeasonsGeometry.EventDay(SeasonEvent.MarchEquinox), Tilt), 2);

        // Without a tilt the Sun stays over the equator all year.
        Assert.Equal(0, SeasonsGeometry.Declination(SeasonsGeometry.EventDay(SeasonEvent.JuneSolstice), 0), 9);
    }

    [Fact]
    public void Earth_IsClosestInEarlyJanuaryAndFarthestInEarlyJuly()
    {
        var perihelion = SeasonsGeometry.DateOf(SeasonsGeometry.PerihelionDay());
        var aphelion = SeasonsGeometry.DateOf(SeasonsGeometry.AphelionDay());

        Assert.Equal(1, perihelion.Month);
        Assert.InRange(perihelion.Day, 2, 5);
        Assert.Equal(7, aphelion.Month);
        Assert.InRange(aphelion.Day, 2, 6);

        var km = SeasonsGeometry.SunDistance(SeasonsGeometry.PerihelionDay()) * SeasonsGeometry.AstronomicalUnitKm / 1e6;
        Assert.InRange(km, 146.9, 147.3);
        km = SeasonsGeometry.SunDistance(SeasonsGeometry.AphelionDay()) * SeasonsGeometry.AstronomicalUnitKm / 1e6;
        Assert.InRange(km, 151.9, 152.3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(40)]
    [InlineData(-60)]
    public void AtTheEquinoxes_DayAndNightAreEqualEverywhere(double latitude)
    {
        Assert.Equal(12, SeasonsGeometry.DayLength(latitude, 0), 6);
    }

    [Fact]
    public void DayLength_FollowsTheSeasonsAndReachesPolarDayAndNight()
    {
        // Beijing: about 15 hours at the June solstice, about 9 at the December one.
        Assert.InRange(SeasonsGeometry.DayLength(39.9, Tilt), 14.6, 15.0);
        Assert.InRange(SeasonsGeometry.DayLength(39.9, -Tilt), 9.0, 9.4);

        // The equator always has 12 hours.
        Assert.Equal(12, SeasonsGeometry.DayLength(0, Tilt), 6);

        // Inside the Arctic Circle: polar day in June, polar night in December; the North Pole too.
        Assert.Equal(24, SeasonsGeometry.DayLength(69.6, Tilt));
        Assert.Equal(0, SeasonsGeometry.DayLength(69.6, -Tilt));
        Assert.Equal(24, SeasonsGeometry.DayLength(90, Tilt));
        Assert.Equal(0, SeasonsGeometry.DayLength(90, -Tilt));
    }

    [Fact]
    public void NoonAltitude_Is90MinusTheDistanceToTheOverheadLatitude()
    {
        Assert.Equal(90, SeasonsGeometry.NoonAltitude(Tilt, Tilt), 9);
        Assert.Equal(90 - 39.9 + Tilt, SeasonsGeometry.NoonAltitude(39.9, Tilt), 9);
        Assert.Equal(90 - 39.9 - Tilt, SeasonsGeometry.NoonAltitude(39.9, -Tilt), 9);
        Assert.Equal(SeasonsGeometry.NoonAltitude(39.9, Tilt), SeasonsGeometry.Altitude(39.9, Tilt, 0), 6);
    }

    [Fact]
    public void SunDirection_RisesDueEastAndSetsDueWestAtTheEquinox()
    {
        var sunrise = SeasonsGeometry.SunDirection(40, 0, -90);
        var sunset = SeasonsGeometry.SunDirection(40, 0, 90);

        Assert.Equal(0, sunrise.Up, 9);
        Assert.Equal(1, sunrise.East, 9);
        Assert.Equal(-1, sunset.East, 9);

        // At noon in the northern hemisphere the Sun is due south.
        var noon = SeasonsGeometry.SunDirection(40, 0, 0);
        Assert.Equal(0, noon.East, 9);
        Assert.True(noon.North < 0);
    }

    [Fact]
    public void DailySunlight_IsOneAtTheEquatorAtAnEquinoxAndGreaterInSummer()
    {
        Assert.Equal(1, SeasonsGeometry.DailySunlight(0, 0), 9);
        Assert.True(SeasonsGeometry.DailySunlight(40, Tilt) > 2 * SeasonsGeometry.DailySunlight(40, -Tilt));
        Assert.Equal(0, SeasonsGeometry.DailySunlight(80, -Tilt));

        // Closer to the Sun, a little more sunlight.
        Assert.True(SeasonsGeometry.DailySunlight(40, 0, 0.983) > SeasonsGeometry.DailySunlight(40, 0, 1.017));
    }

    [Fact]
    public void Seasons_AreOppositeInTheTwoHemispheres()
    {
        var june = SeasonsGeometry.SunLongitude(SeasonsGeometry.EventDay(SeasonEvent.JuneSolstice) + 10);

        Assert.Equal(Season.Summer, SeasonsGeometry.SeasonAt(june, north: true));
        Assert.Equal(Season.Winter, SeasonsGeometry.SeasonAt(june, north: false));
        Assert.Equal(Season.Spring, SeasonsGeometry.SeasonAt(10, north: true));
        Assert.Equal(Season.Autumn, SeasonsGeometry.SeasonAt(10, north: false));
    }

    [Fact]
    public void DayOfLongitude_InvertsSunLongitude()
    {
        for (var longitude = 0; longitude < 360; longitude += 15)
        {
            var day = SeasonsGeometry.DayOfLongitude(longitude);
            Assert.Equal(0, SeasonsGeometry.Normalize180(SeasonsGeometry.SunLongitude(day) - longitude), 4);
        }
    }
}
