# 0003. Windows build: self-contained folder build + per-user Inno Setup installer
Date: 2026-10-22 · Status: Accepted; the self-contained build is replaced by ADR 0010, and every file comes in English and Polish since ADR 0009

## Context
SWPA 3.10 prefers a folder build (onedir) over a single file and requires an installer on the Full tier. WPF cannot be trimmed, so a self-contained build is large (~150 MB unpacked). A framework-dependent build is ~1 MB but needs the .NET 10 Desktop Runtime on the target PC.

## Decision
- `dotnet publish src/Somnus.App -c Release -r win-x64 --self-contained true` to a folder (no `PublishSingleFile`).
- Installer: Inno Setup script `installer/Somnus.iss`, `PrivilegesRequired=lowest`, installs to `{localappdata}\Programs\Somnus`, Start Menu shortcut, optional desktop shortcut, shows `LICENSE`, uninstaller removes the app and `%APPDATA%\Somnus`.
- Portable build: the same folder zipped as `Somnus-<version>-win-x64-portable.zip`.
- Alternative launch without .exe: `run.bat` (`dotnet run --project src/Somnus.App -c Release`) for users with the .NET SDK.
- Release artifacts built only by GitHub Actions from a clean checkout on a tag `vX.Y.Z`.

## Consequences
- Works on a clean PC with no runtime install, no admin rights.
- Larger download (installer compresses to roughly 50–60 MB; to be measured in M5).
- Self-contained builds carry their own runtime, so security patches need a new Somnus release; Dependabot + release workflow keep that cheap.
