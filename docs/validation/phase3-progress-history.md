# Registro histórico anterior al cierre definitivo

# Fase 3 — Punto de continuación

Fecha de inicio: 2026-10-21 (America/Mexico_City).
Solicitud: implementar todos los requisitos de `phase_3.md` y conservar progreso para evitar duplicados.

## Reglas para retomar

1. Leer este archivo y `backend/docs/FASE_3_MATRIZ_REQUISITOS.md` antes de modificar código.
2. Revisar git diff en ambos repositorios usando `git -c safe.directory=<ruta absoluta> -C <repo> status --short`.
3. Reutilizar los servicios y migraciones registrados aquí; no regenerarlos ni sobrescribir cambios del usuario.
4. Actualizar este punto después de cada bloque implementado o validado.
5. Un servicio escrito no equivale a un servicio probado ni a datos migrados. Registrar cada estado por separado.

## Estado actual

**Actualización de cierre solicitada por el usuario:** no se exige conservar/transferir datos anteriores ni desplegar a VPS. No borrar bases actuales. Exclusiones documentadas en backend/docs/FASE_3_FUERA_DE_ALCANCE.md. Se cerraron pruebas de5 000 lecturas y restauración de8 volúmenes en otro proyecto; se corrigió Compose predeterminado, se agregaron distribución por fenómeno/estadísticas del dashboard y cuota atómica de reportes. Pruebas finales de integración y reconstrucción de imágenes en curso; verificar sus resultados antes de declarar cierre final.

**Alcance actualizado por el usuario:** omitir los despliegues a VPS. No considerarlos pendientes para el cierre solicitado ni ejecutar despliegues remotos. El siguiente trabajo debe centrarse en verificar pendientes locales: respaldo/restauración, volumen de reportes, revisión final de requisitos y, solo si existen datos históricos que conservar, su migración local. Kubernetes/OIDC continúan como preparación de Fase4.

**CIERRE LOCAL — 2026-10-21.** Esta actualización sustituye los estados incrementales anteriores conservados debajo. Implementación A–J y pruebas locales concluidas; ningún proceso de trabajo sigue ejecutándose.

- Auth/Community/Alert/Audit/Report implementados e integrados, reutilizando Sensor/Monitoring/Event y Gateway existentes. Angular22.2.3 migrado oficialmente.
- Reportes: ocho PDF reales; estadísticas y ejecutivo separados; historial, roles/permisos, JWT inválido/expirado/issuer/audience, 261 lecturas paginadas, vacíos, expiración, broker/dependencia caídos y recuperación de trabajo tras reinicio APROBADOS con Test-Phase3System.ps1 -Reports.
- Diez imágenes climate/*:phase3 construidas. Test-Phase3Compose.ps1 y -Browser APROBADOS: 17 contenedores saludables, login real, Gateway, PDF, frontend, health agregado y navegador sobre pantallas anteriores; regla Java creada/desactivada, móvil/foco/modal, descarga PDF y 16 axe sin violaciones. Proyectos desechables detenidos.
- Regresión: .NET122 aprobadas+1 omitida; Java7; Auth5; Python11; Angular56/build/lint. UI de roles aislada adicional: tres roles y seis axe aprobados. Pkcs actualizado a10.0.13 y lockfiles renovados por vulnerabilidad detectada al construir.
- Infraestructura: docker-compose.phase3.yml reúne los overlays con los mismos nombres/proyecto, sin duplicar proveedores activos; frontend incluido. K8s actualizado:29 recursos+Secret validados con esquema oficial, PVC PDF privado y bases externas; preparado sin desplegar. Plantilla alternativa Nginx/HTTPS en backend/deploy/nginx.phase3.conf, requiere dominio/certificado real.
- Inventario y matriz actualizados; documentación completa en backend/docs/FASE_3_*.md. Informe principal: backend/docs/FASE_3_INFORME_FINAL.md.
- Pendientes externos: enlace Fase1 devuelve404; transferencia/restauración de datos reales y rollout VPS/HTTPS. K8s/OIDC preparados para Fase4. No se tocaron bases reales ni se declara producción migrada.

**Para retomar:** leer el informe final y FASE_3_MIGRACION_DATOS.md, seleccionar entorno y datos reales, preparar respaldo/transferencia antes de cambiar tráfico. Reutilizar servicios, imágenes, Compose y migradores. No regenerar código ni matriz inicial ni repetir trabajo local validado sin cambios que lo justifiquen.

### Registro incremental anterior (histórico)

- A: inventario generado en `backend/docs/validation/phase3-inventory.json`; diagnóstico en `backend/docs/FASE_3_ARQUITECTURA.md`. Falta fuente Fase 1 (404).
- B: contratos documentados; Gateway con JWT HS256, CorrelationId, JSON logs, rate limit configurable, rutas protegidas y readiness independiente de Audit. Compila sin warnings y pasó HTTP real con NestJS, JWT y CORS. Falta cerrar contratos del resto de servicios por etapa.
- C: Auth NestJS/PostgreSQL implementado y probado con servicios reales aislados. Falta desplegar sobre datos reales del usuario (no se han tocado) y probar imagen Docker/overlay con Gateway.
- D: Community .NET/PostgreSQL y adaptación Sensor implementados, compilados y probados contra bases reales aisladas. Migración sobre datos reales y rollout pendientes.
- E: Alert Spring Boot 3.5.16/Java21/PostgreSQL implementado y compilado; 7 pruebas Java aprobadas (5 dominio, 2 con PostgreSQL y seguridad real). Migración doble desde el esquema .NET real aprobada. Contenedor Java acepta JWT real NestJS y publica auditoría real RabbitMQ, continúa escribiendo con broker detenido y recupera Outbox. Falta circuito HTTP completo con Monitoring/Event/SignalR; ver siguiente paso.
- E.2: APROBADA integración real Sensor→Monitoring→Alert Java→Events/SignalR vía YARP, dashboard y replay de lectura. Se corrigió el dashboard para consultar Community como propietario.
- F: Audit FastAPI/MongoDB implementado. Siete pytest aprobados e imagen climate/audit:phase3 construida. Test-Phase3Audit.ps1 APROBADO: migración doble, duplicados, DLQ, consumidor detenido y recuperación de Mongo/Rabbit sin bloquear Auth.
- G: ReportService implementado con Mongo privado, trabajos persistentes, Jinja2/WeasyPrint y ocho tipos. Imagen climate/reports:phase3 construida. Test-Phase3System.ps1 -Reports APROBADO: ocho PDF reales, hash/tamaño, historial, roles, cero resultados y validaciones; falta ampliar volumen/fallos/recuperación. Endpoint statistics agregado después de esta prueba: pendiente validar.
- H: Angular22.2.3/TypeScript6 migrado mediante ng update oficial con schematics, Node24.21 tools/node y npm11.21.1 tools/npm-phase3. Build y 56 pruebas aprobadas; lint aprobado tras revisar componentes de signals y pasar a OnPush. Módulo reportes, navegación, dashboard y roles listos. scripts/test-reports-browser.mjs APROBADO: tres roles, generación/descarga, móvil, seis axe sin violaciones. Respuestas UI controladas; PDF real se valida aparte.
- I: docker-compose.phase3.yml reúne overlays, imágenes y frontend con mismos nombres de servicio/proyecto, sin duplicar proveedores activos. Diez imágenes climate/*:phase3 CONSTRUIDAS. K8s actualizado: 29 recursos y Secret validados contra esquema oficial; bases externas privadas, PVC PDF RWO, una réplica/recreate, preparación documentada en k8s/FASE_3.md. Test-Phase3Compose.ps1 EN EJECUCIÓN en proyecto desechable separado; consultar resultado.
- J: regresión posterior a Pkcs10.0.13 APROBADA (122 .NET +1 omitida), 11 pytest aprobados. Falta resultado Compose, prueba de seguridad JWT/reinicio agregada a Test-Phase3System.ps1 -Reports, actualización final de inventario/matriz/docs.
- Backend: `backend/`, repositorio Git independiente, inicialmente sin cambios.
- Frontend: `frontend/climate-monitoring-web/`, repositorio Git independiente.
- Frontend presenta previamente archivos de documentación eliminados/inaccesibles. Preservarlos; no atribuir esas eliminaciones a este trabajo.
- Los scripts `tools/phase3-*.py` y `tools/Migrate-Phase3.ps1` son previos y realizan cambios de Fase 2; NO volver a ejecutarlos por su nombre.

## Entorno comprobado

- .NET SDK 10.0.400; Node 22.13.0; Python 3.13.2; Java local 11.0.26; Maven no está en PATH.
- Docker Engine 28.5.1 accesible con ejecución elevada autorizada. No había contenedores activos al inicio. Hay imágenes SQL Server, RabbitMQ, MongoDB, PostgreSQL, Node, .NET y Maven/Java21 reutilizables.
- Git requiere safe.directory por propiedad del usuario; usar opción por comando, sin cambiar configuración global.
- Se descargó `phase_3.reference.md`, enunciado oficial, y se leyó íntegramente junto con el prompt local y `phase_2.reference.md`.
- URL indicada para Fase 1 devuelve HTTP 404. Buscar archivo oficial renombrado o referencia local; no inventar su contenido.

## Próximo paso

Consultar Test-Phase3Compose.ps1 (última sesión exec 86316) y frontend tests/lint (80589). Después ejecutar Test-Phase3System.ps1 -Reports una vez para las nuevas comprobaciones JWT/reinicio y corrección estadística. Actualizar inventario/docs/matriz. No regenerar servicios ni matriz inicial.

Nueva implementación: `src/Services/PythonCommon/climate_common/` comparte contratos HTTP/JWT, sin persistencia compartida; `src/Services/AuditServicePython/` tiene consumidor robusto, ACK tras Mongo durable, idempotencia EventId, DLQ de mensajes inválidos, API compatible y filtros/paginación. Overlay `docker-compose.phase3-audit.yml`. Transferencia offline `tools/Phase3Migration/audit.cjs` preserva historial y admite retomar importaciones parciales (Mongo standalone no ofrece transacción global de toda la transferencia).

Entorno Python local: `backend/.venv-phase3`, ignorado por Git, con PYTHONPATH apuntando a PythonCommon y al servicio correspondiente. Dependencias de Audit fijadas en requirements.txt. Herramientas Node de migración ahora incluyen mongodb (instalado, npm audit 0 vulnerabilidades).

`scripts/Test-Phase3Alerts.ps1` terminó aprobado: 7 JUnit + 1 smoke del contenedor Java con JWT emitido por el módulo real NestJS y RabbitMQ real, detenido y recuperado para comprobar Outbox. Las dependencias de Sensor/Event/Monitoring se sustituyen únicamente en las dos pruebas Java de persistencia; ese smoke prueba el transporte AMQP real pero no cierra todavía las notificaciones de fenómenos/SignalR. No volver a ejecutar pruebas ya aprobadas salvo nuevos cambios o incidencias.

## Implementación realizada

- `backend/src/Services/AuthService/`: NestJS, DTO/guards, usuarios/roles/permisos, login/logout, hashes Identity V2/V3, PostgreSQL, transacción de negocio+Outbox, publicador RabbitMQ confirmado, reintentos, health y graceful shutdown.
- `src/migrate.ts`: transferencia SQL Server→PostgreSQL en lotes, conserva IDs/hashes/fechas/estados/roles y Outbox pendiente. Valida campos y conteos, no sobreescribe colisiones, rollback e idempotencia. No ejecutar contra producción sin respaldo y ventana de migración.
- `backend/docker-compose.phase3-auth.yml`: overlay optativo que sustituye identity-service con NestJS; original sigue disponible como rollback. No se cambió el destino por defecto del Gateway a un servicio inexistente.
- `backend/tools/Phase3Interop/`: comprobación real de hashes y JWT entre Node y ASP.NET.
- `backend/scripts/Test-Phase3Auth.ps1`: prueba aislada, reiniciable; genera credenciales temporales, usa exclusivamente proyecto Docker climate-phase3-auth-validation, conserva fixtures detenidos al terminar. Al volver a ejecutarse reinicia SOLO sus volúmenes de prueba.
- `backend/src/Services/CommunityService/Climate.Communities.Api/`: CRUD y filtros, roles, PostgreSQL privado, esquema SQL idempotente versionado, Outbox transaccional reutilizando el productor .NET existente; APIs internas autenticadas sin consultas a SensorDb.
- `backend/tools/Phase3Migration/communities.cjs`: migración SQL Server SensorDb→PostgreSQL con UUID y campos preservados, validación de referencias de sensores y ejecución repetida. Herramienta offline separada de las dependencias del servicio.
- Sensor mantiene modo original por defecto; con CommunityService:Enabled=true consulta catálogo remoto, hidrata snapshots sin persistirlos, deshabilita rutas de escritura del propietario antiguo y seed local. Nueva migración `20261021153228_Phase3CommunityOwnership` solo retira FK, NO elimina tablas ni datos. No ejecutar Down tras crear comunidades remotas sin reconciliar el catálogo.
- `backend/docker-compose.phase3-community.yml`: overlay optativo para el nuevo propietario y ruta Gateway. Aplicar después de migrar, junto con Auth overlay.
- `backend/src/Services/AlertServiceJava/`: API Spring Boot, reglas y enums compatibles con Angular/.NET (SensorType acepta números del productor .NET y nombres del frontend), evaluación automática, estados y responsables, idempotencia por ReadingId, JDBC/PostgreSQL, Outbox para AUDIT/EVENT/REALTIME.
- `backend/tools/Phase3Migration/alerts.cjs`: conserva reglas, alertas, responsables, estados, UUID, snapshots e historial Outbox pendiente; enums SensorType normalizados a nombres. Transferencia transaccional en lotes, verifica conteos y referencias, no sobreescribe colisiones. Probada dos veces sobre esquema creado por migraciones reales del Alert .NET.
- `backend/docker-compose.phase3-alerts.yml`: sustitución optativa del servicio .NET; original sigue como valor por defecto hasta cerrar integración. No se arrancaron overlays sobre datos reales.

## Validaciones cerradas

- `npm.cmd test` en Auth: 5/5 aprobadas; compilación TypeScript estricta.
- `dotnet build backend/src/Gateway/Climate.Gateway/Climate.Gateway.csproj`: éxito sin warnings tras restauración fuera de sandbox. Build requiere Jwt:SigningKey al arrancar, configurado en Compose.
- `powershell -NoProfile -ExecutionPolicy Bypass -File backend/scripts/Test-Phase3Auth.ps1`: aprobada integración SQL Server/PostgreSQL/NestJS/RabbitMQ. Incluye migrar dos veces, colisión con rollback, conservar IDs/hashes, login usuarios existentes, desactivación, rechazo de elevación de roles, .NET valida token Node y hash Node, broker detenido sin fallo de negocio y recuperación tras reiniciar productor y broker.
- Pruebas .NET iniciales: siete proyectos sin fallos; RabbitMqIntegrationTests omitida por configuración. Ver registros adicionales en docs.
- npm encontró error TLS inicialmente; resuelto usando NODE_EXTRA_CA_CERTS=tools/trusted-roots.pem. No desactivar verificación TLS. Dependencias instaladas, package-lock generado. NuGet funcionó al ejecutar con permisos fuera del sandbox.
- Integración Auth+Gateway repetida: aprobada (JWT por YARP, rutas públicas/protegidas, CORS, un único CorrelationId, readiness independiente de Audit). Se detectó y corrigió duplicación de encabezado antes de aprobar.
- Docker: imágenes finales `climate/auth:phase3`, `climate/community:phase3` y `climate/alerts:phase3` construidas. Auth reconstruido tras actualizar mssql. Community tiene lock NuGet y restore --locked-mode. Spring Boot compilado con Java21 en Docker, sin cambiar Java11 del equipo.
- `npm audit` Auth y Phase3Migration: 0 vulnerabilidades tras actualizar mssql a 12.7.10.
- `dotnet test backend/tests/Climate.Sensors.Tests/Climate.Sensors.Tests.csproj --no-restore --verbosity minimal`: 20/20 tras adaptación Community; se corrigió una regresión de registro DI detectada por estas pruebas.
- `Test-Phase3Auth.ps1` versión actual: DOS integraciones aprobadas (Auth y Community). Community valida migración doble, IDs y referencias históricas, conteos por API, permisos, coordenadas, comunidad existente SOLO en PostgreSQL aceptada por Sensor, ninguna copia SQL, activación/desactivación, error 503 al caer Community y Outbox. Un fallo anterior de limpieza de procesos de la prueba fue corregido y la ejecución final pasó.
- Versión final de Test-Phase3Auth.ps1: TRES integraciones aprobadas (Auth, Community y migración Alert), fixtures detenidos al terminar.
- Regresión final .NET con TRX en `backend/TestResults/phase3`: 122 aprobadas, 0 fallidas, 1 omitida (RabbitMqIntegrationTests). El contador notExecuted de TRX es cero aunque total=123 y executed=122; no confundirlo con ausencia de omitidas.
- `Test-Phase3Alerts.ps1`: 7/7 JUnit aprobadas con PostgreSQL real. Adaptador saliente Delivery sustituido únicamente en esas pruebas para aislar seguridad, transacciones, umbrales, estados e idempotencia. Se corrigió acceso a campos de un proxy Spring detectado por la integración.
- Los tres overlays juntos validan con `docker compose ... config --quiet`; no se imprimieron secretos ni se inició el stack de la aplicación.
- Smoke Java real aprobado: `/health/ready`, 401 sin token, JWT NestJS aceptado, regla persistida, evento RabbitMQ PascalCase con SchemaVersion/CorrelationId, broker detenido sin error HTTP de negocio y Outbox confirmado al recuperar broker. El ejecutor Node requiere keepAlive mientras espera fetch durante arranque; su prueba usa node:test para no confundir promesas incompletas con éxito. Fixtures y contenedor runtime detenidos al cerrar.

## Solicitud sugerida para continuar

«Continúa desde FASE_3_PROGRESO.md para preparar el rollout de datos reales/VPS. A–J ya están implementadas y validadas localmente. Reutiliza servicios, imágenes, Compose y migradores; revisa respaldo y destino antes de cambiar tráfico.»
