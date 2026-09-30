param([string]$Tag = 'phase2', [string]$BrowserScript = '', [switch]$TestRecovery)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = 'climate-images-test-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$override = Join-Path ([IO.Path]::GetTempPath()) "$project.yaml"
$variables = @('SQLSERVER_SA_PASSWORD','SQLSERVER_PORT','GATEWAY_PORT','JWT_SIGNING_KEY','INTERNAL_API_KEY','ADMIN_SEED_PASSWORD','FRONTEND_ORIGIN','RABBITMQ_USER','RABBITMQ_PASSWORD','SIMULATION_ENABLED')
$original = @{}
foreach ($key in $variables) { $original[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
$env:SQLSERVER_SA_PASSWORD = 'Sql!' + [Guid]::NewGuid().ToString('N')
$env:SQLSERVER_PORT = '14349'
$env:GATEWAY_PORT = '18090'
$env:JWT_SIGNING_KEY = [Guid]::NewGuid().ToString('N')
$env:INTERNAL_API_KEY = [Guid]::NewGuid().ToString('N')
$env:ADMIN_SEED_PASSWORD = 'Admin!' + [Guid]::NewGuid().ToString('N')
$env:FRONTEND_ORIGIN = 'http://127.0.0.1:14200'
$env:RABBITMQ_USER = 'image-test'
$env:RABBITMQ_PASSWORD = [Guid]::NewGuid().ToString('N')
$env:SIMULATION_ENABLED = 'false'
$yaml = "services:`n"
foreach ($pair in @(@('gateway','gateway'),@('identity-service','identity'),@('sensor-service','sensors'),@('monitoring-service','monitoring'),@('alert-service','alerts'),@('event-service','events'),@('audit-service','audit'))) {
    $yaml += "  $($pair[0]):`n    image: climate/$($pair[1]):$Tag`n    read_only: true`n    tmpfs: [/tmp]`n    cap_drop: [ALL]`n"
}
$yaml += @"
  rabbitmq:
    environment:
      RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS: '+S 2:2'
      RABBITMQ_CTL_ERL_ARGS: '+S 1:1'
  frontend:
    image: climate/frontend:$Tag
    read_only: true
    cap_drop: [ALL]
    ports: ['127.0.0.1:14200:8080']
    environment:
      GATEWAY_PUBLIC_URL: http://127.0.0.1:18090
    networks: [backend]
"@
[IO.File]::WriteAllText($override, $yaml)
$compose = @('compose','--project-name',$project,'--env-file',"$root/.env.example",'-f',"$root/docker-compose.yml",'-f',$override)
try {
    & docker @compose up -d --no-build --wait --wait-timeout 240
    if ($LASTEXITCODE -ne 0) { throw 'Image stack did not become healthy.' }
    $login = Invoke-RestMethod http://127.0.0.1:18090/api/auth/login -Method Post -ContentType application/json -Body (@{login='admin';password=$env:ADMIN_SEED_PASSWORD}|ConvertTo-Json)
    if (-not $login.accessToken) { throw 'Gateway login failed.' }
    $config = Invoke-RestMethod http://127.0.0.1:14200/runtime-config.json
    if ($config.apiUrl -ne 'http://127.0.0.1:18090') { throw 'Incorrect runtime Gateway URL.' }
    foreach ($url in @('http://127.0.0.1:14200/health','http://127.0.0.1:14200/alert-rules','http://127.0.0.1:18090/health/live')) {
        if ((Invoke-WebRequest $url -UseBasicParsing).StatusCode -ne 200) { throw "Failed: $url" }
    }
    foreach ($service in @('gateway','identity-service','sensor-service','monitoring-service','alert-service','event-service','audit-service','frontend')) {
        $uid = & docker @compose exec -T $service id -u
        if ($LASTEXITCODE -ne 0 -or $uid -eq '0') { throw "Root or unavailable container: $service" }
    }
    $json = & docker @compose exec -T gateway dotnet /app/health/Climate.HealthProbe.dll http://localhost:8080/swagger/v1/swagger.json --print
    if ($LASTEXITCODE -ne 0 -or -not ($json | ConvertFrom-Json).paths) { throw 'OpenAPI download failed.' }
    if ($TestRecovery) { . (Join-Path $PSScriptRoot 'Test-RecoverySteps.ps1') }
    if ($BrowserScript) {
        & node $BrowserScript
        if ($LASTEXITCODE -ne 0) { throw 'Browser validation failed.' }
    }
    Write-Output 'PASS: eight healthy non-root images, read-only application filesystems, SQL/RabbitMQ, Gateway login, OpenAPI, frontend runtime config and deep links.'
} finally {
    & docker @compose down --volumes --remove-orphans
    Remove-Item -LiteralPath $override -ErrorAction SilentlyContinue
    foreach ($key in $variables) { [Environment]::SetEnvironmentVariable($key, $original[$key], 'Process') }
}
