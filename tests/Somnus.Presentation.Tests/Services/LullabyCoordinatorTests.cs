using Somnus.Core.Countdown;
using Somnus.Core.Power;
using Somnus.Core.Settings;
using Somnus.Presentation.Services;
using Somnus.Presentation.Tests.TestDoubles;

namespace Somnus.Presentation.Tests.Services;

/// <summary>Issue #59: the lullaby plays in the last 20 seconds and stops as soon as the countdown does.</summary>
public sealed class LullabyCoordinatorTests : IDisposable
{
    private readonly TestHarness _harness = new(AppSettings.Default with { Sound = EndSound.Lullaby });
    private readonly FakeLullabyPlayer _player = new();
    private readonly LullabyCoordinator _coordinator;

    public LullabyCoordinatorTests()
    {
        _coordinator = new LullabyCoordinator(_harness.Session, _harness.Settings, _player, _harness.Dispatcher);
    }

    public void Dispose()
    {
        _coordinator.Dispose();
        _harness.Dispose();
    }

    [Fact]
    public void Plays_FromTwentySecondsBeforeTheAction()
    {
        Start(TimeSpan.FromMinutes(2));

        _harness.AdvanceInTicks(TimeSpan.FromSeconds(99));
        Assert.Equal(0, _player.Plays);

        _harness.AdvanceInTicks(TimeSpan.FromSeconds(1));
        Assert.Equal(1, _player.Plays);
        Assert.True(_player.IsPlaying);

        _harness.AdvanceInTicks(TimeSpan.FromSeconds(10));
        Assert.Equal(1, _player.Plays);
    }

    [Fact]
    public void ReachingTheAction_StopsTheLullaby()
    {
        Start(TimeSpan.FromSeconds(30), PowerAction.Shutdown);

        _harness.AdvanceInTicks(TimeSpan.FromSeconds(30));

        Assert.Equal(1, _player.Plays);
        Assert.False(_player.IsPlaying);
        Assert.Equal(CountdownState.Done, _harness.Session.Snapshot.State);
    }

    [Fact]
    public void Cancelling_StopsTheLullaby()
    {
        Start(TimeSpan.FromSeconds(15));
        Assert.True(_player.IsPlaying);

        _harness.Session.Cancel();

        Assert.False(_player.IsPlaying);
    }

    [Fact]
    public void DoItNow_StopsTheLullaby()
    {
        Start(TimeSpan.FromSeconds(15));

        _harness.Session.ExecuteNow();

        Assert.False(_player.IsPlaying);
    }

    [Fact]
    public void AMissedDeadline_StopsTheLullaby()
    {
        Start(TimeSpan.FromSeconds(15));

        _harness.Time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(CancellationReason.DeadlineMissed, _harness.Session.Snapshot.CancellationReason);
        Assert.False(_player.IsPlaying);
    }

    [Fact]
    public void WithTheSettingOff_StaysSilent()
    {
        _harness.Settings.Update(settings => settings with { Sound = EndSound.None });
        Start(TimeSpan.FromSeconds(15));

        _harness.AdvanceInTicks(TimeSpan.FromSeconds(15));

        Assert.Equal(0, _player.Plays);
    }

    [Fact]
    public void TurningTheSettingOff_StopsAPlayingLullaby()
    {
        Start(TimeSpan.FromSeconds(15));

        _harness.Settings.Update(settings => settings with { Sound = EndSound.None });
        _harness.AdvanceInTicks(TimeSpan.FromSeconds(1));

        Assert.False(_player.IsPlaying);
    }

    [Fact]
    public void TheLullaby_IsPlayedWithoutAFile()
    {
        Start(TimeSpan.FromSeconds(15));

        Assert.Equal(1, _player.Plays);
        Assert.Null(_player.LastFile);
    }

    [Fact]
    public void TheUsersFile_IsPlayedInsteadOfTheLullaby()
    {
        // Mac's feedback of 2026-11-04: any sound file can replace the lullaby.
        _harness.Settings.Update(settings => settings with { Sound = EndSound.File, SoundFile = @"C:\Music\Morning.mp3" });

        Start(TimeSpan.FromSeconds(15));

        Assert.Equal(@"C:\Music\Morning.mp3", _player.LastFile);
    }

    [Fact]
    public void Dispose_StopsTheLullaby()
    {
        Start(TimeSpan.FromSeconds(15));

        _coordinator.Dispose();

        Assert.False(_player.IsPlaying);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        var h = _harness;
        Assert.Throws<ArgumentNullException>(() => new LullabyCoordinator(null!, h.Settings, _player, h.Dispatcher));
        Assert.Throws<ArgumentNullException>(() => new LullabyCoordinator(h.Session, null!, _player, h.Dispatcher));
        Assert.Throws<ArgumentNullException>(() => new LullabyCoordinator(h.Session, h.Settings, null!, h.Dispatcher));
        Assert.Throws<ArgumentNullException>(() => new LullabyCoordinator(h.Session, h.Settings, _player, null!));
    }

    private void Start(TimeSpan delay, PowerAction action = PowerAction.Sleep) =>
        _harness.Session.Start(new CountdownPlan(_harness.Time.GetUtcNow() + delay, action, ForceCloseApps: false, WarningEnabled: true));

    private sealed class FakeLullabyPlayer : ILullabyPlayer
    {
        public int Plays { get; private set; }

        public bool IsPlaying { get; private set; }

        public string? LastFile { get; private set; }

        public void Play(string? soundFile)
        {
            Plays++;
            IsPlaying = true;
            LastFile = soundFile;
        }

        public void Silence() => IsPlaying = false;
    }
}
