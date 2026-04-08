using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Text;
using TaskLoggerApp.Models;

namespace TaskLoggerApp.Services;

/// <summary>
/// Append-only task-log store that writes CSV files organised by ISO week and day:
///
///   {AppData}/TaskLogger/logs/{ISO_WEEK_KEY}/{yyyy-MM-dd}.csv
///
///   e.g.  ~/.config/TaskLogger/logs/2026-W13/2026-03-26.csv
///
/// File format (RFC 4180 CSV):
///   Time,Task,Description
///   09:15:32,Fixed login bug,"Updated handler, handle null tokens"
///
/// Memory policy: every <see cref="Append"/> call opens the file, writes one
/// row, then immediately closes it.  No log rows are ever held in memory.
/// Retention is enforced separately by <see cref="RetentionService"/>.
/// </summary>
public sealed class TaskLogService
{
    // ── Paths ─────────────────────────────────────────────────────────────────

    private static readonly string LogsRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TaskLogger", "logs");

    private const string CsvHeader = "Time,Task,Description,Duration";


    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Appends <paramref name="entry"/> as a single CSV row to the daily file
    /// for <c>entry.Timestamp.LocalDateTime</c>, creating the file and writing
    /// the header if it does not yet exist.  Then prunes old week directories.
    /// </summary>
    public void Append(TaskEntry entry)
    {
        var local   = entry.Timestamp.LocalDateTime;
        var weekDir = Path.Combine(LogsRoot, GetIsoWeekKey(local));
        Directory.CreateDirectory(weekDir);

        var filePath = Path.Combine(weekDir, $"{local:yyyy-MM-dd}.csv");

        // FileMode.Append creates the file if absent, otherwise seeks to end.
        // Checking Position==0 after opening is atomic with the open itself,
        // removing the separate File.Exists check and any TOCTOU window.
        using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, Encoding.UTF8);

        if (stream.Position == 0)
            writer.WriteLine(CsvHeader); // new (or empty) file — write header first

        writer.WriteLine(
            $"{CsvEscape(local.ToString("HH:mm:ss"))}," +
            $"{CsvEscape(entry.Task)}," +
            $"{CsvEscape(entry.Description)}," +
            $"{CsvEscape(entry.DurationMinutes?.ToString() ?? string.Empty)}");
    }

    // ── ISO week helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the ISO 8601 week key for <paramref name="date"/>,
    /// e.g. <c>"2026-W13"</c>.
    /// Uses <see cref="ISOWeek.GetYear"/> (not <c>date.Year</c>) so that
    /// late-December dates that belong to week 1 of the next year are
    /// keyed correctly.
    /// </summary>
    public static string GetIsoWeekKey(DateTime date)
    {
        var week = ISOWeek.GetWeekOfYear(date);
        var year = ISOWeek.GetYear(date);      // ISO year, may differ from calendar year
        return $"{year}-W{week:D2}";
    }

    // ── RFC 4180 CSV field escaping ───────────────────────────────────────────

    /// <summary>Characters that require a field to be quoted (RFC 4180 §2.6–2.7).</summary>
    private static readonly SearchValues<char> CsvSpecialChars =
        SearchValues.Create(",\"\n\r");

    /// <summary>
    /// Wraps a field in double-quotes if it contains a comma, double-quote,
    /// CR, or LF; doubles any embedded double-quotes (RFC 4180 §2.7).
    /// </summary>
    private static string CsvEscape(string value)
    {
        if (value.AsSpan().IndexOfAny(CsvSpecialChars) < 0)
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

}
