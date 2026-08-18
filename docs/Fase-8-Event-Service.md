# Fase 8 — Event Service

## Objetivo

Event Service mantiene el historial persistente de las alertas climáticas. Es propietario de `EventDb` y recibe cambios desde Alert Service mediante una API interna; no accede directamente a `AlertDb`.

## Modelo

`ClimateEvent` contiene `Id`, `AlertId`, `SensorId`, `CommunityId`, `RiskType`, `AlertLevel`, `Description`, `OccurredAt` y `ResolvedAt`.

`AlertId` tiene un índice único. Por ello, las notificaciones repetidas son idempotentes: la primera crea el registro y las siguientes actualizan el nivel, descripción o fecha de resolución sin duplicar el historial de una alerta.

## Flujo

```text
Monitoring → Alert Service → crea/actualiza/resuelve alerta
                            → POST interno a Event Service
                            → EventDb
```

La integración usa HTTP y el encabezado `X-Internal-Api-Key`. `EventService__ApiKey` de Alert Service debe coincidir con `InternalApi__ApiKey` de Event Service.

## Endpoints

| Método | Ruta | Acceso |
|---|---|---|
| GET | `/api/v1/events` | JWT autenticado |
| GET | `/api/v1/events/{id}` | JWT autenticado |
| POST | `/api/v1/internal/events` | Clave interna |
| GET | `/health` | Público |

La colección permite los filtros `riskType`, `alertLevel`, `sensorId`, `communityId`, `from` y `to`. Los resultados se ordenan por `OccurredAt` descendente y se rechaza un intervalo donde `from` sea posterior a `to`.

Ejemplo:

```http
GET /api/v1/events?riskType=Flood&alertLevel=Red&from=2026-08-01T00:00:00Z
Authorization: Bearer <token>
```

## Persistencia

La migración `InitialEvents` crea `ClimateEvents` e índices para:

- unicidad por alerta;
- riesgo, nivel y fecha;
- sensor y fecha;
- comunidad y fecha.

La migración se aplica al iniciar el servicio:

```powershell
dotnet run --project src/Services/EventService/Climate.Events.Api
```

También puede aplicarse manualmente:

```powershell
dotnet tool run dotnet-ef database update `
  --project src/Services/EventService/Climate.Events.Infrastructure `
  --startup-project src/Services/EventService/Climate.Events.Api
```

## Configuración

```text
ConnectionStrings__EventDb
Jwt__Issuer
Jwt__Audience
Jwt__SigningKey
InternalApi__ApiKey
```

Alert Service incorpora:

```text
EventService__BaseUrl=http://localhost:5105/
EventService__ApiKey=<misma clave interna>
```

## Seguridad y errores

- JWT obligatorio para consultar el historial.
- Clave de al menos 32 caracteres para escritura interna.
- Endpoint interno excluido de Swagger.
- Problem Details con `traceId` y sin exponer stack traces.
- Enums serializados como texto.

## Alcance

Esta fase registra el historial de alertas y resoluciones. La bitácora de acciones de usuarios pertenece a la Fase 9 y SignalR a la Fase 10.
