using Somnus.Core.Power;
using Somnus.Core.Scheduling;
using Somnus.Core.Settings;

namespace Somnus.Core.Tests.Settings;

public sealed class SettingsSerializerTests
{
    private static readonly AppSettings Customized = new()
    {
        Language = AppLanguage.Polish,
        WarningEnabled = false,
        RememberLast = false,
        Sound = EndSound.File,
        SoundFile = @"C:\Users\Mac\Music\Morning.mp3",
        FailedSoundFile = @"C:\Users\Mac\Music\Broken.wma",
        LastMode = TimeMode.At,
        LastAmount = 90,
        LastUnit = TimeUnit.Seconds,
        LastAction = PowerAction.Shutdown,
        LastAtTime = new TimeOnly(7, 5),
    };

    [Fact]
    public void Serialize_ThenDeserialize_RoundTrips()
    {
        var result = SettingsSerializer.Deserialize(SettingsSerializer.Serialize(Customized));

        Assert.Equal(Customized, result.Settings);
        Assert.Empty(result.Problems);
        Assert.False(result.IsCorrupt);
    }

    [Fact]
    public void Serialize_WritesReadableJsonWithoutAnyForceField()
    {
        var json = SettingsSerializer.Serialize(Customized);

        Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"language\": \"pl\"", json, StringComparison.Ordinal);
        Assert.Contains("\"lastAction\": \"Shutdown\"", json, StringComparison.Ordinal);
        Assert.Contains("\"lastAtTime\": \"07:05\"", json, StringComparison.Ordinal);
        Assert.Contains("\"sound\": \"File\"", json, StringComparison.Ordinal);
        Assert.Contains("\"soundFile\": \"C:\\\\Users\\\\Mac\\\\Music\\\\Morning.mp3\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("lullabyEnabled", json, StringComparison.Ordinal);
        Assert.DoesNotContain("force", json, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("}\n", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_EmptyObject_GivesDefaultsWithoutProblems()
    {
        var result = SettingsSerializer.Deserialize("{}");

        // A file that exists comes from an earlier install, so it stays quiet; only a new install gets the lullaby.
        Assert.Equal(AppSettings.Default with { Sound = EndSound.None }, result.Settings);
        Assert.Empty(result.Problems);
        Assert.False(result.IsCorrupt);
    }

    [Fact]
    public void Deserialize_AFileFromVersion1_0_KeepsTheLullabyOff()
    {
        // 1.0 files have no lullaby field; Somnus must not start making sounds after an update (#59).
        var result = SettingsSerializer.Deserialize("""{ "schemaVersion": 1, "language": "pl", "warningEnabled": true }""");

        Assert.Equal(EndSound.None, result.Settings.Sound);
        Assert.Empty(result.Problems);
    }

    [Theory]
    [InlineData("true", EndSound.Lullaby)]
    [InlineData("false", EndSound.None)]
    public void Deserialize_TheLullabySwitchOf1_1Candidates_BecomesTheSoundChoice(string enabled, EndSound expected)
    {
        // Mac's feedback of 2026-11-04: the on/off lullaby became a choice of sound.
        var result = SettingsSerializer.Deserialize($$"""{ "lullabyEnabled": {{enabled}} }""");

        Assert.Equal(expected, result.Settings.Sound);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void Deserialize_TheSoundChoice_WinsOverTheOldSwitch()
    {
        var result = SettingsSerializer.Deserialize("""{ "lullabyEnabled": false, "sound": "Lullaby" }""");

        Assert.Equal(EndSound.Lullaby, result.Settings.Sound);
    }

    [Fact]
    public void Deserialize_AnInvalidSound_IsReportedAndQuiet()
    {
        var result = SettingsSerializer.Deserialize("""{ "sound": "Trumpet", "soundFile": 5 }""");

        Assert.Equal(EndSound.None, result.Settings.Sound);
        Assert.Equal(string.Empty, result.Settings.SoundFile);
        Assert.Equal(2, result.Problems.Count);
    }

    [Fact]
    public void Deserialize_AnInvalidLullabyValue_IsReportedAndOff()
    {
        var result = SettingsSerializer.Deserialize("""{ "lullabyEnabled": "loud" }""");

        Assert.Equal(EndSound.None, result.Settings.Sound);
        Assert.Single(result.Problems);
    }

    [Fact]
    public void Deserialize_PartialFile_KeepsWhatIsThereAndDefaultsTheRest()
    {
        var result = SettingsSerializer.Deserialize("""{ "language": "pl", "lastAction": "Hibernate" }""");

        Assert.Equal(AppSettings.Default with { Language = AppLanguage.Polish, LastAction = PowerAction.Hibernate, Sound = EndSound.None }, result.Settings);
        Assert.Empty(result.Problems);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("not json")]
    [InlineData("""{ "language": }""")]
    public void Deserialize_UnreadableContent_IsCorruptAndFallsBackToDefaults(string json)
    {
        var result = SettingsSerializer.Deserialize(json);

        Assert.True(result.IsCorrupt);
        Assert.Equal(AppSettings.Default, result.Settings);
        Assert.Single(result.Problems);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"text\"")]
    [InlineData("null")]
    public void Deserialize_RootThatIsNotAnObject_IsCorrupt(string json)
    {
        Assert.True(SettingsSerializer.Deserialize(json).IsCorrupt);
    }

    [Fact]
    public void Deserialize_DeeplyNestedContent_IsCorrupt()
    {
        var json = "{\"a\":" + new string('[', 20) + new string(']', 20) + "}";

        Assert.True(SettingsSerializer.Deserialize(json).IsCorrupt);
    }

    [Fact]
    public void Deserialize_EveryInvalidValue_FallsBackToItsDefaultAndIsReported()
    {
        const string json = """
            {
              "language": "de",
              "warningEnabled": "yes",
              "rememberLast": 1,
              "lastMode": "Later",
              "lastAmount": "thirty",
              "lastUnit": "Days",
              "lastAction": "Reboot",
              "lastAtTime": "25:00"
            }
            """;

        var result = SettingsSerializer.Deserialize(json);

        Assert.False(result.IsCorrupt);
        Assert.Equal(AppSettings.Default with { Sound = EndSound.None }, result.Settings);
        Assert.Equal(8, result.Problems.Count);
    }

    [Fact]
    public void Deserialize_OneBadValue_DoesNotDiscardTheOthers()
    {
        var result = SettingsSerializer.Deserialize("""{ "language": "pl", "lastUnit": "Fortnights", "lastAction": "Shutdown" }""");

        Assert.Equal(AppLanguage.Polish, result.Settings.Language);
        Assert.Equal(PowerAction.Shutdown, result.Settings.LastAction);
        Assert.Equal(AppSettings.Default.LastUnit, result.Settings.LastUnit);
        Assert.Single(result.Problems);
    }

    [Theory]
    [InlineData("""{ "lastAction": "1" }""")]
    [InlineData("""{ "lastAction": 1 }""")]
    [InlineData("""{ "lastAction": null }""")]
    public void Deserialize_EnumsAreAcceptedOnlyByName(string json)
    {
        var result = SettingsSerializer.Deserialize(json);

        Assert.Equal(AppSettings.Default.LastAction, result.Settings.LastAction);
        Assert.Single(result.Problems);
    }

    [Theory]
    [InlineData(5, "Seconds")]
    [InlineData(100, "Hours")]
    [InlineData(0, "Minutes")]
    [InlineData(-30, "Minutes")]
    public void Deserialize_LastAmountOutsideTheAllowedRange_ResetsAmountAndUnit(int amount, string unit)
    {
        var result = SettingsSerializer.Deserialize($$"""{ "lastAmount": {{amount}}, "lastUnit": "{{unit}}" }""");

        Assert.Equal(AppSettings.Default.LastAmount, result.Settings.LastAmount);
        Assert.Equal(AppSettings.Default.LastUnit, result.Settings.LastUnit);
        Assert.Single(result.Problems);
    }

    [Fact]
    public void Deserialize_UnknownFieldsAreIgnored()
    {
        var result = SettingsSerializer.Deserialize("""{ "schemaVersion": 7, "futureFeature": { "x": [1, 2] }, "language": "pl" }""");

        Assert.Equal(AppLanguage.Polish, result.Settings.Language);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void Deserialize_FieldNamesAndValuesIgnoreCase()
    {
        var result = SettingsSerializer.Deserialize("""{ "Language": "PL", "WARNINGENABLED": false, "lastmode": "at" }""");

        Assert.Equal(AppLanguage.Polish, result.Settings.Language);
        Assert.False(result.Settings.WarningEnabled);
        Assert.Equal(TimeMode.At, result.Settings.LastMode);
    }

    [Fact]
    public void Deserialize_AllowsCommentsAndTrailingCommas()
    {
        const string json = """
            {
              // edited by hand
              "rememberLast": false,
            }
            """;

        Assert.False(SettingsSerializer.Deserialize(json).Settings.RememberLast);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => SettingsSerializer.Serialize(null!));
        Assert.Throws<ArgumentNullException>(() => SettingsSerializer.Deserialize(null!));
    }
}
