# Fase 3 — Ejecución local y recuperación

Alcance acordado: instalación local nueva, sin requerir históricos ni despliegue VPS. Exclusiones en FASE_3_FUERA_DE_ALCANCE.md.

## Arranque

Desde backend, con Docker Desktop activo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start-Phase3.ps1 -NoBuild
```

Sin imágenes construidas, omitir -NoBuild. Initialize-Phase3.ps1 genera contraseñas aleatorias y referencias URI en .env.phase3.local, ignorado por Git, y conserva un archivo existente. No lee ni sobrescribe .env anterior. Usuario admin; abrir el archivo local y consultar AUTH_ADMIN_SEED_PASSWORD para iniciar sesión. No se imprimen contraseñas.

El proyecto climate-phase3-local tiene volúmenes nuevos; no usa ni borra los de la instalación anterior. Usar una sola instancia local para evitar conflictos de puertos. Frontend http://localhost:4200 y Gateway http://localhost:8080; PostgreSQL/Mongo/Rabbit y APIs internas sin puertos públicos. SQL escucha solo en loopback. Configurar frontend/origen/puertos en el archivo local si corresponde.

Comandos equivalentes y operaciones normales:

```powershell
./scripts/Initialize-Phase3.ps1
docker compose -p climate-phase3-local --env-file .env.phase3.local config --quiet
docker compose -p climate-phase3-local --env-file .env.phase3.local up -d --no-build --wait
docker compose -p climate-phase3-local --env-file .env.phase3.local logs --since 10m gateway report-service audit-service
docker compose -p climate-phase3-local --env-file .env.phase3.local stop
```

docker-compose.yml es ahora la arquitectura final completa. docker-compose.phase3.yml reúne la configuración fuente por overlays y docker-compose.legacy.yml conserva la anterior solo como referencia. Después de cambiar overlays ejecutar scripts/update-phase3-compose.ps1: renderiza referencias sin resolver secretos y valida el resultado. Se comprobó equivalencia de ambos conjuntos y17 servicios, sin duplicar proveedores.

Build manual: scripts/Build-Images.ps1 -Phase3 -IncludeFrontend. En redes con CA propia usar -NpmCaFile ../tools/trusted-roots.pem; no deshabilitar TLS. Node24.21 compatible con Angular22; Java21 se compila en Docker.

## Pruebas desechables

```powershell
./scripts/Test-Phase3System.ps1 -Reports -LargeReports
./scripts/Test-Phase3Compose.ps1 -Browser -Recovery
```

Ejecutar secuencialmente: los scripts de Auth/Alert/Audit/System comparten fixtures climate-phase3-auth-validation. Compose usa climate-phase3-final-validation y la restauración climate-phase3-restored-validation. Solo se eliminan los volúmenes de estos proyectos de prueba al reiniciarlos o limpiarlos; no los del usuario.

## Respaldo/restauración comprobados

Test-Phase3Recovery.ps1 crea datos sintéticos por API, detiene limpiamente todos los servicios y toma snapshots de los8 volúmenes: SQL Server, tres PostgreSQL, dos MongoDB, RabbitMQ y archivos PDF. Verifica SHA-256 y restaura en volúmenes nuevos de otro proyecto. Conserva propietarios/permisos Linux y comprueba healthchecks, login y datos por APIs, incluido PDF con el hash original. Evidencia sin secretos en validation/phase3-recovery.json. Las copias temporales se eliminan al terminar.

Este procedimiento es offline y se validó con las mismas versiones de imágenes; no certifica restauración entre versiones distintas de motores. Para respaldo operativo mantener juntos Mongo Report y report-files, proteger copias fuera del host y utilizar dumps propios de cada motor cuando se necesite portabilidad. No borrar volúmenes para detener la aplicación.

Conservar también la configuración privada de credenciales de forma protegida: restaurar bases existentes requiere sus mismas claves/contraseñas. No regenerar .env.phase3.local sobre un conjunto de bases restaurado. Initialize-Phase3.ps1 conserva un archivo ya existente.

## Preparación conservada fuera del despliegue local

VPS/DNS/HTTPS públicos están excluidos. La plantilla deploy/nginx.phase3.conf y documentos Hostinger permanecen como referencia sin activar. Kubernetes se entrega preparado y validado en k8s/FASE_3.md; no se despliega. OAuth externo pertenece a Fase4. No se necesitan estos pasos para usar el sistema local.
