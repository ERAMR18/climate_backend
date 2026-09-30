param([int]$SqlPort = 14339, [int]$GatewayPort = 18080, [int]$ServicePortBase = 15100)
$ErrorActionPreference = 'Stop'
$backendRoot = Split-Path $PSScriptRoot -Parent
$testName = 'climate-functional-test-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$logDirectory = Join-Path ([IO.Path]::GetTempPath()) $testName
[IO.Directory]::CreateDirectory($logDirectory) | Out-Null
$processes = @()
$created = $false
$socket = $null
$previous = @{}
function Set-TestEnv([string]$name, [string]$value) {
    if (-not $previous.ContainsKey($name)) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
    [Environment]::SetEnvironmentVariable($name, $value, 'Process')
}
function Assert-True($condition, [string]$message) { if (-not $condition) { throw $message } }
function Request([string]$method, [string]$path, $body = $null, $headers = $script:auth) {
    $parameters = @{ Uri = "http://127.0.0.1:$GatewayPort/api/$path"; Method = $method; Headers = $headers; ContentType = 'application/json'; UseBasicParsing = $true }
    if ($null -ne $body) { $parameters.Body = $body | ConvertTo-Json -Depth 10 -Compress }
    $response = Invoke-RestMethod @parameters
    if ($response -is [Array]) { foreach ($item in $response) { $item } } else { $response }
}
function Expect-Status([int]$status, [scriptblock]$operation) {
    try { & $operation | Out-Null; throw "Expected HTTP $status" }
    catch { if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne $status) { throw } }
}
try {
    Set-TestEnv 'MSSQL_SA_PASSWORD' ('Test-Only-' + [Guid]::NewGuid().ToString('N') + '!')
    docker run -d --name $testName -p "127.0.0.1:${SqlPort}:1433" -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD -e MSSQL_MEMORY_LIMIT_MB=2048 mcr.microsoft.com/mssql/server:2022-latest | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not start isolated SQL Server.' }
    $created = $true
    $ready = $false
    for ($attempt = 0; $attempt -lt 45; $attempt++) {
        $logs = docker logs $testName 2>&1 | Out-String
        if ($logs.Contains('SQL Server is now ready for client connections')) { $ready = $true; break }
        Start-Sleep -Seconds 2
    }
    Assert-True $ready 'SQL Server startup timed out.'
    Write-Output 'Isolated SQL Server ready; starting APIs with temporary databases.'
    Set-TestEnv 'OpenApi__ExportOnly' 'false'
    Set-TestEnv 'ASPNETCORE_ENVIRONMENT' 'Development'
    Set-TestEnv 'Jwt__SigningKey' ([Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N'))
    Set-TestEnv 'InternalApi__ApiKey' ([Guid]::NewGuid().ToString('N'))
    Set-TestEnv 'AdminSeed__Username' 'phase2-admin'
    Set-TestEnv 'AdminSeed__Email' 'phase2-admin@example.test'
    Set-TestEnv 'AdminSeed__Password' ('Admin-Test-' + [Guid]::NewGuid().ToString('N') + '!')
    Set-TestEnv 'DemoSeed__Enabled' 'false'
    Set-TestEnv 'Simulation__Enabled' 'false'
    Set-TestEnv 'Simulation__IntervalSeconds' '1'
    # Broker deliberately unavailable: HTTP business writes must still commit to SQL/outbox.
    Set-TestEnv 'RABBITMQ_HOST' '127.0.0.1'
    Set-TestEnv 'RABBITMQ_PORT' '5699'
    Set-TestEnv 'RABBITMQ_USER' 'functional-test'
    Set-TestEnv 'RABBITMQ_PASSWORD' ([Guid]::NewGuid().ToString('N'))
    foreach ($name in @('IdentityDb','SensorDb','MonitoringDb','AlertDb','EventDb','AuditDb')) {
        Set-TestEnv "ConnectionStrings__$name" "Server=127.0.0.1,$SqlPort;Database=$name;User Id=sa;Password=$env:MSSQL_SA_PASSWORD;TrustServerCertificate=True;Encrypt=True"
    }
    foreach ($entry in @(@('SensorService',2),@('AlertService',4),@('EventService',5),@('RealtimeService',3))) {
        Set-TestEnv ($entry[0]+'__BaseUrl') ('http://127.0.0.1:'+($ServicePortBase+$entry[1])+'/')
        Set-TestEnv ($entry[0]+'__ApiKey') $env:InternalApi__ApiKey
    }
    $entries = @(@('Identity','Identity','identity'),@('Sensor','Sensors','sensors'),@('Monitoring','Monitoring','monitoring'),@('Alert','Alerts','alerts'),@('Event','Events','events'),@('Audit','Audit','audit'))
    for ($i = 0; $i -lt $entries.Count; $i++) {
        $e = $entries[$i]; $port = $ServicePortBase + $i + 1
        Set-TestEnv 'ASPNETCORE_URLS' "http://127.0.0.1:$port"
        Set-TestEnv ('ReverseProxy__Clusters__'+$e[2]+'__Destinations__primary__Address') "http://127.0.0.1:$port/"
        Set-TestEnv ('DownstreamHealthEndpoints__'+$i) "http://127.0.0.1:$port/health"
        $dir = Join-Path $backendRoot ('src/Services/'+$e[0]+'Service/Climate.'+$e[1]+'.Api')
        $dll = Join-Path $dir ('bin/Debug/net10.0/Climate.'+$e[1]+'.Api.dll')
        $processes += Start-Process dotnet -ArgumentList ('"'+$dll+'"') -WorkingDirectory $dir -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logDirectory ($e[0]+'.log')) -RedirectStandardError (Join-Path $logDirectory ($e[0]+'.err'))
    }
    Set-TestEnv 'ASPNETCORE_URLS' "http://127.0.0.1:$GatewayPort"
    Set-TestEnv 'ASPNETCORE_ENVIRONMENT' 'Production'
    $dir = Join-Path $backendRoot 'src/Gateway/Climate.Gateway'
    $dll = Join-Path $dir 'bin/Debug/net10.0/Climate.Gateway.dll'
    $processes += Start-Process dotnet -ArgumentList ('"'+$dll+'"') -WorkingDirectory (Split-Path $dll -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logDirectory 'Gateway.log') -RedirectStandardError (Join-Path $logDirectory 'Gateway.err')
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        foreach ($process in $processes) { if ($process.HasExited) { throw "An API exited during startup; inspect $logDirectory" } }
        try { Invoke-RestMethod "http://127.0.0.1:$GatewayPort/health" -TimeoutSec 5 | Out-Null; $ready = $true; break } catch { Start-Sleep -Seconds 2 }
    }
    Assert-True $ready "API startup timed out; inspect $logDirectory"
    $login = Request POST 'auth/login' @{ login = 'phase2-admin'; password = $env:AdminSeed__Password } @{}
    $script:auth = @{ Authorization = 'Bearer '+$login.accessToken }
    Assert-True ($null -ne $login.user.lastLoginAt) 'Successful login did not record last access.'
    Expect-Status 401 { Request GET 'users/me' $null @{} }
    Expect-Status 401 { Request POST 'auth/login' @{login='phase2-admin';password='Wrong-Password-123!'} @{} }
    function Base64Url([byte[]]$bytes) { [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+','-').Replace('/','_') }
    $jwtParts = $login.accessToken.Split('.')
    $encoded = $jwtParts[1].Replace('-','+').Replace('_','/')
    $encoded = $encoded.PadRight($encoded.Length + ((4 - $encoded.Length % 4) % 4), '=')
    $claims = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($encoded)) | ConvertFrom-Json
    $claims.exp = [DateTimeOffset]::UtcNow.AddMinutes(-10).ToUnixTimeSeconds()
    $claims.nbf = [DateTimeOffset]::UtcNow.AddMinutes(-20).ToUnixTimeSeconds()
    $unsigned = $jwtParts[0] + '.' + (Base64Url ([Text.Encoding]::UTF8.GetBytes(($claims | ConvertTo-Json -Compress))))
    $hmac = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($env:Jwt__SigningKey))
    try { $expiredToken = $unsigned + '.' + (Base64Url ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($unsigned)))) } finally { $hmac.Dispose() }
    Expect-Status 401 { Request GET 'users/me' $null @{Authorization='Bearer '+$expiredToken} }
    $negotiation = Invoke-RestMethod -Method POST -Uri "http://127.0.0.1:$GatewayPort/hubs/monitoring/negotiate?negotiateVersion=1" -Headers $script:auth
    $socket = New-Object System.Net.WebSockets.ClientWebSocket
    $wsUri = 'ws://127.0.0.1:'+$GatewayPort+'/hubs/monitoring?id='+[Uri]::EscapeDataString($negotiation.connectionToken)+'&access_token='+[Uri]::EscapeDataString($login.accessToken)
    $socket.ConnectAsync([Uri]$wsUri, [Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
    $handshake = [Text.Encoding]::UTF8.GetBytes('{"protocol":"json","version":1}'+[char]30)
    $socket.SendAsync([ArraySegment[byte]]::new($handshake), [Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
    $community = Request POST 'communities' @{ name='Functional community'; latitude=14.6; longitude=-90.5; municipality='Town'; department='Region'; country='Guatemala' }
    $sensor = Request POST 'sensors' @{ name='Smoke test'; code='SMOKE-TEST'; type='Smoke'; unit='%'; communityId=$community.id; latitude=14.6; longitude=-90.5; location='Station' }
    $community.description = 'Updated community'
    $editedCommunity = Request PUT "communities/$($community.id)" $community
    Assert-True ($editedCommunity.description -eq 'Updated community') 'Community edit failed.'
    Request PATCH "communities/$($community.id)/deactivate" | Out-Null
    Assert-True (@(Request GET 'communities?isActive=false&municipality=Town&department=Region&search=Functional').Count -eq 1) 'Community filters/deactivation failed.'
    Request PATCH "communities/$($community.id)/activate" | Out-Null
    $filteredCommunities = @(Request GET 'communities?isActive=true&municipality=Town')
    Assert-True ($filteredCommunities[0].sensorCount -eq 1) 'Community sensor count missing.'
    Assert-True (@(Request GET "sensors?communityId=$($community.id)&type=Smoke&isActive=true&code=SMOKE-TEST").Count -eq 1) 'Sensor filters failed.'
    $ruleBody = @{ name='Smoke threshold'; sensorType='Smoke'; minimumValue=$null; maximumValue=50; alertLevel='Red'; riskType='ForestFire'; message='Smoke detected'; isActive=$true }
    $rule = Request POST 'alert-rules' $ruleBody
    $ruleBody.message = 'Smoke detected by configured rule'
    Request PUT "alert-rules/$($rule.id)" $ruleBody | Out-Null
    Request PATCH "alert-rules/$($rule.id)/activate" | Out-Null
    Request PUT "monitoring/simulation/values/$($sensor.id)" @{ value=75 } | Out-Null
    $override = Request GET "monitoring/simulation/values/$($sensor.id)"
    Assert-True ($override.value -eq 75) 'Simulation override not persisted.'
    Request POST 'monitoring/readings' @{ sensorId=$sensor.id; value=75 } | Out-Null
    $alerts = @(Request GET "alerts?sensorId=$($sensor.id)&status=Active")
    Assert-True ($alerts.Count -eq 1 -and $alerts[0].ruleId -eq $rule.id) 'Rule did not produce a traceable alert.'
    $alert = $alerts[0]
    $activeSummary = Request GET "dashboard/summary?communityId=$($community.id)"
    Assert-True ($activeSummary.activeAlertCount -eq 1 -and $activeSummary.byAlertLevel.Red -eq 1) 'Dashboard active alerts/distribution failed.'
    Expect-Status 409 { Request PATCH "alerts/$($alert.id)/close" }
    Request PATCH "alerts/$($alert.id)/attend" | Out-Null
    Expect-Status 409 { Request PATCH "alerts/$($alert.id)/attend" }
    Request PATCH "alerts/$($alert.id)/close" | Out-Null
    Expect-Status 409 { Request PATCH "alerts/$($alert.id)/resolve" }
    $closed = Request GET "alerts/$($alert.id)"
    Assert-True ($closed.status -eq 'Closed' -and $closed.attendedByUserId -eq $login.user.id -and $closed.closedByUserId -eq $login.user.id) 'Workflow actor was not recorded.'
    $stats = Request GET "events/statistics?communityId=$($community.id)"
    Assert-True ($stats.total -eq 1 -and $stats.byStatus.Closed -eq 1) 'Event statistics mismatch.'
    $events = @(Request GET "events?communityId=$($community.id)")
    Assert-True ($events[0].value -eq 75 -and $events[0].responsibleUserId -eq $login.user.id) 'Event workflow snapshot missing.'
    $dateFrom = [Uri]::EscapeDataString([DateTimeOffset]::UtcNow.AddHours(-1).ToString('o'))
    $dateTo = [Uri]::EscapeDataString([DateTimeOffset]::UtcNow.AddHours(1).ToString('o'))
    $riskFilters = "communityId=$($community.id)&sensorId=$($sensor.id)&riskType=ForestFire&alertLevel=Red&from=$dateFrom&to=$dateTo"
    Assert-True (@(Request GET "alerts?$riskFilters&status=Closed").Count -eq 1) 'Combined alert filters failed.'
    Assert-True (@(Request GET "events?$riskFilters").Count -eq 1) 'Combined event filters failed.'
    Assert-True (@(Request GET "events?riskType=Frost&communityId=$($community.id)").Count -eq 0) 'Event filter returned another phenomenon.'
    Request PATCH "alert-rules/$($rule.id)/deactivate" | Out-Null
    Request POST 'monitoring/readings' @{ sensorId=$sensor.id; value=99 } | Out-Null
    Assert-True (@(Request GET "alerts?sensorId=$($sensor.id)&status=Active").Count -eq 0) 'Inactive rule produced alert.'
    $readings = Request GET "monitoring/readings?communityId=$($community.id)&sensorId=$($sensor.id)&pageSize=1"
    Assert-True ($readings.total -eq 2 -and $readings.items.Count -eq 1 -and $readings.items[0].sensorWasActive) 'Bulk readings/pagination failed.'
    Assert-True (@(Request GET "monitoring/sensors/$($sensor.id)/history?communityId=$($community.id)&from=$dateFrom&to=$dateTo").Count -eq 2) 'Historical reading date/community filters failed.'
    $dashboard = Request GET "dashboard/summary?communityId=$($community.id)"
    Assert-True ($dashboard.communityCount -eq 1 -and $dashboard.activeSensorCount -eq 1 -and $dashboard.events.total -eq 1 -and $dashboard.evolution.Count -ge 1) 'Dashboard aggregation failed.'
    Request POST 'monitoring/simulation/start' | Out-Null
    Start-Sleep -Seconds 3
    Request POST 'monitoring/simulation/stop' | Out-Null
    $latest = Request GET "monitoring/sensors/$($sensor.id)/latest"
    Assert-True ($latest.value -eq 75) 'Simulator ignored the saved override.'
    Request DELETE "monitoring/simulation/values/$($sensor.id)" | Out-Null
    Request PATCH "sensors/$($sensor.id)/deactivate" | Out-Null
    $inactiveSummary = Request GET "dashboard/summary?communityId=$($community.id)"
    Assert-True ($inactiveSummary.activeSensorCount -eq 0 -and $inactiveSummary.inactiveSensorCount -eq 1) 'Dashboard sensor status counts failed.'
    Expect-Status 404 { Request POST 'monitoring/readings' @{ sensorId=$sensor.id; value=90 } }
    Expect-Status 404 { Request PUT "monitoring/simulation/values/$($sensor.id)" @{ value=80 } }
    $viewerPassword = 'Viewer-Test-' + [Guid]::NewGuid().ToString('N') + '!'
    Request POST 'users' @{ username='phase2-viewer'; email='viewer@example.test'; password=$viewerPassword; role='Viewer' } | Out-Null
    $viewer = Request POST 'auth/login' @{ login='phase2-viewer'; password=$viewerPassword } @{}
    $viewerAuth = @{ Authorization = 'Bearer '+$viewer.accessToken }
    $userDetails = Request GET "users/$($viewer.user.id)"
    Assert-True ($userDetails.createdAt -and $userDetails.lastLoginAt -and $userDetails.role -eq 'Viewer') 'User metadata missing.'
    $operator = Request PUT "users/$($viewer.user.id)" @{username='phase2-operator';email='operator@example.test';role='Operator'}
    Assert-True ($operator.role -eq 'Operator') 'User edit/role assignment failed.'
    Assert-True (@(Request GET 'users?search=phase2-operator&role=Operator&isActive=true').Count -eq 1) 'User filters failed.'
    Request PATCH "users/$($viewer.user.id)/status" @{isActive=$false} | Out-Null
    Expect-Status 403 { Request POST 'auth/login' @{login='phase2-operator';password=$viewerPassword} @{} }
    Request PATCH "users/$($viewer.user.id)/status" @{isActive=$true} | Out-Null
    Expect-Status 403 { Request POST 'alert-rules' $ruleBody $viewerAuth }
    Expect-Status 403 { Request PATCH "alerts/$($alert.id)/attend" $null $viewerAuth }
    Expect-Status 403 { Request PUT "monitoring/simulation/values/$($sensor.id)" @{value=75} $viewerAuth }
    Request POST 'monitoring/system/reset' | Out-Null
    Request POST 'auth/logout' | Out-Null
    $seen = @{}
    $buffer = New-Object byte[] 65536
    $pending = ''
    $deadline = New-Object Threading.CancellationTokenSource
    $deadline.CancelAfter(10000)
    try {
        while ($seen.Count -lt 6) {
            $received = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $deadline.Token).GetAwaiter().GetResult()
            $pending += [Text.Encoding]::UTF8.GetString($buffer, 0, $received.Count)
            while ($pending.Contains([string][char]30)) {
                $index = $pending.IndexOf([char]30)
                $frame = $pending.Substring(0, $index)
                $pending = $pending.Substring($index + 1)
                if ($frame) { $message = $frame | ConvertFrom-Json; if ($message.target) { $seen[$message.target] = $true } }
            }
        }
    } finally { $deadline.Dispose() }
    foreach ($eventName in @('SensorReadingUpdated','AlertGenerated','AlertAttended','AlertClosed','SensorStatusChanged','SystemReset')) {
        Assert-True $seen.ContainsKey($eventName) "Missing SignalR event: $eventName"
    }
    Set-TestEnv 'SQLCMDPASSWORD' $env:MSSQL_SA_PASSWORD
    $auditCount = docker exec -e SQLCMDPASSWORD $testName /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -d AlertDb -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(DISTINCT JSON_VALUE(Payload, '$.Action')) FROM AuditOutbox WHERE PublishedAt IS NULL AND JSON_VALUE(Payload, '$.Action') IN ('CreateAlertRule','UpdateAlertRule','ActivateAlertRule','DeactivateAlertRule','AttendAlert','CloseAlert');"
    Assert-True ($LASTEXITCODE -eq 0 -and [int]($auditCount | Out-String).Trim() -eq 6) 'Rule/workflow audit actions missing from SQL outbox.'
    $hash = docker exec -e SQLCMDPASSWORD $testName /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -d IdentityDb -h -1 -W -Q "SET NOCOUNT ON; SELECT PasswordHash FROM Users WHERE Username = 'phase2-admin';"
    Assert-True ($LASTEXITCODE -eq 0 -and ($hash | Out-String).Trim().Length -gt 40 -and ($hash | Out-String).Trim() -ne $env:AdminSeed__Password) 'Password was not hashed in SQL.'
    Write-Output 'PASS: community CRUD/status/filter/count, sensor filters, user CRUD/status/roles/last login, rejected anonymous/expired JWT, hashed SQL password and logout.'
    Write-Output 'PASS: Gateway/JWT, SQL migrations, rules, evaluation/snapshots, workflow/409, actors, event statistics, bulk readings, dashboard SQL, simulation override, inactive sensor, Viewer authorization, six SignalR events, six audit actions in SQL outbox; RabbitMQ unavailable throughout.'
}
finally {
    if ($socket) { $socket.Dispose() }
    foreach ($process in $processes) { if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } }
    if ($created) { docker rm -f -v $testName | Out-Null }
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    Write-Output "Test logs: $logDirectory"
}

