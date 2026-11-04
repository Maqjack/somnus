#Requires -Version 5.1
<#
.SYNOPSIS
    Static checks for the project rules that a grep can verify.

.DESCRIPTION
    R-01: the real power adapter is referenced only from Somnus.Windows and the
          composition root in src/Somnus.App/Composition, the Windows functions that
          sleep, hibernate or shut down only from Somnus.Windows, and tests use neither.
    R-02: no hex colours in Views XAML; colours come from src/Somnus.App/Theme.
    R-07: no network APIs anywhere in the shipped code.

.EXAMPLE
    ./scripts/check-rules.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$srcRoot = Join-Path $repoRoot 'src'
$violations = [System.Collections.Generic.List[string]]::new()

function Get-SourceFiles {
    param(
        [string] $Path,
        [string[]] $Extensions
    )

    if (-not (Test-Path $Path)) {
        return @()
    }

    return @(Get-ChildItem -Path $Path -Recurse -File |
        Where-Object { $Extensions -contains $_.Extension -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
}

function Get-RelativePath {
    param([string] $FullName)

    return $FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
}

function Add-Violations {
    param(
        [string] $Rule,
        [object[]] $Files,
        [string] $Pattern
    )

    foreach ($file in $Files) {
        foreach ($match in @(Select-String -Path $file.FullName -Pattern $Pattern)) {
            $violations.Add(('{0} {1}:{2}: {3}' -f $Rule, (Get-RelativePath $file.FullName), $match.LineNumber, $match.Line.Trim()))
        }
    }
}

# R-02: Views must take every colour from the theme dictionaries.
$viewFiles = Get-SourceFiles -Path (Join-Path $srcRoot 'Somnus.App/Views') -Extensions @('.xaml')
Add-Violations -Rule 'R-02' -Files $viewFiles -Pattern '#[0-9A-Fa-f]{3,8}\b'

# R-07: no network access of any kind.
$networkPattern = '\b(HttpClient|HttpClientHandler|SocketsHttpHandler|WebClient|WebRequest|HttpWebRequest|TcpClient|TcpListener|UdpClient|ClientWebSocket|Socket|System\.Net\.Http|System\.Net\.Sockets|System\.Net\.WebSockets)\b'
$codeFiles = Get-SourceFiles -Path $srcRoot -Extensions @('.cs', '.xaml')
Add-Violations -Rule 'R-07' -Files $codeFiles -Pattern $networkPattern

# R-01: only the composition root may create the real power controller, only Somnus.Windows may
# call the Windows functions that change the power state, and tests may do neither.
$compositionRoot = (Join-Path $srcRoot 'Somnus.App/Composition').Replace('\', '/')
$windowsProject = (Join-Path $srcRoot 'Somnus.Windows').Replace('\', '/')
$testFiles = Get-SourceFiles -Path (Join-Path $repoRoot 'tests') -Extensions @('.cs')
$outsideAllowedPlaces = $codeFiles | Where-Object {
    $path = $_.FullName.Replace('\', '/')
    -not $path.StartsWith($compositionRoot + '/') -and -not $path.StartsWith($windowsProject + '/')
}
$outsideWindowsProject = $codeFiles | Where-Object { -not $_.FullName.Replace('\', '/').StartsWith($windowsProject + '/') }
Add-Violations -Rule 'R-01' -Files (@($outsideAllowedPlaces) + @($testFiles)) -Pattern '\bWindowsPowerController\b'
Add-Violations -Rule 'R-01' -Files (@($outsideWindowsProject) + @($testFiles)) -Pattern '\b(SetSuspendState|ExitWindowsEx)\b'

if ($violations.Count -gt 0) {
    foreach ($violation in $violations) {
        Write-Host "::error::$violation"
    }

    Write-Host "$($violations.Count) project rule violation(s) found."
    exit 1
}

Write-Host 'Project rules R-01, R-02 and R-07: no violations.'
