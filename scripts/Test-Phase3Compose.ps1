param([switch]$Browser, [switch]$Recovery)
$ErrorActionPreference = 'Stop'
$backendPath = Split-Path $PSScriptRoot -Parent
$composePath = Join-Path $backendPath 'docker-compose.yml'
$temporaryEnvironment = Join-Path ([IO.Path]::GetTempPath()) ('climate-phase3-'+[Guid]::NewGuid().ToString('N')+'.env')
$values = @{}
foreach ($line in Get-Content (Join-Path $backendPath '.env.example')) {
    if ($line -match '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$') { $values[$matches[1]] = $matches[2] }
}
foreach ($name in @('SQLSERVER_SA_PASSWORD','JWT_SIGNING_KEY','INTERNAL_API_KEY','AUTH_POSTGRES_PASSWORD','COMMUNITY_POSTGRES_PASSWORD','ALERT_POSTGRES_PASSWORD','AUDIT_MONGO_PASSWORD','REPORT_MONGO_PASSWORD','RABBITMQ_PASSWORD','AUTH_ADMIN_SEED_PASSWORD')) {
    $values[$name] = 'Test-Aa1!'+[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
}
foreach ($name in @('AUTH_POSTGRES_PASSWORD','AUDIT_MONGO_PASSWORD','REPORT_MONGO_PASSWORD','RABBITMQ_PASSWORD')) { $values[$name+'_URI'] = [Uri]::EscapeDataString($values[$name]) }
$values['RABBITMQ_USER']='phase3test'; $values['RABBITMQ_USER_URI']='phase3test'
$values['SQLSERVER_PORT']='15443'; $values['GATEWAY_PORT']='15280'; $values['FRONTEND_PORT']='14220'
$values['FRONTEND_ORIGIN']='http://localhost:14220'; $values['GATEWAY_PUBLIC_URL']='http://localhost:15280'
$values['SIMULATION_ENABLED']='false'; $values['DEMO_SEED_ENABLED']='false'
[IO.File]::WriteAllLines($temporaryEnvironment, @($values.GetEnumerator() | ForEach-Object { $_.Key+'='+$_.Value }), (New-Object Text.UTF8Encoding($false)))
$composeArguments = @('compose','--project-name','climate-phase3-final-validation','--env-file',$temporaryEnvironment,'-f',$composePath)
try {
    & docker @composeArguments down --volumes
    if ($LASTEXITCODE -ne 0) { throw 'Cannot reset disposable final stack.' }
    & docker @composeArguments config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Invalid final Compose configuration.' }
    & docker @composeArguments up -d --no-build --wait --wait-timeout 240
    if ($LASTEXITCODE -ne 0) { throw 'Final Compose stack failed readiness.' }
    $login = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:15280/api/auth/login' -ContentType 'application/json' -Body (@{login='admin';password=$values['AUTH_ADMIN_SEED_PASSWORD']} | ConvertTo-Json)
    $headers = @{Authorization='Bearer '+$login.accessToken}
    $types = Invoke-RestMethod -Uri 'http://127.0.0.1:15280/api/reports/types' -Headers $headers
    if ($types.Count -ne 8) { throw 'Report catalog missing in final Compose.' }
    $report = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:15280/api/reports/generate' -Headers $headers -ContentType 'application/json' -Body '{"type":"COMMUNITIES"}'
    for ($attempt=0; $attempt -lt 60; $attempt++) {
        $status=Invoke-RestMethod -Uri ('http://127.0.0.1:15280/api/reports/'+$report.id) -Headers $headers
        if ($status.status -in @('COMPLETED','FAILED')) { break }
        Start-Sleep -Seconds 1
    }
    if ($status.status -ne 'COMPLETED') { throw 'Final stack PDF did not complete.' }
    $frontend = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:14220/reports'
    if ($frontend.StatusCode -ne 200) { throw 'Frontend report route failed.' }
    $health = Invoke-RestMethod -Uri 'http://127.0.0.1:15280/health'
    if ($health.status -ne 'Healthy') { throw 'Final aggregate health failed.' }
    if ($Browser) {
        $frontendPath = Join-Path (Split-Path $backendPath -Parent) 'frontend/climate-monitoring-web'
        $nodePath = Join-Path (Split-Path $backendPath -Parent) 'tools/node/node.exe'
        $env:ADMIN_SEED_PASSWORD = $values['AUTH_ADMIN_SEED_PASSWORD']
        $env:FRONTEND_TEST_URL = 'http://localhost:14220'
        $env:GATEWAY_TEST_URL = 'http://localhost:15280'
        Push-Location $frontendPath
        try {
            & $nodePath scripts/test-browser.mjs
            if ($LASTEXITCODE -ne 0) { throw 'Final real browser regression failed.' }
        } finally { Pop-Location }
    }
    if ($Recovery) { & (Join-Path $PSScriptRoot 'Test-Phase3Recovery.ps1') -EnvironmentFile $temporaryEnvironment }
    Write-Output 'PASS final Compose: all service healthchecks, real seeded Auth login, Gateway, eight report types, PDF generation, frontend route and aggregate health.'
} finally {
    & docker @composeArguments stop
    Remove-Item -LiteralPath $temporaryEnvironment -ErrorAction SilentlyContinue
}
