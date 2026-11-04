using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Serilog.Extensions.Logging;
using Somnus.App.Behaviors;
using Somnus.App.Composition;
using Somnus.App.Diagnostics;
using Somnus.App.Logging;
using Somnus.App.Shell;
using Somnus.Core.Power;
using Somnus.Presentation;
using Somnus.Presentation.Localization;
using Somnus.Presentation.Resources;
using Somnus.Presentation.Services;
using Somnus.Presentation.ViewModels;
using Somnus.Windows.Instance;
using Somnus.Windows.Settings;

namespace Somnus.App;

/// <summary>
/// Starts Somnus: one instance per user (R-09), file logging and crash handling (SWPA 3.7), the
/// saved language, the main window and the tray icon.
/// </summary>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The application object lives as long as the process; OnExit disposes its fields.")]
internal sealed partial class App
{
    /// <summary>The name of the single-instance mutex and event; the installer checks the same mutex.</summary>
    public const string InstanceName = "Somnus";

#if DEBUG
    private const bool IsDebugBuild = true;
#else
    private const bool IsDebugBuild = false;
#endif

    private Serilog.Core.Logger? _fileLogger;
    private ILogger<App>? _logger;
    private ServiceProvider? _services;
    private SingleInstance? _instance;
    private string _logDirectory = string.Empty;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Before the single-instance check: a self-test must never bring a running Somnus forward.
        if (SelfTest.IsRequested(e.Args, out var reportPath))
        {
            Shutdown(SelfTest.Run(Dispatcher, reportPath));
            return;
        }

        _instance = new SingleInstance(InstanceName);
        if (!_instance.IsPrimary)
        {
            // Somnus is already running: bring that window forward instead of opening a second one.
            _instance.SignalPrimary();
            Shutdown();
            return;
        }

        // Before any window exists: the text follows Settings > Accessibility > Text size (#104).
        TextScale.Apply(Resources, TextScaleSetting.ReadFactor());
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        var dataDirectory = JsonFileSettingsStore.DefaultDirectory();
        _logDirectory = LogFiles.DirectoryIn(dataDirectory);
        _fileLogger = LogFiles.CreateLogger(_logDirectory, verbose: IsDebugBuild);
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var runOptions = RunOptionsFor(e.Args);
        _services = CompositionRoot.Build(
            runOptions,
            dataDirectory,
            Dispatcher,
            logging => logging.AddProvider(new SerilogLoggerProvider(_fileLogger)));
        _logger = _services.GetRequiredService<ILogger<App>>();

        var localizer = Localizer.Instance;
        localizer.LanguageChanged += (_, _) => ApplyCulture(localizer.Culture);
        localizer.Language = _services.GetRequiredService<SettingsService>().Current.Language;
        ApplyCulture(localizer.Culture);

        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var language = localizer.Language.ToString();
        var os = RuntimeInformation.OSDescription;
        var runtime = RuntimeInformation.FrameworkDescription;
        Log.Starting(_logger, version, runOptions.IsDryRun, language, os, runtime);

        var shell = _services.GetRequiredService<ShellViewModel>();
        if (runOptions.IsDryRun)
        {
            _services.GetRequiredService<DryRunPowerController>().Simulated += (_, args) => shell.NotifySimulated(args.Action);
        }

        // The lullaby follows the countdown from now on (#59), and a schedule goes on where it was (#83).
        _services.GetRequiredService<LullabyCoordinator>();
        _services.GetRequiredService<ScheduleCoordinator>().Resume();

        _instance.ListenForActivation(() => Dispatcher.BeginInvoke(() => shell.ShowMainWindowCommand.Execute(null)));
        _services.GetRequiredService<ShellController>().Start(showMainWindow: !StartsInTray(e.Args));

#if DEBUG
        // Issue #22: lets a developer check the crash handling end to end.
        if (e.Args.Contains("--simulate-crash", StringComparer.OrdinalIgnoreCase))
        {
            Dispatcher.BeginInvoke(() => throw new InvalidOperationException("Simulated crash (--simulate-crash)."));
        }
#endif
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        if (_logger is not null)
        {
            Log.Exiting(_logger);
        }

        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

        // Disposing the services releases keep-awake and removes the tray icon (R-06).
        _services?.Dispose();
        _instance?.Dispose();
        _fileLogger?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Decides how this run treats power actions: Debug builds always simulate them, Release builds
    /// only with <c>--dry-run</c> (rule R-01).
    /// </summary>
    /// <param name="arguments">The command-line arguments.</param>
    /// <returns>The run options.</returns>
    internal static AppRunOptions RunOptionsFor(IEnumerable<string> arguments) =>
        new(DryRunPolicy.ShouldSimulate(IsDebugBuild, arguments));

    /// <summary>
    /// Tells whether Windows started Somnus at sign-in (#83): then it waits in the tray, where a
    /// schedule goes on, instead of opening its window.
    /// </summary>
    /// <param name="arguments">The command-line arguments.</param>
    /// <returns><see langword="true"/> to start without the main window.</returns>
    internal static bool StartsInTray(IEnumerable<string> arguments) =>
        arguments.Contains(RunKeyAutostart.Argument, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Windows announces a new Text size as a settings change; the windows follow it at once.
    /// </summary>
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) =>
        Dispatcher.BeginInvoke(() =>
        {
            var factor = TextScaleSetting.ReadFactor();
            if (!factor.Equals(TextScale.Factor))
            {
                TextScale.Apply(Resources, factor);
            }
        });

    private static void ApplyCulture(CultureInfo culture)
    {
        // WPF's own texts (for example the text box context menu) follow the UI culture.
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    /// <summary>An error on the UI thread: log it, explain it, and keep running.</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogUnhandled(e.Exception, fatal: false);
        e.Handled = true;

        var localizer = Localizer.Instance;
        var message = localizer.Format(nameof(Strings.Error_Recoverable), _logDirectory);
        if (_services is not null)
        {
            _services.GetRequiredService<IDialogService>().ShowError(localizer[nameof(Strings.Error_Heading)], message);
        }
        else
        {
            MessageBox.Show(message, localizer[nameof(Strings.App_Name)], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>An error on another thread ends the process: log it and say so in plain words.</summary>
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogUnhandled(e.ExceptionObject as Exception, fatal: true);
        _fileLogger?.Dispose();

        var localizer = Localizer.Instance;
        MessageBox.Show(
            localizer.Format(nameof(Strings.Error_Fatal), _logDirectory),
            localizer[nameof(Strings.App_Name)],
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (_logger is not null)
        {
            Log.UnobservedTask(_logger, e.Exception);
        }

        e.SetObserved();
    }

    private void LogUnhandled(Exception? exception, bool fatal)
    {
        if (_logger is null)
        {
            _fileLogger?.Fatal(exception, "Unhandled error before startup finished");
        }
        else if (fatal)
        {
            Log.UnhandledFatal(_logger, exception);
        }
        else
        {
            Log.UnhandledRecovered(_logger, exception);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information,
            Message = "Somnus {Version} starting (dry run: {DryRun}, language: {Language}, OS: {Os}, runtime: {Runtime})")]
        public static partial void Starting(ILogger logger, string version, bool dryRun, string language, string os, string runtime);

        [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Somnus exiting")]
        public static partial void Exiting(ILogger logger);

        [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Unhandled error on the UI thread; Somnus keeps running")]
        public static partial void UnhandledRecovered(ILogger logger, Exception? exception);

        [LoggerMessage(EventId = 4, Level = LogLevel.Critical, Message = "Unhandled error; Somnus has to close")]
        public static partial void UnhandledFatal(ILogger logger, Exception? exception);

        [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "A background task failed without being observed")]
        public static partial void UnobservedTask(ILogger logger, Exception exception);
    }
}
