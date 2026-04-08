using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskLoggerApp.Models;
using TaskLoggerApp.Services;

namespace TaskLoggerApp.ViewModels;

/// <summary>
/// Drives the Settings window that is accessible from the tray menu after
/// the initial setup has been completed.
///
/// On save it:
///   1. Persists settings to disk via <see cref="SettingsService"/>.
///   2. Invokes <c>onSaved</c> with the new <see cref="AppSettings"/>
///      so that the caller can call <see cref="AppViewModel.ReloadSettings"/>,
///      which chains retention cleanup and raises
///      <see cref="AppViewModel.SettingsReloaded"/> → scheduler reschedule.
///   3. Requests the hosting window to close.
///
/// Uses <c>decimal?</c> for numeric fields so they bind cleanly to
/// <c>NumericUpDown.Value</c> (which is <c>decimal?</c>) with compiled bindings.
/// </summary>
public partial class SettingsViewModel : ObservableValidator
{
    private readonly SettingsService     _settingsService;
    private readonly Action<AppSettings>? _onSaved;

    /// <summary>Raised when the VM wants the hosting window to close itself.</summary>
    public event EventHandler? RequestClose;

    /// <summary>
    /// Folder-picker delegate injected by <c>SettingsWindow</c> after construction.
    /// Invoked by <see cref="PickFolderCommand"/> to open the OS folder picker.
    /// </summary>
    internal Func<Task<string?>>? FolderPicker { get; set; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public SettingsViewModel(
        SettingsService      settingsService,
        AppSettings?         initial  = null,
        Action<AppSettings>? onSaved  = null)
    {
        _settingsService = settingsService;
        _onSaved         = onSaved;

        var s              = initial ?? AppSettings.CreateDefaults();
        _workStartTime     = s.WorkStart;
        _workEndTime       = s.WorkEnd;
        _intervalMinutes   = s.IntervalMinutes;
        _retentionWeeks    = s.RetentionWeeks;
        _isTaskMode          = s.LogMode == LogMode.Task;
        _popupsEnabled       = s.PopupsEnabled;
        _floatingIconEnabled = s.FloatingIconEnabled;
        _autoDetectScreenSharing = s.AutoDetectScreenSharing;
        _launchAtLogin           = s.LaunchAtLogin;

        Profiles           = new ObservableCollection<ExportProfile>(s.ExportProfiles);
        _activeProfileName = s.ActiveProfileName;
        _selectedProfile   = Profiles.Count > 0 ? Profiles[0] : null;
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

    /// <summary>
    /// Binds to <c>NumericUpDown.Value</c>.
    /// Clamped to [1, <see cref="AppSettings.MaxRetentionWeeks"/>] in
    /// the setter so hand-typed values over 5 are corrected automatically.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationError))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private decimal? _retentionWeeks;

    // Clamp whenever the property changes (source generator calls this).
    partial void OnRetentionWeeksChanged(decimal? value)
    {
        if (value.HasValue)
        {
            var clamped = Math.Clamp(value.Value, 1m, AppSettings.MaxRetentionWeeks);
            if (clamped != value.Value)
                RetentionWeeks = clamped; // re-enter; guard prevents infinite recursion
        }
    }

    // ── Behaviour properties ─────────────────────────────────────────────────

    /// <summary>
    /// <see langword="true"/> when Task mode is selected.
    /// Bound to the "Task mode" RadioButton.
    /// Changing this automatically updates <see cref="IsMissionMode"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMissionMode))]
    private bool _isTaskMode = true;

    /// <summary>Inverse of <see cref="IsTaskMode"/>; bound to the "Mission mode" RadioButton.</summary>
    public bool IsMissionMode
    {
        get => !IsTaskMode;
        set => IsTaskMode = !value;
    }

    /// <summary>
    /// When <see langword="true"/> (default) automatic prompt windows are shown
    /// on each scheduled tick.
    /// </summary>
    [ObservableProperty]
    private bool _popupsEnabled = true;

    /// <summary>
    /// When <see langword="true"/> the small always-on-top floating overlay is shown.
    /// </summary>
    [ObservableProperty]
    private bool _floatingIconEnabled;
    /// <summary>
    /// When <see langword="true"/> the app polls for screen-sharing processes
    /// and auto-pauses prompts.
    /// </summary>
    [ObservableProperty]
    private bool _autoDetectScreenSharing = true;

    /// <summary>When <see langword="true"/> the app registers itself to launch at login.</summary>
    [ObservableProperty]
    private bool _launchAtLogin;
    // ── Profile bound properties ─────────────────────────────────────────────

    /// <summary>All configured export profiles. Bound to the profile ListBox.</summary>
    public ObservableCollection<ExportProfile> Profiles { get; }

    /// <summary>Profile currently highlighted in the list.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PickFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetActiveProfileCommand))]
    private ExportProfile? _selectedProfile;

    /// <summary>Name of the profile currently used for all exports.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetActiveProfileCommand))]
    private string _activeProfileName = "Default";

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
            WorkStart           = WorkStartTime!.Value,
            WorkEnd             = WorkEndTime!.Value,
            IntervalMinutes     = (int)IntervalMinutes!.Value,
            RetentionWeeks      = (int)RetentionWeeks!.Value,
            LogMode                  = IsTaskMode ? LogMode.Task : LogMode.Mission,
            PopupsEnabled            = PopupsEnabled,
            FloatingIconEnabled      = FloatingIconEnabled,
            AutoDetectScreenSharing  = AutoDetectScreenSharing,
            LaunchAtLogin            = LaunchAtLogin,
            ExportProfiles           = [.. Profiles],
            ActiveProfileName        = ActiveProfileName,
        };

        _settingsService.Save(settings);
        _onSaved?.Invoke(settings);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    // ── Profile commands ──────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanPickFolder))]
    private async Task PickFolder()
    {
        if (SelectedProfile is null || FolderPicker is null) return;
        var folder = await FolderPicker();
        if (folder is not null)
            SelectedProfile.ExportFolder = folder; // ExportProfile is ObservableObject → UI updates
    }

    private bool CanPickFolder() => SelectedProfile is not null;

    [RelayCommand]
    private void AddProfile()
    {
        var profile = new ExportProfile { Name = $"Profile {Profiles.Count + 1}" };
        Profiles.Add(profile);
        SelectedProfile = profile;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveProfile))]
    private void RemoveProfile()
    {
        if (SelectedProfile is null) return;
        var wasActive = SelectedProfile.Name == ActiveProfileName;
        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles.Count > 0 ? Profiles[0] : null;
        if (wasActive)
            ActiveProfileName = SelectedProfile?.Name ?? string.Empty;
    }

    private bool CanRemoveProfile() => Profiles.Count > 1 && SelectedProfile is not null;

    [RelayCommand(CanExecute = nameof(CanSetActive))]
    private void SetActiveProfile()
    {
        if (SelectedProfile is null) return;
        ActiveProfileName = SelectedProfile.Name;
    }

    private bool CanSetActive() =>
        SelectedProfile is not null && SelectedProfile.Name != ActiveProfileName;
}
