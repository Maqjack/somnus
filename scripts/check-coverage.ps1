#Requires -Version 5.1
<#
.SYNOPSIS
    Fails when line coverage of a project drops below the threshold (SWPA Full tier, criterion C5).

.DESCRIPTION
    Reads every Cobertura report under -ResultsDirectory, merges the line hits per package
    (assembly) and checks the line coverage of each package listed in -Packages.
    A project without any C# source file has nothing to cover and is skipped.
    A project with code but without coverage data fails, so a broken coverage setup
    can never pass silently.

.EXAMPLE
    ./scripts/check-coverage.ps1 -ResultsDirectory artifacts/test-results
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ResultsDirectory,

    [string[]] $Packages = @('Somnus.Core', 'Somnus.Presentation'),

    [ValidateRange(0, 100)]
    [double] $Threshold = 80
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

# `powershell -File` passes "A,B" as one string; accept both forms.
$Packages = @($Packages | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

function Test-HasSources {
    param([string] $Package)

    $projectDir = Join-Path $repoRoot (Join-Path 'src' $Package)
    if (-not (Test-Path $projectDir)) {
        return $false
    }

    $sources = @(Get-ChildItem -Path $projectDir -Recurse -Filter '*.cs' -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
    return $sources.Count -gt 0
}

# package name -> (source file + line number -> total hits)
$hitsByPackage = @{}

$reports = @()
if (Test-Path $ResultsDirectory) {
    $reports = @(Get-ChildItem -Path $ResultsDirectory -Recurse -Filter '*cobertura*.xml' -File)
}

foreach ($report in $reports) {
    [xml] $document = Get-Content -Path $report.FullName -Raw
    foreach ($package in $document.SelectNodes('/coverage/packages/package')) {
        $name = $package.GetAttribute('name')
        if (-not $hitsByPackage.ContainsKey($name)) {
            $hitsByPackage[$name] = @{}
        }

        $lines = $hitsByPackage[$name]
        foreach ($class in $package.SelectNodes('classes/class')) {
            $file = $class.GetAttribute('filename')
            foreach ($line in $class.SelectNodes('lines/line')) {
                $key = '{0}:{1}' -f $file, $line.GetAttribute('number')
                $hits = [long] $line.GetAttribute('hits')
                if ($lines.ContainsKey($key)) {
                    $lines[$key] += $hits
                }
                else {
                    $lines[$key] = $hits
                }
            }
        }
    }
}

$failed = $false
$summary = [System.Collections.Generic.List[string]]::new()
$summary.Add('| Project | Covered lines | Line coverage | Threshold | Result |')
$summary.Add('|---|---|---|---|---|')

foreach ($package in $Packages) {
    if (-not (Test-HasSources -Package $package)) {
        Write-Host "$package has no C# sources yet; nothing to cover."
        $summary.Add("| $package | - | - | $Threshold% | skipped (no code) |")
        continue
    }

    if (-not $hitsByPackage.ContainsKey($package) -or $hitsByPackage[$package].Count -eq 0) {
        Write-Host "::error::No coverage data for $package. Was the test run started with --coverlet?"
        $summary.Add("| $package | - | - | $Threshold% | **missing data** |")
        $failed = $true
        continue
    }

    $lines = $hitsByPackage[$package]
    $covered = @($lines.Values | Where-Object { $_ -gt 0 }).Count
    $total = $lines.Count
    $percent = [math]::Round(100.0 * $covered / $total, 2)
    $passed = $percent -ge $Threshold
    $result = if ($passed) { 'pass' } else { '**FAIL**' }

    Write-Host ('{0}: {1}/{2} lines covered = {3}% (threshold {4}%)' -f $package, $covered, $total, $percent, $Threshold)
    $summary.Add("| $package | $covered / $total | $percent% | $Threshold% | $result |")

    if (-not $passed) {
        Write-Host "::error::Line coverage of $package is $percent%, below the $Threshold% threshold."
        $failed = $true
    }
}

if ($env:GITHUB_STEP_SUMMARY) {
    Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "### Line coverage`n"
    Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value ($summary -join "`n")
}

if ($failed) {
    exit 1
}
