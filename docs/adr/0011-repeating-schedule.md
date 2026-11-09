# 0011. Repeating schedule on days of the week
Date: 2026-11-01 · Status: Accepted (Mac asked for the schedule and, on 2026-11-02, for starting with Windows as a setting; the last-hour keep-awake is Claude's default, explained to Mac the same day). The point about forced shutdown is replaced by [ADR 0013](0013-forced-shutdown-on-a-schedule.md) (2026-11-05).

## Context
Mac asked for Every day and a strip of weekdays under the clock time. SCOPE 1.0 left recurring schedules and starting with Windows out. After a shutdown Somnus is not running, so a schedule can go on only when Somnus starts again.

## Decision
- An "at" countdown can repeat on chosen days. `ScheduleCoordinator` starts the next run after every run, a failed run or a run the PC slept through, which is skipped without an action (A8). The run after a target is always on a later date, so the night clocks go back runs once.
- The schedule is kept in the settings and starts again when Somnus starts. A run less than a minute away then is skipped, so starting Somnus never ends in an action without the full warning (A8). Cancelling, or exiting Somnus, ends the whole schedule.
- During a repeating countdown the PC is kept awake only for the last hour, so it can still sleep on its own (R-06). If it sleeps at the target, that run is skipped.
- ~~A repeating countdown never forces apps to close, because that option is never remembered (R-05).~~ Replaced by [ADR 0013](0013-forced-shutdown-on-a-schedule.md): a schedule keeps that choice with its other values.
- Starting with Windows is a setting, off by default: a value named Somnus in the user's Run key, with `--autostart`, so Somnus waits in the tray and the schedule goes on after a restart. A dry run only keeps the choice in memory, and uninstalling removes the value. Without the setting, the schedule goes on the next time the user starts Somnus.

## Consequences
- A daily shutdown without the setting needs Somnus to be started after each boot; with it, Somnus is in the tray after every sign-in.
- The window grows in the At a time mode by the row of days.
- ~~A forced shutdown is not available on a schedule: an app with unsaved work can stop it.~~ Since [ADR 0013](0013-forced-shutdown-on-a-schedule.md) a schedule can force apps to close.
