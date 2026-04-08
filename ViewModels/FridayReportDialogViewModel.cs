using System;
using CommunityToolkit.Mvvm.Input;

namespace TaskLoggerApp.ViewModels;

/// <summary>
/// Drives the Friday-end-of-day confirmation dialog.
/// </summary>
public partial class FridayReportDialogViewModel : ViewModelBase
{
    /// <summary>Raised when the VM wants the hosting window to close.</summary>
    public event EventHandler? RequestClose;

    /// <summary>Raised when the user chooses to open the Reports window.</summary>
    public event EventHandler? OpenReportsRequested;

    [RelayCommand]
    private void Yes()
    {
        // First raise Reports, then close so the window is gone before Reports opens.
        OpenReportsRequested?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void No() => RequestClose?.Invoke(this, EventArgs.Empty);
}
