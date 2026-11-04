# 0007. Keep-awake through a Windows power request
Date: 2026-10-22 · Status: Accepted (2026-10-23, by default: proposed to Mac with silence counting as consent) · Supersedes the keep-awake bullet of ADR 0006

## Context
ADR 0006 chose `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)` to keep the PC from
idle sleep during a countdown. That state belongs to the thread that set it: Windows drops it when
the thread exits, and only that thread can clear it. A Somnus countdown starts on the UI thread but
ends either on the UI thread (cancel) or on a timer thread (time is up, missed deadline). Done
naively, the request would leak (the PC never idle-sleeps while Somnus runs) or vanish too early.
Making it safe would need a dedicated thread whose only job is to own the execution state.

## Decision
- Use a power request instead: `PowerCreateRequest` with the simple reason
  "Somnus countdown in progress", `PowerSetRequest(PowerRequestSystemRequired)` on acquire,
  `PowerClearRequest` and `CloseHandle` on release.
- The request belongs to a handle, not to a thread, so `WindowsKeepAwake` can acquire and release
  from any thread. It is idempotent and never throws; failures are logged and the countdown goes on.
- Everything else in ADR 0006 stays as decided.

## Consequences
- `powercfg /requests` (run in an administrator terminal) lists Somnus with a readable reason, which
  makes "why does my PC not sleep" questions easy to answer.
- Same API family (`kernel32.dll`), no new dependency, available since Windows 7.
- As before, only idle system sleep is blocked; the display may still turn off, and the user can
  still put the PC to sleep by hand.
