using System.IO;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Somnus.App.Services;
using Somnus.App.Shell;
using Somnus.App.Views;
using Somnus.Core.Countdown;
using Somnus.Core.Power;
using Somnus.Core.Settings;
using Somnus.Presentation;
using Somnus.Presentation.Localization;
using Somnus.Presentation.Services;
using Somnus.Presentation.ViewModels;
using Somnus.Windows.Power;
using Somnus.Windows.Settings;

namespace Somnus.App.Composition;

/// <summary>
/// The single place that wires Somnus together. It is also the only place allowed to create the
/// real power controller (rule R-01, checked in CI).
/// </summary>
internal static class CompositionRoot
{
    /// <summary>
    /// Builds the service provider.
    /// </summary>
    /// <param name="runOptions">Whether power actions are only simulated.</param>
    /// <param name="dataDirectory">The folder for settings and logs, normally <c>%APPDATA%\Somnus</c>.</param>
    /// <param name="dispatcher">The dispatcher of the UI thread.</param>
    /// <param name="configureLogging">Adds the logging providers.</param>
    /// <returns>The validated service provider.</returns>
    public static ServiceProvider Build(
        AppRunOptions runOptions,
        string dataDirectory,
        Dispatcher dispatcher,
        Action<ILoggingBuilder> configureLogging)
    {
        var services = new ServiceCollection();
        services.AddLogging(configureLogging);
        services.AddSingleton(runOptions);
        services.AddSingleton(TimeProvider.System);

        // Windows adapters. Until the user picks a language, Somnus speaks the installer's (#53).
        var firstStart = InstallerLanguage.Read(AppContext.BaseDirectory) is { } installerLanguage
            ? AppSettings.Default with { Language = installerLanguage }
            : AppSettings.Default;
        services.AddSingleton<ISettingsStore>(provider =>
            new JsonFileSettingsStore(dataDirectory, provider.GetRequiredService<ILogger<JsonFileSettingsStore>>(), firstStart));
        services.AddSingleton<IPowerCapabilities, WindowsPowerCapabilities>();
        services.AddSingleton<IKeepAwake, WindowsKeepAwake>();
        AddPowerController(services, runOptions);
        AddAutostart(services, runOptions);

        // Core and presentation
        services.AddSingleton<CountdownSession>();
        services.AddSingleton(Localizer.Instance);
        services.AddSingleton<SettingsService>();
        services.AddSingleton<IUiDispatcher>(new WpfUiDispatcher(dispatcher));
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<TrayIcon>();
        services.AddSingleton<INotificationService>(provider => provider.GetRequiredService<TrayIcon>());
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<ILullabyPlayer, SoundPlayerLullaby>();
        services.AddSingleton<LullabyCoordinator>();
        services.AddSingleton<ScheduleCoordinator>();

        // Views
        services.AddSingleton<MainWindow>();
        services.AddSingleton<ShellController>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>
    /// Registers the power controller: the simulated one for a dry run (rule R-01), otherwise the
    /// real one. Tests check this choice without ever creating the real controller.
    /// </summary>
    /// <param name="services">The services to add to.</param>
    /// <param name="runOptions">Whether power actions are only simulated.</param>
    internal static void AddPowerController(IServiceCollection services, AppRunOptions runOptions)
    {
        if (runOptions.IsDryRun)
        {
            services.AddSingleton<DryRunPowerController>();
            services.AddSingleton<IPowerController>(provider => provider.GetRequiredService<DryRunPowerController>());
        }
        else
        {
            services.AddSingleton<IPowerController, WindowsPowerController>();
        }
    }

    /// <summary>
    /// Registers starting with Windows (#83): only in memory for a dry run, so checking Somnus never
    /// adds it to the startup apps of a real PC, otherwise the user's Run key.
    /// </summary>
    /// <param name="services">The services to add to.</param>
    /// <param name="runOptions">Whether this is a dry run.</param>
    internal static void AddAutostart(IServiceCollection services, AppRunOptions runOptions)
    {
        if (runOptions.IsDryRun)
        {
            services.AddSingleton<IAutostart, SimulatedAutostart>();
        }
        else
        {
            services.AddSingleton<IAutostart>(provider => new RunKeyAutostart(
                Environment.ProcessPath ?? Path.Join(AppContext.BaseDirectory, "Somnus.exe"),
                provider.GetRequiredService<ILogger<RunKeyAutostart>>()));
        }
    }
}
