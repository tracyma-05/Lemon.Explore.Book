using System.Numerics;
using Vector = Avalonia.Vector;
using Avalonia;
using Lemon.Explore.Book.Chapters.Flight;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class FlightModelTests
{
    [Fact]
    public void Airfoil_LeavesTheTrailingEdgeSmoothly_AndMeetsTheFreeStreamFarAway()
    {
        var wing = new Airfoil(0.092, 0.08, 6);

        // The quarter-chord point is the origin and the chord is 1: the trailing edge is 0.75 behind it, turned nose up.
        var te = wing.TrailingEdge;
        Assert.Equal(0.75, Math.Sqrt((te.X * te.X) + (te.Y * te.Y)), 6);
        Assert.True(te.Y < 0, "nose up puts the trailing edge low");
        Assert.True(wing.LeadingEdge.Y > 0);

        // Kutta: the speed just above and just below the trailing edge is the same (no flow round the sharp edge).
        var above = wing.Surface(0.02).Velocity.Length;
        var below = wing.Surface((2 * Math.PI) - 0.02).Velocity.Length;
        Assert.Equal(above, below, 1);

        var far = wing.Velocity(new Point(-40, 25));
        Assert.Equal(1, far.X, 2);
        Assert.Equal(0, far.Y, 2);

        // The air stands still where it splits at the front.
        Assert.True(wing.Velocity(Nudge(wing, wing.Stagnation)).Length < 0.08);
    }

    [Fact]
    public void Airfoil_SymmetricAtZero_GivesNoLiftAndAMirrorFlow()
    {
        var wing = new Airfoil(0.092, 0, 0);
        Assert.Equal(0, wing.LiftCoefficient, 9);
        Assert.Equal(0, wing.ZeroLiftAlpha, 6);
        var top = wing.PressureCoefficient(new Point(0.1, 0.12));
        var bottom = wing.PressureCoefficient(new Point(0.1, -0.12));
        Assert.Equal(top, bottom, 6);
    }

    [Fact]
    public void Airfoil_LiftsAboutTwoPiPerRadian_AndCamberLiftsAtZero()
    {
        var thin = new Airfoil(0.01, 0, 0);
        var slope = (new Airfoil(0.01, 0, 2).LiftCoefficient - new Airfoil(0.01, 0, -2).LiftCoefficient) / (4 * Math.PI / 180);
        Assert.InRange(slope, 2 * Math.PI, 2 * Math.PI * 1.06);
        Assert.Equal(0, thin.LiftCoefficient, 9);

        // A cambered wing lifts with no angle; its zero-lift angle is negative (about −2 × camber in radians).
        var cambered = new Airfoil(0.092, 0.08, 0);
        Assert.InRange(cambered.LiftCoefficient, 0.4, 0.6);
        Assert.InRange(cambered.ZeroLiftAlpha, -5, -3);
    }

    [Fact]
    public void Airfoil_PressureOverTheSurface_AddsUpToTheKuttaJoukowskiLift()
    {
        var wing = new Airfoil(0.092, 0.08, 5);
        var lift = 0.0;
        var drag = 0.0;
        const int n = 4000;
        for (var i = 0; i < n; i++)
        {
            var a = wing.Surface(2 * Math.PI * i / n);
            var b = wing.Surface(2 * Math.PI * (i + 1) / n);
            var mid = wing.Surface(2 * Math.PI * (i + 0.5) / n);
            var cp = 1 - mid.Velocity.SquaredLength;

            // From the trailing edge over the top runs anticlockwise round the wing: the outward normal is (dy, −dx).
            var edge = b.Point - a.Point;
            var outward = new Vector(edge.Y, -edge.X);
            lift += -cp * outward.Y;
            drag += -cp * outward.X;
        }

        Assert.Equal(wing.LiftCoefficient, lift, 2);

        // d'Alembert: an ideal fluid gives no drag.
        Assert.Equal(0, drag, 2);
        Assert.True(wing.PressureCoefficient(new Point(0, 0.1)) < 0, "suction over the top");
        Assert.True(wing.PressureCoefficient(new Point(0, -0.06)) > wing.PressureCoefficient(new Point(0, 0.1)));
    }

    [Fact]
    public void Airfoil_InverseMap_FindsThePointOutsideTheCircle()
    {
        var wing = new Airfoil(0.092, 0.08, 3);
        foreach (var angle in new[] { 0.3, 1.5, 2.8, 4.0, 5.5 })
        {
            var zeta = wing.Centre + Complex.FromPolarCoordinates(wing.Radius * 1.3, angle);
            var back = wing.Zeta(Airfoil.Map(zeta));
            Assert.Equal(zeta.Real, back.Real, 9);
            Assert.Equal(zeta.Imaginary, back.Imaginary, 9);
        }

        Assert.True(wing.Inside(new Point(0, 0.01)));
        Assert.False(wing.Inside(new Point(0, 0.4)));
        Assert.Equal(default, wing.Velocity(new Point(0, 0.01)));
    }

    [Fact]
    public void Airfoil_CamberLine_RunsFromNoseToTail_AboveTheChord()
    {
        var wing = new Airfoil(0.092, 0.08, 0);
        var line = wing.CamberLine();
        Assert.Equal(wing.LeadingEdge.X, line[0].X, 2);
        Assert.Equal(wing.TrailingEdge.X, line[^1].X, 2);
        var middle = line[line.Count / 2];
        Assert.InRange(middle.Y - ((wing.LeadingEdge.Y + wing.TrailingEdge.Y) / 2), 0.025, 0.06);
    }

    [Theory]
    [InlineData(WingShape.Cambered)]
    [InlineData(WingShape.Symmetric)]
    [InlineData(WingShape.Flat)]
    [InlineData(WingShape.Bird)]
    public void Section_Stalls_LosingLiftAndGainingDrag(WingShape shape)
    {
        var stall = WingSection.StallAlpha(shape, false);
        Assert.InRange(stall, 7, 16);
        var below = WingSection.Lift(shape, false, stall - 3);
        var past = WingSection.Lift(shape, false, stall + 6);
        Assert.True(past < below * 0.8, $"{shape}: {past} vs {below}");
        Assert.True(WingSection.Drag(shape, false, stall + 6) > WingSection.Drag(shape, false, stall - 3) * 3);
        Assert.Equal(0, WingSection.Separation(shape, false, 2), 6);
        Assert.Equal(1, WingSection.Separation(shape, false, stall + 6), 6);

        // The greatest lift is near the stall angle.
        var bestAlpha = Enumerable.Range(0, 300).Select(i => i * 0.1).MaxBy(a => WingSection.Lift(shape, false, a));
        Assert.InRange(bestAlpha, stall - 2.5, stall + 2.5);
    }

    [Fact]
    public void Section_UpsideDown_NeedsMoreAngleForTheSameLift()
    {
        Assert.True(WingSection.ZeroLiftAlpha(WingShape.Cambered, true) > 3);
        Assert.True(WingSection.Lift(WingShape.Cambered, true, 0) < 0);
        Assert.True(WingSection.Lift(WingShape.Cambered, true, 8) > 0);
    }

    [Fact]
    public void Atmosphere_ThinsWithHeight()
    {
        Assert.Equal(1.225, Trainer.Density(0), 3);
        Assert.InRange(Trainer.Density(5000), 0.73, 0.74);
        Assert.InRange(Trainer.Density(1000), 1.11, 1.12);
    }

    [Fact]
    public void Trainer_StallsAndGlidesLikeASmallPlane()
    {
        // About 90 km/h with four seats at sea level; a 60° bank (2 g) raises it by √2.
        var stall = Trainer.StallSpeed(1000, 0);
        Assert.InRange(stall * 3.6, 85, 100);
        Assert.Equal(stall * Math.Sqrt(2), Trainer.StallSpeed(1000, 0, Trainer.LoadFactor(60)), 6);
        Assert.InRange(Trainer.BestGlideRatio, 11, 13);
        Assert.InRange(Trainer.MaxLift(), 1.4, 1.7);

        // Least drag where the friction drag and the drag from lift are equal.
        var vmd = Trainer.MinDragSpeed(1000, 0);
        var (parasite, induced) = Trainer.LevelDrag(vmd, 1000, 0);
        Assert.Equal(parasite, induced, 6);
        Assert.True(Trainer.LevelDrag(vmd * 0.7, 1000, 0).Induced > Trainer.LevelDrag(vmd, 1000, 0).Induced);
    }

    [Fact]
    public void Trainer_TrimmedForLevelFlight_StaysLevel()
    {
        var trim = Trainer.TrimLevel(55, 1000, 1000);
        Assert.NotNull(trim);
        var (alpha, throttle) = trim.Value;
        Assert.InRange(alpha, 0.5, 2.5);
        Assert.InRange(throttle, 0.5, 0.9);

        var state = new FlightState(0, 1000, 55, 0, false);
        for (var i = 0; i < 600; i++) state = Trainer.Step(state, alpha, throttle, 1000, 0.1);
        Assert.InRange(state.Altitude, 995, 1005);
        Assert.InRange(state.Speed, 54.5, 55.5);

        // Too slow to carry the weight below the stall: no trim.
        Assert.Null(Trainer.TrimLevel(20, 1000, 0));
    }

    [Fact]
    public void Trainer_ClimbsOnThrust_WithLiftJustBelowWeight()
    {
        var eq = Trainer.Equilibrium(6, 1, 1000, 500);
        Assert.NotNull(eq);
        var (speed, path) = eq.Value;
        Assert.InRange(speed * 3.6, 120, 160);
        Assert.InRange(path * 180 / Math.PI, 4, 10);

        var forces = Trainer.Forces(new FlightState(0, 500, speed, path, false), 6, 1, 1000);
        Assert.True(forces.Lift < forces.Weight);
        Assert.True(forces.Thrust > forces.Drag);
    }

    [Fact]
    public void Trainer_GlidesAboutTwelveToOne_WithTheEngineOff()
    {
        var alpha = Trainer.AlphaFor(Trainer.BestGlideLift);
        var state = new FlightState(0, 1500, 40, -0.08, false);
        for (var i = 0; i < 300; i++) state = Trainer.Step(state, alpha, 0, 1000, 0.1);
        var start = state;
        for (var i = 0; i < 300; i++) state = Trainer.Step(state, alpha, 0, 1000, 0.1);
        var ratio = (state.X - start.X) / (start.Altitude - state.Altitude);
        Assert.InRange(ratio, 11, 13);
    }

    [Fact]
    public void Trainer_TakesOffAfterAShortRun()
    {
        var state = new FlightState(0, 0, 0, 0, true);
        double? liftOff = null;
        for (var i = 0; i < 900 && state.Altitude < 15; i++)
        {
            var alpha = state.Speed > 28 ? 10 : 0;
            state = Trainer.Step(state, alpha, 1, 1000, 0.05);
            if (!state.OnGround && liftOff is null) liftOff = state.X;
        }

        Assert.NotNull(liftOff);
        Assert.InRange(liftOff.Value, 150, 600);
        Assert.True(state.Altitude >= 15);

        // With the engine off and the brakes on, the run stops.
        var rolling = new FlightState(0, 0, 20, 0, true);
        for (var i = 0; i < 400; i++) rolling = Trainer.Step(rolling, 0, 0, 1000, 0.05);
        Assert.Equal(0, rolling.Speed, 6);
    }

    [Fact]
    public void Trainer_PastTheStall_Sinks()
    {
        var state = new FlightState(0, 1000, 28, 0, false);
        for (var i = 0; i < 100; i++) state = Trainer.Step(state, 20, 0.3, 1000, 0.05);
        Assert.True(state.Altitude < 990);
        Assert.True(Trainer.Separation(20) > 0.9);
        Assert.True(Trainer.LiftCoefficient(20) < Trainer.LiftCoefficient(14));
    }

    [Fact]
    public void Turn_FollowsTheTextbookFormulas()
    {
        Assert.Equal(2, Trainer.LoadFactor(60), 9);
        Assert.Equal(1, Trainer.LoadFactor(0), 9);
        Assert.True(double.IsPositiveInfinity(Trainer.TurnRadius(50, 0)));
        Assert.Equal(50 * 50 / (Trainer.G * Math.Tan(Math.PI / 6)), Trainer.TurnRadius(50, 30), 6);
        Assert.Equal(-Trainer.TurnRate(50, 30), Trainer.TurnRate(50, -30), 9);

        // A full circle takes 2πr / v.
        var seconds = 360 / Trainer.TurnRate(50, 30);
        Assert.Equal(2 * Math.PI * Trainer.TurnRadius(50, 30) / 50, seconds, 6);
    }

    /// <summary>A surface point nudged a hair outwards (off the wing) so the flow there is defined.</summary>
    private static Point Nudge(Airfoil wing, Point point)
    {
        var zeta = wing.Zeta(wing.ToModel(point));
        return wing.ToDisplay(Airfoil.Map(wing.Centre + ((zeta - wing.Centre) * 1.002)));
    }
}
