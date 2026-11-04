using Somnus.Core.Settings;

namespace Somnus.Core.Tests.Settings;

/// <summary>Mac's feedback of 2026-11-04: any sound file on the PC can replace the lullaby.</summary>
public sealed class SoundFilesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Somnus.Tests", Guid.NewGuid().ToString("N"));

    public SoundFilesTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData("song.mp3")]
    [InlineData("SONG.MP3")]
    [InlineData("clip.wav")]
    [InlineData("clip.wma")]
    [InlineData("clip.m4a")]
    [InlineData("clip.aac")]
    [InlineData("clip.flac")]
    public void AnExistingFileInASupportedFormat_IsPlayable(string name)
    {
        Assert.True(SoundFiles.IsPlayable(Create(name)));
    }

    [Fact]
    public void AnotherFormat_IsNotPlayable()
    {
        Assert.False(SoundFiles.IsPlayable(Create("notes.txt")));
    }

    [Fact]
    public void AMissingFile_IsNotPlayable()
    {
        Assert.False(SoundFiles.IsPlayable(Path.Combine(_directory, "gone.mp3")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("song.mp3")]
    [InlineData(@"Music\song.mp3")]
    [InlineData("https://example.com/song.mp3")]
    [InlineData("file://C:/Music/song.mp3")]
    [InlineData(@"\\server\music\song.mp3")]
    [InlineData("//server/music/song.mp3")]
    public void AnythingButAFullLocalPath_IsNotPlayable(string? path)
    {
        // A web address or a network share would make Somnus use the network (R-07).
        Assert.False(SoundFiles.IsPlayable(path));
    }

    private string Create(string name)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, [0]);
        return path;
    }
}
