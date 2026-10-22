using System.Globalization;
using Avalonia.Headless.XUnit;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Explore.Lessons;
using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Xunit;

namespace Lemon.Explore.Book.Tests;

/// <summary>The spoken walkthrough player, driven by hand with a narration channel whose clips end when told to.</summary>
public sealed class LessonPlayerTests
{
    [Fact]
    public void Start_SetsTheSceneSpeaksAndPointsAtTheFirstStep()
    {
        var narration = new FakeNarration();
        var entered = 0;
        var player = Player(narration, new LessonStep("Narr_A") { Enter = () => entered++, Highlight = "earth" }, new LessonStep("Narr_B"));

        player.Start();

        Assert.Equal(1, entered);
        Assert.Equal(LessonState.Speaking, player.State);
        Assert.Equal("Narr_A", narration.CurrentKey);
        Assert.Equal("earth", player.Highlight);
        Assert.Equal(2, player.Count);
    }

    [Fact]
    public void AClipEnding_MovesToTheNextStep_AndTheLastOneEnds()
    {
        var narration = new FakeNarration();
        var player = Player(narration, new LessonStep("Narr_A") { Highlight = "earth" }, new LessonStep("Narr_B"));
        player.Start();

        narration.Finish();
        player.Tick(0.1); // clip over: the gap starts
        player.Tick(0.1); // gap over
        Assert.Equal(1, player.Index);
        Assert.Equal("Narr_B", narration.CurrentKey);
        Assert.Null(player.Highlight);

        narration.Finish();
        player.Tick(0.1);
        player.Tick(0.1);
        Assert.Equal(LessonState.Done, player.State);
        Assert.Null(narration.CurrentKey);
    }

    [Fact]
    public void AQuestion_WaitsForAnAnswer_ThenSaysWhetherItWasRight()
    {
        var narration = new FakeNarration();
        var player = Player(narration,
            new LessonStep("Narr_Q") { Choices = ["Lesson_Skip", "Lesson_Next"], Answer = 1 },
            new LessonStep("Narr_After"));
        player.Start();
        narration.Finish();
        player.Tick(0.1);

        Assert.Equal(LessonState.Quiz, player.State);
        Assert.Equal(2, player.Choices.Count);
        player.Tick(4);
        Assert.Equal(LessonState.Quiz, player.State);

        player.AnswerCommand.Execute(player.Choices[0]);
        Assert.Equal(LessonState.Feedback, player.State);
        Assert.Equal(LessonPlayer.WrongKey, narration.CurrentKey);
        Assert.True(player.Choices[0].IsWrong);
        Assert.True(player.Choices[1].IsRight);

        narration.Finish();
        player.Tick(0.1);
        player.Tick(0.1);
        Assert.Equal("Narr_After", narration.CurrentKey);
    }

    [Fact]
    public void YourTurn_WaitsUntilDone_ThenPraises()
    {
        var narration = new FakeNarration();
        var done = false;
        var player = Player(narration, new LessonStep("Narr_Do") { TryIt = () => done }, new LessonStep("Narr_After"));
        player.Start();
        narration.Finish();
        player.Tick(0.1);
        Assert.Equal(LessonState.TryIt, player.State);

        player.UserInteracted(); // dragging is the task: no pause
        player.Tick(0.1);
        Assert.Equal(LessonState.TryIt, player.State);

        done = true;
        player.Tick(0.1);
        Assert.Equal(LessonPlayer.NiceKey, narration.CurrentKey);
        narration.Finish();
        player.Tick(0.1);
        player.Tick(0.1);
        Assert.Equal("Narr_After", narration.CurrentKey);
    }

    [Fact]
    public void TheViewerTakingOver_Pauses_AndResumeSaysTheStepAgainIfSomethingElseWasPlayed()
    {
        var narration = new FakeNarration();
        var player = Player(narration, new LessonStep("Narr_A"), new LessonStep("Narr_B"));
        player.Start();

        player.UserInteracted();
        Assert.Equal(LessonState.Paused, player.State);
        Assert.True(narration.IsPaused);

        narration.Play("Narr_Seasons_Obj_Sun", new object()); // the viewer clicked the Sun
        player.PauseResumeCommand.Execute(null);
        Assert.Equal(LessonState.Speaking, player.State);
        Assert.Equal("Narr_A", narration.CurrentKey);
        Assert.Same(player, narration.CurrentOwner);
    }

    [Fact]
    public void AQuestion_LeftUnanswered_ShowsTheRightAnswerAfterFiveSeconds_AndGoesOn()
    {
        var narration = new FakeNarration();
        var player = Player(narration,
            new LessonStep("Narr_Q") { Choices = ["Lesson_Skip", "Lesson_Next"], Answer = 1 },
            new LessonStep("Narr_After"));
        player.Start();
        narration.Finish();
        player.Tick(0.1);
        Assert.True(player.IsWaiting);

        player.Tick(2.5);
        Assert.Equal(0.5, player.WaitProgress, 3);
        player.Tick(2.5);

        Assert.Equal(LessonState.Feedback, player.State);
        Assert.Equal(LessonPlayer.RevealKey, narration.CurrentKey);
        Assert.True(player.Choices[1].IsRight);
        Assert.False(player.Choices[0].IsWrong);
        Assert.Equal(0, player.WaitProgress);

        narration.Finish();
        player.Tick(0.1);
        player.Tick(0.1);
        Assert.Equal("Narr_After", narration.CurrentKey);
    }

    [Fact]
    public void YourTurn_LeftAlone_MovesOn_ButTouchingThePictureGivesMoreTime()
    {
        var narration = new FakeNarration();
        var player = Player(narration, new LessonStep("Narr_Do") { TryIt = () => false }, new LessonStep("Narr_After"));
        player.Start();
        narration.Finish();
        player.Tick(0.1);

        player.Tick(player.TryItSeconds - 1);
        player.UserInteracted(); // still working on it
        player.Tick(player.TryItSeconds - 1);
        Assert.Equal(LessonState.TryIt, player.State);

        player.Tick(1.5);
        Assert.Equal(LessonState.Feedback, player.State);
        Assert.Equal(LessonPlayer.MoveOnKey, narration.CurrentKey);
        narration.Finish();
        player.Tick(0.1);
        player.Tick(0.1);
        Assert.Equal("Narr_After", narration.CurrentKey);
    }

    [Fact]
    public void TheViewerTakingOver_ResumesByItself_OnceTheyLeaveThePictureAlone()
    {
        var narration = new FakeNarration();
        var player = Player(narration, new LessonStep("Narr_A"), new LessonStep("Narr_B"));
        player.Start();

        player.UserInteracted();
        player.Tick(player.ResumeSeconds - 1);
        player.UserInteracted(); // still dragging
        player.Tick(player.ResumeSeconds - 1);
        Assert.Equal(LessonState.Paused, player.State);

        player.Tick(1.5);
        Assert.Equal(LessonState.Speaking, player.State);
        Assert.False(narration.IsPaused);
        Assert.Equal("Narr_A", narration.CurrentKey);
    }

    [Fact]
    public void AClickedExplanation_IsHeardToTheEnd_BeforeTheLessonGoesOn()
    {
        var narration = new FakeNarration();
        var player = Player(narration, new LessonStep("Narr_A"), new LessonStep("Narr_B"));
        player.Start();

        narration.Play("Narr_Seasons_Obj_Sun", new object());
        player.Tick(0.1);
        Assert.Equal(LessonState.Paused, player.State);
        player.Tick(player.ResumeSeconds * 3);
        Assert.Equal(LessonState.Paused, player.State); // the explanation is still playing

        narration.Finish();
        player.Tick(player.ResumeSeconds + 0.1);
        Assert.Equal(LessonState.Speaking, player.State);
        Assert.Equal("Narr_A", narration.CurrentKey);
        Assert.Same(player, narration.CurrentOwner);
    }

    [Fact]
    public void ThePauseButton_StaysPaused()
    {
        var narration = new FakeNarration();
        var player = Player(narration, new LessonStep("Narr_A"));
        player.Start();

        player.PauseResumeCommand.Execute(null);
        player.Tick(60);

        Assert.Equal(LessonState.Paused, player.State);
    }

    [Fact]
    public void Stop_ClearsEverything()
    {
        var narration = new FakeNarration();
        var player = Player(narration, new LessonStep("Narr_A") { Highlight = "sun" });
        player.Start();

        player.StopCommand.Execute(null);

        Assert.Equal(LessonState.Idle, player.State);
        Assert.Null(player.Highlight);
        Assert.Null(narration.CurrentKey);
    }

    [AvaloniaTheory]
    [InlineData(SeasonsMode.Tilt)]
    [InlineData(SeasonsMode.SunPath)]
    [InlineData(SeasonsMode.Distance)]
    public void SeasonsWalkthroughs_RunToTheEnd(SeasonsMode mode)
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("en"));
        try
        {
            var narration = new FakeNarration();
            using var page = new SeasonsViewModel(narration, new MemorySettingsStore()) { Mode = mode };
            var lesson = page.Lesson;
            lesson.GapSeconds = 0;
            var known = new HashSet<string> { "orbit", "axis", "paths", "todaypath", "sun", "earth", "june", "december", "perihelion", "aphelion", "skysun", "noon", "pole" };

            lesson.Start();
            for (var guard = 0; guard < 200 && lesson.State != LessonState.Done; guard++)
            {
                Assert.True(lesson.Highlight is null || known.Contains(lesson.Highlight), lesson.Highlight);
                Assert.Equal(mode, page.Mode);
                if (lesson.State == LessonState.Quiz)
                {
                    lesson.AnswerCommand.Execute(lesson.Choices[0]);
                }
                else if (lesson.State == LessonState.TryIt)
                {
                    page.Day = SeasonsGeometry.EventDay(SeasonEvent.DecemberSolstice);
                }

                narration.Finish();
                lesson.Tick(0.1);
            }

            Assert.Equal(LessonState.Done, lesson.State);
            Assert.Equal(SeasonsGeometry.EarthTilt, page.Tilt);
            lesson.Stop();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }

    [AvaloniaFact]
    public void TurningAwayFromTheChapter_StopsItsWalkthrough()
    {
        var narration = new FakeNarration();
        using var page = new SeasonsViewModel(narration, new MemorySettingsStore());
        page.ViewAttached();
        page.ViewAttached();
        page.Lesson.Start();

        page.ViewDetached();
        Assert.True(page.Lesson.IsActive); // the other page of the spread is still showing

        page.ViewDetached();
        Assert.False(page.Lesson.IsActive);
        Assert.Null(narration.CurrentKey);
    }

    private static LessonPlayer Player(FakeNarration narration, params LessonStep[] steps) =>
        new(narration, () => steps, useTimer: false) { GapSeconds = 0.1 };

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

        public double Position => 0;

        public bool IsFinished => Clip is null || _finished;

        public void Update(NarrationSettings settings)
        {
            Settings = settings;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Every clip played, in order.</summary>
        public List<string> Played { get; } = [];

        public NarrationClip Play(string key, object owner)
        {
            Played.Add(key);
            Clip = NarrationClip.Estimate(key, "Some words. More words.", CultureInfo.InvariantCulture);
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
}
