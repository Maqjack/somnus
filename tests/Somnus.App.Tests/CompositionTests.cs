using System.IO;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Somnus.App.Composition;
using Somnus.Core.Power;
using Somnus.Core.Settings;
using Somnus.Presentation;
using Somnus.Windows.Power;
using Somnus.Windows.Settings;

namespace Somnus.App.Tests;

/// <summary>
/// Rule R-01: a dry run must never get the real power controller, and Debug builds always run dry.
/// The real controller is only looked at as a registration, never created.
/// </summary>
[Collection(WpfTestGroup.Name)]
public sealed class CompositionTests(WpfFixture wpf)
{
    [Fact]
    public void DryRun_ResolvesTheSimulatedController()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        CompositionRoot.AddPowerController(services, new AppRunOptions(IsDryRun: true));
        using var provider = services.BuildServiceProvider();

        Assert.IsType<DryRunPowerController>(provider.GetRequiredService<IPowerController>());
    }

    [Fact]
    public void RealRun_RegistersTheWindowsControllerWithoutCreatingIt()
    {
        var services = new ServiceCollection();
        CompositionRoot.AddPowerController(services, new AppRunOptions(IsDryRun: false));

        var registration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IPowerController));
        var type = registration.ImplementationType;
        Assert.NotNull(type);
        Assert.Same(typeof(WindowsKeepAwake).Assembly, type.Assembly);
        Assert.NotEqual(typeof(DryRunPowerController), type);
    }

    [Fact]
    public void DryRun_KeepsStartWithWindowsInMemory()
    {
        // Issue #83 and SWPA: checking Somnus on a real PC must never add it to the startup apps.
        var services = new ServiceCollection();
        services.AddLogging();
        CompositionRoot.AddAutostart(services, new AppRunOptions(IsDryRun: true));
        using var provider = services.BuildServiceProvider();

        Assert.IsType<SimulatedAutostart>(provider.GetRequiredService<IAutostart>());
    }

    [Fact]
    public void RealRun_StartsWithWindowsThroughTheRunKey()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        CompositionRoot.AddAutostart(services, new AppRunOptions(IsDryRun: false));
        using var provider = services.BuildServiceProvider();

        // Creating it reads and writes nothing; only TrySet touches the registry.
        Assert.IsType<RunKeyAutostart>(provider.GetRequiredService<IAutostart>());
    }

    [Theory]
    [InlineData(new[] { "--autostart" }, true)]
    [InlineData(new[] { "--AUTOSTART", "--dry-run" }, true)]
    [InlineData(new[] { "--dry-run" }, false)]
    [InlineData(new string[0], false)]
    public void StartedWithWindows_StaysInTheTray(string[] arguments, bool expected)
    {
        Assert.Equal(expected, App.StartsInTray(arguments));
    }

    [Fact]
    public void WholeComposition_IsValidAndUsesTheSimulatedControllerInADryRun()
    {
        // ValidateOnBuild checks that every service can be created, without creating them.
        wpf.Run(() =>
        {
            var dataDirectory = Path.Combine(Path.GetTempPath(), "somnus-tests", Guid.NewGuid().ToString("N"));
            using var services = CompositionRoot.Build(new AppRunOptions(IsDryRun: true), dataDirectory, Dispatcher.CurrentDispatcher, _ => { });

            Assert.IsType<DryRunPowerController>(services.GetRequiredService<IPowerController>());
        });
    }

    [Fact]
    public void RunOptions_DebugBuildsAlwaysSimulate_ReleaseBuildsOnlyWithTheSwitch()
    {
#if DEBUG
        Assert.True(App.RunOptionsFor([]).IsDryRun);
#else
        Assert.False(App.RunOptionsFor([]).IsDryRun);
#endif
        Assert.True(App.RunOptionsFor(["--dry-run"]).IsDryRun);
        Assert.True(App.RunOptionsFor(["--DRY-RUN"]).IsDryRun);
    }
}
