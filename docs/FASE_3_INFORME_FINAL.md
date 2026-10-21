# Fase 3 — Informe de implementación local

Fecha: 2026-10-21. Implementación y validación local concluidas. Por decisión del usuario, históricos y VPS quedan fuera del alcance; no bloquean el cierre funcional. El punto exacto de continuación se conserva en ../../FASE_3_PROGRESO.md.

1. **Diagnóstico inicial.** Se encontraron seis dominios .NET/SQL Server, Gateway YARP, RabbitMQ/Outbox y Angular21; comunidades compartían propiedad con Sensor y no existía reportería. Inventario real actualizado en validation/phase3-inventory.json. Fase2 local y enunciado Fase3 revisados; el enlace indicado de Fase1 devuelve404, por lo que no se certifica su lectura. Se preservó documentación frontend previamente inaccesible/eliminada.

2. **Arquitectura implementada.** Angular22 → Gateway → APIs propietarias. Auth, Community y Alert usan PostgreSQL separados; Audit y Report usan Mongo separados; Sensor, Monitoring y Event conservan SQL. Report consulta APIs, genera HTML Jinja2/WeasyPrint y conserva PDF privado. Auditoría asíncrona por Outbox/RabbitMQ sin bloquear negocio por caída del consumidor/broker. Diagrama y decisiones en FASE_3_ARQUITECTURA.md.

3. **Servicios creados/migrados.** Auth NestJS sustituye Identity .NET; Community .NET/PostgreSQL asume catálogo; Alert SpringBoot3.5/Java21 sustituye Alert .NET; Audit FastAPI sustituye Audit .NET; Report FastAPI es nuevo. Se conservan fuentes anteriores para migración/rollback y sus pruebas, sin ejecutarlas como proveedores duplicados en el Compose final. Sensor/Monitoring/Event se adaptaron mediante contratos HTTP y paginación.

4. **Archivos backend.** src/Services/AuthService, CommunityService, AlertServiceJava, AuditServicePython, ReportService y PythonCommon; cambios en Gateway, Sensor, Monitoring, Event; migración EF de propiedad Community; herramientas tools/Phase3Migration y Phase3Interop; scripts/Test-Phase3*.ps1; docker-compose.phase3*.yml; k8s; Swagger público docs/openapi/climate-api-v1.json. Dependencia criptográfica transitoria vulnerable fijada a Pkcs10.0.13 y lockfiles renovados sin desactivar auditoría.

5. **Archivos frontend.** Migraciones oficiales Angular22.2.3/TypeScript6.0.3, package.json/lock, configuración HTTP/TS y OnPush revisado; features/reports con API tipada, formulario, historial paginado, progreso, detalle y descarga; navegación y estadísticas de dashboard; paginación de Audit para conservar acceso a todo el historial. Playwright real y prueba aislada de roles/accesibilidad en scripts/.

6. **Datos/migraciones.** Se preservan UUID, hashes Identity V2/V3, usuarios/roles, fechas/estados, reglas/alertas/responsables, referencias de sensores y auditoría histórica. Transferencias SQL→PG/Mongo verifican campos/conteos/fingerprint y repetición; colisiones no se sobrescriben. Sensor retira solo FK de comunidades y conserva tablas históricas. Pruebas realizadas con datos sintéticos y esquemas .NET reales; ninguna transferencia en bases del usuario. Procedimiento y rollback en FASE_3_MIGRACION_DATOS.md.

7. **Endpoints.** Se conservan contratos públicos de auth/users/sensors/communities/monitoring/alerts/alert-rules/events/audit mediante YARP. Nuevos `/api/reports/types`, `/generate`, historial base, `/statistics`, `/{uuid}` y `/{uuid}/download`. Equivalentes internos /api/v1/reports. Auth emite identidad fija Viewer para reportes por endpoint interno protegido, no publicado por Gateway. Sensor/Community/Event/Readings admiten páginas para consultas de reportes. Swagger actualizado sin publicar endpoints privados.

8. **Reportes.** COMMUNITIES, SENSORS, READINGS, ALERTS, EVENTS, SENSOR_STATUS, CLIMATE_STATISTICS y EXECUTIVE, todos generados realmente en PDF. Historial/descargas auditadas; Administrator genera/lee todos/descarga, Operator genera/lee propios, Viewer lee propios. JWT y permisos verificados en Report, no solo en UI. Trabajos persistentes, leases/reintentos, límites por filas/tamaño/tiempo, almacenamiento UUID privado y retención. Detalle en FASE_3_REPORTERIA.md.

9. **Docker.** Diez imágenes climate/*:phase3 construidas. docker-compose.yml ejecuta la arquitectura final y docker-compose.phase3.yml conserva sus fuentes; bases privadas y volúmenes conservados. Test-Phase3Compose.ps1 levantó los 17 contenedores de un proyecto aislado, todos saludables, con login real, PDF, frontend y health agregado; quedaron detenidos al terminar. No se arrancó el proyecto de datos reales.

10. **Kubernetes/OAuth.** Manifiestos actualizados y 29 recursos+Secret validados con esquema oficial; PostgreSQL/Mongo/SQL externos privados, PVC PDF5Gi, Recreate/RWO/una réplica, probes/recursos/secretos. No desplegado en clúster por alcance. Contrato de proveedor externo y tabla external_identities preparados; login externo y OIDC/JWKS requieren Fase4 y configuración de proveedor. HS256 se conserva para interoperabilidad. SignalR necesita backplane/afinidad y simulación liderazgo antes de escalar Monitoring.

11. **Pruebas.** .NET122 aprobadas+1 omitida; Java7 con PostgreSQL; Auth5; Python11; Angular56, build/lint; migraciones dobles reales en fixtures y colisiones; fallos Rabbit/Mongo sin bloquear Auth; integración Sensor→Monitoring→Java→Event/SignalR por YARP; ocho PDF con hash/tamaño/MIME; paginación5 000 lecturas, cero resultados, expiración, roles/JWT/permisos, caída dependencia y recuperación de lease al reiniciar. UI aislada: tres roles y seis axe sin violaciones. UI contra Compose real: login/logout, pantallas anteriores, creación/desactivación de regla Java, foco/modal/móvil y generación/descarga PDF; 16 axe sin violaciones. Comandos/evidencia en FASE_3_PRUEBAS.md.

12. **Estados de requisitos.** Implementación local validada por etapa y requisito en FASE_3_MATRIZ_REQUISITOS.md. Preparación K8s/OIDC entregada, despliegue externo no ejecutado. Migración/rollback validados en fixtures, históricos reales excluidos; restauración local de8 volúmenes comprobada. Fuente Fase1 no disponible en el enlace indicado. No se declara producción migrada ni HTTPS remoto verificado.


13. **Ejecución local.** Desde backend: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start-Phase3.ps1 -NoBuild`; omitir -NoBuild para construir. El script genera .env.phase3.local si falta y usa proyecto nuevo climate-phase3-local. Usuario admin, contraseña en AUTH_ADMIN_SEED_PASSWORD del archivo privado. Frontend localhost:4200 y Gateway localhost:8080. No se importan ni borran datos anteriores.

14. **Alcance cerrado y límites.** No quedan pendientes funcionales identificados en el alcance local acordado. VPS, conservación de históricos, videos/publicaciones y lectura de la fuente externa Fase1 no disponible se registran en FASE_3_FUERA_DE_ALCANCE.md. K8s/OIDC se entregan preparados para Fase4. No se promete rendimiento ilimitado ni portabilidad de snapshots entre versiones diferentes. Lluvia acumulada depende de semántica incremental confirmada; no se inventan datos.

## Verificaciones adicionales del cierre

- Demostración restaurada: tres comunidades, 15 sensores, 27 reglas y lecturas iniciales mediante APIs actuales. Simulación activa y crecimiento de 45 a 60 lecturas verificado; repetir el seed no duplica catálogos. El arranque local carga esta demostración salvo `PHASE3_DEMO_ENABLED=false`. Guía: FASE_3_DATOS_DEMO.md; evidencia: validation/phase3-demo.json.

- Instalación nueva ejecutándose en localhost:4200, con17 contenedores saludables, login admin y ocho volúmenes de datos aislados verificados. La contraseña está en AUTH_ADMIN_SEED_PASSWORD de backend/.env.phase3.local, no en documentación ni logs. Ese archivo está ignorado por Git. Evidencia: validation/phase3-local-runtime.json.

- Dashboard: distribución por fenómeno usando Event Statistics y estadísticas SQL24h por tipo/unidad con cantidad/promedio/mínimo/máximo, filtradas por comunidad. Prueba real verifica valores.
- Reportes: filtros pertinentes en UI y consultas reales por sensor/comunidad/tipo/fecha. Error de historial se limpia al recuperar conexión; loading y vacío se distinguen.
- Concurrencia: índice Mongo único por usuario/cupo activo;20 solicitudes simultáneas aceptan3 y rechazan17 con429. Los cupos se liberan en éxito, fallo y agotamiento de intentos.
- Volumen:5 000 lecturas,20 páginas API, PDF de1061586 bytes en 38.312 segundos; verificado SHA-256 y rechazo explícito del límite.
- Recuperación: snapshots offline de8 volúmenes restaurados en otro proyecto, todos los dominios e historial y PDF comprobados. Artefacto validation/phase3-recovery.json.
- Accesibilidad ampliada a tags WCAG2.2 AA: seis escaneos aislados y16 sobre Compose real sin violaciones detectadas.
- Compose predeterminado completo, validado y equivalente al conjunto fuente de overlays; referencia anterior preservada, sin proveedores duplicados activos.
