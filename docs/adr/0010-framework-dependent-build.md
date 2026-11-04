# 0010. Framework-dependent build with the .NET Desktop Runtime as a prerequisite
Date: 2026-10-30 · Status: Accepted (Mac chose this build on 2026-10-30, and on 2026-11-02 approved the administrator prompt, the runtime download and the portable zip that needs the runtime); replaces the self-contained build of ADR 0003

## Context
A self-contained WPF build takes about 165 MB after installation, which Mac called absurd for so simple an app; the target is close to 10 MB. WPF supports neither trimming nor Native AOT, so the bundled runtime cannot shrink. The alternatives were a framework-dependent build, a port to .NET Framework 4.8.1 or another UI stack. Mac chose the framework-dependent build, with the installer adding the runtime when it is missing.

## Decision
- `dotnet publish` with `--no-self-contained`: the published folder holds Somnus, its NuGet packages and the texts, and `Microsoft.WindowsDesktop.App` 10 comes from the shared runtime.
- Before it installs anything, Setup looks for a 10.x runtime where `Somnus.exe` will look: the registered dotnet folder, then the default one. When there is none, it downloads Microsoft's installer, checks its SHA-256 and starts it.
- `scripts/build.py` takes the download from Microsoft's release metadata at build time, checks the file against Microsoft's SHA-512 and passes URL and SHA-256 to Inno Setup, so each release pins the runtime that was current when it was built.
- Setup stays per-user and unelevated. Microsoft's installer asks Windows for elevation once per PC, because the shared runtime is installed for all users. This is the only exception to R-08. R-07 still holds for the app; Setup's download is the only network use.
- When the download or the installation fails, Setup stays on its page, says what to do and names the download page.
- The portable zip needs the runtime too. Without it, .NET shows its own message with a link to the download.
- Setup removes the runtime files of an older self-contained install, because a `hostfxr.dll` next to `Somnus.exe` would make it look for the runtime in that folder.

## Consequences
- About 7 MB after installation (2.2 MB of app files plus the uninstaller), 3.4 MB for Setup and 1.4 MB for the portable zip, against 165 MB, 51 MB and 68 MB.
- On a PC without the runtime, the first installation needs internet, about 60 MB of download and one administrator confirmation; on a standard account that means an administrator's password, and without it Setup stops with a message saying so. The runtime is shared with other apps and has its own entry under Installed apps; uninstalling Somnus leaves it in place.
- Security patches of the runtime come from Microsoft instead of a new Somnus release. Microsoft Update delivers them when updates for other Microsoft products are on.
- Starting Somnus depends on a runtime outside its folder; an installation that later loses it shows the .NET message.
- The path with a missing runtime cannot run on a PC that has .NET, so it needs a check on a clean Windows (acceptance criterion C6).
