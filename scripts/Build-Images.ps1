param([string]$Tag = 'phase2', [switch]$IncludeFrontend, [string]$FrontendPath = '', [string]$ImagePrefix = 'climate', [string]$NpmCaFile = '')
$ErrorActionPreference = 'Stop'
$backendRoot = Split-Path $PSScriptRoot -Parent
$entries = @(@('gateway','src/Gateway/Climate.Gateway/Dockerfile'),
    @('identity','src/Services/IdentityService/Climate.Identity.Api/Dockerfile'),
    @('sensors','src/Services/SensorService/Climate.Sensors.Api/Dockerfile'),
    @('monitoring','src/Services/MonitoringService/Climate.Monitoring.Api/Dockerfile'),
    @('alerts','src/Services/AlertService/Climate.Alerts.Api/Dockerfile'),
    @('events','src/Services/EventService/Climate.Events.Api/Dockerfile'),
    @('audit','src/Services/AuditService/Climate.Audit.Api/Dockerfile'))
foreach ($entry in $entries) {
    Write-Output "Building $ImagePrefix/$($entry[0]):$Tag"
    docker build --quiet --tag "$ImagePrefix/$($entry[0]):$Tag" --file (Join-Path $backendRoot $entry[1]) $backendRoot
    if ($LASTEXITCODE -ne 0) { throw "Image build failed: $($entry[0])" }
}
if ($IncludeFrontend) {
    if (-not $FrontendPath) { $FrontendPath = Join-Path (Split-Path $backendRoot -Parent) 'frontend/climate-monitoring-web' }
    $extra = @()
    if ($NpmCaFile) { $extra = @('--secret', "id=npm_ca,src=$([IO.Path]::GetFullPath($NpmCaFile))") }
    docker build --quiet @extra --tag "$ImagePrefix/frontend:$Tag" $FrontendPath
    if ($LASTEXITCODE -ne 0) { throw 'Frontend image build failed.' }
}
