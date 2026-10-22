using Avalonia;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Aurora;

/// <summary>
/// One charged particle trapped in Earth's dipole field, moved by its guiding centre: along the field line between the
/// mirror points (worked out exactly from the field, keeping its magnetic moment) and round Earth by its bounce-averaged
/// drift, with the gyration drawn round the centre. Lengths are in Earth radii, the magnetic axis along z; time is in
/// units in which the particle covers one Earth radius.
/// </summary>
/// <remarks>
/// Only the proportions are scaled: a real particle winds thousands of times per bounce and bounces hundreds of times per
/// drift orbit, far too small and too slow to see together. Here it winds <see cref="GyrationsPerBounce"/> times (at the
/// equator's rate, more near the mirror points where the field is stronger), the spiral's radius is enlarged, and one drift
/// orbit takes <see cref="BouncesPerDrift"/> bounces. Where it turns back, and that it does not when inside the loss cone,
/// are exact.
/// </remarks>
internal sealed class TrappedParticle
{
    /// <summary>Gyrations per bounce at the equator's rate (a real electron makes tens of thousands).</summary>
    public const double GyrationsPerBounce = 14;

    /// <summary>Bounces per drift orbit (really hundreds or thousands).</summary>
    public const double BouncesPerDrift = 12;

    /// <summary>Distance between kept points of the path, Earth radii.</summary>
    private const double TrailSpacing = 0.03;

    private const int MaxTrail = 6000;

    private const int TableSize = 240;

    private readonly Vec3[] _trail = new Vec3[MaxTrail];
    private readonly double[] _trailTime = new double[MaxTrail];
    private int _trailStart;
    private int _trailCount;
    private Vec3 _lastKept;

    /// <summary>The highest latitude reached, radians: the mirror point, or the air at the foot for a particle in the loss cone.</summary>
    private readonly double _turn;

    /// <summary>Time from the equator to the angle θ_k = k π / 2 / (TableSize − 1), where λ = turn × sin θ.</summary>
    private readonly double[] _quarter = new double[TableSize];

    private readonly double _mirrorRatio;
    private readonly double _driftRate;
    private readonly double _gyroRate;
    private readonly double _radius;
    private readonly double _startLongitude;
    private double _gyroPhase;
    private double _latitude;

    public TrappedParticle(AuroraSpecies species, double shell, double pitchDegrees, double startLongitudeDegrees = 0)
    {
        Species = species;
        Shell = shell;
        PitchDegrees = Math.Clamp(pitchDegrees, 0.5, 90);
        var s = Math.Sin(PitchDegrees * Math.PI / 180);
        _mirrorRatio = 1 / (s * s);
        var mirror = AuroraModel.MirrorLatitude(PitchDegrees);
        var air = AuroraModel.FootLatitude(shell, AuroraModel.AtmosphereKm);
        WillBeLost = mirror >= air;
        _turn = Math.Min(mirror, air) * Math.PI / 180;
        BuildTable();

        BounceTime = WillBeLost ? _quarter[^1] : 4 * _quarter[^1];
        _gyroRate = (species == AuroraSpecies.Electron ? 1 : -1) * GyrationsPerBounce * 2 * Math.PI / (4 * shell * AuroraModel.BounceIntegrals(PitchDegrees).T);
        _driftRate = (species == AuroraSpecies.Electron ? 1 : -1) * 2 * Math.PI / (BouncesPerDrift * 4 * shell * AuroraModel.BounceIntegrals(PitchDegrees).T);
        _radius = 0.3 * s * Math.Sqrt(shell / 4);
        _startLongitude = startLongitudeDegrees * Math.PI / 180;
        Update();
        _lastKept = Position;
        Keep(Position, 0);
    }

    public AuroraSpecies Species { get; }

    public double Shell { get; }

    public double PitchDegrees { get; }

    /// <summary>Its pitch angle is inside the loss cone: it will reach the air at the first foot it comes to.</summary>
    public bool WillBeLost { get; }

    public Vec3 Position { get; private set; }

    /// <summary>Its velocity (unit length): along the field and round the gyration.</summary>
    public Vec3 Velocity { get; private set; }

    /// <summary>Its guiding centre, on the field line.</summary>
    public Vec3 Center { get; private set; }

    public double Time { get; private set; }

    /// <summary>The particle came down into the air (below <see cref="AuroraModel.AtmosphereKm"/>) and stopped.</summary>
    public bool IsLost { get; private set; }

    public int EquatorCrossings { get; private set; }

    /// <summary>The farthest it has got from the equator so far, degrees of magnetic latitude (north and south).</summary>
    public double HighestNorth { get; private set; }

    public double HighestSouth { get; private set; }

    /// <summary>One bounce, pole to pole and back (or for a particle in the loss cone, the way down to the air).</summary>
    public double BounceTime { get; }

    /// <summary>The magnetic longitude of the guiding centre, degrees: drifting east for electrons, west for protons.</summary>
    public double Longitude => DriftAngle * 180 / Math.PI;

    private double DriftAngle => _startLongitude + (_driftRate * Time);

    /// <summary>The dipole field (1 on the ground at the equator, pointing north there).</summary>
    public static Vec3 Field(Vec3 p)
    {
        var r2 = p.Dot(p);
        var r = Math.Sqrt(r2);
        var r5 = r2 * r2 * r;
        return new Vec3(-3 * p.X * p.Z / r5, -3 * p.Y * p.Z / r5, (r2 - (3 * p.Z * p.Z)) / r5);
    }

    /// <summary>
    /// The time to go from the equator up to each latitude, ∫ ds / v∥ with λ = turn × sin θ (which takes the square-root
    /// slowing at the mirror point out of the integral).
    /// </summary>
    private void BuildTable()
    {
        const int steps = 2000;
        var total = 0.0;
        var previous = Integrand(0);
        _quarter[0] = 0;
        var next = 1;
        for (var i = 1; i <= steps; i++)
        {
            var theta = Math.PI / 2 * i / steps;
            var value = Integrand(theta);
            total += (previous + value) / 2 * (Math.PI / 2 / steps);
            previous = value;
            while (next < TableSize && (double)next / (TableSize - 1) <= (double)i / steps + 1e-12)
            {
                _quarter[next++] = total;
            }
        }
    }

    /// <summary>dt/dθ: the field line's length per latitude over the speed along it.</summary>
    private double Integrand(double theta)
    {
        var lambda = _turn * Math.Sin(theta);
        var (sl, cl) = Math.SinCos(lambda);
        var length = Shell * cl * Math.Sqrt(1 + (3 * sl * sl));
        var along = 1 - (AuroraModel.FieldRatio(lambda * 180 / Math.PI) / _mirrorRatio);
        // At the mirror point both cos θ and v∥ go to zero; their ratio stays finite.
        var cos = Math.Cos(theta);
        if (along <= 1e-12 || cos <= 1e-9)
        {
            if (WillBeLost) return 0;
            var h = 1e-4;
            return Integrand(Math.PI / 2 - h);
        }

        return _turn * cos * length / Math.Sqrt(along);
    }

    /// <summary>The latitude (radians) a quarter-bounce time <paramref name="t"/> after leaving the equator.</summary>
    private double LatitudeAfter(double t)
    {
        var total = _quarter[^1];
        if (t >= total) return _turn;
        var lo = 0;
        var hi = TableSize - 1;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (_quarter[mid] <= t) lo = mid;
            else hi = mid;
        }

        var f = (t - _quarter[lo]) / Math.Max(1e-12, _quarter[hi] - _quarter[lo]);
        var theta = Math.PI / 2 * (lo + f) / (TableSize - 1);
        return _turn * Math.Sin(theta);
    }

    /// <summary>Moves the particle on by <paramref name="duration"/>, in steps of a sixteenth of a local gyration.</summary>
    public void Advance(double duration)
    {
        var left = duration;
        var guard = 0;
        while (left > 0 && !IsLost && guard++ < 100_000)
        {
            var ratio = AuroraModel.FieldRatio(_latitude * 180 / Math.PI);
            var dt = Math.Min(left, 2 * Math.PI / Math.Abs(_gyroRate * ratio) / 16);
            dt = Math.Min(dt, BounceTime / 400);
            left -= dt;
            Time += dt;
            _gyroPhase += _gyroRate * ratio * dt;

            var before = _latitude;
            Update();
            if (Math.Sign(before) != Math.Sign(_latitude) && before != 0) EquatorCrossings++;

            if ((Position - _lastKept).Length >= TrailSpacing || IsLost)
            {
                _lastKept = Position;
                Keep(Position, Time);
            }
        }
    }

    /// <summary>Puts the guiding centre and the particle where they are at <see cref="Time"/>.</summary>
    private void Update()
    {
        var quarter = _quarter[^1];
        double latitude;
        int direction;
        if (WillBeLost)
        {
            latitude = LatitudeAfter(Math.Min(Time, quarter));
            direction = 1;
            if (Time >= quarter) IsLost = true;
        }
        else
        {
            var tau = Time % (4 * quarter);
            (latitude, direction) = tau switch
            {
                _ when tau < quarter => (LatitudeAfter(tau), 1),
                _ when tau < 2 * quarter => (LatitudeAfter((2 * quarter) - tau), -1),
                _ when tau < 3 * quarter => (-LatitudeAfter(tau - (2 * quarter)), -1),
                _ => (-LatitudeAfter((4 * quarter) - tau), 1),
            };
        }

        _latitude = latitude;
        var degrees = latitude * 180 / Math.PI;
        if (degrees > HighestNorth) HighestNorth = degrees;
        if (-degrees > HighestSouth) HighestSouth = -degrees;

        var phi = DriftAngle;
        var (sl, cl) = Math.SinCos(latitude);
        var (sp, cp) = Math.SinCos(phi);
        var r = Shell * cl * cl;
        Center = new Vec3(r * cl * cp, r * cl * sp, r * sl);

        var b = Field(Center);
        var along = b * (1 / b.Length);
        var east = new Vec3(-sp, cp, 0);
        var other = along.Cross(east);
        var ratio = AuroraModel.FieldRatio(degrees);
        var radius = _radius / Math.Sqrt(ratio);
        var (sg, cg) = Math.SinCos(_gyroPhase);
        Position = Center + (((east * cg) + (other * sg)) * radius);

        // Speed along the field from the kept magnetic moment; the rest goes round the gyration.
        var parallel = Math.Sqrt(Math.Max(0, 1 - (ratio / _mirrorRatio)));
        if (WillBeLost) parallel = Math.Max(parallel, 1e-3);
        var perpendicular = Math.Sqrt(Math.Max(0, 1 - (parallel * parallel)));
        var turning = Math.Sign(_gyroRate);
        var round = ((east * -sg) + (other * cg)) * turning;
        Velocity = (along * (parallel * direction)) + (round * perpendicular);
    }

    private void Keep(Vec3 point, double time)
    {
        var index = (_trailStart + _trailCount) % MaxTrail;
        _trail[index] = point;
        _trailTime[index] = time;
        if (_trailCount < MaxTrail) _trailCount++;
        else _trailStart = (_trailStart + 1) % MaxTrail;
    }

    /// <summary>The kept points of the path, oldest first, with the time each was passed.</summary>
    public IEnumerable<(Vec3 Point, double Time)> Trail()
    {
        for (var i = 0; i < _trailCount; i++)
        {
            var index = (_trailStart + i) % MaxTrail;
            yield return (_trail[index], _trailTime[index]);
        }
    }
}
