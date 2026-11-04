using Somnus.Core.Settings;
using Somnus.Presentation.Tests.TestDoubles;
using Somnus.Presentation.ViewModels;

namespace Somnus.Presentation.Tests.ViewModels;

/// <summary>Mac's feedback of 2026-11-04 (#113): the lullaby, no sound or the user's own sound file.</summary>
public sealed class SoundChoiceTests : IDisposable
{
    private readonly TestHarness _harness = new();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Somnus.Tests", Guid.NewGuid().ToString("N"));

    public SoundChoiceTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        _harness.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void ANewInstall_PlaysTheLullaby()
    {
        var viewModel = Create();

        Assert.Equal(EndSound.Lullaby, viewModel.Sound);
        Assert.False(viewModel.IsSoundFileVisible);
        Assert.Equal(["Lullaby (default)", "No sound", "Sound file"], viewModel.SoundOptions.Select(option => option.DisplayName));
    }

    [Fact]
    public void ChoosingAFile_ShowsItsRow()
    {
        var viewModel = Create();

        viewModel.Sound = EndSound.File;

        Assert.True(viewModel.IsSoundFileVisible);
    }

    [Fact]
    public void Browse_FillsInThePickedFile()
    {
        var picked = CreateFile("Morning.mp3");
        _harness.Dialogs.PickedFile = picked;
        var viewModel = Create();
        viewModel.Sound = EndSound.File;

        viewModel.BrowseSoundFileCommand.Execute(null);

        Assert.Equal(picked, viewModel.SoundFile);
        var request = Assert.Single(_harness.Dialogs.FileRequests);
        Assert.Equal("Choose a sound", request.Title);
        Assert.Equal(SoundFiles.Extensions, request.Extensions);
    }

    [Fact]
    public void Browse_ClosedWithoutAFile_KeepsThePath()
    {
        var viewModel = Create();
        viewModel.SoundFile = @"C:\Music\Old.mp3";

        viewModel.BrowseSoundFileCommand.Execute(null);

        Assert.Equal(@"C:\Music\Old.mp3", viewModel.SoundFile);
    }

    [Fact]
    public void Save_WithAPlayableFile_KeepsTheChoice()
    {
        var file = CreateFile("Morning.mp3");
        var viewModel = Create();
        bool? closedWith = null;
        viewModel.CloseRequested += (_, saved) => closedWith = saved;
        viewModel.Sound = EndSound.File;
        viewModel.SoundFile = $"  {file}  ";

        viewModel.SaveCommand.Execute(null);

        Assert.True(closedWith);
        Assert.Equal(EndSound.File, _harness.Store.Stored.Sound);
        Assert.Equal(file, _harness.Store.Stored.SoundFile);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.com/song.mp3")]
    [InlineData(@"C:\Music\gone.mp3")]
    public void Save_WithAFileThatCannotPlay_StaysOpenAndSavesNothing(string path)
    {
        var viewModel = Create();
        var closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;
        var savesBefore = _harness.Store.SaveCount;
        viewModel.WarningEnabled = false;
        viewModel.Sound = EndSound.File;
        viewModel.SoundFile = path;

        viewModel.SaveCommand.Execute(null);

        Assert.False(closed);
        Assert.Equal(_harness.Localizer["Settings_SoundFileInvalid"], viewModel.ErrorMessage);
        Assert.Equal(savesBefore, _harness.Store.SaveCount);
        Assert.True(_harness.Settings.Current.WarningEnabled);
    }

    [Fact]
    public void ASavedFileThatIsGone_IsPointedOut_UntilAnotherIsChosen()
    {
        _harness.Settings.Update(settings => settings with { Sound = EndSound.File, SoundFile = Path.Combine(_directory, "gone.mp3") });
        var viewModel = Create();
        Assert.True(viewModel.IsSoundFileMissing);

        viewModel.SoundFile = CreateFile("Other.mp3");

        Assert.False(viewModel.IsSoundFileMissing);
    }

    [Fact]
    public void ASavedFileWindowsCouldNotPlay_IsPointedOut_UntilAnotherIsChosen()
    {
        var broken = CreateFile("Broken.wma");
        _harness.Settings.Update(settings => settings with { Sound = EndSound.File, SoundFile = broken, FailedSoundFile = broken });
        var viewModel = Create();
        Assert.True(viewModel.IsSoundFileBroken);
        Assert.False(viewModel.IsSoundFileMissing);

        viewModel.SoundFile = CreateFile("Other.mp3");
        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.IsSoundFileBroken);
        Assert.Equal(string.Empty, _harness.Store.Stored.FailedSoundFile);
    }

    [Fact]
    public void SavingTheSameFileAgain_KeepsItsFailure()
    {
        var broken = CreateFile("Broken.wma");
        _harness.Settings.Update(settings => settings with { Sound = EndSound.File, SoundFile = broken, FailedSoundFile = broken });
        var viewModel = Create();

        viewModel.WarningEnabled = false;
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(broken, _harness.Store.Stored.FailedSoundFile);
    }

    [Fact]
    public void TheToolTip_ShowsTheWholePath()
    {
        var viewModel = Create();
        Assert.Null(viewModel.SoundFileToolTip);

        viewModel.SoundFile = @"C:\Users\Mac\Music\A very long folder name\Morning.mp3";

        Assert.Equal(viewModel.SoundFile, viewModel.SoundFileToolTip);
    }

    [Fact]
    public void PreviewingALanguage_RenamesTheSoundsAndKeepsTheChoice()
    {
        var viewModel = Create();
        viewModel.Sound = EndSound.None;

        viewModel.Language = AppLanguage.Polish;

        Assert.Equal(EndSound.None, viewModel.Sound);
        Assert.Equal(["Kołysanka (domyślna)", "Bez dźwięku", "Plik dźwiękowy"], viewModel.SoundOptions.Select(option => option.DisplayName));
    }

    private SettingsViewModel Create() => new(_harness.Settings, _harness.Autostart, _harness.Dialogs, _harness.Localizer);

    private string CreateFile(string name)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, [0]);
        return path;
    }
}
