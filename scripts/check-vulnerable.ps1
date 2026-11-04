#Requires -Version 5.1
<#
.SYNOPSIS
    Fails when any direct or transitive NuGet package has a known vulnerability (SWPA 3.5).

.DESCRIPTION
    Runs `dotnet list package --vulnerable --include-transitive --format json` for the
    solution and fails on any reported advisory, whatever its severity.

.EXAMPLE
    ./scripts/check-vulnerable.ps1
#>
[CmdletBinding()]
param(
    [string] $Solution = 'Somnus.sln'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot $Solution

$json = dotnet list $solutionPath package --vulnerable --include-transitive --format json
if ($LASTEXITCODE -ne 0) {
    Write-Host '::error::dotnet list package failed.'
    exit $LASTEXITCODE
}

$report = ($json -join "`n") | ConvertFrom-Json
$findings = [System.Collections.Generic.List[string]]::new()

foreach ($project in @($report.projects)) {
    if (-not ($project.PSObject.Properties.Name -contains 'frameworks')) {
        continue
    }

    foreach ($framework in @($project.frameworks)) {
        foreach ($kind in 'topLevelPackages', 'transitivePackages') {
            if (-not ($framework.PSObject.Properties.Name -contains $kind)) {
                continue
            }

            foreach ($package in @($framework.$kind)) {
                foreach ($vulnerability in @($package.vulnerabilities)) {
                    $findings.Add(('{0} {1} ({2}): {3} {4}' -f
                            $package.id, $package.resolvedVersion, $framework.framework,
                            $vulnerability.severity, $vulnerability.advisoryurl))
                }
            }
        }
    }
}

if ($findings.Count -gt 0) {
    foreach ($finding in $findings | Sort-Object -Unique) {
        Write-Host "::error::Vulnerable package: $finding"
    }

    exit 1
}

Write-Host 'No known vulnerable packages (direct or transitive).'
