using Somnus.Core.Power;

namespace Somnus.Core.Tests.TestDoubles;

/// <summary>One call made to <see cref="FakePowerController"/>.</summary>
internal sealed record PowerCall(PowerAction Action, bool ForceCloseApps, DateTimeOffset At);

/// <summary>
/// Records power actions instead of performing them, and can be told to fail.
/// </summary>
internal sealed class FakePowerController(TimeProvider timeProvider) : IPowerController
{
    public List<PowerCall> Calls { get; } = [];

    public Exception? FailWith { get; set; }

    public void Sleep() => Record(PowerAction.Sleep, forceCloseApps: false);

    public void Hibernate() => Record(PowerAction.Hibernate, forceCloseApps: false);

    public void Shutdown(bool forceCloseApps) => Record(PowerAction.Shutdown, forceCloseApps);

    private void Record(PowerAction action, bool forceCloseApps)
    {
        Calls.Add(new PowerCall(action, forceCloseApps, timeProvider.GetUtcNow()));
        if (FailWith is not null)
        {
            throw FailWith;
        }
    }
}
