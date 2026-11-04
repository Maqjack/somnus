namespace Somnus.Core.Scheduling;

/// <summary>
/// How the user says when the action should happen.
/// </summary>
public enum TimeMode
{
    /// <summary>After a delay, for example in 30 minutes.</summary>
    In,

    /// <summary>At a clock time, for example at 23:30.</summary>
    At,
}
