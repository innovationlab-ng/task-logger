using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskLoggerApp.Models;
using TaskLoggerApp.Services;

namespace TaskLoggerApp.ViewModels;

/// <summary>
/// Drives the first-run (and re-open) Setup window.
/// Uses <c>decimal?</c> for numeric fields so they bind cleanly to
/// <c>NumericUpDown.Value</c> (which is <c>decimal?</c>) with compiled bindings.
/// </summary>
public partial class SetupViewModel : ObservableValidator
{
    private readonly SettingsService _settingsService;
    private readonly Action? _onSetupComplete;

    /// <summary>Raised when the VM wants the hosting window to close itself.</summary>
    public event EventHandler? RequestClose;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SetupViewModel(
        SettingsService settingsService,
        AppSettings? initial = null,
        Action? onSetupComplete = null)
    {
        _settingsService  = settingsService;
        _onSetupComplete  = onSetupComplete;

        var s             = initial ?? AppSettings.CreateDefaults();
        _workStartTime    = s.WorkStart;
        _workEndTime      = s.WorkEnd;
        _intervalMinutes  = s.IntervalMinutes;
        _retentionWeeks   = s.RetentionWeeks;
    }

    // ── Bound properties ──────────────────────────────────────────────────────

    /// <summary>Binds to <c>TimePicker.SelectedTime</c> (both are <c>TimeSpan?</c>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationError))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private TimeSpan? _workStartTime;

    /// <summary>Binds to <c>TimePicker.SelectedTime</c> (both are <c>TimeSpan?</c>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationError))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private TimeSpan? _workEndTime;

    /// <summary>Binds to <c>NumericUpDown.Value</c> (both are <c>decimal?</c>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationError))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private decimal? _intervalMinutes;

    /// <summary>Binds to <c>NumericUpDown.Value</c> (both are <c>decimal?</c>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationError))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private decimal? _retentionWeeks;

    // ── Validation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the first validation failure message, or <c>null</c> when all
    /// fields are valid.  Bound to a red <c>TextBlock</c> in the view.
    /// </summary>
    public string? ValidationError
    {
        get
        {
            if (WorkStartTime is null)
                return "Work Start time is required.";

            if (WorkEndTime is null)
                return "Work End time is required.";

            if (WorkEndTime.Value <= WorkStartTime.Value)
                return "Work End must be later than Work Start.";

            if (IntervalMinutes is null or <= 0)
                return "Prompt interval must be at least 1 minute.";

            if (RetentionWeeks is null or < 1 or > AppSettings.MaxRetentionWeeks)
                return $"Retention must be between 1 and {AppSettings.MaxRetentionWeeks} weeks.";

            return null;
        }
    }

    // ── CanExecute guard ──────────────────────────────────────────────────────

    private bool IsSaveEnabled() =>
        ValidationError is null
        && WorkStartTime.HasValue
        && WorkEndTime.HasValue
        && IntervalMinutes.HasValue
        && RetentionWeeks.HasValue;

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(IsSaveEnabled))]
    private void Save()
    {
        var settings = new AppSettings
        {
            WorkStart      = WorkStartTime!.Value,
            WorkEnd        = WorkEndTime!.Value,
            IntervalMinutes = (int)IntervalMinutes!.Value,
            RetentionWeeks  = (int)RetentionWeeks!.Value,
        };

        _settingsService.Save(settings);
        _onSetupComplete?.Invoke();
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
