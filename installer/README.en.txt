Somnus {version}

Somnus puts a Windows PC to sleep, hibernates it or shuts it down after a countdown or at a
chosen time, with a 60-second warning you can stop.

Made by Mac (Maqjack). Home page and source code: https://github.com/Maqjack/somnus


HOW TO USE IT

1. Choose Countdown (a delay) or At a time (a clock time).
2. Type the number and pick seconds, minutes or hours, or type the hour and the minute.
   Clicking or tabbing into a box selects its value, so you can type straight over it.
3. Choose Sleep, Hibernate or Shut down. For a shutdown you can tick Force close apps;
   unsaved work in other apps is then lost.
4. Press Start (or Enter). The window hides to the tray; hover over the crescent to see the
   time left.

During the last 60 seconds a warning in the bottom-right corner of the screen offers Stop
(also Esc) and Do it now; its minus button only puts it on the taskbar while the countdown goes
on. Click the tray icon to change or stop a running countdown.
If the PC wakes up less than a minute before the scheduled time or after it, Somnus cancels
instead of acting.

In At a time, tick Every day or the days you want and the countdown repeats. Somnus has to be
running: turn on Start with Windows in the settings, or start it again after a shutdown, and it
picks the schedule up. Stop ends the schedule.

The gear opens the settings: the language (English or Polish), the warning, whether to remember
the last countdown, the sound of the last 20 seconds (the lullaby, no sound, or the first 20
seconds of your own MP3, WAV, WMA, M4A, AAC or FLAC file), and Start with Windows.


REQUIREMENTS

Windows 10 22H2 or Windows 11, x64, and the Microsoft .NET Desktop Runtime 10. The installer adds
the runtime when it is missing, and Windows then asks for permission once. For the portable build,
install it from https://dotnet.microsoft.com/download/dotnet/10.0. Somnus itself needs no
administrator rights.


PRIVACY

Somnus never uses the network, sends no telemetry and never asks for administrator rights. It
starts with Windows only if you turn that on in its settings. Only its installer goes online, to get the .NET Desktop Runtime from
Microsoft when the PC lacks it. It keeps its settings in %APPDATA%\Somnus\settings.json and daily logs
in %APPDATA%\Somnus\logs for 7 days.


IF SOMETHING DOES NOT WORK

- "Windows protected your PC": Somnus is not code-signed. Compare the SHA256 checksum of the
  downloaded file with SHA256SUMS.txt on the release page, then choose More info and Run anyway.
- Hibernate is greyed out: hibernation is turned off in Windows. Run "powercfg /hibernate on"
  in a terminal opened as administrator, then bring the Somnus window back.
- Sleep is greyed out: the PC uses Modern Standby, which apps cannot start. Use Hibernate or
  Shut down.
- A normal shutdown did not happen: an app with unsaved work stopped it. Save your work first,
  or tick Force close apps.
- Anything else: the details are in %APPDATA%\Somnus\logs. Please report it at
  https://github.com/Maqjack/somnus/issues


UNINSTALLING

Installed: Settings > Apps > Installed apps > Somnus > Uninstall; this also removes the settings,
the logs and Start with Windows. Portable: turn off Start with Windows in the settings, then delete
the Somnus folder and %APPDATA%\Somnus. The .NET Desktop Runtime stays,
because other apps may use it.


LICENSE

MIT License, copyright (c) 2026 Mac (Maqjack): see LICENSE.txt. The licenses of the bundled
components are in THIRD_PARTY_LICENSES.txt.
