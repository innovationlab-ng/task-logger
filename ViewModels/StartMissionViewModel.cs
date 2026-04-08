using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskLoggerApp.Models;

namespace TaskLoggerApp.ViewModels;

/// <summary>
/// Drives the "Start Mission" window.
/// The user supplies a mission name, optional description, and a duration
/// (hours + minutes). On confirmation the <c>onMissionStarted</c> callback
/// receives the constructed <see cref="ActiveMission"/> and the window closes.
/// </summary>
public partial class StartMissionViewModel : ViewModelBase
{
    private readonly Action<ActiveMission> _onMissionStarted;

    /// <summary>Raised when the VM wants the hosting window to close.</summary>
    public event EventHandler? RequestClose;

    // ── Constructor ───────────────────────────────────────────────────────────

    public StartMissionViewModel(Action<ActiveMission> onMissionStarted)
    {
        _onMissionStarted = onMissionStarted;
    }

    // ── Bound properties ──────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string _missionName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>Hour component of the planned mission duration.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private decimal _durationHours = 0;

    /// <summary>Minute component of the planned mission duration.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private decimal _durationMinutes = 0;

    // ── CanExecute ────────────────────────────────────────────────────────────

    private bool CanStart()
    {
        if (string.IsNullOrWhiteSpace(MissionName)) return false;
        return (DurationHours * 60m) + DurationMinutes > 0m;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        var totalMinutes = (DurationHours * 60m) + DurationMinutes;
        var mission = new ActiveMission
        {
            Name        = MissionName.Trim(),
            Description = Description.Trim(),
            Duration    = TimeSpan.FromMinutes((double)totalMinutes),
        };
        _onMissionStarted(mission);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, EventArgs.Empty);
}
