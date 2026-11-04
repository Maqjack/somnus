namespace Somnus.Core.Settings;

/// <summary>
/// The language of the user interface.
/// </summary>
public enum AppLanguage
{
    /// <summary>English, the default.</summary>
    English,

    /// <summary>Polish.</summary>
    Polish,
}

/// <summary>
/// Conversions between <see cref="AppLanguage"/> and culture names.
/// </summary>
public static class AppLanguageExtensions
{
    /// <summary>
    /// Returns the neutral culture name of the language, for example <c>en</c> or <c>pl</c>.
    /// </summary>
    /// <param name="language">A defined language.</param>
    /// <returns>The culture name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The language is not a defined value.</exception>
    public static string ToCultureName(this AppLanguage language) => language switch
    {
        AppLanguage.English => "en",
        AppLanguage.Polish => "pl",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown language."),
    };

    /// <summary>
    /// Finds the language for a culture name such as <c>pl</c> or <c>pl-PL</c>, ignoring case.
    /// </summary>
    /// <param name="cultureName">The culture name.</param>
    /// <param name="language">The language, when found.</param>
    /// <returns><see langword="true"/> if the culture belongs to a supported language.</returns>
    public static bool TryFromCultureName(string? cultureName, out AppLanguage language)
    {
        var neutral = cultureName?.Split('-')[0];
        foreach (var candidate in Enum.GetValues<AppLanguage>())
        {
            if (string.Equals(candidate.ToCultureName(), neutral, StringComparison.OrdinalIgnoreCase))
            {
                language = candidate;
                return true;
            }
        }

        language = AppLanguage.English;
        return false;
    }
}
