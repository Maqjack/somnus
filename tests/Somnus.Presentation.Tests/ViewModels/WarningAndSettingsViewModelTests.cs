using Microsoft.Extensions.Logging.Abstractions;
using Somnus.Core.Countdown;
using Somnus.Core.Power;
using Somnus.Core.Settings;
using Somnus.Presentation.Services;
using Somnus.Presentation.Tests.TestDoubles;
using Somnus.Presentation.ViewModels;

namespace Somnus.Presentation.Tests.ViewModels;

public sealed class WarningViewModelTests : IDisposable
{
    private readonly TestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Theory]
    [InlineData(PowerAction.Sleep, "The PC will go to sleep in")]
    [InlineData(PowerAction.Hibernate, "The PC will hibernate in")]
    [InlineData(PowerAction.Shutdown, "The PC will shut down in")]
    public void Heading_NamesTheAction(PowerAction action, string expected)
    {
        var warning = new WarningViewModel(_harness.Session, _harness.Localizer);

        warning.Update(Snapshot(action, forceCloseApps: false, remainingSeconds: 45));

        Assert.Equal(expected, warning.Heading);
        Assert.Equal("00:45", warning.RemainingText);
        Assert.False(warning.IsForceWarningVisible);
    }

    [Fact]
    public void ForcedShutdown_ShowsTheDataLossWarning()
    {
        var warning = new WarningViewModel(_harness.Session, _harness.Localizer);

        warning.Update(Snapshot(PowerAction.Shutdown, forceCloseApps: true, remainingSeconds: 60));

        Assert.True(warning.IsForceWarningVisible);
        Assert.Equal("01:00", warning.RemainingText);
    }

    [Fact]
    public void DoItNow_PerformsTheActionImmediately()
    {
        _harness.Session.Start(new CountdownPlan(_harness.Time.GetUtcNow().AddSeconds(30), PowerAction.Sleep, false, true));
        var warning = new WarningViewModel(_harness.Session, _harness.Localizer);

        warning.DoItNowCommand.Execute(null);

        Assert.Single(_harness.Power.Calls);
        Assert.Equal(CountdownState.Done, _harness.Session.Snapshot.State);
    }

    [Fact]
    public void RefreshTexts_RaisesTheTextProperties()
    {
        var warning = new WarningViewModel(_harness.Session, _harness.Localizer);
        var raised = new List<string?>();
        warning.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        warning.RefreshTexts();

        Assert.Contains(nameof(WarningViewModel.Heading), raised);
        Assert.Contains(nameof(WarningViewModel.RemainingText), raised);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new WarningViewModel(null!, _harness.Localizer));
        Assert.Throws<ArgumentNullException>(() => new WarningViewModel(_harness.Session, null!));
        Assert.Throws<ArgumentNullException>(() => new WarningViewModel(_harness.Session, _harness.Localizer).Update(null!));
    }

    private static CountdownSnapshot Snapshot(PowerAction action, bool forceCloseApps, int remainingSeconds) => new(
        CountdownState.Warning,
        new CountdownPlan(TestHarness.StartTime.AddSeconds(remainingSeconds), action, forceCloseApps, true),
        TimeSpan.FromSeconds(remainingSeconds),
        CancellationReason.None);
}

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly TestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void StartsWithTheCurrentSettings()
    {
        _harness.Settings.Update(settings => settings with { Language = AppLanguage.Polish, WarningEnabled = false });

        var viewModel = new SettingsViewModel(_harness.Settings, _harness.Autostart, _harness.Dialogs, _harness.Localizer);

        Assert.Equal(AppLanguage.Polish, viewModel.Language);
        Assert.False(viewModel.WarningEnabled);
        Assert.True(viewModel.RememberLast);
        Assert.Equal(["English", "Polski"], viewModel.Languages.Select(language => language.DisplayName));
    }

    [Fact]
    public void Save_StoresAppliesTheLanguageAndCloses()
    {
        var viewModel = new SettingsViewModel(_harness.Settings, _harness.Autostart, _harness.Dialogs, _harness.Localizer);
        bool? closedWith = null;
        viewModel.CloseRequested += (_, saved) => closedWith = saved;

        viewModel.Language = AppLanguage.Polish;
        viewModel.WarningEnabled = false;
        viewModel.RememberLast = false;
        viewModel.Sound = EndSound.None;
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(AppLanguage.Polish, _harness.Store.Stored.Language);
        Assert.False(_harness.Store.Stored.WarningEnabled);
        Assert.False(_harness.Store.Stored.RememberLast);
        Assert.Equal(EndSound.None, _harness.Store.Stored.Sound);
        Assert.Equal(AppLanguage.Polish, _harness.Localizer.Language);
        Assert.True(closedWith);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public void Save_WhenTheDiskFails_StaysOpenAndExplains()
    {
        _harness.Store.FailOnSave = true;
        var viewModel = new SettingsViewModel(_harness.Settings, _harness.Autostart, _harness.Dialogs, _harness.Localizer);
        var closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

        viewModel.WarningEnabled = false;
        viewModel.SaveCommand.Execute(null);

        Assert.False(closed);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.False(_harness.Settings.Current.WarningEnabled);
    }

    [Fact]
    public void Cancel_ClosesWithoutSaving()
    {
        var viewModel = new SettingsViewModel(_harness.Settings, _harness.Autostart, _harness.Dialogs, _harness.Localizer);
        bool? closedWith = null;
        viewModel.CloseRequested += (_, saved) => closedWith = saved;
        var savesBefore = _harness.Store.SaveCount;

        viewModel.WarningEnabled = false;
        viewModel.CancelCommand.Execute(null);

        Assert.False(closedWith);
        Assert.Equal(savesBefore, _harness.Store.SaveCount);
        Assert.True(_harness.Settings.Current.WarningEnabled);
    }

    [Fact]
    public void PickingALanguage_PreviewsItAtOnceWithoutSaving()
    {
        // Issue #54: the texts switch as soon as a language is picked.
        var viewModel = new SettingsViewModel(_harness.Settings, _harness.Autostart, _harness.Dialogs, _harness.Localizer);
        var savesBefore = _harness.Store.SaveCount;

        viewModel.Language = AppLanguage.Polish;

        Assert.Equal(AppLanguage.Polish, _harness.Localizer.Language);
        Assert.Equal(AppLanguage.English, _harness.Settings.Current.Language);
        Assert.Equal(savesBefore, _harness.Store.SaveCount);
    }

    [Fact]
    public void Cancel_AfterAPreview_GoesBackToTheLanguageInEffect()
    {
        var viewModel = new SettingsViewModel(_harness.Settings, _harness.Autostart, _harness.Dialogs, _harness.Localizer);
        viewModel.Language = AppLanguage.Polish;

        viewModel.CancelCommand.Execute(null);

        Assert.Equal(AppLanguage.English, _harness.Localizer.Language);
        Assert.Equal(AppLanguage.English, _harness.Store.Stored.Language);
    }

    [Fact]
    public void StartWithWindows_ShowsWhatWindowsHas()
    {
        // Issue #83: Windows keeps this choice, so the panel asks it, not the settings file.
        _harness.Autostart.IsEnabled = true;

        var viewModel = new SettingsViewModel(_harness.Settings, _harness.Autostart, _harness.Dialogs, _harness.Localizer);

        Assert.True(viewModel.StartWithWindows);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Save_ChangesStartWithWindows(bool enabled)
    {
        _harness.Autostart.IsEnabled = !enabled;
        var viewModel = new SettingsViewModel(_harness.Settings, _harness.Autostart, _harness.Dialogs, _harness.Localizer);
        bool? closedWith = null;
        viewModel.CloseRequested += (_, saved) => closedWith = saved;

        viewModel.StartWithWindows = enabled;
        viewModel.SaveCommand.Execute(null);

        Assert.Equal([enabled], _harness.Autostart.Changes);
        Assert.True(closedWith);
    }

    [Fact]
    public void Save_WithoutAChange_LeavesWindowsAlone()
    {
        var viewModel = new SettingsViewModel(_harness.Settings, _harness.Autostart, _harness.Dialogs, _harness.Localizer);

        viewModel.WarningEnabled = false;
        viewModel.SaveCommand.Execute(null);

        Assert.Empty(_harness.Autostart.Changes);
    }

    [Fact]
    public void Save_WhenWindowsRefuses_KeepsTheOtherSettingsAndExplains()
    {
        _harness.Autostart.Fails = true;
        var viewModel = new SettingsViewModel(_harness.Settings, _harness.Autostart, _harness.Dialogs, _harness.Localizer);
        var closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

        viewModel.WarningEnabled = false;
        viewModel.StartWithWindows = true;
        viewModel.SaveCommand.Execute(null);

        Assert.False(closed);
        Assert.Equal(_harness.Localizer["Settings_AutostartFailed"], viewModel.ErrorMessage);
        Assert.False(_harness.Store.Stored.WarningEnabled);
        Assert.False(_harness.Autostart.IsEnabled);
    }

    [Fact]
    public void Cancel_LeavesStartWithWindowsAlone()
    {
        var viewModel = new SettingsViewModel(_harness.Settings, _harness.Autostart, _harness.Dialogs, _harness.Localizer);

        viewModel.StartWithWindows = true;
        viewModel.CancelCommand.Execute(null);

        Assert.Empty(_harness.Autostart.Changes);
        Assert.False(_harness.Autostart.IsEnabled);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(null!, _harness.Autostart, _harness.Dialogs, _harness.Localizer));
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(_harness.Settings, null!, _harness.Dialogs, _harness.Localizer));
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(_harness.Settings, _harness.Autostart, null!, _harness.Localizer));
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(_harness.Settings, _harness.Autostart, _harness.Dialogs, null!));
    }
}

public sealed class SettingsServiceTests
{
    [Fact]
    public void LoadsOnCreation()
    {
        var store = new InMemorySettingsStore(AppSettings.Default with { Language = AppLanguage.Polish });

        var service = new SettingsService(store, NullLogger<SettingsService>.Instance);

        Assert.Equal(AppLanguage.Polish, service.Current.Language);
    }

    [Fact]
    public void Update_SavesAndNotifies()
    {
        var store = new InMemorySettingsStore();
        var service = new SettingsService(store, NullLogger<SettingsService>.Instance);
        var changes = 0;
        service.Changed += (_, _) => changes++;

        Assert.True(service.Update(settings => settings with { RememberLast = false }));

        Assert.False(store.Stored.RememberLast);
        Assert.False(service.Current.RememberLast);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Update_KeepsTheChangeWhenSavingFails()
    {
        var store = new InMemorySettingsStore { FailOnSave = true };
        var service = new SettingsService(store, NullLogger<SettingsService>.Instance);

        Assert.False(service.Update(settings => settings with { RememberLast = false }));

        Assert.False(service.Current.RememberLast);
        Assert.True(store.Stored.RememberLast);
    }

    [Fact]
    public void RejectsMissingArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsService(null!, NullLogger<SettingsService>.Instance));
        Assert.Throws<ArgumentNullException>(() => new SettingsService(new InMemorySettingsStore(), null!));
        Assert.Throws<ArgumentNullException>(() => new SettingsService(new InMemorySettingsStore(), NullLogger<SettingsService>.Instance).Update(null!));
    }
}
