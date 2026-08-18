# Fase 11 — API Gateway con YARP

## Objetivo

`Climate.Gateway` es el punto de entrada público para Angular. Utiliza YARP Reverse Proxy y mantiene privados los endpoints internos entre microservicios.

## Rutas

| Ruta pública | Destino |
|---|---|
| `/api/auth/*` | Identity `/api/v1/auth/*` |
| `/api/users/*` | Identity `/api/v1/users/*` |
| `/api/sensors/*` | Sensor `/api/v1/sensors/*` |
| `/api/communities/*` | Sensor `/api/v1/communities/*` |
| `/api/monitoring/*` | Monitoring `/api/v1/monitoring/*` |
| `/api/alerts/*` | Alert `/api/v1/alerts/*` |
| `/api/events/*` | Event `/api/v1/events/*` |
| `/api/audit/*` | Audit `/api/v1/audit/*` |
| `/hubs/monitoring` | Monitoring SignalR Hub |

YARP reescribe únicamente el prefijo. Conserva método, cuerpo, query string y el encabezado `Authorization`, por lo que cada microservicio sigue siendo responsable de validar JWT y roles.

Los endpoints `/api/v1/internal/*` no tienen rutas públicas.

## SignalR

La ruta `/hubs/monitoring` permite el upgrade a WebSocket y también los transportes alternativos de SignalR. El frontend debe conectarse al Gateway, no directamente a Monitoring:

```typescript
new HubConnectionBuilder()
  .withUrl(`${gatewayUrl}/hubs/monitoring`, {
    accessTokenFactory: () => token
  })
  .withAutomaticReconnect()
  .build();
```

## CORS

El origen permitido por defecto es `http://localhost:4200`. Se configura mediante:

```text
Cors__AllowedOrigins__0=http://localhost:4200
```

La política permite encabezados y métodos necesarios, y credenciales. No utiliza un origen comodín junto con credenciales.

## Protección y errores

- Límite global por IP: 120 solicitudes por minuto.
- Respuesta `429 Too Many Requests` al superar el límite.
- Problem Details para excepciones no controladas del Gateway.
- Propagación de `X-Forwarded-For`, `X-Forwarded-Proto` y `X-Forwarded-Host`.
- Los errores de conexión al destino se presentan como `502 Bad Gateway`.

El limitador es en memoria. En un despliegue con varias réplicas debe sustituirse o complementarse con un límite distribuido en el proxy frontal.

## Health check

```http
GET /health
```

El Gateway consulta `/health` de los seis microservicios con un timeout de tres segundos. Devuelve `Healthy` solamente cuando todos responden correctamente; de lo contrario devuelve `Unhealthy` y HTTP 503.

## Configuración de destinos

Los destinos locales usan los puertos 5101–5106. Pueden sobrescribirse con variables como:

```text
ReverseProxy__Clusters__identity__Destinations__primary__Address
ReverseProxy__Clusters__sensors__Destinations__primary__Address
ReverseProxy__Clusters__monitoring__Destinations__primary__Address
ReverseProxy__Clusters__alerts__Destinations__primary__Address
ReverseProxy__Clusters__events__Destinations__primary__Address
ReverseProxy__Clusters__audit__Destinations__primary__Address
```

La Fase 12 reemplazará los destinos locales por nombres DNS de Docker Compose.

## Ejecución local

Después de iniciar los microservicios:

```powershell
dotnet run --project src/Gateway/Climate.Gateway
```

El perfil actual publica el Gateway en `http://localhost:5243`.

## Alcance

Esta fase no agrega contenedores ni orquestación; eso corresponde a la Fase 12.
