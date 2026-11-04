using Microsoft.Extensions.Logging;
using Somnus.Core.Settings;

namespace Somnus.Presentation.Services;

/// <summary>
/// Holds the current settings for the whole app and saves every change.
/// </summary>
public sealed partial class SettingsService
{
    private readonly ISettingsStore _store;
    private readonly ILogger<SettingsService> _logger;

    /// <summary>
    /// Initializes a new instance and loads the settings.
    /// </summary>
    /// <param name="store">Where settings are kept.</param>
    /// <param name="logger">Receives save failures.</param>
    public SettingsService(ISettingsStore store, ILogger<SettingsService> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _logger = logger;
        Current = store.Load();
    }

    /// <summary>Raised after <see cref="Current"/> changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the settings in effect.</summary>
    public AppSettings Current { get; private set; }

    /// <summary>
    /// Applies <paramref name="change"/> and saves the result. The change takes effect even when
    /// saving fails; it then lasts only until Somnus closes.
    /// </summary>
    /// <param name="change">Returns the new settings from the current ones.</param>
    /// <returns><see langword="true"/> if the settings were saved.</returns>
    public bool Update(Func<AppSettings, AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        Current = change(Current);
        Changed?.Invoke(this, EventArgs.Empty);

        try
        {
            _store.Save(Current);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.SaveFailed(_logger, exception);
            return false;
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 400, Level = LogLevel.Warning, Message = "Saving the settings failed; they apply until Somnus closes")]
        public static partial void SaveFailed(ILogger logger, Exception exception);
    }
}
