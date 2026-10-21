param([string]$EnvironmentFile='', [string]$GatewayUrl='http://localhost:8080')
$ErrorActionPreference='Stop'
$backendPath=Split-Path $PSScriptRoot -Parent
if(-not $EnvironmentFile){$EnvironmentFile=Join-Path $backendPath '.env.phase3.local'}
$settings=@{}
foreach($line in Get-Content -LiteralPath $EnvironmentFile){
    if($line -match '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$'){$settings[$matches[1]]=$matches[2]}
}
$headers=@{}
function Request([string]$route,[string]$method='GET',$body=$null){
    $parameters=@{Uri=($GatewayUrl.TrimEnd('/')+$route);Method=$method;Headers=$headers;TimeoutSec=60}
    if($null -ne $body){
        $parameters.ContentType='application/json; charset=utf-8'
        $parameters.Body=[Text.Encoding]::UTF8.GetBytes(($body|ConvertTo-Json -Depth 10))
    }
    # Windows PowerShell 5 otherwise decodes JSON without a charset as Latin-1.
    try {
        $response=Invoke-WebRequest @parameters -UseBasicParsing
        $stream=$response.RawContentStream
        $stream.Position=0
        $reader=New-Object IO.StreamReader($stream,[Text.Encoding]::UTF8)
        try {$json=$reader.ReadToEnd()} finally {$reader.Dispose()}
        if($json){
            $decoded=$json | ConvertFrom-Json
            foreach($item in $decoded){Write-Output $item}
        }
    } catch {throw "Demo request failed: $method $route. $($_.Exception.Message)"}
}
$login=Request '/api/auth/login' 'POST' @{login=$settings['ADMIN_SEED_USERNAME'];password=$settings['AUTH_ADMIN_SEED_PASSWORD']}
$headers.Authorization='Bearer '+$login.accessToken
$fixture=Get-Content -LiteralPath (Join-Path $backendPath 'seed/phase3-demo.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$communities=@(Request '/api/communities')
$sensors=@(Request '/api/sensors')
$rules=@(Request '/api/alert-rules')
$createdCommunities=0; $createdSensors=0; $createdRules=0; $createdReadings=0
foreach($entry in $fixture.communities){
    if(-not ($communities | Where-Object name -eq $entry.name)){
        $communities+=Request '/api/communities' 'POST' $entry
        $createdCommunities++
    }
}
foreach($entry in $fixture.rules){
    if(-not ($rules | Where-Object name -eq $entry.name)){
        $rules+=Request '/api/alert-rules' 'POST' $entry
        $createdRules++
    }
}
foreach($entry in $fixture.sensors){
    if(-not ($sensors | Where-Object code -eq $entry.code)){
        $community=@($communities | Where-Object name -eq $entry.communityName)[0]
        if(-not $community.isActive){continue}
        $body=@{name=$entry.name;code=$entry.code;type=$entry.type;unit=$entry.unit;description=$entry.description;communityId=$community.id;latitude=$entry.latitude;longitude=$entry.longitude}
        $sensors+=Request '/api/sensors' 'POST' $body
        $createdSensors++
    }
}
$current=@(Request '/api/monitoring/current')
foreach($entry in $fixture.sensors){
    $sensor=@($sensors | Where-Object code -eq $entry.code)[0]
    if($sensor -and $sensor.isActive -and -not ($current | Where-Object sensorId -eq $sensor.id)){
        $null=Request '/api/monitoring/readings' 'POST' @{sensorId=$sensor.id;value=$entry.initialValue}
        $createdReadings++
    }
}
$null=Request '/api/monitoring/simulation/start' 'POST'
Write-Output "Demo ready. Created communities=$createdCommunities sensors=$createdSensors rules=$createdRules initialReadings=$createdReadings. Simulation started. Existing entries preserved."
