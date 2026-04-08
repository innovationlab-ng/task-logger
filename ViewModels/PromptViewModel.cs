using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskLoggerApp.Models;
using TaskLoggerApp.Services;

namespace TaskLoggerApp.ViewModels;

/// <summary>
/// Drives the "Log a Task" / "Log a Mission" prompt window.
/// <para>
/// Two construction paths exist:
/// <list type="bullet">
///   <item>Regular task prompt — plain constructor; user fills in all fields.</item>
///   <item>Mission prompt — pre-filled constructor; a 10-second countdown
///     auto-saves unless the user clicks <b>Edit</b> to cancel it.</item>
/// </list>
/// </para>
/// Memory policy: no log history is held here; each Save writes one record
/// to disk via <see cref="TaskLogService"/> and immediately forgets it.
/// </summary>
public partial class PromptViewModel : ViewModelBase
{
    private readonly TaskLogService _logService;
    private DispatcherTimer? _countdownTimer;

    /// <summary>Raised when the VM wants the hosting window to close.</summary>
    public event EventHandler? RequestClose;

    /// <summary>
    /// Raised after a mission entry is saved, so <c>App.axaml.cs</c> can
    /// open the Start Mission window for the next mission.
    /// </summary>
    public event EventHandler? MissionSaved;

    // ── Regular task constructor ─────────────────────────────────────────────

    public PromptViewModel(TaskLogService logService)
    {
        _logService = logService;
    }

    // ── Mission constructor (pre-filled + 10 s auto-save countdown) ──────────

    public PromptViewModel(TaskLogService logService, string missionName, string description, int durationMinutes)
    {
        _logService     = logService;
        _task           = missionName;
        _description    = description;
        DurationMinutes = durationMinutes;
        IsMissionPrompt = true;
        StartCountdown();
    }

    // ── Bound properties ─────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _task = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// Feedback message shown after a successful save (task prompts only).
    /// Cleared the next time the user starts typing in the Task field.
    /// </summary>
    [ObservableProperty]
    private string _savedMessage = string.Empty;

    /// <summary>Planned mission duration in minutes; <see langword="null"/> for regular tasks.</summary>
    public int? DurationMinutes { get; }

    /// <summary><see langword="true"/> when this prompt was opened at the end of a mission.</summary>
    public bool IsMissionPrompt { get; }

    /// <summary>Seconds remaining before auto-save fires.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountdownText))]
    private int _countdownSeconds;

    /// <summary><see langword="true"/> while the auto-save countdown is ticking.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotCountingDown))]
    private bool _isCountingDown;

    /// <summary>Inverse of <see cref="IsCountingDown"/>; drives Close button visibility.</summary>
    public bool IsNotCountingDown => !IsCountingDown;

    /// <summary>Label shown in the countdown banner.</summary>
    public string CountdownText => $"\u23f1\u2002Auto-saving in {CountdownSeconds}s\u2026";

    // Clear the "Saved" banner as soon as the user starts a new entry.
    partial void OnTaskChanged(string value)
    {
        if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(SavedMessage))
            SavedMessage = string.Empty;
    }

    // ── Countdown ────────────────────────────────────────────────────────────

    private void StartCountdown()
    {
        CountdownSeconds = 10;
        IsCountingDown   = true;
        _countdownTimer  = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) =>
        {
            CountdownSeconds--;
            if (CountdownSeconds <= 0)
            {
                StopCountdown();
                if (CanSave()) Save();
            }
        };
        _countdownTimer.Start();
    }

    private void StopCountdown()
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;
        IsCountingDown  = false;
    }

    // ── CanExecute ────────────────────────────────────────────────────────────

    private bool CanSave() => !string.IsNullOrWhiteSpace(Task);

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        StopCountdown();

        _logService.Append(new TaskEntry(
            Timestamp:       DateTimeOffset.Now,
            Task:            Task.Trim(),
            Description:     Description.Trim(),
            DurationMinutes: DurationMinutes));

        if (IsMissionPrompt)
        {
            // Mission log — close window immediately after saving.
            RequestClose?.Invoke(this, EventArgs.Empty);
            MissionSaved?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            // Regular task prompt — clear fields and confirm; keep window open.
            Task         = string.Empty;
            Description  = string.Empty;
            SavedMessage = "\u2713 Saved";
        }
    }

    /// <summary>Cancels the countdown so the user can freely edit the pre-filled fields.</summary>
    [RelayCommand]
    private void Edit() => StopCountdown();

    [RelayCommand]
    private void Close()
    {
        StopCountdown();
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
