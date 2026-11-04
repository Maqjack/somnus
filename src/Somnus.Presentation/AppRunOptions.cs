namespace Somnus.Presentation;

/// <summary>
/// How this instance of Somnus was started.
/// </summary>
/// <param name="IsDryRun">Power actions are only simulated (rule R-01).</param>
public sealed record AppRunOptions(bool IsDryRun);
