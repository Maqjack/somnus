using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Somnus.Presentation.Tests.Repository;

/// <summary>
/// Rule R-04: every user-facing string lives in <c>Strings.resx</c>, so every key used in XAML must exist there.
/// </summary>
public sealed partial class ResourceTests
{
    [Fact]
    public void EveryKeyUsedInXaml_ExistsInTheEnglishResources()
    {
        var keys = ResourceKeys(EnglishResources);
        var xamlFiles = Directory.GetFiles(RepositoryFiles.PathOf("src/Somnus.App"), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        var used = xamlFiles
            .SelectMany(path => TrKeyPattern().Matches(File.ReadAllText(path)).Select(match => (File: Path.GetFileName(path), Key: match.Groups[1].Value)))
            .ToList();

        Assert.NotEmpty(used);
        Assert.Empty(used.Where(use => !keys.Contains(use.Key)).Select(use => $"{use.File}: {use.Key}"));
    }

    [Fact]
    public void EnglishResources_HaveNoEmptyValues()
    {
        var document = XDocument.Load(RepositoryFiles.PathOf("src/Somnus.Presentation/Resources/Strings.resx"));

        var empty = document.Root!.Elements("data")
            .Where(data => string.IsNullOrWhiteSpace(data.Element("value")?.Value))
            .Select(data => data.Attribute("name")!.Value);

        Assert.Empty(empty);
    }

    [Fact]
    public void PolishResources_HaveExactlyTheEnglishKeys()
    {
        var english = ResourceKeys(EnglishResources);
        var polish = ResourceKeys(PolishResources);

        Assert.Empty(english.Except(polish));
        Assert.Empty(polish.Except(english));
    }

    [Fact]
    public void PolishResources_HaveNoEmptyValues()
    {
        Assert.Empty(ResourceValues(PolishResources).Where(entry => string.IsNullOrWhiteSpace(entry.Value)).Select(entry => entry.Key));
    }

    [Fact]
    public void BothLanguages_UseTheSamePlaceholders()
    {
        var polish = ResourceValues(PolishResources);

        var mismatches = ResourceValues(EnglishResources)
            .Where(entry => polish.TryGetValue(entry.Key, out var translation)
                && !Placeholders(entry.Value).SetEquals(Placeholders(translation)))
            .Select(entry => entry.Key);

        Assert.Empty(mismatches);
    }

    [Fact]
    public void PolishResources_NeverDeclineTheAppName()
    {
        // Mac: inside the app it is just Somnus; only documents may decline the name.
        Assert.Empty(ResourceValues(PolishResources).Where(entry => DeclinedNamePattern().IsMatch(entry.Value)).Select(entry => entry.Key));
    }

    internal static HashSet<string> ResourceKeys(string relativePath) =>
        ResourceValues(relativePath).Keys.ToHashSet(StringComparer.Ordinal);

    private const string EnglishResources = "src/Somnus.Presentation/Resources/Strings.resx";
    private const string PolishResources = "src/Somnus.Presentation/Resources/Strings.pl.resx";

    private static Dictionary<string, string> ResourceValues(string relativePath) =>
        XDocument.Load(RepositoryFiles.PathOf(relativePath)).Root!
            .Elements("data")
            .ToDictionary(data => data.Attribute("name")!.Value, data => data.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static HashSet<string> Placeholders(string text) =>
        PlaceholderPattern().Matches(text).Select(match => match.Value).ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"\{l:Tr\s+(?:Key=)?([A-Za-z0-9_]+)\s*\}")]
    private static partial Regex TrKeyPattern();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"Somnus\p{L}")]
    private static partial Regex DeclinedNamePattern();
}
