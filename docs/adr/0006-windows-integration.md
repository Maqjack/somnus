# 0006. Windows integration: direct power APIs, WinForms tray icon, keep-awake
Date: 2026-10-22 · Status: Accepted; the keep-awake bullet is superseded by ADR 0007

## Context
Somnus must sleep, hibernate and shut down reliably without admin rights, show a tray icon and keep the PC from idle-sleeping during a countdown. `rundll32 powrprof.dll,SetSuspendState` is unreliable: it passes wrong arguments and hibernates instead of sleeping when hibernation is enabled. `shutdown.exe` cannot sleep.

## Decision
- Sleep / hibernate: P/Invoke `SetSuspendState(hibernate, false, false)` from `powrprof.dll`.
- Shutdown: enable `SeShutdownPrivilege` via `AdjustTokenPrivileges`, then `ExitWindowsEx(EWX_SHUTDOWN | EWX_POWEROFF | flag, SHTDN_REASON_FLAG_PLANNED)`; flag = `EWX_FORCEIFHUNG` for normal, `EWX_FORCE` for forced.
- Capabilities: `GetPwrCapabilities` (`SystemS3`, `SystemS4`, `HiberFilePresent`, `AoAc` for Modern Standby).
- Keep-awake: `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)` while a countdown runs; reset on cancel, completion and exit. Superseded by ADR 0007: a power request, because the execution state belongs to one thread.
- Tray: `System.Windows.Forms.NotifyIcon` (`UseWindowsForms=true`), so no third-party tray library.
- P/Invoke via `LibraryImport` source generator; all native calls live only in `Somnus.Windows` behind interfaces from `Somnus.Core`.

## Consequences
- No extra dependencies; all native code isolated and replaceable by fakes in tests.
- Mixing WinForms into WPF needs care with namespace clashes (`using` aliases).
