using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Somnus.Core.Countdown;
using Somnus.Core.Power;
using Somnus.Core.Scheduling;
using Somnus.Core.Tests.TestDoubles;
using Somnus.Presentation.Localization;
using Somnus.Presentation.Services;
using Somnus.Presentation.Tests.TestDoubles;
using Somnus.Presentation.ViewModels;
using Somnus.Windows.Settings;
using Somnus.Windows.Tests.TestDoubles;

namespace Somnus.App.Tests;

/// <summary>
/// #125: a schedule that forces apps to close still does so after Somnus starts again, through the
/// real settings file and the dry-run power controller, so no power action is real (R-01).
/// </summary>
public sealed class ForcedScheduleRestartTests : IDisposable
{
    private static readonly DateTimeOffset StartTime = new(2026, 11, 5, 23, 0, 0, TimeSpan.FromHours(1));

    private readonly string _folder = Directory.CreateTempSubdirectory("somnus-force-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void AForcedSchedule_ForcesAppsToCloseAfterSomnusStartsAgain()
    {
        var time = new FakeTimeProvider(StartTime);
        time.SetLocalTimeZone(TestTimeZones.CentralEurope);

        // Somnus starts a forced schedule and ends without Stop, as when Windows shuts down.
        using (var before = new Parts(_folder, time))
        {
            before.Schedule.Begin(CountdownRequest.At(new TimeOnly(23, 30), PowerAction.Shutdown, forceCloseApps: true, WeekDays.EveryDay));
        }

        // Somnus starts again: new objects read the same settings file.
        using var after = new Parts(_folder, time);
        after.Schedule.Resume();
        var target = after.Session.Snapshot.Plan!.Target;
        time.Advance(target - time.GetUtcNow() - TimeSpan.FromMinutes(2));
        AdvanceInTicks(time, TimeSpan.FromSeconds(90));
        Assert.True(after.Shell.IsWarningOpen);
        Assert.True(after.Shell.Warning.IsForceWarningVisible);

        AdvanceInTicks(time, TimeSpan.FromSeconds(31));

        var simulated = Assert.Single(after.PowerLog.Entries, entry => entry.Message.StartsWith("Dry run:", StringComparison.Ordinal));
        Assert.Contains("Dry run: Shutdown simulated (force close apps: True)", simulated.Message, StringComparison.Ordinal);
    }

    private static void AdvanceInTicks(FakeTimeProvider time, TimeSpan duration)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += CountdownSession.TickInterval)
        {
            time.Advance(CountdownSession.TickInterval);
        }
    }

    /// <summary>The parts of Somnus that a schedule goes through, with the real settings file.</summary>
    private sealed class Parts : IDisposable
    {
        public Parts(string folder, FakeTimeProvider time)
        {
            var settings = new SettingsService(new JsonFileSettingsStore(folder, NullLogger<JsonFileSettingsStore>.Instance), NullLogger<SettingsService>.Instance);
            var dispatcher = new ImmediateDispatcher();
            Session = new CountdownSession(time, new DryRunPowerController(PowerLog), new FakeKeepAwake(), NullLogger<CountdownSession>.Instance, RunSynchronously);
            Schedule = new ScheduleCoordinator(Session, settings, time, dispatcher);
            Shell = new ShellViewModel(Session, new Localizer(), new FakeDialogService(), new FakeNotificationService(), dispatcher, time);
        }

        public ListLogger<DryRunPowerController> PowerLog { get; } = new();

        public CountdownSession Session { get; }

        public ScheduleCoordinator Schedule { get; }

        public ShellViewModel Shell { get; }

        public void Dispose()
        {
            Shell.Dispose();
            Schedule.Dispose();
            Session.Dispose();
        }

        private static Task RunSynchronously(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
