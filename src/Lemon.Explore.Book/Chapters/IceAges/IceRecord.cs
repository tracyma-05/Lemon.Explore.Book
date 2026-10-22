namespace Lemon.Explore.Book.Chapters.IceAges;

/// <summary>The ice model's three states (Paillard 1998).</summary>
public enum IceState
{
    /// <summary>Interglacial: little ice outside Greenland and Antarctica, as now.</summary>
    Interglacial,

    /// <summary>Glacial: the northern ice sheets growing (or holding) on and off.</summary>
    Glacial,

    /// <summary>Full glacial: the sheets so big that the next strong summer sunshine brings them down fast.</summary>
    FullGlacial,
}

/// <summary>One moment of the ice model.</summary>
/// <param name="Kyr">Thousand years from 1950 (negative: ago).</param>
/// <param name="Sunshine">Midsummer daily sunshine at 65° N, W/m².</param>
/// <param name="Forcing">That sunshine, softened at the low end and scaled to mean 0 and spread 1 over the last million years.</param>
/// <param name="Ice">Northern ice volume in the model's units: about 0 now and 1.3 at the last glacial maximum.</param>
/// <param name="State">Which of the three states the model is in.</param>
/// <param name="Ground">How far the ground under the ice has sunk, as the ice it has caught up with (lags ~3,000 years).</param>
public readonly record struct IceMoment(double Kyr, double Sunshine, double Forcing, double Ice, IceState State, double Ground)
{
    /// <summary>The northern ice beyond today's (Greenland's), as a share of the last glacial maximum's: 0 now, 1 then.</summary>
    public double IceShare => Math.Clamp(Beyond(Ice), 0, 1.15);

    /// <summary>The ice the sunk ground has caught up with, on the same scale as <see cref="IceShare"/>.</summary>
    public double GroundShare => Math.Clamp(Beyond(Ground), 0, 1.15);

    /// <summary>Sea level against today's, metres (−120 at the last glacial maximum; a little higher in warm interglacials).</summary>
    public double SeaLevel => Math.Clamp(-IceRecord.SeaDrop * Beyond(Ice), -140, 8);

    /// <summary>Carbon dioxide in the air in this model, ppm: about 280 in warm times and 185 at the coldest (ice cores: 180–300).</summary>
    public double Co2 => Math.Clamp(280 - (95 * Beyond(Ice)), 180, 300);

    /// <summary>Global mean temperature against pre-industrial, °C, scaled with the ice (about −6 at the last glacial maximum).</summary>
    public double Temperature => Math.Clamp(-6 * Beyond(Ice), -7, 2);

    private static double Beyond(double ice) => (ice - IceRecord.TodayIce) / (IceRecord.MaximumIce - IceRecord.TodayIce);
}

/// <summary>
/// The northern ice sheets through time, from the three-state conceptual model of Paillard (1998, Nature 391): ice
/// volume v relaxes towards 0 (interglacial, 10 kyr) or 1 (glacial, 50 kyr) and is pushed by the midsummer sunshine at
/// 65° N (dv/dt = (v_R − v)/τ_R − F/τ_F, τ_F = 25 kyr). The model leaves an interglacial when F drops below −0.75, enters
/// full glacial once v passes 1, and a full glacial ends (a termination) when F rises above 0. Driven by
/// <see cref="IceAgeModel"/>, it finds the last ten deglaciations within a few thousand years of the ocean record. The
/// table runs from 1.2 million years ago (spin-up) to 100,000 years ahead, every 100 years.
/// </summary>
public sealed class IceRecord
{
    /// <summary>The earliest time shown, thousand years from 1950.</summary>
    public const double Start = -800;

    /// <summary>The latest time shown: 100,000 years ahead (without the carbon dioxide people add).</summary>
    public const double End = 100;

    /// <summary>The table's step, thousand years.</summary>
    public const double Step = 0.1;

    /// <summary>The model's ice at the last glacial maximum, used to scale everything shown.</summary>
    public const double MaximumIce = 1.3;

    /// <summary>The model's ice today (it does not reach 0 in the Holocene).</summary>
    public const double TodayIce = 0.17;

    /// <summary>Sea level at the last glacial maximum against today's, metres.</summary>
    public const double SeaDrop = 120;

    private const double SpinUp = -1200;
    private const double LowThreshold = -0.75;
    private const double HighThreshold = 0;
    private const double FullIce = 1;
    private const double ForcingTime = 25;
    private const double GroundTime = 3;

    private static readonly Lazy<IceRecord> Shared = new(() => new IceRecord());

    private readonly IceMoment[] _moments;

    /// <summary>The one table, built on first use (about 13,000 steps, a few milliseconds).</summary>
    public static IceRecord Instance => Shared.Value;

    private IceRecord()
    {
        var count = (int)Math.Round((End - SpinUp) / Step) + 1;
        var sunshine = new double[count];
        for (var i = 0; i < count; i++) sunshine[i] = IceAgeModel.SummerSunshine(SpinUp + (i * Step));

        // Scaled over the last million years: softened low values (they matter less), then mean 0 and spread 1.
        var (mean, spread) = Stats(sunshine, count);
        var soft = sunshine.Select(s => Soften((s - mean) / spread)).ToArray();
        var (softMean, softSpread) = Stats(soft, count);
        MeanSunshine = mean;
        SunshineSpread = spread;

        _moments = new IceMoment[count];
        var ice = 0.5;
        var ground = 0.5;
        var state = IceState.Glacial;
        for (var i = 0; i < count; i++)
        {
            var forcing = (soft[i] - softMean) / softSpread;
            state = state switch
            {
                IceState.Interglacial when forcing < LowThreshold => IceState.Glacial,
                IceState.Glacial when ice > FullIce => IceState.FullGlacial,
                IceState.FullGlacial when forcing > HighThreshold => IceState.Interglacial,
                _ => state,
            };
            var (target, time) = state == IceState.Interglacial ? (0.0, 10.0) : (1.0, 50.0);
            ice += (((target - ice) / time) - (forcing / ForcingTime)) * Step;
            ground += (ice - ground) / GroundTime * Step;
            _moments[i] = new IceMoment(SpinUp + (i * Step), sunshine[i], forcing, ice, state, ground);
        }

        Terminations = FindTerminations();
    }

    /// <summary>The mean of the midsummer sunshine at 65° N over the last million years, W/m².</summary>
    public double MeanSunshine { get; }

    /// <summary>The spread (standard deviation) of that sunshine, W/m².</summary>
    public double SunshineSpread { get; }

    /// <summary>When the model's full glacials ended, thousand years from 1950 (from the earliest shown on).</summary>
    public IReadOnlyList<double> Terminations { get; }

    /// <summary>The model at <paramref name="kyr"/> thousand years from 1950, read between table rows.</summary>
    public IceMoment At(double kyr)
    {
        var x = (Math.Clamp(kyr, SpinUp, End) - SpinUp) / Step;
        var i = Math.Min((int)x, _moments.Length - 2);
        var f = x - i;
        var a = _moments[i];
        var b = _moments[i + 1];
        return new IceMoment(kyr, Lerp(a.Sunshine, b.Sunshine, f), Lerp(a.Forcing, b.Forcing, f), Lerp(a.Ice, b.Ice, f),
            f < 0.5 ? a.State : b.State, Lerp(a.Ground, b.Ground, f));
    }

    /// <summary>Whether the ice is growing (positive), shrinking (negative), per thousand years, at <paramref name="kyr"/>.</summary>
    public double IceTrend(double kyr) => (At(kyr + 0.5).Ice - At(kyr - 0.5).Ice) / 1.0;

    /// <summary>Rows from <paramref name="from"/> to <paramref name="to"/>, every <paramref name="every"/> thousand years.</summary>
    public IEnumerable<IceMoment> Range(double from, double to, double every)
    {
        for (var t = from; t <= to + 1e-9; t += every) yield return At(t);
    }

    private List<double> FindTerminations()
    {
        var list = new List<double>();
        for (var i = 1; i < _moments.Length; i++)
        {
            if (_moments[i - 1].State == IceState.FullGlacial && _moments[i].State == IceState.Interglacial && _moments[i].Kyr >= Start)
                list.Add(_moments[i].Kyr);
        }

        return list;
    }

    /// <summary>Paillard's softening: high values pass, low ones are squeezed (f(x) = (x + √(4a² + x²) − 2a)/2, a = 1).</summary>
    private static double Soften(double x) => (x + Math.Sqrt(4 + (x * x)) - 2) / 2;

    /// <summary>Mean and spread over the rows between 1 million years ago and 1950.</summary>
    private static (double Mean, double Spread) Stats(double[] values, int count)
    {
        double sum = 0, squares = 0;
        var n = 0;
        for (var i = 0; i < count; i++)
        {
            var t = SpinUp + (i * Step);
            if (t < -1000 - 1e-9 || t > 1e-9) continue;
            var v = values[i];
            sum += v;
            squares += v * v;
            n++;
        }

        var mean = sum / n;
        return (mean, Math.Sqrt(Math.Max(1e-12, (squares / n) - (mean * mean))));
    }

    private static double Lerp(double a, double b, double f) => a + ((b - a) * f);
}
