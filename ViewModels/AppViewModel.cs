using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskLoggerApp.Models;
using TaskLoggerApp.Services;

namespace TaskLoggerApp.ViewModels;

public partial class AppViewModel : ViewModelBase
{
    private readonly SettingsService  _settingsService;
    private readonly TaskLogService   _logService;
    private readonly ReportService    _reportService;
    private readonly RetentionService _retentionService;
    private AppSettings               _settings;

    /// <summary>
    /// Raised when "Show Prompt" is activated so that <c>App.axaml.cs</c>
    /// can create and show <c>PromptWindow</c> without VM knowing about Views.
    /// </summary>
    public event EventHandler? OpenPromptRequested;

    /// <summary>
    /// Raised when "Reports" is activated so that <c>App.axaml.cs</c>
    /// can create and show <c>ReportsWindow</c> without VM knowing about Views.
    /// </summary>
    public event EventHandler? OpenReportsRequested;

    /// <summary>
    /// Raised when the tray "Settings" item is activated so that
    /// <c>App.axaml.cs</c> can create and show the <c>SetupWindow</c>
    /// without the ViewModel knowing about the View layer.
    /// </summary>
    public event EventHandler? OpenSettingsRequested;

    /// <summary>
    /// Raised at the end of <see cref="ReloadSettings"/> so that
    /// <c>App.axaml.cs</c> can forward the change to the scheduler.
    /// </summary>
    public event EventHandler? SettingsReloaded;

    /// <summary>
    /// Raised when the user triggers "Start Mission" from the tray so that
    /// <c>App.axaml.cs</c> can create and show <c>StartMissionWindow</c>.
    /// </summary>
    public event EventHandler? StartMissionRequested;

    /// <summary>
    /// Raised when a mission ends (manually or by elapsed time) so that
    /// <c>App.axaml.cs</c> can open the pre-filled mission-log prompt window.
    /// </summary>
    public event EventHandler<ActiveMission>? MissionCompletedRequested;

    // ── Constructor ───────────────────────────────────────────────────────────

    public AppViewModel(SettingsService settingsService, TaskLogService logService, ReportService reportService, RetentionService retentionService, AppSettings settings)
    {
        _settingsService  = settingsService;
        _logService       = logService;
        _reportService    = reportService;
        _retentionService = retentionService;
        _settings         = settings;
    }

    /// <summary>Replaces the in-memory settings after the Setup window saves.</summary>
    public void ReloadSettings(AppSettings settings)
    {
        _settings = settings;
        _retentionService.UpdateRetention(settings.RetentionWeeks);
        _retentionService.Apply();
        SettingsReloaded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Current live settings; used by <c>SchedulerService</c> via a delegate.</summary>
    internal AppSettings CurrentSettings => _settings;

    // ── Active mission ────────────────────────────────────────────────────────

    /// <summary>
    /// The currently running mission, or <see langword="null"/> when none is active.
    /// Raising property-changed notifies the tray NativeMenu bindings.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMissionActive))]
    [NotifyCanExecuteChangedFor(nameof(EndMissionCommand))]
    private ActiveMission? _currentMission;

    /// <summary>
    /// <see langword="true"/> when a mission has been started and its duration has
    /// not yet elapsed.  Used by <c>App.axaml.cs</c> to suppress prompts and by
    /// the tray menu to enable/disable the End Mission item.
    /// </summary>
    public bool IsMissionActive => CurrentMission is { IsElapsed: false };

    /// <summary>
    /// Sets (or clears) the active mission.
    /// When clearing, raises <see cref="MissionCompletedRequested"/> so
    /// <c>App.axaml.cs</c> can open the pre-filled mission-log prompt.
    /// </summary>
    public void SetCurrentMission(ActiveMission? mission)
    {
        if (mission is null && CurrentMission is not null)
            MissionCompletedRequested?.Invoke(this, CurrentMission);

        CurrentMission = mission;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ShowPrompt() => OpenPromptRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenSettings() => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenReports() => OpenReportsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void StartMission() => StartMissionRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(IsMissionActive))]
    private void EndMission() => SetCurrentMission(null);

    // ── Pause / do-not-disturb ────────────────────────────────────────────────

    /// <summary>
    /// When <see langword="true"/> all automatic prompts and the floating icon
    /// are suppressed.  Toggled via the tray menu or set automatically by the
    /// screen-share detector.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseMenuLabel))]
    private bool _isPaused;

    /// <summary>
    /// Tray menu label that flips between Pause and Resume depending on
    /// <see cref="IsPaused"/>.
    /// </summary>
    public string PauseMenuLabel => IsPaused ? "\u25b6\u2002Resume Prompts" : "\u23f8\u2002Pause Prompts";

    /// <summary>Toggles <see cref="IsPaused"/>.</summary>
    [RelayCommand]
    private void TogglePause() => IsPaused = !IsPaused;

    [RelayCommand]
    private void Quit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            lifetime.Shutdown();
    }

    // ── Accessors for App.axaml.cs ────────────────────────────────────────────

    /// <summary>Exposes <see cref="TaskLogService"/> so App.axaml.cs can pass it to PromptViewModel.</summary>
    internal TaskLogService LogService => _logService;

    /// <summary>Exposes <see cref="ReportService"/> so App.axaml.cs can pass it to ReportsViewModel.</summary>
    internal ReportService ReportService => _reportService;

    /// <summary>Exposes <see cref="RetentionService"/> so App.axaml.cs can pass it to ReportsViewModel.</summary>
    internal RetentionService RetentionService => _retentionService;
}
