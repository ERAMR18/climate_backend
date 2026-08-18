# Fase 5 — Sensor Service

## 1. Objetivo

Sensor Service administra comunidades y el catálogo de sensores simulados. Es propietario exclusivo de `SensorDb`; ningún otro microservicio accede directamente a sus tablas.

La API exige un JWT emitido por Identity Service. `Viewer` puede consultar, mientras `Administrator` y `Operator` pueden crear, modificar, activar o desactivar recursos.

## 2. Estructura implementada

```text
src/Services/SensorService/
├── Climate.Sensors.Api/
│   ├── Configuration/JwtOptions.cs
│   ├── Controllers/
│   │   ├── CommunitiesController.cs
│   │   └── SensorsController.cs
│   ├── Errors/
│   │   ├── GlobalExceptionHandler.cs
│   │   └── ResultExtensions.cs
│   └── Program.cs
├── Climate.Sensors.Application/
│   ├── Abstractions/ISensorCatalogRepository.cs
│   ├── Common/ValidationError.cs
│   ├── Communities/
│   │   ├── CommunityModels.cs
│   │   ├── CommunityService.cs
│   │   └── CommunityValidators.cs
│   ├── Sensors/
│   │   ├── SensorModels.cs
│   │   ├── SensorService.cs
│   │   └── SensorValidators.cs
│   └── DependencyInjection.cs
├── Climate.Sensors.Domain/
│   ├── Communities/Community.cs
│   └── Sensors/
│       ├── Sensor.cs
│       └── SensorType.cs
└── Climate.Sensors.Infrastructure/
    ├── Persistence/
    │   ├── SensorsDbContext.cs
    │   ├── CommunityConfiguration.cs
    │   ├── SensorConfiguration.cs
    │   ├── SensorCatalogRepository.cs
    │   └── Migrations/InitialSensors
    ├── Seeding/
    │   ├── DemoSeedOptions.cs
    │   └── SensorsDatabaseInitializer.cs
    └── DependencyInjection.cs
```

## 3. Dominio

### Community

Contiene nombre, descripción opcional, coordenadas, estado y fecha de creación. Una comunidad puede desactivarse desde su actualización.

### Sensor

Contiene nombre, código, descripción, tipo, unidad, comunidad, coordenadas, estado y fechas de creación/actualización.

El código dispone de `NormalizedCode` para garantizar unicidad sin depender de mayúsculas. `DELETE` es un borrado lógico: cambia `IsActive` a `false` y conserva el sensor para las referencias históricas de otros servicios.

Los tipos soportados son:

```text
Temperature
Humidity
WindSpeed
Rainfall
WaterLevel
```

## 4. Endpoints

### Comunidades

| Método | Ruta | Acceso |
|---|---|---|
| GET | `/api/v1/communities` | Usuario autenticado |
| GET | `/api/v1/communities/{id}` | Usuario autenticado |
| POST | `/api/v1/communities` | Administrator u Operator |
| PUT | `/api/v1/communities/{id}` | Administrator u Operator |

### Sensores

| Método | Ruta | Acceso |
|---|---|---|
| GET | `/api/v1/sensors` | Usuario autenticado |
| GET | `/api/v1/sensors/{id}` | Usuario autenticado |
| POST | `/api/v1/sensors` | Administrator u Operator |
| PUT | `/api/v1/sensors/{id}` | Administrator u Operator |
| PATCH | `/api/v1/sensors/{id}/activate` | Administrator u Operator |
| PATCH | `/api/v1/sensors/{id}/deactivate` | Administrator u Operator |
| DELETE | `/api/v1/sensors/{id}` | Administrator u Operator |
| GET | `/health` | Público |

## 5. Reglas implementadas

- El código del sensor es único sin distinguir mayúsculas.
- Una comunidad debe existir y estar activa al crear o mover un sensor.
- Un sensor no puede reactivarse si su comunidad está inactiva.
- Latitud debe estar entre -90 y 90.
- Longitud debe estar entre -180 y 180.
- Tipo debe pertenecer al catálogo definido.
- Nombre, código y unidad son obligatorios.
- Las entidades EF nunca se exponen directamente.
- Las operaciones I/O propagan `CancellationToken`.

Los conflictos retornan `409`, validaciones `422`, ausencias `404` y errores inesperados Problem Details `500` sin stack trace.

## 6. Persistencia

`SensorDb` contiene tablas `Communities` y `Sensors`. La relación es una clave foránea local con eliminación restringida. Existen índices para:

- nombre de comunidad;
- estado de comunidad;
- código normalizado;
- comunidad y estado del sensor;
- tipo y estado del sensor.

Las coordenadas utilizan `decimal(9,6)` y las fechas `datetimeoffset(0)`.

La migración se administra con:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project src/Services/SensorService/Climate.Sensors.Infrastructure `
  --startup-project src/Services/SensorService/Climate.Sensors.Api
```

## 7. Datos iniciales

Cuando `DemoSeed__Enabled=true` y no existen comunidades, se crea:

- una comunidad demostrativa;
- un sensor de temperatura;
- un sensor de humedad;
- un sensor de viento;
- un sensor de lluvia;
- un sensor de nivel de río.

Los identificadores son deterministas para facilitar la integración académica. Los datos se crean después de aplicar migraciones y no se duplican en reinicios.

## 8. Seguridad

Sensor Service valida de nuevo firma, emisor, audiencia y expiración del JWT. No depende de que Gateway haya realizado la validación. La clave debe proporcionarse mediante `Jwt__SigningKey` y contener al menos 32 caracteres.

La política `ManageSensors` admite los roles `Administrator` y `Operator`. `Viewer` conserva acceso de lectura.

## 9. Paquetes agregados

```text
FluentValidation.DependencyInjectionExtensions 12.1.1
Microsoft.EntityFrameworkCore.SqlServer 10.0.10
Microsoft.EntityFrameworkCore.Design 10.0.10
Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 10.0.10
Microsoft.Extensions.Options.ConfigurationExtensions 10.0.10
Microsoft.AspNetCore.Authentication.JwtBearer 10.0.10
Swashbuckle.AspNetCore 10.2.3
Microsoft.EntityFrameworkCore.InMemory 10.0.10 (tests)
Moq 4.20.72 (tests)
```

## 10. Ejecución local

```powershell
$env:ConnectionStrings__SensorDb='Server=localhost,1433;Database=SensorDb;User Id=sa;Password=REPLACE_ME;TrustServerCertificate=True'
$env:Jwt__SigningKey='THE_SAME_SIGNING_KEY_USED_BY_IDENTITY'
$env:Jwt__Issuer='ClimateMonitoring.Identity'
$env:Jwt__Audience='ClimateMonitoring.Client'
$env:DemoSeed__Enabled='true'

dotnet run --project src/Services/SensorService/Climate.Sensors.Api
```

Swagger se publica en `/swagger` durante Development. `/health` verifica la aplicación y la conexión con `SensorDb`.

## 11. Pruebas

Las pruebas cubren:

- creación de sensor;
- asignación a comunidad;
- unicidad de código;
- borrado lógico;
- activación y desactivación;
- rechazo de comunidad inactiva;
- creación de comunidad;
- coordenadas inválidas;
- tipos desconocidos.

Comandos:

```powershell
dotnet build ClimateMonitoringSystem.sln --no-restore
dotnet test ClimateMonitoringSystem.sln --no-build --no-restore
```

## 12. Límite de la fase

Los eventos de auditoría y `SensorStatusChanged` se conectarán en las fases de Audit y SignalR. Monitoring Service consumirá el catálogo mediante API/contrato explícito en la Fase 6; no accederá a `SensorDb`.

La siguiente etapa es la **Fase 6 — Monitoring Service** y debe comenzar sólo cuando se solicite.
