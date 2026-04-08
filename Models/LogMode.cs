namespace TaskLoggerApp.Models;

/// <summary>
/// Controls how the scheduler prompt behaves.
/// </summary>
public enum LogMode
{
    /// <summary>
    /// Regular interval prompts – the classic task-logging mode.
    /// A window appears at each scheduled tick to capture what the user is working on.
    /// </summary>
    Task,

    /// <summary>
    /// Mission mode: the user declares a mission (name, description, duration).
    /// Scheduler prompts are suppressed while the mission is still active.
    /// Once the duration elapses the next scheduled tick resumes normally.
    /// </summary>
    Mission,
}
