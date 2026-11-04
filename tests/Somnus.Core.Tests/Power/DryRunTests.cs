using Microsoft.Extensions.Logging.Abstractions;
using Somnus.Core.Power;

namespace Somnus.Core.Tests.Power;

public sealed class DryRunTests
{
    [Fact]
    public void DebugBuilds_AlwaysSimulate()
    {
        Assert.True(DryRunPolicy.ShouldSimulate(isDebugBuild: true, []));
    }

    [Fact]
    public void ReleaseBuilds_ActForReal_UnlessAskedToSimulate()
    {
        Assert.False(DryRunPolicy.ShouldSimulate(isDebugBuild: false, []));
        Assert.False(DryRunPolicy.ShouldSimulate(isDebugBuild: false, ["--verbose"]));
        Assert.True(DryRunPolicy.ShouldSimulate(isDebugBuild: false, ["--dry-run"]));
        Assert.True(DryRunPolicy.ShouldSimulate(isDebugBuild: false, ["--DRY-RUN"]));
    }

    [Fact]
    public void ShouldSimulate_NullArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DryRunPolicy.ShouldSimulate(false, null!));
    }

    [Fact]
    public void DryRunController_ReportsEveryActionInsteadOfPerformingIt()
    {
        var controller = new DryRunPowerController(NullLogger<DryRunPowerController>.Instance);
        var simulated = new List<(PowerAction Action, bool Force)>();
        controller.Simulated += (_, e) => simulated.Add((e.Action, e.ForceCloseApps));

        controller.Sleep();
        controller.Hibernate();
        controller.Shutdown(forceCloseApps: false);
        controller.Shutdown(forceCloseApps: true);

        Assert.Equal(
            [
                (PowerAction.Sleep, false),
                (PowerAction.Hibernate, false),
                (PowerAction.Shutdown, false),
                (PowerAction.Shutdown, true),
            ],
            simulated);
    }

    [Fact]
    public void DryRunController_WorksWithoutListeners()
    {
        var controller = new DryRunPowerController(NullLogger<DryRunPowerController>.Instance);

        controller.Sleep();
    }

    [Fact]
    public void DryRunController_RequiresALogger()
    {
        Assert.Throws<ArgumentNullException>(() => new DryRunPowerController(null!));
    }
}
