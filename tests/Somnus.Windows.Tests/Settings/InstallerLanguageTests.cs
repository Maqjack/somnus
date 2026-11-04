using Somnus.Core.Settings;
using Somnus.Windows.Settings;

namespace Somnus.Windows.Tests.Settings;

/// <summary>Issue #53: the installer's language, as Inno Setup writes it next to Somnus.exe.</summary>
public sealed class InstallerLanguageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Somnus.Tests", Guid.NewGuid().ToString("N"));

    public InstallerLanguageTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData("polish", AppLanguage.Polish)]
    [InlineData("english", AppLanguage.English)]
    [InlineData("Polish", AppLanguage.Polish)]
    public void Read_GivesTheInstallerLanguage(string written, AppLanguage expected)
    {
        Write($"[Installer]\r\nLanguage={written}\r\n");

        Assert.Equal(expected, InstallerLanguage.Read(_directory));
    }

    [Fact]
    public void Read_WithoutTheFile_GivesNothing()
    {
        // A run from source has no Somnus.ini.
        Assert.Null(InstallerLanguage.Read(_directory));
    }

    [Theory]
    [InlineData("[Installer]\r\nLanguage=german\r\n")]
    [InlineData("[Other]\r\nLanguage=polish\r\n")]
    [InlineData("Language=polish\r\n")]
    [InlineData("")]
    public void Read_AnotherLanguageOrSection_GivesNothing(string content)
    {
        Write(content);

        Assert.Null(InstallerLanguage.Read(_directory));
    }

    [Fact]
    public void Read_FindsTheKeyAmongOtherSections()
    {
        Write("[First]\r\nLanguage=english\r\n\r\n[Installer]\r\nOther=1\r\nLanguage = polish\r\n");

        Assert.Equal(AppLanguage.Polish, InstallerLanguage.Read(_directory));
    }

    private void Write(string content) => File.WriteAllText(Path.Combine(_directory, InstallerLanguage.FileName), content);
}
