using Somnus.Core.Countdown;
using Somnus.Core.Power;
using Somnus.Core.Settings;
using Somnus.Core.Tests.TestDoubles;
using Somnus.Presentation.Formatting;
using Somnus.Presentation.Localization;
using Somnus.Presentation.Resources;

namespace Somnus.Presentation.Tests;

public sealed class LocalizerTests
{
    [Fact]
    public void StartsInEnglish()
    {
        var localizer = new Localizer();

        Assert.Equal(AppLanguage.English, localizer.Language);
        Assert.Equal("Start", localizer[nameof(Strings.Main_Start)]);
    }

    [Fact]
    public void MissingKey_IsVisibleInsteadOfFailing()
    {
        Assert.Equal("[No_Such_Key]", new Localizer()["No_Such_Key"]);
    }

    [Fact]
    public void Format_FillsThePlaceholders()
    {
        Assert.Equal("The PC will shut down at 07:15 (Fri)", new Localizer().Format(nameof(Strings.Main_AtPreviewShutdown), "Fri", "07:15"));
    }

    [Fact]
    public void LanguageChange_NotifiesBindingsOnce()
    {
        var localizer = new Localizer();
        var properties = new List<string?>();
        var languageChanges = 0;
        localizer.PropertyChanged += (_, e) => properties.Add(e.PropertyName);
        localizer.LanguageChanged += (_, _) => languageChanges++;

        localizer.Language = AppLanguage.Polish;
        localizer.Language = AppLanguage.Polish;

        Assert.Equal(AppLanguage.Polish, localizer.Language);
        Assert.Equal("pl", localizer.Culture.Name);
        Assert.Contains("Item[]", properties);
        Assert.Equal(1, languageChanges);
    }

    [Theory]
    [InlineData(nameof(Strings.Tray_Cancel), "Zatrzymaj odliczanie")]
    [InlineData(nameof(Strings.Action_Shutdown), "Wyłącz")]
    [InlineData(nameof(Strings.Main_ModeIn), "Odliczanie")]
    [InlineData(nameof(Strings.Main_ModeAt), "O której")]
    [InlineData(nameof(Strings.Main_ForceCloseApps), "Wymuś zamknięcie programów")]
    public void Polish_IsLoadedFromTheSatelliteAssembly(string key, string expected)
    {
        var localizer = new Localizer { Language = AppLanguage.Polish };

        Assert.Equal(expected, localizer[key]);
    }

    [Fact]
    public void Polish_FormatsDatesInPolish()
    {
        var localizer = new Localizer { Language = AppLanguage.Polish };
        var target = new DateTimeOffset(2026, 10, 22, 21, 30, 0, TimeSpan.Zero);

        var text = CountdownFormat.Target(localizer, PowerAction.Sleep, target, TestTimeZones.CentralEurope);

        Assert.StartsWith("Uśpienie czw", text, StringComparison.Ordinal);
        Assert.EndsWith("o 23:30", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedInstance_IsReused()
    {
        Assert.Same(Localizer.Instance, Localizer.Instance);
    }
}

public sealed class CountdownFormatTests
{
    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(59, "00:00:59")]
    [InlineData(3_661, "01:01:01")]
    [InlineData(356_400, "99:00:00")]
    [InlineData(-5, "00:00:00")]
    public void Remaining_IsHoursMinutesSeconds(long seconds, string expected)
    {
        Assert.Equal(expected, CountdownFormat.Remaining(seconds));
    }

    [Theory]
    [InlineData(60, "01:00")]
    [InlineData(45, "00:45")]
    [InlineData(-1, "00:00")]
    public void RemainingShort_IsMinutesSeconds(long seconds, string expected)
    {
        Assert.Equal(expected, CountdownFormat.RemainingShort(seconds));
    }

    [Theory]
    [InlineData(PowerAction.Sleep, "Sleep on Thu, 22 Oct at 23:30")]
    [InlineData(PowerAction.Hibernate, "Hibernate on Thu, 22 Oct at 23:30")]
    [InlineData(PowerAction.Shutdown, "Shut down on Thu, 22 Oct at 23:30")]
    public void Target_NamesActionDateAndTime(PowerAction action, string expected)
    {
        var target = new DateTimeOffset(2026, 10, 22, 21, 30, 0, TimeSpan.Zero);

        Assert.Equal(expected, CountdownFormat.Target(new Localizer(), action, target, TestTimeZones.CentralEurope));
    }

    [Fact]
    public void LocalDateAndTime_UsesTheZoneClock()
    {
        var instant = new DateTimeOffset(2026, 10, 22, 22, 15, 0, TimeSpan.Zero);

        var (date, time) = CountdownFormat.LocalDateAndTime(new Localizer(), instant, TestTimeZones.CentralEurope);

        Assert.Equal("Fri, 23 Oct", date);
        Assert.Equal("00:15", time);
    }

    [Theory]
    [InlineData(PowerAction.Sleep, "Sleep")]
    [InlineData(PowerAction.Hibernate, "Hibernate")]
    [InlineData(PowerAction.Shutdown, "Shut down")]
    public void ActionName_IsLocalized(PowerAction action, string expected)
    {
        Assert.Equal(expected, CountdownFormat.ActionName(new Localizer(), action));
    }

    [Theory]
    [InlineData(CountdownState.Cancelled, CancellationReason.DeadlineMissed, PowerAction.Shutdown, nameof(Strings.Status_DeadlineMissed))]
    [InlineData(CountdownState.Failed, CancellationReason.None, PowerAction.Sleep, nameof(Strings.Status_FailedSleep))]
    [InlineData(CountdownState.Failed, CancellationReason.None, PowerAction.Hibernate, nameof(Strings.Status_FailedHibernate))]
    [InlineData(CountdownState.Failed, CancellationReason.None, PowerAction.Shutdown, nameof(Strings.Status_FailedShutdown))]
    [InlineData(CountdownState.Cancelled, CancellationReason.User, PowerAction.Sleep, null)]
    [InlineData(CountdownState.Done, CancellationReason.None, PowerAction.Sleep, null)]
    [InlineData(CountdownState.Running, CancellationReason.None, PowerAction.Sleep, null)]
    public void OutcomeMessageKey_NamesOnlyCountdownsThatEndedWithoutTheirAction(
        CountdownState state, CancellationReason reason, PowerAction action, string? expected)
    {
        var plan = new CountdownPlan(DateTimeOffset.UnixEpoch, action, ForceCloseApps: false, WarningEnabled: true);
        var snapshot = new CountdownSnapshot(state, plan, TimeSpan.Zero, reason);

        Assert.Equal(expected, CountdownFormat.OutcomeMessageKey(snapshot));
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => CountdownFormat.LocalDateAndTime(null!, DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc));
        Assert.Throws<ArgumentNullException>(() => CountdownFormat.LocalDateAndTime(new Localizer(), DateTimeOffset.UnixEpoch, null!));
        Assert.Throws<ArgumentNullException>(() => CountdownFormat.ActionName(null!, PowerAction.Sleep));
        Assert.Throws<ArgumentNullException>(() => CountdownFormat.OutcomeMessageKey(null!));
    }
}
