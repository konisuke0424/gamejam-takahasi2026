# Generates raw voice WAVs from lines.json using Windows SAPI (System.Speech).
# Usage: powershell -ExecutionPolicy Bypass -File Tools/audio/gen_tts.ps1 [-Force]
# Raw files land in Tools/audio/raw/<id>.wav. Existing files are kept unless -Force,
# so hand-recorded replacements survive a regenerate.
param([switch]$Force)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$cfg = Get-Content (Join-Path $root 'lines.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$rawDir = Join-Path $root 'raw'
New-Item -ItemType Directory -Force $rawDir | Out-Null

$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$fmt = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(44100, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)

foreach ($line in $cfg.lines) {
    $out = Join-Path $rawDir ($line.id + '.wav')
    if ((Test-Path $out) -and -not $Force) { Write-Host "keep  $($line.id)"; continue }
    $text = [System.Security.SecurityElement]::Escape($line.text)
    $ssml = "<speak version=`"1.0`" xmlns=`"http://www.w3.org/2001/10/synthesis`" xml:lang=`"ja-JP`">" +
            "<voice name=`"$($cfg.voice)`"><prosody rate=`"$($line.rate)`" pitch=`"$($line.pitch)`">$text</prosody></voice></speak>"
    $synth.SetOutputToWaveFile($out, $fmt)
    $synth.SpeakSsml($ssml)
    $synth.SetOutputToNull()
    Write-Host "write $($line.id)"
}
$synth.Dispose()
