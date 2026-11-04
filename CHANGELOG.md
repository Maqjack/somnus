# Changelog

All notable changes to Somnus are documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and the project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- On a PC without the .NET Desktop Runtime, Setup first shows a page that says what it installs, from
  where and why, and starts the download only after Next.
- The text follows Windows' Text size (Settings > Accessibility > Text size), and the windows widen with
  it, so nothing is cut off up to 225% (ADR 0012).
- **Start with Windows** in the settings, off by default: Somnus then waits in the tray after
  sign-in, so a schedule goes on after a restart. Uninstalling removes it.
- At a time can repeat: **Every day** or chosen days under the clock time. The next run starts by
  itself, a run the PC slept through is skipped, and the schedule comes back when Somnus starts.
  Cancel ends it (ADR 0011).
- A tiny `by Maqjack` signature in the bottom-right corner of the window.
- An optional lullaby: with the new setting on (it is off by default), a quiet recorded lullaby
  plays during the last 20 seconds before the action and stops at once on cancel.
- Somnus starts in the language of its installer or portable zip, until another one is picked in
  the settings.

### Changed

- The lullaby switch is now a choice of sound for the last 20 seconds: the lullaby (the default for a
  new install), no sound, or the first 20 seconds of your own MP3, WAV, WMA, M4A, AAC or FLAC file.
  A file that is gone plays the lullaby instead; an earlier setting is kept.
- The warning has no close button any more: its minus button puts it on the taskbar while the
  countdown goes on, and **Stop** replaces **Cancel** wherever a button stops the countdown
  (Mac's feedback, 2026-11-04).
- Somnus takes about 7 MB after installation instead of 165 MB, and the installer about 3 MB instead
  of 51 MB, because it now runs on the shared Microsoft .NET Desktop Runtime 10 instead of carrying
  its own copy. On a PC without the runtime the installer downloads it from Microsoft and installs it
  first, which needs internet and one permission prompt from Windows; the portable build needs the
  runtime on the PC already (ADR 0010).
- For maintainers: `scripts/build.py` replaces `scripts/build-release.ps1`. It adds
  `Somnus.exe --self-test`, a build manifest compared in CI, a check for forbidden content and the
  size budget of SWPA 3.14.
- For maintainers: the SWPA guard hook stops agents from touching the build backups, the hook and
  the Claude settings, and keeps auditors from changing files.
- Changing a running countdown ends with **Save** instead of Restart countdown. Saving restarts the
  countdown and keeps the window open with the new clock; only Start hides the window. Confirmation
  dialogs ask their question once, inside the dialog, with an empty title bar.
- A single left click on the tray icon opens the window. In the tray menu the first item is now
  **Edit countdown** while a countdown runs and **Open Somnus** otherwise, and the Polish texts of
  the app no longer decline the name Somnus.
- Every download comes in an English and a Polish version: `Somnus-Setup-<version>-en.exe` and
  `-pl.exe`, and the portable `Somnus-<version>-win-x64-portable-en.zip` and `-pl.zip`. Each one
  runs in its language with `README.txt` and `LICENSE.txt` in that language, and the installer no
  longer asks for a language. The Polish licence is an informational translation followed by the
  binding English original.
- The 60-second warning opens in the bottom-right corner of the screen, like a notification,
  instead of the middle.
- Clicking or tabbing into the amount, hour or minute box selects its value, so typing replaces it;
  a digit that would make the value too long or impossible (33 for an hour) starts it over.
- The time modes are called **Countdown** and **At a time** (PL: **Odliczanie**, **O której**), and the
  preview under the clock time says what will happen, for example "The PC will shut down at 13:24
  (Sat, 24 Oct)".
- The settings open as a panel inside the main window, so the window moves with them and Start
  stays blocked while they are open. A picked language shows at once; Save keeps it, and the cross
  or Esc goes back to the previous one.
- The installer is black with white text, the purple crescent and a purple progress bar, and the
  page with the desktop shortcut ends with Install instead of a summary page.

### Fixed

- In the day strip a chosen day now answers the mouse, and the keyboard focus ring no longer touches
  the next day.
- Disabled controls use the disabled text colour on the window background instead of fading, so
  their labels stay readable, and a disabled Start says in a tooltip what to correct.
- For maintainers: `scripts/archive-release.ps1` no longer fails at the end when `builds\` holds
  the `rc` folder, and it also keeps release candidates, in `builds\rc\<version>`, without making
  them the current build.

## [1.0.1] - 2026-10-23

Fixes from the independent review of 1.0.0.

### Fixed

- Waking the PC less than a minute before the scheduled time, or up to 30 seconds after it, could
  still start the action without the full warning. Somnus now cancels in both cases, with the
  warning on or off.
- The unit box went blank after switching the language while the main window was open; the
  chosen unit now stays selected.
- Log lines contained the Windows user name in paths; the profile folder is now written as
  `%USERPROFILE%`. The power capabilities are logged when they change, not every time the window
  is activated.
- Settings are flushed to disk before they replace the old file, so a power cut cannot leave an
  empty settings file.

### Changed

- For developers: tests prove that dry runs and Debug builds never get the real power controller
  and run the shutdown flags and privilege on real Windows without shutting down; the R-01 check
  also scans the tests; the build checks cyclomatic complexity (CA1502) and maintainability
  (CA1505). The WPF tests no longer start the app itself, which they did briefly since.
- `THIRD_PARTY_LICENSES.txt` now contains the third-party notices of the bundled .NET runtime and
  links the WPF and Windows Forms notices of the same version. A failing licence check stops the
  release build, and the build log names the Inno Setup version.
- For maintainers: `scripts/archive-release.ps1` keeps every production build in `builds\<version>`,
  checked against its SHA256 sums, makes it the current one in `dist\` and keeps the first build and
  the ten newest.

## [1.0.0] - 2026-10-23

First release.

### Added

- Sleep, hibernate or shut down the PC after a countdown (10 seconds to 99 hours) or at a clock
  time, today or tomorrow, with daylight saving changes handled.
- A 60-second warning with **Cancel** and **Do it now**, which can be turned off. A countdown can
  be cancelled at any time from the window or the tray; Esc also cancels, after a confirmation.
- Optional forced shutdown that closes apps without asking, with a data-loss warning. It is off
  by default and never remembered.
- Actions the PC cannot perform are greyed out with the reason: hibernation turned off (with the
  command that turns it on) or Modern Standby.
- During a countdown Somnus waits in the tray, shows the remaining time in the tooltip and keeps
  the PC from going to sleep on its own.
- A countdown whose deadline passed while the PC was asleep or the clock jumped ahead is
  cancelled instead of acting late.
- Editing the values during a countdown offers a restart with the new values.
- English and Polish, switchable without a restart. Purple interface with a crescent-moon logo,
  dark title bars, visible keyboard focus and text contrast of at least 4.5:1.
- One instance: starting Somnus again brings the running window forward.
- Settings in `%APPDATA%\Somnus\settings.json`, daily logs in `%APPDATA%\Somnus\logs` (7 days
  kept); unexpected errors are logged and explained in plain words.
- `--dry-run` only simulates the power actions, for trying Somnus out safely.
- Per-user installer in English and Polish (no administrator rights; uninstalling also removes the
  settings and logs) and a portable zip, with SHA256 checksums and third-party license notices.
- For developers: four projects (Core, Presentation, Windows, App), 320 tests with an 80% line
  coverage gate, CI with formatting, analyzers as errors, vulnerability, license and secret
  checks, and a release workflow that builds from version tags.

[Unreleased]: https://github.com/Maqjack/somnus/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/Maqjack/somnus/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/Maqjack/somnus/releases/tag/v1.0.0
