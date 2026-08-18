# Fase 12 — Docker y Docker Compose

## Objetivo

La solución completa se ejecuta en contenedores Linux con .NET 10 y SQL Server 2022. Compose crea ocho contenedores:

```text
gateway
identity-service
sensor-service
monitoring-service
alert-service
event-service
audit-service
sqlserver
```

## Imágenes de aplicación

Cada API y el Gateway tienen un Dockerfile multi-stage:

- compilación con `mcr.microsoft.com/dotnet/sdk:10.0.300`;
- publicación Release sin apphost;
- ejecución con `mcr.microsoft.com/dotnet/aspnet:10.0`;
- usuario no privilegiado `app`;
- `curl` disponible únicamente para health checks;
- puerto interno 8080.

La banda 10.0.300 coincide con `global.json`, evitando que una etiqueta flotante seleccione una banda SDK incompatible.

## SQL Server y persistencia

Se usa `mcr.microsoft.com/mssql/server:2022-latest` en edición Developer. Una instancia aloja seis bases independientes:

```text
IdentityDb
SensorDb
MonitoringDb
AlertDb
EventDb
AuditDb
```

Los servicios no comparten tablas ni `DbContext`. El volumen `sqlserver-data` monta `/var/opt/mssql`, por lo que los datos sobreviven reinicios y `docker compose down`.

## Preparación

```bash
cp .env.example .env
```

Antes de iniciar, reemplazar todos los valores `CHANGE_ME`. Como mínimo:

```text
SQLSERVER_SA_PASSWORD
JWT_SIGNING_KEY
INTERNAL_API_KEY
ADMIN_SEED_PASSWORD
```

La contraseña de SQL Server debe tener al menos ocho caracteres y combinar mayúsculas, minúsculas, números y símbolos. JWT e Internal API Key deben tener al menos 32 caracteres.

No se copia `.env` en las imágenes gracias a `.dockerignore`.

## Ejecución

Construir e iniciar:

```bash
docker compose up --build -d
docker compose ps
```

Gateway queda disponible por defecto en:

```text
http://localhost:8080
http://localhost:8080/health
http://localhost:8080/hubs/monitoring
```

SQL Server se expone en `localhost:1433` para administración local. Los seis servicios solo son accesibles en la red privada `backend`.

Ver logs:

```bash
docker compose logs -f gateway
docker compose logs -f monitoring-service
```

Detener conservando los datos:

```bash
docker compose down
```

Eliminar también los datos, únicamente cuando se desee un reinicio irreversible de las bases:

```bash
docker compose down --volumes
```

## Dependencias y salud

SQL Server debe estar saludable antes de iniciar las APIs. Su health check ejecuta `SELECT 1`. Cada servicio comprueba su `DbContext`; el Gateway inicia después de que los seis servicios estén saludables y su `/health` agrega sus resultados.

Las migraciones EF Core se aplican automáticamente al arrancar cada microservicio.

## Comunicación interna

Compose utiliza DNS por nombre de servicio:

```text
monitoring-service → sensor-service, alert-service, audit-service
alert-service      → event-service, monitoring-service
sensor-service     → audit-service, monitoring-service
identity-service   → audit-service
gateway            → todos los servicios públicos
```

Las llamadas internas usan `INTERNAL_API_KEY`. Gateway no publica las rutas internas.

## Variables operativas

Pueden personalizarse sin editar Compose:

```text
GATEWAY_PORT
SQLSERVER_PORT
FRONTEND_ORIGIN
JWT_EXPIRATION_MINUTES
DEMO_SEED_ENABLED
SIMULATION_ENABLED
SIMULATION_INTERVAL_SECONDS
```

## Despliegue GNU/Linux

La configuración no requiere interfaz gráfica. En una VPS se recomienda además:

- usar Docker Engine y el plugin Compose actualizados;
- guardar secretos fuera del repositorio;
- no publicar el puerto 1433 a Internet;
- colocar TLS delante del Gateway mediante un reverse proxy;
- fijar el digest de las imágenes para despliegues reproducibles;
- realizar copias de seguridad del volumen de SQL Server.

## Validación de la fase

Se validaron la sintaxis de Compose, los siete builds Linux, el arranque saludable de los ocho contenedores, las migraciones de las seis bases y el health agregado del Gateway.
