using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Somnus.Core.Countdown;
using Somnus.Core.Settings;
using Somnus.Core.Tests.TestDoubles;
using Somnus.Presentation.Localization;
using Somnus.Presentation.Services;
using Somnus.Presentation.ViewModels;

namespace Somnus.Presentation.Tests.TestDoubles;

/// <summary>
/// Everything a view model needs, wired with fakes: a fake clock in Central European time
/// (Thursday 22 October 2026, 18:00), a synchronous countdown and in-memory settings.
/// </summary>
internal sealed class TestHarness : IDisposable
{
    public static readonly DateTimeOffset StartTime = new(2026, 10, 22, 18, 0, 0, TimeSpan.FromHours(2));

    public TestHarness(AppSettings? settings = null)
    {
        Time.SetLocalTimeZone(TestTimeZones.CentralEurope);
        Power = new FakePowerController(Time);
        Store = new InMemorySettingsStore(settings);
        Settings = new SettingsService(Store, NullLogger<SettingsService>.Instance);
        Session = new CountdownSession(Time, Power, KeepAwake, NullLogger<CountdownSession>.Instance, RunSynchronously);
        Schedule = new ScheduleCoordinator(Session, Settings, Time, Dispatcher);
    }

    public FakeTimeProvider Time { get; } = new(StartTime);

    public FakePowerController Power { get; }

    public FakeKeepAwake KeepAwake { get; } = new();

    public FakePowerCapabilities Capabilities { get; } = new();

    public FakeAutostart Autostart { get; } = new();

    public InMemorySettingsStore Store { get; }

    public SettingsService Settings { get; }

    public Localizer Localizer { get; } = new();

    public FakeDialogService Dialogs { get; } = new();

    public FakeNotificationService Notifications { get; } = new();

    public ImmediateDispatcher Dispatcher { get; } = new();

    public CountdownSession Session { get; }

    public ScheduleCoordinator Schedule { get; }

    public MainViewModel CreateMain(bool dryRun = false) =>
        new(Session, Capabilities, Settings, Autostart, Schedule, Time, Localizer, Dialogs, Dispatcher, new AppRunOptions(dryRun));

    public ShellViewModel CreateShell() =>
        new(Session, Localizer, Dialogs, Notifications, Dispatcher, Time);

    /// <summary>Moves the fake clock forward one countdown tick at a time.</summary>
    public void AdvanceInTicks(TimeSpan duration)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += CountdownSession.TickInterval)
        {
            Time.Advance(CountdownSession.TickInterval);
        }
    }

    public void Dispose()
    {
        Schedule.Dispose();
        Session.Dispose();
    }

    private static Task RunSynchronously(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
