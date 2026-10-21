param([switch]$NoBuild, [switch]$Recreate)
$ErrorActionPreference='Stop'
$backendPath=Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Initialize-Phase3.ps1')
if(-not (Test-Path -LiteralPath (Join-Path $backendPath '.env.phase3.local'))){throw 'Local environment was not created.'}
Push-Location $backendPath
try {
    $arguments=@('compose','--project-name','climate-phase3-local','--env-file','.env.phase3.local','-f','docker-compose.yml')
    & docker @arguments config --quiet
    if($LASTEXITCODE -ne 0){throw 'Invalid local configuration.'}
    $buildArgument=if($NoBuild){'--no-build'}else{'--build'}
    [string[]]$extra=@()
    if($Recreate){$extra+= '--force-recreate'}
    & docker @arguments up -d $buildArgument @extra --wait --wait-timeout 240
    if($LASTEXITCODE -ne 0){throw 'Local stack did not become healthy; inspect docker compose logs.'}
    $demoSetting=Get-Content -LiteralPath '.env.phase3.local' | Where-Object {$_ -match '^PHASE3_DEMO_ENABLED='} | Select-Object -Last 1
    if(-not $demoSetting -or $demoSetting -notmatch '(?i)^PHASE3_DEMO_ENABLED=false$'){
        & (Join-Path $PSScriptRoot 'Seed-Phase3Demo.ps1')
    }
    Write-Output 'Phase 3 ready: http://localhost:4200 ; Gateway: http://localhost:8080 . Administrator credentials are in .env.phase3.local.'
} finally {Pop-Location}
