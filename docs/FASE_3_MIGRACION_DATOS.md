# Fase 3 — Migración de datos

## Estado

Auth y Community tienen migraciones ejecutables probadas con SQL Server y PostgreSQL reales aislados.
Alerts y Audit tienen también transferencia doble verificada desde sus esquemas .NET reales.
Ninguna base de la aplicación real fue transferida ni eliminada.

**Alcance vigente:** el usuario no exige conservar datos anteriores; las transferencias reales están fuera de alcance y no bloquean una instalación nueva. Los migradores se conservan como herramientas ya probadas. Se comprobó además respaldo/restauración local de ocho volúmenes en otro proyecto; evidencia en validation/phase3-recovery.json y procedimiento en FASE_3_DESPLIEGUE.md.

## Auth

Origen: IdentityDb SQL Server, tablas `Users` y `AuditOutbox`. Destino: PostgreSQL privado de Auth,
tablas `users` y `audit_outbox`. Script: `src/Services/AuthService/src/migrate.ts`.

Se conservan ID, usuario/nombre normalizado, email/normalizado, PasswordHash, Role, IsActive,
CreatedAt, UpdatedAt y LastLoginAt. Los hashes ASP.NET Identity V2/V3 son verificados por Node;
los hashes nuevos V3 creados por Node también fueron verificados por ASP.NET Identity real.
Fechas pasan por el driver JavaScript y tienen precisión de milisegundos. Los campos heredados
CreatedAt/UpdatedAt tienen precisión SQL de segundos; LastLoginAt puede tener más precisión
en SQL, por lo que el traspaso conserva el instante hasta milisegundos, no ticks de 100 ns.

La importación usa lotes de 250, transacción SERIALIZABLE de lectura en origen y transacción
PostgreSQL destino, bloqueo de migración, verificación de todos los campos y conteo/IDs.
Una identidad ya importada solo se acepta si coincide exactamente; diferencias abortan la
transacción y no se sobreescriben. Se copian eventos Outbox pendientes conservando EventId.
El fingerprint impreso permite comparar ejecuciones, sin mostrar hashes ni identidades.

## Ejecución sobre datos reales (pendiente)

1. Respaldar SQL Server y destino PostgreSQL, verificar restauración y anotar conteos.
2. Suspender escrituras de identidad y detener el productor Identity .NET. No detener los
   demás dominios, no ejecutar dos productores de identidad simultáneamente.
3. Crear/iniciar `auth-postgres` con el overlay, sin iniciar aún el Auth NestJS.
4. Instalar dependencias con `npm ci` y compilar con `npm run build` en AuthService.
5. Inyectar `AUTH_DATABASE_URL` y `SOURCE_SQLSERVER_CONFIG` desde un administrador de secretos
   o entorno del proceso. SOURCE_SQLSERVER_CONFIG es JSON del driver mssql, incluyendo servidor,
   puerto, base IdentityDb, usuario de lectura, contraseña y opciones TLS apropiadas.
6. Ejecutar `npm run migrate`. Guardar únicamente el resumen de conteos y fingerprint.
7. Repetir `npm run migrate`, comprobar mismo resumen y ausencia de duplicados.
8. Iniciar el overlay NestJS, sin ADMIN_SEED_PASSWORD, y verificar login de cuentas migradas,
   tres roles, llamadas por Gateway y procesamiento del Outbox.
9. Conservar SQL Server y respaldos; no retirar el origen automáticamente.

Rollback antes de nuevas escrituras en PostgreSQL: detener NestJS y restaurar Identity .NET
mediante el Compose original. Si hubo nuevas escrituras en PostgreSQL, bloquear escrituras,
respaldar ambos orígenes y reconciliar antes del retorno; no existe todavía sincronización inversa.

## Validaciones ejecutadas

`scripts/Test-Phase3Auth.ps1`: tres identidades con hashes generados realmente por ASP.NET,
dos importaciones consecutivas, validación de IDs/hashes, login y último acceso, colisión de datos
con rollback, origen sin cambios, migración de Outbox, recuperación con broker detenido y
reinicio del proceso productor. No acredita una migración sobre datos de producción.

## Community

Herramienta: `tools/Phase3Migration/communities.cjs`, `npm ci` antes de utilizarla. Inyectar
`SOURCE_SQLSERVER_CONFIG` apuntando a SensorDb y `COMMUNITY_DATABASE_URL` apuntando al PostgreSQL
privado de Community. Ejecutar `npm run communities` dentro de tools/Phase3Migration.

Transfiere Communities en lotes de 250 con IDs, geografía, estado y fecha original. Comprueba
igualdad de campos, conteos, IDs y referencias distintas de sensores. Una colisión de datos aborta
sin sobreescribir. Usa lectura SERIALIZABLE en origen y transacción destino con bloqueo de migración.
El esquema PostgreSQL está versionado en `Persistence/001_initial.sql`.

El Outbox antiguo de SensorDb continúa siendo procesado por Sensor; no se retira ni se copia
indiscriminadamente al nuevo propietario. Nuevas operaciones Community escriben su propio Outbox.

Después de respaldar y suspender escrituras de comunidades: transferir, repetir la verificación,
iniciar Community y activar el overlay. La migración EF nueva de Sensor solo retira la FK; no borra
el catálogo SQL. En modo remoto se deshabilita su API de comunidades y seed, y el catálogo viejo
queda como respaldo histórico. No realizar rollback de la FK después de crear comunidades nuevas
sin reconciliar primero las referencias SQL y el estado actualizado PostgreSQL.

## Alert

Herramienta: `tools/Phase3Migration/alerts.cjs`. Inyectar SOURCE_SQLSERVER_CONFIG apuntando a
AlertDb y ALERT_POSTGRES_URL apuntando al PostgreSQL privado de Alert. Ejecutar `npm run alerts`.
Transfiere reglas, alertas, estados, responsables, fechas y snapshots de regla, en lotes de 250;
convierte SensorType numérico a los nombres exactos del contrato y valida enums y referencias.
Conserva UUID; copia Outbox pendiente con EventId original a delivery_outbox tipo AUDIT.
No modifica ni elimina el origen. Colisiones, conteos o referencias inconsistentes abortan el destino.

Ejecutar durante una ventana con el productor .NET detenido y antes de iniciar Spring Boot para
el tráfico final. El origen queda disponible para recuperación. Reconciliar nuevas escrituras
si se pretende volver a .NET después de abrir tráfico en PostgreSQL. No existe sincronización inversa.

## Audit y Report

Audit: tools/Phase3Migration/audit.cjs, ejecutar `npm run audit` después de npm ci. Variables SOURCE_SQLSERVER_CONFIG (AuditDb), AUDIT_MONGO_URL y opcional AUDIT_MONGO_DATABASE (audit). Detener el consumidor anterior durante la transferencia; conservar mensajes pendientes en RabbitMQ. Importación por lotes de 250, EventId/UUID original, timestamp, usuario, acción, recurso y correlationId nulo histórico preservados. SetOnInsert y comparación campo por campo permiten repetir; validar IDs/conteos/fingerprint antes del tráfico. Una colisión aborta sin sobrescribir. El origen se lee en transacción SERIALIZABLE que termina en rollback, sin escrituras. Mongo standalone conserva lo ya importado si falla un lote; retomar es idempotente, no es rollback global. Test-Phase3Audit.ps1 comprobó doble transferencia desde el esquema .NET real y rechazo de duplicados del consumidor.

Report usa MongoDB nuevo sin importar entidades de negocio: solo trabajos, parámetros, metadatos y registros de descargas. Datos de cada informe provienen de APIs y no se transfieren tablas a Report. Respaldar/restaurar Mongo y report-files juntos. La prueba Compose valida una instalación nueva; todavía no se ejecutó la transferencia de datos reales del usuario ni una restauración de sus respaldos.
