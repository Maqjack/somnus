using System.ComponentModel;
using System.Globalization;
using Somnus.Core.Settings;
using Somnus.Presentation.Resources;

namespace Somnus.Presentation.Localization;

/// <summary>
/// Looks up user-facing strings in the current interface language and tells bindings when the
/// language changes, so the whole UI switches without a restart.
/// </summary>
/// <remarks>
/// XAML binds to <see cref="Instance"/> through the indexer (<c>Item[]</c>); view models receive the
/// same instance through dependency injection, and tests create their own.
/// </remarks>
public sealed class Localizer : INotifyPropertyChanged
{
    private const string IndexerName = "Item[]";

    private CultureInfo _culture = CultureInfo.GetCultureInfo(AppLanguage.English.ToCultureName());

    /// <summary>Gets the instance shared by XAML bindings and the app.</summary>
    public static Localizer Instance { get; } = new();

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after <see cref="Language"/> changes.</summary>
    public event EventHandler? LanguageChanged;

    /// <summary>Gets the culture used for strings, dates and numbers.</summary>
    public CultureInfo Culture => _culture;

    /// <summary>Gets or sets the interface language.</summary>
    public AppLanguage Language
    {
        get => AppLanguageExtensions.TryFromCultureName(_culture.Name, out var language) ? language : AppLanguage.English;
        set
        {
            var culture = CultureInfo.GetCultureInfo(value.ToCultureName());
            if (culture.Equals(_culture))
            {
                return;
            }

            _culture = culture;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(IndexerName));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets the string for <paramref name="key"/> in the current language. A missing key shows up
    /// as <c>[key]</c> instead of failing; tests make sure it never happens.
    /// </summary>
    /// <param name="key">A resource key from <see cref="Strings"/>.</param>
    /// <returns>The localized text.</returns>
    public string this[string key] => Strings.ResourceManager.GetString(key, _culture) ?? $"[{key}]";

    /// <summary>
    /// Formats the string for <paramref name="key"/> with <paramref name="arguments"/> in the current culture.
    /// </summary>
    /// <param name="key">A resource key whose text has placeholders such as <c>{0}</c>.</param>
    /// <param name="arguments">The values for the placeholders.</param>
    /// <returns>The formatted text.</returns>
    public string Format(string key, params object?[] arguments) => string.Format(_culture, this[key], arguments);
}
