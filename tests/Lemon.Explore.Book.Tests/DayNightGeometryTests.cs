using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Chapters.Seasons;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class DayNightGeometryTests
{
    [Theory]
    [InlineData(SeasonEvent.MarchEquinox, "2026-03-20T14:46:00Z")]
    [InlineData(SeasonEvent.JuneSolstice, "2026-06-21T08:24:00Z")]
    [InlineData(SeasonEvent.SeptemberEquinox, "2026-09-23T00:05:00Z")]
    [InlineData(SeasonEvent.DecemberSolstice, "2026-12-21T20:50:00Z")]
    public void EquinoxesAndSolstices_MatchPublishedTimes(SeasonEvent e, string published)
    {
        var expected = DateTime.Parse(published, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        var actual = DayNightGeometry.EventTime(2026, e);

        Assert.InRange((actual - expected).TotalMinutes, -30, 30);
    }

    [Fact]
    public void Declination_IsTheTiltAtTheJuneSolstice()
    {
        var sun = DayNightGeometry.Sun(DayNightGeometry.EventTime(2026, SeasonEvent.JuneSolstice));

        Assert.Equal(23.44, sun.Declination, 1);
    }

    [Fact]
    public void EarlyNovember_SundialsRunAboutSixteenMinutesFast()
    {
        // The Sun crosses Greenwich about 16 minutes before 12:00 UTC, so at noon it is already 4° west.
        var sun = DayNightGeometry.Sun(new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc));

        Assert.InRange(sun.EquationOfTime, 16.0, 16.8);
        Assert.InRange(sun.SubsolarLongitude, -4.3, -3.9);
        Assert.Equal(12 + (sun.EquationOfTime / 60), DayNightGeometry.SolarTime(sun, 0), 3);
    }

    [Theory]
    [InlineData("Beijing", "2026-06-21", "04:46", "19:46")]
    [InlineData("London", "2026-01-01", "08:06", "16:02")]
    [InlineData("Sydney", "2026-12-21", "05:41", "20:05")]
    public void SunriseAndSunset_MatchPublishedTimes(string cityId, string date, string sunrise, string sunset)
    {
        var city = WorldCities.Find(cityId)!;

        var events = DayNightGeometry.EventsOn(DateOnly.Parse(date), city.TimeZone, city.Latitude, city.Longitude);

        AssertClock(sunrise, events.Rise, city.TimeZone);
        AssertClock(sunset, events.Set, city.TimeZone);
    }

    [Fact]
    public void Tromso_HasPolarNightInDecemberAndMidnightSunInJune()
    {
        var tromso = WorldCities.Find("Tromso")!;

        var december = DayNightGeometry.EventsOn(new DateOnly(2026, 12, 21), tromso.TimeZone, tromso.Latitude, tromso.Longitude);
        var june = DayNightGeometry.EventsOn(new DateOnly(2026, 6, 21), tromso.TimeZone, tromso.Latitude, tromso.Longitude);

        Assert.True(december.AlwaysDown);
        Assert.Null(december.Rise);
        Assert.Equal(TimeSpan.Zero, december.Daylight);
        Assert.True(june.AlwaysUp);
        Assert.Equal(TimeSpan.FromHours(24), june.Daylight);
    }

    [Fact]
    public void Daylight_IsAboutTwelveHoursAtTheEquinoxAnywhere()
    {
        var events = DayNightGeometry.Events(40, 0, new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc));

        // A few minutes over twelve: refraction and the Sun's disc.
        Assert.InRange(events.Daylight.TotalHours, 12.0, 12.25);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-6.0)]
    [InlineData(-18.0)]
    public void Circle_IsWhereTheSunStandsAtThatAltitude(double altitude)
    {
        var sun = DayNightGeometry.Sun(new DateTime(2026, 6, 21, 8, 0, 0, DateTimeKind.Utc));

        var points = DayNightGeometry.Circle(sun, altitude, 90);

        Assert.All(points, p => Assert.Equal(altitude, DayNightGeometry.Altitude(sun, p.Latitude, p.Longitude), 6));
    }

    [Fact]
    public void DayNightLine_ReachesThePolarCirclesAtTheSolstice()
    {
        var sun = DayNightGeometry.Sun(DayNightGeometry.EventTime(2026, SeasonEvent.JuneSolstice));

        var points = DayNightGeometry.Circle(sun, 0, 720);

        Assert.Equal(90 - 23.44, points.Max(p => p.Latitude), 0);
        Assert.Equal(-(90 - 23.44), points.Min(p => p.Latitude), 0);
    }

    [Fact]
    public void Kashgar_RunsOnBeijingTimeAlmostThreeHoursAheadOfTheSun()
    {
        var kashgar = WorldCities.Find("Kashgar")!;
        var utc = new DateTime(2026, 4, 15, 4, 0, 0, DateTimeKind.Utc);
        var clock = TimeZoneInfo.ConvertTimeFromUtc(utc, kashgar.TimeZone);

        var solar = DayNightGeometry.SolarTime(DayNightGeometry.Sun(utc), kashgar.Longitude);

        Assert.Equal(12, clock.Hour);
        Assert.InRange(clock.TimeOfDay.TotalHours - solar, 2.7, 3.0);
    }

    [Theory]
    [InlineData(-0.5, -12, DayPhase.Sunrise)]
    [InlineData(-0.5, 12, DayPhase.Sunset)]
    [InlineData(-3, -100, DayPhase.Dawn)]
    [InlineData(-3, 100, DayPhase.Dusk)]
    [InlineData(-9, 100, DayPhase.NauticalTwilight)]
    [InlineData(-15, 100, DayPhase.AstronomicalTwilight)]
    [InlineData(-30, 170, DayPhase.Night)]
    [InlineData(40, 0, DayPhase.Day)]
    public void PhaseOf_FollowsTheSunsAltitude(double altitude, double hourAngle, DayPhase expected)
    {
        Assert.Equal(expected, DayNightGeometry.PhaseOf(altitude, hourAngle));
    }

    [Theory]
    [InlineData(116.4, 8)]
    [InlineData(-7.4, 0)]
    [InlineData(7.6, 1)]
    [InlineData(-179, -12)]
    [InlineData(179, 12)]
    public void NominalZone_IsOneHourPerFifteenDegrees(double longitude, int zone)
    {
        Assert.Equal(zone, DayNightGeometry.NominalZone(longitude));
    }

    [Fact]
    public void Zones_KnowDaylightSavingAndOddOffsets()
    {
        var instant = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(TimeSpan.FromHours(8), WorldCities.Find("Beijing")!.TimeZone.GetUtcOffset(instant));
        Assert.Equal(TimeSpan.FromHours(11), WorldCities.Find("Sydney")!.TimeZone.GetUtcOffset(instant));
        Assert.Equal(new TimeSpan(5, 45, 0), WorldCities.Find("Kathmandu")!.TimeZone.GetUtcOffset(instant));
        Assert.Equal(TimeSpan.FromHours(14), WorldCities.Find("Kiritimati")!.TimeZone.GetUtcOffset(instant));
        Assert.Equal("UTC+5:45", WorldCities.OffsetText(new TimeSpan(5, 45, 0)));
        Assert.Equal("UTC−3", WorldCities.OffsetText(TimeSpan.FromHours(-3)));
    }

    [Theory]
    [InlineData(39.9, 116.4, true)]     // Beijing
    [InlineData(23, 10, true)]          // Sahara
    [InlineData(-85, 0, true)]          // Antarctica
    [InlineData(-25, 135, true)]        // Australia
    [InlineData(0, -150, false)]        // Pacific
    [InlineData(30, -40, false)]        // Atlantic
    [InlineData(-30, 80, false)]        // Indian Ocean
    public void LandMask_KnowsLandFromSea(double latitude, double longitude, bool land)
    {
        var share = WorldMap.LandMask.Sample(latitude, longitude);

        Assert.Equal(land, share > 0.5);
    }

    [Fact]
    public void Land_HasEveryContinent()
    {
        Assert.InRange(WorldMap.Land.Count, 100, 200);
        Assert.Contains(WorldMap.Land, ring => ring.Any(p => p.Latitude < -89));
    }

    private static void AssertClock(string expected, DateTime? utc, TimeZoneInfo zone)
    {
        Assert.NotNull(utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc!.Value, zone);
        var wanted = TimeSpan.Parse(expected);
        Assert.InRange((local.TimeOfDay - wanted).TotalMinutes, -2.5, 2.5);
    }
}
