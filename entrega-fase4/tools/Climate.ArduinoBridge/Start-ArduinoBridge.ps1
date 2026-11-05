param(
    [switch]$Simulate,
    [string]$PortName='',
    [ValidateSet('Debug','Release')][string]$Configuration='Debug',
    [switch]$NoBuild
)
$ErrorActionPreference='Stop'
$mutex=New-Object Threading.Mutex($false,'Local\ClimateArduinoBridgeLocal')
if (-not $mutex.WaitOne(0)) { $mutex.Dispose(); throw 'ArduinoBridge is already running. Stop its existing terminal with Ctrl+C before starting another instance.' }
try {
$privatePath=Join-Path $PSScriptRoot 'device-token.local'
if (-not (Test-Path -LiteralPath $privatePath)) { throw 'Run backend/scripts/Configure-Phase4Arduino.ps1 -SensorId <UUID> first.' }
$env:Backend__DeviceToken=[IO.File]::ReadAllText($privatePath).Trim()
if ($Simulate) { $env:Arduino__Simulate='true' } else { $env:Arduino__Simulate='false' }
if ($PortName) { $env:Arduino__PortName=$PortName }
Push-Location $PSScriptRoot
try {
    $runArguments=@('run','--project','Climate.ArduinoBridge.csproj','--configuration',$Configuration)
    if ($NoBuild) { $runArguments+='--no-build' }
    & dotnet @runArguments
    if ($LASTEXITCODE -ne 0) { throw 'ArduinoBridge failed. Review the preceding diagnostic; configuration and pending readings were preserved.' }
}
finally { Pop-Location; Remove-Item Env:Backend__DeviceToken -ErrorAction SilentlyContinue }
} finally { $mutex.ReleaseMutex(); $mutex.Dispose() }
