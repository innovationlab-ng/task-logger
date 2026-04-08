using System;

namespace TaskLoggerApp.Models;

/// <summary>
/// Represents a mission that is currently in progress.
/// Held in-memory in <see cref="ViewModels.AppViewModel"/>; never persisted.
/// </summary>
public sealed class ActiveMission
{
    /// <summary>Short name / title of the mission.</summary>
    public required string Name { get; init; }

    /// <summary>Optional additional context about the mission.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>How long the mission is expected to last.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>When the mission was started.</summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.Now;

    /// <summary>
    /// Returns <see langword="true"/> once
    /// <see cref="StartedAt"/> + <see cref="Duration"/> has elapsed.
    /// </summary>
    public bool IsElapsed => DateTimeOffset.Now >= StartedAt + Duration;

    /// <summary>
    /// How much time remains before the mission elapses.
    /// Returns <see cref="TimeSpan.Zero"/> once the mission has elapsed.
    /// </summary>
    public TimeSpan Remaining
    {
        get
        {
            var r = StartedAt + Duration - DateTimeOffset.Now;
            return r > TimeSpan.Zero ? r : TimeSpan.Zero;
        }
    }
}
