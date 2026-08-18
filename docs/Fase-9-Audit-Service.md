# Fase 9 — Audit Service

## Objetivo

Audit Service conserva una bitácora inmutable de acciones realizadas por usuarios. Es propietario de `AuditDb`; los demás servicios escriben mediante una API interna y solo administradores pueden consultar el historial.

## Modelo

`AuditLog` almacena `Id`, `UserId`, `UserName`, `Action`, `Resource`, `ResourceId`, `Description`, `IpAddress` y `Timestamp`.

El identificador del mensaje es también el identificador del registro. Repetir un mensaje devuelve el registro existente, evitando duplicados durante reintentos.

## Acciones conectadas

- `Login` desde Identity Service.
- `UpdateUser` para edición y cambio de estado.
- `CreateSensor`, `UpdateSensor`, `ActivateSensor` y `DeactivateSensor` desde Sensor Service.
- `StartSimulation`, `StopSimulation` y `ResetSystem` desde Monitoring Service.

Solo se registra una acción después de que la operación principal haya terminado correctamente. Se guarda el usuario extraído del JWT, el recurso, su identificador cuando aplica y la IP remota. En el login se utiliza el usuario retornado por la autenticación.

## Endpoints

| Método | Ruta | Acceso |
|---|---|---|
| GET | `/api/v1/audit` | Solo Administrator |
| GET | `/api/v1/audit/{id}` | Solo Administrator |
| POST | `/api/v1/internal/audit` | `X-Internal-Api-Key` |
| GET | `/health` | Público |

La colección admite `userId`, `action`, `resource`, `from` y `to`, y se ordena por fecha descendente.

## Configuración

Audit Service requiere `ConnectionStrings__AuditDb`, la configuración JWT común e `InternalApi__ApiKey`.

Identity, Sensor y Monitoring requieren:

```text
AuditService__BaseUrl=http://localhost:5106/
AuditService__ApiKey=<misma clave interna>
```

## Migración

La migración `InitialAudit` crea `AuditLogs` e índices por fecha, usuario/fecha y acción/fecha. Se aplica al iniciar el servicio o manualmente:

```powershell
dotnet tool run dotnet-ef database update `
  --project src/Services/AuditService/Climate.Audit.Infrastructure `
  --startup-project src/Services/AuditService/Climate.Audit.Api
```

## Reinicio del sistema

`POST /api/v1/monitoring/system/reset` detiene la simulación y elimina sus lecturas operativas mediante el comportamiento definido en Monitoring. Conserva usuarios, comunidades, sensores y la propia bitácora. La operación exitosa genera `ResetSystem`.

## Alcance

Esta fase no implementa el dashboard agregado ni SignalR; pertenecen a fases posteriores.
