$ErrorActionPreference = 'Stop'
$backendPath = Split-Path $PSScriptRoot -Parent
$fixturePath = Join-Path $PSScriptRoot 'phase3-auth-validation.compose.yml'
$javaPath = Join-Path $backendPath 'src/Services/AlertServiceJava'
$env:PHASE3_TEST_POSTGRES_PASSWORD = [Guid]::NewGuid().ToString('N')
$env:PHASE3_TEST_SQL_PASSWORD = 'Sql-Test-Aa1!' + [Guid]::NewGuid().ToString('N')
$env:PHASE3_TEST_RABBIT_PASSWORD = [Guid]::NewGuid().ToString('N')
$env:ALERT_DATABASE_URL = 'jdbc:postgresql://postgres:5432/alerts_test'
$env:ALERT_DATABASE_USER = 'auth'
$env:ALERT_DATABASE_PASSWORD = $env:PHASE3_TEST_POSTGRES_PASSWORD
$env:ALERT_INTEGRATION = 'true'
$env:JWT_SIGNING_KEY = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
$env:INTERNAL_API_KEY = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
$env:PHASE3_TEST_COMPOSE = $fixturePath
try {
    & docker compose -f $fixturePath down --volumes
    if ($LASTEXITCODE -ne 0) { throw 'Cannot reset isolated integration fixtures.' }
    & docker compose -f $fixturePath up -d --wait --wait-timeout 150 postgres rabbitmq
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL integration fixture failed.' }
    & docker compose -f $fixturePath exec -T postgres psql -U auth -d auth -c 'CREATE DATABASE alerts_test'
    if ($LASTEXITCODE -ne 0) { throw 'Cannot create disposable alert database.' }
    & docker run --rm --network climate-phase3-auth-validation_default -v "${javaPath}:/app" -v climate-phase3-maven:/root/.m2 -w /app -e ALERT_DATABASE_URL -e ALERT_DATABASE_USER -e ALERT_DATABASE_PASSWORD -e ALERT_INTEGRATION -e JWT_SIGNING_KEY -e INTERNAL_API_KEY -e RABBITMQ_HOST=unused -e RABBITMQ_USER=unused -e RABBITMQ_PASSWORD=unused -e SENSOR_SERVICE_URL=http://unused -e EVENT_SERVICE_URL=http://unused -e REALTIME_SERVICE_URL=http://unused maven:3.9-eclipse-temurin-21 mvn -B -ntp verify
    if ($LASTEXITCODE -ne 0) { throw 'Java Alert integration failed.' }
    & docker compose -f $fixturePath exec -T postgres psql -U auth -d auth -c 'CREATE DATABASE alerts_runtime'
    if ($LASTEXITCODE -ne 0) { throw 'Cannot create disposable Java runtime database.' }
    $env:ALERT_DATABASE_URL = 'jdbc:postgresql://postgres:5432/alerts_runtime'
    $env:ALERT_POSTGRES_URL = 'postgresql://auth:' + $env:PHASE3_TEST_POSTGRES_PASSWORD + '@127.0.0.1:15432/alerts_runtime'
    $env:RABBITMQ_PASSWORD = $env:PHASE3_TEST_RABBIT_PASSWORD
    $env:RABBITMQ_URL = 'amqp://test:' + $env:PHASE3_TEST_RABBIT_PASSWORD + '@127.0.0.1:15672'
    & docker run --rm -d --name climate-phase3-alert-runtime --network climate-phase3-auth-validation_default -p 127.0.0.1:15104:8080 -e ALERT_DATABASE_URL -e ALERT_DATABASE_USER -e ALERT_DATABASE_PASSWORD -e JWT_SIGNING_KEY -e INTERNAL_API_KEY -e RABBITMQ_HOST=rabbitmq -e RABBITMQ_USER=test -e RABBITMQ_PASSWORD -e SENSOR_SERVICE_URL=http://sensor-service:8080/ -e EVENT_SERVICE_URL=http://event-service:8080/ -e REALTIME_SERVICE_URL=http://monitoring-service:8080/ climate/alerts:phase3
    if ($LASTEXITCODE -ne 0) { throw 'Cannot start Java runtime smoke container; build climate/alerts:phase3 first.' }
    & node (Join-Path $backendPath 'tools/Phase3Migration/smoke-alerts.cjs')
    if ($LASTEXITCODE -ne 0) { throw 'Java container integration failed.' }
} finally {
    $runtimeId = & docker ps -aq --filter 'name=^climate-phase3-alert-runtime$'
    if ($runtimeId) { & docker stop climate-phase3-alert-runtime }
    & docker compose -f $fixturePath stop
    foreach ($name in @('PHASE3_TEST_POSTGRES_PASSWORD','PHASE3_TEST_SQL_PASSWORD','PHASE3_TEST_RABBIT_PASSWORD',
        'ALERT_DATABASE_URL','ALERT_DATABASE_USER','ALERT_DATABASE_PASSWORD','ALERT_POSTGRES_URL','ALERT_INTEGRATION','JWT_SIGNING_KEY','INTERNAL_API_KEY','RABBITMQ_PASSWORD','RABBITMQ_URL')) {
        [Environment]::SetEnvironmentVariable($name,$null,'Process')
    }
}
