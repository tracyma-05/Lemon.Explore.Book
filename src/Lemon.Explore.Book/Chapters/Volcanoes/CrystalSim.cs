namespace Lemon.Explore.Book.Chapters.Volcanoes;

/// <summary>Which magma cools in the magnifier: silica-rich (granite, rhyolite) or silica-poor (gabbro, basalt).</summary>
public enum MagmaKind
{
    Felsic,
    Mafic,
}

/// <summary>A mineral that crystallises from a magma: below which temperature it starts, its share of the finished rock, its shape.</summary>
/// <param name="Id">Name: <c>Volc_Mineral_{Id}</c>.</param>
/// <param name="StartC">The temperature it starts to crystallise at, °C.</param>
/// <param name="Share">Its share of the rock once all the melt has crystallised.</param>
/// <param name="Aspect">Length over width of its crystals (feldspar laths 2.5, quartz filling the gaps 1).</param>
public sealed record Mineral(string Id, double StartC, double Share, double Aspect);

/// <summary>
/// A slice of magma 20 mm wide cooling from its liquidus to its solidus, cell by cell: crystals of each mineral appear
/// (nucleate) once the melt is cool enough for it, in the order of Bowen's reaction series, and grow as ellipses until they
/// meet their neighbours. Slow cooling makes few crystals with time to grow big (granite); fast cooling makes many that
/// stay small (basalt); cooling faster still leaves no time even for that and the melt freezes as glass (obsidian). Gas
/// that cannot escape leaves bubbles (pumice).
/// </summary>
/// <remarks>
/// <para>
/// Cooling rate r is log₁₀ of °C a year: −3 deep in a pluton, 0 in a thin intrusion, about 4 inside a lava flow, 7 when lava
/// meets water. The crystals made over the whole cooling grow as 10^(1.6 + 0.33(r + 3)) up to r = 4.5 and then fall again
/// (both nucleation and growth stall when the melt is very undercooled); each crystal's minor radius grows by
/// 0.2·10^(−0.1(r + 3)) cells a step, ten times less again for every 2.5 past r = 4.5. These are fitted to give the textbook textures, not measured.
/// </para>
/// <para>
/// Deterministic for a seed: the same settings give the same rock, so going back on the timeline replays to that step.
/// </para>
/// </remarks>
public sealed class CrystalSim
{
    public const int Width = 180;
    public const int Height = 120;

    /// <summary>Steps from liquidus to solidus.</summary>
    public const int Steps = 240;

    /// <summary>The slice's width, mm.</summary>
    public const double FieldMm = 20;

    /// <summary>Cell values that are not a crystal: melt still liquid, glass, a gas bubble.</summary>
    public const int Melt = 0;

    public const int Glass = -1;
    public const int Bubble = -2;

    /// <summary>The cooling rate of a two-stage cooling's first, slow stage and second, fast stage (log₁₀ °C a year).</summary>
    public const double SlowStage = -1.5;

    public const double FastStage = 4.5;

    /// <summary>The share of the steps a two-stage cooling spends deep down before it erupts.</summary>
    public const double FirstStageShare = 0.45;

    private static readonly Mineral[] FelsicMinerals =
    [
        new("Plagioclase", 880, 0.30, 2.6),
        new("Biotite", 830, 0.10, 3.2),
        new("KFeldspar", 780, 0.30, 2.0),
        new("Quartz", 740, 0.30, 1.0),
    ];

    private static readonly Mineral[] MaficMinerals =
    [
        new("Olivine", 1240, 0.15, 1.4),
        new("Pyroxene", 1200, 0.40, 1.9),
        new("Plagioclase", 1180, 0.45, 3.0),
    ];

    private readonly Random _random;
    private readonly int[] _cells = new int[Width * Height];
    private readonly List<Crystal> _crystals = [];
    private readonly double[] _carry;
    private readonly int[] _mineralCells;
    private readonly Queue<int> _queue = new();
    private int _bubbleCells;
    private int _glassCells;

    public CrystalSim(MagmaKind magma, double logRate, bool gas = false, bool twoStage = false, int seed = 7)
    {
        Magma = magma;
        LogRate = logRate;
        Gas = gas;
        TwoStage = twoStage;
        Minerals = MineralsOf(magma);

        // Each mineral's first crystal appears as soon as the melt is cool enough for it.
        _carry = Enumerable.Repeat(0.999, Minerals.Count).ToArray();
        _mineralCells = new int[Minerals.Count];
        _random = new Random(seed);
        if (gas) MakeBubbles();
    }

    public MagmaKind Magma { get; }

    public double LogRate { get; }

    public bool Gas { get; }

    public bool TwoStage { get; }

    public IReadOnlyList<Mineral> Minerals { get; }

    /// <summary>Steps done, 0 to <see cref="Steps"/>.</summary>
    public int Step { get; private set; }

    /// <summary>Changes whenever a cell changes, so a picture of the cells knows when to redraw.</summary>
    public int Version { get; private set; }

    public bool IsFinished => Step >= Steps;

    /// <summary>Each cell: <see cref="Melt"/>, <see cref="Glass"/>, <see cref="Bubble"/>, or a crystal's number from 1.</summary>
    public ReadOnlySpan<int> Cells => _cells;

    /// <summary>The mineral of crystal number <paramref name="crystal"/> (a cell value from 1).</summary>
    public int MineralOf(int crystal) => _crystals[crystal - 1].Mineral;

    /// <summary>The crystal's long axis, radians (for the shading of its cleavage lines).</summary>
    public double AngleOf(int crystal) => _crystals[crystal - 1].Angle;

    public static (double Liquidus, double Solidus) Range(MagmaKind magma) => magma == MagmaKind.Mafic ? (1250, 1000) : (900, 650);

    public static IReadOnlyList<Mineral> MineralsOf(MagmaKind magma) => magma == MagmaKind.Mafic ? MaficMinerals : FelsicMinerals;

    /// <summary>The temperature now, °C.</summary>
    public double Temperature => TemperatureAt(Step);

    private double TemperatureAt(int step)
    {
        var (liquidus, solidus) = Range(Magma);
        return liquidus - ((liquidus - solidus) * step / Steps);
    }

    /// <summary>The cooling rate at <paramref name="step"/> (a two-stage cooling is slow, then fast), log₁₀ °C a year.</summary>
    public double RateAt(int step) => !TwoStage ? LogRate : step < Steps * FirstStageShare ? SlowStage : FastStage;

    #region what came out

    /// <summary>Cells that are crystal.</summary>
    public int CrystalCells => _mineralCells.Sum();

    /// <summary>The share of the slice (bubbles left out) that is crystal, glass and still melt.</summary>
    public double CrystalShare => CrystalCells / (double)Math.Max(1, _cells.Length - _bubbleCells);

    public double GlassShare => _glassCells / (double)Math.Max(1, _cells.Length - _bubbleCells);

    public double BubbleShare => _bubbleCells / (double)_cells.Length;

    /// <summary>The share of the slice (bubbles left out) that is mineral <paramref name="index"/>.</summary>
    public double MineralShare(int index) => _mineralCells[index] / (double)Math.Max(1, _cells.Length - _bubbleCells);

    /// <summary>Crystals that have grown.</summary>
    public int CrystalCount => _crystals.Count;

    /// <summary>The average crystal's size, mm (the side of a square of its area).</summary>
    public double MeanSizeMm
    {
        get
        {
            if (_crystals.Count == 0) return 0;
            var cell = FieldMm / Width;
            return Math.Sqrt(CrystalCells / (double)_crystals.Count) * cell;
        }
    }

    /// <summary>The biggest crystal's size, mm.</summary>
    public double LargestSizeMm => _crystals.Count == 0 ? 0 : Math.Sqrt(_crystals.Max(c => c.Cells)) * FieldMm / Width;

    #endregion

    /// <summary>Runs on to <paramref name="step"/> (never back: make a new simulation for that).</summary>
    public void AdvanceTo(int step)
    {
        step = Math.Clamp(step, 0, Steps);
        while (Step < step) StepOnce();
    }

    private void StepOnce()
    {
        var before = Step;
        Step++;
        var temperature = TemperatureAt(Step);
        var (liquidus, solidus) = Range(Magma);
        var rate = RateAt(before);
        var crystalsTotal = CrystalsOverCooling(rate);
        var growth = GrowthPerStep(rate);
        var total = _cells.Length - _bubbleCells;

        for (var m = 0; m < Minerals.Count; m++)
        {
            var mineral = Minerals[m];
            if (temperature > mineral.StartC) continue;

            // Crystals of this mineral appear evenly over the steps it is active, as many as its share of the total.
            var activeSteps = Steps * (mineral.StartC - solidus) / (liquidus - solidus);
            _carry[m] += crystalsTotal * mineral.Share / Math.Max(1, activeSteps);
            while (_carry[m] >= 1)
            {
                _carry[m] -= 1;
                if (_mineralCells[m] < Allowed(m, temperature) * total) Nucleate(m);
            }
        }

        // Grow every crystal by its step, each mineral only up to its share so far.
        foreach (var crystal in _crystals)
        {
            var m = crystal.Mineral;
            if (temperature > Minerals[m].StartC) continue;
            crystal.Radius += growth;
            Grow(crystal, (int)(Allowed(m, temperature) * total));
        }

        if (Step == Steps)
        {
            // Cooled slowly, the last melt crystallises onto the crystals around it; only quick cooling leaves glass.
            if (RateAt(Steps - 1) < 2.5) FillLastMelt();
            for (var i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != Melt) continue;
                _cells[i] = Glass;
                _glassCells++;
            }
        }

        Version++;
    }

    /// <summary>Gives each melt cell left to a crystal next to it, spreading in from the crystals' edges until none is left.</summary>
    private void FillLastMelt()
    {
        var frontier = new Queue<int>();
        for (var i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] > 0) frontier.Enqueue(i);
        }

        while (frontier.Count > 0)
        {
            var cell = frontier.Dequeue();
            var id = _cells[cell];
            var x = cell % Width;
            var y = cell / Width;
            foreach (var next in new[] { x > 0 ? cell - 1 : -1, x < Width - 1 ? cell + 1 : -1, y > 0 ? cell - Width : -1, y < Height - 1 ? cell + Width : -1 })
            {
                if (next < 0 || _cells[next] != Melt) continue;
                var crystal = _crystals[id - 1];
                _cells[next] = id;
                crystal.Cells++;
                _mineralCells[crystal.Mineral]++;
                frontier.Enqueue(next);
            }
        }
    }

    /// <summary>How much of the slice mineral <paramref name="m"/> may fill by <paramref name="temperature"/> (all of its share at the solidus).</summary>
    private double Allowed(int m, double temperature)
    {
        var mineral = Minerals[m];
        var (_, solidus) = Range(Magma);
        var t = Math.Clamp((mineral.StartC - temperature) / (mineral.StartC - solidus), 0, 1);
        return mineral.Share * t;
    }

    /// <summary>The crystals a whole cooling at rate <paramref name="r"/> makes: more for faster cooling, fewer again past 10⁴·⁵ °C a year.</summary>
    internal static double CrystalsOverCooling(double r)
    {
        var rising = Math.Pow(10, 1.6 + (0.33 * (Math.Min(r, 4.5) + 3)));
        var falling = r > 4.5 ? Math.Pow(10, -0.9 * (r - 4.5)) : 1;
        return Math.Min(rising * falling, Width * Height * 0.6);
    }

    /// <summary>How far a crystal's edge moves in a step at rate <paramref name="r"/>, cells.</summary>
    internal static double GrowthPerStep(double r) => 0.2 * Math.Pow(10, -0.1 * (r + 3)) * (r > 4.5 ? Math.Pow(10, -0.4 * (r - 4.5)) : 1);

    private void Nucleate(int mineral)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var index = _random.Next(_cells.Length);
            if (_cells[index] != Melt) continue;
            var crystal = new Crystal(mineral, index % Width, index / Width, _random.NextDouble() * Math.PI, Minerals[mineral].Aspect)
            {
                Radius = 0.5,
                Id = _crystals.Count + 1,
            };
            _crystals.Add(crystal);
            Claim(index, crystal.Id, crystal);
            return;
        }
    }

    private void Claim(int index, int id, Crystal crystal)
    {
        _cells[index] = id;
        crystal.Cells++;
        _mineralCells[crystal.Mineral]++;
        var x = index % Width;
        var y = index / Width;
        if (x > 0) crystal.Frontier.Add(index - 1);
        if (x < Width - 1) crystal.Frontier.Add(index + 1);
        if (y > 0) crystal.Frontier.Add(index - Width);
        if (y < Height - 1) crystal.Frontier.Add(index + Width);
    }

    /// <summary>Claims the melt cells next to the crystal that lie inside its ellipse, spreading until none do or its mineral is full.</summary>
    private void Grow(Crystal crystal, int limit)
    {
        var id = crystal.Id;
        var pending = crystal.Frontier;
        crystal.Frontier = [];
        foreach (var cell in pending) _queue.Enqueue(cell);
        while (_queue.Count > 0)
        {
            var cell = _queue.Dequeue();
            if (_cells[cell] != Melt) continue;
            if (_mineralCells[crystal.Mineral] >= limit || !crystal.Reaches(cell % Width, cell / Width))
            {
                crystal.Frontier.Add(cell);
                continue;
            }

            var before = crystal.Frontier.Count;
            Claim(cell, id, crystal);

            // The cells just exposed are tried at once, so the crystal fills its whole ellipse this step.
            for (var i = crystal.Frontier.Count - 1; i >= before; i--)
            {
                _queue.Enqueue(crystal.Frontier[i]);
                crystal.Frontier.RemoveAt(i);
            }
        }

        // The same cell can sit in the list more than once; keep it small.
        if (crystal.Frontier.Count > 64) crystal.Frontier = crystal.Frontier.Distinct().ToList();
    }

    /// <summary>Gas trapped by quick cooling: round bubbles up to 65 % of the rock (pumice), none when cooling is slow enough for it to escape.</summary>
    private void MakeBubbles()
    {
        var share = (Magma == MagmaKind.Felsic ? 0.65 : 0.45) * VolcanoModel.SmoothStep(2.5, 6, TwoStage ? FastStage : LogRate);
        var goal = (int)(share * _cells.Length);
        for (var guard = 0; _bubbleCells < goal && guard < 4000; guard++)
        {
            var cx = _random.Next(Width);
            var cy = _random.Next(Height);
            var r = 2 + (_random.NextDouble() * 6);
            for (var y = (int)Math.Max(0, cy - r); y <= Math.Min(Height - 1, cy + r); y++)
            {
                for (var x = (int)Math.Max(0, cx - r); x <= Math.Min(Width - 1, cx + r); x++)
                {
                    var dx = x - cx;
                    var dy = (y - cy) * 1.15;
                    if ((dx * dx) + (dy * dy) > r * r) continue;
                    var index = (y * Width) + x;
                    if (_cells[index] == Bubble) continue;
                    _cells[index] = Bubble;
                    _bubbleCells++;
                }
            }
        }
    }

    /// <summary>The rock this cooling makes (granite, rhyolite, obsidian, pumice …), from the settings.</summary>
    public static string RockName(MagmaKind magma, double logRate, bool gas, bool twoStage)
    {
        var felsic = magma == MagmaKind.Felsic;
        if (twoStage) return felsic ? "Porphyry" : "PorphyriticBasalt";
        if (logRate < 0) return felsic ? "Granite" : "Gabbro";
        if (logRate < 2.5) return felsic ? "Microgranite" : "Dolerite";
        if (logRate < 5.2) return gas && logRate >= 3.5 ? (felsic ? "Pumice" : "Scoria") : felsic ? "Rhyolite" : "Basalt";
        if (gas) return felsic ? "Pumice" : "Scoria";
        return felsic ? "Obsidian" : "BasaltGlass";
    }

    /// <summary>Years the whole cooling takes (liquidus to solidus) at <paramref name="logRate"/>.</summary>
    public static double CoolingYears(MagmaKind magma, double logRate)
    {
        var (liquidus, solidus) = Range(magma);
        return (liquidus - solidus) / Math.Pow(10, logRate);
    }

    private sealed class Crystal(int mineral, int x, int y, double angle, double aspect)
    {
        private readonly double _cos = Math.Cos(angle);
        private readonly double _sin = Math.Sin(angle);

        public int Mineral { get; } = mineral;

        public double Angle { get; } = angle;

        /// <summary>Its number in the cells, from 1.</summary>
        public int Id { get; init; }

        public double Radius { get; set; }

        public int Cells { get; set; }

        public List<int> Frontier { get; set; } = [];

        /// <summary>Whether cell (cx, cy) is inside the ellipse of minor radius <see cref="Radius"/> and major radius aspect × it.</summary>
        public bool Reaches(int cx, int cy)
        {
            double dx = cx - x, dy = cy - y;
            var along = ((dx * _cos) + (dy * _sin)) / aspect;
            var across = (-dx * _sin) + (dy * _cos);
            return (along * along) + (across * across) <= (Radius + 0.5) * (Radius + 0.5);
        }
    }
}
