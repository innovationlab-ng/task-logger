using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TaskLoggerApp.Services;

/// <summary>
/// Reads task-log data from the weekly CSV directories produced by
/// <see cref="TaskLogService"/> and consolidates them into report files.
///
/// Storage layout (input):
///   {AppData}/TaskLogger/logs/{weekKey}/{yyyy-MM-dd}.csv
///
/// Report output:
///   {AppData}/TaskLogger/reports/TaskReport_{weekKey}.csv
///   Header: Date,Time,Task,Description
///
/// Memory policy: source files are read line-by-line via <see cref="StreamReader"/>.
/// No rows are ever held in a collection — each line is copied to the output
/// writer and then discarded.
/// </summary>
public sealed class ReportService
{
    // ── Paths ─────────────────────────────────────────────────────────────────

    private static readonly string LogsRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TaskLogger", "logs");

    private static readonly string ReportsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TaskLogger", "reports");

    private const string ReportHeader = "Date,Time,Task,Description,Duration";

    // ── Folder health helpers ──────────────────────────────────────────────

    /// <summary>Exposed as the fallback target when <see cref="ResolveExportRoot"/>
    /// finds no healthy user-configured folder.</summary>
    internal static readonly string InternalReportsDir = ReportsDir;

    /// <summary>
    /// Returns <c>true</c> when <paramref name="path"/> exists and a temporary
    /// probe file can be created inside it (verifies actual write permission).
    /// </summary>
    public static bool IsFolderWritable(string path)
    {
        if (!Directory.Exists(path))
            return false;
        try
        {
            var probe = Path.Combine(path, $".tl_probe_{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns <paramref name="preferred"/> when it is non-empty and writable;
    /// otherwise falls back to the Desktop, then to the internal app-data
    /// reports directory as a last resort.
    /// Pass <c>AppSettings.ActiveProfile.ExportFolder</c> as the argument.
    /// </summary>
    public static string ResolveExportRoot(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred) && IsFolderWritable(preferred))
            return preferred;

        // Desktop is the preferred fallback so reports are easy to find.
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (IsFolderWritable(desktop))
            return desktop;

        // Last resort: internal app-data directory (always writable).
        return InternalReportsDir;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the ISO week keys for which a log directory exists on disk,
    /// sorted descending (most-recent first). Only directory names are read —
    /// no file contents are loaded.
    /// </summary>
    public IReadOnlyList<string> GetAvailableWeekKeys()
    {
        if (!Directory.Exists(LogsRoot))
            return Array.Empty<string>();

        var keys = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(LogsRoot))
        {
            var name = Path.GetFileName(dir);
            if (IsValidWeekKey(name))
                keys.Add(name);
        }

        // Descending lexicographic order is correct for "YYYY-Www" keys.
        keys.Sort(static (a, b) => string.Compare(b, a, StringComparison.Ordinal));
        return keys;
    }

    /// <summary>
    /// Consolidates every daily CSV in the <paramref name="weekKey"/> log
    /// directory into <c>{exportRoot}/{weekKey}/TaskReport_{weekKey}.csv</c>.
    ///
    /// Call <see cref="ResolveExportRoot"/> first to obtain a healthy export root.
    /// </summary>
    /// <returns>Absolute path of the written report file.</returns>
    /// <exception cref="DirectoryNotFoundException">
    ///   Thrown when the log directory for <paramref name="weekKey"/> does not exist.
    /// </exception>
    public string ExportWeek(string weekKey, string exportRoot)
    {
        var weekDir = Path.Combine(LogsRoot, weekKey);
        if (!Directory.Exists(weekDir))
            throw new DirectoryNotFoundException($"No log directory found for week: {weekKey}");

        // Per-week subfolder under the chosen export root.
        var outputDir  = Path.Combine(exportRoot, weekKey);
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, $"TaskReport_{weekKey}.csv");

        // Overwrite any existing report for this week.
        using var writer = new StreamWriter(outputPath, append: false, Encoding.UTF8);
        writer.WriteLine(ReportHeader);

        // Process daily files in chronological order.
        var dailyFiles = Directory.EnumerateFiles(weekDir, "????-??-??.csv")
            .OrderBy(static f => f, StringComparer.Ordinal);

        foreach (var filePath in dailyFiles)
        {
            var date = Path.GetFileNameWithoutExtension(filePath);

            using var reader = new StreamReader(filePath, Encoding.UTF8);
            reader.ReadLine(); // Skip the daily header: "Time,Task,Description"

            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                writer.WriteLine($"{date},{line}");
            }
        }

        return outputPath;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns <c>true</c> for names of the form <c>"YYYY-Www"</c>.</summary>
    private static bool IsValidWeekKey(string name) =>
        name.Length == 8
        && name[4] == '-'
        && name[5] == 'W'
        && int.TryParse(name.AsSpan(0, 4), out _)
        && int.TryParse(name.AsSpan(6, 2), out _);
}
