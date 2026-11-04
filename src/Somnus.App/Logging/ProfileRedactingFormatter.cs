using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Serilog.Events;
using Serilog.Formatting;

namespace Somnus.App.Logging;

/// <summary>
/// Formats log events with another formatter, then writes the user's profile folder as
/// <c>%USERPROFILE%</c>. The folder name is usually the Windows user name, often a real name, and it
/// appears in every path under <c>%APPDATA%</c> and in exception messages.
/// </summary>
internal sealed class ProfileRedactingFormatter : ITextFormatter
{
    /// <summary>What the profile folder is replaced with.</summary>
    public const string Placeholder = "%USERPROFILE%";

    private readonly ITextFormatter _inner;
    private readonly Regex? _profile;

    /// <summary>
    /// Initializes the formatter.
    /// </summary>
    /// <param name="inner">Formats the event before the profile folder is replaced.</param>
    /// <param name="profileDirectory">The profile folder, for example <c>C:\Users\Mac</c>; empty to replace nothing.</param>
    public ProfileRedactingFormatter(ITextFormatter inner, string profileDirectory)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(profileDirectory);
        _inner = inner;

        var profile = profileDirectory.TrimEnd('\\', '/');
        if (profile.Length > 0)
        {
            // Not followed by a letter or digit, so C:\Users\Mac does not match inside C:\Users\Mac2.
            _profile = new Regex(
                Regex.Escape(profile) + @"(?![\p{L}\p{N}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        }
    }

    /// <inheritdoc />
    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var buffer = new StringWriter(CultureInfo.InvariantCulture);
        _inner.Format(logEvent, buffer);
        var text = buffer.ToString();
        output.Write(_profile is null ? text : _profile.Replace(text, Placeholder));
    }
}
