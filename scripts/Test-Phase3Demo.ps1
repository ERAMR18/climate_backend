param([string]$EnvironmentFile='', [string]$GatewayUrl='http://localhost:8080')
$ErrorActionPreference='Stop'
# Reuse the same login and UTF-8 API client as the seed, and verify a repeat run.
. (Join-Path $PSScriptRoot 'Seed-Phase3Demo.ps1') -EnvironmentFile $EnvironmentFile -GatewayUrl $GatewayUrl
$before=@{communities=@(Request '/api/communities').Count;sensors=@(Request '/api/sensors').Count;rules=@(Request '/api/alert-rules').Count}
. (Join-Path $PSScriptRoot 'Seed-Phase3Demo.ps1') -EnvironmentFile $EnvironmentFile -GatewayUrl $GatewayUrl
$after=@{communities=@(Request '/api/communities').Count;sensors=@(Request '/api/sensors').Count;rules=@(Request '/api/alert-rules').Count}
foreach($key in $before.Keys){if($before[$key] -ne $after[$key]){throw "Repeated seed duplicated $key."}}
$initial=(Request '/api/monitoring/readings?pageSize=1').total
$deadline=[DateTime]::UtcNow.AddSeconds(50)
do {
    Start-Sleep -Seconds 3
    $final=(Request '/api/monitoring/readings?pageSize=1').total
} while($final -le $initial -and [DateTime]::UtcNow -lt $deadline)
if($final -le $initial){throw 'Simulation did not generate new readings within 50 seconds.'}
$dashboard=Request '/api/dashboard/summary'
if($dashboard.communityCount -lt 3 -or $dashboard.activeSensorCount -lt 15 -or @($dashboard.readings).Count -lt 15 -or @($dashboard.climateStatistics).Count -lt 5 -or -not $dashboard.simulation.isRunning){throw 'Demo dashboard is incomplete.'}
$evidence=@{verifiedAt=[DateTime]::UtcNow.ToString('o');catalogs=$after;seedIdempotent=$true;readingsBefore=$initial;readingsAfter=$final;simulation=$dashboard.simulation;dashboard=@{communities=$dashboard.communityCount;activeSensors=$dashboard.activeSensorCount;activeAlerts=$dashboard.activeAlertCount;currentReadings=@($dashboard.readings).Count;statisticGroups=@($dashboard.climateStatistics).Count;evolutionPoints=@($dashboard.evolution).Count}}
$path=Join-Path (Split-Path $PSScriptRoot -Parent) 'docs/validation/phase3-demo.json'
[IO.File]::WriteAllText($path,($evidence|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
$evidence | ConvertTo-Json -Depth 8
