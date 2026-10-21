# Fase 3 — Validaciones

## Cierre definitivo del alcance acordado

Arranque local final APROBADO con scripts/Start-Phase3.ps1 -NoBuild -Recreate:17 contenedores saludables, login real, catálogo de8 reportes, frontend200, health agregado Healthy y dashboard nuevo sin comunidades/sensores. Aislamiento de los8 volúmenes de datos comprobado; evidence validation/phase3-local-runtime.json. Initialize-Phase3.ps1 repetido conserva exactamente el archivo de credenciales existente.

Se corrigió un problema detectado al renderizar Compose: config sintetiza nombres de red/volúmenes que fijaban el proyecto anterior. update-phase3-compose.ps1 ahora elimina esos nombres y valida con otro nombre de proyecto que todos los recursos estén aislados. Los contenedores creados antes de corregirlo se recrearon sobre su red nueva. No se borraron volúmenes anteriores. El sistema final usa exclusivamente los volúmenes climate-phase3-local_* para sus datos.

VPS e importación de históricos no son pendientes: excluidos por el usuario. Ver FASE_3_FUERA_DE_ALCANCE.md.

| Verificación adicional ejecutada | Resultado |
| --- | --- |
| Test-Phase3System.ps1 -Reports -LargeReports | APROBADO: ocho PDF, filtros sensor/comunidad/tipo/fechas, estadísticas SQL dashboard,20 solicitudes simultáneas (3 aceptadas/17 rechazadas429), recuperación/reinicio y fallos previos |
| Reporte de5 000 lecturas | APROBADO:20 páginas de API, 38.312s, 1061586 bytes, hash/MIME/descarga correctos, rechazo explícito al superar límite |
| Test-Phase3Compose.ps1 -Browser -Recovery | APROBADO:17 contenedores saludables;8 volúmenes respaldados/restaurados en otro proyecto, login y todos los dominios y PDF verificados |
| Navegador final | Filtros pertinentes, validación de fechas, error503/recuperación, tres roles, seis axe aislados y16 reales con WCAG2.2 AA |
| Regresión después de cambios | .NET122 aprobadas+1 omitida; Python11; Angular56, build y lint aprobados |
| Compose predeterminado y fuente overlays |17 servicios equivalentes, config --quiet aprobado; secretos no mostrados |

Evidencia reproducible en validation/phase3-large-report.json, phase3-report-integration.json y phase3-recovery.json. El resto del documento conserva las pruebas incrementales anteriores; sus pendientes remotos quedan sustituidos por el alcance vigente.


## Cierre de implementación local (2026-10-21)

| Validación | Resultado actual |
| --- | --- |
| `scripts/Test-Phase3Audit.ps1` | APROBADO: migración doble, ACK Mongo durable, EventId duplicado, DLQ, consumidor detenido, fallos Mongo/Rabbit y negocio Auth independiente |
| `scripts/Test-Phase3System.ps1 -Reports` | APROBADO: circuito Sensor/Monitoring/Java/Events/SignalR/YARP, ocho PDF reales, 261 lecturas, hash/tamaño/MIME, roles y permisos explícitos, JWT directo inválido/expirado/issuer/audience, vacíos, expiración, Rabbit detenido y recuperación, fallo de dependencia, lease recuperado tras reiniciar worker |
| `scripts/Build-Images.ps1 -Phase3 -IncludeFrontend -NpmCaFile ../tools/trusted-roots.pem` | Diez imágenes finales construidas; Gateway/frontend reconstruidos al agregar Swagger y paginación Audit |
| `scripts/Test-Phase3Compose.ps1` | APROBADO: 17 contenedores saludables, login NestJS real, catálogo8, PDF, frontend y health agregado; proyecto separado climate-phase3-final-validation |
| `dotnet test ClimateMonitoringSystem.sln --no-restore --verbosity quiet` | 122 aprobadas, 1 omitida, 0 fallos después de actualizar Pkcs10.0.13 |
| `pytest src/Services/ReportService/tests src/Services/AuditServicePython/tests -q -p no:cacheprovider` | 11 aprobadas con PYTHONPATH PythonCommon+ambos servicios |
| Angular `ng update @angular/core@22 @angular/cli@22 angular-eslint@22 --allow-dirty` | Migraciones oficiales ejecutadas; Angular22.2.3, TypeScript6.0.3, RxJS7.8; Node24.21 y npm11.21.1 |
| Frontend `ng build`, `ng test --watch=false`, `ng lint` | Build producción aprobado; 56/56 pruebas y lint sin errores |
| Frontend `node scripts/test-reports-browser.mjs` | APROBADO: tres roles, generación/descarga, móvil/escritorio, seis axe sin violaciones; API controlada para aislar interfaz |
| `python scripts/validate-manifests.py --schema ../tools/kubernetes-1.34.1-openapi.json` | 29 recursos y Secret de ejemplo validados contra esquema oficial, DNS, probes, referencias y ClusterIP |
| `git diff --check` | Sin errores de whitespace en cambios accesibles; documentación frontend inaccesible era previa |

`Test-Phase3Compose.ps1 -Browser` APROBADO: navegador real sobre Compose final, login/logout, pantallas previas, regla Java creada/desactivada, foco/modal/navegación móvil y generación/descarga PDF; 16 axe sin violaciones y cero errores JavaScript. Últimas imágenes Gateway/frontend y paginación Audit incluidas. Todos los contenedores desechables quedaron detenidos.

Problemas detectados y corregidos: npm10 falló internamente al resolver dependencias de Angular22 (se restauró instalación y se migró con npm11); axe detectó tabla móvil sin foco (tabindex y región accesible); la prueba matemática detectó variable de tipo sobrescrita que mezclaba estadísticas y ejecutivo (ahora separados, validado PDF estadístico con dos filas); Docker detectó Pkcs9.0.4 vulnerable (actualizado a10.0.13 según aviso Microsoft https://github.com/advisories/GHSA-846f-f764-76mx, sin desactivar auditoría NuGet).

No ejecutado en producción: transferencia de datos del usuario, restauración de sus respaldos, VPS/dominio/certificado HTTPS. K8s no desplegado por alcance Fase4; OAuth externo preparado mediante contrato y tabla, no activado. Fuente Fase1 indicada devuelve404. Los apartados siguientes conservan el registro incremental de validaciones anteriores.

Los resultados se registran al ejecutar cada comando. No interpretar PENDIENTE como éxito.

| Comando | Resultado |
| --- | --- |
| `dotnet --version` | 10.0.400 |
| `node --version` | 22.13.0 |
| `python --version` | 3.13.2 |
| `java -version` | Java 11; insuficiente para Spring Boot moderno; usar imagen Maven con Java21 disponible |
| `docker info --format '{{.ServerVersion}}'` | 28.5.1, requiere permiso de ejecución fuera del sandbox |
| `dotnet test ClimateMonitoringSystem.sln --no-restore --verbosity minimal` | Siete proyectos terminaron sin fallos; una prueba RabbitMQ omitida por configuración |
| `npm.cmd test` en AuthService | 5/5 aprobadas, TypeScript estricto compila |
| `dotnet build src/Gateway/Climate.Gateway/Climate.Gateway.csproj` | Aprobado, 0 warnings/errores |
| `dotnet build src/Services/CommunityService/Climate.Communities.Api/Climate.Communities.Api.csproj` | Aprobado, 0 warnings/errores |
| `dotnet test tests/Climate.Sensors.Tests/Climate.Sensors.Tests.csproj --no-restore --verbosity minimal` | 20/20 aprobadas tras adaptar Sensor |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Phase3Auth.ps1` | 3/3 integraciones aprobadas: Auth, Community y transferencia de reglas/alertas desde el esquema .NET real |
| `docker build -t climate/auth:phase3 ./src/Services/AuthService` | Imagen final construida con driver corregido |
| `docker build -t climate/community:phase3 -f src/Services/CommunityService/Climate.Communities.Api/Dockerfile .` | Imagen construida con restore NuGet locked-mode |
| `docker build -t climate/alerts:phase3 src/Services/AlertServiceJava` | Imagen Java21 construida; 5 pruebas de dominio aprobadas y 2 integraciones omitidas en el build por no configurar fixture; ejecutadas separadamente con PostgreSQL |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Phase3Alerts.ps1` | 7/7 JUnit y 1/1 smoke del contenedor Java real aprobados |
| `dotnet test ClimateMonitoringSystem.sln --no-restore --verbosity minimal --logger trx --results-directory TestResults/phase3` | 122 aprobadas, 0 fallidas, 1 omitida; TRX en TestResults/phase3 |
| `docker compose -f docker-compose.yml -f docker-compose.phase3-auth.yml -f docker-compose.phase3-community.yml -f docker-compose.phase3-alerts.yml config --quiet` | Composición válida; sin levantar servicios ni imprimir secretos |
| `npm audit` AuthService y Phase3Migration | 0 vulnerabilidades tras actualizar mssql a 12.7.10 |

Prueba Auth: migración doble y sin duplicados, colisiones con rollback, conservación de IDs y hashes,
login de usuarios existentes, roles, desactivación, logout, rechazo de asignación pública de rol,
compatibilidad real de hashes/JWT con ASP.NET, broker detenido y recuperación durable al reiniciar
productor y RabbitMQ. Gateway: JWT, acceso sin token denegado, login público, CORS, CorrelationId.

Prueba Community: migración doble, UUID y datos históricos conservados, referencias válidas,
conteos consultados vía API, filtros, roles, coordenadas inválidas, duplicados, creación de sensor
con comunidad que solo existe en PostgreSQL, catálogo SQL sin copias, activación/desactivación,
error 503 al fallar Community y Outbox persistido. Usa las migraciones EF reales de Sensor.

Hallazgos corregidos antes del resultado final: encabezado CorrelationId duplicado por YARP,
registro DI de CommunityDirectory y limpieza de procesos terminados por señal en la prueba.
La primera restauración de Community detectó avisos del driver SQL Server: se retiró ese driver
del servicio de dominio y se mantuvo la transferencia en una herramienta offline independiente.

Alert Java: 5 pruebas de negocio cubren umbrales exclusivos, regla de mayor gravedad por fenómeno,
reglas verdes/inactivas, transiciones y responsables, validación de intervalos y enum SensorType
numérico .NET/string Angular. Dos pruebas con PostgreSQL real y filtro JWT real verifican issuer,
expiración, roles, errores de rangos, transacción+Outbox, replay sin duplicar alertas ni entregas,
estados y sensores inactivos. Solo Delivery se sustituye en esas dos pruebas.

Migración Alert: esquema creado por el servicio .NET existente, 27 reglas sembradas por su
inicializador, alerta histórica atendida, usuario responsable y Outbox pendiente. Dos importaciones
con mismo fingerprint, UUID, estados y snapshots conservados; colisiones rechazadas sin modificar origen.

Smoke del contenedor Java: readiness y persistencia reales, token emitido por el módulo Tokens
de NestJS aceptado, evento entregado a RabbitMQ con EventId/SchemaVersion/CorrelationId, broker
detenido sin fallo de creación de regla, Outbox PostgreSQL pendiente y confirmado tras recuperar
RabbitMQ. No sustituye la integración todavía pendiente con Event/Monitoring/SignalR reales.
Se corrigió una finalización prematura del ejecutor Node y la ejecución final node:test pasó.

Fuentes: prompt local leído íntegramente, enunciado oficial Fase 3 descargado como
`../phase_3.reference.md`, Fase 2 local leída íntegramente. URL de Fase 1 indicada devuelve 404.
No se leyó ni publicó el contenido de archivos `.env`.
