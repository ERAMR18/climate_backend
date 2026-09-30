# Matriz final de cumplimiento — Fase 2

Fuente: [referencia local de phase_2.md](../../phase_2.reference.md), usada en las entregas anteriores. La evaluación se refiere al código y las pruebas locales de esta entrega, no a un despliegue de producción.

## Criterio y evidencias

`COMPLETED` indica funcionalidad implementada con evidencia ejecutada de API, dominio, integración o navegador. No significa que cada combinación de datos o cada formulario haya sido probado de extremo a extremo. `PARTIAL` identifica una limitación concreta pendiente. La infraestructura retirada se marca explícitamente.

- **F1:** [prueba funcional](../scripts/Test-Phase2Functional.ps1): SQL, JWT, catálogos, filtros, reglas, workflow, dashboard y WebSocket reales.
- **F2:** [recuperación](../scripts/Test-RecoverySteps.ps1), ejecutada mediante [Test-Images](../scripts/Test-Images.ps1): UpdateSensor con Audit detenido, SQL, broker persistente, dos consumidores y consultas de auditoría.
- **U-Sensors / U-Monitoring / U-Alerts / U-Events / U-Identity / U-Audit:** suites respectivas de [tests backend](../tests), ejecutadas con [RabbitMQ temporal](../scripts/Test-AuditMessaging.ps1).
- **T:** [pruebas de contratos Angular](../../frontend/climate-monitoring-web/src/app/shared/testing), auth/interceptores y DashboardStore.
- **B:** [navegador real](../../frontend/climate-monitoring-web/scripts/test-browser.mjs): rutas, login/logout, reglas, diálogos, móvil y axe. Su cobertura exacta figura en [validación](phase-2-validation.md).
- **K:** [validación de manifiestos](../scripts/validate-manifests.py), esquema oficial y restricciones de configuración.

## Requisitos funcionales

| Requirement | Backend | Frontend | Status | Evidence |
|---|---|---|---|---|
| RF-ADM-01 — Inicio de sesión | Identity + JWT en APIs | Login, AuthStore, guards/interceptor | COMPLETED | F1, T, B: Login real devuelve JWT y usuario. |
| RF-ADM-02 — Protección del panel | Identity + JWT en APIs | Login, AuthStore, guards/interceptor | COMPLETED | F1, T, B: Ruta protegida redirige a login; API anónima devuelve 401. |
| RF-ADM-03 — Cierre de sesión | Identity + JWT en APIs | Login, AuthStore, guards/interceptor | COMPLETED | F1, T, B: Logout HTTP auditado y limpieza de sesión en navegador. |
| RF-ADM-04 — Expiración | Identity + JWT en APIs | Login, AuthStore, guards/interceptor | COMPLETED | F1, T, B: Token expirado firmado rechazado; interceptor y estado de sesión probados. |
| RF-ADM-05 — JWT | Identity + JWT en APIs | Login, AuthStore, guards/interceptor | COMPLETED | F1, T, B: JWT real en REST y negociación SignalR. |
| RF-ADM-06 — Roles | Identity + JWT en APIs | Login, AuthStore, guards/interceptor | COMPLETED | F1, T, B: Administrator/Operator/Viewer; edición de rol y rechazo Viewer. |
| RF-ADM-07 — Autorización | Identity + JWT en APIs | Login, AuthStore, guards/interceptor | COMPLETED | F1, T, B: Viewer recibe 403 en reglas, alertas y simulación. |
| RF-ADM-08 — Crear comunidades | Sensor: comunidades y consultas SQL | Comunidades: lista, formulario y detalle | COMPLETED | F1, U-Sensors, B: Crear comunidad con geografía en SQL. |
| RF-ADM-09 — Editar comunidades | Sensor: comunidades y consultas SQL | Comunidades: lista, formulario y detalle | COMPLETED | F1, U-Sensors, B: Editar descripción y recuperar el cambio. |
| RF-ADM-10 — Activar/desactivar | Sensor: comunidades y consultas SQL | Comunidades: lista, formulario y detalle | COMPLETED | F1, U-Sensors, B: Desactivar/activar y consultar estado. |
| RF-ADM-11 — Listado | Sensor: comunidades y consultas SQL | Comunidades: lista, formulario y detalle | COMPLETED | F1, U-Sensors, B: Listado real en API y navegador. |
| RF-ADM-12 — Búsqueda y filtrado | Sensor: comunidades y consultas SQL | Comunidades: lista, formulario y detalle | COMPLETED | F1, U-Sensors, B: Búsqueda, estado, municipio y departamento combinados. |
| RF-ADM-13 — Sensores por comunidad | Sensor: comunidades y consultas SQL | Comunidades: lista, formulario y detalle | COMPLETED | F1, U-Sensors, B: Comunidad devuelve sensorCount=1. |
| RF-ADM-14 — Dashboard por comunidad | Monitoring: resumen por comunidad | Selector y DashboardStore | COMPLETED | F1, T, B: Resumen limitado a la comunidad y filtrado de eventos realtime. |
| RF-ADM-15 — Crear sensores | Sensor + Monitoring + outbox | Sensores: lista, formulario y detalle | COMPLETED | F1, F2, U-Sensors: Crear sensor con tipo, ubicación y comunidad. |
| RF-ADM-16 — Editar sensores | Sensor + Monitoring + outbox | Sensores: lista, formulario y detalle | COMPLETED | F1, F2, U-Sensors: UpdateSensor exitoso con Audit detenido y dato comprobado en SensorDb. |
| RF-ADM-17 — Activar/desactivar sensores | Sensor + Monitoring + outbox | Sensores: lista, formulario y detalle | COMPLETED | F1, F2, U-Sensors: Cambio de estado y conteos SQL/dashboard. |
| RF-ADM-18 — Sensores desactivados | Sensor + Monitoring + outbox | Sensores: lista, formulario y detalle | COMPLETED | F1, F2, U-Sensors: Lecturas y overrides rechazados para sensor inactivo; evaluación no crea alertas. |
| RF-ADM-19 — Sensores por comunidad | Sensor + Monitoring + outbox | Sensores: lista, formulario y detalle | COMPLETED | F1, F2, U-Sensors: Consulta por communityId. |
| RF-ADM-20 — Búsqueda y filtros | Sensor + Monitoring + outbox | Sensores: lista, formulario y detalle | COMPLETED | F1, F2, U-Sensors: Filtros de comunidad/tipo/estado/código combinados. |
| RF-ADM-21 — Simulación | Sensor + Monitoring + outbox | Sensores: lista, formulario y detalle | COMPLETED | F1, F2, U-Sensors: Valor fijo persistido y utilizado por simulador. |
| RF-ADM-22 — Auditoría | Sensor + Monitoring + outbox | Sensores: lista, formulario y detalle | COMPLETED | F1, F2, U-Sensors: Evento Update recuperado en AuditDb tras reiniciar consumidor. |
| RF-ADM-23 — Almacenamiento | Monitoring: lecturas persistidas | Historial y gráficos | COMPLETED | F1, U-Monitoring, T: Lecturas reales persistidas y consultadas. |
| RF-ADM-24 — Información de lectura | Monitoring: lecturas persistidas | Historial y gráficos | COMPLETED | F1, U-Monitoring, T: Sensor, fecha/hora, valor, unidad y snapshot sensorWasActive. |
| RF-ADM-25 — Historial | Monitoring: lecturas persistidas | Historial y gráficos | COMPLETED | F1, U-Monitoring, T: Historial del sensor devuelve las lecturas creadas. |
| RF-ADM-26 — Filtro por fechas | Monitoring: lecturas persistidas | Historial y gráficos | COMPLETED | F1, U-Monitoring, T: Rango temporal en historial y consulta paginada. |
| RF-ADM-27 — Lecturas por comunidad | Monitoring: lecturas persistidas | Historial y gráficos | COMPLETED | F1, U-Monitoring, T: Historial y listado global por comunidad. |
| RF-ADM-28 — Dashboard | Monitoring: lecturas persistidas | Historial y gráficos | COMPLETED | F1, U-Monitoring, T: Evolución SQL no vacía después de registrar lecturas. |
| RF-ADM-29 — Crear reglas | Alerts: reglas y evaluación SQL | Reglas y snapshot de alerta | COMPLETED | F1, U-Alerts, T, B: Creación por API y por formulario real. |
| RF-ADM-30 — Parámetros | Alerts: reglas y evaluación SQL | Reglas y snapshot de alerta | COMPLETED | F1, U-Alerts, T, B: Contrato de límites, tipo, nivel, fenómeno, mensaje y estado. |
| RF-ADM-31 — Niveles | Alerts: reglas y evaluación SQL | Reglas y snapshot de alerta | COMPLETED | F1, U-Alerts, T, B: Casos de clasificación y enum Green/Yellow/Orange/Red. |
| RF-ADM-32 — Evaluación automática | Alerts: reglas y evaluación SQL | Reglas y snapshot de alerta | COMPLETED | F1, U-Alerts, T, B: Reglas SQL afectan la siguiente evaluación. |
| RF-ADM-33 — Generación automática | Alerts: reglas y evaluación SQL | Reglas y snapshot de alerta | COMPLETED | F1, U-Alerts, T, B: Lectura 75 sobre máximo 50 genera alerta. |
| RF-ADM-34 — Información de alerta | Alerts: reglas y evaluación SQL | Reglas y snapshot de alerta | COMPLETED | F1, U-Alerts, T, B: Snapshot sensor/comunidad/regla/valor/nivel/fenómeno. |
| RF-ADM-35 — Administración | Alerts: reglas y evaluación SQL | Reglas y snapshot de alerta | COMPLETED | F1, U-Alerts, T, B: Edición API y activación/desactivación; confirmación UI. |
| RF-ADM-36 — Trazabilidad | Alerts: reglas y evaluación SQL | Reglas y snapshot de alerta | COMPLETED | F1, U-Alerts, T, B: ruleId y snapshot conservados en detalle. |
| RF-ADM-37 — Listado | Alerts: consulta y transiciones | Alertas, detalle y dashboard | COMPLETED | F1, U-Alerts, T, B: Consulta de alerta generada y página de listado. |
| RF-ADM-38 — Información | Alerts: consulta y transiciones | Alertas, detalle y dashboard | COMPLETED | F1, U-Alerts, T, B: Detalle contiene snapshot y workflow. |
| RF-ADM-39 — Filtros | Alerts: consulta y transiciones | Alertas, detalle y dashboard | COMPLETED | F1, U-Alerts, T, B: Fechas/comunidad/sensor/fenómeno/nivel/estado combinados. |
| RF-ADM-40 — Detalle | Alerts: consulta y transiciones | Alertas, detalle y dashboard | COMPLETED | F1, U-Alerts, T, B: Detalle GET con JWT. |
| RF-ADM-41 — Estados | Alerts: consulta y transiciones | Alertas, detalle y dashboard | COMPLETED | F1, U-Alerts, T, B: Activa→Atendida→Cerrada y 409 en transiciones inválidas. |
| RF-ADM-42 — Responsable | Alerts: consulta y transiciones | Alertas, detalle y dashboard | COMPLETED | F1, U-Alerts, T, B: attendedByUserId/closedByUserId coinciden con actor. |
| RF-ADM-43 — Dashboard | Alerts: consulta y transiciones | Alertas, detalle y dashboard | COMPLETED | F1, U-Alerts, T, B: Dashboard devuelve una alerta activa antes de cerrarla. |
| RF-ADM-44 — Registro | Events: historial y estadísticas | Eventos, filtros y estadísticas | COMPLETED | F1, U-Events, T, B: Evento creado desde evaluación real. |
| RF-ADM-45 — Información | Events: historial y estadísticas | Eventos, filtros y estadísticas | COMPLETED | F1, U-Events, T, B: Valor 75, Closed y usuario responsable propagados. |
| RF-ADM-46 — Consulta | Events: historial y estadísticas | Eventos, filtros y estadísticas | COMPLETED | F1, U-Events, T, B: Historial autenticado real. |
| RF-ADM-47 — Filtros | Events: historial y estadísticas | Eventos, filtros y estadísticas | COMPLETED | F1, U-Events, T, B: Fechas/comunidad/fenómeno/nivel; otro fenómeno devuelve vacío. |
| RF-ADM-48 — Estadísticas | Events: historial y estadísticas | Eventos, filtros y estadísticas | COMPLETED | F1, U-Events, T, B: Estadísticas Closed=1 y agrupaciones con fechas inclusivas. |
| RF-ADM-49 — Crear usuarios | Identity: usuarios y roles | Usuarios, formularios y último acceso | COMPLETED | F1, U-Identity, T, B: Crear Viewer mediante endpoint administrativo. |
| RF-ADM-50 — Editar usuarios | Identity: usuarios y roles | Usuarios, formularios y último acceso | COMPLETED | F1, U-Identity, T, B: Editar username/email y recuperar respuesta. |
| RF-ADM-51 — Activar/desactivar | Identity: usuarios y roles | Usuarios, formularios y último acceso | COMPLETED | F1, U-Identity, T, B: Desactivar impide login con 403; reactivar disponible. |
| RF-ADM-52 — Roles | Identity: usuarios y roles | Usuarios, formularios y último acceso | COMPLETED | F1, U-Identity, T, B: Asignación a Operator por administrador. |
| RF-ADM-53 — Listado | Identity: usuarios y roles | Usuarios, formularios y último acceso | COMPLETED | F1, U-Identity, T, B: Listado real y navegación de usuarios. |
| RF-ADM-54 — Filtros | Identity: usuarios y roles | Usuarios, formularios y último acceso | COMPLETED | F1, U-Identity, T, B: search/role/isActive aplicados conjuntamente. |
| RF-ADM-55 — Información adicional | Identity: usuarios y roles | Usuarios, formularios y último acceso | COMPLETED | F1, U-Identity, T, B: createdAt/lastLoginAt/role/estado; último acceso visible en listado. |
| RF-ADM-56 — Registro | Outbox + RabbitMQ + Audit | Bitácora, filtros y detalle | COMPLETED | F1, F2, U-Audit, T, B: Acciones CRUD/login/logout y seis acciones reglas/workflow en outbox. |
| RF-ADM-57 — Información | Outbox + RabbitMQ + Audit | Bitácora, filtros y detalle | COMPLETED | F1, F2, U-Audit, T, B: Detalle real contiene actor, acción, timestamp, entidad, ID y descripción. |
| RF-ADM-58 — Consulta | Outbox + RabbitMQ + Audit | Bitácora, filtros y detalle | COMPLETED | F1, F2, U-Audit, T, B: Consulta por Gateway con Administrator; política de rol en Audit. |
| RF-ADM-59 — Filtros | Outbox + RabbitMQ + Audit | Bitácora, filtros y detalle | COMPLETED | F1, F2, U-Audit, T, B: Filtros userId/action/resource/from/to devuelven solo el cambio de sensor. |
| RF-ADM-60 — ** Mostrar cantidad de comunidades | Monitoring: DashboardSummary | Dashboard + SignalR | COMPLETED | F1, T, B: communityCount=1 en resumen real. |
| RF-ADM-61 — ** Mostrar cantidad de sensores activos | Monitoring: DashboardSummary | Dashboard + SignalR | COMPLETED | F1, T, B: activeSensorCount=1 y luego 0. |
| RF-ADM-62 — ** Mostrar cantidad de sensores inactivos | Monitoring: DashboardSummary | Dashboard + SignalR | COMPLETED | F1, T, B: inactiveSensorCount=1 tras desactivar. |
| RF-ADM-63 — ** Mostrar alertas activas | Monitoring: DashboardSummary | Dashboard + SignalR | COMPLETED | F1, T, B: activeAlertCount=1 antes del cierre. |
| RF-ADM-64 — ** Mostrar distribución de alertas por nivel | Monitoring: DashboardSummary | Dashboard + SignalR | COMPLETED | F1, T, B: byAlertLevel.Red=1. |
| RF-ADM-65 — ** Mostrar evolución de lecturas | Monitoring: DashboardSummary | Dashboard + SignalR | COMPLETED | F1, T, B: Puntos SQL por tipo/unidad y Chart.js. |
| RF-ADM-66 — ** Permitir filtrar por comunidad | Monitoring: DashboardSummary | Dashboard + SignalR | COMPLETED | F1, T, B: Selector y consulta limitada a comunidad; realtime ignora otra comunidad. |
| RF-ADM-67 — ** Mostrar eventos climáticos | Monitoring: DashboardSummary | Dashboard + SignalR | COMPLETED | F1, T, B: events.total=1 y acceso al historial. |
| RF-ADM-68 — ** Actualizar información dinámicamente | Monitoring: DashboardSummary | Dashboard + SignalR | COMPLETED | F1, T, B: Seis eventos por WebSocket y actualización bufferizada del store. |

## Requisitos no funcionales

| Requirement | Backend | Frontend | Status | Evidence |
|---|---|---|---|---|
| RNF-ADM-01 — ** Las contraseñas deberán almacenarse mediante un algoritmo seguro de hashing | PasswordHasher de ASP.NET Identity | No persiste contraseñas | COMPLETED | F1: hash en SQL distinto de contraseña; login correcto/incorrecto; U-Identity. |
| RNF-ADM-02 — ** La API deberá estar protegida mediante JWT | JWT validado por cada API | Interceptor dirigido al Gateway | COMPLETED | F1: autenticación real y rechazo anónimo; T. |
| RNF-ADM-03 — ** Los endpoints administrativos deberán requerir autenticación | Authorize en operaciones administrativas | Guards de rutas | COMPLETED | F1: 401 sin token; B: redirección de /users a login. |
| RNF-ADM-04 — ** Los endpoints deberán validar roles y permisos | Policies/roles en APIs | Guards y acciones por rol | COMPLETED | F1: Viewer recibe 403; U-Alerts comprueba escritura administrativa; T. |
| RNF-ADM-05 — ** Los JWT deberán tener expiración configurable | Jwt:ExpirationMinutes configurable | expiresAt de respuesta | COMPLETED | F1: token real y fecha de expiración; JwtTokenGenerator consume opciones de configuración. |
| RNF-ADM-06 — ** Los tokens expirados deberán ser rechazados | ValidateLifetime en JWT | Sesión y manejo de 401 | COMPLETED | F1: JWT firmado con exp pasado devuelve 401; T. |
| RNF-ADM-07 — ** Las credenciales no deberán almacenarse en el código fuente | Variables/Secrets; ejemplos ficticios | Runtime config solo contiene URL pública | COMPLETED | K: sin secretos en ConfigMaps; exclusión Git y .dockerignore; imágenes no requieren credenciales al construir. |
| RNF-ADM-08 — ** Las operaciones administrativas deberán registrarse en la bitácora | Outbox transaccional por productor | Consulta administrativa de bitácora | COMPLETED | F1: seis acciones distintas en outbox; F2: Update llega a AuditDb; U-Sensors: commit/rollback conjunto. |

## Infraestructura y diferencias de la referencia

| Requirement | Backend | Frontend | Status | Evidence |
|---|---|---|---|---|
| Secciones 15–17: VPS, dominio, HTTPS, Certbot y Nginx global | Excluidos por el prompt actual | Excluidos | NOT APPLICABLE — OUT OF CURRENT SCOPE | Secciones 3 y 60 del prompt; no se añadieron configuraciones de servidor público. |
| Kubernetes del alcance nuevo | 24 recursos y SQL externo | Deployment/Service y runtime config | PARTIAL | K aprobado; contenedores reales probados. Falta ejecutar en clúster y verificar PVC/CNI/conexión SQL desde Pods. |
| Angular 22+ de la sección de tecnologías original | No aplica | Se conserva Angular 21 existente | PARTIAL | No se realizó migración mayor de framework; las funcionalidades RF fueron integradas y verificadas sobre la aplicación existente. |
| Imágenes coherentes y configurables | Build-Images acepta ImagePrefix/Tag | Imagen multi-stage y servidor estático | COMPLETED | Ocho imágenes verificadas; selección de registro documentada en kubernetes.md. |
| Persistencia SQL y desacoplamiento | Seis bases SQL, outbox y RabbitMQ | Solo Gateway | COMPLETED | F1/F2: cambios SQL, recuperación, replay y conexiones frontend verificadas. |

## Resumen

68 RF y 8 RNF tienen evidencia local. Las dos limitaciones adicionales son la validación Kubernetes real y la diferencia de versión mayor Angular respecto de la referencia. No se presenta esta entrega como un despliegue de producción ni como una auditoría exhaustiva de seguridad/accesibilidad.
