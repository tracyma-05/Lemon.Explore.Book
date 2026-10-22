using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Lightning;

/// <summary>
/// Static mode: a bench a metre wide, seen from the front. A rod (or a balloon) is rubbed on a cloth and electrons hop
/// across; then it is held over bits of paper (they jump up, cling, and are flung off once they have taken some of its
/// charge), beside a thin stream of water (it bends toward the rod, whatever the sign) and up to an earthed metal ball
/// (it sparks when the gap is short enough for its voltage). The charge leaks away faster the damper the air.
/// </summary>
public sealed partial class LightningDiagram
{
    // The bench in metres, y up from the table top.
    private const double BenchWidth = 1.0;
    private const double BenchHeight = 0.5;
    private const double ClothLeft = 0.04;
    private const double ClothRight = 0.30;
    private const double ClothTop = 0.02;
    private const double PaperLeft = 0.39;
    private const double PaperRight = 0.53;
    private const double TapX = 0.665;
    private const double TapY = 0.42;
    private const double BeakerLeft = 0.625;
    private const double BeakerRight = 0.705;
    private const double BeakerTop = 0.085;
    private const double WaterLevel = 0.05;
    private const double BallX = 0.885;
    private const double BallY = 0.21;
    private const double BallRadius = 0.035;
    private const double RodLength = 0.32;
    private const double RodRadius = 0.011;
    private const double ChargedLength = 0.13;
    private const double BalloonX = 0.072;
    private const double BalloonY = 0.088;

    /// <summary>The balloon is rubbed on its bottom: its charge sits within this angle of straight down, radians.</summary>
    private const double BalloonPatch = 1.2;

    /// <summary>The demonstrating hand's round of the bench, playback seconds.</summary>
    private const double ScriptLength = 20;

    /// <summary>Rubbing moves electrons in packets of this much charge, C, each drawn as one dot hopping across.</summary>
    private const double ElectronPacket = 4e-9;

    private Point _tip = new(0.17, 0.16);
    private double _charge;
    private double _clothCharge;
    private double _staticTime;
    private double _scriptTime;
    private double _lastRub = -10;
    private double _eventTime = -10;
    private StaticState _event;
    private double _electronDebt;
    private Vector _grabOffset;
    private List<(double X, double Y)> _stream = [];
    private (Point From, Point To, double Time, double Volts)? _spark;
    private readonly List<PaperBit> _bits = [];
    private readonly List<Electron> _electrons = [];

    private enum BitMode
    {
        Rest,
        Rising,
        Stuck,
        Falling,
    }

    private sealed class PaperBit
    {
        public Point At;
        public Vector Velocity;
        public Vector Offset;
        public BitMode Mode;
        public double Timer;
        public double Angle;
        public double Spin;
        public double Size;
    }

    private sealed class Electron
    {
        public Point From;
        public Point To;
        public double Age;
        public double Bend;
    }

    private bool IsBalloon => Rod == Material.Balloon;

    private void OnAutoHandChanged(bool value)
    {
        // Handing the rod back to the demonstrator starts its round from the top (or the part it is showing); taking the
        // rod away ends showing one part.
        if (value) _scriptTime = SceneWindow(HandScene).Start;
        else SetCurrentValue(HandSceneProperty, HandScene.Round);
    }

    private void OnHandSceneChanged(HandScene scene) => _scriptTime = SceneWindow(scene).Start;

    /// <summary>The stretch of the hand's round (playback seconds) a scene repeats.</summary>
    private static (double Start, double End) SceneWindow(HandScene scene) => scene switch
    {
        HandScene.Rub => (0.8, 5.6),
        HandScene.Paper => (6.8, 10.0),
        HandScene.Stream => (11.2, 13.8),
        HandScene.Ball => (14.6, 18.6),
        _ => (0, ScriptLength),
    };

    private void OnChargeChanged(double nanoCoulombs)
    {
        if (_pushing) return;
        _charge = nanoCoulombs * 1e-9;
        if (Math.Abs(nanoCoulombs) < 1e-6) _clothCharge = 0;
    }

    #region the rod's shape

    /// <summary>The rod leans over, its handle up and to the left: flatter near the table, so it can be rubbed along the cloth.</summary>
    private Vector RodAxis()
    {
        var angle = Mix(1.2, 0.55, Smooth(0.015, 0.12, _tip.Y));
        return new Vector(-Math.Sin(angle), Math.Cos(angle));
    }

    private Point BalloonCentre => new(_tip.X, _tip.Y + BalloonY);

    /// <summary>A point of the balloon's surface at an angle (radians, 0 to the right, −π/2 straight down).</summary>
    private Point BalloonPoint(double angle) => BalloonCentre + new Vector(Math.Cos(angle) * BalloonX, Math.Sin(angle) * BalloonY);

    /// <summary>The point of the rod's charged part (its surface) nearest to <paramref name="p"/>.</summary>
    private Point NearestCharged(Point p)
    {
        if (IsBalloon)
        {
            // Near enough: the angle in the ellipse's own (circular) frame, kept within the rubbed patch.
            var v = p - BalloonCentre;
            var angle = Math.Atan2(v.Y / BalloonY, v.X / BalloonX);
            return BalloonPoint(Math.Clamp(angle, (-Math.PI / 2) - BalloonPatch, (-Math.PI / 2) + BalloonPatch));
        }

        var axis = RodAxis();
        var along = Math.Clamp(Vector.Dot(p - _tip, axis), 0, ChargedLength);
        var onAxis = _tip + (axis * along);
        Vector out_ = p - onAxis;
        return out_.Length < 1e-9 ? onAxis : onAxis + (out_ / out_.Length * RodRadius);
    }

    /// <summary>The lowest the tip may go at x: the cloth, the table, the beaker's rim.</summary>
    private static double Floor(double x)
    {
        if (x >= BeakerLeft - 0.012 && x <= BeakerRight + 0.012) return BeakerTop + 0.004;
        if (x >= ClothLeft && x <= ClothRight) return ClothTop;
        return 0.003;
    }

    /// <summary>Keeps the rod on the bench: above the table and the cloth, out of the metal ball.</summary>
    private void ClampTip()
    {
        var x = Math.Clamp(_tip.X, 0.03, BenchWidth - 0.03);
        var y = Math.Clamp(_tip.Y, Floor(x), BenchHeight - 0.03);
        _tip = new Point(x, y);

        var ball = new Point(BallX, BallY);
        var near = IsBalloon ? BalloonPoint(Math.Atan2((BallY - BalloonCentre.Y) / BalloonY, (BallX - BalloonCentre.X) / BalloonX)) : NearestCharged(ball);
        var gap = ((Vector)(near - ball)).Length;
        if (gap < BallRadius + 0.0006 && gap > 1e-9)
        {
            var push = (near - ball) / gap * (BallRadius + 0.0006 - gap);
            _tip += push;
        }
    }

    #endregion

    #region the demonstrating hand

    /// <summary>How a key of the hand's round places the rod.</summary>
    private enum Aim
    {
        /// <summary>The tip (the balloon's bottom) at the point.</summary>
        Tip,

        /// <summary>The charged part's right side at the point.</summary>
        Side,

        /// <summary>The charged part this far (x, m) from the ball's surface, raised by y.</summary>
        Ball,
    }

    /// <summary>
    /// Where the hand wants the rod at a time of its round: rub along the cloth, hold it over the paper, beside the
    /// stream, then up to the ball until it sparks, and back.
    /// </summary>
    private Point ScriptTip(double t)
    {
        var (start, end) = SceneWindow(HandScene);
        t = start + (Frac((t - start) / (end - start)) * (end - start));
        if (t >= 0.8 && t < 5.6) return new Point(0.17 - (0.09 * Math.Cos(2 * Math.PI * (t - 0.8) / 1.2)), ClothTop);

        (double T, double X, double Y, Aim Aim)[] keys =
        [
            (0, 0.17, 0.16, Aim.Tip),
            (0.8, 0.08, ClothTop, Aim.Tip),
            (5.6, 0.08, ClothTop, Aim.Tip),
            (6.8, 0.46, 0.09, Aim.Tip),
            (8.3, 0.46, 0.045, Aim.Tip),
            (10.0, 0.47, 0.05, Aim.Tip),
            (11.2, 0.635, 0.22, Aim.Side),
            (13.8, 0.64, 0.21, Aim.Side),
            (15.0, 0.05, 0, Aim.Ball),
            (17.0, 0.0004, 0, Aim.Ball),
            (18.6, 0.06, 0.05, Aim.Ball),
            (ScriptLength, 0.17, 0.16, Aim.Tip),
        ];

        var i = 0;
        while (i < keys.Length - 2 && t >= keys[i + 1].T) i++;
        var a = keys[i];
        var b = keys[i + 1];
        var f = Smooth(0, 1, (t - a.T) / (b.T - a.T));
        return Mix(Placed(a.X, a.Y, a.Aim), Placed(b.X, b.Y, b.Aim), f);
    }

    /// <summary>The tip that puts the rod where a key wants it.</summary>
    private Point Placed(double x, double y, Aim aim)
    {
        // Where the balloon's patch faces sideways (to the stream) and down and right (to the ball), from its tip.
        var side = new Vector(Math.Cos(-0.35) * BalloonX, BalloonY + (Math.Sin(-0.35) * BalloonY));
        var corner = new Vector(Math.Cos(-0.8) * BalloonX, BalloonY + (Math.Sin(-0.8) * BalloonY));
        switch (aim)
        {
            case Aim.Side:
                return IsBalloon ? new Point(x, y) - side : new Point(x - RodRadius, y);

            case Aim.Ball:
                if (!IsBalloon) return new Point(BallX - BallRadius - RodRadius - x, BallY - 0.004 + y);

                // The balloon comes down onto the ball's upper left with the bottom of its patch.
                var at = new Point(BallX, BallY) + (new Vector(-Math.Cos(0.8), Math.Sin(0.8)) * (BallRadius + x)) + new Vector(0, y);
                return at - corner;

            default:
                return new Point(x, y);
        }
    }

    #endregion

    #region mouse

    private void GrabRod(Point screen)
    {
        var view = BenchLayout();
        var world = view.World(screen);
        var near = NearestCharged(world);
        var handle = IsBalloon ? BalloonCentre : _tip + (RodAxis() * RodLength * 0.5);

        // Grabbed on (or close to) the rod it moves with the pointer as it is; elsewhere its tip jumps to the pointer.
        _grabOffset = ((Vector)(world - near)).Length < 0.06 || ((Vector)(world - handle)).Length < 0.12 ? _tip - world : new Vector();
        SetCurrentValue(AutoHandProperty, false);
        DragRod(screen);
    }

    private void DragRod(Point screen)
    {
        var world = BenchLayout().World(screen);
        _tip = world + _grabOffset;
        ClampTip();
    }

    #endregion

    #region the simulation

    private void StepStatic(double step)
    {
        if (_bits.Count == 0) SeedBits();
        if (step <= 0) return;
        _staticTime += step;
        var old = _tip;

        if (AutoHand && (IsPlaying || IsLessonActive) && !_dragging)
        {
            _scriptTime += step;
            var want = ScriptTip(_scriptTime);
            _tip += (want - _tip) * (1 - Math.Exp(-14 * step));
            ClampTip();
        }

        Rub(old);
        _charge = StaticModel.Leak(_charge, step, Humidity);
        _clothCharge = StaticModel.Leak(_clothCharge, step * 3, Humidity);
        StepElectrons(step);
        StepBits(step);
        CheckSpark();

        var near = NearestCharged(new Point(TapX, Math.Clamp(_tip.Y, WaterLevel, TapY)));
        _stream = StaticModel.Stream(TapX, TapY, WaterLevel, _charge, near.X, near.Y);

        Push(ChargeProperty, Math.Round(_charge * 1e9, 1));
        Push(StaticTimeProperty, Math.Round(_staticTime, 2));
        Push(StateProperty, CurrentState());
    }

    /// <summary>Rubbing: the rod's charged part sliding along the cloth moves electrons across.</summary>
    private void Rub(Point old)
    {
        bool OnCloth(Point p) => p.Y <= ClothTop + 0.004 && p.X >= ClothLeft && p.X <= ClothRight;
        if (!OnCloth(_tip) || !OnCloth(old)) return;
        var slide = Math.Abs(_tip.X - old.X);
        if (slide < 1e-6) return;

        var before = _charge;
        _charge = StaticModel.Rub(_charge, slide, Rod, Cloth);
        var moved = _charge - before;
        _clothCharge -= moved;
        _lastRub = _staticTime;

        // Electrons go to whichever gets more negative.
        _electronDebt += Math.Abs(moved);
        while (_electronDebt > ElectronPacket && _electrons.Count < 40)
        {
            _electronDebt -= ElectronPacket;
            var x = Math.Clamp(_tip.X + ((Hash(_electrons.Count + (int)(_staticTime * 37), 2.3) - 0.5) * 0.05), ClothLeft, ClothRight);
            var onCloth = new Point(x, ClothTop);
            var onRod = NearestCharged(new Point(x, ClothTop - 0.05));
            var toRod = moved < 0;
            _electrons.Add(new Electron
            {
                From = toRod ? onCloth : onRod,
                To = toRod ? onRod : onCloth,
                Bend = (Hash(_electrons.Count, 5.5) - 0.5) * 0.03,
            });
        }
    }

    private void StepElectrons(double step)
    {
        foreach (var e in _electrons) e.Age += step;
        _electrons.RemoveAll(e => e.Age > 0.5);
    }

    private void SeedBits()
    {
        for (var i = 0; i < 14; i++)
        {
            _bits.Add(new PaperBit
            {
                At = new Point(PaperLeft + 0.01 + ((i + (0.8 * Hash(i, 1.3))) / 14 * (PaperRight - PaperLeft - 0.02)), 0),
                Angle = (Hash(i, 2.7) - 0.5) * 60,
                Size = 0.009 + (Hash(i, 3.1) * 0.005),
            });
        }
    }

    /// <summary>
    /// Bits of paper: pulled up when the rod comes within reach (they are polarised: their near side takes the opposite
    /// charge), cling for a moment, then share its charge and are flung off; they drift down like paper.
    /// </summary>
    private void StepBits(double step)
    {
        var reach = Math.Abs(_charge) > 2e-9 ? StaticModel.LiftReach(_charge) : 0;
        var n = 0;
        foreach (var bit in _bits)
        {
            n++;
            var near = NearestCharged(bit.At);
            var gap = ((Vector)(near - bit.At)).Length;
            switch (bit.Mode)
            {
                case BitMode.Rest:
                    bit.Timer -= step;
                    if (bit.Timer <= 0 && gap < reach * (0.85 + (0.3 * Hash(n, 4.4))))
                    {
                        bit.Mode = BitMode.Rising;
                        bit.Spin = (Hash(n, 6.1) - 0.5) * 900;
                    }

                    break;

                case BitMode.Rising:
                    if (gap > reach * 1.3)
                    {
                        bit.Mode = BitMode.Falling;
                        bit.Velocity = new Vector();
                        break;
                    }

                    var dir = near - bit.At;
                    var move = Math.Min(gap, 0.9 * step);
                    bit.At += dir / Math.Max(1e-9, gap) * move;
                    bit.Angle += bit.Spin * step;
                    if (gap < 0.004)
                    {
                        bit.Mode = BitMode.Stuck;
                        bit.Offset = bit.At - _tip;
                        bit.Timer = 0.7 + (1.4 * Hash(n + (int)_staticTime, 7.9));
                    }

                    break;

                case BitMode.Stuck:
                    bit.At = _tip + bit.Offset;
                    bit.Timer -= step;
                    if (bit.Timer <= 0 || Math.Abs(_charge) < 2e-9)
                    {
                        // It has taken some of the rod's charge: like charges push it away.
                        Vector away = bit.At - (IsBalloon ? BalloonCentre : _tip + (RodAxis() * ChargedLength * 0.5));
                        away = away.Length > 1e-9 ? away / away.Length : new Vector(0, -1);
                        bit.Velocity = away * 0.35;
                        bit.Mode = BitMode.Falling;
                    }

                    break;

                default:
                    // Paper falls slowly: air drag keeps it under about 0.7 m/s.
                    bit.Velocity = new Vector(bit.Velocity.X * Math.Exp(-2 * step), Math.Max(-0.7, bit.Velocity.Y - (6 * step)));
                    bit.At += bit.Velocity * step;
                    bit.Angle += bit.Spin * step * 0.5;
                    var floor = bit.At.X >= ClothLeft && bit.At.X <= ClothRight ? ClothTop : 0;
                    if (bit.At.Y <= floor)
                    {
                        bit.At = new Point(Math.Clamp(bit.At.X, 0.02, BenchWidth - 0.02), floor);
                        bit.Mode = BitMode.Rest;
                        bit.Timer = 1.5;
                    }

                    break;
            }
        }
    }

    /// <summary>A spark (or a quiet touch) when the gap to the ball is short enough for the rod's voltage.</summary>
    private void CheckSpark()
    {
        if (_staticTime - _eventTime < 0.4 || Math.Abs(_charge) < 1e-9) return;
        var ball = new Point(BallX, BallY);
        var near = NearestCharged(ball);
        var gap = ((Vector)(near - ball)).Length - BallRadius;
        var volts = StaticModel.Voltage(_charge);
        if (gap > Math.Max(0.0009, StaticModel.SparkGap(volts))) return;

        var surface = ball + ((near - ball) / ((Vector)(near - ball)).Length * BallRadius);
        _event = Math.Abs(volts) >= 3000 ? StaticState.Spark : StaticState.Touched;
        if (_event == StaticState.Spark) _spark = (near, surface, _staticTime, volts);
        _eventTime = _staticTime;
        _charge *= StaticModel.KeptAfterSpark;
    }

    private StaticState CurrentState()
    {
        if (_staticTime - _eventTime < 1.5) return _event;
        if (_staticTime - _lastRub < 0.3) return StaticState.Rubbing;
        if (_bits.Any(b => b.Mode is BitMode.Rising or BitMode.Stuck)) return StaticState.Lifting;
        if (_stream.Count > 1 && Math.Abs(_stream[^1].X - TapX) > 0.004) return StaticState.Bending;
        return Math.Abs(_charge) > 5e-9 ? StaticState.Charged : StaticState.Neutral;
    }

    #endregion

    #region drawing

    private readonly record struct BenchView(double X0, double Y0, double Scale)
    {
        public Point P(double x, double y) => new(X0 + (x * Scale), Y0 - (y * Scale));

        public Point P(Point world) => P(world.X, world.Y);

        public Point World(Point screen) => new((screen.X - X0) / Scale, (Y0 - screen.Y) / Scale);
    }

    private BenchView BenchLayout()
    {
        var stage = Stage();
        var room = new Rect(stage.Left + 30, stage.Top + 4, Math.Max(100, stage.Width - 60), Math.Max(100, stage.Height - 60));
        var scale = Math.Min(room.Width / BenchWidth, room.Height / BenchHeight);

        // The table sits at the bottom, just above its labels; in a tall window the room above it grows.
        return new BenchView(room.Left + ((room.Width - (scale * BenchWidth)) / 2), room.Bottom, scale);
    }

    private void DrawStaticMode(PaintContext dc, double width, double height)
    {
        var view = BenchLayout();
        dc.DrawRectangle(StaticInk.Wall, null, new Rect(0, 0, width, height));

        // The table, its top at y = 0.
        var left = view.P(-0.02, 0).X;
        var right = view.P(BenchWidth + 0.02, 0).X;
        dc.DrawRectangle(StaticInk.TableTop, null, new Rect(left, view.Y0, right - left, 10));
        dc.DrawRectangle(StaticInk.TableFront, null, new Rect(left, view.Y0 + 10, right - left, 18));

        DrawCloth(dc, view);
        DrawBeakerAndStream(dc, view);
        DrawBall(dc, view);
        DrawBits(dc, view);
        DrawRod(dc, view);
        DrawElectrons(dc, view);
        DrawSpark(dc, view);
        if (ShowLabels) DrawStaticLabels(dc, view);
    }

    /// <summary>The cloth (or hair, or fur) on the table, with its charge.</summary>
    private void DrawCloth(PaintContext dc, BenchView view)
    {
        var a = view.P(ClothLeft, ClothTop);
        var b = view.P(ClothRight, 0);
        var rect = new Rect(a, b);
        var (fill, strand) = Cloth switch
        {
            Material.Hair => (Color.FromRgb(0x3A, 0x26, 0x1A), Color.FromRgb(0x6A, 0x48, 0x30)),
            Material.Fur => (Color.FromRgb(0x8A, 0x6A, 0x48), Color.FromRgb(0xC8, 0xA8, 0x80)),
            Material.Wool => (Color.FromRgb(0xD8, 0xCC, 0xB0), Color.FromRgb(0xA8, 0x98, 0x78)),
            Material.Silk => (Color.FromRgb(0xE8, 0xC8, 0xD8), Color.FromRgb(0xFF, 0xF0, 0xF8)),
            _ => (Color.FromRgb(0xE8, 0xEA, 0xEE), Color.FromRgb(0xB8, 0xBC, 0xC8)),
        };
        dc.DrawRoundedRectangle(FadedBrush(fill, 1), null, rect, 4, 4);
        _targets.AddBox("cloth", new Rect(rect.Left, rect.Top - 12, rect.Width, rect.Height + 12));

        // Its texture: strands for hair and fur, a knit for wool, a sheen for silk, a weave for cotton.
        var n = (int)(rect.Width / 5);
        for (var i = 0; i < n; i++)
        {
            var x = rect.Left + 3 + (i * (rect.Width - 6) / n);
            switch (Cloth)
            {
                case Material.Hair:
                case Material.Fur:
                    var length = (Cloth == Material.Hair ? 9 : 6) * (0.7 + (0.6 * Hash(i, 8.1)));
                    var lean = (Hash(i, 9.2) - 0.5) * 8;
                    _batch.Add(strand, 0.9, [new Point(x, rect.Top + 4), new Point(x + lean, rect.Top - length + 4)]);
                    break;

                case Material.Wool:
                    _batch.Add(strand, 0.8, [new Point(x, rect.Top + 3), new Point(x + 2.5, rect.Bottom - 3), new Point(x + 5, rect.Top + 3)]);
                    break;

                case Material.Cotton:
                    _batch.Add(strand, 0.6, [new Point(x, rect.Top + 2), new Point(x, rect.Bottom - 2)]);
                    break;
            }
        }

        if (Cloth == Material.Silk) dc.DrawRoundedRectangle(StaticInk.Sheen, null, new Rect(rect.Left, rect.Top, rect.Width, rect.Height * 0.5), 4, 4);
        _batch.Draw(dc, this, 1.2);

        // Its charge, opposite to what the rod took, along the top.
        var signs = SignCount(_clothCharge);
        for (var i = 0; i < signs; i++)
        {
            var x = rect.Left + ((i + 0.5) / signs * rect.Width);
            DrawSign(dc, new Point(x, rect.Top - 9 - (3 * (i % 2))), _clothCharge > 0, 8);
        }
    }

    /// <summary>How many signs show a charge: up to 16 for the most a rod can hold, at least one when there is any.</summary>
    private static int SignCount(double charge)
    {
        var n = (int)Math.Round(16 * Math.Abs(charge) / StaticModel.MaxCharge);
        return n == 0 && Math.Abs(charge) > 3e-9 ? 1 : Math.Min(18, n);
    }

    private void DrawBeakerAndStream(PaintContext dc, BenchView view)
    {
        // The tap: a pipe coming in from the right and a spout.
        var spout = view.P(TapX, TapY);
        var pipe = view.P(TapX + 0.11, TapY + 0.035);
        var thick = Math.Max(5, 0.018 * view.Scale);
        dc.DrawLine(StaticInk.Pipe(thick), pipe, new Point(spout.X, pipe.Y));
        dc.DrawLine(StaticInk.Pipe(thick), new Point(spout.X, pipe.Y), new Point(spout.X, spout.Y - 2));
        dc.DrawRectangle(StaticInk.Valve, null, new Rect(spout.X + 10, pipe.Y - (thick * 0.9), thick * 0.8, thick * 0.6));

        // The water: a thin stream, a few millimetres wide.
        if (_stream.Count > 1)
        {
            var points = _stream.Select(p => view.P(p.X, p.Y)).ToList();
            dc.DrawGeometry(null, FadedPen(StaticInk.Water, 0.35, Math.Max(4, 0.007 * view.Scale)), Line(points));
            dc.DrawGeometry(null, FadedPen(StaticInk.WaterBright, 0.9, Math.Max(2, 0.0035 * view.Scale)), Line(points));
            var end = points[^1];
            for (var k = 0; k < 2; k++)
            {
                var r = (Frac((_t * 2.2) + (k * 0.5)) * 14) + 2;
                dc.DrawEllipse(null, FadedPen(StaticInk.WaterBright, 0.6 * (1 - (r / 16)), 1), end, r, r * 0.3);
            }
        }

        // The beaker it falls into.
        var bl = view.P(BeakerLeft, BeakerTop);
        var br = view.P(BeakerRight, 0);
        var water = new Rect(view.P(BeakerLeft, WaterLevel), br);
        dc.DrawRectangle(StaticInk.BeakerWater, null, water);
        dc.DrawGeometry(StaticInk.Glass, StaticInk.GlassEdge, Shape(bl, new Point(bl.X, br.Y), br, new Point(br.X, bl.Y), new Point(br.X - 1, bl.Y), new Point(br.X - 1, br.Y - 1), new Point(bl.X + 1, br.Y - 1), new Point(bl.X + 1, bl.Y)));
        _targets.AddBox("water", new Rect(bl, view.P(BeakerRight, 0)));
        if (_stream.Count > 1)
        {
            // The stream, a few points down it.
            for (var i = 0; i < _stream.Count; i += Math.Max(1, _stream.Count / 6)) _targets.Add("water", view.P(_stream[i].X, _stream[i].Y), 7);
        }
    }

    private void DrawBall(PaintContext dc, BenchView view)
    {
        var centre = view.P(BallX, BallY);
        var r = BallRadius * view.Scale;

        // The stand (an insulator), its base, and the wire to earth.
        var foot = view.P(BallX, 0);
        dc.DrawRectangle(StaticInk.Stand, null, new Rect(centre.X - (r * 0.18), centre.Y + (r * 0.8), r * 0.36, foot.Y - centre.Y - (r * 0.8)));
        dc.DrawRoundedRectangle(StaticInk.Base, null, new Rect(foot.X - (r * 1.1), foot.Y - 6, r * 2.2, 6), 2, 2);
        var earth = view.P(BallX + 0.07, 0);
        dc.DrawGeometry(null, StaticInk.Wire, Line([new Point(centre.X + (r * 0.7), centre.Y + (r * 0.7)), new Point(earth.X, centre.Y + (r * 1.6)), new Point(earth.X, earth.Y - 12)]));
        for (var k = 0; k < 3; k++)
        {
            var w = 9 - (k * 3);
            dc.DrawLine(StaticInk.Wire, new Point(earth.X - w, earth.Y - 12 + (k * 3.5)), new Point(earth.X + w, earth.Y - 12 + (k * 3.5)));
        }

        _targets.Add("earth", new Point(earth.X, earth.Y - 9), 9);
        dc.DrawEllipse(StaticInk.Metal, StaticInk.MetalEdge, centre, r, r);
        _targets.Add("ball", centre, r);

        // The rod pulls the opposite charge up from the earth to the side facing it.
        var near = NearestCharged(new Point(BallX, BallY));
        var gap = Math.Max(0, ((Vector)(near - new Point(BallX, BallY))).Length - BallRadius);
        var count = (int)Math.Round(Math.Min(10, 10 * Math.Abs(_charge) / StaticModel.MaxCharge * (0.06 / (gap + 0.03))));
        if (count > 0)
        {
            var facing = Math.Atan2(-(near.Y - BallY), near.X - BallX);
            for (var i = 0; i < count; i++)
            {
                var a = facing + (((i + 0.5) / count) - 0.5) * 1.8;
                DrawSign(dc, centre + new Vector(Math.Cos(a) * r * 0.72, Math.Sin(a) * r * 0.72), _charge < 0, 7);
            }
        }
    }

    private void DrawBits(PaintContext dc, BenchView view)
    {
        _targets.AddBox("paper", new Rect(view.P(PaperLeft, 0.03), view.P(PaperRight, 0)));
        foreach (var bit in _bits)
        {
            var at = view.P(bit.At);
            var s = Math.Max(3, bit.Size * view.Scale);
            dc.PushTransform(SpaceDrawing.Rotation(bit.Angle, at.X, at.Y - (s * 0.3)));
            dc.DrawRectangle(StaticInk.Paper, null, new Rect(at.X - (s / 2), at.Y - (s * 0.55), s, s * 0.5));
            dc.Pop();
        }
    }

    private void DrawRod(PaintContext dc, BenchView view)
    {
        if (IsBalloon)
        {
            var c = view.P(BalloonCentre);
            var rx = BalloonX * view.Scale;
            var ry = BalloonY * view.Scale;

            // The string runs up to the hand, off the top.
            var knot = new Point(c.X, c.Y - ry);
            var hand = Math.Min(knot.Y - 20, Stage().Top + 6);
            dc.DrawGeometry(null, StaticInk.String, Line([knot, new Point(knot.X - 6, Mix(knot.Y, hand, 0.3)), new Point(knot.X + 4, Mix(knot.Y, hand, 0.7)), new Point(knot.X - 2, hand)]));
            dc.DrawGeometry(StaticInk.Balloon, null, Shape(knot + new Vector(-4, -6), knot + new Vector(4, -6), knot + new Vector(0, 2)));
            dc.DrawEllipse(StaticInk.Balloon, null, c, rx, ry);
            dc.DrawEllipse(StaticInk.BalloonShine, null, c + new Vector(-rx * 0.35, -ry * 0.4), rx * 0.3, ry * 0.22);
            _targets.Add("rod", c, Math.Max(rx, ry));

            var signs = SignCount(_charge);
            for (var i = 0; i < signs; i++)
            {
                // Rubbed on its bottom: the charge sits there (an insulator does not spread it round).
                var a = (Math.PI / 2) + ((((i + 0.5) / signs) - 0.5) * 2 * BalloonPatch);
                DrawSign(dc, c + new Vector(Math.Cos(a) * rx * 0.82, Math.Sin(a) * ry * 0.82), _charge > 0, 8);
            }

            return;
        }

        var axis = RodAxis();
        var tip = view.P(_tip);
        var end = view.P(_tip + (axis * RodLength));
        var thick = Math.Max(6, 2 * RodRadius * view.Scale);
        var (body, shine) = Rod switch
        {
            Material.Glass => (Color.FromArgb(150, 0xA8, 0xE0, 0xF0), Color.FromArgb(220, 0xF0, 0xFC, 0xFF)),
            Material.Rubber => (Color.FromRgb(0x24, 0x22, 0x28), Color.FromArgb(160, 0x7A, 0x78, 0x88)),
            _ => (Color.FromRgb(0x8A, 0x98, 0xAC), Color.FromArgb(200, 0xD8, 0xE0, 0xEC)),
        };
        dc.DrawLine(StaticInk.RodPen(body, thick), tip, end);
        var normal = new Vector(-(end - tip).Y, (end - tip).X);
        normal /= normal.Length;
        dc.DrawLine(StaticInk.RodPen(shine, thick * 0.22), tip + (normal * thick * 0.22) + ((end - tip) * 0.03), end + (normal * thick * 0.22) - ((end - tip) * 0.05));

        // The hand's grip at the far end.
        var grip = tip + ((end - tip) * 0.82);
        dc.DrawLine(StaticInk.RodPen(Color.FromRgb(0xD8, 0xA8, 0x88), thick * 1.9), grip, end);
        for (var k = 0; k <= 6; k++) _targets.Add("rod", Mix(end, tip, k / 6.0), thick);

        var count = SignCount(_charge);
        for (var i = 0; i < count; i++)
        {
            var along = (i + 0.5) / count * ChargedLength;
            var side = i % 2 == 0 ? 1 : -1;
            var at = _tip + (axis * along);
            DrawSign(dc, view.P(at) + (normal * side * (thick * 0.5 + 6)), _charge > 0, 8);
        }
    }

    private void DrawElectrons(PaintContext dc, BenchView view)
    {
        foreach (var e in _electrons)
        {
            var f = Smooth(0, 1, e.Age / 0.5);
            var from = view.P(e.From);
            var to = view.P(e.To);
            var mid = Mix(from, to, f) + new Vector(e.Bend * view.Scale * Math.Sin(f * Math.PI), 0);
            dc.DrawEllipse(Glow(Ink.Minus, 0.8), null, mid, 7, 7);
            dc.DrawEllipse(FadedBrush(Ink.Minus, 1), null, mid, 2.4, 2.4);
        }
    }

    private void DrawSpark(PaintContext dc, BenchView view)
    {
        if (_spark is not { } spark) return;
        var age = _staticTime - spark.Time;
        if (age > 0.6) return;
        var fade = 1 - Smooth(0, 0.35, age);
        var from = view.P(spark.From);
        var to = view.P(spark.To);
        if (fade > 0)
        {
            dc.DrawEllipse(Glow(Ink.Bolt, 0.9 * fade), null, Mix(from, to, 0.5), 40, 40);
            var points = Zigzag(from, to, 5, 0.25, (int)(spark.Time * 100));
            dc.DrawGeometry(null, FadedPen(Ink.Violet, 0.6 * fade, 5), Line(points));
            dc.DrawGeometry(null, FadedPen(Ink.White, fade, 1.8), Line(points));
        }

        if (ShowLabels) AddBoxTarget("spark", DrawPill(dc, LocFormat("Lightning_Static_Zap", VoltageText(spark.Volts)), to + new Vector(0, -BallRadius * view.Scale - 28), Ink.Gold, 12));
    }

    private void DrawStaticLabels(PaintContext dc, BenchView view)
    {
        var below = view.Y0 + 42;
        AddBoxTarget("cloth", DrawPill(dc, MaterialName(Cloth), new Point(view.P((ClothLeft + ClothRight) / 2, 0).X, below), Ink.Text, 11));
        AddBoxTarget("paper", DrawPill(dc, Loc("Lightning_Static_Paper"), new Point(view.P((PaperLeft + PaperRight) / 2, 0).X, below), Ink.Text, 11));
        AddBoxTarget("water", DrawPill(dc, Loc("Lightning_Static_Water"), new Point(view.P(TapX, 0).X, below), Ink.Cyan, 11));
        AddBoxTarget("ball", DrawPill(dc, Loc("Lightning_Static_Ball"), new Point(view.P(BallX, 0).X, below), Ink.Text, 11));
        AddBoxTarget("humidity", DrawPill(dc, LocFormat("Lightning_Static_Humidity", Number(Humidity, "0")), view.P(0.93, BenchHeight - 0.03), Ink.Cyan, 11));

        // The rod's name and voltage, by its handle.
        var volts = StaticModel.Voltage(_charge);
        var color = Math.Abs(volts) < 300 ? Ink.Text : volts < 0 ? Ink.Minus : Ink.Plus;
        var anchor = IsBalloon ? view.P(BalloonCentre) + new Vector(0, -(BalloonY * view.Scale) - 22) : view.P(_tip + (RodAxis() * RodLength * 0.62)) + new Vector(-18, -16);
        AddBoxTarget("rod", DrawPill(dc, LocFormat("Lightning_Static_RodLabel", MaterialName(Rod), VoltageText(volts)), anchor, color, 11.5));

        // The reach of the pull, while it is held over the paper.
        if (Math.Abs(_charge) > 2e-9 && _tip.X > PaperLeft - 0.08 && _tip.X < PaperRight + 0.08 && _tip.Y < 0.15)
        {
            var reach = StaticModel.LiftReach(_charge) * view.Scale;
            var near = view.P(NearestCharged(new Point(_tip.X, 0)));
            dc.DrawEllipse(null, StaticInk.Reach, near, reach, reach);
        }
    }

    #endregion

    private static class StaticInk
    {
        public static readonly IBrush Wall = SpaceDrawing.LinearGradient(Color.FromRgb(0x10, 0x14, 0x1E), Color.FromRgb(0x1C, 0x22, 0x30), 90);
        public static readonly IBrush TableTop = SpaceDrawing.LinearGradient(Color.FromRgb(0x8A, 0x62, 0x40), Color.FromRgb(0x6A, 0x48, 0x2E), 90);
        public static readonly IBrush TableFront = Solid(255, 0x4A, 0x32, 0x22);
        public static readonly IBrush Sheen = SpaceDrawing.LinearGradient(Color.FromArgb(110, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90);
        public static readonly Color Water = Color.FromRgb(0x5A, 0xA8, 0xFF);
        public static readonly Color WaterBright = Color.FromRgb(0xB8, 0xE0, 0xFF);
        public static readonly IBrush BeakerWater = Solid(110, 0x3A, 0x7A, 0xC8);
        public static readonly IBrush Glass = Solid(30, 0xC8, 0xE8, 0xFF);
        public static readonly IPen GlassEdge = MakePen(Solid(150, 0xC8, 0xE8, 0xFF), 1.2);
        public static readonly IBrush Valve = Solid(255, 0xC8, 0x4A, 0x3A);
        public static readonly IBrush Stand = Solid(200, 0xD8, 0xA8, 0x48);
        public static readonly IBrush Base = Solid(255, 0x3A, 0x3E, 0x48);
        public static readonly IPen Wire = MakePen(Solid(220, 0x8A, 0xD0, 0x6A), 1.4);
        public static readonly IBrush Paper = Solid(240, 0xF4, 0xF0, 0xE4);
        public static readonly IPen String = MakePen(Solid(200, 0xE0, 0xE0, 0xE0), 1);
        public static readonly IPen Reach = MakePen(Solid(110, 0xFF, 0xD3, 0x6B), 1, dashed: true);

        public static readonly IBrush Metal = new ImmutableRadialGradientBrush(
        [
            new ImmutableGradientStop(0, Color.FromRgb(0xF4, 0xF6, 0xFA)),
            new ImmutableGradientStop(1, Color.FromRgb(0x5A, 0x62, 0x70)),
        ],
            center: new RelativePoint(0.42, 0.38, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.35, 0.3, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.75, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.75, RelativeUnit.Relative));

        public static readonly IPen MetalEdge = MakePen(Solid(160, 0x2A, 0x2E, 0x38), 1);

        public static readonly IBrush Balloon = new ImmutableRadialGradientBrush(
        [
            new ImmutableGradientStop(0, Color.FromRgb(0xF0, 0x5A, 0x4A)),
            new ImmutableGradientStop(1, Color.FromRgb(0x9A, 0x1E, 0x1A)),
        ],
            center: new RelativePoint(0.45, 0.42, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.38, 0.32, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.7, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.7, RelativeUnit.Relative));

        public static readonly IBrush BalloonShine = Solid(90, 255, 255, 255);

        private static readonly Dictionary<double, IPen> Pipes = [];

        public static IPen Pipe(double thickness)
        {
            thickness = Math.Round(thickness);
            if (Pipes.TryGetValue(thickness, out var pen)) return pen;
            pen = new ImmutablePen(((IBrush)Solid(255, 0x9A, 0xA4, 0xB2)).ToImmutable(), thickness, null, PenLineCap.Round, PenLineJoin.Miter);
            Pipes[thickness] = pen;
            return pen;
        }

        private static readonly Dictionary<(Color, double), IPen> Rods = [];

        /// <summary>A round-capped pen for drawing a rod as a thick line.</summary>
        public static IPen RodPen(Color color, double thickness)
        {
            var key = (color, Math.Round(thickness, 1));
            if (Rods.TryGetValue(key, out var pen)) return pen;
            pen = new ImmutablePen(((IBrush)new SolidColorBrush(color)).ToImmutable(), key.Item2, null, PenLineCap.Round, PenLineJoin.Miter);
            Rods[key] = pen;
            return pen;
        }
    }
}
