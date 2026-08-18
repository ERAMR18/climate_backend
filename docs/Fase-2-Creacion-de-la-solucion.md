# Fase 2 — Creación de la solución

## 1. Objetivo

Esta fase crea el esqueleto compilable de la solución con .NET 10. No implementa aún contratos compartidos ni lógica de los microservicios. Esas tareas comienzan en la Fase 3.

El entorno utilizado dispone del SDK `10.0.300`, fijado mediante `global.json` para obtener compilaciones reproducibles.

## 2. Estructura creada

```text
ClimateMonitoringSystem.sln
global.json
Directory.Build.props
src/
├── Gateway/
│   └── Climate.Gateway/
├── BuildingBlocks/
│   ├── Climate.SharedKernel/
│   └── Climate.Contracts/
└── Services/
    ├── IdentityService/
    │   ├── Climate.Identity.Api/
    │   ├── Climate.Identity.Application/
    │   ├── Climate.Identity.Domain/
    │   └── Climate.Identity.Infrastructure/
    ├── SensorService/
    │   ├── Climate.Sensors.Api/
    │   ├── Climate.Sensors.Application/
    │   ├── Climate.Sensors.Domain/
    │   └── Climate.Sensors.Infrastructure/
    ├── MonitoringService/ (Api, Application, Domain, Infrastructure)
    ├── AlertService/      (Api, Application, Domain, Infrastructure)
    ├── EventService/      (Api, Application, Domain, Infrastructure)
    └── AuditService/      (Api, Application, Domain, Infrastructure)
tests/
├── Climate.Identity.Tests/
├── Climate.Sensors.Tests/
├── Climate.Monitoring.Tests/
├── Climate.Alerts.Tests/
├── Climate.Events.Tests/
└── Climate.Audit.Tests/
```

La solución contiene 33 proyectos: Gateway, dos Building Blocks, 24 proyectos de servicios y seis proyectos de pruebas.

## 3. Comandos de creación

Los siguientes comandos se ejecutan desde la raíz del repositorio.

### SDK y solución

```powershell
dotnet new globaljson --sdk-version 10.0.300 --roll-forward latestPatch
dotnet new sln --name ClimateMonitoringSystem --format sln
```

- `globaljson` fija el SDK y permite avanzar únicamente a revisiones compatibles.
- `sln --format sln` crea la solución clásica solicitada y compatible con Visual Studio y herramientas académicas.

### Gateway y Building Blocks

```powershell
dotnet new webapi -n Climate.Gateway `
  -o src/Gateway/Climate.Gateway `
  --framework net10.0 --use-controllers --no-https

dotnet new classlib -n Climate.SharedKernel `
  -o src/BuildingBlocks/Climate.SharedKernel --framework net10.0

dotnet new classlib -n Climate.Contracts `
  -o src/BuildingBlocks/Climate.Contracts --framework net10.0
```

- `webapi` proporciona el host ASP.NET Core del Gateway. `--no-https` evita certificados dentro del contenedor; TLS terminará en el proxy de la VPS.
- `classlib` crea bibliotecas sin host web para elementos compartidos mínimos.

### Capas de un microservicio

Identity se creó con este patrón, repetido para Sensors, Monitoring, Alerts, Events y Audit:

```powershell
dotnet new webapi -n Climate.Identity.Api `
  -o src/Services/IdentityService/Climate.Identity.Api `
  --framework net10.0 --use-controllers --no-https

dotnet new classlib -n Climate.Identity.Application `
  -o src/Services/IdentityService/Climate.Identity.Application --framework net10.0

dotnet new classlib -n Climate.Identity.Domain `
  -o src/Services/IdentityService/Climate.Identity.Domain --framework net10.0

dotnet new classlib -n Climate.Identity.Infrastructure `
  -o src/Services/IdentityService/Climate.Identity.Infrastructure --framework net10.0

dotnet new xunit -n Climate.Identity.Tests `
  -o tests/Climate.Identity.Tests --framework net10.0
```

- `Api` será la frontera HTTP y raíz de composición.
- `Application` alojará casos de uso, puertos, DTO y validadores.
- `Domain` contendrá entidades y reglas sin dependencias de infraestructura.
- `Infrastructure` implementará persistencia EF Core y adaptadores externos.
- `Tests` alojará las pruebas unitarias del servicio.

### Incorporación a la solución

```powershell
$projects = Get-ChildItem -Recurse -Filter *.csproj |
  ForEach-Object { $_.FullName }

dotnet sln ClimateMonitoringSystem.sln add $projects
```

Este comando registra todos los proyectos creados en la solución.

## 4. Referencias entre capas

Para cada microservicio se aplicó el siguiente esquema:

```text
Api ───────────> Application ───────────> Domain
 │                    ▲
 └──> Infrastructure ─┘
          │
          └─────────────────────────────> Domain

Tests ─────────> Application + Domain
```

Ejemplo ejecutado para Identity:

```powershell
dotnet add src/Services/IdentityService/Climate.Identity.Application/Climate.Identity.Application.csproj `
  reference src/Services/IdentityService/Climate.Identity.Domain/Climate.Identity.Domain.csproj

dotnet add src/Services/IdentityService/Climate.Identity.Infrastructure/Climate.Identity.Infrastructure.csproj `
  reference `
  src/Services/IdentityService/Climate.Identity.Application/Climate.Identity.Application.csproj `
  src/Services/IdentityService/Climate.Identity.Domain/Climate.Identity.Domain.csproj

dotnet add src/Services/IdentityService/Climate.Identity.Api/Climate.Identity.Api.csproj `
  reference `
  src/Services/IdentityService/Climate.Identity.Application/Climate.Identity.Application.csproj `
  src/Services/IdentityService/Climate.Identity.Infrastructure/Climate.Identity.Infrastructure.csproj

dotnet add tests/Climate.Identity.Tests/Climate.Identity.Tests.csproj `
  reference `
  src/Services/IdentityService/Climate.Identity.Application/Climate.Identity.Application.csproj `
  src/Services/IdentityService/Climate.Identity.Domain/Climate.Identity.Domain.csproj
```

No existe ninguna referencia de proyecto entre microservicios. `Domain` no referencia `Application`, `Infrastructure` ni `Api`.

Los contratos compartidos dependen únicamente del kernel mínimo:

```powershell
dotnet add src/BuildingBlocks/Climate.Contracts/Climate.Contracts.csproj `
  reference src/BuildingBlocks/Climate.SharedKernel/Climate.SharedKernel.csproj
```

## 5. Paquetes de esta fase

La plantilla de .NET agregó `Microsoft.AspNetCore.OpenApi 10.0.8` a cada host Web API. Durante la restauración se detectó que su dependencia transitiva `Microsoft.OpenApi 2.0.0` estaba afectada por una vulnerabilidad de severidad alta. Se agregó una referencia directa compatible a la línea 2.x corregida:

```powershell
dotnet add <proyecto-api.csproj> package Microsoft.OpenApi --version 2.11.0
```

La referencia se aplicó a Gateway y a las seis API. Los paquetes funcionales de YARP, EF Core, JWT, FluentValidation, health checks y Moq se agregarán en la fase donde se utilicen, evitando dependencias sin uso y facilitando explicar su propósito.

## 6. Configuración común

`Directory.Build.props` aplica a todos los proyectos:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>
</Project>
```

Esto unifica el framework, habilita análisis de nulabilidad y hace que los warnings impidan integrar una compilación degradada.

## 7. Restauración y comprobación

```powershell
dotnet restore ClimateMonitoringSystem.sln
dotnet build ClimateMonitoringSystem.sln --no-restore
dotnet test ClimateMonitoringSystem.sln --no-build
```

- `restore` descarga y resuelve paquetes.
- `build --no-restore` confirma por separado que toda la solución compila.
- `test --no-build` ejecuta los seis proyectos xUnit usando los binarios ya compilados.

## 8. Resultado de la fase

- SDK fijado en .NET 10.0.300.
- Solución clásica creada.
- Límites de microservicios representados físicamente.
- Capas y referencias configuradas sin dependencias circulares.
- Proyectos xUnit creados.
- Configuración común de calidad habilitada.
- Dependencia vulnerable de la plantilla corregida.

No se han implementado todavía Building Blocks, entidades, DTO, persistencia, endpoints ni lógica de negocio. La siguiente etapa es la **Fase 3 — Building Blocks** y debe comenzar solamente cuando se solicite.
