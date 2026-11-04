@echo off
rem Runs Somnus from source with the .NET 10 SDK, an alternative to the .exe (SWPA 3.10).
rem The Release configuration performs real power actions.
rem Add --dry-run to only simulate them, for example: run.bat --dry-run
setlocal
cd /d "%~dp0"
dotnet run --project src\Somnus.App\Somnus.App.csproj --configuration Release -- %*
endlocal
