using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using TaskLoggerApp.ViewModels;

namespace TaskLoggerApp.Services;

/// <summary>
/// Polls every <see cref="PollIntervalMs"/> milliseconds for signs that the
/// user is sharing their screen, and sets <see cref="AppViewModel.IsPaused"/>
/// accordingly so prompts and the floating icon are suppressed automatically.
///
/// Detection strategy (macOS):
///   • Zoom: spawns a helper process named "CptHost" only while screen-sharing.
///   • Teams: spawns "MSTeams Helper" with a "--type=renderer" flag for sharing.
///   • Generic: looks for the "screencapturekit" system helper that macOS activates
///     when any app has an active SCStream capture session.
///
/// On Windows / Linux no reliable cross-process detection exists without
/// native API access, so this service is a no-op on those platforms.
///
/// Note: this is best-effort. It won't detect every possible screen-sharing
/// tool, but covers the most common enterprise ones (Teams, Zoom).
/// </summary>
public sealed class ScreenShareMonitor : IDisposable
{
    private const int PollIntervalMs = 8_000; // check every 8 seconds

    private readonly AppViewModel _vm;
    private readonly Timer        _timer;
    private bool                  _disposed;

    // Track whether *we* set the pause (vs the user toggling it manually)
    // so we don't unexpectedly un-pause something the user paused themselves.
    private bool _autoPaused;

    public ScreenShareMonitor(AppViewModel vm)
    {
        _vm    = vm;
        _timer = new Timer(_ => Check(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(PollIntervalMs));
    }

    // ── Detection ─────────────────────────────────────────────────────────────

    private void Check()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        var sharing = IsScreenSharingActive();

        if (sharing && !_vm.IsPaused)
        {
            _autoPaused = true;
            // Must run on the UI thread so ObservableProperty notifications fire correctly.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _vm.IsPaused = true);
        }
        else if (!sharing && _autoPaused && _vm.IsPaused)
        {
            // Only auto-resume if we were the one who auto-paused.
            _autoPaused = false;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _vm.IsPaused = false);
        }
    }

    private static bool IsScreenSharingActive()
    {
        try
        {
            var processes = Process.GetProcesses();

            // Zoom: "CptHost" is the screen-share capture helper — only present during active share.
            if (processes.Any(p => p.ProcessName.Equals("CptHost", StringComparison.OrdinalIgnoreCase)))
                return true;

            // macOS generic: "screencapturekit" daemon becomes active when any SCStream is running.
            if (processes.Any(p => p.ProcessName.Contains("screencapturekit", StringComparison.OrdinalIgnoreCase)))
                return true;

            // Teams (classic and new): helper renderer spawned during screen share.
            // The new Teams app uses "MSTeams" as the main process name on macOS.
            if (processes.Any(p =>
                    p.ProcessName.Contains("MSTeams", StringComparison.OrdinalIgnoreCase) ||
                    p.ProcessName.Contains("Teams Helper", StringComparison.OrdinalIgnoreCase)))
            {
                // Teams runs even when not sharing; try to narrow it down by
                // checking for a "screensharing" or "renderer" helper sub-process.
                if (processes.Any(p =>
                        p.ProcessName.Contains("Teams Helper (Renderer)", StringComparison.OrdinalIgnoreCase) ||
                        p.ProcessName.Contains("MSTeams Helper", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            return false;
        }
        catch
        {
            // Process enumeration can fail with permission errors — safe to ignore.
            return false;
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
    }
}
