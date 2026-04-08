using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaskLoggerApp.Models;

/// <summary>
/// Persisted user preferences for TaskLogger.
/// </summary>
public sealed class AppSettings
{
    // ── Defaults ──────────────────────────────────────────────────────────────

    public static readonly TimeSpan DefaultWorkStart      = new(9, 0, 0);   // 09:00
    public static readonly TimeSpan DefaultWorkEnd        = new(17, 0, 0);  // 17:00
    public const  int               DefaultIntervalMinutes = 120;
    public const  int               DefaultRetentionWeeks  = 3;
    public const  int               MaxRetentionWeeks      = 5;

    /// <summary>Friday end-of-day prompt is always fixed at 17:00.</summary>
    public static readonly TimeSpan FridayPromptTime = new(17, 0, 0);

    // ── Persisted fields ──────────────────────────────────────────────────────

    /// <summary>Start of the working day. Prompts won't fire before this time.</summary>
    public TimeSpan WorkStart { get; set; } = DefaultWorkStart;

    /// <summary>End of the working day. Prompts won't fire after this time.</summary>
    public TimeSpan WorkEnd { get; set; } = DefaultWorkEnd;

    /// <summary>How often (in minutes) the periodic task-log prompt fires.</summary>
    public int IntervalMinutes { get; set; } = DefaultIntervalMinutes;

    /// <summary>
    /// How many weeks of log data to keep.
    /// Clamped to [1, <see cref="MaxRetentionWeeks"/>] on every get/set.
    /// </summary>
    private int _retentionWeeks = DefaultRetentionWeeks;
    public int RetentionWeeks
    {
        get => _retentionWeeks;
        set => _retentionWeeks = Math.Clamp(value, 1, MaxRetentionWeeks);
    }
    // ── Behaviour ────────────────────────────────────────────────────────────

    /// <summary>
    /// Describes the user's preferred logging workflow.
    /// In <see cref="LogMode.Task"/> mode the user logs tasks reactively when prompted.
    /// In <see cref="LogMode.Mission"/> mode the user proactively declares a mission and
    /// duration upfront; automatic prompts are suppressed while a mission is active
    /// regardless of which mode is selected.
    /// </summary>
    public LogMode LogMode { get; set; } = LogMode.Task;

    /// <summary>
    /// When <see langword="false"/> the scheduler still ticks but no prompt window
    /// is ever shown. Useful when the user wants to pause interruptions temporarily.
    /// </summary>
    public bool PopupsEnabled { get; set; } = true;

    /// <summary>
    /// When <see langword="true"/> a small always-on-top floating icon overlay
    /// is shown so the user can quickly open the prompt from anywhere on screen.
    /// </summary>
    public bool FloatingIconEnabled { get; set; } = false;

    /// <summary>
    /// When <see langword="true"/> the app polls every few seconds for known
    /// screen-sharing processes (Zoom, Teams) and automatically pauses prompts
    /// while a share is active.
    /// </summary>
    public bool AutoDetectScreenSharing { get; set; } = true;

    /// <summary>
    /// When <see langword="true"/> the app is registered to start automatically
    /// when the user logs in (macOS LaunchAgent / Windows registry).
    /// </summary>
    public bool LaunchAtLogin { get; set; } = false;

    // ── Export profiles ──────────────────────────────────────────────────

    /// <summary>All configured export destinations. Always contains at least one entry.</summary>
    public List<ExportProfile> ExportProfiles { get; set; } = [new ExportProfile()];

    /// <summary>Name of the profile used for all exports and the Friday reminder.</summary>
    public string ActiveProfileName { get; set; } = "Default";

    /// <summary>
    /// Returns the active <see cref="ExportProfile"/>, falling back to the first
    /// profile (or a fresh default) if the named profile no longer exists.
    /// Not serialised — computed on each access.
    /// </summary>
    [JsonIgnore]
    public ExportProfile ActiveProfile =>
        ExportProfiles.Find(p => p.Name == ActiveProfileName)
        ?? (ExportProfiles.Count > 0 ? ExportProfiles[0] : new ExportProfile());
    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>Returns a new instance populated with all default values.</summary>
    public static AppSettings CreateDefaults() => new()
    {
        ExportProfiles = [new ExportProfile
        {
            Name         = "Default",
            ExportFolder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        }],
    };
}
