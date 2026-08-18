# Climate Monitoring System

## Requisitos previos

Antes de iniciar, instala:

- .NET SDK **10.0.300** (la versión está fijada en `global.json`).
- Docker Desktop con Docker Compose v2, recomendado para ejecutar todo el sistema.
- PowerShell 5.1 o superior, necesario para regenerar el contrato OpenAPI.
- Opcional para ejecución sin Docker: SQL Server 2022 accesible en `localhost:1433`.

Verifica las herramientas:

```powershell
dotnet --version
docker --version
docker compose version
```

Los puertos predeterminados son `8080` para el Gateway, `1433` para SQL Server y `4200` para el frontend permitido por CORS. Deben estar libres.

## Descripción y objetivo

Backend de monitoreo climático construido con .NET 10 y microservicios. Registra comunidades y sensores, recibe o simula lecturas ambientales, calcula riesgos, produce alertas y eventos, conserva auditoría y transmite actualizaciones en tiempo real.

Su objetivo es exponer un único contrato HTTP estable mediante el API Gateway para que aplicaciones web puedan consultar y administrar el sistema sin acoplarse a las APIs internas.

## Arquitectura

Cada servicio aplica una separación Domain, Application, Infrastructure y Api. Los servicios poseen bases de datos independientes en una instancia de SQL Server. El Gateway es la única entrada pública y reenvía `/api/*` y `/hubs/monitoring` mediante YARP.

```mermaid
flowchart LR
    FE[Frontend] -->|HTTP / WebSocket| GW[API Gateway :8080]
    GW --> ID[Identity]
    GW --> SE[Sensor]
    GW --> MO[Monitoring + SignalR]
    GW --> AL[Alert]
    GW --> EV[Event]
    GW --> AU[Audit]
    ID --> DB[(SQL Server)]
    SE --> DB
    MO --> DB
    AL --> DB
    EV --> DB
    AU --> DB
    MO --> SE
    MO --> AL
    AL --> EV
    ID --> AU
    SE --> AU
    MO --> AU
```

### Microservicios

| Servicio | Responsabilidad | Puerto local |
|---|---|---:|
| Identity | Registro, inicio de sesión JWT y usuarios | 5101 |
| Sensor | Comunidades, sensores y estado operativo | 5102 |
| Monitoring | Lecturas, simulación, estadísticas y SignalR | 5103 |
| Alert | Reglas, alertas y resolución | 5104 |
| Event | Historial de eventos climáticos | 5105 |
| Audit | Trazabilidad administrativa | 5106 |
| Gateway | Entrada pública y health agregado | 8080 |

### Tecnologías

.NET 10, ASP.NET Core Web API, Entity Framework Core 10, SQL Server 2022, JWT Bearer, SignalR, YARP, Swashbuckle/OpenAPI 3.0, xUnit, FluentAssertions, Moq, Testcontainers y Docker Compose.

## Configuración

1. Crea el archivo local de variables:

```powershell
Copy-Item .env.example .env
```

2. Cambia todos los valores `CHANGE_ME` en `.env`. Las claves JWT e interna deben tener al menos 32 caracteres y la contraseña de SQL Server debe cumplir su política de complejidad.

Variables principales:

| Variable | Uso |
|---|---|
| `SQLSERVER_SA_PASSWORD` | Contraseña del contenedor SQL Server |
| `JWT_SIGNING_KEY` | Firma de tokens; debe coincidir en todos los servicios |
| `INTERNAL_API_KEY` | Autenticación entre microservicios |
| `ADMIN_SEED_USERNAME`, `ADMIN_SEED_EMAIL`, `ADMIN_SEED_PASSWORD` | Administrador inicial |
| `DEMO_SEED_ENABLED` | Carga comunidades y sensores de demostración |
| `SIMULATION_ENABLED`, `SIMULATION_INTERVAL_SECONDS` | Simulador de lecturas |
| `FRONTEND_ORIGIN` | Origen permitido por CORS |
| `GATEWAY_PORT`, `SQLSERVER_PORT` | Puertos publicados |

No confirmes `.env` ni secretos reales en el repositorio.

## Ejecutar con Docker (recomendado)

Desde la raíz:

```powershell
docker compose --env-file .env up -d --build --wait
docker compose --env-file .env ps
```

Comprueba el sistema en `http://localhost:8080/health`. Las migraciones se aplican automáticamente al arrancar cada API y los datos quedan en el volumen `climate-monitoring_sqlserver-data`.

Para ver registros o detenerlo sin borrar datos:

```powershell
docker compose --env-file .env logs -f
docker compose --env-file .env down
```

`docker compose down -v` también elimina todas las bases de datos; úsalo únicamente si deseas reiniciar los datos.

## Ejecutar localmente

1. Inicia SQL Server, crea `.env` y ajusta las seis variables `ConnectionStrings__*` de `.env.example`.
2. Carga esas variables en la terminal o configura los mismos valores con User Secrets.
3. Restaura, compila y ejecuta cada proceso en una terminal distinta:

```powershell
dotnet restore ClimateMonitoringSystem.sln
dotnet build ClimateMonitoringSystem.sln --no-restore
dotnet run --project src/Services/IdentityService/Climate.Identity.Api
dotnet run --project src/Services/SensorService/Climate.Sensors.Api
dotnet run --project src/Services/MonitoringService/Climate.Monitoring.Api
dotnet run --project src/Services/AlertService/Climate.Alerts.Api
dotnet run --project src/Services/EventService/Climate.Events.Api
dotnet run --project src/Services/AuditService/Climate.Audit.Api
dotnet run --project src/Gateway/Climate.Gateway
```

Los archivos `launchSettings.json` asignan los puertos 5101–5106. Las URLs locales de los servicios y las claves `*Service__ApiKey` deben coincidir con `InternalApi__ApiKey`.

## Migraciones

En Docker se aplican automáticamente. Para crear una migración nueva, sustituye `<Service>` y rutas por el servicio correspondiente:

```powershell
dotnet ef migrations add NombreMigracion --project src/Services/<Service>/<InfrastructureProject>.csproj --startup-project src/Services/<Service>/<ApiProject>.csproj --context <DbContext>
dotnet ef database update --project src/Services/<Service>/<InfrastructureProject>.csproj --startup-project src/Services/<Service>/<ApiProject>.csproj --context <DbContext>
```

No reutilices un `DbContext` ni una base entre servicios.

## Swagger y contrato para el frontend

La interfaz pública está disponible en `http://localhost:8080/swagger` y su JSON en `http://localhost:8080/swagger/v1/swagger.json`, también cuando Docker se ejecuta en Production. El contrato canónico y versionado es [`docs/openapi/climate-api-v1.json`](docs/openapi/climate-api-v1.json). Describe exactamente las rutas públicas del Gateway, cuerpos, parámetros, respuestas, esquemas y seguridad Bearer; excluye endpoints internos. Es el archivo que debe usarse para generar el cliente del frontend.

Para regenerarlo después de modificar controladores:

```powershell
docker compose -f docker-compose.yml -f docker-compose.swagger.yml --env-file .env up -d --build --wait
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/export-openapi.ps1 -ComposeEnvFile .env
docker compose --env-file .env down
```

El archivo contiene `operationId` deterministas. Un ejemplo de generación para Angular es:

```powershell
npx @openapitools/openapi-generator-cli generate -i docs/openapi/climate-api-v1.json -g typescript-angular -o ../frontend/src/app/api
```

En modo Development, cada servicio expone además `/swagger` y `/swagger/v1/swagger.json` en su puerto directo. En Production solamente se publica la documentación unificada del Gateway.

Autenticación en Swagger: llama `POST /api/auth/login`, copia el valor de `accessToken`, pulsa **Authorize** y pega únicamente el token, sin el prefijo `Bearer`. Swagger UI agrega el prefijo y conserva la autorización al recargar. Para clientes HTTP envía `Authorization: Bearer <token>`. Registro e inicio de sesión son anónimos; el resto del contrato indica Bearer y documenta `401`/`403`.

## SignalR

El hub público está en `http://localhost:8080/hubs/monitoring`. El cliente debe enviar el JWT mediante `accessTokenFactory`. Los eventos disponibles son `ReadingReceived`, `AlertCreated`, `AlertResolved`, `SensorStatusChanged` y `SystemReset`.

## Pruebas

Ejecuta toda la suite:

```powershell
dotnet test ClimateMonitoringSystem.sln --no-restore
```

Algunas pruebas de integración usan Testcontainers y requieren Docker activo. Para cobertura:

```powershell
dotnet test ClimateMonitoringSystem.sln --collect:"XPlat Code Coverage"
```

## Endpoints públicos principales

Todas las rutas se consumen a través de `http://localhost:8080`:

- Autenticación: `/api/auth/register`, `/api/auth/login`.
- Usuarios: `/api/users`, `/api/users/me`, `/api/users/{id}`.
- Catálogo: `/api/communities`, `/api/sensors`.
- Monitoreo: `/api/monitoring/current`, `/api/monitoring/readings`, `/api/monitoring/sensors/{sensorId}/history`.
- Simulación: `/api/monitoring/simulation/status|start|stop|reset`.
- Alertas, eventos y auditoría: `/api/alerts`, `/api/events`, `/api/audit`.

El detalle normativo de métodos, consultas y modelos está en el contrato OpenAPI; no dupliques manualmente esos tipos en el frontend.

## Datos iniciales

Al primer arranque se crea el administrador configurado en `ADMIN_SEED_USERNAME`, `ADMIN_SEED_EMAIL` y `ADMIN_SEED_PASSWORD`. Con los valores de ejemplo es `admin` / `CHANGE_ME_Admin_2026!`; cambia la contraseña antes de cualquier despliegue. Los roles disponibles son `Administrator`, `Operator` y `Viewer`.

Con `DEMO_SEED_ENABLED=true`, el Sensor Service agrega de forma idempotente tres comunidades de demostración —Ciudad de Guatemala, Puerto Barrios y Quetzaltenango— y quince sensores de temperatura, humedad, viento, lluvia y nivel de agua. El simulador de Monitoring genera lecturas para los sensores activos. Reiniciar los servicios no duplica estos registros.

## Solución de problemas

- **El contenedor SQL Server no está healthy:** valida complejidad de `SQLSERVER_SA_PASSWORD`, puerto 1433 y memoria disponible en Docker.
- **401:** renueva el token y confirma que issuer, audience y `JWT_SIGNING_KEY` coincidan en todos los servicios.
- **403:** el usuario está autenticado, pero su rol no permite la operación.
- **502/503 en Gateway:** ejecuta `docker compose --env-file .env ps` y revisa `docker compose --env-file .env logs <servicio>`.
- **CORS:** establece `FRONTEND_ORIGIN` con esquema, host y puerto exactos, sin comodines.
- **No aparecen datos demo:** confirma `DEMO_SEED_ENABLED=true`; el seed solo agrega registros faltantes.
- **Conflictos de puertos:** cambia `GATEWAY_PORT` o `SQLSERVER_PORT` en `.env`.
- **Regeneración OpenAPI falla:** inicia con `docker-compose.swagger.yml`; Swagger está deshabilitado en Production deliberadamente.
