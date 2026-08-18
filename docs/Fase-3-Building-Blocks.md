# Fase 3 — Building Blocks

## 1. Objetivo

Esta fase implementa los elementos comunes estrictamente necesarios para mantener respuestas consistentes y permitir comunicación explícita entre microservicios. No contiene entidades EF Core, lógica climática ni implementaciones HTTP.

La separación es:

- `Climate.SharedKernel`: primitivas técnicas pequeñas, independientes del negocio.
- `Climate.Contracts`: contratos públicos de integración entre procesos.

Los microservicios no comparten entidades, repositorios, `DbContext` ni servicios de aplicación.

## 2. Estructura de archivos

```text
src/BuildingBlocks/
├── Climate.SharedKernel/
│   ├── Pagination/
│   │   ├── PagedResult.cs
│   │   └── PaginationDefaults.cs
│   └── Results/
│       ├── ApplicationError.cs
│       ├── ErrorType.cs
│       ├── Result.cs
│       └── ResultOfT.cs
└── Climate.Contracts/
    ├── Alerts/
    │   ├── AlertGenerated.cs
    │   ├── AlertLevel.cs
    │   └── RiskType.cs
    ├── Audit/
    │   ├── AuditActions.cs
    │   └── AuditLogRequested.cs
    ├── Common/
    │   ├── CorrelationHeaders.cs
    │   └── IntegrationEvent.cs
    ├── Events/
    │   └── ClimateEventRequested.cs
    ├── Identity/
    │   └── SystemRoles.cs
    ├── Monitoring/
    │   └── SensorReadingRecorded.cs
    ├── Realtime/
    │   └── RealtimeEventNames.cs
    └── Sensors/
        ├── SensorSummary.cs
        └── SensorType.cs

tests/Climate.BuildingBlocks.Tests/
├── Contracts/
│   ├── IntegrationContractTests.cs
│   └── SystemRolesTests.cs
├── Pagination/
│   └── PagedResultTests.cs
└── Results/
    └── ResultTests.cs
```

## 3. Shared Kernel

### Resultados de aplicación

`Result` y `Result<TValue>` representan éxito o fallo esperado sin usar excepciones para el flujo normal. Un fallo contiene `ApplicationError`, compuesto por:

- `Code`: identificador estable y apto para clientes;
- `Description`: detalle legible;
- `Type`: categoría que posteriormente se traducirá a un código HTTP.

Las categorías son `Failure`, `Validation`, `NotFound`, `Conflict`, `Unauthorized` y `Forbidden`. El constructor protege las invariantes: un resultado exitoso no puede contener error y uno fallido debe contenerlo.

Ejemplo de uso futuro:

```csharp
return sensor is null
    ? Result.Failure<SensorResponse>(
        ApplicationError.NotFound("sensor.not_found", "The sensor was not found."))
    : Result.Success(sensorResponse);
```

### Paginación

`PagedResult<T>` contiene elementos, página, tamaño, total de registros, total de páginas y navegación. Valida sus argumentos para que ningún servicio produzca metadatos incoherentes.

Los valores comunes son:

```text
Página inicial: 1
Tamaño predeterminado: 20
Tamaño máximo: 100
```

Estos valores son convenciones técnicas de API, no reglas climáticas.

## 4. Contratos de integración

`IntegrationEvent` proporciona a cada mensaje:

- `EventId`, necesario para idempotencia futura;
- `OccurredAt`, siempre expresado como `DateTimeOffset` UTC por el emisor;
- `CorrelationId`, para trazabilidad distribuida.

Contratos definidos:

| Contrato | Emisor previsto | Consumidor previsto |
|---|---|---|
| `SensorSummary` | Sensor Service | Monitoring Service |
| `SensorReadingRecorded` | Monitoring Service | Alert Service y SignalR |
| `AlertGenerated` | Alert Service | Event Service y SignalR |
| `ClimateEventRequested` | Alert Service | Event Service |
| `AuditLogRequested` | Servicios administrativos | Audit Service |

Los contratos son records inmutables y sólo transportan datos. No ejecutan lógica ni dependen de ASP.NET, EF Core o serializadores concretos.

## 5. Catálogos compartidos

- `SystemRoles`: nombres canónicos `Administrator`, `Operator` y `Viewer`, con comprobación sin distinción de mayúsculas.
- `SensorType`: tipos intercambiados entre Sensors, Monitoring y Alerts.
- `AlertLevel` y `RiskType`: valores intercambiados por Alerts, Events y tiempo real.
- `AuditActions`: nombres estables de las acciones mínimas exigidas.
- `RealtimeEventNames`: nombres estables que consumirá Angular.
- `CorrelationHeaders.HeaderName`: evita repetir el literal `X-Correlation-ID`.

Estos enums pertenecen al contrato de integración. Cada dominio puede conservar tipos internos propios y mapearlos en su frontera, evitando que un contrato externo gobierne sus reglas.

## 6. Decisión arquitectónica

### Problema

Los servicios deben intercambiar datos consistentes, pero compartir modelos internos produciría acoplamiento y un monolito distribuido.

### Alternativas

1. Duplicar todos los DTO en emisor y consumidor.
2. Compartir entidades y modelos de dominio.
3. Compartir únicamente contratos de integración versionables.

### Decisión

Compartir sólo mensajes y catálogos que cruzan procesos. Mantener las entidades, validaciones y casos de uso dentro de cada servicio.

### Justificación

Reduce errores de serialización y nombres divergentes sin permitir acceso a detalles internos. Los contratos podrán versionarse o convertirse en esquemas de eventos cuando se incorpore mensajería.

## 7. Comandos ejecutados

Se agregó un proyecto dedicado de pruebas:

```powershell
dotnet new xunit -n Climate.BuildingBlocks.Tests `
  -o tests/Climate.BuildingBlocks.Tests `
  --framework net10.0 --no-restore

dotnet add tests/Climate.BuildingBlocks.Tests/Climate.BuildingBlocks.Tests.csproj `
  reference `
  src/BuildingBlocks/Climate.SharedKernel/Climate.SharedKernel.csproj `
  src/BuildingBlocks/Climate.Contracts/Climate.Contracts.csproj

dotnet sln ClimateMonitoringSystem.sln add `
  tests/Climate.BuildingBlocks.Tests/Climate.BuildingBlocks.Tests.csproj
```

No fue necesario agregar paquetes de producción. Se reutilizó xUnit, ya seleccionado para las pruebas de la solución.

## 8. Comprobación

```powershell
dotnet restore tests/Climate.BuildingBlocks.Tests/Climate.BuildingBlocks.Tests.csproj
dotnet build ClimateMonitoringSystem.sln --no-restore
dotnet test ClimateMonitoringSystem.sln --no-build --no-restore
```

Las pruebas comprueban:

- invariantes de éxito y fallo;
- acceso seguro al valor de `Result<T>`;
- cálculo y validación de paginación;
- reconocimiento de roles;
- conservación de metadatos en eventos de integración;
- representación de riesgo y nivel en alertas.

## 9. Límite de la fase

No se agregaron middleware, controllers, persistencia, autenticación, simulación ni clientes HTTP. Tampoco se referenciaron todavía los contratos desde servicios que aún no los consumen. Cada referencia se añadirá cuando su caso de uso sea implementado.

La siguiente etapa es la **Fase 4 — Identity Service** y debe comenzar únicamente cuando se solicite.
