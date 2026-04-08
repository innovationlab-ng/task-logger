using System;
using Avalonia.Threading;
using TaskLoggerApp.Models;

namespace TaskLoggerApp.Services;

/// <summary>
/// Drives two background schedules entirely on the Avalonia UI thread
/// (via <see cref="DispatcherTimer"/>), so subscribers never need to
/// marshal to the UI thread themselves.
///
/// Interval prompt rules
/// ─────────────────────
///  • Only fires on weekdays, within [WorkStart, WorkEnd).
///  • If the computed next-fire time is outside those bounds, it is deferred
///    to the start of the next valid working day (skipping weekends).
///  • No busy-polling: each timer is one-shot; it re-arms itself with a
///    freshly computed delay after each tick.
///
/// Friday end-of-day reminder
/// ──────────────────────────
///  • Fires every Friday at <see cref="AppSettings.FridayPromptTime"/> (17:00).
///  • If today is Friday and 17:00 has not yet passed, fires today.
///  • Otherwise fires on the next coming Friday.
/// </summary>
public sealed class SchedulerService : IDisposable
{
    private readonly Func<AppSettings> _getSettings;
    private DispatcherTimer? _intervalTimer;
    private DispatcherTimer? _fridayTimer;
    private bool _started;

    // ── Events (UI thread) ────────────────────────────────────────────────────

    /// <summary>Fired on the UI thread when a periodic task-log prompt is due.</summary>
    public event Action? PromptFired;

    /// <summary>Fired on the UI thread every Friday at 17:00.</summary>
    public event Action? FridayReportFired;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SchedulerService(Func<AppSettings> settingsProvider)
        => _getSettings = settingsProvider;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Arms both timers using the current settings.
    /// Safe to call multiple times; subsequent calls simply recompute delays.
    /// </summary>
    public void Start()
    {
        _started = true;
        ScheduleNextInterval();
        ScheduleNextFriday();
    }

    /// <summary>
    /// Recomputes both timer delays from the current settings.
    /// No-op until <see cref="Start"/> has been called at least once.
    /// Call this whenever settings change.
    /// </summary>
    public void RescheduleAll()
    {
        if (!_started) return;
        ScheduleNextInterval();
        ScheduleNextFriday();
    }

    /// <summary>Stops both timers without clearing the started flag.</summary>
    public void Stop()
    {
        _intervalTimer?.Stop();
        _fridayTimer?.Stop();
    }

    /// <summary>
    /// Stops and detaches both timers.  Call when the app is shutting down
    /// to ensure no timer callbacks fire after disposal.
    /// </summary>
    public void Dispose()
    {
        if (_intervalTimer is not null)
        {
            _intervalTimer.Stop();
            _intervalTimer.Tick -= OnIntervalTick;
            _intervalTimer = null;
        }
        if (_fridayTimer is not null)
        {
            _fridayTimer.Stop();
            _fridayTimer.Tick -= OnFridayTick;
            _fridayTimer = null;
        }
    }

    // ── Interval prompt ───────────────────────────────────────────────────────

    private void ScheduleNextInterval()
    {
        if (_intervalTimer is not null)
        {
            _intervalTimer.Stop();
            _intervalTimer.Tick -= OnIntervalTick; // detach before discarding
        }

        var settings = _getSettings();
        var delay    = DelayUntil(ComputeNextInterval(DateTime.Now, settings));

        _intervalTimer = new DispatcherTimer { Interval = Max(delay, TimeSpan.FromMilliseconds(1)) };
        _intervalTimer.Tick += OnIntervalTick;
        _intervalTimer.Start();
    }

    private void OnIntervalTick(object? sender, EventArgs e)
    {
        _intervalTimer?.Stop();
        PromptFired?.Invoke();
        ScheduleNextInterval();
    }

    // ── Friday end-of-day reminder ────────────────────────────────────────────

    private void ScheduleNextFriday()
    {
        if (_fridayTimer is not null)
        {
            _fridayTimer.Stop();
            _fridayTimer.Tick -= OnFridayTick; // detach before discarding
        }

        var delay = DelayUntil(NextFriday17(DateTime.Now));

        _fridayTimer = new DispatcherTimer { Interval = Max(delay, TimeSpan.FromMilliseconds(1)) };
        _fridayTimer.Tick += OnFridayTick;
        _fridayTimer.Start();
    }

    private void OnFridayTick(object? sender, EventArgs e)
    {
        _fridayTimer?.Stop();
        FridayReportFired?.Invoke();
        ScheduleNextFriday();
    }

    // ── Scheduling logic (internal for testability) ───────────────────────────

    /// <summary>
    /// Returns when the next interval prompt should fire.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>Weekend → next Monday at WorkStart.</item>
    ///   <item>Before WorkStart today → today at WorkStart.</item>
    ///   <item>After WorkEnd today → next valid weekday at WorkStart.</item>
    ///   <item>Within work hours → <c>now + IntervalMinutes</c>, unless that
    ///     exceeds WorkEnd, in which case defer to next WorkStart.</item>
    /// </list>
    /// </remarks>
    internal static DateTime ComputeNextInterval(DateTime now, AppSettings s)
    {
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return NextWorkStart(now, s);

        var workStart = now.Date + s.WorkStart;
        var workEnd   = now.Date + s.WorkEnd;

        if (now < workStart) return workStart;
        if (now >= workEnd)  return NextWorkStart(now, s);

        // Within work hours.
        var next = now.AddMinutes(s.IntervalMinutes);
        return next < workEnd ? next : NextWorkStart(now, s);
    }

    /// <summary>
    /// Returns the Monday of the next working week (skips Saturday and Sunday)
    /// at the configured WorkStart time.
    /// </summary>
    private static DateTime NextWorkStart(DateTime from, AppSettings s)
    {
        var candidate = from.Date.AddDays(1) + s.WorkStart;
        while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            candidate = candidate.AddDays(1);
        return candidate;
    }

    /// <summary>
    /// Returns the next Friday at <see cref="AppSettings.FridayPromptTime"/>.
    /// If today is Friday and 17:00 has not yet passed, returns today at 17:00.
    /// </summary>
    internal static DateTime NextFriday17(DateTime from)
    {
        int daysUntil = ((int)DayOfWeek.Friday - (int)from.DayOfWeek + 7) % 7;

        if (daysUntil == 0)
        {
            var todayAt17 = from.Date + AppSettings.FridayPromptTime;
            if (todayAt17 > from) return todayAt17;
            daysUntil = 7; // already past 17:00 today
        }

        return from.Date.AddDays(daysUntil) + AppSettings.FridayPromptTime;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TimeSpan DelayUntil(DateTime target)
    {
        var d = target - DateTime.Now;
        return d > TimeSpan.Zero ? d : TimeSpan.Zero;
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
