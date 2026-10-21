param([string]$OutputFile='')
$ErrorActionPreference='Stop'
$backendPath=Split-Path $PSScriptRoot -Parent
if(-not $OutputFile){$OutputFile=Join-Path $backendPath '.env.phase3.local'}
if(Test-Path -LiteralPath $OutputFile){Write-Output 'Phase 3 local environment already exists; credentials were preserved.'; return}
function NewSecret {
    $bytes=New-Object byte[] 32
    $random=[Security.Cryptography.RandomNumberGenerator]::Create()
    try{$random.GetBytes($bytes)}finally{$random.Dispose()}
    return 'Aa1!'+[BitConverter]::ToString($bytes).Replace('-','').ToLowerInvariant()
}
$settings=@{}
foreach($line in Get-Content (Join-Path $backendPath '.env.example')){
    if($line -match '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$' -and -not $matches[1].Contains('__')){$settings[$matches[1]]=$matches[2]}
}
foreach($name in @('SQLSERVER_SA_PASSWORD','JWT_SIGNING_KEY','INTERNAL_API_KEY','AUTH_POSTGRES_PASSWORD','COMMUNITY_POSTGRES_PASSWORD','ALERT_POSTGRES_PASSWORD','AUDIT_MONGO_PASSWORD','REPORT_MONGO_PASSWORD','RABBITMQ_PASSWORD','AUTH_ADMIN_SEED_PASSWORD')){$settings[$name]=NewSecret}
$settings['RABBITMQ_USER']='climate-local';$settings['RABBITMQ_USER_URI']='climate-local'
foreach($name in @('AUTH_POSTGRES_PASSWORD','AUDIT_MONGO_PASSWORD','REPORT_MONGO_PASSWORD','RABBITMQ_PASSWORD')){$settings[$name+'_URI']=[Uri]::EscapeDataString($settings[$name])}
$settings.Remove('ADMIN_SEED_PASSWORD')
$settings['ADMIN_SEED_USERNAME']='admin';$settings['ADMIN_SEED_EMAIL']='admin@example.com'
$settings['DEMO_SEED_ENABLED']='false'
$settings['PHASE3_DEMO_ENABLED']='true'
$lines=@($settings.GetEnumerator()|Sort-Object Key|ForEach-Object{$_.Key+'='+$_.Value})
[IO.File]::WriteAllLines([IO.Path]::GetFullPath($OutputFile),$lines,(New-Object Text.UTF8Encoding($false)))
Write-Output 'Created a private local environment. Administrator: admin. Open .env.phase3.local and use AUTH_ADMIN_SEED_PASSWORD to sign in. Do not publish this file.'
