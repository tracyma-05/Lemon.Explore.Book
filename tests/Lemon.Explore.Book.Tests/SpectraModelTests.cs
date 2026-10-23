using Avalonia.Media;
using Lemon.Explore.Book.Chapters.Spectra;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class SpectraModelTests
{
    [Theory]
    [InlineData(2, 3, 656.28)]
    [InlineData(2, 4, 486.13)]
    [InlineData(2, 5, 434.05)]
    [InlineData(2, 6, 410.17)]
    [InlineData(1, 2, 121.57)]
    [InlineData(3, 4, 1875.1)]
    public void Hydrogen_LinesComeOutOfTheRydbergFormula(int lower, int upper, double expected)
    {
        Assert.InRange(SpectraModel.Wavelength(lower, upper), expected - 0.05 * Math.Max(1, expected / 500), expected + 0.05 * Math.Max(1, expected / 500));
    }

    [Fact]
    public void Hydrogen_LevelsAndPhotonsAgree()
    {
        Assert.Equal(-13.598, SpectraModel.Energy(1), 3);
        Assert.Equal(-3.3995, SpectraModel.Energy(2), 3);

        // Hα carries the energy between levels 3 and 2 (1.89 eV).
        var gap = SpectraModel.Energy(3) - SpectraModel.Energy(2);
        Assert.InRange(SpectraModel.PhotonEnergy(SpectraModel.Wavelength(2, 3)), gap * 0.999, gap * 1.001);
        Assert.Equal("Balmer", SpectraModel.SeriesKey(2));
        Assert.Equal("Lyman", SpectraModel.SeriesKey(1));
    }

    [Fact]
    public void Photons_AreTakenOnlyAtTheRightColour()
    {
        // From the ground, Lyman α lifts to level 2; red light does nothing; far ultraviolet sets the electron free.
        Assert.Equal(2, SpectraModel.Absorb(1, SpectraModel.Wavelength(1, 2)));
        Assert.Equal(-1, SpectraModel.Absorb(1, 656.28));
        Assert.Equal(0, SpectraModel.Absorb(1, 80));

        // From level 2, the same red light is taken: up to 3. A little off, it is not.
        Assert.Equal(3, SpectraModel.Absorb(2, 656.28));
        Assert.Equal(-1, SpectraModel.Absorb(2, 640));
    }

    [Fact]
    public void Falling_FollowsKramers()
    {
        var chances = SpectraModel.FallChances(3);
        Assert.Equal(1, chances.Sum(), 9);
        Assert.True(chances[1] > chances[2]);
        Assert.Equal(1, SpectraModel.FallTo(3, FallMode.ToGround, 0.9));
        Assert.Equal(2, SpectraModel.FallTo(3, FallMode.OneStep, 0.1));
        Assert.Equal(1, SpectraModel.FallTo(3, FallMode.Chance, 0.1));
        Assert.Equal(2, SpectraModel.FallTo(3, FallMode.Chance, 0.99));
        Assert.Equal(1, SpectraModel.FallTo(1, FallMode.Chance, 0.5));
    }

    [Theory]
    [InlineData(1, 0, 1.0)]
    [InlineData(2, 1, 4.0)]
    [InlineData(3, 2, 9.0)]
    public void Clouds_PeakAtBohrsRadiiForTheRoundestOrbit(int n, int l, double peak)
    {
        var best = Enumerable.Range(1, 4000).Select(i => i * 0.01).MaxBy(r => SpectraModel.RadialChance(n, l, r));
        Assert.InRange(best, peak - 0.02, peak + 0.02);
    }

    [Fact]
    public void Clouds_HaveNodes()
    {
        // 2s has a spherical node at r = 2 a₀; 2p has none along its axis but nothing in the plane across it.
        Assert.True(SpectraModel.Cloud(2, 0, 2, 1) < 1e-12);
        Assert.True(SpectraModel.Cloud(2, 1, 4, 0) < 1e-12);
        Assert.True(SpectraModel.Cloud(2, 1, 4, 1) > 0);
    }

    [Fact]
    public void Lamps_HaveTheirFamousLines()
    {
        Assert.Contains(SpectraModel.Lines(Gas.Sodium), l => Math.Abs(l.Nm - 589.0) < 0.1);
        Assert.Contains(SpectraModel.Lines(Gas.Mercury), l => Math.Abs(l.Nm - 546.07) < 0.01);
        Assert.Contains(SpectraModel.Lines(Gas.Helium), l => Math.Abs(l.Nm - 587.56) < 0.01);
        Assert.All(Enum.GetValues<Gas>(), gas => Assert.Equal(1, SpectraModel.Lines(gas).Max(l => l.Strength), 9));

        // The lamp's balmer lines agree with the formula.
        foreach (var (line, upper) in SpectraModel.Lines(Gas.Hydrogen).Take(4).Select((l, i) => (l, i + 3)))
        {
            Assert.InRange(line.Nm, SpectraModel.Wavelength(2, upper) - 0.05, SpectraModel.Wavelength(2, upper) + 0.05);
        }
    }

    [Fact]
    public void BlackBodies_PeakByWienAndGlowByTheirTemperature()
    {
        Assert.InRange(SpectraModel.PeakWavelength(5772), 501, 503);
        var peak = Enumerable.Range(300, 1000).MaxBy(nm => SpectraModel.Planck(nm, 5772));
        Assert.InRange(peak, 500, 504);

        // Hot stars are blue-white, cool ones orange-red.
        var hot = SpectraModel.BodyColour(20_000);
        var cool = SpectraModel.BodyColour(3_000);
        Assert.True(hot.B > hot.R);
        Assert.True(cool.R > cool.B + 100);
        Assert.InRange(SpectraModel.BodyColour(6_500).R, 225, 255);
        Assert.InRange(SpectraModel.BodyColour(6_500).B, 215, 255);
    }

    [Fact]
    public void Hydrogen_LinesAreStrongestInAStars()
    {
        var best = Enumerable.Range(30, 370).Select(i => i * 100.0).MaxBy(SpectraModel.BalmerStrength);
        Assert.InRange(best, 8_500, 11_000);
        Assert.True(SpectraModel.BalmerStrength(5772) < 0.3);
        Assert.True(SpectraModel.BalmerStrength(30_000) < 0.3);
        Assert.Equal('A', SpectraModel.SpectralClass(best));
        Assert.Equal('G', SpectraModel.SpectralClass(SpectraModel.SunKelvin));
        Assert.Equal('M', SpectraModel.SpectralClass(3_600));
    }

    [Fact]
    public void StarLines_FollowTheTemperature()
    {
        // A cool star has molecular bands and strong sodium; a hot one helium and no bands.
        Assert.Contains(SpectraModel.StarLines(3_400), l => l.Band);
        Assert.DoesNotContain(SpectraModel.StarLines(20_000), l => l.Band);
        Assert.Contains(SpectraModel.StarLines(20_000), l => Math.Abs(l.Nm - 447.15) < 0.01);
        Assert.True(SpectraModel.Transmission(SpectraModel.StarLines(9_600), 656.28) < 0.5);
        Assert.True(SpectraModel.Transmission(SpectraModel.StarLines(9_600), 600) > 0.95);
    }

    [Fact]
    public void Doppler_ShiftsAwayToTheRed()
    {
        Assert.True(SpectraModel.Shift(656.28, 3000) > 656.28);
        Assert.True(SpectraModel.Shift(656.28, -3000) < 656.28);

        // 1000 km/s moves Hα by about 2.19 nm.
        Assert.InRange(SpectraModel.Shift(656.28, 1000) - 656.28, 2.18, 2.2);
        Assert.Equal(12_345, SpectraModel.SpeedFromShift(500, SpectraModel.Shift(500, 12_345)), 6);

        // The shifted line is dark at its new place, not at the old one.
        var lines = SpectraModel.StarLines(9_600);
        var shifted = SpectraModel.Shift(656.28, 5000);
        Assert.True(SpectraModel.Transmission(lines, shifted, 5000) < 0.5);
        Assert.True(SpectraModel.Transmission(lines, 656.28, 5000) > 0.8);
    }
}
