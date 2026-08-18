# Fase 14 — Swagger y documentación final

## Resultado

La documentación operativa quedó centralizada en el `README.md` y el contrato público del Gateway en `docs/openapi/climate-api-v1.json`.

El contrato se genera a partir de los documentos Swagger producidos en ejecución por Identity, Sensor, Monitoring, Alert, Event y Audit. El script `scripts/export-openapi.ps1`:

1. Descarga los seis documentos desde la red privada de Docker.
2. excluye rutas `/internal`;
3. transforma `/api/v1/*` en las rutas públicas `/api/*` del Gateway;
4. combina y verifica los esquemas sin aceptar colisiones incompatibles;
5. agrega seguridad JWT por operación y respuestas 401/403;
6. asigna `operationId` únicos y deterministas;
7. escribe OpenAPI 3.0.1 en UTF-8.

## Verificación realizada

- Solución compilada con cero advertencias y cero errores.
- Stack completo de ocho contenedores saludable.
- Gateway agregado saludable con los seis downstream disponibles.
- Contrato con 29 rutas, 35 operaciones, 24 esquemas y 35 identificadores únicos.
- Cero referencias a esquemas inexistentes.
- Cero rutas internas o rutas `/api/v1` expuestas en el contrato público.

## Regeneración

```powershell
docker compose -f docker-compose.yml -f docker-compose.swagger.yml --env-file .env up -d --build --wait
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/export-openapi.ps1 -ComposeEnvFile .env
docker compose --env-file .env down
```

El JSON generado debe versionarse junto con cualquier cambio incompatible o aditivo de la API para que el frontend pueda regenerar su cliente de manera reproducible.
