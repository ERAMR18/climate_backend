param(
    [ValidatePattern('^COM[0-9]+$')][string]$PortName = 'COM3',
    [string]$SketchPath = '',
    [switch]$Upload
)
$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path $PSScriptRoot -Parent
$targetSketchPath = if ($SketchPath) { (Resolve-Path -LiteralPath $SketchPath).Path } else { Join-Path $workspacePath 'hardware/arduino/temperature-sensor' }
$relativeCli = 'resources/app/lib/backend/resources/arduino-cli.exe'
$candidates = @(
    (Join-Path $env:LOCALAPPDATA "Programs/arduino-ide/$relativeCli"),
    (Join-Path $env:ProgramFiles "Arduino IDE/$relativeCli"),
    (Join-Path $env:ProgramFiles "arduino-ide/$relativeCli")
)
$cliPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $cliPath) { throw 'Arduino IDE not found. Install ArduinoSA.IDE.stable with winget.' }
& $cliPath compile --fqbn arduino:avr:uno $targetSketchPath
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed. Ensure Arduino AVR Boards is installed in Boards Manager.' }
if ($Upload) {
    & $cliPath upload --fqbn arduino:avr:uno --port $PortName $targetSketchPath
    if ($LASTEXITCODE -ne 0) { throw "Upload failed on $PortName. Close Serial Monitor and ArduinoBridge, then retry the same command." }
    Write-Output "Sketch uploaded to Arduino Uno on $PortName. Serial speed: 9600 baud."
} else {
    Write-Output "Compilation verified for $targetSketchPath. Repeat the same command adding -Upload -PortName COM3 (adjust port if needed)."
}
