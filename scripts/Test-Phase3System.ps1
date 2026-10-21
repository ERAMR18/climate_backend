param([switch]$Reports, [switch]$LargeReports)
$ErrorActionPreference = 'Stop'
$backendPath = Split-Path $PSScriptRoot -Parent
$fixturePath = Join-Path $PSScriptRoot 'phase3-auth-validation.compose.yml'
$env:PHASE3_TEST_POSTGRES_PASSWORD = [Guid]::NewGuid().ToString('N')
$env:PHASE3_TEST_SQL_PASSWORD = 'Sql-Test-Aa1!' + [Guid]::NewGuid().ToString('N')
$env:PHASE3_TEST_RABBIT_PASSWORD = [Guid]::NewGuid().ToString('N')
$env:JWT_SIGNING_KEY = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
$env:INTERNAL_API_KEY = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
$env:PHASE3_TEST_BACKEND = $backendPath
$env:PHASE3_TEST_COMPOSE = $fixturePath
$env:PHASE3_TEST_REPORTS = if ($Reports) { 'true' } else { 'false' }
if ($LargeReports -and -not $Reports) { throw 'LargeReports requires Reports.' }
$env:PHASE3_TEST_REPORT_ROWS = if ($LargeReports) { '5000' } else { '261' }
try {
    foreach ($entry in @(@('SensorService','Sensors'),@('MonitoringService','Monitoring'),@('EventService','Events'),@('CommunityService','Communities'))) {
        & dotnet build (Join-Path $backendPath ('src/Services/'+$entry[0]+'/Climate.'+$entry[1]+'.Api/Climate.'+$entry[1]+'.Api.csproj')) --no-restore --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw ('Build failed: '+$entry[0]) }
    }
    & dotnet build (Join-Path $backendPath 'src/Gateway/Climate.Gateway/Climate.Gateway.csproj') --no-restore --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Gateway build failed.' }
    & docker compose -f $fixturePath --profile audit down --volumes
    if ($LASTEXITCODE -ne 0) { throw 'Cannot reset test fixtures.' }
    $fixtureServices = if ($Reports) { @('postgres','sqlserver','rabbitmq','mongo') } else { @('postgres','sqlserver','rabbitmq') }
    & docker compose -f $fixturePath --profile audit up -d --wait --wait-timeout 150 @fixtureServices
    if ($LASTEXITCODE -ne 0) { throw 'Test fixtures unavailable.' }
    & node --test (Join-Path $backendPath 'src/Services/AuthService/tests/system.integration.cjs')
    if ($LASTEXITCODE -ne 0) { throw 'System integration failed.' }
} finally {
    $runtimeId = & docker ps -aq --filter 'name=^climate-phase3-system-alert$'
    if ($runtimeId) { & docker stop climate-phase3-system-alert }
    foreach ($runtimeName in @('climate-phase3-system-reports','climate-phase3-system-audit')) {
        $runtimeId = & docker ps -aq --filter ('name=^'+$runtimeName+'$')
        if ($runtimeId) { & docker rm -f $runtimeName }
    }
    & docker compose -f $fixturePath --profile audit stop
}
