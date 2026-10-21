param([string]$Tag = 'phase2', [switch]$IncludeFrontend, [string]$FrontendPath = '', [string]$ImagePrefix = 'climate', [string]$NpmCaFile = '', [switch]$Phase3)
$ErrorActionPreference = 'Stop'
$backendRoot = Split-Path $PSScriptRoot -Parent
$entries = @(@('gateway','src/Gateway/Climate.Gateway/Dockerfile'),
    @('identity','src/Services/IdentityService/Climate.Identity.Api/Dockerfile'),
    @('sensors','src/Services/SensorService/Climate.Sensors.Api/Dockerfile'),
    @('monitoring','src/Services/MonitoringService/Climate.Monitoring.Api/Dockerfile'),
    @('alerts','src/Services/AlertService/Climate.Alerts.Api/Dockerfile'),
    @('events','src/Services/EventService/Climate.Events.Api/Dockerfile'),
    @('audit','src/Services/AuditService/Climate.Audit.Api/Dockerfile'))
if ($Phase3) {
    if (-not $PSBoundParameters.ContainsKey('Tag')) { $Tag = 'phase3' }
    $entries = @(@('gateway','src/Gateway/Climate.Gateway/Dockerfile'),
        @('auth','Dockerfile','src/Services/AuthService'),
        @('community','src/Services/CommunityService/Climate.Communities.Api/Dockerfile'),
        @('sensors','src/Services/SensorService/Climate.Sensors.Api/Dockerfile'),
        @('monitoring','src/Services/MonitoringService/Climate.Monitoring.Api/Dockerfile'),
        @('alerts','Dockerfile','src/Services/AlertServiceJava'),
        @('events','src/Services/EventService/Climate.Events.Api/Dockerfile'),
        @('audit','src/Services/AuditServicePython/Dockerfile'),
        @('reports','src/Services/ReportService/Dockerfile'))
}
foreach ($entry in $entries) {
    $buildContext = if ($entry.Count -gt 2) { Join-Path $backendRoot $entry[2] } else { $backendRoot }
    Write-Output "Building $ImagePrefix/$($entry[0]):$Tag"
    docker build --quiet --tag "$ImagePrefix/$($entry[0]):$Tag" --file (Join-Path $buildContext $entry[1]) $buildContext
    if ($LASTEXITCODE -ne 0) { throw "Image build failed: $($entry[0])" }
}
if ($IncludeFrontend) {
    if (-not $FrontendPath) { $FrontendPath = Join-Path (Split-Path $backendRoot -Parent) 'frontend/climate-monitoring-web' }
    $extra = @()
    if ($NpmCaFile) { $extra = @('--secret', "id=npm_ca,src=$([IO.Path]::GetFullPath($NpmCaFile))") }
    docker build --quiet @extra --tag "$ImagePrefix/frontend:$Tag" $FrontendPath
    if ($LASTEXITCODE -ne 0) { throw 'Frontend image build failed.' }
}
