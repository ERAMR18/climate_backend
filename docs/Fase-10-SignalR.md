# Fase 10 — Comunicación en tiempo real con SignalR

## Objetivo

La fase incorpora un canal autenticado para que el dashboard reciba cambios sin refrescar la página. El punto único de conexión se aloja en Monitoring Service:

```text
/hubs/monitoring
```

`MonitoringHub` no expone comandos de negocio; funciona como canal servidor → cliente. Las operaciones continúan realizándose por las APIs REST autorizadas.

## Eventos

| Evento | Origen | Contenido |
|---|---|---|
| `SensorReadingUpdated` | Monitoring | Lectura persistida con sensor, comunidad, valor, unidad y fecha |
| `AlertGenerated` | Alert | Estado actual de la alerta, incluida su resolución |
| `SensorStatusChanged` | Sensor | Identificador del sensor y estado activo |
| `SystemReset` | Monitoring | Estado de la simulación después del reset |

Las lecturas manuales y las generadas por el worker se publican después de guardarse y evaluarse. Los cambios de Alert y Sensor llegan a Monitoring por `POST /api/v1/internal/realtime`, protegido con `X-Internal-Api-Key` y excluido de Swagger.

## Seguridad

El Hub requiere un JWT válido. Para WebSockets o Server-Sent Events, SignalR puede enviar el token con `access_token` en la cadena de consulta exclusivamente bajo `/hubs/monitoring`; el mismo emisor, audiencia, firma y expiración de las APIs son validados.

La ruta interna solo acepta los cuatro nombres declarados en `RealtimeEventNames`, lo que impide utilizarla para emitir eventos arbitrarios.

## Configuración entre servicios

Alert y Sensor Service utilizan:

```text
RealtimeService__BaseUrl=http://localhost:5103/
RealtimeService__ApiKey=<misma clave que InternalApi__ApiKey de Monitoring>
```

## Cliente Angular

Ejemplo conceptual con `@microsoft/signalr`:

```typescript
const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${apiUrl}/hubs/monitoring`, {
    accessTokenFactory: () => accessToken
  })
  .withAutomaticReconnect()
  .build();

connection.on('SensorReadingUpdated', reading => updateReading(reading));
connection.on('AlertGenerated', alert => updateAlert(alert));
connection.on('SensorStatusChanged', sensor => updateSensor(sensor));
connection.on('SystemReset', status => resetDashboard(status));

await connection.start();
```

El frontend debe eliminar los handlers con `connection.off(...)` al destruir el servicio o componente y manejar los estados de reconexión.

## Flujo

```text
Monitoring ──lecturas/reset──────────────┐
                                         ▼
Sensor ──API interna──┐            MonitoringHub ──► Angular
Alert  ──API interna──┘
```

## Alcance

Esta fase usa el backplane en memoria de ASP.NET Core, adecuado para una instancia académica. Si Monitoring se replica horizontalmente, deberá añadirse un backplane como Redis o Azure SignalR para distribuir mensajes entre instancias.
