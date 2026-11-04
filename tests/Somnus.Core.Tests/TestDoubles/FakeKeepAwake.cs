using Somnus.Core.Power;

namespace Somnus.Core.Tests.TestDoubles;

/// <summary>
/// Tracks whether keep-awake is held, without touching Windows.
/// </summary>
internal sealed class FakeKeepAwake : IKeepAwake
{
    public bool IsAcquired { get; private set; }

    public int AcquireCount { get; private set; }

    public void Acquire()
    {
        IsAcquired = true;
        AcquireCount++;
    }

    public void Release() => IsAcquired = false;
}
