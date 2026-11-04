using System.Buffers.Binary;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Somnus.App.Services;
using Somnus.Presentation.Services;
using Somnus.Presentation.Tests.TestDoubles;

namespace Somnus.App.Tests;

/// <summary>
/// The sound side of the lullaby (#59, #71) and of the user's sound file (#113). The tests never
/// call Play: a test run must not make a sound on the developer's PC.
/// </summary>
public sealed class SoundPlayerLullabyTests
{
    private static readonly byte[] Recording = ReadRecording();

    [Fact]
    public void SilenceAndDispose_BeforeAnyPlay_DoNothing()
    {
        using var harness = new TestHarness();
        var player = new SoundPlayerLullaby(NullLogger<SoundPlayerLullaby>.Instance, harness.Settings);

        player.Silence();
        player.Dispose();
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        using var harness = new TestHarness();
        Assert.Throws<ArgumentNullException>(() => new SoundPlayerLullaby(null!, harness.Settings));
        Assert.Throws<ArgumentNullException>(() => new SoundPlayerLullaby(NullLogger<SoundPlayerLullaby>.Instance, null!));
    }

    [Fact]
    public void AFileWindowsCannotPlay_IsRemembered_UntilItPlays()
    {
        // #113: the settings panel says when Windows refused the file, until it plays again or another is chosen.
        using var harness = new TestHarness();
        using var player = new SoundPlayerLullaby(NullLogger<SoundPlayerLullaby>.Instance, harness.Settings);

        player.RecordFailure(@"C:\Music\Broken.wma");
        Assert.Equal(@"C:\Music\Broken.wma", harness.Store.Stored.FailedSoundFile);

        player.RecordSuccess(@"C:\Music\Other.mp3");
        Assert.Equal(@"C:\Music\Broken.wma", harness.Settings.Current.FailedSoundFile);

        player.RecordSuccess(@"C:\MUSIC\broken.wma");
        Assert.Equal(string.Empty, harness.Store.Stored.FailedSoundFile);
    }

    [Theory]
    [InlineData(@"C:\Music\Morning.mp3")]
    [InlineData(@"C:\Music\Rock & Roll #1\100% song.mp3")]
    [InlineData(@"D:\Muzyka\Kołysanki\Śpij, kotku.flac")]
    public void AFileAddress_KeepsTheWholePath(string path)
    {
        // The user's file goes to the Windows media player as a file address (#113).
        var uri = SoundPlayerLullaby.ToFileUri(path);

        Assert.True(uri.IsFile);
        Assert.Equal(path, uri.LocalPath);
    }

    [Fact]
    public void Recording_IsSixteenBitMonoPcm()
    {
        // The only WAV format SoundPlayer plays without help, with the 44-byte header it expects.
        Assert.Equal("RIFF", Encoding.ASCII.GetString(Recording, 0, 4));
        Assert.Equal(Recording.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(Recording.AsSpan(4)));
        Assert.Equal("WAVEfmt ", Encoding.ASCII.GetString(Recording, 8, 8));
        Assert.Equal(16, BinaryPrimitives.ReadInt32LittleEndian(Recording.AsSpan(16)));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(Recording.AsSpan(20)));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(Recording.AsSpan(22)));
        Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(Recording.AsSpan(34)));
        Assert.Equal("data", Encoding.ASCII.GetString(Recording, 36, 4));
        Assert.Equal(Recording.Length - 44, BinaryPrimitives.ReadInt32LittleEndian(Recording.AsSpan(40)));
    }

    [Fact]
    public void Recording_FillsTheLeadTimeWithoutOutlastingIt()
    {
        var sampleRate = BinaryPrimitives.ReadInt32LittleEndian(Recording.AsSpan(24));
        var duration = TimeSpan.FromSeconds((double)Samples().Length / sampleRate);

        Assert.InRange(duration, LullabyCoordinator.LeadTime - TimeSpan.FromSeconds(1), LullabyCoordinator.LeadTime);
    }

    [Fact]
    public void Recording_IsAudibleButQuietAndFadesOut()
    {
        var samples = Samples();

        // make-lullaby.ps1 sets the loudest moment to -3 dBFS, about 0.71 of full scale.
        var peak = samples.Max(sample => Math.Abs((int)sample));
        Assert.InRange(peak, short.MaxValue / 2, (int)(0.75 * short.MaxValue));
        Assert.InRange(Math.Abs((int)samples[^1]), 0, 50);
    }

    [Fact]
    public void Recording_StartsAtOnce()
    {
        var sampleRate = BinaryPrimitives.ReadInt32LittleEndian(Recording.AsSpan(24));
        var firstTenthOfASecond = Samples().Take(sampleRate / 10);

        Assert.Contains(firstTenthOfASecond, sample => Math.Abs((int)sample) > 1000);
    }

    private static byte[] ReadRecording()
    {
        using var stream = SoundPlayerLullaby.OpenRecording();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static short[] Samples()
    {
        var data = Recording.AsSpan(44);
        var samples = new short[data.Length / 2];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(data[(i * 2)..]);
        }

        return samples;
    }
}
