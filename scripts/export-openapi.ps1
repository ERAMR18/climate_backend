param(
    [string]$OutputPath = "docs/openapi/climate-api-v1.json",
    [string]$ComposeEnvFile = ".env"
)

$ErrorActionPreference = "Stop"
$serviceNames = @("identity-service", "sensor-service", "monitoring-service", "alert-service", "event-service", "audit-service")
$documents = @()

foreach ($serviceName in $serviceNames) {
    $url = "http://${serviceName}:8080/swagger/v1/swagger.json"
    $json = docker compose --env-file $ComposeEnvFile exec -T gateway curl --fail --silent $url
    if ($LASTEXITCODE -ne 0) { throw "Could not download OpenAPI from $serviceName." }
    $documents += $json | ConvertFrom-Json
}

$root = [ordered]@{
    openapi = "3.0.1"
    info = [ordered]@{
        title = "Climate Monitoring System API"
        description = "Public API exposed by Climate.Gateway. Internal service endpoints are intentionally excluded."
        version = "v1"
    }
    servers = @([ordered]@{ url = "http://localhost:8080"; description = "Local Docker Gateway" })
    paths = [ordered]@{}
    components = [ordered]@{
        schemas = [ordered]@{}
        securitySchemes = [ordered]@{
            Bearer = [ordered]@{ type = "http"; scheme = "bearer"; bearerFormat = "JWT"; description = "Paste only the accessToken returned by POST /api/auth/login. Do not include the 'Bearer ' prefix; Swagger UI adds it automatically." }
        }
    }
    tags = @(
        @{ name = "Auth" }, @{ name = "Users" }, @{ name = "Sensors" }, @{ name = "Communities" },
        @{ name = "Monitoring" }, @{ name = "Alerts" }, @{ name = "Events" }, @{ name = "Audit" }
    )
}

foreach ($document in $documents) {
    foreach ($schemaProperty in $document.components.schemas.PSObject.Properties) {
        $schemaName = $schemaProperty.Name
        $incoming = $schemaProperty.Value | ConvertTo-Json -Depth 100 -Compress
        if ($root.components.schemas.Contains($schemaName)) {
            $existing = $root.components.schemas[$schemaName] | ConvertTo-Json -Depth 100 -Compress
            if ($existing -ne $incoming) { throw "Conflicting OpenAPI schema: $schemaName" }
        } else {
            $root.components.schemas[$schemaName] = $schemaProperty.Value
        }
    }

    foreach ($pathProperty in $document.paths.PSObject.Properties) {
        if ($pathProperty.Name -like "*/internal/*") { continue }
        $publicPath = $pathProperty.Name -replace '^/api/v1', '/api'
        if ($root.paths.Contains($publicPath)) { throw "Duplicate public OpenAPI path: $publicPath" }

        foreach ($operationProperty in $pathProperty.Value.PSObject.Properties) {
            if ($operationProperty.Name -notin @("get", "post", "put", "patch", "delete")) { continue }
            $operation = $operationProperty.Value
            $operationIdPath = ($publicPath.Trim('/') -replace '[{}]', '' -replace '[^a-zA-Z0-9]+', '_')
            $operationId = "{0}_{1}_{2}" -f $operation.tags[0], $operationProperty.Name, $operationIdPath
            $operation | Add-Member -NotePropertyName operationId -NotePropertyValue $operationId -Force
            $isAnonymous = $publicPath -in @("/api/auth/login", "/api/auth/register")
            if (-not $isAnonymous) {
                $operation | Add-Member -NotePropertyName security -NotePropertyValue @(@{ Bearer = @() }) -Force
                if (-not $operation.responses.'401') {
                    $operation.responses | Add-Member -NotePropertyName '401' -NotePropertyValue @{ description = "Unauthorized" }
                }
                if (-not $operation.responses.'403') {
                    $operation.responses | Add-Member -NotePropertyName '403' -NotePropertyValue @{ description = "Forbidden" }
                }
            }
        }
        $root.paths[$publicPath] = $pathProperty.Value
    }
}

$absoluteOutput = [System.IO.Path]::GetFullPath($OutputPath)
$directory = [System.IO.Path]::GetDirectoryName($absoluteOutput)
[System.IO.Directory]::CreateDirectory($directory) | Out-Null
[System.IO.File]::WriteAllText($absoluteOutput, ($root | ConvertTo-Json -Depth 100), [System.Text.UTF8Encoding]::new($false))
Write-Output "OpenAPI exported to $absoluteOutput"
