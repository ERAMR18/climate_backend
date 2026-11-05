param([string]$SensorId, [ValidateSet('SIMULATED','ARDUINO')][string]$Source='SIMULATED', [string]$DeviceId='arduino-temp-001')
$ErrorActionPreference='Stop'
if (-not $SensorId) { throw 'Provide -SensorId with an active Temperature sensor UUID from /sensors.' }
$parsedId=[Guid]::Empty
if (-not [Guid]::TryParse($SensorId,[ref]$parsedId) -or $parsedId -eq [Guid]::Empty) { throw 'Invalid sensor UUID.' }
if ($DeviceId -notmatch '^[A-Za-z0-9_-]{1,100}$') { throw 'Invalid device ID.' }
$backendPath=Split-Path $PSScriptRoot -Parent
$workspacePath=Split-Path $backendPath -Parent
$environmentPath=Join-Path $backendPath '.env.phase3.local'
$bridgePath=Join-Path $workspacePath 'tools/Climate.ArduinoBridge'
$privatePath=Join-Path $bridgePath 'device-token.local'
if (-not (Test-Path -LiteralPath $environmentPath)) { throw 'Run Initialize-Phase3.ps1 first.' }
if (-not (Test-Path -LiteralPath $privatePath)) {
    $bytes=New-Object byte[] 32
    $random=[Security.Cryptography.RandomNumberGenerator]::Create()
    try { $random.GetBytes($bytes) } finally { $random.Dispose() }
    [IO.File]::WriteAllText($privatePath,[Convert]::ToBase64String($bytes))
}
$credential=[IO.File]::ReadAllText($privatePath).Trim()
$algorithm=[Security.Cryptography.SHA256]::Create()
try { $hash=([BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($credential)))).Replace('-','').ToLowerInvariant() }
finally { $algorithm.Dispose() }
$updates=@{ARDUINO_SENSOR_ID=$SensorId;ARDUINO_DEVICE_ID=$DeviceId;ARDUINO_SOURCE=$Source;ARDUINO_ENABLED='true';ARDUINO_TOKEN_SHA256=$hash}
$lines=@(Get-Content -LiteralPath $environmentPath | Where-Object { $_ -notmatch '^ARDUINO_(SENSOR_ID|DEVICE_ID|SOURCE|ENABLED|TOKEN_SHA256)=' })
foreach ($key in ($updates.Keys | Sort-Object)) { $lines += "$key=$($updates[$key])" }
[IO.File]::WriteAllLines($environmentPath,$lines)
$configPath=Join-Path $bridgePath 'appsettings.json'
if (Test-Path -LiteralPath $configPath) { $config=Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json }
else { $config=Get-Content -LiteralPath (Join-Path $bridgePath 'appsettings.example.json') -Raw | ConvertFrom-Json }
$config.Device.DeviceId=$DeviceId
$config.Device.SensorId=$SensorId
[IO.File]::WriteAllText($configPath,($config | ConvertTo-Json -Depth 8))
Write-Output 'Device association saved without printing the credential. Restart Phase 4 to load it. Run the bridge through Start-ArduinoBridge.ps1.'
