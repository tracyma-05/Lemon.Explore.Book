using Lemon.Explore.Book.Chapters.StarLife;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class StarModelTests
{
    [Theory]
    [InlineData(0.1, StarClass.RedDwarf)]
    [InlineData(0.49, StarClass.RedDwarf)]
    [InlineData(1, StarClass.SunLike)]
    [InlineData(7.9, StarClass.SunLike)]
    [InlineData(8, StarClass.Massive)]
    [InlineData(18, StarClass.Massive)]
    [InlineData(25, StarClass.VeryMassive)]
    public void TheFateDependsOnTheMass(double mass, StarClass expected) => Assert.Equal(expected, StarModel.ClassOf(mass));

    [Fact]
    public void HeavierStarsLiveShorter_TheSunTenBillionYears()
    {
        Assert.Equal(1e10, StarModel.MainSequenceYears(1), 1);
        Assert.True(StarModel.MainSequenceYears(0.1) > 1e12);
        Assert.InRange(StarModel.MainSequenceYears(15), 8e6, 2e7);
        Assert.InRange(StarModel.MainSequenceYears(40), 3e6, 7e6);

        var previous = double.MaxValue;
        for (var mass = StarModel.MinMass; mass <= StarModel.MaxMass; mass *= 1.1)
        {
            var years = StarModel.MainSequenceYears(mass);
            Assert.True(years < previous);
            previous = years;
        }
    }

    [Fact]
    public void RadiusFollowsFromLuminosityAndTemperature()
    {
        Assert.Equal(1, StarModel.RadiusOf(1, StarModel.SunTemperature), 9);
        Assert.Equal(4, StarModel.LuminosityOf(2, StarModel.SunTemperature), 9);

        // Real stars come out at their measured sizes.
        Assert.InRange(Known("Betelgeuse").Radius, 700, 1000);
        Assert.InRange(Known("Rigel").Radius, 70, 90);
        Assert.InRange(Known("SiriusB").Radius, 0.01, 0.014);
        Assert.InRange(Known("Proxima").Radius, 0.13, 0.17);
    }

    [Fact]
    public void TheSun_IsOnTheMainSequenceToday_AndBecomesARedGiantThenAWhiteDwarf()
    {
        var track = StarModel.Track(1);
        Assert.Equal(
            [StarStage.Protostar, StarStage.MainSequence, StarStage.Subgiant, StarStage.RedGiant, StarStage.HeliumBurning,
                StarStage.Agb, StarStage.PlanetaryNebula, StarStage.WhiteDwarf],
            track.Select(s => s.Stage));

        var today = StarModel.At(track, 1, StarModel.StoryAtAge(track, StarModel.SunAgeYears));
        Assert.Equal(StarStage.MainSequence, today.Stage);
        Assert.InRange(today.AgeYears, 4.5e9, 4.7e9);
        Assert.InRange(today.Luminosity, 0.85, 1.15);
        Assert.InRange(today.Temperature, 5500, 6000);
        Assert.InRange(today.Radius, 0.9, 1.1);

        // At the tip of the red giant branch: thousands of times as bright and over a hundred times as big.
        var tip = StarModel.At(track, 1, StarModel.StoryStart(track, 4) - 1e-6);
        Assert.Equal(StarStage.RedGiant, tip.Stage);
        Assert.InRange(tip.Luminosity, 2000, 3000);
        Assert.InRange(tip.Radius, 120, 250);

        // A white dwarf about the size of Earth, half the Sun's mass.
        var dwarf = StarModel.At(track, 1, StarModel.StoryLength(track) - 1e-6);
        Assert.Equal(StarStage.WhiteDwarf, dwarf.Stage);
        Assert.InRange(StarModel.WhiteDwarfMass(1), 0.48, 0.56);
        Assert.InRange(dwarf.Radius * StarModel.SunRadiusKm / StarModel.EarthRadiusKm, 1, 2);
        Assert.InRange(dwarf.AgeYears, 2e10, 2.4e10);
    }

    [Fact]
    public void AgesGrowThroughTheStory_AndStagesFollowInOrder()
    {
        foreach (var mass in new[] { 0.2, 1, 3, 12, 30 })
        {
            var track = StarModel.Track(mass);
            var length = StarModel.StoryLength(track);
            var previousAge = -1.0;
            var previousIndex = 0;
            for (var story = 0.0; story < length; story += 0.05)
            {
                var state = StarModel.At(track, mass, story);
                Assert.True(state.AgeYears >= previousAge);
                Assert.True(state.Index >= previousIndex);
                Assert.Equal(track[state.Index].Stage, state.Stage);
                previousAge = state.AgeYears;
                previousIndex = state.Index;
            }
        }
    }

    [Theory]
    [InlineData(0.2, StarStage.WhiteDwarf)]
    [InlineData(5, StarStage.WhiteDwarf)]
    [InlineData(12, StarStage.NeutronStar)]
    [InlineData(30, StarStage.BlackHole)]
    public void EveryStarEndsAsARemnant(double mass, StarStage expected)
    {
        var track = StarModel.Track(mass);
        Assert.Equal(expected, track[^1].Stage);
        Assert.Equal(StarStage.Protostar, track[0].Stage);
        Assert.Contains(track, s => s.Stage == StarStage.MainSequence);
    }

    [Fact]
    public void Remnants_HaveTheirFamousSizes()
    {
        // A white dwarf is Earth-sized, and shrinks as it gets heavier, up to 1.44 Suns.
        Assert.InRange(StarModel.WhiteDwarfRadiusKm(0.6), 7000, 13000);
        Assert.True(StarModel.WhiteDwarfRadiusKm(1.2) < StarModel.WhiteDwarfRadiusKm(0.6));
        Assert.True(StarModel.WhiteDwarfMass(StarModel.MaxMass) < StarModel.ChandrasekharMass);

        // A 10-Sun black hole: an event horizon 30 km across in radius terms of 3 km per Sun.
        Assert.InRange(StarModel.SchwarzschildRadiusKm(10), 29, 30);
        Assert.Equal(1.4, StarModel.RemnantMass(12));
        Assert.InRange(StarModel.RemnantMass(30), 5, 15);
    }

    [Fact]
    public void ARedDwarf_HasNotHadTimeToDie()
    {
        var track = StarModel.Track(0.2);
        var mainSequence = track.First(s => s.Stage == StarStage.MainSequence);
        Assert.True(mainSequence.Years > 50 * StarModel.UniverseAgeYears);
    }

    [Fact]
    public void Colours_GoFromRedToBlue()
    {
        var cool = StarModel.ColorOf(3000);
        var sun = StarModel.ColorOf(StarModel.SunTemperature);
        var hot = StarModel.ColorOf(30000);
        Assert.True(cool.R > cool.B + 100);
        Assert.True(hot.B > hot.R);
        Assert.True(sun.R > 240 && sun.G > 220 && sun.B > 200);
    }

    private static KnownStar Known(string id) => StarModel.KnownStars.First(s => s.Id == id);
}
