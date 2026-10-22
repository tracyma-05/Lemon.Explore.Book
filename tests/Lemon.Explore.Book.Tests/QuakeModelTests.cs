using System;
using System.Linq;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Chapters.Earthquakes;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class QuakeModelTests
{
    #region plates

    [Theory]
    [InlineData(28.23, 112.94, "EU")] // Changsha
    [InlineData(51.51, -0.13, "EU")] // London
    [InlineData(1.35, 103.82, "EU")] // Singapore
    [InlineData(40.71, -74.01, "NA")] // New York
    [InlineData(-33.87, 151.21, "AU")] // Sydney
    [InlineData(28.6, 77.2, "IN")] // Delhi
    [InlineData(19.6, -155.5, "PA")] // Hawaii
    [InlineData(34.05, -118.25, "PA")] // Los Angeles, west of the San Andreas
    [InlineData(-12.05, -77.04, "SA")] // Lima
    [InlineData(30.04, 31.24, "AF")] // Cairo
    [InlineData(24.71, 46.68, "AR")] // Riyadh
    [InlineData(-15, -100, "NZ")]
    [InlineData(10, -95, "CO")]
    [InlineData(15, -75, "CA")]
    [InlineData(20, 135, "PH")]
    [InlineData(-58, -45, "SC")]
    [InlineData(46, -128.5, "JF")]
    [InlineData(-85, 0, "AN")]
    [InlineData(64.13, -21.9, "NA")] // Reykjavík, west of the ridge
    [InlineData(89, 0, "NA")]
    public void EveryPlace_IsOnTheRightPlate(double latitude, double longitude, string plate)
    {
        Assert.Equal(plate, PlateModel.PlateAt(latitude, longitude).Id);
    }

    [Fact]
    public void ThePlates_AreAllThereAndNoneSpillsIntoAnother()
    {
        var grid = PlateModel.PlateGrid;
        var counts = new int[PlateModel.Plates.Count];
        foreach (var cell in grid) counts[cell]++;

        // Every plate has room; the big ones are big and none floods the whole map.
        Assert.All(counts, c => Assert.True(c > 100));
        Assert.True(counts[PlateModel.IndexOf("PA")] > counts[PlateModel.IndexOf("NZ")]);
        Assert.True(counts.Max() < grid.Length / 3);
    }

    [Fact]
    public void ThePlates_MoveAsMeasured()
    {
        // Hawaii rides the Pacific plate about 7 cm a year to the west-north-west.
        var (speed, azimuth) = PlateModel.Velocity(PlateModel.Find("PA"), 19.5, -155);
        Assert.InRange(speed, 6, 8);
        Assert.InRange(azimuth, 280, 315);

        // India heads north-north-east at about 5 cm a year.
        (speed, azimuth) = PlateModel.Velocity(PlateModel.Find("IN"), 28.6, 77.2);
        Assert.InRange(speed, 4, 6.5);
        Assert.InRange(azimuth, 10, 60);

        // The Atlantic widens about 2.3 cm a year; the East Pacific Rise opens about 14.
        Assert.InRange(PlateModel.RelativeSpeed(PlateModel.Find("NA"), PlateModel.Find("EU"), 45, -28), 1.8, 2.8);
        Assert.InRange(PlateModel.RelativeSpeed(PlateModel.Find("PA"), PlateModel.Find("NZ"), -10, -110), 12, 17);

        // Nazca meets South America at about 8 cm a year, the Pacific meets Japan at about 8–9.
        Assert.InRange(PlateModel.RelativeSpeed(PlateModel.Find("NZ"), PlateModel.Find("SA"), -25, -71), 6.5, 9);
        Assert.InRange(PlateModel.RelativeSpeed(PlateModel.Find("PA"), PlateModel.Find("NA"), 38, 144), 7, 10);

        // Juan de Fuca drifts north-east into North America.
        (speed, azimuth) = PlateModel.Velocity(PlateModel.Find("JF"), 46, -128);
        Assert.InRange(speed, 2, 4);
        Assert.InRange(azimuth, 30, 90);
    }

    [Theory]
    [InlineData("MidAtlanticNorth", -1)]
    [InlineData("EastPacificSouth", -1)]
    [InlineData("PeruChile", 1)]
    [InlineData("JapanKurilAleutian", 1)]
    [InlineData("Himalaya", 1)]
    public void Boundaries_OpenOrCloseAsTheirKindSays(string id, int sign)
    {
        var boundary = PlateModel.Boundaries.First(b => b.Id == id);
        var segment = boundary.Points.Length / 2;
        var (closing, _) = PlateModel.RelativeMotion(boundary, segment, 0.5);
        Assert.Equal(sign, Math.Sign(closing));
        Assert.True(Math.Abs(closing) > 1);
    }

    [Fact]
    public void TheSanAndreas_SlidesMoreThanItCloses()
    {
        var boundary = PlateModel.Boundaries.First(b => b.Id == "SanAndreas");
        var (closing, sliding) = PlateModel.RelativeMotion(boundary, 2, 0.5);
        Assert.InRange(sliding, 3.5, 6);
        Assert.True(Math.Abs(closing) < sliding / 2);
    }

    [Fact]
    public void Quakes_GoDeepOnlyBehindTrenches()
    {
        var quakes = PlateModel.Quakes;
        Assert.True(quakes.Count > 2000);
        var deep = quakes.Where(q => q.Depth > 300).ToList();
        Assert.NotEmpty(deep);
        Assert.All(deep, q => Assert.Equal(BoundaryKind.Trench, PlateModel.Boundaries[q.Boundary].Kind));

        // Most are shallow; and ten times fewer for each step of magnitude.
        Assert.True(quakes.Count(q => q.Depth < 70) > quakes.Count / 2);
        var fives = quakes.Count(q => q.Magnitude is >= 5 and < 6);
        var sixes = quakes.Count(q => q.Magnitude is >= 6 and < 7);
        Assert.InRange((double)fives / sixes, 6, 15);

        // Deep quakes sit inland of the trench, on the upper plate.
        var japan = PlateModel.Boundaries.ToList().FindIndex(b => b.Id == "JapanKurilAleutian");
        var deepJapan = quakes.Where(q => q.Boundary == japan && q.Depth > 300).ToList();
        Assert.NotEmpty(deepJapan);
        Assert.All(deepJapan, q => Assert.Contains(PlateModel.PlateAt(q.Latitude, q.Longitude).Id, new[] { "NA", "EU" }));
    }

    [Fact]
    public void Volcanoes_StandBehindTheTrenches()
    {
        Assert.True(PlateModel.Volcanoes.Count > 60);
        var andes = PlateModel.Volcanoes.Where(v => v.Latitude is > -30 and < -15 && v.Longitude is > -75 and < -60 && !v.Hotspot).ToList();
        Assert.NotEmpty(andes);
        Assert.All(andes, v => Assert.Equal("SA", PlateModel.PlateAt(v.Latitude, v.Longitude).Id));
        Assert.All(PlateModel.Hotspots, h => Assert.True(PlateModel.Nearest(h).Km > 500 || h.Latitude < 0));
    }

    [Fact]
    public void Distances_AreGreatCircles()
    {
        // London to New York: about 5570 km.
        Assert.InRange(PlateModel.Distance(new GeoPoint(51.51, -0.13), new GeoPoint(40.71, -74.01)), 5520, 5620);

        // Changsha is well inside Eurasia: the nearest boundary (Taiwan or the Ryukyu trench) is about 1000 km away.
        var nearest = PlateModel.Nearest(new GeoPoint(28.23, 112.94));
        Assert.InRange(nearest.Km, 700, 1500);
    }

    [Fact]
    public void Magnitude_StepsMultiplyTheEnergyBy32()
    {
        Assert.Equal(Math.Pow(10, 1.5), PlateModel.Energy(7) / PlateModel.Energy(6), 6);
        Assert.InRange(PlateModel.Energy(7) / 4.184e15, 0.3, 1.0); // half a megaton of TNT
    }

    #endregion

    #region inside the Earth

    [Fact]
    public void TheModel_HasTheTextbookLayers()
    {
        Assert.Equal(0, EarthModel.Velocity(WaveKind.S, 3500));
        Assert.True(EarthModel.Velocity(WaveKind.P, EarthModel.Moho, below: true) > EarthModel.Velocity(WaveKind.P, EarthModel.Moho) + 1);
        Assert.True(EarthModel.Velocity(WaveKind.P, EarthModel.Gutenberg, below: true) < EarthModel.Velocity(WaveKind.P, EarthModel.Gutenberg) - 5);
        Assert.Equal(0, EarthModel.Velocity(WaveKind.S, EarthModel.Gutenberg, below: true));
        Assert.True(EarthModel.Velocity(WaveKind.S, EarthModel.InnerCore, below: true) > 3);
        Assert.Equal(EarthLayer.OuterCore, EarthModel.LayerAt(4000));
    }

    [Theory]
    [InlineData(30, 370)]
    [InlineData(60, 605)]
    [InlineData(90, 780)]
    public void P_ArrivesWhenTheTablesSay(double distance, double seconds)
    {
        var (p, _) = EarthModel.FirstArrivals(distance);
        Assert.NotNull(p);
        Assert.InRange(p!.Value, seconds * 0.96, seconds * 1.04);
    }

    [Theory]
    [InlineData(30, 670)]
    [InlineData(60, 1090)]
    public void S_ArrivesWhenTheTablesSay(double distance, double seconds)
    {
        var (_, s) = EarthModel.FirstArrivals(distance);
        Assert.NotNull(s);
        Assert.InRange(s!.Value, seconds * 0.95, seconds * 1.05);
    }

    [Fact]
    public void ThroughTheCentre_TakesAboutTwentyMinutes()
    {
        var ray = EarthModel.Trace(WaveKind.P, 0.05);
        Assert.Equal(Phase.PKIKP, ray.Phase);
        Assert.InRange(ray.Distance, 179, 181);
        Assert.InRange(ray.TravelTime, 1212 * 0.97, 1212 * 1.03);
    }

    [Fact]
    public void TheCore_CastsShadows()
    {
        // No S beyond about 104°: the liquid outer core stops it.
        Assert.Null(EarthModel.FirstArrivals(120).S);
        Assert.Null(EarthModel.FirstArrivals(150).S);
        Assert.NotNull(EarthModel.FirstArrivals(90).S);
        Assert.Contains(EarthModel.Fan(WaveKind.S), r => r.Phase == Phase.SStopped);

        // No P through the mantle or the outer core alone between about 100° and 140°.
        foreach (var distance in new[] { 110.0, 120, 130 })
        {
            Assert.Null(EarthModel.FirstArrivals(distance, countInnerCore: false).P);
        }

        Assert.NotNull(EarthModel.FirstArrivals(95, countInnerCore: false).P);
        Assert.NotNull(EarthModel.FirstArrivals(160, countInnerCore: false).P);

        // In between, only waves through or off the inner core (how Inge Lehmann found it in 1936).
        Assert.All(EarthModel.Arrivals(120), a => Assert.Contains(a.Phase, new[] { Phase.PKIKP, Phase.PKiKP }));
        Assert.NotEmpty(EarthModel.Arrivals(120));
    }

    [Fact]
    public void ARay_IsFollowedInTime()
    {
        var ray = EarthModel.Trace(WaveKind.P, 30);
        Assert.Equal(Phase.P, ray.Phase);
        Assert.Null(ray.At(-1));
        Assert.Null(ray.At(ray.TravelTime + 1));
        var middle = ray.At(ray.TravelTime / 2)!.Value;
        Assert.True(middle.Radius < EarthModel.Radius - 200);
        Assert.InRange(middle.Angle, 0.1 * ray.Angle[^1], 0.9 * ray.Angle[^1]);
    }

    #endregion

    #region the boundaries

    [Fact]
    public void TheSeaFloor_RecordsTheFlips()
    {
        Assert.True(BoundaryModel.IsNormal(0.5));
        Assert.False(BoundaryModel.IsNormal(1.5));
        Assert.True(BoundaryModel.IsNormal(1.0));
        Assert.True(BoundaryModel.IsNormal(2.7));

        // At 2.5 cm a year each side, 10 km is 0.4 Ma (normal) and 30 km is 1.2 Ma (reversed); the two sides mirror.
        Assert.True(BoundaryModel.Anomaly(10, 2.5) > 0.5);
        Assert.True(BoundaryModel.Anomaly(30, 2.5) < -0.5);
        Assert.Equal(BoundaryModel.Anomaly(-30, 2.5), BoundaryModel.Anomaly(30, 2.5), 6);
    }

    [Fact]
    public void TheSeaFloor_SinksAsItAges()
    {
        Assert.Equal(2.5, BoundaryModel.SeaDepth(0), 6);
        Assert.InRange(BoundaryModel.SeaDepth(36), 4.5, 4.7);
        Assert.Equal(4, BoundaryModel.Age(100, 2.5), 6);
        Assert.True(BoundaryModel.LithosphereThickness(50) > BoundaryModel.LithosphereThickness(5));
        Assert.True(BoundaryModel.HasRiftValley(1.2));
        Assert.False(BoundaryModel.HasRiftValley(7));
    }

    [Fact]
    public void Volcanoes_ComeUpFartherFromAShallowerSlab()
    {
        var steep = BoundaryModel.ArcDistance(60);
        var shallow = BoundaryModel.ArcDistance(25);
        Assert.True(shallow > steep + 50);
        Assert.InRange(BoundaryModel.ArcDistance(40), 120, 260);
        Assert.InRange(BoundaryModel.SlabTop(400, 45).Depth, 200, 290);
        Assert.All(BoundaryModel.SlabQuakes(660, 45), q => Assert.InRange(BoundaryModel.SlabTop(q.S, 45).Depth, 0, 661));
    }

    [Fact]
    public void India_CrossedAnOceanAndThenSlowed()
    {
        Assert.InRange(BoundaryModel.IndiaSpeed(65), 14, 19);
        Assert.InRange(BoundaryModel.IndiaSpeed(10), 4, 5.5);

        // Since the collision it has come about 2500 km farther north.
        Assert.InRange(BoundaryModel.IndiaTravel(BoundaryModel.ContactAge, 0), 2000, 3200);
        Assert.InRange(BoundaryModel.TethysWidth(70), 2000, 4000);
        Assert.Equal(0, BoundaryModel.TethysWidth(30));
        Assert.InRange(BoundaryModel.Heights(0).Himalaya, 8.7, 8.9);
        Assert.InRange(BoundaryModel.TibetCrust(0), 68, 72);
    }

    [Fact]
    public void AStuckFault_StoresWhatItReleases()
    {
        // Far from the fault the ground has moved half the plate motion each way; at the fault, nothing yet.
        Assert.Equal(0, BoundaryModel.Bending(0, 3.5, 100), 9);
        Assert.InRange(BoundaryModel.Bending(1000, 3.5, 100), 1.7, 1.75);
        Assert.Equal(-BoundaryModel.Bending(20, 3.5, 100), BoundaryModel.Bending(-20, 3.5, 100), 9);

        // 150 years at 3.5 cm is 5.25 m: on a 300 km fault that is a magnitude near 7.8, like 1906.
        Assert.Equal(5.25, BoundaryModel.SlipPerQuake(3.5, 150), 6);
        Assert.InRange(BoundaryModel.Magnitude(5.25, 300), 7.6, 8.0);
    }

    #endregion
}
