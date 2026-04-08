using System;

namespace TaskLoggerApp.Models;

/// <summary>
/// A single task-log entry. Immutable after creation.
/// Serialised as one CSV row by <see cref="TaskLoggerApp.Services.TaskLogService"/>.
/// </summary>
public sealed record TaskEntry(
    /// <summary>Local moment the entry was saved.</summary>
    DateTimeOffset Timestamp,

    /// <summary>Short task title entered by the user.</summary>
    string Task,

    /// <summary>Optional longer description / notes.</summary>
    string Description,

    /// <summary>
    /// Planned mission duration in minutes.
    /// <see langword="null"/> for regular task entries; populated for mission entries.
    /// </summary>
    int? DurationMinutes = null);
