#Requires -Version 5.1
<#
.SYNOPSIS
    Checks every NuGet dependency against the SWPA 3.5 license allow-list and writes
    THIRD_PARTY_LICENSES.txt for the shipped app.

.DESCRIPTION
    1. Validates all direct and transitive packages of the solution, including test-only
       ones, against scripts/license-allowlist.json. Any other license fails the check.
    2. Lists the packages that ship inside Somnus (src/Somnus.App and its references) and
       writes them, together with the full license texts, to -OutputFile.

    Run `dotnet restore` first: nuget-license reads project.assets.json.

.EXAMPLE
    ./scripts/check-licenses.ps1 -OutputFile artifacts/THIRD_PARTY_LICENSES.txt
#>
[CmdletBinding()]
param(
    [string] $OutputFile = 'artifacts/THIRD_PARTY_LICENSES.txt'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$allowList = Join-Path $PSScriptRoot 'license-allowlist.json'
$licenseTexts = Join-Path $PSScriptRoot 'license-texts'

Push-Location $repoRoot
try {
    dotnet tool restore | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host '::error::dotnet tool restore failed.'
        exit $LASTEXITCODE
    }

    # 1. Every package in the solution must use an allowed license.
    dotnet tool run nuget-license --input Somnus.sln --include-transitive --allowed-license-types $allowList --output Table
    if ($LASTEXITCODE -ne 0) {
        Write-Host '::error::At least one dependency uses a license outside the SWPA 3.5 allow-list.'
        exit 1
    }

    # 2. Notices for everything that ships with the app.
    $tableFile = [System.IO.Path]::GetTempFileName()
    dotnet tool run nuget-license --input src/Somnus.App/Somnus.App.csproj --include-transitive --allowed-license-types $allowList --output Table --file-output $tableFile
    if ($LASTEXITCODE -ne 0) {
        Write-Host '::error::Could not list the licenses of the shipped packages.'
        exit 1
    }

    $outputPath = Join-Path $repoRoot $OutputFile
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null

    $notice = [System.Collections.Generic.List[string]]::new()
    $notice.Add('Somnus - third-party software notices')
    $notice.Add('=====================================')
    $notice.Add('')
    $notice.Add('Somnus itself is licensed under the MIT License (see LICENSE).')
    $notice.Add('Its releases bundle the third-party components listed below.')
    $notice.Add('')
    $notice.Add('NuGet packages')
    $notice.Add('--------------')
    $notice.AddRange([string[]](Get-Content -Path $tableFile -Encoding UTF8))
    $notice.Add('')
    $notice.Add('.NET Desktop Runtime (not bundled)')
    $notice.Add('----------------------------------')
    $notice.Add('Somnus runs on the Microsoft .NET Desktop Runtime 10, which is shared with other apps and comes')
    $notice.Add('from Microsoft (ADR 0010). It is not part of these downloads, so its own notices apply to it.')
    $notice.Add('License: MIT. Copyright (c) .NET Foundation and Contributors.')
    $notice.Add('Sources: https://github.com/dotnet/runtime, https://github.com/dotnet/wpf, https://github.com/dotnet/winforms')

    foreach ($licenseFile in Get-ChildItem -Path $licenseTexts -Filter '*.txt' | Sort-Object Name) {
        $notice.Add('')
        $notice.Add(('=' * 72))
        $notice.Add("Full text: $($licenseFile.BaseName)")
        $notice.Add(('=' * 72))
        $notice.AddRange([string[]](Get-Content -Path $licenseFile.FullName -Encoding UTF8))
    }

    Set-Content -Path $outputPath -Value $notice -Encoding UTF8
    Remove-Item -Path $tableFile -Force
    Write-Host "All dependency licenses are allowed. Notices written to $OutputFile."
}
finally {
    Pop-Location
}
