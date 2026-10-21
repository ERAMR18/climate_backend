# MongoDB en la VPS Ubuntu 26.04

La VPS con Ubuntu 26.04.1 y kernel `7.0.0-38-generic` rechazó el arranque de `mongo:8.0` con el error `MongoDB cannot start: Linux kernel versions 6.19 and newer...`. MongoDB documenta esta combinación en [SERVER-136848](https://jira.mongodb.org/browse/SERVER-136848) y la compatibilidad por versión en [SERVER-125742](https://jira.mongodb.org/browse/SERVER-125742).

El archivo `docker-compose.vps-mongo.yml` conserva el ajuste que funcionó en esa VPS: MongoDB 7.0 para Audit y Report, con volúmenes independientes `audit-mongo7-data` y `report-mongo7-data`. Debe agregarse al final de los archivos Compose. Las demás instalaciones conservan la configuración base de MongoDB 8.0.

## Operación en Bash

Desde `/opt/climate/climate_backend`, con el frontend disponible en `../frontend/climate-monitoring-web`:

```bash
COMPOSE=(
  docker compose
  --project-name climate-phase3-vps
  --env-file .env.phase3.local
  -f docker-compose.legacy.yml
  -f docker-compose.phase3-auth.yml
  -f docker-compose.phase3-community.yml
  -f docker-compose.phase3-alerts.yml
  -f docker-compose.phase3-audit.yml
  -f docker-compose.phase3-reports.yml
  -f docker-compose.phase3-web.yml
  -f docker-compose.phase3-images.yml
  -f docker-compose.hostinger.yml
  -f docker-compose.vps-mongo.yml
)

"${COMPOSE[@]}" config --quiet
"${COMPOSE[@]}" pull audit-mongo report-mongo
"${COMPOSE[@]}" up -d --wait --wait-timeout 300 \
  audit-mongo report-mongo audit-service report-service
"${COMPOSE[@]}" ps
```

Conservar el mismo proyecto y el último archivo en todas las actualizaciones para reutilizar los datos que ya existen en los volúmenes MongoDB 7. No ejecutar `down --volumes` para aplicar este ajuste ni montar volúmenes de MongoDB 8 directamente en MongoDB 7. Los volúmenes anteriores quedan conservados; el cambio inicial crea bases independientes para Audit y Report. No modifica comunidades, sensores, lecturas, usuarios, reglas ni alertas.

El archivo privado debe contener `AUTH_ADMIN_SEED_PASSWORD` y también `ADMIN_SEED_PASSWORD` con el mismo valor: el Compose base valida el segundo nombre antes de aplicar el overlay de Auth. Los valores públicos de frontend y CORS deben utilizar el dominio HTTPS. No publicar el archivo privado de entorno.
