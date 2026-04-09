using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Linq;
using System.Runtime;
using System.Threading;
using TaskLoggerApp.Models;
using TaskLoggerApp.Services;
using TaskLoggerApp.ViewModels;
using TaskLoggerApp.Views;

namespace TaskLoggerApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            // Tray-only mode: process stays alive with no windows open.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // ── Load settings (detects first run / corrupt file) ──────────────
            var settingsService = new SettingsService();
            var settings        = settingsService.Load(out bool isFirstRun);

            // ── Services ──────────────────────────────────────────────────────
            var logService       = new TaskLogService();
            var reportService    = new ReportService();
            var retentionService = new RetentionService(settings.RetentionWeeks);

            // ── Top-level VM ──────────────────────────────────────────────────
            var vm = new AppViewModel(settingsService, logService, reportService, retentionService, settings);
            DataContext = vm;

            // ── Scheduler ──────────────────────────────────────────────────────
            var scheduler = new SchedulerService(() => vm.CurrentSettings);

            // Dispose scheduler (stops + detaches timers) on clean shutdown.
            desktop.ShutdownRequested += (_, _) => scheduler.Dispose();

            // ── Screen-share monitor ───────────────────────────────────────────
            // Polls every 8 s for known screen-sharing processes and auto-pauses.
            // Created lazily; recreated or disposed as the setting is toggled.
            ScreenShareMonitor? screenShareMonitor =
                settings.AutoDetectScreenSharing ? new ScreenShareMonitor(vm) : null;

            void UpdateScreenShareMonitor()
            {
                if (vm.CurrentSettings.AutoDetectScreenSharing)
                {
                    screenShareMonitor ??= new ScreenShareMonitor(vm);
                }
                else
                {
                    screenShareMonitor?.Dispose();
                    screenShareMonitor = null;
                }
            }

            desktop.ShutdownRequested += (_, _) => screenShareMonitor?.Dispose();

            // ── Helper: open the prompt window ────────────────────────────────
            // Tracks the single live instance so a second activation brings the
            // existing window to the front rather than spawning a duplicate.
            PromptWindow? promptWindow = null;

            void ShowPromptWindow()
            {
                if (promptWindow is { IsVisible: true })
                {
                    promptWindow.Activate();
                    return;
                }
                var promptVm = new PromptViewModel(vm.LogService);
                promptWindow = new PromptWindow(promptVm);
                promptWindow.Closed += (_, _) => { promptWindow = null; TrimMemory(); };
                promptWindow.Show();
            }

            // Called by explicit user intent (tray item, floating icon).
            // Always opens the prompt — bypasses all automatic suppression.
            void OpenPromptDirect() => ShowPromptWindow();

            // Called by the scheduler timer.
            // Respects PopupsEnabled and active-mission suppression.
            void OpenPromptAuto()
            {
                // Manual pause (tray toggle or auto screen-share detection).
                if (vm.IsPaused) return;

                // Global popup suppression toggle.
                if (!vm.CurrentSettings.PopupsEnabled) return;

                // In Mission mode all interval prompts are suppressed — the user
                // works in declared mission blocks and is prompted only when a
                // mission ends.
                if (vm.CurrentSettings.LogMode == Models.LogMode.Mission) return;

                // If a mission is active (any mode), skip this tick.
                // Auto-clear the mission object once its duration has elapsed
                // so the tray state stays accurate.
                if (vm.CurrentMission is not null)
                {
                    if (vm.CurrentMission.IsElapsed)
                        vm.SetCurrentMission(null);  // fires MissionCompletedRequested → opens mission log popup
                    return;                           // either way skip regular prompt this tick
                }

                ShowPromptWindow();
            }

            vm.OpenPromptRequested += (_, _) => OpenPromptDirect();
            scheduler.PromptFired  += OpenPromptAuto;

            // ── Helper: floating icon overlay ─────────────────────────────────
            FloatingIconWindow? floatingIcon = null;

            void UpdateFloatingIcon()
            {
                if (vm.CurrentSettings.FloatingIconEnabled && !vm.IsPaused)
                {
                    if (floatingIcon is null || !floatingIcon.IsVisible)
                    {
                        floatingIcon = new FloatingIconWindow(
                            new FloatingIconViewModel(vm.ShowPromptCommand));
                        floatingIcon.Show();
                    }
                }
                else
                {
                    floatingIcon?.Close();
                    floatingIcon = null;
                    TrimMemory();
                }
            }

            // Re-evaluate floating icon whenever pause state changes.
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(vm.IsPaused))
                    UpdateFloatingIcon();
            };

            // ── Precise mission-expiry timer ─────────────────────────────────────
            // Fires exactly when the mission duration elapses, independent of
            // the scheduler interval, so the mission-log popup appears on time.
            System.Threading.Timer? missionExpiryTimer = null;

            void ResetMissionTimer(ActiveMission? mission)
            {
                missionExpiryTimer?.Dispose();
                missionExpiryTimer = null;

                if (mission is null) return;

                // How long until the mission actually elapses from now.
                var remaining = mission.StartedAt + mission.Duration - DateTimeOffset.Now;
                if (remaining <= TimeSpan.Zero)
                {
                    // Already elapsed (e.g. app restarted mid-mission).
                    Dispatcher.UIThread.Post(() => vm.SetCurrentMission(null));
                    return;
                }

                missionExpiryTimer = new System.Threading.Timer(_ =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        // Guard: mission may have been ended manually already.
                        if (vm.CurrentMission is not null && vm.CurrentMission.IsElapsed)
                            vm.SetCurrentMission(null);
                    });
                }, null, remaining, Timeout.InfiniteTimeSpan);
            }

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(vm.CurrentMission))
                    ResetMissionTimer(vm.CurrentMission);
            };

            desktop.ShutdownRequested += (_, _) => missionExpiryTimer?.Dispose();

            // ── Helper: open the Start Mission window ──────────────────────────
            void OpenStartMissionWindow()
            {
                var missionVm = new StartMissionViewModel(mission =>
                    vm.SetCurrentMission(mission));
                new StartMissionWindow(missionVm).Show();
            }

            vm.StartMissionRequested += (_, _) => OpenStartMissionWindow();

            // ── Helper: open mission-log prompt when a mission ends ────────────
            // Pre-fills the prompt with the mission name and description; a 10-second
            // countdown auto-saves unless the user clicks Edit.
            void OpenMissionPromptWindow(ActiveMission mission)
            {
                var durationMinutes = (int)mission.Duration.TotalMinutes;
                var promptVm = new PromptViewModel(
                    vm.LogService, mission.Name, mission.Description, durationMinutes);

                // After saving the mission log, automatically offer to start a new mission.
                promptVm.MissionSaved += (_, _) => OpenStartMissionWindow();

                new PromptWindow(promptVm).Show();
            }

            vm.MissionCompletedRequested += (_, mission) => OpenMissionPromptWindow(mission);

            // Forward settings-changed notifications into the scheduler.
            vm.SettingsReloaded += (_, _) =>
            {
                scheduler.RescheduleAll();
                UpdateFloatingIcon();
                UpdateScreenShareMonitor();
                StartupService.Apply(vm.CurrentSettings.LaunchAtLogin);
            };

            // ── Helper: open the reports window ────────────────────────────
            void OpenReportsWindow()
            {
                var reportsVm = new ReportsViewModel(vm.ReportService, vm.RetentionService, vm.CurrentSettings);
                new ReportsWindow(reportsVm).Show();
            }

            vm.OpenReportsRequested += (_, _) => OpenReportsWindow();

            // ── Helper: Friday end-of-day report reminder ──────────────────────
            scheduler.FridayReportFired += () =>
            {
                var dialogVm = new FridayReportDialogViewModel();
                dialogVm.OpenReportsRequested += (_, _) => OpenReportsWindow();
                new FridayReportDialog(dialogVm).Show();
            };

            // ── Helper: create and show the Setup/Settings window ─────────────
            void OpenSetupWindow()
            {
                var setupVm = new SetupViewModel(
                    settingsService,
                    initial:          settingsService.Load(out _),
                    onSetupComplete:  () =>
                    {
                        // Reload the saved settings back into the top-level VM
                        // (also updates LogService retention via ReloadSettings).
                        vm.ReloadSettings(settingsService.Load(out _));
                        // Start (or reschedule) the scheduler with the confirmed settings.
                        scheduler.Start();
                    });

                new SetupWindow(setupVm).Show();
            }

            // ── Helper: open the Settings window (tray "Settings" item) ──────────
            void OpenSettingsWindow()
            {
                var settingsVm = new SettingsViewModel(
                    settingsService,
                    initial: vm.CurrentSettings,
                    onSaved: s => vm.ReloadSettings(s)); // → retention cleanup + SettingsReloaded → reschedule
                new SettingsWindow(settingsVm).Show();
            }

            // Tray "Settings" item → SettingsWindow (not the first-run SetupWindow).
            vm.OpenSettingsRequested += (_, _) => OpenSettingsWindow();

            // ── Wire tray icon ────────────────────────────────────────────────
            // NativeMenuItem commands are bound declaratively in App.axaml;
            // only the icon single-click needs imperative wiring.
            var icons = TrayIcon.GetIcons(this);
            if (icons is { Count: > 0 })
            {
                var trayIcon = icons[0];
                trayIcon.Clicked += (_, _) => vm.ShowPromptCommand.Execute(null);
            }

            // ── First-run: open Setup automatically ───────────────────────────
            if (isFirstRun)
                OpenSetupWindow();
            else
            {
                scheduler.Start(); // Settings already exist; begin scheduling now.
                UpdateFloatingIcon();  // Show overlay if it was enabled in saved settings.
            }

            // ── Periodic idle memory trim ─────────────────────────────────────────
            // Every 5 minutes, force a full compacting GC to return freed memory
            // to the OS. The .NET GC by default keeps freed heap pages reserved;
            // this ensures steady-state RAM stays low for a background tray app.
            var idleTrimTimer = new System.Threading.Timer(
                _ => TrimMemory(),
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5));

            desktop.ShutdownRequested += (_, _) => idleTrimTimer.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Forces a full compacting GC cycle to return freed heap memory to the OS.
    /// Call this after closing windows or during idle periods.
    /// </summary>
    private static void TrimMemory()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
