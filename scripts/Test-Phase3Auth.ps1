$ErrorActionPreference = 'Stop'
$backendPath = Split-Path $PSScriptRoot -Parent
$authPath = Join-Path $backendPath 'src/Services/AuthService'
$fixturePath = Join-Path $PSScriptRoot 'phase3-auth-validation.compose.yml'
$env:PHASE3_TEST_POSTGRES_PASSWORD = [Guid]::NewGuid().ToString('N')
$env:PHASE3_TEST_SQL_PASSWORD = 'Sql-Test-Aa1!' + [Guid]::NewGuid().ToString('N')
$env:PHASE3_TEST_RABBIT_PASSWORD = [Guid]::NewGuid().ToString('N')
$env:AUTH_DATABASE_URL = 'postgresql://auth:' + $env:PHASE3_TEST_POSTGRES_PASSWORD + '@127.0.0.1:15432/auth'
$env:RABBITMQ_URL = 'amqp://test:' + $env:PHASE3_TEST_RABBIT_PASSWORD + '@127.0.0.1:15672'
$env:JWT_SIGNING_KEY = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
$env:SOURCE_SQLSERVER_CONFIG = @{server='127.0.0.1';port=15433;user='sa';password=$env:PHASE3_TEST_SQL_PASSWORD;database='Phase3AuthMigrationTest';options=@{trustServerCertificate=$true;encrypt=$true}} | ConvertTo-Json -Compress
$env:PHASE3_INTEROP_DLL = Join-Path $backendPath 'tools/Phase3Interop/bin/Debug/net10.0/Phase3Interop.dll'
$env:ADMIN_SEED_PASSWORD = ''
$env:PHASE3_TEST_BACKEND = $backendPath
$env:PHASE3_TEST_COMPOSE = $fixturePath
$env:COMMUNITY_DATABASE_URL = 'postgresql://auth:' + $env:PHASE3_TEST_POSTGRES_PASSWORD + '@127.0.0.1:15432/community'
$env:NODE_EXTRA_CA_CERTS = Join-Path (Split-Path $backendPath -Parent) 'tools/trusted-roots.pem'
try {
    # Reset only this named disposable test project, so random credentials match fresh fixtures.
    & docker compose -f $fixturePath down --volumes
    if ($LASTEXITCODE -ne 0) { throw 'Cannot reset isolated integration fixtures.' }
    & docker compose -f $fixturePath up -d --wait --wait-timeout 150
    if ($LASTEXITCODE -ne 0) { throw 'Integration dependencies failed.' }
    & dotnet build (Join-Path $backendPath 'tools/Phase3Interop/Phase3Interop.csproj') --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Interop helper build failed.' }
    & dotnet build (Join-Path $backendPath 'src/Gateway/Climate.Gateway/Climate.Gateway.csproj') --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Gateway build failed.' }
    & dotnet build (Join-Path $backendPath 'src/Services/CommunityService/Climate.Communities.Api/Climate.Communities.Api.csproj') --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Community build failed.' }
    & dotnet build (Join-Path $backendPath 'src/Services/SensorService/Climate.Sensors.Api/Climate.Sensors.Api.csproj') --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Sensor build failed.' }
    & dotnet build (Join-Path $backendPath 'src/Services/AlertService/Climate.Alerts.Api/Climate.Alerts.Api.csproj') --no-restore --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Legacy Alert fixture build failed.' }
    Push-Location $authPath
    try {
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Auth build failed.' }
        & node --test tests/integration.cjs
        if ($LASTEXITCODE -ne 0) { throw 'Auth integration failed.' }
        & node --test tests/community.integration.cjs
        if ($LASTEXITCODE -ne 0) { throw 'Community integration failed.' }
        & node --test tests/alerts-migration.cjs
        if ($LASTEXITCODE -ne 0) { throw 'Alert migration integration failed.' }
    } finally { Pop-Location }
} finally {
    # Retain fixture data for diagnosis; no application volumes or containers are touched.
    & docker compose -f $fixturePath stop
    foreach ($name in @('PHASE3_TEST_POSTGRES_PASSWORD','PHASE3_TEST_SQL_PASSWORD','PHASE3_TEST_RABBIT_PASSWORD',
        'AUTH_DATABASE_URL','COMMUNITY_DATABASE_URL','RABBITMQ_URL','JWT_SIGNING_KEY','SOURCE_SQLSERVER_CONFIG','ADMIN_SEED_PASSWORD')) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
}
