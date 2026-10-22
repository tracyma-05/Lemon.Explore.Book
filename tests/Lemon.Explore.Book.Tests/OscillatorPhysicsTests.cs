using System;
using System.Linq;
using Lemon.Explore.Book.Chapters.Oscillations;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class OscillatorPhysicsTests
{
    private const double Deg = Math.PI / 180;

    [Fact]
    public void AMetrePendulumOnEarthSwingsInAboutTwoSeconds()
    {
        Assert.Equal(2.006, OscillatorPhysics.SmallAnglePeriod(1, OscillatorPhysics.EarthGravity), 3);

        // A quarter of the length: half the period. The Moon's weaker pull: about 2.5 times as long.
        Assert.Equal(0.5, OscillatorPhysics.SmallAnglePeriod(0.25, 9.81) / OscillatorPhysics.SmallAnglePeriod(1, 9.81), 9);
        var moon = OscillatorPhysics.SmallAnglePeriod(1, OscillatorPhysics.Gravity(OscillatorPlace.Moon));
        Assert.InRange(moon / OscillatorPhysics.SmallAnglePeriod(1, 9.81), 2.45, 2.47);
    }

    [Theory]
    [InlineData(10, 1.0019)]
    [InlineData(30, 1.0174)]
    [InlineData(60, 1.0732)]
    [InlineData(90, 1.1803)]
    public void BigSwingsTakeLongerThanTheFormulaSays(double amplitude, double stretch)
    {
        Assert.Equal(stretch, OscillatorPhysics.PeriodStretch(amplitude * Deg), 4);
        Assert.Equal(OscillatorPhysics.PeriodStretch(amplitude * Deg) * OscillatorPhysics.SmallAnglePeriod(1.5, 3.71),
            OscillatorPhysics.PendulumPeriod(1.5, 3.71, amplitude * Deg), 12);
    }

    [Fact]
    public void ArithmeticGeometricMeanMeetsInTheMiddle()
    {
        Assert.Equal(1, OscillatorPhysics.ArithmeticGeometricMean(1, 1), 12);

        // Gauss's constant: 1 / agm(1, √2).
        Assert.Equal(0.8346268416740731, 1 / OscillatorPhysics.ArithmeticGeometricMean(1, Math.Sqrt(2)), 12);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(45)]
    [InlineData(90)]
    public void SimulatedSwingMatchesTheExactPeriod(double amplitudeDegrees)
    {
        const double length = 0.8;
        const double gravity = 9.81;
        const double dt = 1.0 / 600;
        var amplitude = amplitudeDegrees * Deg;

        // Let go at the right; time the next turns at the right by interpolating where the velocity changes sign.
        var (angle, velocity) = (amplitude, 0.0);
        var turns = new System.Collections.Generic.List<double>();
        for (var step = 0; step < 6000 && turns.Count < 3; step++)
        {
            var (nextAngle, nextVelocity) = OscillatorPhysics.StepPendulum(angle, velocity, length, gravity, 0, dt);
            if (velocity > 0 && nextVelocity <= 0) turns.Add((step + (velocity / (velocity - nextVelocity))) * dt);
            (angle, velocity) = (nextAngle, nextVelocity);
        }

        Assert.Equal(3, turns.Count);
        var period = OscillatorPhysics.PendulumPeriod(length, gravity, amplitude);
        Assert.Equal(period, turns[1] - turns[0], 4);
        Assert.Equal(period, turns[2] - turns[1], 4);
    }

    [Fact]
    public void WithoutAirTheEnergyStaysTheSame()
    {
        const double release = 70 * Deg;
        var (angle, velocity) = (release, 0.0);
        for (var step = 0; step < 6000; step++)
        {
            (angle, velocity) = OscillatorPhysics.StepPendulum(angle, velocity, 1.2, 1.62, 0, 1.0 / 240);
            var energy = OscillatorPhysics.PendulumEnergy(angle, velocity, 1.2, 1.62, release);
            Assert.Equal(1, energy.Kinetic + energy.Stored, 6);
        }

        Assert.Equal(release, OscillatorPhysics.PendulumAmplitude(angle, velocity, 1.2, 1.62), 5);
    }

    [Fact]
    public void TheAirTakesTheEnergyAway()
    {
        const double release = 30 * Deg;
        var (angle, velocity) = (release, 0.0);
        for (var step = 0; step < 240 * 23; step++)
        {
            (angle, velocity) = OscillatorPhysics.StepPendulum(angle, velocity, 1, 9.81, OscillatorPhysics.PendulumAirDamping, 1.0 / 240);
        }

        // The swing shrinks as e^(−ct/2): to about half in 23 s, three quarters of the energy gone.
        var amplitude = OscillatorPhysics.PendulumAmplitude(angle, velocity, 1, 9.81);
        Assert.InRange(amplitude / release, 0.47, 0.53);
        Assert.InRange(OscillatorPhysics.PendulumEnergy(angle, velocity, 1, 9.81, release).Lost, 0.72, 0.78);
    }

    [Fact]
    public void TheBobIsFastestAtTheBottom()
    {
        // From 90° it falls the whole length: v = √(2gL).
        Assert.Equal(Math.Sqrt(2 * 9.81 * 2), OscillatorPhysics.PendulumTopSpeed(2, 9.81, 90 * Deg), 9);

        var energy = OscillatorPhysics.PendulumEnergy(0, OscillatorPhysics.PendulumTopSpeed(2, 9.81, 40 * Deg) / 2, 2, 9.81, 40 * Deg);
        Assert.Equal(1, energy.Kinetic, 9);
        Assert.Equal(0, energy.Stored, 9);
    }

    [Fact]
    public void ASpringBouncesAtTheSameRateHoweverFarItIsPulledAndWherever()
    {
        Assert.Equal(2 * Math.PI * Math.Sqrt(0.5 / 20), OscillatorPhysics.SpringPeriod(0.5, 20), 12);

        // At rest it hangs mg/k lower: 0.5 kg on 20 N/m sags 24.5 cm on Earth, 4 cm on the Moon.
        Assert.Equal(0.245, OscillatorPhysics.SpringStretch(0.5, 20, 9.81), 3);
        Assert.Equal(0.0405, OscillatorPhysics.SpringStretch(0.5, 20, 1.62), 4);

        var omega = OscillatorPhysics.SpringOmega(0.5, 20);
        foreach (var pull in new[] { 0.02, 0.2 })
        {
            var (height, velocity) = (pull, 0.0);
            var turns = new System.Collections.Generic.List<double>();
            const double dt = 1.0 / 1000;
            for (var step = 0; step < 5000 && turns.Count < 2; step++)
            {
                var next = OscillatorPhysics.StepSpring(height, velocity, omega, 0, _ => 0, step * dt, dt);
                if (velocity > 0 && next.Velocity <= 0) turns.Add((step + (velocity / (velocity - next.Velocity))) * dt);
                (height, velocity) = next;
            }

            Assert.Equal(OscillatorPhysics.SpringPeriod(0.5, 20), turns[1] - turns[0], 4);
            Assert.Equal(pull, OscillatorPhysics.SpringAmplitude(height, velocity, omega), 6);
        }
    }

    [Fact]
    public void ShakenAtItsOwnFrequencyTheSpringSwingsFarthest()
    {
        const double omega = 6.0;
        const double damping = 0.3;

        // Shaken slowly it just follows the top; fast, it hardly moves; at its own frequency, ω₀ / c times as far.
        Assert.Equal(1, OscillatorPhysics.ShakenResponse(omega, damping, 0.001), 4);
        Assert.True(OscillatorPhysics.ShakenResponse(omega, damping, 30) < 0.05);
        Assert.Equal(omega / damping, OscillatorPhysics.ShakenResponse(omega, damping, omega), 9);

        var peak = Enumerable.Range(1, 2000).Select(i => i * 0.005).MaxBy(w => OscillatorPhysics.ShakenResponse(omega, damping, w));
        Assert.Equal(omega, peak, 1);

        // Shaken without air, it still loses enough to settle at Q times the shake, a little under the pull's limit.
        var shaken = OscillatorPhysics.ShakenDamping(omega, 0);
        Assert.Equal(OscillatorPhysics.ShakenQuality, OscillatorPhysics.ShakenResponse(omega, shaken, omega), 9);
        Assert.InRange(OscillatorPhysics.ShakenQuality * OscillatorPhysics.DriveAmplitude, 0.15, 0.25);
    }

    [Fact]
    public void ShakenSpringSettlesToTheSteadySwing()
    {
        const double omega = 5.0;
        const double damping = 0.8;
        const double drive = 4.0;
        const double dt = 1.0 / 1000;
        double Support(double t) => OscillatorPhysics.DriveAmplitude * Math.Sin(drive * t);

        var (height, velocity) = (0.0, 0.0);
        var largest = 0.0;
        for (var step = 0; step < 40_000; step++)
        {
            (height, velocity) = OscillatorPhysics.StepSpring(height, velocity, omega, damping, Support, step * dt, dt);
            if (step > 30_000) largest = Math.Max(largest, Math.Abs(height));
        }

        Assert.Equal(OscillatorPhysics.ShakenResponse(omega, damping, drive) * OscillatorPhysics.DriveAmplitude, largest, 4);
    }

    [Fact]
    public void ThePendulumWaveComesBackIntoLineEveryCycle()
    {
        // Lengths from about 34 cm down to 21 cm.
        Assert.Equal(0.344, OscillatorPhysics.WaveLength(0), 3);
        Assert.Equal(0.212, OscillatorPhysics.WaveLength(OscillatorPhysics.WaveCount - 1), 3);

        foreach (var place in Enum.GetValues<OscillatorPlace>())
        {
            var gravity = OscillatorPhysics.Gravity(place);
            var cycle = OscillatorPhysics.WaveCycle(gravity);
            for (var i = 0; i < OscillatorPhysics.WaveCount; i++)
            {
                Assert.Equal(OscillatorPhysics.WaveAmplitude, OscillatorPhysics.WaveAngle(i, cycle, gravity), 9);

                // Half way, neighbours swing opposite ways: two rows.
                var half = OscillatorPhysics.WaveAngle(i, cycle / 2, gravity);
                Assert.Equal(i % 2 == 0 ? 1 : -1, Math.Sign(half) * Math.Sign(OscillatorPhysics.WaveAngle(0, cycle / 2, gravity)));
            }
        }

        Assert.Equal(60, OscillatorPhysics.WaveCycle(9.81), 9);
        Assert.InRange(OscillatorPhysics.WaveCycle(1.62), 147, 148);
    }

    [Theory]
    [InlineData(0.004, WavePattern.InLine, 1)]
    [InlineData(0.996, WavePattern.InLine, 1)]
    [InlineData(0.06, WavePattern.Snake, 1)]
    [InlineData(0.5, WavePattern.Rows, 2)]
    [InlineData(0.333, WavePattern.Rows, 3)]
    [InlineData(0.75, WavePattern.Rows, 4)]
    [InlineData(0.6, WavePattern.Rows, 5)]
    [InlineData(0.37, WavePattern.Mixed, 0)]
    public void ThePatternFollowsTheFractionOfTheCycle(double fraction, WavePattern pattern, int rows)
    {
        Assert.Equal(pattern, OscillatorPhysics.Pattern(fraction, out var found));
        Assert.Equal(rows, found);
    }

    [Theory]
    [InlineData(0.99, 0.0, SwingStage.Turning)]
    [InlineData(0.05, -1.0, SwingStage.Middle)]
    [InlineData(0.5, -1.0, SwingStage.Falling)]
    [InlineData(-0.5, 1.0, SwingStage.Falling)]
    [InlineData(-0.5, -1.0, SwingStage.Rising)]
    public void StagesFollowWhereItIsAndWhichWayItGoes(double offset, double velocity, SwingStage stage)
    {
        Assert.Equal(stage, OscillatorPhysics.Stage(offset, velocity, 1, 0.01));
        Assert.Equal(SwingStage.Resting, OscillatorPhysics.Stage(offset * 0.001, velocity, 0.001, 0.01));
    }
}
