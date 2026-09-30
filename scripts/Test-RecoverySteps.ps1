# Runs only inside Test-Images.ps1's isolated project and cleanup boundary.
if (-not $project -or $project -notmatch '^climate-images-test-[a-f0-9]{8}$') { throw 'Use Test-Images.ps1 -TestRecovery.' }
function Invoke-TestSql([string]$Query) {
    $result = & docker @compose exec -T -e "SQLCMDPASSWORD=$env:SQLSERVER_SA_PASSWORD" sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -h -1 -W -Q "SET NOCOUNT ON; $Query"
    if ($LASTEXITCODE -ne 0) { throw 'Recovery SQL assertion failed.' }
    return ($result -join "`n").Trim()
}
function Wait-TestCondition([scriptblock]$Condition, [string]$Description) {
    for ($attempt = 0; $attempt -lt 45; $attempt++) {
        if (& $Condition) { return }
        Start-Sleep -Seconds 2
    }
    throw "Timed out: $Description"
}
function Get-TestQueue([string]$Name = 'climate.audit.events') {
    $json = & docker @compose exec -T rabbitmq rabbitmqctl list_queues name messages --formatter json
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect RabbitMQ queues.' }
    $rows = ConvertFrom-Json -InputObject ($json -join "`n")
    $queue = @($rows) | Where-Object { $_.name -eq $Name }
    if (-not $queue) { throw "Queue does not exist: $Name. Broker output: $($json -join ' ')" }
    return [int]$queue.messages
}

& docker @compose stop audit-service
if ($LASTEXITCODE -ne 0) { throw 'Cannot stop consumer.' }
$sensorAuth = @{ Authorization = 'Bearer ' + $login.accessToken }
$sensors = Invoke-RestMethod http://127.0.0.1:18090/api/sensors -Headers $sensorAuth
$sensor = @($sensors)[0]
if (-not $sensor.id) { throw 'Missing seeded sensor for decoupling test.' }
$sensor.name = 'Updated while Audit is stopped'
$updated = Invoke-RestMethod "http://127.0.0.1:18090/api/sensors/$($sensor.id)" -Headers $sensorAuth -Method Put -ContentType application/json -Body ($sensor | ConvertTo-Json -Depth 5)
if ($updated.name -ne $sensor.name) { throw 'UpdateSensor did not succeed while Audit was stopped.' }
$savedName = Invoke-TestSql "SELECT Name FROM SensorDb.dbo.Sensors WHERE Id = '$($sensor.id)'"
if ($savedName -ne $sensor.name) { throw 'UpdateSensor was not committed to SensorDb.' }
Write-Output 'PASS: Audit stopped; UpdateSensor returned success and SensorDb contains the change.'
for ($i = 0; $i -lt 12; $i++) {
    $response = Invoke-RestMethod http://127.0.0.1:18090/api/auth/login -Method Post -ContentType application/json -Body (@{login='admin';password=$env:ADMIN_SEED_PASSWORD}|ConvertTo-Json)
    if (-not $response.accessToken) { throw 'Business operation failed while Audit was stopped.' }
}
Wait-TestCondition { (Invoke-TestSql 'SELECT COUNT(*) FROM IdentityDb.dbo.AuditOutbox WHERE PublishedAt IS NULL') -eq '0' } 'outbox publication'
Wait-TestCondition { (Invoke-TestSql 'SELECT COUNT(*) FROM SensorDb.dbo.AuditOutbox WHERE PublishedAt IS NULL') -eq '0' } 'sensor outbox publication'
$pending = Get-TestQueue
if ($pending -lt 12) { throw 'Events were not retained with consumer stopped.' }
# Recreate the process/container with the same named volume, like replacement of a StatefulSet pod.
& docker @compose up -d --no-deps --force-recreate --wait --wait-timeout 120 rabbitmq
if ($LASTEXITCODE -ne 0) { throw 'RabbitMQ did not recover.' }
if ((Get-TestQueue) -ne $pending) { throw 'Persistent messages were lost on broker replacement.' }
& docker @compose up -d --no-deps --no-build --scale audit-service=2 --wait --wait-timeout 180 audit-service
if ($LASTEXITCODE -ne 0) { throw 'Two Audit instances did not start.' }
$expected = [int](Invoke-TestSql 'SELECT (SELECT COUNT(*) FROM IdentityDb.dbo.AuditOutbox) + (SELECT COUNT(*) FROM SensorDb.dbo.AuditOutbox)')
Wait-TestCondition { [int](Invoke-TestSql 'SELECT COUNT(*) FROM AuditDb.dbo.AuditLogs') -eq $expected } 'recovery into AuditDb'
Wait-TestCondition { (Get-TestQueue) -eq 0 } 'acknowledgment of recovered events'
$sensorAudit = Invoke-TestSql "SELECT COUNT(*) FROM AuditDb.dbo.AuditLogs WHERE Resource = 'Sensor' AND ResourceId = '$($sensor.id)' AND Action = 'Update'"
if ($sensorAudit -ne '1') { throw 'Recovered sensor update audit event missing.' }
$from = [Uri]::EscapeDataString([DateTimeOffset]::UtcNow.AddMinutes(-10).ToString('o'))
$to = [Uri]::EscapeDataString([DateTimeOffset]::UtcNow.AddMinutes(1).ToString('o'))
$auditRows = Invoke-RestMethod "http://127.0.0.1:18090/api/audit?userId=$($login.user.id)&action=Update&resource=Sensor&from=$from&to=$to" -Headers $sensorAuth
if (@($auditRows).Count -ne 1 -or $auditRows[0].resourceId -ne $sensor.id) { throw 'Audit filters did not return the recovered sensor update.' }
$auditDetail = Invoke-RestMethod "http://127.0.0.1:18090/api/audit/$($auditRows[0].id)" -Headers $sensorAuth
if (-not $auditDetail.userName -or -not $auditDetail.description -or -not $auditDetail.timestamp) { throw 'Audit detail metadata missing.' }

# Republish the same EventIds while two real SQL-backed consumers compete.
$null = Invoke-TestSql 'UPDATE IdentityDb.dbo.AuditOutbox SET PublishedAt = NULL'
Wait-TestCondition { (Invoke-TestSql 'SELECT COUNT(*) FROM IdentityDb.dbo.AuditOutbox WHERE PublishedAt IS NULL') -eq '0' } 'duplicate publication'
Wait-TestCondition { (Get-TestQueue) -eq 0 } 'duplicate acknowledgment'
if ([int](Invoke-TestSql 'SELECT COUNT(*) FROM AuditDb.dbo.AuditLogs') -ne $expected) { throw 'Duplicate EventId created additional audit rows.' }
if ((Get-TestQueue 'climate.audit.events.dlq') -ne 0) { throw 'Valid replay events reached DLQ.' }

# Broker outage: business writes remain committed to the outbox and recover automatically.
& docker @compose stop rabbitmq
if ($LASTEXITCODE -ne 0) { throw 'Cannot stop broker.' }
$null = Invoke-RestMethod http://127.0.0.1:18090/api/auth/login -Method Post -ContentType application/json -Body (@{login='admin';password=$env:ADMIN_SEED_PASSWORD}|ConvertTo-Json)
if ([int](Invoke-TestSql 'SELECT COUNT(*) FROM IdentityDb.dbo.AuditOutbox WHERE PublishedAt IS NULL') -lt 1) { throw 'Missing pending event during broker outage.' }
& docker @compose up -d --no-deps --wait --wait-timeout 120 rabbitmq
if ($LASTEXITCODE -ne 0) { throw 'Broker restart failed.' }
Wait-TestCondition { [int](Invoke-TestSql 'SELECT COUNT(*) FROM AuditDb.dbo.AuditLogs') -eq ($expected + 1) } 'automatic publisher/consumer reconnection'

# Readiness reflects SQL outages; liveness remains independent of dependencies.
& docker @compose stop sqlserver
if ($LASTEXITCODE -ne 0) { throw 'Cannot stop SQL.' }
& docker @compose exec -T identity-service dotnet /app/health/Climate.HealthProbe.dll http://localhost:8080/health/live
if ($LASTEXITCODE -ne 0) { throw 'Liveness depends on SQL.' }
& docker @compose exec -T identity-service dotnet /app/health/Climate.HealthProbe.dll http://localhost:8080/health
if ($LASTEXITCODE -eq 0) { throw 'Readiness ignored SQL outage.' }
& docker @compose restart identity-service
if ($LASTEXITCODE -ne 0) { throw 'Cannot restart Identity before SQL.' }
Start-Sleep -Seconds 5
& docker @compose up -d --no-deps --wait --wait-timeout 120 sqlserver
if ($LASTEXITCODE -ne 0) { throw 'SQL restart failed.' }
Wait-TestCondition {
    & docker @compose exec -T identity-service dotnet /app/health/Climate.HealthProbe.dll
    return $LASTEXITCODE -eq 0
} 'service startup with late SQL dependency'
Write-Output "PASS: $pending messages survived broker replacement; two consumers recovered $expected unique events; duplicate replay, broker outage, late SQL startup and health probes verified."
