# 0013. Forced shutdown on a schedule
Date: 2026-11-05 · Status: Accepted (Mac, 2026-11-05: "zmień"; replaces the point about forced shutdown in ADR 0011)

## Context
ADR 0011 kept "Force close apps" off a repeating schedule, because rule R-05 says the option is never remembered. Mac saw the box disappear as soon as he ticked a day and asked why, since a forced shutdown is there to ignore apps that ask to save changes. Without it, a normal shutdown (`EWX_FORCEIFHUNG`, assumption A3) can be stopped by one app that asks "Save changes?", so a nightly schedule may leave the PC on.

## Decision
- A repeating shutdown can force apps to close. The box and its data-loss warning show for every shutdown, once or on a schedule.
- The choice is kept with the schedule (`scheduleForceCloseApps` in the settings), so it survives a restart of Somnus or the PC and applies to every run. Stopping the schedule clears it. A one-time countdown still never remembers it (R-05).
- With the 60-second warning on (the default), it shows the data-loss warning before every run. With the warning turned off in the settings there is none, as for a one-time countdown (Claude's default). While the schedule runs, the main window shows the ticked box and the warning.
- Settings that ask a schedule which does not shut down to force apps to close are corrected when Somnus reads them, like other invalid values.
- An update never turns force on: a settings file without the field gives a schedule without force (Claude's default).

## Consequences
- A scheduled shutdown happens even when an app has unsaved work, and that work is lost on every run the user does not stop.
- R-05 now reads: "Force close apps" is off by default. It is never remembered as a last value or for a one-time countdown; a running schedule keeps it with its other values, and ending the schedule clears it. When on, it shows the data-loss warning next to it and in the warning dialog.
- The settings file has one more field. An older Somnus ignores it and runs the schedule without force.
