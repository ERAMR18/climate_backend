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
$env:AUTH_DATABASE_URL = 'postgresql://auth:'+$env:PHASE3_TEST_POSTGRES_PASSWORD+'@127.0.0.1:15432/auth'
$env:RABBITMQ_URL = 'amqp://test:'+$env:PHASE3_TEST_RABBIT_PASSWORD+'@127.0.0.1:15672/'
$env:AUDIT_MONGO_URL = 'mongodb://audit:'+$env:PHASE3_TEST_POSTGRES_PASSWORD+'@127.0.0.1:15417/?authSource=admin'
$env:SOURCE_SQLSERVER_CONFIG = @{server='127.0.0.1';port=15433;user='sa';password=$env:PHASE3_TEST_SQL_PASSWORD;database='Phase3AuditMigrationTest';options=@{trustServerCertificate=$true;encrypt=$true}} | ConvertTo-Json -Compress
try {
    & dotnet build (Join-Path $backendPath 'src/Services/AuditService/Climate.Audit.Api/Climate.Audit.Api.csproj') --no-restore --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Legacy Audit build failed.' }
    & docker compose -f $fixturePath --profile audit down --volumes
    if ($LASTEXITCODE -ne 0) { throw 'Cannot reset fixtures.' }
    & docker compose -f $fixturePath --profile audit up -d --wait --wait-timeout 150
    if ($LASTEXITCODE -ne 0) { throw 'Fixtures unavailable.' }
    & node --test (Join-Path $backendPath 'src/Services/AuthService/tests/audit.integration.cjs')
    if ($LASTEXITCODE -ne 0) { throw 'Python Audit integration failed.' }
} finally {
    $runtimeId = & docker ps -aq --filter 'name=^climate-phase3-audit-runtime$'
    if ($runtimeId) { & docker rm -f climate-phase3-audit-runtime }
    & docker compose -f $fixturePath --profile audit stop
}
