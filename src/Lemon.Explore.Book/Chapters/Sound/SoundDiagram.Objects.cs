using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Sound;

/// <summary>
/// The things in the sound diagram that can be clicked for an explanation, the walkthrough's highlight, and the hands-on
/// parts: dragging the speaker's cone pushes the air, dragging the string plucks it (a click on it plucks there, a click
/// on the neck holds a fret), a click on a harmonic or pipe row picks it, the keys play, and the overtone bars drag.
/// </summary>
public sealed partial class SoundDiagram
{
    /// <summary>What the walkthrough points at: a clickable thing's id (see <see cref="ObjectName"/>).</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<SoundDiagram, string?>(nameof(Highlight));

    /// <summary>The thing last clicked, whose explanation the page shows; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedObjectProperty =
        AvaloniaProperty.Register<SoundDiagram, string?>(nameof(SelectedObject), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer clicks or drags, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<SoundDiagram, ICommand?>(nameof(InteractionCommand));

    /// <summary>A walkthrough is running: leave room at the bottom for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<SoundDiagram, bool>(nameof(IsLessonActive), false);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<SoundDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnObjects = RedrawOnObjects();

    private static bool RedrawOnObjects()
    {
        AffectsRender<SoundDiagram>(TitleHeightProperty);
        return true;
    }

    public string? Highlight
    {
        get => GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    public string? SelectedObject
    {
        get => GetValue(SelectedObjectProperty);
        set => SetValue(SelectedObjectProperty, value);
    }

    public ICommand? InteractionCommand
    {
        get => GetValue(InteractionCommandProperty);
        set => SetValue(InteractionCommandProperty, value);
    }

    public bool IsLessonActive
    {
        get => GetValue(IsLessonActiveProperty);
        set => SetValue(IsLessonActiveProperty, value);
    }

    public double TitleHeight
    {
        get => GetValue(TitleHeightProperty);
        set => SetValue(TitleHeightProperty, value);
    }

    private readonly ExploreTargets _targets = new();
    private readonly LessonReserve _reserve = new();

    /// <summary>What the mouse is doing now.</summary>
    private enum DragKind
    {
        None,

        /// <summary>The button is down; moving far enough starts one of the drags below, else it is a click.</summary>
        Pending,
        Cone,
        String,
        Bars,
        Keys,
    }

    private DragKind _drag;

    /// <summary>Where the pointer last was: a capture lost mid-drag ends the drag there.</summary>
    private Point _lastPointer;

    /// <summary>The thing the button went down on; a click if it comes up there without moving far.</summary>
    private string? _pressed;

    private Point _dragStart;
    private bool _dragMoved;
    private int _lastKey = -1;
    private int _lastBar = -1;
    private double _lastBarValue;

    /// <summary>The thing a target id stands for (<c>harmonic3</c> → <c>Obj_Harmonic</c>). It names the title and the narration clip.</summary>
    internal static string? ObjectName(string? id) => id switch
    {
        "speaker" => "Obj_Speaker",
        "compression" => "Obj_Compression",
        "rarefaction" => "Obj_Rarefaction",
        "particle" => "Obj_Particle",
        "wavelength" => "Obj_Wavelength",
        "pressure" => "Obj_Pressure",
        "ear" => "Obj_Ear",
        "medium" => "Obj_Medium",
        "string" => "Obj_String",
        "bridge" => "Obj_Bridge",
        "fret" => "Obj_Fret",
        "pluck" => "Obj_Pluck",
        "node" => "Obj_Node",
        "antinode" => "Obj_Antinode",
        "body" => "Obj_Body",
        "harmonic1" => "Obj_Harmonic1",
        "harmonic2" or "harmonic3" or "harmonic4" or "harmonic5" or "harmonic6" => "Obj_Harmonic",
        "pipe" => "Obj_Pipe",
        "openend" => "Obj_OpenEnd",
        "closedend" => "Obj_ClosedEnd",
        "pnode" => "Obj_PipeNode",
        "pantinode" => "Obj_PipeAntinode",
        "pipemode1" or "pipemode2" or "pipemode3" or "pipemode4" => "Obj_PipeMode",
        "waveform" => "Obj_Waveform",
        "period" => "Obj_Period",
        "timbre" => "Obj_Timbre",
        "envelope" => "Obj_Envelope",
        "fundamental" => "Obj_Fundamental",
        "overtone" => "Obj_Overtone",
        "keyboard" => "Obj_Keyboard",
        "octave" => "Obj_Octave",
        "beat" => "Obj_Beat",
        _ => null,
    };

    /// <summary>The resource key of a target's title: <c>Sound_Obj_Speaker</c>.</summary>
    internal static string? ObjectTitleKey(string? id) => ObjectName(id) is { } name ? "Sound_" + name : null;

    /// <summary>The narration clip (and card text) explaining a target, or null.</summary>
    internal static string? ObjectClipKey(string? id) => ObjectName(id) is { } name ? "Narr_Sound_" + name : null;

    /// <summary>Lines and curves are glowed along their length by the diagram; everything else gets a ring.</summary>
    private static bool IsLineHighlight(string id) => id is "string";

    /// <summary>Whether the walkthrough points at <paramref name="id"/>, which the diagram glows itself.</summary>
    private bool Glows(string id) => Highlight == id;

    /// <summary>A text, a pill or a part already drawn in <paramref name="box"/>, as a target.</summary>
    private void AddBoxTarget(string id, Rect box)
    {
        box = box.Inflate(new Thickness(2, 1));
        _targets.AddBox(id, box);
    }

    /// <summary>The title sits over the top of the picture: the picture starts below its last line (its top margin is 22), however it wraps.</summary>
    // The book puts the title on the left page: only a margin at the top, not the original's title band.
    private double TitleSpace => 22 + TitleHeight + 14;

    #region clicking and dragging

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragStart = e.GetPosition(this);
        _lastPointer = _dragStart;
        _dragMoved = false;
        _pressed = _targets.Find(_dragStart)?.Id;
        e.Pointer.Capture(this);

        e.Handled = true;
        _drag = DragKind.Pending;
        if (Mode != SoundMode.Timbre || _pressed is not null) return;

        // Keys sound the moment they are pressed; the bars follow the mouse at once.
        if (KeyAt(_dragStart) is { } key)
        {
            _drag = DragKind.Keys;
            _lastKey = -1;
            PlayKey(key);
        }
        else if (InBars(_dragStart))
        {
            Interacted();
            _drag = DragKind.Bars;
            _lastBar = -1;
            EditBars(_dragStart, done: false);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        _lastPointer = point;
        switch (_drag)
        {
            case DragKind.None:
                UpdateHover(point);
                return;

            case DragKind.Pending:
                if (((Vector)(point - _dragStart)).Length <= 4) return;
                _dragMoved = true;
                StartDrag(point);
                break;

            case DragKind.Keys:
                if (KeyAt(point) is { } key && key != _lastKey) PlayKey(key);
                return;

            case DragKind.Bars:
                EditBars(point, done: false);
                return;
        }

        switch (_drag)
        {
            case DragKind.Cone:
                _handCone = Math.Clamp((point.X - _dragStart.X) * 0.8, -24, 24);
                break;

            case DragKind.String:
                PullString(point);
                break;
        }

        InvalidateVisual();
    }

    /// <summary>A press that moved: what it drags depends on where it started.</summary>
    private void StartDrag(Point point)
    {
        _pressed = null;
        if (Mode == SoundMode.Wave && (Inflate(_wave.Speaker, 10, 10).Contains(_dragStart)))
        {
            Interacted();
            _drag = DragKind.Cone;
            _handCone = _cone;
            return;
        }

        if (Mode == SoundMode.Strings && Resonator == Resonator.String && NearString(_dragStart))
        {
            Interacted();
            _drag = DragKind.String;
            PullString(point);
            return;
        }

        _drag = DragKind.None;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetPosition(this);
        var drag = _drag;
        var picked = _pressed;
        _drag = DragKind.None;
        _pressed = null;
        e.Pointer.Capture(null);

        switch (drag)
        {
            case DragKind.Cone:
                ReleaseCone();
                return;

            case DragKind.String:
                if (PluckCommand is { } pluck && pluck.CanExecute(null)) pluck.Execute(new PluckRequest(_pullPoint, _pullHeight));
                return;

            case DragKind.Bars:
                EditBars(point, done: true);
                return;

            case DragKind.Keys:
                return;
        }

        if (_dragMoved) return;
        if (picked is not null)
        {
            if (_targets.Find(point)?.Id == picked) Pick(picked);
            return;
        }

        ClickEmpty(point);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _targets.Hover = null;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_drag == DragKind.Cone) ReleaseCone();
        if (_drag == DragKind.Bars) EditBars(_lastPointer, done: true);
        _drag = DragKind.None;
        _pressed = null;
    }

    private void UpdateHover(Point point)
    {
        var hover = _targets.Find(point)?.Id;
        _targets.Hover = hover;
        Cursor = hover is "speaker" ? new Cursor(StandardCursorType.SizeWestEast)
            : hover is not null ? new Cursor(StandardCursorType.Hand)
            : Mode == SoundMode.Strings && Resonator == Resonator.String && NearString(point) ? new Cursor(StandardCursorType.SizeNorthSouth)
            : Mode == SoundMode.Timbre && KeyAt(point) is not null ? new Cursor(StandardCursorType.Hand)
            : Mode == SoundMode.Timbre && InBars(point) ? new Cursor(StandardCursorType.SizeNorthSouth)
            : Mode == SoundMode.Strings && Resonator == Resonator.String && _string.Board.Contains(point) ? new Cursor(StandardCursorType.Hand)
            : null;
    }

    /// <summary>A click on nothing in particular: follow a dot of air, hold a fret, or pluck the string there.</summary>
    private void ClickEmpty(Point point)
    {
        switch (Mode)
        {
            case SoundMode.Wave when Medium != SoundMedium.Vacuum && Inflate(_wave.Band, 6, 6).Contains(point):
            {
                var columns = _wave.Columns;
                var rows = _wave.Rows;
                var spacingX = _wave.Band.Width / columns;
                var spacingY = _wave.Band.Height / rows;
                var best = double.MaxValue;
                for (var c = 0; c < columns; c++)
                {
                    var x = _wave.X0 + ((c + 0.5) * spacingX) + Displacement((c + 0.5) * spacingX);
                    if (Math.Abs(x - point.X) > spacingX * 1.5) continue;
                    for (var r = 0; r < rows; r++)
                    {
                        var (jx, jy) = Jitter(c, r);
                        var d = ((Vector)(new Point(x + (jx * spacingX * 0.32), _wave.Band.Top + ((r + 0.5) * spacingY) + (jy * spacingY * 0.32)) - point)).Length;
                        if (d < best)
                        {
                            best = d;
                            _trackColumn = c;
                            _trackRow = r;
                        }
                    }
                }

                Pick("particle");
                break;
            }

            case SoundMode.Strings when Resonator == Resonator.String:
                if (NearString(point))
                {
                    Interacted();
                    var u = Math.Clamp(_string.Fraction(point.X), 0.05, 0.95);
                    if (PluckCommand is { } pluck && pluck.CanExecute(null)) pluck.Execute(new PluckRequest(u, 0.7));
                }
                else if (Inflate(_string.Board, 0, 6).Contains(point))
                {
                    Interacted();
                    var fret = 0;
                    for (var k = 1; k <= 12; k++)
                    {
                        if (point.X > _string.FretX(k - 1)) fret = k;
                    }

                    if (point.X > _string.FretX(12)) return;
                    SetCurrentValue(FretProperty, fret);
                }

                break;
        }
    }

    /// <summary>Whether <paramref name="point"/> is on (or close to) the vibrating part of the string.</summary>
    private bool NearString(Point point)
    {
        var s = _string;
        if (point.X < s.At(0.02) || point.X > s.At(0.98)) return false;
        var y = s.Y - (StringY(s.Fraction(point.X)) * s.MaxPull);
        return Math.Abs(point.Y - y) < 16;
    }

    /// <summary>The string held at <paramref name="point"/>: where along it, and how far aside (at most 1.2 of the usual pull).</summary>
    private void PullString(Point point)
    {
        _pullPoint = Math.Clamp(_string.Fraction(point.X), 0.05, 0.95);
        _pullHeight = Math.Clamp((_string.Y - point.Y) / _string.MaxPull, -1.2, 1.2);
        if (Math.Abs(_pullHeight) < 0.15) _pullHeight = 0.15 * (_pullHeight < 0 ? -1 : 1);
    }

    private bool InBars(Point point)
    {
        var bars = _timbre.Bars;
        return new Rect(bars.Left, bars.Top - 10, bars.Width, bars.Height + 22).Contains(point);
    }

    /// <summary>Sets the bar under the mouse (and any skipped over since the last move) to the mouse's height.</summary>
    private void EditBars(Point point, bool done)
    {
        if (EditHarmonicCommand is not { } command) return;
        var bar = BarAt(point.X);
        var value = BarValue(point.Y);
        if (_lastBar >= 0 && Math.Abs(bar - _lastBar) > 1)
        {
            var step = Math.Sign(bar - _lastBar);
            for (var b = _lastBar + step; b != bar; b += step)
            {
                var t = (b - _lastBar) / (double)(bar - _lastBar);
                command.Execute(new HarmonicEdit(b, _lastBarValue + ((value - _lastBarValue) * t), false));
            }
        }

        command.Execute(new HarmonicEdit(bar, value, done));
        _lastBar = bar;
        _lastBarValue = value;
    }

    /// <summary>A key pressed: it becomes the note, and the page plays it.</summary>
    private void PlayKey(int midi)
    {
        Interacted();
        _lastKey = midi;
        SetCurrentValue(NoteMidiProperty, midi);
        if (PlayNoteCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>A click on a thing: show (and say) what it is; a harmonic or pipe row is also chosen.</summary>
    private void Pick(string id)
    {
        Interacted();
        if (id.StartsWith("harmonic", StringComparison.Ordinal) && int.TryParse(id.AsSpan(8), out var n))
        {
            SetCurrentValue(SoloHarmonicProperty, SoloHarmonic == n ? 0 : n);
        }
        else if (id.StartsWith("pipemode", StringComparison.Ordinal) && int.TryParse(id.AsSpan(8), out var m))
        {
            SetCurrentValue(PipeModeProperty, m);
        }

        // Cleared first so clicking the same thing again says it again.
        SetCurrentValue(SelectedObjectProperty, null);
        SetCurrentValue(SelectedObjectProperty, id);
    }

    #endregion
}
