param([int]$Port = 5679)
$ErrorActionPreference = 'Stop'
$backendRoot = Split-Path $PSScriptRoot -Parent
$testName = 'climate-audit-test-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$names = @('RABBITMQ_DEFAULT_USER', 'RABBITMQ_DEFAULT_PASS', 'RABBITMQ_HOST', 'RABBITMQ_PORT', 'RABBITMQ_USER', 'RABBITMQ_PASSWORD', 'CLIMATE_RABBITMQ_INTEGRATION')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$created = $false
try {
    $env:RABBITMQ_DEFAULT_USER = 'climate-test'
    $env:RABBITMQ_DEFAULT_PASS = [Guid]::NewGuid().ToString('N')
    $env:RABBITMQ_HOST = '127.0.0.1'
    $env:RABBITMQ_PORT = $Port.ToString()
    $env:RABBITMQ_USER = $env:RABBITMQ_DEFAULT_USER
    $env:RABBITMQ_PASSWORD = $env:RABBITMQ_DEFAULT_PASS
    $env:CLIMATE_RABBITMQ_INTEGRATION = '1'
    docker run -d --name $testName --user rabbitmq -v "${testName}:/var/lib/rabbitmq" `
        -p "127.0.0.1:${Port}:5672" -e RABBITMQ_DEFAULT_USER -e RABBITMQ_DEFAULT_PASS `
        -e 'RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS=+S 2:2' -e 'RABBITMQ_CTL_ERL_ARGS=+S 1:1' rabbitmq:4.1-management
    if ($LASTEXITCODE -ne 0) { throw 'Could not create isolated RabbitMQ.' }
    $created = $true
    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        docker exec $testName rabbitmq-diagnostics -q ping
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { docker logs --tail 30 $testName; throw 'RabbitMQ did not become ready.' }
    dotnet test (Join-Path $backendRoot 'ClimateMonitoringSystem.sln') --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }
}
finally {
    if ($created) { docker stop $testName; docker rm -v $testName }
    # Only the uniquely named volume allocated by this invocation is removed.
    docker volume rm $testName
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
