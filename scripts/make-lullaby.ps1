#Requires -Version 5.1
<#
.SYNOPSIS
    Turns a lullaby recording into assets/lullaby.wav, the sound Somnus plays in the last 20 seconds
    of a countdown (issue #71).

.DESCRIPTION
    1. Speeds the recording up by -Tempo without changing its pitch (ffmpeg atempo).
    2. Keeps the first -Seconds, fading out over the last two seconds.
    3. Mixes it to mono at 24 kHz and sets its loudest moment to -PeakDb dBFS, so it stays gentle.
       24 kHz keeps the 20 seconds under the 1 MB limit of the large-file hook.
    4. Writes 16-bit PCM WAV without encoder metadata, the format System.Media.SoundPlayer plays.

    Needs ffmpeg on PATH (winget install Gyan.FFmpeg). The result is committed; this script only
    documents and repeats how it was made.

.EXAMPLE
    ./scripts/make-lullaby.ps1 -Source C:\apki\Somnus\zzz.mp3
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Source,

    [string] $Output = 'assets/lullaby.wav',

    [ValidateRange(0.5, 4.0)]
    [double] $Tempo = 2.0,

    [ValidateRange(5, 60)]
    [double] $Seconds = 20,

    [ValidateRange(-20, 0)]
    [double] $PeakDb = -3
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $repoRoot $Output
$culture = [Globalization.CultureInfo]::InvariantCulture
$fadeStart = ($Seconds - 2).ToString($culture)
$shape = 'atempo={0},atrim=0:{1},asetpts=PTS-STARTPTS,afade=t=out:st={2}:d=2,aresample=24000,pan=mono|c0=0.5*c0+0.5*c1' -f $Tempo.ToString($culture), $Seconds.ToString($culture), $fadeStart

Write-Host '== Measure the loudest moment'
# ffmpeg reports on stderr, which Windows PowerShell would turn into terminating errors here.
$ErrorActionPreference = 'Continue'
$report = & ffmpeg -hide_banner -nostats -i $Source -af "$shape,volumedetect" -f null - 2>&1 | Out-String
$exitCode = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($exitCode -ne 0) {
    throw "ffmpeg could not read $Source."
}

if ($report -notmatch 'max_volume: (-?[\d.]+) dB') {
    throw 'ffmpeg did not report the loudest moment.'
}

$gain = $PeakDb - [double]::Parse($Matches[1], $culture)
Write-Host ([string]::Format($culture, 'Loudest moment {0} dB; gain {1:0.0} dB', $Matches[1], $gain))

Write-Host "== Write $Output"
& ffmpeg -hide_banner -loglevel error -y -i $Source -af ("{0},volume={1}dB" -f $shape, $gain.ToString('0.0', $culture)) -c:a pcm_s16le -map_metadata -1 -fflags +bitexact -flags:a +bitexact $outputPath
if ($LASTEXITCODE -ne 0) {
    throw "ffmpeg could not write $Output."
}

Write-Host ('{0}: {1:N0} KB' -f $Output, ((Get-Item $outputPath).Length / 1KB))
