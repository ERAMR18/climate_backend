# Fase 6 — Monitoring Service

## 1. Objetivo

Monitoring Service recibe y genera lecturas, las almacena en `MonitoringDb`, ofrece datos actuales e históricos y controla la simulación climática. Consume el catálogo de Sensor Service mediante HTTP; no accede a `SensorDb`.

SignalR se implementará en la Fase 10 y la evaluación de riesgos en la Fase 7. Esta fase deja los puntos de extensión preparados sin simular esas dependencias.

## 2. Componentes

```text
Climate.Monitoring.Api
├── Controllers/MonitoringController.cs
├── Configuration/JwtOptions.cs
├── Errors/
└── Program.cs

Climate.Monitoring.Application
├── Abstractions/
│   ├── IMonitoringRepository.cs
│   ├── ISensorCatalogClient.cs
│   ├── ISimulatedValueGenerator.cs
│   └── ISimulationControl.cs
├── Readings/
│   ├── modelos y respuestas
│   ├── validador
│   └── MonitoringService.cs
└── Simulation/SimulationService.cs

Climate.Monitoring.Domain
└── Readings/
    ├── SensorReading.cs
    └── SensorType.cs

Climate.Monitoring.Infrastructure
├── Clients/SensorCatalogClient.cs
├── Persistence/
│   ├── MonitoringDbContext.cs
│   ├── SensorReadingConfiguration.cs
│   ├── MonitoringRepository.cs
│   └── Migrations/InitialMonitoring
└── Simulation/
    ├── ClimateSimulationWorker.cs
    ├── SimulationControl.cs
    ├── SimulationOptions.cs
    └── SimulatedValueGenerator.cs
```

## 3. Persistencia

Cada lectura almacena:

- identificador propio;
- `SensorId` externo;
- `CommunityId` desnormalizado para consultas;
- tipo de sensor;
- valor y unidad;
- fecha UTC de registro.

`SensorId` y `CommunityId` no son claves foráneas hacia Sensor Service. Los índices están optimizados para búsquedas por sensor/fecha, comunidad/fecha y fecha global.

La migración se encuentra en `Persistence/Migrations/InitialMonitoring`.

## 4. Integración con Sensor Service

Monitoring consulta:

```http
GET /api/v1/internal/sensors/active
X-Internal-Api-Key: ...
```

La ruta interna devuelve `SensorSummary` y no aparece en Swagger público. Sensor Service valida una clave de al menos 32 caracteres. Las configuraciones deben coincidir:

```text
Sensor Service: InternalApi__ApiKey
Monitoring:     SensorService__ApiKey
```

La URL se configura con `SensorService__BaseUrl`. El cliente usa `HttpClientFactory`, timeout/cancelación del request y un contrato explícito de Building Blocks.

## 5. Simulador

`ClimateSimulationWorker` hereda de `BackgroundService` y usa `PeriodicTimer`. En cada ciclo:

1. comprueba si la simulación está activa;
2. solicita sensores activos;
3. genera un valor por sensor;
4. crea las lecturas con el mismo instante UTC;
5. persiste el lote.

El estado START/STOP es seguro para concurrencia y vive durante la ejecución del proceso. `Simulation__Enabled` define el estado inicial.

## 6. Rangos de demostración

Los rangos están en configuración, no en la lógica:

```json
{
  "Simulation": {
    "Enabled": true,
    "IntervalSeconds": 30,
    "Temperature": { "Minimum": -5, "Maximum": 45, "DecimalPlaces": 1 },
    "Humidity": { "Minimum": 10, "Maximum": 100, "DecimalPlaces": 1 },
    "WindSpeed": { "Minimum": 0, "Maximum": 120, "DecimalPlaces": 1 },
    "Rainfall": { "Minimum": 0, "Maximum": 80, "DecimalPlaces": 1 },
    "WaterLevel": { "Minimum": 0.2, "Maximum": 8, "DecimalPlaces": 2 }
  }
}
```

Son valores simulados para demostración, no límites oficiales ni reglas de alerta. Se validan al iniciar: intervalo entre 1 y 3600 segundos, mínimo no mayor que máximo y entre 0 y 4 decimales.

## 7. Endpoints

| Método | Ruta | Acceso |
|---|---|---|
| GET | `/api/v1/monitoring/current` | Autenticado |
| GET | `/api/v1/monitoring/sensors/{id}/latest` | Autenticado |
| GET | `/api/v1/monitoring/sensors/{id}/history` | Autenticado |
| GET | `/api/v1/monitoring/sensors/{id}/chart` | Autenticado |
| POST | `/api/v1/monitoring/readings` | Administrator u Operator |
| GET | `/api/v1/monitoring/simulation/status` | Autenticado |
| POST | `/api/v1/monitoring/simulation/start` | Administrator u Operator |
| POST | `/api/v1/monitoring/simulation/stop` | Administrator u Operator |
| POST | `/api/v1/monitoring/simulation/reset` | Administrator |
| POST | `/api/v1/monitoring/system/reset` | Administrator |
| GET | `/health` | Público |

El historial admite `from`, `to` y `communityId`. El sensor se recibe en la ruta. Las series admiten intervalos `1m`, `5m`, `15m` y `1h`; cada punto contiene el promedio del intervalo.

## 8. Semántica de reset

Ambas rutas de reset ejecutan en esta fase la misma operación:

- detienen la simulación;
- eliminan todas las lecturas de `MonitoringDb`;
- conservan usuarios;
- conservan comunidades y sensores;
- conservan alertas, eventos y auditoría cuando esos servicios existan;
- no reinician automáticamente la simulación.

El evento `SystemReset` y el registro `ResetSystem` se conectarán en SignalR y Audit Service. No se usa una transacción distribuida.

## 9. Seguridad y errores

La API valida JWT de forma independiente con la misma firma, emisor y audiencia de Identity Service. Las operaciones de simulación usan políticas por rol.

FluentValidation rechaza identificadores vacíos, valores fuera del rango técnico de almacenamiento y fechas futuras. Los errores esperados utilizan Problem Details con `404` o `422`; el manejador global no expone stack traces.

## 10. Paquetes adicionales

```text
FluentValidation.DependencyInjectionExtensions 12.1.1
Microsoft.EntityFrameworkCore.SqlServer 10.0.10
Microsoft.EntityFrameworkCore.Design 10.0.10
Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 10.0.10
Microsoft.Extensions.Options.ConfigurationExtensions 10.0.10
Microsoft.Extensions.Hosting.Abstractions 10.0.10
Microsoft.Extensions.Http 10.0.10
Microsoft.AspNetCore.Authentication.JwtBearer 10.0.10
Swashbuckle.AspNetCore 10.2.3
Microsoft.EntityFrameworkCore.InMemory 10.0.10 (tests)
Moq 4.20.72 (tests)
```

## 11. Ejecución

```powershell
$env:ConnectionStrings__MonitoringDb='Server=localhost,1433;Database=MonitoringDb;User Id=sa;Password=REPLACE_ME;TrustServerCertificate=True'
$env:Jwt__SigningKey='THE_SAME_KEY_USED_BY_IDENTITY'
$env:SensorService__BaseUrl='http://localhost:5102/'
$env:SensorService__ApiKey='THE_SAME_INTERNAL_KEY_USED_BY_SENSOR_SERVICE'

dotnet run --project src/Services/MonitoringService/Climate.Monitoring.Api
```

Sensor Service y SQL Server deben estar disponibles para la simulación. Una falla de ciclo se registra y el worker continúa en el intervalo siguiente.

## 12. Pruebas

Se prueban creación manual, metadatos del catálogo, sensor inactivo, generación de lotes, validación temporal, historial, agregación de gráfica, START, STOP y RESET.

```powershell
dotnet build ClimateMonitoringSystem.sln --no-restore
dotnet test ClimateMonitoringSystem.sln --no-build --no-restore
```

La siguiente etapa es la **Fase 7 — Alert Service** y debe comenzar sólo cuando se solicite.
