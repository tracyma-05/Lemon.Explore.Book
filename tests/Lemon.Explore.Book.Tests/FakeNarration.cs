using System.Globalization;
using Lemon.Book.Shell.Explore.Narration;

namespace Lemon.Explore.Book.Tests;

/// <summary>A narration channel whose clip ends only when <see cref="Finish"/> is called.</summary>
internal sealed class FakeNarration : INarrationService
{
    private bool _finished = true;

    public NarrationSettings Settings { get; private set; } = new();

    public event EventHandler? SettingsChanged;

    public NarrationClip? Clip { get; private set; }

    public string? CurrentKey => Clip?.Key;

    public object? CurrentOwner { get; private set; }

    public bool IsPaused { get; private set; }

    /// <summary>Seconds into the clip, as a test sets it.</summary>
    public double Position { get; set; }

    public bool IsFinished => Clip is null || _finished;

    public void Update(NarrationSettings settings)
    {
        Settings = settings;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Every clip played, in order.</summary>
    public List<string> Played { get; } = [];

    public NarrationClip Play(string key, object owner, string? text = null)
    {
        Played.Add(key);
        Clip = NarrationClip.Estimate(key, text ?? "Some words. More words.", CultureInfo.InvariantCulture);
        Position = 0;
        CurrentOwner = owner;
        IsPaused = false;
        _finished = false;
        return Clip;
    }

    public void Pause() => IsPaused = true;

    public void Resume() => IsPaused = false;

    public void Stop(object? owner = null)
    {
        if (owner is not null && !ReferenceEquals(owner, CurrentOwner))
        {
            return;
        }

        Clip = null;
        CurrentOwner = null;
    }

    public void Finish() => _finished = true;
}
