#Requires -Version 5.1
<#
.SYNOPSIS
    Writes the GitHub release notes for a version from CHANGELOG.md and SHA256SUMS.txt.

.DESCRIPTION
    Takes the section "## [<version>]" of CHANGELOG.md (or "## [Unreleased]" for a pre-release
    without its own section), then adds the SHA256 checksums of every file (SWPA 3.10) and the
    note about the unsigned installer (ADR 0004). The public repository's copy of the changelog
    has its version headings without brackets ("## 1.1.0 - 2026-11-13"), which works the same.

.EXAMPLE
    ./scripts/release-notes.ps1 -Version 1.0.0 -ChecksumsFile artifacts/release/SHA256SUMS.txt -OutputFile artifacts/release-notes.md
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $ChecksumsFile,

    [Parameter(Mandatory = $true)]
    [string] $OutputFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$changelog = Get-Content -Path (Join-Path $repoRoot 'CHANGELOG.md') -Encoding UTF8

function Get-Section {
    param([string] $Heading)

    $pattern = '^## \[?' + [regex]::Escape($Heading) + '\]?(\s|$)'
    $start = -1
    for ($i = 0; $i -lt $changelog.Count; $i++) {
        if ($changelog[$i] -match $pattern) {
            $start = $i + 1
            break
        }
    }

    if ($start -lt 0) {
        return $null
    }

    # The section ends at the next version heading or at the link definitions at the bottom.
    $lines = [System.Collections.Generic.List[string]]::new()
    for ($i = $start; $i -lt $changelog.Count -and -not $changelog[$i].StartsWith('## ') -and $changelog[$i] -notmatch '^\[[^\]]+\]: '; $i++) {
        $lines.Add($changelog[$i])
    }

    return ($lines -join "`n").Trim()
}

$body = Get-Section -Heading $Version
if (-not $body) {
    $body = Get-Section -Heading 'Unreleased'
}

if (-not $body) {
    throw "CHANGELOG.md has no entries under [$Version] or [Unreleased]."
}

$checksums = (Get-Content -Path $ChecksumsFile -Encoding ASCII) -join "`n"
$notes = @"
$body

## Download

Every download comes in English (``-en``) and in Polish (``-pl``), with the README and the licence
in that language. Somnus starts in that language; the settings switch to the other one at any time.

Somnus needs the Microsoft .NET Desktop Runtime 10. The installers add it when it is missing, which
takes one permission prompt from Windows; the portable builds need it on the PC already.

- **Somnus-Setup-$Version-en.exe**, **Somnus-Setup-$Version-pl.exe**: installer for the current user, no administrator rights needed.
- **Somnus-$Version-win-x64-portable-en.zip**, **Somnus-$Version-win-x64-portable-pl.zip**: unzip anywhere and run ``Somnus.exe``.

The files are not code-signed (ADR 0004), so Windows SmartScreen may say "Windows protected your PC".
Choose **More info** and **Run anyway** after checking the SHA256 checksum below.

## SHA256 checksums

``````text
$checksums
``````
"@

$outputPath = Join-Path $repoRoot $OutputFile
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
Set-Content -Path $outputPath -Value $notes -Encoding UTF8
Write-Host "Release notes written to $OutputFile."
