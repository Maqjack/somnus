using System.Globalization;
using System.Text;
using System.Text.Json;
using Somnus.Core.Power;
using Somnus.Core.Scheduling;

namespace Somnus.Core.Settings;

/// <summary>
/// Converts <see cref="AppSettings"/> to and from the JSON stored in <c>settings.json</c>.
/// </summary>
/// <remarks>
/// Reading is migration-safe: unknown fields are ignored, missing fields take their defaults,
/// and each invalid value is replaced by its default and reported as a problem, so one bad
/// value never discards the rest of the settings.
/// </remarks>
public static class SettingsSerializer
{
    /// <summary>The schema version written into new files.</summary>
    public const int SchemaVersion = 1;

    private const string ClockTimeFormat = "HH:mm";

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 8,
    };

    private static readonly JsonWriterOptions WriteOptions = new() { Indented = true, NewLine = "\n" };

    /// <summary>
    /// Writes the settings as indented JSON.
    /// </summary>
    /// <param name="settings">The settings to write.</param>
    /// <returns>The JSON text.</returns>
    public static string Serialize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriteOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber(Fields.SchemaVersion, SchemaVersion);
            writer.WriteString(Fields.Language, settings.Language.ToCultureName());
            writer.WriteBoolean(Fields.WarningEnabled, settings.WarningEnabled);
            writer.WriteBoolean(Fields.RememberLast, settings.RememberLast);
            writer.WriteString(Fields.Sound, settings.Sound.ToString());
            writer.WriteString(Fields.SoundFile, settings.SoundFile);
            writer.WriteString(Fields.FailedSoundFile, settings.FailedSoundFile);
            writer.WriteString(Fields.LastMode, settings.LastMode.ToString());
            writer.WriteNumber(Fields.LastAmount, settings.LastAmount);
            writer.WriteString(Fields.LastUnit, settings.LastUnit.ToString());
            writer.WriteString(Fields.LastAction, settings.LastAction.ToString());
            writer.WriteString(Fields.LastAtTime, settings.LastAtTime.ToString(ClockTimeFormat, CultureInfo.InvariantCulture));
            writer.WriteNumber(Fields.LastRepeat, (int)settings.LastRepeat);
            writer.WriteNumber(Fields.ScheduleDays, (int)settings.ScheduleDays);
            writer.WriteString(Fields.ScheduleTime, settings.ScheduleTime.ToString(ClockTimeFormat, CultureInfo.InvariantCulture));
            writer.WriteString(Fields.ScheduleAction, settings.ScheduleAction.ToString());
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>
    /// Reads settings from JSON. Never throws for bad content.
    /// </summary>
    /// <param name="json">The file content.</param>
    /// <returns>The settings, the problems found and whether the content was unreadable.</returns>
    public static SettingsReadResult Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, ReadOptions);
        }
        catch (JsonException exception)
        {
            return SettingsReadResult.Corrupt($"The settings are not valid JSON: {exception.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return SettingsReadResult.Corrupt("The settings must be a JSON object.");
            }

            var reader = new FieldReader(document.RootElement);
            var defaults = AppSettings.Default;
            var settings = new AppSettings
            {
                Language = reader.Read(Fields.Language, TryReadLanguage, defaults.Language),
                WarningEnabled = reader.Read(Fields.WarningEnabled, TryReadBoolean, defaults.WarningEnabled),
                RememberLast = reader.Read(Fields.RememberLast, TryReadBoolean, defaults.RememberLast),
                Sound = reader.Read(Fields.Sound, TryReadEnum<EndSound>, SoundBeforeTheChoice(reader)),
                SoundFile = reader.Read(Fields.SoundFile, TryReadString, defaults.SoundFile),
                FailedSoundFile = reader.Read(Fields.FailedSoundFile, TryReadString, defaults.FailedSoundFile),
                LastMode = reader.Read(Fields.LastMode, TryReadEnum<TimeMode>, defaults.LastMode),
                LastAmount = reader.Read(Fields.LastAmount, TryReadInt32, defaults.LastAmount),
                LastUnit = reader.Read(Fields.LastUnit, TryReadEnum<TimeUnit>, defaults.LastUnit),
                LastAction = reader.Read(Fields.LastAction, TryReadEnum<PowerAction>, defaults.LastAction),
                LastAtTime = reader.Read(Fields.LastAtTime, TryReadClockTime, defaults.LastAtTime),
                LastRepeat = reader.Read(Fields.LastRepeat, TryReadWeekDays, defaults.LastRepeat),
                ScheduleDays = reader.Read(Fields.ScheduleDays, TryReadWeekDays, defaults.ScheduleDays),
                ScheduleTime = reader.Read(Fields.ScheduleTime, TryReadClockTime, defaults.ScheduleTime),
                ScheduleAction = reader.Read(Fields.ScheduleAction, TryReadEnum<PowerAction>, defaults.ScheduleAction),
            };

            var lastDelay = CountdownInput.ParseDelay(settings.LastAmount.ToString(CultureInfo.InvariantCulture), settings.LastUnit);
            if (!lastDelay.IsValid)
            {
                reader.Problems.Add($"'{Fields.LastAmount}' {settings.LastAmount} {settings.LastUnit} is outside 10 s to 99 h; the default is used.");
                settings = settings with { LastAmount = defaults.LastAmount, LastUnit = defaults.LastUnit };
            }

            return new SettingsReadResult(settings, reader.Problems, IsCorrupt: false);
        }
    }

    /// <summary>
    /// A file written before the sound choice keeps what it said: the lullaby only when it was on, so
    /// Somnus never starts making a sound after an update (#59).
    /// </summary>
    private static EndSound SoundBeforeTheChoice(FieldReader reader) =>
        reader.Read(Fields.LullabyEnabled, TryReadBoolean, fallback: false) ? EndSound.Lullaby : EndSound.None;

    private static bool TryReadString(JsonElement element, out string value)
    {
        value = element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : string.Empty;
        return element.ValueKind == JsonValueKind.String;
    }

    private static bool TryReadBoolean(JsonElement element, out bool value)
    {
        value = element.ValueKind == JsonValueKind.True;
        return element.ValueKind is JsonValueKind.True or JsonValueKind.False;
    }

    private static bool TryReadInt32(JsonElement element, out int value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }

    private static bool TryReadWeekDays(JsonElement element, out WeekDays value)
    {
        value = WeekDays.None;
        if (!TryReadInt32(element, out var number) || number < 0 || number > (int)WeekDays.EveryDay)
        {
            return false;
        }

        value = (WeekDays)number;
        return true;
    }

    private static bool TryReadLanguage(JsonElement element, out AppLanguage value)
    {
        value = AppLanguage.English;
        return element.ValueKind == JsonValueKind.String
            && AppLanguageExtensions.TryFromCultureName(element.GetString(), out value);
    }

    private static bool TryReadEnum<TEnum>(JsonElement element, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        // Only names count; numbers such as "1" would silently map to arbitrary values.
        var text = element.GetString();
        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
            {
                value = Enum.Parse<TEnum>(name);
                return true;
            }
        }

        return false;
    }

    private static bool TryReadClockTime(JsonElement element, out TimeOnly value)
    {
        value = TimeOnly.MinValue;
        return element.ValueKind == JsonValueKind.String
            && TimeOnly.TryParseExact(element.GetString(), ClockTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private static class Fields
    {
        public const string SchemaVersion = "schemaVersion";
        public const string Language = "language";
        public const string WarningEnabled = "warningEnabled";
        public const string RememberLast = "rememberLast";
        public const string LullabyEnabled = "lullabyEnabled";
        public const string Sound = "sound";
        public const string SoundFile = "soundFile";
        public const string FailedSoundFile = "failedSoundFile";
        public const string LastMode = "lastMode";
        public const string LastAmount = "lastAmount";
        public const string LastUnit = "lastUnit";
        public const string LastAction = "lastAction";
        public const string LastAtTime = "lastAtTime";
        public const string LastRepeat = "lastRepeat";
        public const string ScheduleDays = "scheduleDays";
        public const string ScheduleTime = "scheduleTime";
        public const string ScheduleAction = "scheduleAction";
    }

    private delegate bool TryRead<T>(JsonElement element, out T value);

    /// <summary>
    /// Looks up fields by name, ignoring case, and records every value it had to replace.
    /// </summary>
    private sealed class FieldReader
    {
        private readonly Dictionary<string, JsonElement> _fields = new(StringComparer.OrdinalIgnoreCase);

        public FieldReader(JsonElement root)
        {
            foreach (var property in root.EnumerateObject())
            {
                _fields[property.Name] = property.Value;
            }
        }

        public List<string> Problems { get; } = [];

        public T Read<T>(string name, TryRead<T> tryRead, T fallback)
        {
            if (!_fields.TryGetValue(name, out var element))
            {
                return fallback;
            }

            if (tryRead(element, out var value))
            {
                return value;
            }

            Problems.Add($"'{name}' has an invalid value ({element.GetRawText()}); the default is used.");
            return fallback;
        }
    }
}
