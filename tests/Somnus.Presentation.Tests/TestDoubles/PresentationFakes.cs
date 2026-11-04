using Somnus.Core.Power;
using Somnus.Core.Settings;
using Somnus.Presentation.Services;

namespace Somnus.Presentation.Tests.TestDoubles;

/// <summary>Starts with Windows in memory; <see cref="Fails"/> makes every change fail.</summary>
internal sealed class FakeAutostart : IAutostart
{
    public bool IsEnabled { get; set; }

    public bool Fails { get; set; }

    public List<bool> Changes { get; } = [];

    public bool TrySet(bool enabled)
    {
        if (Fails)
        {
            return false;
        }

        Changes.Add(enabled);
        IsEnabled = enabled;
        return true;
    }
}

/// <summary>Runs posted work immediately, as if the test thread were the UI thread.</summary>
internal sealed class ImmediateDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

/// <summary>Answers every confirmation with <see cref="Answer"/> and remembers the questions.</summary>
internal sealed class FakeDialogService : IDialogService
{
    public bool Answer { get; set; } = true;

    public List<ConfirmationRequest> Requests { get; } = [];

    public List<(string Heading, string Message)> Errors { get; } = [];

    public bool Confirm(ConfirmationRequest request)
    {
        Requests.Add(request);
        return Answer;
    }

    public void ShowError(string heading, string message) => Errors.Add((heading, message));

    /// <summary>Gets or sets what the file dialog returns; <see langword="null"/> means the user closed it.</summary>
    public string? PickedFile { get; set; }

    public List<FilePickRequest> FileRequests { get; } = [];

    public string? PickFile(FilePickRequest request)
    {
        FileRequests.Add(request);
        return PickedFile;
    }
}

/// <summary>Collects notifications instead of showing them.</summary>
internal sealed class FakeNotificationService : INotificationService
{
    public List<(string Title, string Message)> Notifications { get; } = [];

    public void Notify(string title, string message) => Notifications.Add((title, message));
}

/// <summary>Keeps settings in memory and can be told to fail on save.</summary>
internal sealed class InMemorySettingsStore(AppSettings? initial = null) : ISettingsStore
{
    public AppSettings Stored { get; private set; } = initial ?? AppSettings.Default;

    public int SaveCount { get; private set; }

    public bool FailOnSave { get; set; }

    public AppSettings Load() => Stored;

    public void Save(AppSettings settings)
    {
        if (FailOnSave)
        {
            // Fully qualified: WPF test projects do not import System.IO implicitly.
            throw new System.IO.IOException("Disk full.");
        }

        Stored = settings;
        SaveCount++;
    }
}

/// <summary>Reports whatever support the test sets.</summary>
internal sealed class FakePowerCapabilities : IPowerCapabilities
{
    public PowerSupport Support { get; set; } = new(CanSleep: true, CanHibernate: true, IsModernStandby: false);

    public PowerSupport Query() => Support;
}
