namespace Somnus.Core.Power;

/// <summary>
/// Keeps Windows from going to sleep on its own while a countdown runs, so that idle sleep
/// cannot interrupt a scheduled action (rule R-06). The display may still turn off.
/// </summary>
/// <remarks>
/// Implementations must be idempotent and callable from any thread, and must not throw:
/// a failure to keep the PC awake is logged, and the countdown goes on without it.
/// </remarks>
public interface IKeepAwake
{
    /// <summary>Asks Windows to stay awake. Does nothing if already acquired.</summary>
    void Acquire();

    /// <summary>Lets Windows sleep on its own again. Does nothing if not acquired.</summary>
    void Release();
}
