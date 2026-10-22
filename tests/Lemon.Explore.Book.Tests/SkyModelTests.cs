using Lemon.Explore.Book.Chapters.StarTrails;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class SkyModelTests
{
    [Theory]
    [InlineData(28.2)]
    [InlineData(51.5)]
    [InlineData(5)]
    public void PolarisStandsAsHighAsTheLatitude(double latitude)
    {
        var polaris = StarCatalog.Get("Polaris");
        for (var lst = 0.0; lst < 24; lst += 1.5)
        {
            var p = SkyModel.ToHorizontal(polaris.Ra, polaris.Dec, lst, latitude);
            Assert.InRange(p.Altitude, latitude - 0.8, latitude + 0.8);
            Assert.True(p.Azimuth < 2 || p.Azimuth > 358, $"{p.Azimuth}");
        }
    }

    [Fact]
    public void AStarOnTheMeridianIsDueSouthAtItsHighest()
    {
        var sirius = StarCatalog.Get("Sirius");
        var p = SkyModel.ToHorizontal(sirius.Ra, sirius.Dec, sirius.Ra, 28.2);
        Assert.Equal(180, p.Azimuth, 3);
        Assert.Equal(SkyModel.HighestAltitude(sirius.Dec, 28.2), p.Altitude, 6);
        Assert.Equal(90 - 28.2 - 16.716, p.Altitude, 6);

        // Six sidereal hours later a star on the equator sets due west; six hours before it rises due east.
        Assert.Equal(270, SkyModel.ToHorizontal(0, 0, 6, 28.2).Azimuth, 6);
        Assert.Equal(0, SkyModel.ToHorizontal(0, 0, 6, 28.2).Altitude, 6);
        Assert.Equal(90, SkyModel.ToHorizontal(0, 0, -6, 28.2).Azimuth, 6);
    }

    [Fact]
    public void WhichStarsNeverSetDependsOnTheLatitude()
    {
        // The Big Dipper's Dubhe just dips under the northern horizon from 28° N, but never sets from 40° N.
        Assert.Equal(StarPath.RisesAndSets, SkyModel.PathOf(StarCatalog.Get("Dubhe").Dec, 28.2));
        Assert.Equal(StarPath.Circumpolar, SkyModel.PathOf(StarCatalog.Get("Dubhe").Dec, 40));
        Assert.Equal(StarPath.Circumpolar, SkyModel.PathOf(89.26, 28.2));
        Assert.Equal(StarPath.RisesAndSets, SkyModel.PathOf(-16.7, 28.2));
        Assert.Equal(StarPath.NeverRises, SkyModel.PathOf(-63.1, 28.2));
        Assert.Equal(StarPath.RisesAndSets, SkyModel.PathOf(-63.1, 20));

        // At the pole half the sky never rises; at the equator every star rises and sets.
        Assert.Equal(0.5, SkyModel.VisibleShare(90), 9);
        Assert.Equal(1, SkyModel.VisibleShare(0), 9);
        Assert.Equal(StarPath.RisesAndSets, SkyModel.PathOf(80, 0));
    }

    [Fact]
    public void HoursAboveTheHorizon()
    {
        // On the celestial equator: half the time, anywhere.
        Assert.Equal(SkyModel.SiderealDay / 2, SkyModel.HoursAbove(0, 28.2), 6);
        Assert.Equal(SkyModel.SiderealDay, SkyModel.HoursAbove(70, 28.2), 6);
        Assert.Equal(0, SkyModel.HoursAbove(-70, 28.2), 6);

        // Further north, longer up; and it agrees with stepping the star through a day.
        var sampled = Enumerable.Range(0, 24000).Count(i => SkyModel.ToHorizontal(0, 30, i / 1000.0, 28.2).Altitude > 0) / 1000.0 / SkyModel.SiderealRate;
        Assert.Equal(SkyModel.HoursAbove(30, 28.2), sampled, 1);
    }

    [Fact]
    public void TheStarsComeBackFourMinutesSoonerEachDay()
    {
        var first = SkyModel.TransitTime(6.75, 100);
        var next = SkyModel.TransitTime(6.75, 101);
        Assert.Equal(-3.93 / 60, next - first, 2);

        // Midnight sidereal time is about the Sun's right ascension plus 12 h (here within 20 minutes all year).
        for (var day = 1; day <= 365; day += 7)
        {
            var difference = SkyModel.Wrap24(SkyModel.SiderealTime(day, 0) - SkyModel.SunRa(day)) - 12;
            Assert.InRange(difference, -0.34, 0.34);
        }

        // Orion stands south at midnight in December, Scorpius in June.
        double FromMidnight(double transit) => SkyModel.Wrap24(transit + 12) - 12;
        Assert.InRange(FromMidnight(SkyModel.TransitTime(5.6, 349)), -1.5, 1.5);
        Assert.InRange(FromMidnight(SkyModel.TransitTime(16.5, 170)), -1.5, 1.5);
    }

    [Fact]
    public void TheStereographicCameraKeepsCirclesRound()
    {
        var center = new Horizontal(40, 10);
        var straight = SkyModel.Stereographic(center, center);
        Assert.Equal(0, straight.X, 9);
        Assert.Equal(0, straight.Y, 9);

        // A point 60° to the right lands at 2·tan 30°; up is up.
        var right = SkyModel.Stereographic(new Horizontal(0, 90), new Horizontal(0, 30));
        Assert.Equal(2 * Math.Tan(Math.PI / 6), right.X, 6);
        Assert.Equal(0, right.Y, 6);
        Assert.True(SkyModel.Stereographic(new Horizontal(30, 0), new Horizontal(0, 0)).Y > 0);

        // A star's daily path projects to a circle: every point at the same distance from a centre.
        var dubhe = StarCatalog.Get("Dubhe");
        var points = Enumerable.Range(0, 24).Select(i => SkyModel.ToHorizontal(dubhe.Ra, dubhe.Dec, i, 28.2))
            .Select(p => SkyModel.Stereographic(p, new Horizontal(25, 0))).ToList();
        var (a, b, c) = (points[0], points[8], points[16]);
        var d = 2 * ((a.X * (b.Y - c.Y)) + (b.X * (c.Y - a.Y)) + (c.X * (a.Y - b.Y)));
        var cx = (((a.X * a.X) + (a.Y * a.Y)) * (b.Y - c.Y) + ((b.X * b.X) + (b.Y * b.Y)) * (c.Y - a.Y) + ((c.X * c.X) + (c.Y * c.Y)) * (a.Y - b.Y)) / d;
        var cy = (((a.X * a.X) + (a.Y * a.Y)) * (c.X - b.X) + ((b.X * b.X) + (b.Y * b.Y)) * (a.X - c.X) + ((c.X * c.X) + (c.Y * c.Y)) * (b.X - a.X)) / d;
        var radius = Math.Sqrt(((a.X - cx) * (a.X - cx)) + ((a.Y - cy) * (a.Y - cy)));
        foreach (var p in points) Assert.Equal(radius, Math.Sqrt(((p.X - cx) * (p.X - cx)) + ((p.Y - cy) * (p.Y - cy))), 6);
    }

    [Fact]
    public void TheCatalogueHangsTogether()
    {
        Assert.Equal(StarCatalog.Bright.Count, StarCatalog.Bright.Select(s => s.Id).Distinct().Count());
        foreach (var figure in StarCatalog.Figures)
        {
            foreach (var (a, b) in figure.Lines)
            {
                Assert.NotNull(StarCatalog.Find(a));
                Assert.NotNull(StarCatalog.Find(b));
            }
        }

        Assert.Equal(StarCatalog.Fill().Select(s => (s.Ra, s.Dec)), StarCatalog.Fill().Select(s => (s.Ra, s.Dec)));
        Assert.Equal(-1.46, StarCatalog.Bright.Min(s => s.Magnitude));

        // The Sun crosses the equator around 20 March (a circular orbit: within a degree) and is highest in late June.
        Assert.InRange(SkyModel.SunDec(80), -1.0, 1.0);
        Assert.InRange(SkyModel.SunDec(172), 23.2, 23.5);
    }
}
