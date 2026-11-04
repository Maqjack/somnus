using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Somnus.Core.Countdown;
using Somnus.Core.Power;
using Somnus.Presentation.Formatting;
using Somnus.Presentation.Localization;
using Somnus.Presentation.Resources;

namespace Somnus.Presentation.ViewModels;

/// <summary>
/// The warning window shown during the last 60 seconds: what will happen, how soon, and the
/// choice to cancel or to do it now.
/// </summary>
public sealed partial class WarningViewModel : ObservableObject
{
    private readonly CountdownSession _session;
    private readonly Localizer _localizer;
    private CountdownSnapshot _snapshot = CountdownSnapshot.Idle;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="session">The countdown to cancel or to finish now.</param>
    /// <param name="localizer">The interface texts.</param>
    public WarningViewModel(CountdownSession session, Localizer localizer)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(localizer);
        _session = session;
        _localizer = localizer;
    }

    /// <summary>Gets the heading, for example "The PC will go to sleep in".</summary>
    public string Heading => _localizer[_snapshot.Plan?.Action switch
    {
        PowerAction.Hibernate => nameof(Strings.Warning_HeadingHibernate),
        PowerAction.Shutdown => nameof(Strings.Warning_HeadingShutdown),
        _ => nameof(Strings.Warning_HeadingSleep),
    }];

    /// <summary>Gets the remaining time as MM:SS.</summary>
    public string RemainingText => CountdownFormat.RemainingShort(_snapshot.RemainingWholeSeconds);

    /// <summary>Gets a value indicating whether the data-loss warning is shown (rule R-05).</summary>
    public bool IsForceWarningVisible => _snapshot.Plan is { Action: PowerAction.Shutdown, ForceCloseApps: true };

    /// <summary>
    /// Updates the window from the countdown. Called on the UI thread.
    /// </summary>
    /// <param name="snapshot">The current state of the countdown.</param>
    public void Update(CountdownSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshot = snapshot;
        OnPropertyChanged(nameof(Heading));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(IsForceWarningVisible));
    }

    /// <summary>Refreshes the texts after a language change.</summary>
    public void RefreshTexts() => Update(_snapshot);

    [RelayCommand]
    private void Cancel() => _session.Cancel();

    [RelayCommand]
    private void DoItNow() => _session.ExecuteNow();
}
