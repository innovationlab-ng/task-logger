using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskLoggerApp.Models;
using TaskLoggerApp.Services;

namespace TaskLoggerApp.ViewModels;

/// <summary>
/// Drives the Reports window.
/// Week keys are loaded fresh from disk on construction and on Refresh —
/// no log row data is ever held in memory.
/// </summary>
public partial class ReportsViewModel : ViewModelBase
{
    private readonly ReportService    _reportService;
    private readonly RetentionService _retentionService;
    private readonly AppSettings      _settings;

    /// <summary>Raised when the VM wants the hosting window to close.</summary>
    public event EventHandler? RequestClose;

    // ── Constructor ──────────────────────────────────────────────────────

    public ReportsViewModel(
        ReportService    reportService,
        RetentionService retentionService,
        AppSettings      settings)
    {
        _reportService    = reportService;
        _retentionService = retentionService;
        _settings         = settings;
        LoadWeekKeys();
    }

    // ── Bound properties ──────────────────────────────────────────────────────

    /// <summary>
    /// ISO week keys present on disk. Populated by <see cref="LoadWeekKeys"/>;
    /// never holds log row data — only folder name strings.
    /// </summary>
    public ObservableCollection<string> WeekKeys { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private string? _selectedWeekKey;

    /// <summary>Feedback line shown below the buttons.</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// <c>true</c> when the last status update was an error, so the view
    /// can colour the message red rather than green.
    /// </summary>
    [ObservableProperty]
    private bool _isStatusError;

    /// <summary>Folder of the most-recently exported report; enables "Open Folder".</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenFolderCommand))]
    private string? _lastExportFolder;

    // ── CanExecute ──────────────────────────────────────────────────────

    private bool CanExport()     => SelectedWeekKey is not null;
    private bool CanOpenFolder() => LastExportFolder is not null && Directory.Exists(LastExportFolder);

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Refresh()
    {
        var prev = SelectedWeekKey;
        LoadWeekKeys();

        // Restore selection if the week is still present after refresh.
        SelectedWeekKey = (prev != null && WeekKeys.Contains(prev)) ? prev : null;

        if (SelectedWeekKey is null)
            StatusMessage = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Export()
    {
        try
        {
            var exportRoot = ReportService.ResolveExportRoot(_settings.ActiveProfile.ExportFolder);
            var path       = _reportService.ExportWeek(SelectedWeekKey!, exportRoot);

            LastExportFolder = Path.GetDirectoryName(path); // per-week subfolder
            StatusMessage    = $"Exported to {path}";
            IsStatusError    = false;

            _retentionService.Apply();

            var prev = SelectedWeekKey;
            LoadWeekKeys();
            SelectedWeekKey = WeekKeys.Contains(prev!) ? prev : null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            IsStatusError = true;
        }
    }

    /// <summary>Opens the export folder in Finder / Explorer / file manager.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private void OpenFolder()
    {
        if (LastExportFolder is null) return;
        Process.Start(new ProcessStartInfo
        {
            FileName        = LastExportFolder,
            UseShellExecute = true,
        });
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke(this, EventArgs.Empty);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads week keys from disk (directory names only) and populates
    /// <see cref="WeekKeys"/>. Called on construction and by Refresh.
    /// </summary>
    private void LoadWeekKeys()
    {
        WeekKeys.Clear();
        foreach (var key in _reportService.GetAvailableWeekKeys())
            WeekKeys.Add(key);
    }
}
