using System.Diagnostics;
using System.Text.Json;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;

namespace Lemon.Explore.Book.Explore.Narration;

/// <summary>The reader's narration preferences, saved as one JSON setting.</summary>
public sealed record NarrationSettings
{
    /// <summary>Speak the clips; when off, walkthroughs still run with subtitles at a reading pace.</summary>
    public bool VoiceEnabled { get; init; } = true;

    public double Volume { get; init; } = 0.9;

    public bool ShowSubtitles { get; init; } = true;

    /// <summary>Clicking a thing in a picture reads its explanation aloud (it is always shown).</summary>
    public bool SpeakOnClick { get; init; } = true;
}

/// <summary>
/// The book's one narration channel: plays a clip (<c>Narr_*</c> key) in the current language, one at a time.
/// Audio is pre-rendered (<see cref="NarrationManifest"/>); a clip without audio runs on a silent clock with estimated
/// timings so subtitles and walkthroughs behave the same.
/// </summary>
public interface INarrationService
{
    NarrationSettings Settings { get; }

    event EventHandler? SettingsChanged;

    void Update(NarrationSettings settings);

    /// <summary>Starts <paramref name="key"/>, replacing whatever was playing. <paramref name="owner"/> is who asked, for <see cref="Stop"/>.</summary>
    NarrationClip Play(string key, object owner);

    void Pause();

    void Resume();

    /// <summary>Stops the current clip; with an owner, only if that owner started it.</summary>
    void Stop(object? owner = null);

    /// <summary>The clip playing now (its timings follow the audio), or null.</summary>
    NarrationClip? Clip { get; }

    string? CurrentKey { get; }

    object? CurrentOwner { get; }

    bool IsPaused { get; }

    /// <summary>Seconds into the current clip.</summary>
    double Position { get; }

    /// <summary>The current clip has played to the end (also true when nothing is playing).</summary>
    bool IsFinished { get; }
}

public sealed class NarrationService : INarrationService
{
    private readonly ISettingsStore _store;
    private readonly NarrationManifest _manifest;
    private readonly INarrationAudio _audio;
    private readonly Stopwatch _silent = new();
    private NarrationClip? _clip;
    private bool _playingAudio;

    public NarrationService(ISettingsStore store, NarrationManifest manifest, INarrationAudio audio)
    {
        _store = store;
        _manifest = manifest;
        _audio = audio;
        Settings = Load(store);
        audio.Failed += OnAudioFailed;
    }

    public NarrationSettings Settings { get; private set; }

    public event EventHandler? SettingsChanged;

    public NarrationClip? Clip => _clip;

    public string? CurrentKey => _clip?.Key;

    public object? CurrentOwner { get; private set; }

    public bool IsPaused { get; private set; }

    public double Position => _clip is null ? 0 : _playingAudio ? _audio.Position : _silent.Elapsed.TotalSeconds;

    public bool IsFinished => _clip is null
        || (_playingAudio ? _audio.IsEnded || Position > _clip.Duration + 3 : _silent.Elapsed.TotalSeconds >= _clip.Duration);

    public void Update(NarrationSettings settings)
    {
        if (settings == Settings)
        {
            return;
        }

        var voiceTurnedOff = Settings.VoiceEnabled && !settings.VoiceEnabled;
        Settings = settings with { Volume = Math.Clamp(settings.Volume, 0, 1) };
        _store.Set(SettingKeys.Narration, JsonSerializer.Serialize(Settings, BookJson.Default.NarrationSettings));
        _audio.Volume = Settings.Volume;

        // Carry on where the voice was, without it.
        if (voiceTurnedOff && _playingAudio)
        {
            SwitchToSilent(Position);
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public NarrationClip Play(string key, object owner)
    {
        StopPlayback();
        var culture = LocalizationService.Instance.CurrentCulture;
        var folder = CultureFolder(culture.Name);
        var entry = _manifest.Culture(folder)?.Clips.GetValueOrDefault(key);
        var hasAudio = entry is not null && _audio.Has(folder, key);
        _clip = entry is null
            ? NarrationClip.Estimate(key, LocalizationService.Instance.GetString(key), culture)
            : new NarrationClip(key, entry.Duration, entry.Sentences, hasAudio);
        CurrentOwner = owner;
        IsPaused = false;

        _playingAudio = hasAudio && Settings.VoiceEnabled && _audio.Play(folder, key, Settings.Volume);
        if (!_playingAudio)
        {
            _silent.Restart();
        }

        return _clip;
    }

    public void Pause()
    {
        if (_clip is null || IsPaused)
        {
            return;
        }

        IsPaused = true;
        if (_playingAudio)
        {
            _audio.Pause();
        }
        else
        {
            _silent.Stop();
        }
    }

    public void Resume()
    {
        if (_clip is null || !IsPaused)
        {
            return;
        }

        IsPaused = false;
        if (_playingAudio)
        {
            _audio.Resume();
        }
        else
        {
            _silent.Start();
        }
    }

    public void Stop(object? owner = null)
    {
        if (owner is not null && !ReferenceEquals(owner, CurrentOwner))
        {
            return;
        }

        StopPlayback();
        _clip = null;
        CurrentOwner = null;
        IsPaused = false;
    }

    /// <summary>The audio folder for a culture: zh-CN for any Chinese, otherwise English.</summary>
    internal static string CultureFolder(string culture) =>
        culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en";

    private static NarrationSettings Load(ISettingsStore store)
    {
        try
        {
            return store.Get(SettingKeys.Narration) is { } json
                ? JsonSerializer.Deserialize(json, BookJson.Default.NarrationSettings) ?? new NarrationSettings()
                : new NarrationSettings();
        }
        catch (JsonException)
        {
            return new NarrationSettings();
        }
    }

    private void StopPlayback()
    {
        if (_playingAudio)
        {
            _audio.Stop();
        }

        _playingAudio = false;
        _silent.Reset();
    }

    private void OnAudioFailed(object? sender, EventArgs e)
    {
        Trace.TraceWarning($"Narration audio {_clip?.Key} failed to play; showing subtitles only.");
        if (_playingAudio)
        {
            SwitchToSilent(0);
        }
    }

    private void SwitchToSilent(double seconds)
    {
        _audio.Stop();
        _playingAudio = false;
        _silent.Restart();

        // Stopwatch cannot start part-way, so the clip is shifted back by what was already heard.
        if (_clip is not null && seconds > 0)
        {
            _clip = _clip with
            {
                Duration = Math.Max(0, _clip.Duration - seconds),
                Sentences = _clip.Sentences.Select(s => s with { Start = s.Start - seconds, End = s.End - seconds }).ToList(),
            };
        }

        if (IsPaused)
        {
            _silent.Stop();
        }
    }
}
