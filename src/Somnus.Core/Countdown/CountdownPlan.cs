using Somnus.Core.Power;

namespace Somnus.Core.Countdown;

/// <summary>
/// What a <see cref="CountdownSession"/> runs: the target instant and what to do then.
/// </summary>
/// <param name="Target">The absolute instant at which the action runs.</param>
/// <param name="Action">The power action.</param>
/// <param name="ForceCloseApps">Whether a shutdown closes apps without asking (rule R-05).</param>
/// <param name="WarningEnabled">Whether the warning window opens before the action.</param>
/// <param name="Repeats">Whether this is one run of a repeating schedule (#83).</param>
public sealed record CountdownPlan(DateTimeOffset Target, PowerAction Action, bool ForceCloseApps, bool WarningEnabled, bool Repeats = false);
