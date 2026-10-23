using Lemon.Explore.Book.Chapters.Sound;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class SoundModelTests
{
    [Fact]
    public void SoundInAir_Is331AtFreezingAnd343AtRoomTemperature()
    {
        Assert.Equal(331.3, SoundModel.SpeedInAir(0), 6);
        Assert.InRange(SoundModel.SpeedInAir(20), 343.0, 343.4);
        Assert.InRange(SoundModel.AirDensity(20), 1.20, 1.21);
        Assert.True(SoundModel.Speed(SoundMedium.Water, 20) > 4 * SoundModel.Speed(SoundMedium.Air, 20));
        Assert.Equal(0, SoundModel.Speed(SoundMedium.Vacuum, 20));
    }

    [Fact]
    public void ConcertA_HasAWavelengthOfAbout78Centimetres()
    {
        Assert.InRange(SoundModel.Wavelength(440, SoundModel.SpeedInAir(20)), 0.775, 0.785);
    }

    [Fact]
    public void Decibels_FollowTheTwentyMicropascalReference()
    {
        // A sound level calibrator's 94 dB is 1 Pa (RMS); 60 dB is 20 mPa.
        Assert.InRange(SoundModel.PressureRms(94), 0.99, 1.01);
        Assert.Equal(0.02, SoundModel.PressureRms(60), 6);
        Assert.Equal(Math.Sqrt(2) * 0.02, SoundModel.PressureAmplitude(60), 6);

        // 0 dB carries about 10⁻¹² W/m² through air.
        Assert.InRange(SoundModel.Intensity(0, SoundMedium.Air, 20), 0.9e-12, 1.05e-12);
    }

    [Fact]
    public void ConversationAtOneKilohertz_MovesTheAirAboutTenNanometres()
    {
        var displacement = SoundModel.DisplacementAmplitude(60, 1000, SoundMedium.Air, 20);
        Assert.InRange(displacement, 9e-9, 13e-9);
        Assert.Equal(0, SoundModel.DisplacementAmplitude(60, 1000, SoundMedium.Vacuum, 20));
    }

    [Fact]
    public void HearingIsMostSensitiveAroundThreeToFourKilohertz()
    {
        var best = Enumerable.Range(0, 600)
            .Select(i => 20 * Math.Pow(1000, i / 600.0))
            .MinBy(SoundModel.HearingThreshold);
        Assert.InRange(best, 2500, 4500);
        Assert.InRange(SoundModel.HearingThreshold(1000), 2, 5);
        Assert.True(SoundModel.HearingThreshold(100) > 20);
        Assert.True(SoundModel.HearingThreshold(18000) > 60);
        Assert.True(SoundModel.IsAudible(1000, 30));
        Assert.False(SoundModel.IsAudible(100, 10));
        Assert.False(SoundModel.IsAudible(25_000, 100));
    }

    [Fact]
    public void EqualTemperament_PutsMiddleCAt261HertzAndDoublesEveryOctave()
    {
        Assert.Equal(440, SoundModel.NoteFrequency(69), 9);
        Assert.InRange(SoundModel.NoteFrequency(60), 261.62, 261.64);
        Assert.Equal(2, SoundModel.NoteFrequency(81) / SoundModel.NoteFrequency(69), 9);
        Assert.Equal("C4", SoundModel.NoteName(60));
        Assert.Equal("A4", SoundModel.NoteName(440.0));
        Assert.Equal("E2", SoundModel.NoteName(SoundModel.GuitarStrings[0]));
        Assert.InRange(SoundModel.Cents(445), 19, 20.5);
        Assert.True(SoundModel.IsBlack(61));
        Assert.False(SoundModel.IsBlack(64));
    }

    [Fact]
    public void AStringTunedByItsMass_PlaysItsNote_AndTheTwelfthFretPlaysTheOctave()
    {
        var hz = SoundModel.NoteFrequency(SoundModel.GuitarStrings[1]);
        var mu = SoundModel.MassPerMetre(hz, SoundModel.GuitarScale, SoundModel.GuitarTensions[1]);
        Assert.InRange(mu * 1000, 3, 15);
        Assert.Equal(hz, SoundModel.StringFrequency(SoundModel.GuitarScale, SoundModel.GuitarTensions[1], mu), 6);
        var half = SoundModel.FrettedLength(SoundModel.GuitarScale, 12);
        Assert.Equal(SoundModel.GuitarScale / 2, half, 9);
        Assert.Equal(2 * hz, SoundModel.StringFrequency(half, SoundModel.GuitarTensions[1], mu), 6);
    }

    [Fact]
    public void PluckingInTheMiddle_StartsNoEvenHarmonic()
    {
        for (var n = 2; n <= 12; n += 2)
        {
            Assert.True(Math.Abs(SoundModel.PluckAmplitude(n, 0.5)) < 1e-9, n.ToString());
            Assert.True(SoundModel.IsSilenced(n, 0.5));
        }

        Assert.False(SoundModel.IsSilenced(3, 0.5));

        // A fifth of the way along, every fifth harmonic is missing.
        Assert.True(SoundModel.IsSilenced(5, 0.2));
        Assert.False(SoundModel.IsSilenced(4, 0.2));
    }

    [Fact]
    public void TheSumOfTheModes_IsTheTriangleTheStringWasPulledInto()
    {
        const double Pluck = 0.2;
        foreach (var x in new[] { 0.05, 0.2, 0.4, 0.7, 0.95 })
        {
            var triangle = x <= Pluck ? x / Pluck : (1 - x) / (1 - Pluck);
            Assert.InRange(SoundModel.StringShape(x, 0, Pluck, 400, _ => 1), triangle - 0.02, triangle + 0.02);
        }

        // Half a period later the shape is turned over (mirrored and upside down).
        Assert.InRange(SoundModel.StringShape(0.8, 0.5, Pluck, 400, _ => 1), -1.02, -0.98);
    }

    [Fact]
    public void AnOpenPipe_SoundsAboutAnOctaveAboveTheSameClosedPipe_AndTheClosedOneHasOnlyOddHarmonics()
    {
        var speed = SoundModel.SpeedInAir(20);
        var open = SoundModel.PipeFrequency(1, 0.6, speed, closed: false);
        var closed = SoundModel.PipeFrequency(1, 0.6, speed, closed: true);
        Assert.InRange(open, 275, 286);
        Assert.InRange(closed / open, 0.49, 0.52);
        Assert.Equal(3, SoundModel.PipeHarmonic(2, closed: true));
        Assert.Equal(2, SoundModel.PipeHarmonic(2, closed: false));
        Assert.Equal(3 * closed, SoundModel.PipeFrequency(2, 0.6, speed, closed: true), 9);

        // The air moves most at an open end and not at all at a closed one; the pressure the other way round.
        Assert.Equal(1, SoundModel.PipeDisplacement(1, 0, closed: true), 9);
        Assert.Equal(0, SoundModel.PipeDisplacement(1, 1, closed: true), 9);
        Assert.Equal(1, Math.Abs(SoundModel.PipeDisplacement(1, 1, closed: false)), 9);
        Assert.Equal(1, Math.Abs(SoundModel.PipePressure(1, 1, closed: true)), 9);
    }

    [Fact]
    public void Envelopes_StartAtZero_AndPlucksDieAway()
    {
        foreach (var instrument in Enum.GetValues<Instrument>())
        {
            var envelope = SoundModel.Envelope(instrument);
            Assert.Equal(0, envelope.At(0), 9);
            Assert.InRange(envelope.At(envelope.Attack + 1e-3), 0.3, 1);
            Assert.Equal(0, envelope.At(envelope.Length + 0.01), 9);
            Assert.Equal(SoundModel.HarmonicCount, SoundModel.Harmonics(instrument).Length);
        }

        Assert.True(SoundModel.Envelope(Instrument.Piano).At(2) < 0.5 * SoundModel.Envelope(Instrument.Piano).At(0.1));
    }

    [Fact]
    public void ASynthNote_HasItsPitch_StaysInRange_AndEnds()
    {
        var synth = new SoundSynth(44_100);
        synth.Play(new SoundNote(441, [1], [], new NoteEnvelope(0.01, 0.5, 0.05, 0, 0, 0), 0.8));
        var buffer = new float[44_100];
        synth.Render(buffer);

        // 441 Hz crosses zero upwards 441 times a second (counted in the held part).
        var crossings = 0;
        for (var i = 1000; i < 22_050; i++)
        {
            if (buffer[i - 1] < 0 && buffer[i] >= 0) crossings++;
        }

        Assert.InRange(crossings, 0.4765 * 441 - 2, 0.4765 * 441 + 2);
        Assert.All(buffer, s => Assert.InRange(s, -1f, 1f));
        Assert.False(synth.IsSounding);
        Assert.True(buffer.Skip(30_000).All(s => s == 0));
    }

    [Fact]
    public void APluckInTheMiddle_SoundsWithoutItsSecondHarmonic()
    {
        var synth = new SoundSynth(44_100);
        var amplitudes = Enumerable.Range(1, 8).Select(n => SoundModel.PluckAmplitude(n, 0.5)).ToArray();
        synth.Play(new SoundNote(220, amplitudes, [], new NoteEnvelope(0.001, 1, 0.1, 0, 0, 0), 0.8));
        var buffer = new float[22_050];
        synth.Render(buffer);
        var first = Power(buffer, 220, 44_100);
        var second = Power(buffer, 440, 44_100);
        var third = Power(buffer, 660, 44_100);
        Assert.True(second < first * 1e-4);
        Assert.True(third > first * 0.005);
    }

    [Fact]
    public void TheTone_GlidesToANewPitchWithoutAJump()
    {
        var synth = new SoundSynth(44_100);
        synth.SetTone(220, 0.3);
        var buffer = new float[4410];
        synth.Render(buffer);
        synth.SetTone(880, 0.3);
        synth.Render(buffer);
        var jump = 0.0;
        for (var i = 1; i < buffer.Length; i++) jump = Math.Max(jump, Math.Abs(buffer[i] - buffer[i - 1]));

        // A step of 880 Hz at 0.3 changes by at most 2π·880/44100·0.3 ≈ 0.038 a sample.
        Assert.True(jump < 0.045, jump.ToString());
        synth.SetTone(880, 0);
        for (var k = 0; k < 10; k++) synth.Render(buffer);
        Assert.False(synth.IsSounding);
    }

    /// <summary>The signal's power at one frequency (Goertzel).</summary>
    private static double Power(float[] samples, double hertz, int rate)
    {
        var w = 2 * Math.PI * hertz / rate;
        var coefficient = 2 * Math.Cos(w);
        double s1 = 0, s2 = 0;
        foreach (var x in samples)
        {
            var s = x + (coefficient * s1) - s2;
            s2 = s1;
            s1 = s;
        }

        return (s1 * s1) + (s2 * s2) - (coefficient * s1 * s2);
    }
}
