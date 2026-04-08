using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TaskLoggerApp.Services;

/// <summary>
/// Registers or unregisters the app to launch automatically at user login.
///
/// macOS  : writes/removes a LaunchAgent plist at
///          ~/Library/LaunchAgents/com.tasklogger.app.plist
///
/// Windows: writes/removes a value in
///          HKCU\Software\Microsoft\Windows\CurrentVersion\Run
/// </summary>
public static class StartupService
{
    private const string AppName   = "TaskLogger";
    private const string PlistLabel = "com.tasklogger.app";

    // ── macOS plist path ──────────────────────────────────────────────────────

    private static string MacPlistPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", $"{PlistLabel}.plist");

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Enables or disables launch-at-login depending on <paramref name="enable"/>.
    /// Silently does nothing on unsupported platforms.
    /// </summary>
    public static void Apply(bool enable)
    {
        try
        {
            if (enable) Enable();
            else        Disable();
        }
        catch
        {
            // Never crash the app over a startup-entry failure.
        }
    }

    /// <summary>Returns <see langword="true"/> when the startup entry is present.</summary>
    public static bool IsEnabled()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return File.Exists(MacPlistPath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return WindowsIsRegistered();
        }
        catch { /* ignore */ }
        return false;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void Enable()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath)) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            WriteMacPlist(exePath);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            WindowsSetValue(exePath);
    }

    private static void Disable()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (File.Exists(MacPlistPath))
                File.Delete(MacPlistPath);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsRemoveValue();
        }
    }

    // ── macOS ─────────────────────────────────────────────────────────────────

    private static void WriteMacPlist(string exePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MacPlistPath)!);

        var plist = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
                "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>{PlistLabel}</string>
                <key>ProgramArguments</key>
                <array>
                    <string>{exePath}</string>
                </array>
                <key>RunAtLoad</key>
                <true/>
                <key>KeepAlive</key>
                <false/>
            </dict>
            </plist>
            """;

        File.WriteAllText(MacPlistPath, plist);
    }

    // ── Windows ───────────────────────────────────────────────────────────────

#pragma warning disable CA1416  // Windows-specific API: guarded by IsOSPlatform check

    private static bool WindowsIsRegistered()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: false);
        return key?.GetValue(AppName) is not null;
    }

    private static void WindowsSetValue(string exePath)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.SetValue(AppName, $"\"{exePath}\"");
    }

    private static void WindowsRemoveValue()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

#pragma warning restore CA1416
}
