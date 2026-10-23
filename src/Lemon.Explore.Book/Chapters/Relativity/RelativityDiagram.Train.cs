using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Relativity;

/// <summary>
/// Einstein's train: a train 600 m long (on the train) passes a platform just as long (on the platform). Just as the
/// lamp in its middle passes the person on the platform, it flashes. Light goes both ways at c in every frame, so on the
/// train it reaches the two ends together; on the platform the back runs into it and the front runs away from it, and the
/// train is shorter. The picture is worked out in the chosen frame: the flash at x = 0, t = 0, everything else from there.
/// </summary>
public sealed partial class RelativityDiagram
{
    /// <summary>The train's (and the platform's) half length where it stands still: 300 m, which light crosses in 1 µs.</summary>
    private const double HalfLength = 1;

    /// <summary>Metres per unit of length (light goes one unit in 1 µs).</summary>
    internal const double MetresPerUnit = 299.792458;

    /// <summary>How long before the flash the run starts (µs of the frame).</summary>
    private const double TrainLead = 1.1;

    /// <summary>Seconds the run waits once both ends are hit, before it starts again.</summary>
    private const double TrainRest = 2.6;

    /// <summary>The frame's time in the run, µs (0 = the flash).</summary>
    private double _trainTime = -TrainLead;

    private double _trainWait;

    private void RestartTrain()
    {
        _trainTime = double.IsNaN(TrainHold) ? -TrainLead : TrainHold;
        _trainWait = 0;
        ReportTrainPhase();
    }

    private void OnTrainHold()
    {
        if (!double.IsNaN(TrainHold)) _trainTime = TrainHold;
        _trainWait = 0;
        ReportTrainPhase();
    }

    /// <summary>The run in the chosen frame: how fast the train and the platform move, how long they are, and when the ends are hit.</summary>
    private readonly record struct TrainRun(double TrainSpeed, double PlatformSpeed, double TrainHalf, double PlatformHalf, double RearHit, double FrontHit)
    {
        public double TrainMiddle(double t) => TrainSpeed * t;

        public double PlatformMiddle(double t) => PlatformSpeed * t;
    }

    private TrainRun Run()
    {
        var gamma = RelativityModel.Gamma(Beta);
        var onTrain = Frame == RelativityFrame.Ship;
        var u = onTrain ? 0 : Beta;
        var trainHalf = onTrain ? HalfLength : HalfLength / gamma;
        return new TrainRun(u, onTrain ? -Beta : 0, trainHalf, onTrain ? HalfLength / gamma : HalfLength,
            trainHalf / (1 + u), trainHalf / (1 - u));
    }

    private void StepTrain(double dt)
    {
        if (!double.IsNaN(TrainHold)) return;
        var run = Run();
        if (_trainTime >= run.FrontHit)
        {
            _trainWait += dt;
            if (_trainWait >= TrainRest) RestartTrain();
            return;
        }

        // Slow enough to follow, but never more than about nine seconds from the flash to the second hit.
        var rate = Math.Max(1 / 1.5, run.FrontHit / 9);
        _trainTime = Math.Min(run.FrontHit, _trainTime + (dt * rate));
        ReportTrainPhase();
    }

    private string CurrentTrainPhase()
    {
        var run = Run();
        return _trainTime < 0 ? "Before" : _trainTime < run.RearHit - 1e-9 ? "Flight" : _trainTime < run.FrontHit - 1e-9 ? "RearHit" : "BothHit";
    }

    private void ReportTrainPhase()
    {
        var phase = CurrentTrainPhase();
        if (phase != TrainPhase) SetCurrentValue(TrainPhaseProperty, phase);
    }

    /// <summary>Pixels per unit and the frame's x shown at the stage's middle, so the whole run fits.</summary>
    private (double Scale, double Centre) TrainView(Rect stage, TrainRun run)
    {
        double[] xs =
        [
            run.TrainMiddle(-TrainLead) - run.TrainHalf, run.TrainMiddle(run.FrontHit) + run.TrainHalf,
            run.TrainMiddle(-TrainLead) + run.TrainHalf, run.TrainMiddle(run.FrontHit) - run.TrainHalf,
            run.PlatformMiddle(-TrainLead) - run.PlatformHalf, run.PlatformMiddle(run.FrontHit) + run.PlatformHalf,
            run.PlatformMiddle(-TrainLead) + run.PlatformHalf, run.PlatformMiddle(run.FrontHit) - run.PlatformHalf,
        ];
        var min = xs.Min();
        var max = xs.Max();
        var scale = Math.Min((stage.Width - 120) / Math.Max(0.1, max - min), stage.Width * 0.3 / HalfLength);
        return (scale, (min + max) / 2);
    }

    private void DrawTrainMode(PaintContext dc, double now)
    {
        var stage = Stage();
        var run = Run();
        var (scale, centre) = TrainView(stage, run);
        var t = _trainTime;
        var gamma = RelativityModel.Gamma(Beta);
        var onTrain = Frame == RelativityFrame.Ship;
        double X(double x) => stage.Left + (stage.Width / 2) + ((x - centre) * scale);

        var rails = stage.Top + (stage.Height * 0.56);
        var bodyTop = rails - 86;
        var windowY = bodyTop + 30;
        var platformTop = rails + 18;

        // The ground: rails with sleepers that slide by when watched from the train.
        dc.DrawRectangle(Ink.GroundSky, null, new Rect(stage.Left, stage.Top, stage.Width, rails - stage.Top));
        dc.DrawRectangle(Ink.Platform, null, new Rect(stage.Left, rails, stage.Width, stage.Bottom - rails));
        var sleeperGap = 0.25 * scale;
        var shift = run.PlatformMiddle(t) * scale;
        if (sleeperGap > 6)
        {
            for (var x = Wrap(shift, sleeperGap) - sleeperGap; x < stage.Width + sleeperGap; x += sleeperGap)
            {
                dc.DrawLine(FadedPen(Ink.Steel, 0.2, 4, flat: true), new Point(stage.Left + x, rails - 2), new Point(stage.Left + x, rails + 6));
            }
        }

        dc.DrawLine(FadedPen(Ink.Steel, 0.8, 2), new Point(stage.Left, rails), new Point(stage.Right, rails));

        // The platform (in front of the track), and where the person on it stands.
        var pMid = run.PlatformMiddle(t);
        var platform = new Rect(new Point(X(pMid - run.PlatformHalf), platformTop), new Point(X(pMid + run.PlatformHalf), platformTop + 26));
        dc.DrawRoundedRectangle(Ink.PlatformSlab, FadedPen(Ink.Steel, 0.5, 1), platform, 3, 3);
        dc.DrawLine(FadedPen(Ink.Gold, 0.7, 3, flat: true), platform.TopLeft, platform.TopRight);
        AddBoxTarget("platform", platform);
        var watcher = new Point(X(pMid), platformTop);

        // Where the flash happened: it stays put in this frame, wherever the lamp goes.
        var origin = new Point(X(0), windowY);
        if (t >= 0)
        {
            dc.DrawLine(FadedPen(Ink.Light, 0.5, 1, dashed: true), new Point(origin.X, bodyTop - 26), new Point(origin.X, platformTop + 40));
            _targets.Add("origin", new Point(origin.X, bodyTop - 18), 10);
            if (ShowLabels) DrawText(dc, Loc("Rel_Train_Origin"), new Point(origin.X + 8, platformTop + 46), Ink.Light, 10.5);
        }

        // The train: windows and wheels squeezed with it.
        var mid = run.TrainMiddle(t);
        var rear = mid - run.TrainHalf;
        var front = mid + run.TrainHalf;
        var body = new Rect(new Point(X(rear), bodyTop), new Point(X(front), rails - 14));
        DrawTrain(dc, body, gamma, onTrain);
        AddBoxTarget("train", new Rect(body.Left + 18, body.Bottom - 16, Math.Max(8, body.Width - 36), 14));

        // The light: from where the flash was, both ways at c, until it reaches an end.
        var rearHit = t >= run.RearHit;
        var frontHit = t >= run.FrontHit;
        if (t >= 0)
        {
            var left = rearHit ? rear : -t;
            var right = frontHit ? front : t;
            dc.DrawLine(FadedPen(Ink.Light, 0.28, 6, flat: true), new Point(X(left), windowY), new Point(X(right), windowY));
            if (!rearHit) DrawFlash(dc, new Point(X(-t), windowY), 5.5, Ink.Light);
            if (!frontHit) DrawFlash(dc, new Point(X(t), windowY), 5.5, Ink.Light);
            if (!rearHit || !frontHit) _targets.Add("light", new Point(X(rearHit ? t : -t), windowY), 14);
        }

        // The lamp on the roof, flashing at 0.
        var lamp = new Point(X(mid), bodyTop - 8);
        var glow = t >= 0 && t < 0.25 ? 1 - (t / 0.25) : 0;
        if (glow > 0) DrawFlash(dc, lamp, 6 + (10 * glow), Ink.Light);
        dc.DrawEllipse(FadedBrush(Ink.Light, t < 0 ? 0.55 : 0.95), FadedPen(Ink.Steel, 0.8, 1), lamp, 7, 7);
        _targets.Add("lamp", lamp, 14);

        // The person stands on the platform, in front of the train.
        DrawWatcher(dc, watcher);

        // The ends' light catchers, lit when hit, with the frame's time of each hit.
        DrawCatcher(dc, new Point(body.Left + 4, windowY), rearHit, "rear", run.RearHit, now, left: true);
        DrawCatcher(dc, new Point(body.Right - 4, windowY), frontHit, "front", run.FrontHit, now, left: false);

        if (ShowLengths) DrawLengths(dc, body, platform, gamma, onTrain);
        if (ShowClocks) DrawTrainClocks(dc, run, t, X, bodyTop, watcher, gamma, onTrain);

        if (ShowLabels)
        {
            // One row along the top, clear of the clocks above the train.
            var frameText = Loc(onTrain ? "Rel_Frame_SeenFromTrain" : "Rel_Frame_SeenFromPlatform");
            var clockText = LocFormat(onTrain ? "Rel_Train_TimeTrain" : "Rel_Train_TimePlatform", Number(t, "0.00"));
            var speedText = LocFormat(onTrain ? "Rel_Train_PlatformSpeed" : "Rel_Train_Speed", Number(Beta, "0.00"));
            DrawPillRow(dc, stage.Right - 16, stage.Top + 14,
                (speedText, onTrain ? Ink.Green : Ink.Cyan, onTrain ? "platform" : "train"), (clockText, Ink.Text, "time"), (frameText, Ink.Gold, "frame"));
        }
    }

    /// <summary>The train between <paramref name="body"/>'s ends; its windows and wheels are as squeezed as the train.</summary>
    private void DrawTrain(PaintContext dc, Rect body, double gamma, bool onTrain)
    {
        var squeeze = onTrain ? 1 : 1 / gamma;
        if (squeeze < 0.999)
        {
            var rest = body.Width / squeeze;
            var ghost = new Rect(body.Left + (body.Width / 2) - (rest / 2), body.Top, rest, body.Height);
            dc.DrawRoundedRectangle(null, FadedPen(Ink.Cyan, 0.35, 1.2, dashed: true), ghost, 8, 8);
        }

        dc.DrawRoundedRectangle(Ink.Carriage, FadedPen(Ink.Steel, 0.7, 1.2), body, 8 * squeeze, 8);
        var count = 12;
        var step = body.Width / count;
        for (var i = 0; i < count; i++)
        {
            var w = new Rect(body.Left + (i * step) + (step * 0.18), body.Top + 14, step * 0.64, 22);
            dc.DrawRoundedRectangle(Ink.TrainWindow, null, w, 3 * squeeze, 3);
        }

        dc.DrawLine(FadedPen(Ink.Cyan, 0.7, 3, flat: true), new Point(body.Left + 2, body.Bottom - 12), new Point(body.Right - 2, body.Bottom - 12));
        var wheels = 8;
        for (var i = 0; i < wheels; i++)
        {
            var cx = body.Left + (body.Width * (i + 0.5) / wheels);
            dc.DrawEllipse(Ink.Wheel, FadedPen(Ink.Steel, 0.8, 1.2), new Point(cx, body.Bottom + 6), 7 * squeeze, 7);
        }
    }

    private void DrawCatcher(PaintContext dc, Point at, bool hit, string id, double hitTime, double now, bool left)
    {
        var box = new Rect(at.X - 5, at.Y - 13, 10, 26);
        dc.DrawRoundedRectangle(hit ? FadedBrush(Ink.Light, 0.95) : FadedBrush(Ink.Steel, 0.4), null, box, 3, 3);
        if (hit)
        {
            var pulse = 0.5 + (0.5 * Math.Sin(now * 6));
            dc.DrawEllipse(FadedBrush(Ink.Light, 0.18 + (0.12 * pulse)), null, at, 18, 18);
        }

        _targets.Add(id, at, 16);
        if (!ShowLabels) return;
        var text = hit ? LocFormat("Rel_Train_HitAt", Number(hitTime, "0.00")) : Loc(left ? "Rel_Train_Rear" : "Rel_Train_Front");
        var pill = new Point(at.X + (left ? -8 : 8), at.Y - 64);
        var formatted = _labels.Text(text, 11, hit ? Ink.Light : Ink.Dim);
        var x = left ? pill.X - (formatted.Width / 2) : pill.X + (formatted.Width / 2);
        DrawTargetPill(dc, text, new Point(Math.Clamp(x, 70, ActualWidth - 70), pill.Y), hit ? Ink.Light : Ink.Dim, id, 11);
    }

    /// <summary>The person on the platform, feet at <paramref name="feet"/> (in front of the train, which is behind the platform).</summary>
    private void DrawWatcher(PaintContext dc, Point feet)
    {
        var pen = FadedPen(Ink.Green, 0.95, 2.2);
        var stand = feet + new Vector(0, 4);
        var head = stand + new Vector(0, -64);
        var hip = stand + new Vector(0, -26);
        dc.DrawEllipse(Ink.Backdrop, pen, head, 7, 7);
        dc.DrawLine(pen, head + new Vector(0, 7), hip);
        dc.DrawLine(pen, hip, stand + new Vector(-8, 0));
        dc.DrawLine(pen, hip, stand + new Vector(8, 0));
        dc.DrawLine(pen, head + new Vector(0, 18), head + new Vector(-11, 32));
        dc.DrawLine(pen, head + new Vector(0, 18), head + new Vector(11, 32));
        _targets.Add("watcher", head + new Vector(0, 26), 22);
    }

    /// <summary>Lengths: the train's above it and the platform's below, each against its own 600 m.</summary>
    private void DrawLengths(PaintContext dc, Rect body, Rect platform, double gamma, bool onTrain)
    {
        var trainMetres = 2 * HalfLength * MetresPerUnit / (onTrain ? 1 : gamma);
        var platformMetres = 2 * HalfLength * MetresPerUnit / (onTrain ? gamma : 1);
        var top = body.Top - 70;
        var trainText = !onTrain && gamma > 1.0005 ? LocFormat("Rel_Train_LengthShort", Number(trainMetres, "0")) : LocFormat("Rel_Train_Length", Number(trainMetres, "0"));
        DimensionLine(dc, body.Left, body.Right, top, trainText, Ink.Cyan, "trainlength");
        var bottom = platform.Bottom + 104;
        var platformText = onTrain && gamma > 1.0005 ? LocFormat("Rel_Train_PlatformLengthShort", Number(platformMetres, "0")) : LocFormat("Rel_Train_PlatformLength", Number(platformMetres, "0"));
        DimensionLine(dc, platform.Left, platform.Right, bottom, platformText, Ink.Green, "platformlength");
    }

    private void DimensionLine(PaintContext dc, double left, double right, double y, string text, Color color, string id)
    {
        var pen = FadedPen(color, 0.85, 1.4);
        dc.DrawLine(pen, new Point(left, y - 6), new Point(left, y + 6));
        dc.DrawLine(pen, new Point(right, y - 6), new Point(right, y + 6));
        dc.DrawLine(pen, new Point(left, y), new Point(right, y));
        var box = DrawPill(dc, text, new Point((left + right) / 2, y), color, 11);
        box = box.Union(new Rect(left, y - 6, Math.Max(1, right - left), 12));
        AddBoxTarget(id, box);
    }

    /// <summary>
    /// The train's three clocks (back, lamp, front), set together on the train so they all read 0 as the lamp flashed,
    /// and the clock of the person on the platform. Each reads its own time at this moment of the frame being watched.
    /// </summary>
    private void DrawTrainClocks(PaintContext dc, TrainRun run, double t, Func<double, double> x, double bodyTop, Point watcher, double gamma, bool onTrain)
    {
        var y = bodyTop - 122;
        var box = default(Rect);
        foreach (var along in new[] { -HalfLength, 0, HalfLength })
        {
            var reading = onTrain ? t : RelativityModel.TrainClock(t, along, Beta);
            var at = new Point(x(run.TrainMiddle(t) + (along * (onTrain ? 1 : 1 / gamma))), y);
            box = box.Union(DrawDial(dc, at, 15, reading, 4, Ink.Cyan, LocFormat("Rel_Value_Us", Number(reading, "0.00"))));
        }

        AddBoxTarget("trainclocks", box);
        var platformReading = onTrain ? RelativityModel.TrainClock(t, 0, -Beta) : t;
        var dial = new Point(watcher.X - 44, watcher.Y + 50);
        var pbox = DrawDial(dc, dial, 15, platformReading, 4, Ink.Green, LocFormat("Rel_Value_Us", Number(platformReading, "0.00")));
        AddBoxTarget("platformclock", pbox);
    }
}
