using Somnus.Core.Settings;

namespace Somnus.Core.Tests.Settings;

public sealed class AppLanguageTests
{
    [Theory]
    [InlineData(AppLanguage.English, "en")]
    [InlineData(AppLanguage.Polish, "pl")]
    public void ToCultureName_ReturnsTheNeutralCulture(AppLanguage language, string expected)
    {
        Assert.Equal(expected, language.ToCultureName());
    }

    [Fact]
    public void ToCultureName_UnknownLanguage_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((AppLanguage)7).ToCultureName());
    }

    [Theory]
    [InlineData("en", AppLanguage.English)]
    [InlineData("en-US", AppLanguage.English)]
    [InlineData("pl", AppLanguage.Polish)]
    [InlineData("PL-pl", AppLanguage.Polish)]
    public void TryFromCultureName_FindsSupportedLanguages(string cultureName, AppLanguage expected)
    {
        Assert.True(AppLanguageExtensions.TryFromCultureName(cultureName, out var language));
        Assert.Equal(expected, language);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("de")]
    [InlineData("english")]
    public void TryFromCultureName_RejectsEverythingElse(string? cultureName)
    {
        Assert.False(AppLanguageExtensions.TryFromCultureName(cultureName, out var language));
        Assert.Equal(AppLanguage.English, language);
    }
}
