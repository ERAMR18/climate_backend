param([switch]$NoBuild, [switch]$BuildAll)
$ErrorActionPreference = 'Stop'
$backendPath = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Initialize-Phase3.ps1')
Push-Location $backendPath
try {
    $arguments = @('compose', '--project-name', 'climate-phase3-local', '--env-file', '.env.phase3.local', '-f', 'docker-compose.yml', '-f', 'docker-compose.phase4.yml')
    & docker @arguments config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Invalid local Phase 4 configuration.' }
    if (-not $NoBuild) {
        if ($BuildAll) { & docker @arguments build }
        else { & docker @arguments build identity-service monitoring-service gateway frontend }
        if ($LASTEXITCODE -ne 0) { throw 'Image build failed; resume with this same command after resolving the reported dependency error.' }
    }
    & docker @arguments up -d --no-build --wait --wait-timeout 300
    if ($LASTEXITCODE -ne 0) { throw 'Phase 4 stack failed; inspect Docker Compose logs. Existing volumes retained.' }
    # Container health probes run inside Docker; also verify Windows published ports.
    function Get-LocalPort([string]$Name, [int]$Default) {
        $entry = Get-Content -LiteralPath '.env.phase3.local' | Where-Object { $_ -match ('^' + $Name + '=') } | Select-Object -Last 1
        if ($entry) { return [int]($entry.Substring($entry.IndexOf('=') + 1)) }
        return $Default
    }
    function Test-LocalEndpoint([string]$Url) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 10 -DisableKeepAlive
            return $response.StatusCode -eq 200
        } catch { return $false }
    }
    function Ensure-PublishedEndpoint([string]$Service, [string]$Url) {
        if (Test-LocalEndpoint $Url) { return }
        Write-Output "Published port for $Service did not respond; recreating only that local container. Data volumes are preserved."
        & docker @arguments up -d --no-deps --no-build --force-recreate --wait --wait-timeout 120 $Service
        if ($LASTEXITCODE -ne 0 -or -not (Test-LocalEndpoint $Url)) {
            throw "Published endpoint $Url is unavailable. Resume Start-Phase4.ps1 -NoBuild after checking Docker Desktop port forwarding."
        }
    }
    $gatewayPort = Get-LocalPort 'GATEWAY_PORT' 8080
    $frontendPort = Get-LocalPort 'FRONTEND_PORT' 4200
    Ensure-PublishedEndpoint 'gateway' "http://127.0.0.1:$gatewayPort/health/live"
    Ensure-PublishedEndpoint 'frontend' "http://127.0.0.1:$frontendPort/health"
    & (Join-Path $PSScriptRoot 'Seed-Phase3Demo.ps1') -GatewayUrl "http://127.0.0.1:$gatewayPort"
    Write-Output 'Phase 4 local: http://localhost:4200 ; Gateway http://localhost:8080. See docs/FASE_4_ARDUINO.md for device registration.'
} finally { Pop-Location }
