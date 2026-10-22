using System;
using System.Linq;
using Lemon.Explore.Book.Chapters.BlackHole;
using Xunit;

namespace Lemon.Explore.Book.Tests;

/// <summary>
/// The black-hole page's physics: light bending against Einstein's weak-field result and the exact critical ray, the
/// orbits' energy, angular momentum and the innermost stable orbit, the probe's integration, clocks, and lensing.
/// </summary>
public sealed class BlackHoleModelTests
{
    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(400)]
    public void FarRays_BendByEinsteinsFourOverB(double b)
    {
        // Second order: 4/b + 15π/(4b²).
        var expected = (4 / b) + (15 * Math.PI / (4 * b * b));
        var deflection = BlackHoleModel.Deflection(b, 0.0005);
        Assert.NotNull(deflection);
        Assert.Equal(expected, deflection!.Value, 3);
        Assert.InRange(deflection.Value / expected, 0.99, 1.01);
    }

    [Fact]
    public void TheSunBendsStarlightBy1Point75Arcseconds()
    {
        // The Sun's radius is 696,000 km = 471,000 M of the Sun.
        var b = 696_000 / BlackHoleModel.SunMassKm;
        var arcsec = 4 / b * 180 / Math.PI * 3600;
        Assert.InRange(arcsec, 1.74, 1.76);
    }

    [Fact]
    public void RaysInsideTheCriticalImpactFallIn_OutsideEscape()
    {
        var bc = BlackHoleModel.CriticalImpact;
        Assert.Equal(5.196152, bc, 5);
        Assert.True(BlackHoleModel.TraceLight(bc * 0.995, 0, 0.002).Captured);
        Assert.False(BlackHoleModel.TraceLight(bc * 1.005, 0, 0.002).Captured);

        // Just outside, the ray comes close to the photon sphere and is turned through more than a full circle.
        var near = BlackHoleModel.TraceLight(bc * 1.0005, 0, 0.002);
        Assert.False(near.Captured);
        Assert.InRange(near.Closest, 3, 3.2);
        Assert.True(near.EndAngle - Math.PI > 2 * Math.PI);
    }

    [Fact]
    public void ClosestApproach_MatchesTheOrbitEquation()
    {
        // For a ray turning at r0: b = r0 / √(1 − 2/r0).
        foreach (var r0 in new[] { 4.0, 6, 10, 30 })
        {
            var b = r0 / Math.Sqrt(1 - (2 / r0));
            var path = BlackHoleModel.TraceLight(b, 0, 0.001);
            Assert.False(path.Captured);
            Assert.Equal(r0, path.Closest, 2);
        }
    }

    [Fact]
    public void CircularOrbits_EnergyAndMomentum()
    {
        // The innermost stable orbit: L = √12, E = √(8/9).
        Assert.Equal(Math.Sqrt(12), BlackHoleModel.CircularAngularMomentum(6), 9);
        Assert.Equal(Math.Sqrt(8.0 / 9), BlackHoleModel.CircularEnergy(6), 9);

        // The potential's bottom and top for L = 4: r = 12 and r = 4.
        var turns = BlackHoleModel.PotentialTurns(4);
        Assert.NotNull(turns);
        Assert.Equal(4, turns!.Value.Barrier, 9);
        Assert.Equal(12, turns.Value.Well, 9);
        Assert.Null(BlackHoleModel.PotentialTurns(3));
    }

    [Fact]
    public void AProbeOnACircularOrbitStaysOnIt_AndItsClockIsSlow()
    {
        var (r, rDot, l) = Probe.Launch(ProbeLaunch.Circle);
        var probe = new Probe(r, 0, rDot, l, newton: false);
        for (var i = 0; i < 400; i++)
        {
            probe.Advance(2);
            Assert.InRange(probe.R, r - 0.02, r + 0.02);
        }

        // dτ/dt = √(1 − 3/r) on a circular orbit.
        Assert.Equal(Math.Sqrt(1 - (3 / r)), probe.ProperTime / probe.FarTime, 3);

        // One lap in far-away time is 2π r^1.5, Kepler's law exactly.
        var lapTime = 2 * Math.PI * Math.Pow(r, 1.5);
        Assert.Equal(probe.FarTime / lapTime, probe.Phi / (2 * Math.PI), 3);
    }

    [Fact]
    public void TheRosette_TurnsAbout100DegreesEachLap_NewtonsDoesNot()
    {
        var (r, rDot, l) = Probe.Launch(ProbeLaunch.Rosette);
        double FarTurnAngle(bool newton)
        {
            var probe = new Probe(r, 0, rDot, l, newton);
            var wasIn = false;
            var lastR = probe.R;
            for (var i = 0; i < 400_000; i++)
            {
                probe.Advance(0.05);
                if (probe.R < lastR) wasIn = true;
                if (wasIn && probe.R > lastR && probe.RDot > 0 && probe.R > 15)
                {
                    // Past the next far point: go on until dr/dτ turns negative.
                    while (probe.RDot > 0) probe.Advance(0.01);
                    return probe.Phi;
                }

                lastR = probe.R;
            }

            return double.NaN;
        }

        var gr = FarTurnAngle(newton: false) - (2 * Math.PI);
        var newtonShift = FarTurnAngle(newton: true) - (2 * Math.PI);
        Assert.InRange(gr * 180 / Math.PI, 80, 140);
        Assert.InRange(newtonShift * 180 / Math.PI, -2, 2);
    }

    [Theory]
    [InlineData(ProbeLaunch.Plunge, true)]
    [InlineData(ProbeLaunch.Isco, true)]
    [InlineData(ProbeLaunch.Escape, false)]
    public void ThePlungeAndTheNudgedIscoFallIn_TheFastOneEscapes(ProbeLaunch launch, bool fallsIn)
    {
        var (r, rDot, l) = Probe.Launch(launch);
        var probe = new Probe(r, 0, rDot, l, newton: false);
        for (var i = 0; i < 100_000 && !probe.IsLost && !probe.HasEscaped; i++) probe.Advance(0.5);
        Assert.Equal(fallsIn, probe.IsLost);
        Assert.Equal(!fallsIn, probe.HasEscaped);
        if (fallsIn)
        {
            // Seen from far away it never quite gets there.
            Assert.True(double.IsPositiveInfinity(probe.FarTime));
            Assert.True(probe.ProperTime < 5000);
        }

        // Newton's plunging probe with the same start just swings by.
        if (launch == ProbeLaunch.Plunge)
        {
            var newton = new Probe(r, 0, rDot, l, newton: true);
            for (var i = 0; i < 2000; i++) newton.Advance(0.5);
            Assert.False(newton.IsLost);
        }
    }

    [Fact]
    public void TheDisk_AnApproachingSideIsBlueshifted()
    {
        // Seen edge on, light from the approaching side leaves with Lz ≈ +b along the orbit.
        var coming = BlackHoleModel.DiskShift(10, 10);
        var going = BlackHoleModel.DiskShift(10, -10);
        Assert.True(coming > 1);
        Assert.True(going < 0.8);
        Assert.Equal(Math.Sqrt(0.7), BlackHoleModel.DiskShift(10, 10, doppler: false), 9);

        Assert.Equal(0, BlackHoleModel.DiskGlow(6));
        Assert.Equal(1, BlackHoleModel.DiskGlow(49.0 / 6), 9);
        Assert.True(BlackHoleModel.DiskGlow(20) < BlackHoleModel.DiskGlow(10));
    }

    [Fact]
    public void Lensing_ImagesAndBrightening()
    {
        // Both images map back to the source.
        foreach (var u in new[] { 0.1, 0.5, 1, 2 })
        {
            var (outer, inner, outerGain, innerGain) = BlackHoleModel.LensImages(u);
            Assert.Equal(u, BlackHoleModel.LensSourceOf(outer, 0).X, 9);
            Assert.Equal(u, BlackHoleModel.LensSourceOf(inner, 0).X, 9);
            Assert.Equal(BlackHoleModel.Magnification(u), outerGain + innerGain, 9);
            Assert.Equal(1, outerGain - innerGain, 9);
        }

        // One Einstein radius away: 1.34 times as bright.
        Assert.Equal(3 / Math.Sqrt(5), BlackHoleModel.Magnification(1), 9);
    }

    [Fact]
    public void Examples_HaveTheirKnownSizes()
    {
        // The Sun as a black hole: 2.95 km across... as a radius.
        Assert.Equal(2.95, BlackHoleModel.SchwarzschildKm(1), 2);

        // M87*'s shadow ≈ 40 µas (EHT 2019: ring 42 ± 3 µas), Sgr A*'s ≈ 50 µas (EHT 2022: 51.8 ± 2.3).
        Assert.InRange(BlackHoleExample.M87.ShadowMicroArcsec, 36, 46);
        Assert.InRange(BlackHoleExample.SgrA.ShadowMicroArcsec, 45, 58);

        // Microlensing by a star-sized hole: about a milliarcsecond; a galaxy: about an arcsecond; a cluster: tens.
        Assert.InRange(LensExample.StellarHole.EinsteinArcsec() * 1000, 3, 8);
        Assert.InRange(LensExample.Galaxy.EinsteinArcsec(), 1, 5);
        Assert.InRange(LensExample.Cluster.EinsteinArcsec(), 20, 90);
    }

    [Fact]
    public void TheRayTable_AgreesWithTheShadowSize()
    {
        var rays = ShadowRenderer.Rays(bent: true);
        var edge = rays.First(r => !r.Captured).Impact;
        Assert.InRange(edge, BlackHoleModel.CriticalImpact - 0.02, BlackHoleModel.CriticalImpact + 0.02);

        var straight = ShadowRenderer.Rays(bent: false);
        Assert.InRange(straight.First(r => !r.Captured).Impact * Math.Sqrt(1 - (2 / ShadowRenderer.CameraDistance)), 1.99, 2.02);
    }
}
