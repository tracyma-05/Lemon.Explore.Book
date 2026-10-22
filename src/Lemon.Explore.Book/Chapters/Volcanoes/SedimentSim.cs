namespace Lemon.Explore.Book.Chapters.Volcanoes;

/// <summary>
/// A tank of still water where a river pours in mud: gravel, sand, silt and clay together. Each grain drifts out with the
/// river's current and sinks at its own speed, coarse grains first and near the mouth, clay last and far out, and lands on
/// the bed, rolling off steep spots. Pour after pour the bed builds up in layers, each graded from coarse at its bottom to
/// fine at its top. The bed is a grid of cells holding the size of the grain that settled there.
/// </summary>
/// <remarks>
/// Sinking times are real (<see cref="VolcanoModel.SecondsPerMetre"/>: about 2.5 s a metre for gravel, half a minute for
/// sand, 20 minutes for silt, three days for clay) but squeezed for the screen: a grain takes
/// 1.2 s·(real time / gravel's)^0.27 to sink through the tank, so clay takes about 25 s instead of days.
/// </remarks>
public sealed class SedimentSim
{
    public const int Width = 240;

    /// <summary>Height of the tank (water and bed), cells.</summary>
    public const int Height = 150;

    /// <summary>Grain size classes in a cell: 0 is water.</summary>
    public const byte Water = 0;

    public const byte Gravel = 1;
    public const byte Sand = 2;
    public const byte Silt = 3;
    public const byte Clay = 4;

    /// <summary>The bed stops growing when it reaches this height, cells (the tank is full).</summary>
    public const int MaxBed = 118;

    /// <summary>Grains of each class in one pour (gravel, sand, silt, clay), and the cells each one fills.</summary>
    private static readonly int[] PerPour = [26, 420, 560, 900];

    private static readonly int[] CellsPerGrain = [9, 1, 1, 1];

    private readonly byte[] _bed = new byte[Width * Height];
    private readonly byte[] _layer = new byte[Width * Height];
    private readonly int[] _heights = new int[Width];
    private readonly List<Grain> _falling = [];
    private readonly int[] _settled = new int[5];
    private readonly Random _random;
    private double _time;

    public SedimentSim(int seed = 11)
    {
        _random = new Random(seed);
    }

    /// <summary>Cells of the bed, row by row from the top: <see cref="Water"/> or a grain class.</summary>
    public ReadOnlySpan<byte> Bed => _bed;

    /// <summary>The pour each bed cell's grain came in with (1, 2, …; 0 for water), for telling the layers apart.</summary>
    public ReadOnlySpan<byte> Layers => _layer;

    /// <summary>The grains still sinking.</summary>
    public IReadOnlyList<Grain> Falling => _falling;

    /// <summary>The bed's height in each column, cells.</summary>
    public ReadOnlySpan<int> Heights => _heights;

    /// <summary>Pours so far.</summary>
    public int Pours { get; private set; }

    /// <summary>Changes whenever the bed changes.</summary>
    public int Version { get; private set; }

    /// <summary>Whether the tank cannot take another pour.</summary>
    public bool IsFull => _heights.Max() >= MaxBed - 2;

    /// <summary>Grains of a class that have reached the bed.</summary>
    public int Settled(byte size) => _settled[size];

    /// <summary>Grains of a class still sinking.</summary>
    public int SinkingCount(byte size) => _falling.Count(g => g.Size == size);

    /// <summary>The seconds on the screen a grain of a class takes to sink through the whole tank.</summary>
    public static double ScreenFallSeconds(byte size)
    {
        var gravel = VolcanoModel.SecondsPerMetre(VolcanoModel.GrainSizes[0].Diameter);
        var real = VolcanoModel.SecondsPerMetre(VolcanoModel.GrainSizes[size - 1].Diameter);
        return 1.2 * Math.Pow(real / gravel, 0.27);
    }

    /// <summary>A muddy river pours in a load of mixed grains at the left, near the surface.</summary>
    public void Pour()
    {
        if (IsFull) return;
        Pours++;
        for (var size = Gravel; size <= Clay; size++)
        {
            var count = PerPour[size - 1];
            for (var i = 0; i < count; i++)
            {
                // A pour lasts about two seconds: grains enter one after another, a little apart.
                _falling.Add(new Grain
                {
                    Size = size,
                    X = 2 + (_random.NextDouble() * 10),
                    Y = 2 + (_random.NextDouble() * 8),
                    Delay = _random.NextDouble() * 2.2,
                    Target = LandingTarget(size),
                    Layer = (byte)Math.Min(255, Pours),
                });
            }
        }
    }

    /// <summary>Empties the tank.</summary>
    public void Clear()
    {
        Array.Clear(_bed);
        Array.Clear(_layer);
        Array.Clear(_heights);
        Array.Clear(_settled);
        _falling.Clear();
        Pours = 0;
        Version++;
    }

    /// <summary>Moves every sinking grain on by <paramref name="seconds"/> of screen time and settles those that reach the bed.</summary>
    public void Step(double seconds)
    {
        _time += seconds;
        var changed = false;
        for (var i = _falling.Count - 1; i >= 0; i--)
        {
            var grain = _falling[i];
            if (grain.Delay > 0)
            {
                grain.Delay -= seconds;
                continue;
            }

            grain.Age += seconds;

            // Carried out towards where it will land, quickly at first: coarse grains drop near the mouth, fine ones spread far.
            var fall = ScreenFallSeconds(grain.Size);
            var sink = Height / fall;
            var pull = 1 - Math.Exp(-seconds / (fall * 0.35));
            var wobble = Math.Sin((grain.Age * 2.3) + (grain.Target * 0.7)) * (grain.Size >= Silt ? 3 : 1);
            grain.X = Math.Clamp(grain.X + ((grain.Target - grain.X) * pull) + (wobble * seconds), 0, Width - 1);
            grain.Y += sink * seconds;

            var column = (int)grain.X;
            if (grain.Y < Height - _heights[column] - 1) continue;
            Settle(grain, column);
            _falling.RemoveAt(i);
            changed = true;
        }

        if (changed) Version++;
    }

    /// <summary>Where a grain of this size will come down: gravel near the mouth, sand a little further, silt and clay all over.</summary>
    private double LandingTarget(byte size)
    {
        var u = _random.NextDouble();
        return size switch
        {
            Gravel => 6 + (Math.Pow(u, 1.3) * 60),
            Sand => 10 + (Math.Pow(u, 0.9) * 150),
            Silt => 20 + (u * 215),
            _ => 2 + (u * 236),
        };
    }

    /// <summary>Lays a grain on the bed at <paramref name="column"/>, rolling it downhill while a neighbour is lower by more than one cell.</summary>
    private void Settle(Grain grain, int column)
    {
        var cells = CellsPerGrain[grain.Size - 1];
        var spread = grain.Size == Gravel ? 3 : 1;

        // Mud slides flat; sand and gravel keep a slope.
        var steep = grain.Size >= Silt ? 0 : 1;
        for (var k = 0; k < cells; k++)
        {
            var c = Math.Clamp(column + (k % spread) - (spread / 2), 0, Width - 1);
            for (var roll = 0; roll < 30; roll++)
            {
                var left = c > 0 ? _heights[c - 1] : int.MaxValue;
                var right = c < Width - 1 ? _heights[c + 1] : int.MaxValue;
                var lower = left < right ? c - 1 : c + 1;
                var lowest = Math.Min(left, right);
                if (lowest >= _heights[c] - steep) break;
                c = lower;
            }

            if (_heights[c] >= MaxBed) continue;
            var y = Height - 1 - _heights[c];
            _bed[(y * Width) + c] = grain.Size;
            _layer[(y * Width) + c] = grain.Layer;
            _heights[c]++;
        }

        _settled[grain.Size]++;
    }

    /// <summary>A grain on its way down: its class, where it is (cells), and how long it has been in the water.</summary>
    public sealed class Grain
    {
        public byte Size { get; init; }

        public double X { get; set; }

        public double Y { get; set; }

        /// <summary>Seconds before it enters the tank.</summary>
        public double Delay { get; set; }

        /// <summary>Seconds it has been sinking.</summary>
        public double Age { get; set; }

        /// <summary>Where (cells from the left) the river carries this grain before it settles.</summary>
        public double Target { get; init; }

        public byte Layer { get; init; }
    }
}
