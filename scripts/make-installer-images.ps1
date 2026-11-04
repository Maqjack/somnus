#Requires -Version 5.1
<#
.SYNOPSIS
    Draws the wizard images of the dark installer from the logo (issues #16 and #52).

.DESCRIPTION
    The installer uses Inno Setup's dark style on black (installer/Somnus.iss). This script writes:
    - the crescent on a transparent background as PNG files, for the welcome and finish banner
      (164 x 314, plus 2x) and for the small image in the page header (one file per DPI step);
    - two one-pixel bitmaps that the installer stretches into its own purple progress bar, because
      the dark style paints the standard bar in its own colours.
    The files are generated at build time instead of being committed, and the purple comes from the
    app theme (rule R-02).

.EXAMPLE
    ./scripts/make-installer-images.ps1 -OutputDirectory artifacts/installer-images
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory = 'artifacts/installer-images'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$logoPath = Join-Path $repoRoot 'assets/logo-512.png'
$outputPath = Join-Path $repoRoot $OutputDirectory
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

[xml] $theme = Get-Content -Path (Join-Path $repoRoot 'src/Somnus.App/Theme/Colors.xaml') -Raw
function Get-ThemeColor([string] $key) {
    $hex = ($theme.ResourceDictionary.Color | Where-Object { $_.Key -eq $key }).'#text'.Trim()
    return [System.Drawing.ColorTranslator]::FromHtml($hex)
}

function New-WizardImage {
    param(
        [int] $Width,
        [int] $Height,
        [double] $LogoShare,
        [double] $LogoCenterY,
        [string] $FileName
    )

    $logo = [System.Drawing.Image]::FromFile($logoPath)
    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $size = [int]([Math]::Min($Width, $Height) * $LogoShare)
        $x = [int](($Width - $size) / 2)
        $y = [int]($Height * $LogoCenterY - $size / 2)
        $graphics.DrawImage($logo, $x, $y, $size, $size)

        $bitmap.Save((Join-Path $outputPath $FileName), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
        $logo.Dispose()
    }
}

function New-PixelBitmap {
    param(
        [System.Drawing.Color] $Color,
        [string] $FileName
    )

    $bitmap = New-Object System.Drawing.Bitmap 1, 1, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $bitmap.SetPixel(0, 0, $Color)
        $bitmap.Save((Join-Path $outputPath $FileName), [System.Drawing.Imaging.ImageFormat]::Bmp)
    }
    finally {
        $bitmap.Dispose()
    }
}

New-WizardImage -Width 164 -Height 314 -LogoShare 0.7 -LogoCenterY 0.38 -FileName 'wizard-large.png'
New-WizardImage -Width 328 -Height 628 -LogoShare 0.7 -LogoCenterY 0.38 -FileName 'wizard-large-2x.png'

# The header image area at 100% to 250% scaling (Inno Setup 6.6 and later).
foreach ($size in 58, 77, 97, 116, 124, 143, 159) {
    New-WizardImage -Width $size -Height $size -LogoShare 0.86 -LogoCenterY 0.5 -FileName "wizard-small-$size.png"
}

New-PixelBitmap -Color (Get-ThemeColor 'PrimaryColor') -FileName 'progress-fill.bmp'
New-PixelBitmap -Color (Get-ThemeColor 'SurfaceColor') -FileName 'progress-track.bmp'

Write-Host "Installer images written to $OutputDirectory."
