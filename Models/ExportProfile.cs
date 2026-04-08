using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskLoggerApp.Models;

/// <summary>
/// A named export destination.  The
/// <see cref="AppSettings.ActiveProfileName">active profile</see> is used
/// for all report exports and the Friday end-of-day reminder.
///
/// Reports are written to:
///   <c>{ExportFolder}/{weekKey}/TaskReport_{weekKey}.csv</c>
///
/// When <see cref="ExportFolder"/> is <c>null</c> / empty, or when the
/// folder is missing or unwritable, <see cref="Services.ReportService"/>
/// falls back to the internal app-data reports directory automatically.
/// </summary>
public partial class ExportProfile : ObservableObject
{
    /// <summary>Display name shown in the Settings profile list.</summary>
    [ObservableProperty]
    private string _name = "Default";

    /// <summary>
    /// Absolute path of the root folder where per-week report subfolders
    /// are created.  <c>null</c> or empty means "use the internal default".
    /// </summary>
    [ObservableProperty]
    private string? _exportFolder;
}
