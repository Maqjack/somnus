using Microsoft.Extensions.Logging.Abstractions;
using Somnus.Core.Power;
using Somnus.Core.Settings;
using Somnus.Windows.Settings;

namespace Somnus.Windows.Tests.Settings;

public sealed class JsonFileSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Somnus.Tests", Guid.NewGuid().ToString("N"));
    private readonly JsonFileSettingsStore _store;

    public JsonFileSettingsStoreTests()
    {
        _store = new JsonFileSettingsStore(_directory, NullLogger<JsonFileSettingsStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Load_WithoutAFile_GivesDefaults()
    {
        Assert.Equal(AppSettings.Default, _store.Load());
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public void Load_WithoutAFile_GivesTheFirstStartSettings()
    {
        // Issue #53: the first start speaks the installer's language.
        var firstStart = AppSettings.Default with { Language = AppLanguage.Polish };
        var store = new JsonFileSettingsStore(_directory, NullLogger<JsonFileSettingsStore>.Instance, firstStart);

        Assert.Equal(firstStart, store.Load());
    }

    [Fact]
    public void Load_ASavedLanguage_WinsOverTheFirstStartSettings()
    {
        _store.Save(AppSettings.Default with { Language = AppLanguage.English });
        var store = new JsonFileSettingsStore(_directory, NullLogger<JsonFileSettingsStore>.Instance, AppSettings.Default with { Language = AppLanguage.Polish });

        Assert.Equal(AppLanguage.English, store.Load().Language);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var settings = AppSettings.Default with { Language = AppLanguage.Polish, LastAction = PowerAction.Shutdown, WarningEnabled = false };

        _store.Save(settings);

        Assert.Equal(settings, _store.Load());
        Assert.True(File.Exists(_store.FilePath));
        Assert.False(File.Exists(_store.FilePath + ".tmp"));
    }

    [Fact]
    public void Save_ReplacesTheExistingFile()
    {
        _store.Save(AppSettings.Default with { Language = AppLanguage.Polish });

        _store.Save(AppSettings.Default with { Language = AppLanguage.English, RememberLast = false });

        var loaded = _store.Load();
        Assert.Equal(AppLanguage.English, loaded.Language);
        Assert.False(loaded.RememberLast);
    }

    [Fact]
    public void Save_WritesUtf8WithoutByteOrderMark()
    {
        _store.Save(AppSettings.Default);

        var bytes = File.ReadAllBytes(_store.FilePath);
        Assert.Equal((byte)'{', bytes[0]);
    }

    [Fact]
    public void Load_CorruptFile_KeepsABackupAndGivesDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_store.FilePath, "{ this is not json");

        var settings = _store.Load();

        Assert.Equal(AppSettings.Default, settings);
        Assert.False(File.Exists(_store.FilePath));
        Assert.Equal("{ this is not json", File.ReadAllText(_store.BackupPath));
    }

    [Fact]
    public void Load_CorruptFile_ReplacesAnOlderBackup()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_store.BackupPath, "old backup");
        File.WriteAllText(_store.FilePath, "[]");

        _store.Load();

        Assert.Equal("[]", File.ReadAllText(_store.BackupPath));
    }

    [Fact]
    public void Load_FileWithSomeInvalidValues_KeepsTheValidOnesAndTheFile()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_store.FilePath, """{ "language": "pl", "lastUnit": "Weeks" }""");

        var settings = _store.Load();

        Assert.Equal(AppLanguage.Polish, settings.Language);
        Assert.Equal(AppSettings.Default.LastUnit, settings.LastUnit);
        Assert.True(File.Exists(_store.FilePath));
        Assert.False(File.Exists(_store.BackupPath));
    }

    [Fact]
    public void Load_FileLockedByAnotherProcess_GivesDefaults()
    {
        _store.Save(AppSettings.Default with { Language = AppLanguage.Polish });

        using (new FileStream(_store.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal(AppSettings.Default, _store.Load());
        }

        Assert.Equal(AppLanguage.Polish, _store.Load().Language);
    }

    [Fact]
    public void Save_WhenTheFileCannotBeReplaced_ThrowsAndLeavesNoTemporaryFile()
    {
        _store.Save(AppSettings.Default);

        using (new FileStream(_store.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var exception = Record.Exception(() => _store.Save(AppSettings.Default with { RememberLast = false }));
            Assert.True(exception is IOException or UnauthorizedAccessException, $"Unexpected exception: {exception}");
        }

        Assert.False(File.Exists(_store.FilePath + ".tmp"));
        Assert.True(_store.Load().RememberLast);
    }

    [Fact]
    public void DefaultDirectory_IsSomnusInsideAppData()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Somnus");

        Assert.Equal(expected, JsonFileSettingsStore.DefaultDirectory());
    }

    [Fact]
    public void Constructor_RejectsMissingArguments()
    {
        Assert.Throws<ArgumentException>(() => new JsonFileSettingsStore(" ", NullLogger<JsonFileSettingsStore>.Instance));
        Assert.Throws<ArgumentNullException>(() => new JsonFileSettingsStore(_directory, null!));
        Assert.Throws<ArgumentNullException>(() => _store.Save(null!));
    }
}
