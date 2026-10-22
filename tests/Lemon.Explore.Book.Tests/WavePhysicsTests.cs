using System;
using System.Linq;
using Avalonia;
using Lemon.Explore.Book.Chapters.Waves;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class WavePhysicsTests
{
    [Fact]
    public void OnTheMiddleLineTheWavesArriveInStep()
    {
        var point = new Point(0, 9);
        Assert.Equal(0, WavePhysics.PathDifference(point, 6), 12);
        Assert.Equal(Meeting.InStep, WavePhysics.MeetingOf(WavePhysics.PhaseGap(0, 2.5, opposite: false)));

        // Dippers moving opposite ways: the middle line is still.
        Assert.Equal(Meeting.OutOfStep, WavePhysics.MeetingOf(WavePhysics.PhaseGap(0, 2.5, opposite: true)));
        Assert.Equal(0, WavePhysics.Combined(point, 6, 2.5, opposite: true, second: true).Share, 9);
        Assert.Equal(1, WavePhysics.Combined(point, 6, 2.5, opposite: false, second: true).Share, 9);
    }

    [Theory]
    [InlineData(0.5, Meeting.OutOfStep)]
    [InlineData(1.0, Meeting.InStep)]
    [InlineData(1.5, Meeting.OutOfStep)]
    [InlineData(1.25, Meeting.Between)]
    [InlineData(-2.0, Meeting.InStep)]
    public void HalfAWavelengthApartCancels(double wavelengths, Meeting expected)
    {
        Assert.Equal(expected, WavePhysics.MeetingOf(WavePhysics.PhaseGap(wavelengths * 3, 3, opposite: false)));
    }

    [Theory]
    [InlineData(6, 2.5, false, new[] { -3.75, -1.25, 1.25, 3.75 })]
    [InlineData(6, 2.5, true, new[] { -5.0, -2.5, 0.0, 2.5, 5.0 })]
    [InlineData(1, 2.5, false, new double[0])]
    [InlineData(5, 2.5, false, new[] { -3.75, -1.25, 1.25, 3.75 })]
    public void QuietLinesComeAtOddHalfWavelengths(double separation, double wavelength, bool opposite, double[] expected)
    {
        Assert.Equal(expected, WavePhysics.QuietDifferences(separation, wavelength, opposite));
    }

    [Fact]
    public void LoudLinesIncludeTheMiddleWhenInStep()
    {
        Assert.Equal([-5.0, -2.5, 0.0, 2.5, 5.0], WavePhysics.LoudDifferences(6, 2.5, opposite: false));
        Assert.Equal([-3.75, -1.25, 1.25, 3.75], WavePhysics.LoudDifferences(6, 2.5, opposite: true));
    }

    [Theory]
    [InlineData(1.25)]
    [InlineData(-3.75)]
    [InlineData(0)]
    public void EveryPointOfAHyperbolaHasItsPathDifference(double delta)
    {
        var points = WavePhysics.Hyperbola(delta, 6, 20).ToList();
        Assert.NotEmpty(points);
        foreach (var point in points) Assert.Equal(delta, WavePhysics.PathDifference(point, 6), 9);
        Assert.Equal(-20, points[0].Y, 9);
        Assert.Equal(20, points[^1].Y, 9);
    }

    [Fact]
    public void TheProbeTracesAddUp()
    {
        var point = new Point(4, 7);
        for (var t = 0.0; t < 1; t += 0.037)
        {
            var (a, b, sum) = WavePhysics.RippleHeights(point, 6, 2.5, false, true, t);
            Assert.Equal(a + b, sum, 12);
        }

        // Over a cycle, the sum swings as far as the combined amplitude says.
        var together = WavePhysics.Combined(point, 6, 2.5, false, true).Together;
        var period = 1 / WavePhysics.RippleFrequency(2.5);
        var top = Enumerable.Range(0, 2000).Max(i => WavePhysics.RippleHeights(point, 6, 2.5, false, true, i * period / 2000).Sum);
        Assert.Equal(together, top, 4);
    }

    [Fact]
    public void GreenLightThroughSlitsAQuarterMillimetreApart()
    {
        // 532 nm, 2 m away, 0.25 mm apart: stripes 4.26 mm apart.
        Assert.Equal(4.256, WavePhysics.FringeSpacing(532, 2, 0.25), 3);
        Assert.Equal(563.5, WavePhysics.LightFrequency(532) / 1e12, 1);

        var spacing = WavePhysics.FringeSpacing(532, 2, 0.25);
        Assert.Equal(1, WavePhysics.ScreenIntensity(0, 532, 2, 0.25, true), 12);
        Assert.True(WavePhysics.ScreenIntensity(spacing / 2, 532, 2, 0.25, true) < 1e-6);
        Assert.True(WavePhysics.ScreenIntensity(spacing, 532, 2, 0.25, true) > 0.85);

        // One slit: no stripes, a quarter of the light, dark first where a·sinθ = λ.
        Assert.Equal(0.25, WavePhysics.ScreenIntensity(0, 532, 2, 0.25, false), 12);
        Assert.True(WavePhysics.ScreenIntensity(spacing / 2, 532, 2, 0.25, false) > 0.24);
        var firstDark = 532e-9 * 2 / (WavePhysics.SlitWidth * 1e-3) * 1e3;
        Assert.True(WavePhysics.ScreenIntensity(firstDark, 532, 2, 0.25, false) < 1e-4);
    }

    [Theory]
    [InlineData(0.25, 13)]
    [InlineData(0.1, 5)]
    [InlineData(0.4, 19)]
    public void StripesInsideTheMiddleGlow(double spacing, int expected)
    {
        Assert.Equal(expected, WavePhysics.StripesInMiddle(spacing));
    }

    [Fact]
    public void AheadHigherBehindLower()
    {
        Assert.Equal(880, WavePhysics.Ahead(0.5), 9);
        Assert.Equal(293.333, WavePhysics.Behind(0.5), 3);
        Assert.True(double.IsPositiveInfinity(WavePhysics.Ahead(1.2)));
        Assert.Equal(30, WavePhysics.ConeAngle(2) * 180 / Math.PI, 9);
        Assert.True(double.IsNaN(WavePhysics.ConeAngle(0.9)));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void BelowTheSpeedOfSoundThereIsOneSoundThatArrivesWhenItShould(double mach)
    {
        var listener = new Point(60, 45);
        var pass = WavePhysics.PassDuration(mach);
        for (var t = 0.0; t <= pass; t += pass / 37)
        {
            var sounds = WavePhysics.Heard(listener, t, mach);
            var sound = Assert.Single(sounds);
            var source = new Point(WavePhysics.SourceX(sound.Emitted, mach), 0);
            Assert.Equal(WavePhysics.SpeedOfSound * (t - sound.Emitted), ((Vector)(listener - source)).Length, 6);
        }
    }

    [Fact]
    public void ThePitchFallsAsTheSourceGoesPast()
    {
        var listener = new Point(0, 30);
        const double mach = 0.3;
        var pass = WavePhysics.PassDuration(mach);
        var early = WavePhysics.Heard(listener, 0, mach)[0].Frequency;
        var late = WavePhysics.Heard(listener, pass, mach)[0].Frequency;
        Assert.True(early > 560 && early < WavePhysics.Ahead(mach));
        Assert.True(late < 360 && late > WavePhysics.Behind(mach));

        // The true pitch arrives from the moment it was right beside the listener, a little after that moment.
        var beside = pass / 2;
        var delay = 30 / WavePhysics.SpeedOfSound;
        Assert.Equal(WavePhysics.SourcePitch, WavePhysics.Heard(listener, beside + delay, mach)[0].Frequency, 6);

        // Approaching, passing, going away.
        Assert.Equal(Hearing.Approaching, Hear(listener, 1, mach));
        Assert.Equal(Hearing.Passing, Hear(listener, beside + delay, mach));
        Assert.Equal(Hearing.Receding, Hear(listener, pass, mach));
        Assert.Equal(Hearing.Still, Hear(listener, 1, 0));
    }

    [Fact]
    public void FasterThanSoundNothingThenABangThenTwoSounds()
    {
        var listener = new Point(50, 40);
        const double mach = 1.6;
        var boom = WavePhysics.ConeArrival(listener, mach);
        Assert.InRange(boom, 0, WavePhysics.PassDuration(mach));

        Assert.Empty(WavePhysics.Heard(listener, boom - 0.01, mach));
        Assert.Equal(2, WavePhysics.Heard(listener, boom + 0.01, mach).Count);
        Assert.Equal(Hearing.Silent, Hear(listener, boom - 0.01, mach));
        Assert.Equal(Hearing.Boom, Hear(listener, boom + 0.01, mach));
        Assert.Equal(Hearing.Two, Hear(listener, boom + 0.5, mach));

        // Both sounds really arrive now.
        foreach (var sound in WavePhysics.Heard(listener, boom + 0.5, mach))
        {
            var source = new Point(WavePhysics.SourceX(sound.Emitted, mach), 0);
            Assert.Equal(WavePhysics.SpeedOfSound * (boom + 0.5 - sound.Emitted), ((Vector)(listener - source)).Length, 5);
        }
    }

    [Theory]
    [InlineData(440, "A4")]
    [InlineData(261.63, "C4")]
    [InlineData(880, "A5")]
    [InlineData(466.16, "A♯4")]
    [InlineData(293.33, "D4")]
    [InlineData(32.7, "C1")]
    public void NotesOfThePiano(double hertz, string expected)
    {
        Assert.Equal(expected, WavePhysics.NoteName(hertz));
    }

    private static Hearing Hear(Point listener, double t, double mach) => WavePhysics.HearingOf(
        WavePhysics.Heard(listener, t, mach), mach, t, WavePhysics.ConeArrival(listener, mach));
}
