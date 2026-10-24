using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.Explore.Lessons;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.Infrastructures.Timing;
using Lemon.Explore.Book.ViewModels;
using Lemon.Explore.Book.Views;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

/// <summary>
/// Not a check: records a chapter's walkthroughs, each view in turn, as a video for short-video sites. Runs only with
/// <c>BOOK_VIDEO_DIR</c> set (use <c>scripts/video/make-chapter-video.py</c>, which also adds the voice).
/// <c>BOOK_VIDEO_CHAPTER</c> is the chapter's slug, <c>BOOK_VIDEO_CULTURE</c> its language, <c>BOOK_VIDEO_VIEWS</c>
/// (comma-separated, optional) the views to record, <c>BOOK_VIDEO_LAYOUT</c> <c>desktop</c> (the spread, 16:9, the
/// default) or <c>phone</c> (one page, 9:16), and <c>BOOK_VIDEO_SCALE</c> the pixels per layout unit. Writes <c>frames.mp4</c> (picture only, via ffmpeg on the PATH)
/// and <c>timeline.json</c> (when each clip started and stopped).
/// </summary>
/// <remarks>
/// Rendering here is far slower than real time, so <see cref="BookClock"/> is held and stepped one frame per frame:
/// every animation, walkthrough and clip moves on exactly 1/30 s between captures, however long a capture takes.
/// </remarks>
public sealed class ChapterVideoExport
{
    private const int Fps = 30;

    /// <summary>Layout units: the desktop window's default width at 16:9 (it opens 1280 x 820), or a phone page.</summary>
    private static readonly Size Desktop = new(1280, 720);
    private static readonly Size Phone = new(400, 711.2);

    /// <summary>Pixels per unit when none is given: 1280 x 720, or 720 x 1280 for the phone.</summary>
    private const double DesktopScale = 1.0;
    private const double PhoneScale = 1.8;

    [AvaloniaFact]
    public void RecordsAChapter_WhenAskedTo()
    {
        var directory = Environment.GetEnvironmentVariable("BOOK_VIDEO_DIR");
        if (string.IsNullOrEmpty(directory)) return;

        var slug = Environment.GetEnvironmentVariable("BOOK_VIDEO_CHAPTER") is { Length: > 0 } s ? s : "Seasons";
        var culture = Environment.GetEnvironmentVariable("BOOK_VIDEO_CULTURE") is { Length: > 0 } c ? c : "zh-CN";
        var views = Environment.GetEnvironmentVariable("BOOK_VIDEO_VIEWS") is { Length: > 0 } v
            ? v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
        var phone = string.Equals(Environment.GetEnvironmentVariable("BOOK_VIDEO_LAYOUT"), "phone", StringComparison.OrdinalIgnoreCase);
        var scale = double.TryParse(Environment.GetEnvironmentVariable("BOOK_VIDEO_SCALE"), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            ? x
            : phone ? PhoneScale : DesktopScale;
        var layout = phone ? Phone : Desktop;

        // Even pixel sizes, as H.264 in 4:2:0 needs.
        var size = new PixelSize((int)(layout.Width * scale / 2) * 2, (int)(layout.Height * scale / 2) * 2);
        Directory.CreateDirectory(directory);

        var previous = LocalizationService.Instance.CurrentCulture;
        LocalizationService.Instance.SetCulture(CultureInfo.GetCultureInfo(culture));
        BookClock.BeginStepping();
        try
        {
            Record(slug, views, phone, scale, size, directory);
        }
        finally
        {
            BookClock.EndStepping();
            LocalizationService.Instance.SetCulture(previous);
        }
    }

    private static void Record(string slug, string[] views, bool phone, double scale, PixelSize size, string directory)
    {
        var clock = new BookStopwatch();
        var audioRoot = Path.Combine(LocalizationCoverageTests.FindRepositoryRoot(), "assets", "narration", "audio");
        var audio = new RecordingAudio(clock, NarrationManifest.Bundled, audioRoot);
        var settings = new MemorySettingsStore();
        settings.Set(SettingKeys.AnimatesTurns, bool.FalseString);
        var model = new MainViewModel(BookServices.Create(settings, NarrationManifest.Bundled, audio));

        // The layout sees the window's size in units; the frame has scale times as many pixels each way.
        var view = new LayoutTransformControl
        {
            LayoutTransform = new ScaleTransform(scale, scale),
            Child = new MainView { DataContext = model },
        };
        var window = new Window { Width = size.Width, Height = size.Height, Content = view };
        window.Show();

        // A phone shows one page, the diagram; the desktop shows the chapter's whole spread.
        var page = TextPageOf(slug);
        model.GoToPage(phone ? page + 1 : page);
        Frames(15);

        // The chapter's view model is the text page's property that owns the walkthrough. Most chapters have views
        // (a Mode enum), each with its own walkthrough; a chapter without has one.
        var text = model.Pages[page];
        var content = text.GetType().GetProperties().Select(p => p.GetValue(text))
            .First(v => v?.GetType().GetProperty("Lesson")?.PropertyType == typeof(LessonPlayer))!;
        var lesson = (LessonPlayer)content.GetType().GetProperty("Lesson")!.GetValue(content)!;
        var mode = content.GetType().GetProperty("Mode") is { PropertyType.IsEnum: true } m ? m : null;
        var modes = mode is null
            ? [null]
            : Enum.GetValues(mode.PropertyType).Cast<object?>()
                .Where(m => views.Length == 0 || views.Contains(m!.ToString(), StringComparer.OrdinalIgnoreCase))
                .ToList();
        Assert.NotEmpty(modes);

        // Nobody watching a video can answer or have a go: show the answer and move on soon.
        lesson.AnswerSeconds = 3;
        lesson.TryItSeconds = 2;

        using var encoder = new FrameEncoder(Path.Combine(directory, "frames.mp4"), size);
        clock.Start();
        foreach (var value in modes)
        {
            mode?.SetValue(content, value);
            Hold(window, encoder, clock, 1.0);
            lesson.Start();
            var limit = clock.Elapsed.TotalSeconds + 600;
            while (lesson.State != LessonState.Done && clock.Elapsed.TotalSeconds < limit)
            {
                Step(window, encoder);
            }

            Hold(window, encoder, clock, 1.5);
            lesson.Stop();
        }

        clock.Stop();
        encoder.Finish();
        window.Close();

        var timeline = new
        {
            chapter = slug,
            culture = NarrationService.CultureFolder(LocalizationService.Instance.CurrentCulture.Name),
            duration = clock.Elapsed.TotalSeconds,
            clips = audio.Log.Select(e => new { file = e.File, key = e.Key, start = e.Start, end = e.End }),
        };
        File.WriteAllText(Path.Combine(directory, "timeline.json"),
            JsonSerializer.Serialize(timeline, new JsonSerializerOptions { WriteIndented = true }));
        Assert.NotEmpty(audio.Log);
    }

    private static void Hold(Window window, FrameEncoder encoder, BookStopwatch clock, double seconds)
    {
        var until = clock.Elapsed.TotalSeconds + seconds;
        while (clock.Elapsed.TotalSeconds < until) Step(window, encoder);
    }

    /// <summary>One frame: time moves on 1/30 s, the page updates and renders, and the picture is kept.</summary>
    private static void Step(Window window, FrameEncoder encoder)
    {
        BookClock.Advance(1.0 / Fps);
        Frames(1);
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        encoder.Add(frame);
    }

    /// <summary>Pipes frames to ffmpeg as raw pixels, one video frame each.</summary>
    private sealed class FrameEncoder : IDisposable
    {
        private readonly Process _ffmpeg;
        private readonly Stream _input;
        private readonly PixelSize _size;
        private readonly byte[] _pixels;

        public FrameEncoder(string path, PixelSize size)
        {
            _size = size;
            _pixels = new byte[size.Width * size.Height * 4];
            _ffmpeg = Process.Start(new ProcessStartInfo("ffmpeg",
                [
                    "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "bgra", "-s", $"{size.Width}x{size.Height}",
                    "-r", Fps.ToString(CultureInfo.InvariantCulture), "-i", "-",
                    "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p", path,
                ])
            { RedirectStandardInput = true, UseShellExecute = false })!;
            _input = _ffmpeg.StandardInput.BaseStream;
        }

        public void Add(Avalonia.Media.Imaging.Bitmap frame)
        {
            var handle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
            try
            {
                frame.CopyPixels(new PixelRect(_size), handle.AddrOfPinnedObject(), _pixels.Length, _size.Width * 4);
            }
            finally
            {
                handle.Free();
            }

            _input.Write(_pixels);
        }

        public void Finish()
        {
            _input.Close();
            _ffmpeg.WaitForExit();
            Assert.Equal(0, _ffmpeg.ExitCode);
        }

        public void Dispose()
        {
            if (!_ffmpeg.HasExited) _ffmpeg.Kill();
            _ffmpeg.Dispose();
        }
    }

    /// <summary>A clip played, in seconds on the recording's clock; <c>End</c> is null when it played to the end.</summary>
    private sealed record Entry(string File, string Key, double Start)
    {
        public double? End { get; set; }
    }

    /// <summary>
    /// "Plays" the committed mp3s without a sound card: notes when each starts and stops, and reports the position
    /// from the manifest's length. A video run never pauses (nobody touches the picture), so pausing only holds the
    /// position.
    /// </summary>
    private sealed class RecordingAudio(BookStopwatch clock, NarrationManifest manifest, string root) : INarrationAudio
    {
        private Entry? _current;
        private double _duration;
        private double? _pausedAt;

        public List<Entry> Log { get; } = [];

        public bool Has(string clip) => File.Exists(Path.Combine(root, clip));

        public bool Play(string clip, double volume)
        {
            Stop();
            var folder = clip.Split('/')[0];
            var key = Path.GetFileName(clip).Split('.')[0];
            _duration = manifest.Culture(folder)?.Clips.GetValueOrDefault(key)?.Duration ?? 0;
            _current = new Entry(clip, key, clock.Elapsed.TotalSeconds);
            _pausedAt = null;
            Log.Add(_current);
            return true;
        }

        public void Pause() => _pausedAt ??= Position;

        public void Resume() => _pausedAt = null;

        public void Stop()
        {
            if (_current is { End: null } && !IsEnded) _current.End = clock.Elapsed.TotalSeconds;
            _current = null;
        }

        public double Volume
        {
            set { }
        }

        public double Rate
        {
            set { }
        }

        public double Position => _pausedAt ?? (_current is null ? 0 : clock.Elapsed.TotalSeconds - _current.Start);

        public bool IsEnded => _current is null || Position >= _duration;

        public event EventHandler? Failed
        {
            add { }
            remove { }
        }
    }
}
