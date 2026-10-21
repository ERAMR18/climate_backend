param([Parameter(Mandatory=$true)][string]$EnvironmentFile)
$ErrorActionPreference='Stop'
$backendPath=Split-Path $PSScriptRoot -Parent
$composePath=Join-Path $backendPath 'docker-compose.phase3.yml'
$sourceProject='climate-phase3-final-validation'
$restoreProject='climate-phase3-restored-validation'
$sourceArgs=@('compose','--project-name',$sourceProject,'--env-file',$EnvironmentFile,'-f',$composePath)
$restoreArgs=@('compose','--project-name',$restoreProject,'--env-file',$EnvironmentFile,'-f',$composePath)
$settings=@{}
foreach($line in Get-Content -LiteralPath $EnvironmentFile){if($line -match '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$'){$settings[$matches[1]]=$matches[2]}}
$snapshotDirectory=Join-Path ([IO.Path]::GetTempPath()) ('climate-phase3-recovery-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $snapshotDirectory | Out-Null
$volumes=@('sqlserver-data','rabbitmq-data','auth-postgres-data','community-postgres-data','alert-postgres-data','audit-mongo-data','report-mongo-data','report-files')
$headers=@{}
function Request([string]$route,[string]$method='GET',$body=$null){
    $parameters=@{Uri=('http://127.0.0.1:15280'+$route);Method=$method;Headers=$headers}
    if($null -ne $body){$parameters['ContentType']='application/json';$parameters['Body']=($body|ConvertTo-Json -Depth 8)}
    return Invoke-RestMethod @parameters
}
try {
    $login=Request '/api/auth/login' 'POST' @{login='admin';password=$settings['AUTH_ADMIN_SEED_PASSWORD']}
    $headers['Authorization']='Bearer '+$login.accessToken
    $community=Request '/api/communities' 'POST' @{name='Recovery community';latitude=14;longitude=-90;municipality='Guatemala';department='Guatemala';country='Guatemala'}
    $sensor=Request '/api/sensors' 'POST' @{name='Recovery sensor';code='RECOVERY-TEMP';type='Temperature';unit='C';communityId=$community.id;latitude=14;longitude=-90}
    $rule=Request '/api/alert-rules' 'POST' @{name='Recovery hot weather';sensorType='Temperature';maximumValue=30;alertLevel='Red';riskType='ForestFire';message='Recovery threshold';isActive=$true}
    $reading=Request '/api/monitoring/readings' 'POST' @{sensorId=$sensor.id;value=45}
    for($attempt=0;$attempt -lt 40;$attempt++){
        $events=@(Request ('/api/events?sensorId='+$sensor.id))
        $audit=@(Request '/api/audit')
        if($events.Count -gt 0 -and $audit.Count -gt 0){break}
        Start-Sleep -Seconds 1
    }
    if($events.Count -eq 0 -or $audit.Count -eq 0){throw 'Recovery fixture did not persist events/audit.'}
    $report=Request '/api/reports/generate' 'POST' @{type='COMMUNITIES'}
    for($attempt=0;$attempt -lt 60;$attempt++){
        $metadata=Request ('/api/reports/'+$report.id)
        if($metadata.status -in @('COMPLETED','FAILED')){break}
        Start-Sleep -Seconds 1
    }
    if($metadata.status -ne 'COMPLETED'){throw 'Recovery fixture PDF failed.'}
    $before=Request '/api/reports'
    & docker @sourceArgs stop
    if($LASTEXITCODE -ne 0){throw 'Source stack must stop cleanly before taking offline snapshots.'}
    $helper=Join-Path $PSScriptRoot 'phase3_volume_snapshot.py'
    $backupArguments=@('run','--rm','--mount',('type=bind,source='+$snapshotDirectory+',target=/backup'),'--mount',('type=bind,source='+$helper+',target=/snapshot.py,readonly'))
    foreach($volume in $volumes){$backupArguments+=@('--mount',('type=volume,source='+$sourceProject+'_'+$volume+',target=/volumes/'+$volume+',readonly'))}
    & docker @backupArguments python:3.13-slim python /snapshot.py backup @volumes
    if($LASTEXITCODE -ne 0){throw 'Offline snapshot failed.'}
    & docker @restoreArgs down --volumes
    if($LASTEXITCODE -ne 0){throw 'Cannot reset the isolated restore project.'}
    foreach($volume in $volumes){
        & docker volume create --label ('com.docker.compose.project='+$restoreProject) --label ('com.docker.compose.volume='+$volume) ($restoreProject+'_'+$volume) | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Cannot create isolated restore volume.'}
    }
    $restoreArguments=@('run','--rm','--mount',('type=bind,source='+$snapshotDirectory+',target=/backup,readonly'),'--mount',('type=bind,source='+$helper+',target=/snapshot.py,readonly'))
    foreach($volume in $volumes){$restoreArguments+=@('--mount',('type=volume,source='+$restoreProject+'_'+$volume+',target=/volumes/'+$volume))}
    & docker @restoreArguments python:3.13-slim python /snapshot.py restore @volumes
    if($LASTEXITCODE -ne 0){throw 'Snapshot verification/restoration failed.'}
    & docker @restoreArgs up -d --no-build --wait --wait-timeout 240
    if($LASTEXITCODE -ne 0){throw 'Restored stack did not become healthy.'}
    $login=Request '/api/auth/login' 'POST' @{login='admin';password=$settings['AUTH_ADMIN_SEED_PASSWORD']}
    $headers['Authorization']='Bearer '+$login.accessToken
    if((Request ('/api/communities/'+$community.id)).name -ne 'Recovery community'){throw 'Community was not restored.'}
    if((Request ('/api/sensors/'+$sensor.id)).communityId -ne $community.id){throw 'Sensor reference was not restored.'}
    if((Request ('/api/alert-rules/'+$rule.id)).maximumValue -ne 30){throw 'Alert rule was not restored.'}
    if(@(Request ('/api/alerts?sensorId='+$sensor.id)).Count -ne 1){throw 'Alert was not restored.'}
    if(@(Request ('/api/events?sensorId='+$sensor.id)).Count -ne 1){throw 'Event was not restored.'}
    if((Request ('/api/monitoring/readings?sensorId='+$sensor.id)).items.Count -ne 1){throw 'Reading was not restored.'}
    if(@(Request '/api/audit').Count -lt $audit.Count){throw 'Audit history was not restored.'}
    if((Request '/api/reports').total -ne $before.total){throw 'Report history was not restored.'}
    $restoredMetadata=Request ('/api/reports/'+$report.id)
    $restoredPdf=Join-Path $snapshotDirectory 'restored.pdf'
    Invoke-WebRequest -UseBasicParsing -Uri ('http://127.0.0.1:15280/api/reports/'+$report.id+'/download') -Headers $headers -OutFile $restoredPdf
    if((Get-FileHash -LiteralPath $restoredPdf -Algorithm SHA256).Hash.ToLowerInvariant() -ne $metadata.sha256){throw 'Restored PDF digest mismatch.'}
    if($restoredMetadata.sha256 -ne $metadata.sha256){throw 'Restored metadata digest mismatch.'}
    $evidence=@{passed=$true;volumeCount=$volumes.Count;volumes=$volumes;restoredInSeparateProject=$true;checks=@('authLogin','community','sensorReference','alertRule','alert','reading','event','auditHistory','reportHistory','pdfSha256');timestamp=[DateTime]::UtcNow.ToString('o')}
    $evidencePath=Join-Path $backendPath 'docs/validation/phase3-recovery.json'
    [IO.File]::WriteAllText($evidencePath,($evidence|ConvertTo-Json -Depth 5),(New-Object Text.UTF8Encoding($false)))
    Write-Output 'PASS recovery: eight offline volume snapshots verified and restored into a separate project; Auth, Community, Sensor, Monitoring, Alert, Event, Audit and PDF history/hash preserved.'
} finally {
    & docker @restoreArgs down --volumes
    $resolvedSnapshot=[IO.Path]::GetFullPath($snapshotDirectory)
    $temporaryRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if($resolvedSnapshot.StartsWith($temporaryRoot,[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolvedSnapshot).StartsWith('climate-phase3-recovery-')){
        Remove-Item -LiteralPath $resolvedSnapshot -Recurse -Force
    }
}
