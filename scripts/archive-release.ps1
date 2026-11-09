#Requires -Version 5.1
<#
.SYNOPSIS
    Keeps a local copy of a GitHub release (SWPA 3.15, issues #62, #75 and #79).

.DESCRIPTION
    1. Downloads the files of GitHub release vX.Y.Z (drafts too) and, from its release workflow, the
       manifest and the self-test reports, while the workflow still keeps them.
    2. Checks every file against its line in SHA256SUMS.txt and stops on any mismatch.
    3. Hands them to scripts/rotate_backups.py, which adds the copy and rotates the copies.
    4. Makes the build the current one: dist/ then holds exactly its files (skip with -NotCurrent).
       A release candidate (X.Y.Z-rc.N) never becomes the current build.

    Needs gh signed in to GitHub, and Python 3 for the rotation.

    The repository comes from the working copy's origin remote; -Repository picks another one.

.EXAMPLE
    ./scripts/archive-release.ps1 -Version 1.1.0

.EXAMPLE
    ./scripts/archive-release.ps1 -Version 1.1.0-rc.5
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string] $Version,

    [string] $Repository,

    [switch] $NotCurrent
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Repository) {
    Push-Location $repoRoot
    try {
        $Repository = gh repo view --json nameWithOwner --jq .nameWithOwner
    }
    finally {
        Pop-Location
    }
    if ($LASTEXITCODE -ne 0 -or -not $Repository) {
        throw 'Could not tell the repository from the origin remote; pass -Repository.'
    }
}
$isCandidate = $Version.Contains('-')
$download = Join-Path $repoRoot "artifacts/release-download/$Version"
$reports = Join-Path $repoRoot "artifacts/release-reports/$Version"
$dist = Join-Path $repoRoot 'dist'

foreach ($folder in $download, $reports) {
    if (Test-Path $folder) {
        Remove-Item -Path $folder -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $folder | Out-Null
}

Write-Host "== Download release v$Version"
gh release download "v$Version" --repo $Repository --dir $download --clobber
if ($LASTEXITCODE -ne 0) {
    throw "Downloading release v$Version failed with exit code $LASTEXITCODE."
}

Write-Host '== Check SHA256 sums'
$sumsFile = Join-Path $download 'SHA256SUMS.txt'
if (-not (Test-Path $sumsFile)) {
    throw "Release v$Version has no SHA256SUMS.txt."
}

foreach ($line in Get-Content -Path $sumsFile -Encoding ASCII | Where-Object { $_.Trim() }) {
    $expected, $name = $line -split '\s+', 2
    $actual = (Get-FileHash -Path (Join-Path $download $name.Trim()) -Algorithm SHA256).Hash
    if ($actual -ne $expected.ToUpperInvariant()) {
        throw "Checksum mismatch for $($name.Trim()): expected $expected, got $actual."
    }

    Write-Host "OK  $($name.Trim())"
}

Write-Host '== Get the manifest and the self-test reports of the release workflow'
$runId = gh run list --repo $Repository --workflow release.yml --branch "v$Version" --limit 1 --json databaseId --jq '.[0].databaseId'
if ($LASTEXITCODE -eq 0 -and $runId) {
    gh run download $runId --repo $Repository --name release --dir $reports
}

$extras = @(Get-ChildItem -Path $reports -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like 'self-test-*.json' -or $_.Name -eq 'build-manifest-win-x64.txt' } |
    ForEach-Object { $_.FullName })
if ($extras.Count -eq 0) {
    Write-Host 'The workflow no longer keeps them; the copy gets its manifest from the zip and no self-test report.'
}

Write-Host '== Keep a copy'
git -C $repoRoot fetch --quiet --tags origin
$arguments = @((Join-Path $PSScriptRoot 'rotate_backups.py'), '--add', $download) + $extras + @('--version', $Version, '--commit', "v$Version", '--list')
py -3 @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Keeping a copy of v$Version failed with exit code $LASTEXITCODE."
}

if ($isCandidate) {
    Write-Host '== A release candidate: dist/ keeps the current build'
}
elseif (-not $NotCurrent) {
    Write-Host "== Make $Version the current build in dist/"
    if (Test-Path $dist) {
        Get-ChildItem -Path $dist -Force | Remove-Item -Recurse -Force
    }
    else {
        New-Item -ItemType Directory -Path $dist | Out-Null
    }

    Copy-Item -Path (Join-Path $download '*') -Destination $dist
}
