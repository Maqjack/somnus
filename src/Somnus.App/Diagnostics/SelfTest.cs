using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Somnus.App.Composition;
using Somnus.App.Logging;
using Somnus.App.Services;
using Somnus.App.Shell;
using Somnus.App.Views;
using Somnus.Core.Power;
using Somnus.Core.Settings;
using Somnus.Presentation;
using Somnus.Presentation.Resources;
using Somnus.Presentation.Services;
using Somnus.Presentation.ViewModels;
using Somnus.Windows.Settings;

namespace Somnus.App.Diagnostics;

/// <summary>
/// <c>Somnus.exe --self-test [report.json]</c> (SWPA 3.14): goes through what the finished build loads
/// only when it is used, without interaction. It shows no window, plays no sound and needs no network;
/// power actions are only simulated (rule R-01), a running Somnus is left alone and files stay in a
/// temporary folder. The exit code is 0 or 1, and the report is JSON.
/// </summary>
internal static class SelfTest
{
    /// <summary>The command-line switch; the argument after it, when there is one, is the report file.</summary>
    public const string Switch = "--self-test";

    private static readonly JsonSerializerOptions ReportFormat = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>Tells whether this run is a self-test.</summary>
    /// <param name="arguments">The command-line arguments.</param>
    /// <param name="reportPath">The report file, or <see langword="null"/> for standard output.</param>
    /// <returns><see langword="true"/> for a self-test.</returns>
    public static bool IsRequested(string[] arguments, out string? reportPath)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var index = Array.FindIndex(arguments, argument => argument.Equals(Switch, StringComparison.OrdinalIgnoreCase));
        reportPath = index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        return index >= 0;
    }

    /// <summary>Runs every check on the UI thread and writes the report.</summary>
    /// <param name="dispatcher">The dispatcher of the UI thread.</param>
    /// <param name="reportPath">The report file, or <see langword="null"/> for standard output.</param>
    /// <returns>The exit code: 0 when every check passed, otherwise 1.</returns>
    public static int Run(Dispatcher dispatcher, string? reportPath)
    {
        var workDirectory = Directory.CreateTempSubdirectory("Somnus-self-test-").FullName;
        ServiceProvider? services = null;
        ServiceProvider built() => services ?? throw new InvalidOperationException("The services were not built.");

        try
        {
            var results = new[]
            {
                Check("resources", CheckResources),
                Check("lullaby", CheckLullaby),
                Check("installer-language", () => InstallerLanguage.Read(AppContext.BaseDirectory)),
                Check("settings", () => CheckSettings(workDirectory)),
                Check("logging", () => CheckLogging(workDirectory)),
                Check("composition", () => services = BuildServices(dispatcher, workDirectory)),
                Check("power", () => CheckPower(built())),
                Check("windows", () => CheckWindows(built())),
            };

            var passed = Array.TrueForAll(results, result => result.Passed);
            WriteReport(new Report("Somnus", Version(), RuntimeInformation.FrameworkDescription, passed, results), reportPath);
            return passed ? 0 : 1;
        }
        finally
        {
            services?.Dispose();
            Directory.Delete(workDirectory, recursive: true);
        }
    }

    private static CheckResult Check(string name, Action check)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            check();
            return new CheckResult(name, true, watch.ElapsedMilliseconds, null);
        }
#pragma warning disable CA1031 // The report has to name any failure of the build under test.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return new CheckResult(name, false, watch.ElapsedMilliseconds, exception.ToString());
        }
    }

    private static string Version() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    /// <summary>Both languages load, including the Polish satellite assembly.</summary>
    private static void CheckResources()
    {
        var polish = Strings.ResourceManager.GetResourceSet(CultureInfo.GetCultureInfo("pl"), createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException("The Polish resources are missing.");

        if (polish.GetString(nameof(Strings.Action_Sleep)) == Strings.ResourceManager.GetString(nameof(Strings.Action_Sleep), CultureInfo.InvariantCulture))
        {
            throw new InvalidOperationException("The Polish resources are not translated.");
        }
    }

    /// <summary>The embedded recording opens and <see cref="SoundPlayer"/> reads it; nothing is played.</summary>
    private static void CheckLullaby()
    {
        using var recording = SoundPlayerLullaby.OpenRecording();
        var header = new byte[12];
        recording.ReadExactly(header);
        if (Encoding.ASCII.GetString(header, 8, 4) != "WAVE")
        {
            throw new InvalidOperationException("The lullaby recording is not a WAV file.");
        }

        recording.Position = 0;
        using var player = new SoundPlayer(recording);
        player.Load();
    }

    private static void CheckSettings(string workDirectory)
    {
        var store = new JsonFileSettingsStore(Path.Combine(workDirectory, "settings"), NullLogger<JsonFileSettingsStore>.Instance);
        var saved = AppSettings.Default with { Sound = EndSound.File, SoundFile = @"C:\Music\Morning.mp3" };

        store.Save(saved);

        if (store.Load() != saved)
        {
            throw new InvalidOperationException("The settings changed between saving and loading.");
        }
    }

    private static void CheckLogging(string workDirectory)
    {
        var directory = Path.Combine(workDirectory, "logs");
        using (var logger = LogFiles.CreateLogger(directory, verbose: false))
        {
            logger.Information("Self-test");
        }

        if (Directory.GetFiles(directory, "somnus-*.log").Length != 1)
        {
            throw new InvalidOperationException("The log file was not written.");
        }
    }

    /// <summary>The whole app is wired together, tray icon included, with simulated power actions only.</summary>
    private static ServiceProvider BuildServices(Dispatcher dispatcher, string workDirectory)
    {
        var services = CompositionRoot.Build(new AppRunOptions(IsDryRun: true), workDirectory, dispatcher, static _ => { });
        if (services.GetRequiredService<IPowerController>() is not DryRunPowerController)
        {
            throw new InvalidOperationException("The self-test must never get the real power controller (rule R-01).");
        }

        services.GetRequiredService<TrayIcon>();
        services.GetRequiredService<LullabyCoordinator>();
        services.GetRequiredService<ShellController>();
        return services;
    }

    /// <summary>The Windows power functions load; a query and a released keep-awake request change nothing.</summary>
    private static void CheckPower(ServiceProvider services)
    {
        services.GetRequiredService<IPowerCapabilities>().Query();
        var keepAwake = services.GetRequiredService<IKeepAwake>();
        keepAwake.Acquire();
        keepAwake.Release();
    }

    /// <summary>
    /// Every window loads its XAML, gets its native handle (which applies the dark title bar) and renders
    /// in the theme. The windows are never shown.
    /// </summary>
    private static void CheckWindows(ServiceProvider services)
    {
        var warning = services.GetRequiredService<ShellViewModel>().Warning;
        Window[] windows =
        [
            services.GetRequiredService<MainWindow>(),
            new WarningWindow(warning),
            new ConfirmDialog(new ConfirmationRequest("Self-test", "Self-test", "Yes", "No")),
            new MessageDialog("Self-test", "Self-test"),
        ];

        foreach (var window in windows)
        {
            new WindowInteropHelper(window).EnsureHandle();
            OffscreenRenderer.Render(OffscreenRenderer.LayOut(window));
        }
    }

    private static void WriteReport(Report report, string? reportPath)
    {
        var json = JsonSerializer.Serialize(report, ReportFormat);
        if (reportPath is null)
        {
            Console.Out.WriteLine(json);
        }
        else
        {
            File.WriteAllText(reportPath, json);
        }
    }

    private sealed record CheckResult(string Name, bool Passed, long Milliseconds, string? Error);

    private sealed record Report(string App, string Version, string Runtime, bool Passed, IReadOnlyList<CheckResult> Checks);
}
