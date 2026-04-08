using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TaskLoggerApp.Models;

namespace TaskLoggerApp.Services;

/// <summary>
/// Centralized retention policy for TaskLogger storage.
///
/// After <see cref="Apply"/> is called it:
///   1. Enumerates all valid ISO-week directories under <c>logs/</c>.
///   2. Sorts them by their true calendar date (not lexicographically) so that
///      cross-year boundaries (e.g. 2025-W52 → 2026-W01) are ordered correctly.
///   3. Keeps the N most-recent week directories (<see cref="_retentionWeeks"/>).
///   4. Deletes every older log directory and, if present, its paired report CSV at
///      <c>reports/TaskReport_{weekKey}.csv</c>.
///
/// <see cref="UpdateRetention"/> should be called whenever the user changes
/// settings so that the next <see cref="Apply"/> call uses the new value.
/// </summary>
public sealed class RetentionService
{
    // ── Paths ─────────────────────────────────────────────────────────────────

    private static readonly string LogsRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TaskLogger", "logs");

    private static readonly string ReportsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TaskLogger", "reports");

    // ── State ─────────────────────────────────────────────────────────────────

    private int _retentionWeeks;

    // ── Constructor ───────────────────────────────────────────────────────────

    public RetentionService(int retentionWeeks = AppSettings.DefaultRetentionWeeks)
        => _retentionWeeks = Math.Clamp(retentionWeeks, 1, AppSettings.MaxRetentionWeeks);

    /// <summary>Updates the live retention window (call after settings save).</summary>
    public void UpdateRetention(int retentionWeeks)
        => _retentionWeeks = Math.Clamp(retentionWeeks, 1, AppSettings.MaxRetentionWeeks);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Enforces the current retention policy.
    /// Weeks beyond the newest <see cref="_retentionWeeks"/> are deleted
    /// together with any paired report CSV.
    /// Safe to call at any time; silently skips locked or already-deleted items.
    /// </summary>
    public void Apply()
    {
        if (!Directory.Exists(LogsRoot))
            return;

        // Build a list of (directory-path, week-key, monday-date) tuples.
        var entries = new List<(string DirPath, string WeekKey, DateTime Monday)>();

        foreach (var dir in Directory.EnumerateDirectories(LogsRoot))
        {
            var key = Path.GetFileName(dir);
            if (TryParseWeekKey(key, out var monday))
                entries.Add((dir, key, monday));
        }

        if (entries.Count <= _retentionWeeks)
            return; // Nothing to prune.

        // Sort newest → oldest by the actual Monday date of each ISO week so
        // that cross-year boundaries (2025-W52 < 2026-W01) are handled correctly.
        entries.Sort(static (a, b) => b.Monday.CompareTo(a.Monday));

        // Skip the N most-recent; delete everything beyond.
        for (int i = _retentionWeeks; i < entries.Count; i++)
        {
            var (dirPath, weekKey, _) = entries[i];

            DeleteLogDirectory(dirPath);
            DeleteInternalReportSubfolder(weekKey);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void DeleteLogDirectory(string dirPath)
    {
        try   { Directory.Delete(dirPath, recursive: true); }
        catch (IOException)            { /* skip if locked */ }
        catch (UnauthorizedAccessException) { }
    }

    private static void DeleteInternalReportSubfolder(string weekKey)
    {
        var subDir = Path.Combine(ReportsDir, weekKey);
        if (!Directory.Exists(subDir))
            return;

        try   { Directory.Delete(subDir, recursive: true); }
        catch (IOException)            { /* skip if locked */ }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Parses a key of the form <c>"YYYY-Www"</c> into the Monday that starts
    /// that ISO week.  Returns <c>false</c> for any unrecognised name.
    /// </summary>
    internal static bool TryParseWeekKey(string key, out DateTime monday)
    {
        monday = default;

        if (key.Length != 8 || key[4] != '-' || key[5] != 'W')
            return false;

        if (!int.TryParse(key.AsSpan(0, 4), out var year) ||
            !int.TryParse(key.AsSpan(6, 2), out var week))
            return false;

        try
        {
            monday = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
