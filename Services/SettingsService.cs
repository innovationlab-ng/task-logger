using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskLoggerApp.Models;

namespace TaskLoggerApp.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON inside the platform
/// application-data folder:
///   macOS/Linux : ~/.config/TaskLogger/settings.json
///   Windows     : %APPDATA%\TaskLogger\settings.json
/// </summary>
public sealed class SettingsService
{
    // ── Paths ─────────────────────────────────────────────────────────────────

    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TaskLogger");

    private static readonly string SettingsPath = Path.Combine(AppDataDir, "settings.json");

    // ── JSON options (shared, thread-safe) ───────────────────────────────────

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented          = true,
        PropertyNameCaseInsensitive = true,
        // Lets TimeSpan round-trip as "hh:mm:ss" strings.
        Converters             = { new TimeSpanConverter() }
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads settings from disk.
    /// </summary>
    /// <param name="isFirstRun">
    ///   <c>true</c> when the settings file did not exist (first launch) or
    ///   when it was corrupt and defaults were substituted.
    /// </param>
    /// <returns>
    ///   The persisted <see cref="AppSettings"/>, or defaults if the file is
    ///   absent or unreadable.
    /// </returns>
    public AppSettings Load(out bool isFirstRun)
    {
        if (!File.Exists(SettingsPath))
        {
            isFirstRun = true;
            return AppSettings.CreateDefaults();
        }

        try
        {
            var json     = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

            if (settings is null)
            {
                isFirstRun = true;
                return AppSettings.CreateDefaults();
            }

            // Re-apply clamp in case the file was hand-edited.
            settings.RetentionWeeks = settings.RetentionWeeks;

            isFirstRun = false;
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable — fall back to defaults silently.
            isFirstRun = true;
            return AppSettings.CreateDefaults();
        }
    }

    /// <summary>
    /// Persists <paramref name="settings"/> to disk.
    /// Enforces <see cref="AppSettings.MaxRetentionWeeks"/> before writing.
    /// </summary>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Enforce the cap before serialisation.
        settings.RetentionWeeks = settings.RetentionWeeks;

        Directory.CreateDirectory(AppDataDir);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Full path of the settings file (useful for diagnostics).</summary>
    public static string SettingsFilePath => SettingsPath;
}

// ── TimeSpan JSON converter ───────────────────────────────────────────────────

/// <summary>
/// Serialises <see cref="TimeSpan"/> as an "hh:mm:ss" string so the JSON
/// file is human-readable.
/// </summary>
internal sealed class TimeSpanConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();

        if (TimeSpan.TryParse(raw, out var result))
            return result;

        throw new JsonException($"Cannot convert \"{raw}\" to TimeSpan. Expected format: hh:mm:ss");
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(@"hh\:mm\:ss"));
}
