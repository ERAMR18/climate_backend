Reporte generado por: Christopher Herrera  
Analisis y correcciones: Christopher Herrera  
Agente: Codex  

Destino: Erwin Biluk 
Capa: Backend (.NET)  
Objetivo: Poder localizar los componentes núcleo del backend utilizados durante el proyecto para compartir el conocimiento con el resto del grupo y facilitar su comprensión y exposición.

# Documentación esencial del Backend

**Climate Monitoring System**

Copia local analizada: rama master, commit eb90750. Fecha: 9 de septiembre de 2026.

Las rutas de archivo se expresan desde la carpeta backend. Este reporte explica las piezas principales del código; no enumera archivos generados ni pruebas como componentes. Los ejemplos JSON son ficticios y los códigos HTTP son los que devuelve el código en caso de éxito; no se realizaron solicitudes contra un servidor.

## 1. Arquitectura y ubicación de las partes

El backend es la parte del sistema que recibe solicitudes del frontend, revisa permisos y datos, aplica las reglas y consulta o guarda información. Este proyecto usa C# y .NET 10, según Directory.Build.props. Está dividido en seis microservicios: programas separados que atienden responsabilidades diferentes. El gateway es la entrada común que lleva cada solicitud al servicio correspondiente.

```text
backend/
├── src/
│   ├── Gateway/Climate.Gateway/
│   ├── BuildingBlocks/
│   │   ├── Climate.Contracts/
│   │   └── Climate.SharedKernel/
│   └── Services/
│       ├── AlertService/
│           Climate.Alerts.Api/
│           Climate.Alerts.Application/
│           Climate.Alerts.Domain/
│           Climate.Alerts.Infrastructure/
│       ├── AuditService/
│           Climate.Audit.Api/
│           Climate.Audit.Application/
│           Climate.Audit.Domain/
│           Climate.Audit.Infrastructure/
│       ├── EventService/
│           Climate.Events.Api/
│           Climate.Events.Application/
│           Climate.Events.Domain/
│           Climate.Events.Infrastructure/
│       ├── IdentityService/
│           Climate.Identity.Api/
│           Climate.Identity.Application/
│           Climate.Identity.Domain/
│           Climate.Identity.Infrastructure/
│       ├── MonitoringService/
│           Climate.Monitoring.Api/
│           Climate.Monitoring.Application/
│           Climate.Monitoring.Domain/
│           Climate.Monitoring.Infrastructure/
│       └── SensorService/
│           Climate.Sensors.Api/
│           Climate.Sensors.Application/
│           Climate.Sensors.Domain/
│           Climate.Sensors.Infrastructure/
├── tests/
├── docs/
├── Directory.Build.props
└── ClimateMonitoringSystem.sln
```

| Carpeta o capa | Para qué sirve |
|---|---|
| Api | Recibe solicitudes y decide qué respuesta HTTP devolver. Contiene Controllers y Program.cs. |
| Application | Realiza las tareas del sistema y valida los datos, como registrar una cuenta o calcular una gráfica. |
| Domain | Representa los elementos del negocio, como User, Sensor y ClimateAlert. |
| Infrastructure | Consulta SQL Server, conecta con otros servicios y ejecuta trabajos en segundo plano. |
| BuildingBlocks/Climate.Contracts | Comparte estructuras de mensajes y nombres entre servicios. |
| BuildingBlocks/Climate.SharedKernel | Comparte formas de representar resultados y errores. |

| Microservicio | Responsabilidad |
|---|---|
| IdentityService | Acceso y administración de cuentas. |
| SensorService | Comunidades y catálogo de sensores. |
| MonitoringService | Lecturas, gráficas y simulación. |
| AlertService | Evaluación de riesgos y alertas. |
| EventService | Registro de eventos relacionados con alertas. |
| AuditService | Registro de acciones realizadas por usuarios. |

### Climate.Gateway

**Categoría:** Gateway

**Descripción:** Sirve como entrada de las solicitudes del frontend y las dirige al microservicio adecuado. Por ejemplo, /api/auth/login se reenvía a /api/v1/auth/login del servicio de identidad.

**Ruta del archivo:**

```text
src/Gateway/Climate.Gateway/Program.cs
```

**Configuración de rutas:** `src/Gateway/Climate.Gateway/appsettings.json`. También limita a 120 solicitudes por minuto por dirección de origen y responde 429 cuando se supera el límite. /health queda fuera de ese límite. La validación JWT de las operaciones se realiza en los microservicios.

### Program.cs

**Categoría:** Configuration

**Descripción:** Es el punto de arranque de cada aplicación. Activa controladores, servicios, acceso a datos, comprobación de sesión y permisos. Cada microservicio tiene su propio archivo.

**Ruta del archivo:**

```text
src/Services/IdentityService/Climate.Identity.Api/Program.cs
```

**Otros archivos de arranque:**

- `src/Services/AlertService/Climate.Alerts.Api/Program.cs`
- `src/Services/AuditService/Climate.Audit.Api/Program.cs`
- `src/Services/EventService/Climate.Events.Api/Program.cs`
- `src/Services/MonitoringService/Climate.Monitoring.Api/Program.cs`
- `src/Services/SensorService/Climate.Sensors.Api/Program.cs`

## 2. Services: tareas principales del backend

Un Service realiza una tarea del backend. El Controller recibe los datos enviados desde el frontend y llama al Service; este revisa reglas, usa repositorios y prepara los datos que terminarán en la respuesta (response). Un repositorio es la pieza encargada de leer o guardar datos. Las interfaces que empiezan por I indican qué operaciones debe ofrecer cada pieza.

### UserService

**Categoría:** Service

**Descripción:** Sirve para registrar cuentas, comprobar usuario y contraseña, iniciar sesión, consultar cuentas y cambiar su estado o rol. Al registrarse, una cuenta recibe el rol Viewer. No permite repetir usuario o correo.

**Ruta del archivo:**

```text
src/Services/IdentityService/Climate.Identity.Application/Users/UserService.cs
```

**Métodos principales:** RegisterAsync, LoginAsync, GetByIdAsync, GetAllAsync, UpdateAsync, SetStatusAsync.

**Relación con otras partes:** AuthController y UsersController; IUserRepository, IPasswordService e IJwtTokenGenerator.

### CommunityService

**Categoría:** Service

**Descripción:** Sirve para consultar, crear y modificar comunidades. Revisa los datos y evita nombres repetidos antes de guardar.

**Ruta del archivo:**

```text
src/Services/SensorService/Climate.Sensors.Application/Communities/CommunityService.cs
```

**Métodos principales:** GetAllAsync, GetByIdAsync, CreateAsync, UpdateAsync.

**Relación con otras partes:** CommunitiesController; ISensorCatalogRepository.

### SensorService

**Categoría:** Service

**Descripción:** Sirve para consultar, crear, modificar, activar y desactivar sensores. Comprueba que el código no esté repetido y que la comunidad permita la operación.

**Ruta del archivo:**

```text
src/Services/SensorService/Climate.Sensors.Application/Sensors/SensorService.cs
```

**Métodos principales:** GetAllAsync, GetByIdAsync, CreateAsync, UpdateAsync, SetStatusAsync.

**Relación con otras partes:** SensorsController e InternalSensorsController; ISensorCatalogRepository.

### MonitoringService

**Categoría:** Service

**Descripción:** Sirve para guardar lecturas de sensores activos, consultar el histórico y calcular gráficas. Después de guardar una lectura, pide evaluar riesgos y publica la lectura en vivo. Las gráficas agrupan y promedian mediciones por intervalo.

**Ruta del archivo:**

```text
src/Services/MonitoringService/Climate.Monitoring.Application/Readings/MonitoringService.cs
```

**Métodos principales:** GetCurrentAsync, GetLatestAsync, GetHistoryAsync, CreateAsync, GetChartAsync, GenerateSimulationBatchAsync.

**Relación con otras partes:** MonitoringController y ClimateSimulationWorker; IMonitoringRepository, ISensorCatalogClient, IAlertEvaluationClient e IRealtimePublisher.

### SimulationService

**Categoría:** Service

**Descripción:** Sirve para consultar, iniciar o detener la simulación. ResetAsync la detiene y elimina las lecturas guardadas; no borra por ese método las comunidades, cuentas, alertas o eventos.

**Ruta del archivo:**

```text
src/Services/MonitoringService/Climate.Monitoring.Application/Simulation/SimulationService.cs
```

**Métodos principales:** GetStatus, Start, StopSimulation, ResetAsync.

**Relación con otras partes:** MonitoringController; ISimulationControl e IMonitoringRepository.

### AlertService

**Categoría:** Service

**Descripción:** Sirve para consultar alertas y comparar cada lectura con las reglas de riesgo. Crea o actualiza una alerta cuando hay riesgo; si la medición vuelve al nivel Green, resuelve la alerta activa. También permite resolverla manualmente y registrar su estado en eventos.

**Ruta del archivo:**

```text
src/Services/AlertService/Climate.Alerts.Application/Alerts/AlertService.cs
```

**Métodos principales:** ListAsync, GetByIdAsync, EvaluateAsync, ResolveAsync.

**Relación con otras partes:** AlertsController e InternalEvaluationController; IAlertRepository, IRiskEvaluationService e IEventHistoryClient.

### RiskEvaluationService

**Categoría:** Service

**Descripción:** Sirve para comparar el valor de un sensor con límites configurados y decidir si corresponde Green, Yellow, Orange o Red. Puede evaluar riesgos por valores demasiado altos o demasiado bajos.

**Ruta del archivo:**

```text
src/Services/AlertService/Climate.Alerts.Application/Risk/RiskEvaluationService.cs
```

**Métodos principales:** Evaluate.

**Relación con otras partes:** AlertService; IRiskRuleProvider.

### ClimateEventService

**Categoría:** Service

**Descripción:** Sirve para consultar eventos y registrar el estado de una alerta. Si ya existe un evento de esa alerta, actualiza ese registro en vez de crear otro por cada aviso.

**Ruta del archivo:**

```text
src/Services/EventService/Climate.Events.Application/Events/ClimateEventService.cs
```

**Métodos principales:** ListAsync, GetByIdAsync, RecordAsync.

**Relación con otras partes:** EventsController e InternalEventsController; IClimateEventRepository.

### AuditService

**Categoría:** Service

**Descripción:** Sirve para consultar la bitácora y registrar quién hizo una acción, sobre qué elemento y cuándo. Si llega otra vez el mismo identificador de evento, devuelve el registro existente.

**Ruta del archivo:**

```text
src/Services/AuditService/Climate.Audit.Application/Auditing/AuditService.cs
```

**Métodos principales:** ListAsync, GetAsync, RecordAsync.

**Relación con otras partes:** AuditController e InternalAuditController; IAuditLogRepository.

**Reglas de consulta:** MonitoringService acepta intervalos de gráfica 1m, 5m, 15m y 1h. Rechaza periodos con fecha inicial posterior a la final. No acepta 1d en esta versión del backend.

## 3. Autenticación, permisos y errores

JWT significa JSON Web Token: es el comprobante de acceso que recibe el frontend al iniciar sesión. El backend revisa su firma, origen, destinatario y vencimiento en las rutas protegidas. Authorize exige sesión y puede exigir además un rol; AllowAnonymous permite entrar sin JWT. Los endpoints internos usan una clave separada en X-Internal-Api-Key.

### JwtTokenGenerator

**Categoría:** Service

**Descripción:** Sirve para crear el JWT con los datos de identificación y rol del usuario y una fecha de vencimiento. La clave de firma se obtiene de la configuración y no se incluye en este documento.

**Ruta del archivo:**

```text
src/Services/IdentityService/Climate.Identity.Infrastructure/Security/JwtTokenGenerator.cs
```

**Utilizado por:** UserService a través de IJwtTokenGenerator.

### PasswordService

**Categoría:** Service

**Descripción:** Sirve para guardar una representación protegida de la contraseña y comprobar la contraseña al iniciar sesión. Utiliza PasswordHasher; no guarda la contraseña como texto.

**Ruta del archivo:**

```text
src/Services/IdentityService/Climate.Identity.Infrastructure/Security/PasswordService.cs
```

**Utilizado por:** UserService a través de IPasswordService.

### SystemRoles

**Categoría:** Constant

**Descripción:** Define los roles Administrator, Operator y Viewer para que los servicios usen los mismos nombres al revisar permisos.

**Ruta del archivo:**

```text
src/BuildingBlocks/Climate.Contracts/Identity/SystemRoles.cs
```

| Permiso | Quién puede usarlo | Dónde se configura |
|---|---|---|
| AdministratorsOnly | Administrator: listado y administración de usuarios. | src/Services/IdentityService/Climate.Identity.Api/Program.cs |
| ManageSensors | Administrator y Operator: creación, cambios y estado de sensores y comunidades. | src/Services/SensorService/Climate.Sensors.Api/Program.cs |
| OperateSimulation | Administrator y Operator: registrar lecturas, iniciar y detener simulación. | src/Services/MonitoringService/Climate.Monitoring.Api/Program.cs |
| ResetSystem | Administrator: reiniciar simulación y lecturas. | src/Services/MonitoringService/Climate.Monitoring.Api/Program.cs |
| ManageAlerts | Administrator y Operator: resolver alertas. | src/Services/AlertService/Climate.Alerts.Api/Program.cs |
| Administrator en AuditController | Solo Administrator: consultar auditoría. | src/Services/AuditService/Climate.Audit.Api/Controllers/AuditController.cs |

### Result y Result<T>

**Categoría:** Result

**Descripción:** Sirven para indicar si una tarea terminó correctamente o falló. Result<T> también lleva los datos que se devolverán cuando todo sale bien.

**Ruta del archivo:**

```text
src/BuildingBlocks/Climate.SharedKernel/Results/Result.cs
```

**Versión con datos:** `src/BuildingBlocks/Climate.SharedKernel/Results/ResultOfT.cs`.

### ApplicationError

**Categoría:** Model

**Descripción:** Describe un error de la aplicación con un código, una explicación y un tipo, como datos incorrectos, recurso no encontrado o conflicto.

**Ruta del archivo:**

```text
src/BuildingBlocks/Climate.SharedKernel/Results/ApplicationError.cs
```

### ResultExtensions

**Categoría:** ErrorHandler

**Descripción:** Convierte el resultado de una tarea en una respuesta HTTP. Por ejemplo, devuelve 200 con datos, 204 sin cuerpo o un error con su explicación.

**Ruta del archivo:**

```text
src/Services/SensorService/Climate.Sensors.Api/Errors/ResultExtensions.cs
```

**Otras implementaciones por microservicio:**

- `src/Services/MonitoringService/Climate.Monitoring.Api/Errors/ResultExtensions.cs`
- `src/Services/IdentityService/Climate.Identity.Api/Errors/ResultExtensions.cs`
- `src/Services/EventService/Climate.Events.Api/Errors/ResultExtensions.cs`
- `src/Services/AuditService/Climate.Audit.Api/Errors/ResultExtensions.cs`
- `src/Services/AlertService/Climate.Alerts.Api/Errors/ResultExtensions.cs`

### GlobalExceptionHandler

**Categoría:** ErrorHandler

**Descripción:** Captura errores inesperados, los registra y devuelve una respuesta 500 sin exponer los detalles internos de la excepción.

**Ruta del archivo:**

```text
src/Services/SensorService/Climate.Sensors.Api/Errors/GlobalExceptionHandler.cs
```

**Otras implementaciones por microservicio:**

- `src/Services/MonitoringService/Climate.Monitoring.Api/Errors/GlobalExceptionHandler.cs`
- `src/Services/IdentityService/Climate.Identity.Api/Errors/GlobalExceptionHandler.cs`
- `src/Services/EventService/Climate.Events.Api/Errors/GlobalExceptionHandler.cs`
- `src/Services/AuditService/Climate.Audit.Api/Errors/GlobalExceptionHandler.cs`
- `src/Services/AlertService/Climate.Alerts.Api/Errors/GlobalExceptionHandler.cs`

Los errores controlados usan ProblemDetails, una estructura con status, title, detail y traceId. Los códigos habituales son 400 para una solicitud mal formada, 401 para acceso no válido, 403 para falta de permisos, 404 si no existe el recurso, 409 para datos repetidos o conflictos, 422 para datos que no cumplen las reglas y 500 para fallos inesperados. Los códigos de éxito aparecen en las tablas de endpoints.

## 4. Datos, repositorios y reglas de entrada

Los datos se guardan en SQL Server mediante Entity Framework Core, la herramienta que relaciona objetos de C# con tablas. Cada microservicio tiene un DbContext para acceder a sus datos. Los repositorios reúnen las consultas para que las reglas del sistema no tengan que escribirlas directamente.

### UserRepository

**Categoría:** Repository

**Descripción:** Sirve para consultar y guardar cuentas de usuario en la base de datos.

**Ruta del archivo:**

```text
src/Services/IdentityService/Climate.Identity.Infrastructure/Persistence/UserRepository.cs
```

**Acceso a datos:** IdentityDbContext.

**Ruta del DbContext:** `src/Services/IdentityService/Climate.Identity.Infrastructure/Persistence/IdentityDbContext.cs`.

### SensorCatalogRepository

**Categoría:** Repository

**Descripción:** Sirve para consultar y guardar sensores y comunidades en la base de datos.

**Ruta del archivo:**

```text
src/Services/SensorService/Climate.Sensors.Infrastructure/Persistence/SensorCatalogRepository.cs
```

**Acceso a datos:** SensorsDbContext.

**Ruta del DbContext:** `src/Services/SensorService/Climate.Sensors.Infrastructure/Persistence/SensorsDbContext.cs`.

### MonitoringRepository

**Categoría:** Repository

**Descripción:** Sirve para consultar y guardar lecturas de sensores en la base de datos.

**Ruta del archivo:**

```text
src/Services/MonitoringService/Climate.Monitoring.Infrastructure/Persistence/MonitoringRepository.cs
```

**Acceso a datos:** MonitoringDbContext.

**Ruta del DbContext:** `src/Services/MonitoringService/Climate.Monitoring.Infrastructure/Persistence/MonitoringDbContext.cs`.

### AlertRepository

**Categoría:** Repository

**Descripción:** Sirve para consultar y guardar alertas en la base de datos.

**Ruta del archivo:**

```text
src/Services/AlertService/Climate.Alerts.Infrastructure/Persistence/AlertRepository.cs
```

**Acceso a datos:** AlertsDbContext.

**Ruta del DbContext:** `src/Services/AlertService/Climate.Alerts.Infrastructure/Persistence/AlertsDbContext.cs`.

### ClimateEventRepository

**Categoría:** Repository

**Descripción:** Sirve para consultar y guardar eventos climáticos en la base de datos.

**Ruta del archivo:**

```text
src/Services/EventService/Climate.Events.Infrastructure/Persistence/ClimateEventRepository.cs
```

**Acceso a datos:** EventsDbContext.

**Ruta del DbContext:** `src/Services/EventService/Climate.Events.Infrastructure/Persistence/EventsDbContext.cs`.

### AuditLogRepository

**Categoría:** Repository

**Descripción:** Sirve para consultar y guardar acciones de auditoría en la base de datos.

**Ruta del archivo:**

```text
src/Services/AuditService/Climate.Audit.Infrastructure/Persistence/AuditLogRepository.cs
```

**Acceso a datos:** AuditDbContext.

**Ruta del DbContext:** `src/Services/AuditService/Climate.Audit.Infrastructure/Persistence/AuditDbContext.cs`.

| Entidad | Descripción | Ruta |
|---|---|---|
| User | Cuenta con identidad, rol, estado y contraseña protegida. | src/Services/IdentityService/Climate.Identity.Domain/Users/User.cs |
| Community | Comunidad con ubicación y estado. | src/Services/SensorService/Climate.Sensors.Domain/Communities/Community.cs |
| Sensor | Dispositivo asociado a una comunidad. | src/Services/SensorService/Climate.Sensors.Domain/Sensors/Sensor.cs |
| SensorReading | Medición con valor, unidad y momento. | src/Services/MonitoringService/Climate.Monitoring.Domain/Readings/SensorReading.cs |
| ClimateAlert | Riesgo detectado con nivel y estado de resolución. | src/Services/AlertService/Climate.Alerts.Domain/Alerts/ClimateAlert.cs |
| ClimateEvent | Registro del estado de una alerta. | src/Services/EventService/Climate.Events.Domain/Events/ClimateEvent.cs |
| AuditLog | Registro de una acción de usuario. | src/Services/AuditService/Climate.Audit.Domain/Auditing/AuditLog.cs |

Los archivos de Persistence/Migrations guardan cambios de estructura de las bases. Los inicializadores de base de datos se invocan al arrancar cada API. Para ubicar registros y configuraciones de acceso a datos, consultar DependencyInjection.cs dentro de cada proyecto Infrastructure. Las credenciales y cadenas de conexión no se reproducen aquí.

| Validador | Qué comprueba | Ruta |
|---|---|---|
| LoginRequestValidator | Usuario/correo obligatorio hasta 254 caracteres y contraseña obligatoria hasta 128. | src/Services/IdentityService/Climate.Identity.Application/Users/LoginRequestValidator.cs |
| RegisterRequestValidator | Usuario de 3 a 50 caracteres permitidos; correo válido hasta 254; contraseña de 12 a 128 con mayúscula, minúscula y número. | src/Services/IdentityService/Climate.Identity.Application/Users/RegisterRequestValidator.cs |
| UpdateUserRequestValidator | Usuario y correo válidos y rol reconocido. | src/Services/IdentityService/Climate.Identity.Application/Users/UpdateUserRequestValidator.cs |
| CreateCommunityRequestValidator y UpdateCommunityRequestValidator | Nombre obligatorio hasta 120, descripción hasta 500, latitud entre -90 y 90 y longitud entre -180 y 180. | src/Services/SensorService/Climate.Sensors.Application/Communities/CommunityValidators.cs |
| CreateSensorRequestValidator, UpdateSensorRequestValidator y SensorFieldsValidator<T> | Nombre, código y unidad; límites de longitud; tipo reconocido; comunidad indicada y coordenadas dentro del rango. | src/Services/SensorService/Climate.Sensors.Application/Sensors/SensorValidators.cs |
| CreateReadingRequestValidator | Sensor indicado, valor entre -1000000 y 1000000 y fecha no superior al momento actual más un minuto. | src/Services/MonitoringService/Climate.Monitoring.Application/Readings/CreateReadingRequestValidator.cs |

## 5. Comunicación interna y actualización en vivo

Los microservicios se comunican mediante solicitudes HTTP internas. No se identificó un intermediario de mensajes como RabbitMQ en las implementaciones revisadas. Estas solicitudes usan X-Internal-Api-Key. El navegador recibe las actualizaciones en vivo mediante el hub SignalR del servicio de monitoreo.

### SensorCatalogClient

**Categoría:** Client

**Descripción:** Pide a SensorService los sensores activos para saber cuáles pueden generar lecturas.

**Ruta del archivo:**

```text
src/Services/MonitoringService/Climate.Monitoring.Infrastructure/Clients/SensorCatalogClient.cs
```

### AlertEvaluationClient

**Categoría:** Client

**Descripción:** Envía una lectura a AlertService para comprobar si representa un riesgo.

**Ruta del archivo:**

```text
src/Services/MonitoringService/Climate.Monitoring.Infrastructure/Clients/AlertEvaluationClient.cs
```

### EventHistoryClient

**Categoría:** Client

**Descripción:** Envía el estado de una alerta a EventService para registrar o actualizar su evento.

**Ruta del archivo:**

```text
src/Services/AlertService/Climate.Alerts.Infrastructure/Clients/EventHistoryClient.cs
```

### AuditWriter

**Categoría:** Client

**Descripción:** Envía a AuditService las acciones de acceso, usuarios, sensores y simulación que llaman a este escritor.

**Ruta del archivo:**

```text
src/BuildingBlocks/Climate.Contracts/Audit/AuditWriter.cs
```

### RealtimeWriter

**Categoría:** Client

**Descripción:** Envía avisos de sensores y alertas al servicio de monitoreo para distribuirlos a las conexiones del navegador.

**Ruta del archivo:**

```text
src/BuildingBlocks/Climate.Contracts/Realtime/RealtimeEnvelope.cs
```

### MonitoringHub

**Categoría:** Hub

**Descripción:** Es el punto de conexión /hubs/monitoring para usuarios autenticados. Permite recibir las actualizaciones en vivo.

**Ruta del archivo:**

```text
src/Services/MonitoringService/Climate.Monitoring.Api/Realtime/MonitoringHub.cs
```

### SignalRRealtimePublisher

**Categoría:** Service

**Descripción:** Envía el nombre del evento y sus datos a todos los clientes conectados al MonitoringHub.

**Ruta del archivo:**

```text
src/Services/MonitoringService/Climate.Monitoring.Api/Realtime/SignalRRealtimePublisher.cs
```

### ClimateSimulationWorker

**Categoría:** Worker

**Descripción:** Ejecuta ciclos en segundo plano mientras la simulación está activa. En cada ciclo pide generar lecturas; registra los fallos y sigue esperando el siguiente ciclo.

**Ruta del archivo:**

```text
src/Services/MonitoringService/Climate.Monitoring.Infrastructure/Simulation/ClimateSimulationWorker.cs
```

### SimulationControl

**Categoría:** Service

**Descripción:** Recuerda en memoria si la simulación debe estar funcionando o detenida.

**Ruta del archivo:**

```text
src/Services/MonitoringService/Climate.Monitoring.Infrastructure/Simulation/SimulationControl.cs
```

### SimulatedValueGenerator

**Categoría:** Service

**Descripción:** Genera un valor aleatorio dentro del rango configurado para cada tipo de sensor.

**Ruta del archivo:**

```text
src/Services/MonitoringService/Climate.Monitoring.Infrastructure/Simulation/SimulatedValueGenerator.cs
```

### ConfiguredRiskRuleProvider

**Categoría:** Service

**Descripción:** Entrega las reglas configuradas para evaluar cada tipo de sensor.

**Ruta del archivo:**

```text
src/Services/AlertService/Climate.Alerts.Infrastructure/Risk/ConfiguredRiskRuleProvider.cs
```

| Aviso en vivo | Qué comunica |
|---|---|
| SensorReadingUpdated | Nueva lectura de un sensor. |
| AlertGenerated | Alerta creada, actualizada o resuelta. |
| SensorStatusChanged | Sensor activado o desactivado. |
| SystemReset | La simulación se reinició y sus lecturas se eliminaron. |

La frecuencia y los rangos se definen en SimulationOptions; el valor predeterminado del intervalo es 5 segundos. SimulationService cambia el estado que consulta el worker. MonitoringService guarda lecturas → AlertEvaluationClient pide evaluar riesgos → AlertService llama EventHistoryClient → los avisos llegan a MonitoringHub. Esta secuencia describe llamadas del código, no una transacción única que abarque todos los servicios.

## 6. Modelos e interfaces que se usan al intercambiar datos

Un modelo indica qué datos forman una solicitud o una respuesta. Request es lo que recibe el backend; response es lo que devuelve. En el código C# estos modelos se declaran principalmente como record. Los nombres JSON usan camelCase: por ejemplo, SensorId se escribe sensorId. Los modelos del backend son la fuente de los ejemplos de este reporte.

| Nombre | Categoría | Descripción | Ruta |
|---|---|---|---|
| AlertResponse | Record | Datos de alerta y resolución. | src/Services/AlertService/Climate.Alerts.Application/Alerts/AlertModels.cs |
| AuditResponse | Record | Datos de una acción registrada. | src/Services/AuditService/Climate.Audit.Application/Auditing/AuditModels.cs |
| CommunityResponse | Record | Datos de la comunidad. | src/Services/SensorService/Climate.Sensors.Application/Communities/CommunityModels.cs |
| CreateCommunityRequest | Record | Datos para crear comunidad. | src/Services/SensorService/Climate.Sensors.Application/Communities/CommunityModels.cs |
| CreateReadingRequest | Record | Nueva medición de un sensor. | src/Services/MonitoringService/Climate.Monitoring.Application/Readings/ReadingModels.cs |
| CreateSensorRequest | Record | Datos para crear sensor. | src/Services/SensorService/Climate.Sensors.Application/Sensors/SensorModels.cs |
| EventResponse | Record | Datos del evento asociado a una alerta. | src/Services/EventService/Climate.Events.Application/Events/EventModels.cs |
| InternalRealtimeRequest | Record | Nombre del aviso y datos que se distribuirán en vivo. | src/Services/MonitoringService/Climate.Monitoring.Api/Controllers/InternalRealtimeController.cs |
| LoginRequest | Record | Datos para iniciar sesión. | src/Services/IdentityService/Climate.Identity.Application/Users/LoginRequest.cs |
| LoginResponse | Record | Token, vencimiento y datos de la cuenta. | src/Services/IdentityService/Climate.Identity.Application/Users/LoginResponse.cs |
| RecordAuditRequest | Record | Datos internos para guardar una acción. | src/Services/AuditService/Climate.Audit.Application/Auditing/AuditModels.cs |
| RecordClimateEventRequest | Record | Datos internos para guardar un evento. | src/Services/EventService/Climate.Events.Application/Events/EventModels.cs |
| RegisterRequest | Record | Datos para registrar una cuenta. | src/Services/IdentityService/Climate.Identity.Application/Users/RegisterRequest.cs |
| SensorChartResponse | Record | Sensor, unidad y puntos de gráfica. | src/Services/MonitoringService/Climate.Monitoring.Application/Readings/ReadingModels.cs |
| SensorReadingRecorded | Record | Aviso de lectura que se envía a evaluación de riesgos. | src/BuildingBlocks/Climate.Contracts/Monitoring/SensorReadingRecorded.cs |
| SensorReadingResponse | Record | Medición almacenada. | src/Services/MonitoringService/Climate.Monitoring.Application/Readings/ReadingModels.cs |
| SensorResponse | Record | Datos del sensor y su comunidad. | src/Services/SensorService/Climate.Sensors.Application/Sensors/SensorModels.cs |
| SensorSummary | Record | Datos de sensores activos para otros servicios. | src/BuildingBlocks/Climate.Contracts/Sensors/SensorSummary.cs |
| SimulationStatusResponse | Record | Indica si la simulación está activa. | src/Services/MonitoringService/Climate.Monitoring.Application/Readings/ReadingModels.cs |
| UpdateCommunityRequest | Record | Cambios de comunidad y estado. | src/Services/SensorService/Climate.Sensors.Application/Communities/CommunityModels.cs |
| UpdateSensorRequest | Record | Datos para actualizar sensor. | src/Services/SensorService/Climate.Sensors.Application/Sensors/SensorModels.cs |
| UpdateUserRequest | Record | Cambios de identidad y rol. | src/Services/IdentityService/Climate.Identity.Application/Users/UpdateUserRequest.cs |
| UpdateUserStatusRequest | Record | Nuevo estado de acceso. | src/Services/IdentityService/Climate.Identity.Application/Users/UpdateUserStatusRequest.cs |
| UserResponse | Record | Información pública de la cuenta, sin contraseña. | src/Services/IdentityService/Climate.Identity.Application/Users/UserResponse.cs |

**Estructuras adicionales:** ChartPoint representa fecha y valor de cada punto de gráfica. AlertFilter, EventFilter, AuditFilter e HistoryFilter agrupan filtros. RiskRule y RiskAssessment representan los límites y el resultado de evaluar riesgos. Se encuentran en los archivos de modelos de cada Application y en `src/Services/AlertService/Climate.Alerts.Application/Risk/Rules.cs`. Las interfaces de repositorios y clientes están dentro de Application/Abstractions; las interfaces de tareas principales están junto a sus servicios.

## 7. Controllers y endpoints

Un Controller recibe la solicitud que llega al backend, llama a la tarea correspondiente y devuelve la respuesta (response). Un endpoint es una combinación de verbo y dirección, como GET /api/sensors. GET consulta, POST crea o ejecuta una acción, PUT actualiza, PATCH cambia una parte y DELETE solicita una eliminación; el comportamiento real se explica por operación.

Las tablas separan la ruta directa del microservicio de la ruta pública del gateway. JWT significa protegido por JSON Web Token; donde se indica un rol también se exige JWT. «Clave interna» significa que se comprueba X-Internal-Api-Key, no que la operación sea pública sin protección. Un identificador entre llaves debe sustituirse por un GUID existente. Los ejemplos JSON son ilustrativos y no contienen credenciales reales; TOKEN_DE_EJEMPLO no es un token utilizable. 200 indica éxito, 201 creación, 202 aceptación y 204 éxito sin body. Los códigos no garantizan que cualquier ejemplo funcione contra una base concreta.

### AuthController

**Categoría:** Controller

**Descripción:** Recibe los datos de registro o inicio de sesión y devuelve la cuenta o el acceso.

**Ruta del archivo:**

```text
src/Services/IdentityService/Climate.Identity.Api/Controllers/AuthController.cs
```

| Verbo | Ruta del microservicio | Ruta del gateway | Acceso | Descripción |
|---|---|---|---|---|
| POST | /api/v1/auth/register | /api/auth/register | Público | Registra una cuenta nueva con rol Viewer. |
| POST | /api/v1/auth/login | /api/auth/login | Público | Comprueba usuario y contraseña y entrega el acceso. |

| Operación | Body request (JSON) | Response (JSON) | HTTP de éxito |
|---|---|---|---|
| POST /api/v1/auth/register | <pre>{&#10;  "username": "usuario_ejemplo",&#10;  "email": "usuario@example.com",&#10;  "password": "EjemploSeguro123"&#10;}</pre> | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "username": "usuario_ejemplo",&#10;  "email": "usuario@example.com",&#10;  "role": "Viewer",&#10;  "isActive": true,&#10;  "createdAt": "2026-09-09T12:00:00Z",&#10;  "updatedAt": "2026-09-09T12:00:00Z"&#10;}</pre> | 201 Created |
| POST /api/v1/auth/login | <pre>{&#10;  "login": "usuario@example.com",&#10;  "password": "EjemploSeguro123"&#10;}</pre> | <pre>{&#10;  "accessToken": "TOKEN_DE_EJEMPLO",&#10;  "expiresAt": "2026-09-09T13:00:00Z",&#10;  "user": {&#10;    "id": "11111111-1111-4111-8111-111111111111",&#10;    "username": "usuario_ejemplo",&#10;    "email": "usuario@example.com",&#10;    "role": "Viewer",&#10;    "isActive": true,&#10;    "createdAt": "2026-09-09T12:00:00Z",&#10;    "updatedAt": "2026-09-09T12:00:00Z"&#10;  }&#10;}</pre> | 200 OK |

### UsersController

**Categoría:** Controller

**Descripción:** Recibe consultas y cambios sobre cuentas de usuario.

**Ruta del archivo:**

```text
src/Services/IdentityService/Climate.Identity.Api/Controllers/UsersController.cs
```

| Verbo | Ruta del microservicio | Ruta del gateway | Acceso | Descripción |
|---|---|---|---|---|
| GET | /api/v1/users/me | /api/users/me | JWT | Consulta la cuenta de la persona conectada. |
| GET | /api/v1/users | /api/users | Administrator | Lista las cuentas registradas. |
| GET | /api/v1/users/{id} | /api/users/{id} | Administrator | Consulta una cuenta. |
| PUT | /api/v1/users/{id} | /api/users/{id} | Administrator | Cambia identidad y rol. |
| PATCH | /api/v1/users/{id}/status | /api/users/{id}/status | Administrator | Activa o desactiva una cuenta. |

| Operación | Body request (JSON) | Response (JSON) | HTTP de éxito |
|---|---|---|---|
| GET /api/v1/users/me | Sin body. | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "username": "usuario_ejemplo",&#10;  "email": "usuario@example.com",&#10;  "role": "Viewer",&#10;  "isActive": true,&#10;  "createdAt": "2026-09-09T12:00:00Z",&#10;  "updatedAt": "2026-09-09T12:00:00Z"&#10;}</pre> | 200 OK |
| GET /api/v1/users | Sin body. | <pre>[&#10;  {&#10;    "id": "11111111-1111-4111-8111-111111111111",&#10;    "username": "usuario_ejemplo",&#10;    "email": "usuario@example.com",&#10;    "role": "Viewer",&#10;    "isActive": true,&#10;    "createdAt": "2026-09-09T12:00:00Z",&#10;    "updatedAt": "2026-09-09T12:00:00Z"&#10;  }&#10;]</pre> | 200 OK |
| GET /api/v1/users/{id} | Sin body. | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "username": "usuario_ejemplo",&#10;  "email": "usuario@example.com",&#10;  "role": "Viewer",&#10;  "isActive": true,&#10;  "createdAt": "2026-09-09T12:00:00Z",&#10;  "updatedAt": "2026-09-09T12:00:00Z"&#10;}</pre> | 200 OK |
| PUT /api/v1/users/{id} | <pre>{&#10;  "username": "usuario_ejemplo",&#10;  "email": "usuario@example.com",&#10;  "role": "Viewer"&#10;}</pre> | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "username": "usuario_ejemplo",&#10;  "email": "usuario@example.com",&#10;  "role": "Viewer",&#10;  "isActive": true,&#10;  "createdAt": "2026-09-09T12:00:00Z",&#10;  "updatedAt": "2026-09-09T12:00:00Z"&#10;}</pre> | 200 OK |
| PATCH /api/v1/users/{id}/status | <pre>{&#10;  "isActive": true&#10;}</pre> | Sin body. | 204 No Content |

### CommunitiesController

**Categoría:** Controller

**Descripción:** Recibe consultas, altas y cambios de comunidades.

**Ruta del archivo:**

```text
src/Services/SensorService/Climate.Sensors.Api/Controllers/CommunitiesController.cs
```

| Verbo | Ruta del microservicio | Ruta del gateway | Acceso | Descripción |
|---|---|---|---|---|
| GET | /api/v1/communities | /api/communities | JWT | Lista comunidades. |
| GET | /api/v1/communities/{id} | /api/communities/{id} | JWT | Consulta una comunidad. |
| POST | /api/v1/communities | /api/communities | Administrator / Operator | Registra una comunidad. |
| PUT | /api/v1/communities/{id} | /api/communities/{id} | Administrator / Operator | Actualiza la comunidad y su estado. |

| Operación | Body request (JSON) | Response (JSON) | HTTP de éxito |
|---|---|---|---|
| GET /api/v1/communities | Sin body. | <pre>[&#10;  {&#10;    "id": "11111111-1111-4111-8111-111111111111",&#10;    "name": "Nombre de ejemplo",&#10;    "description": null,&#10;    "latitude": 0,&#10;    "longitude": 0,&#10;    "isActive": true,&#10;    "createdAt": "2026-09-09T12:00:00Z"&#10;  }&#10;]</pre> | 200 OK |
| GET /api/v1/communities/{id} | Sin body. | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "name": "Nombre de ejemplo",&#10;  "description": null,&#10;  "latitude": 0,&#10;  "longitude": 0,&#10;  "isActive": true,&#10;  "createdAt": "2026-09-09T12:00:00Z"&#10;}</pre> | 200 OK |
| POST /api/v1/communities | <pre>{&#10;  "name": "Nombre de ejemplo",&#10;  "description": null,&#10;  "latitude": 0,&#10;  "longitude": 0&#10;}</pre> | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "name": "Nombre de ejemplo",&#10;  "description": null,&#10;  "latitude": 0,&#10;  "longitude": 0,&#10;  "isActive": true,&#10;  "createdAt": "2026-09-09T12:00:00Z"&#10;}</pre> | 201 Created |
| PUT /api/v1/communities/{id} | <pre>{&#10;  "name": "Nombre de ejemplo",&#10;  "description": null,&#10;  "latitude": 0,&#10;  "longitude": 0,&#10;  "isActive": true&#10;}</pre> | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "name": "Nombre de ejemplo",&#10;  "description": null,&#10;  "latitude": 0,&#10;  "longitude": 0,&#10;  "isActive": true,&#10;  "createdAt": "2026-09-09T12:00:00Z"&#10;}</pre> | 200 OK |

### SensorsController

**Categoría:** Controller

**Descripción:** Recibe consultas, altas y cambios del catálogo de sensores.

**Ruta del archivo:**

```text
src/Services/SensorService/Climate.Sensors.Api/Controllers/SensorsController.cs
```

| Verbo | Ruta del microservicio | Ruta del gateway | Acceso | Descripción |
|---|---|---|---|---|
| GET | /api/v1/sensors | /api/sensors | JWT | Lista sensores. |
| GET | /api/v1/sensors/{id} | /api/sensors/{id} | JWT | Consulta un sensor. |
| POST | /api/v1/sensors | /api/sensors | Administrator / Operator | Registra un sensor. |
| PUT | /api/v1/sensors/{id} | /api/sensors/{id} | Administrator / Operator | Modifica los datos del sensor. |
| PATCH | /api/v1/sensors/{id}/activate | /api/sensors/{id}/activate | Administrator / Operator | Activa el sensor si su comunidad está activa. |
| PATCH | /api/v1/sensors/{id}/deactivate | /api/sensors/{id}/deactivate | Administrator / Operator | Desactiva el sensor. |
| DELETE | /api/v1/sensors/{id} | /api/sensors/{id} | Administrator / Operator | Desactiva el sensor; el código no lo borra físicamente. |

| Operación | Body request (JSON) | Response (JSON) | HTTP de éxito |
|---|---|---|---|
| GET /api/v1/sensors | Sin body. | <pre>[&#10;  {&#10;    "id": "11111111-1111-4111-8111-111111111111",&#10;    "name": "Nombre de ejemplo",&#10;    "code": "TEMP-001",&#10;    "description": null,&#10;    "type": "Temperature",&#10;    "unit": "°C",&#10;    "communityId": "11111111-1111-4111-8111-111111111111",&#10;    "communityName": "ejemplo",&#10;    "latitude": 0,&#10;    "longitude": 0,&#10;    "isActive": true,&#10;    "createdAt": "2026-09-09T12:00:00Z",&#10;    "updatedAt": "2026-09-09T12:00:00Z"&#10;  }&#10;]</pre> | 200 OK |
| GET /api/v1/sensors/{id} | Sin body. | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "name": "Nombre de ejemplo",&#10;  "code": "TEMP-001",&#10;  "description": null,&#10;  "type": "Temperature",&#10;  "unit": "°C",&#10;  "communityId": "11111111-1111-4111-8111-111111111111",&#10;  "communityName": "ejemplo",&#10;  "latitude": 0,&#10;  "longitude": 0,&#10;  "isActive": true,&#10;  "createdAt": "2026-09-09T12:00:00Z",&#10;  "updatedAt": "2026-09-09T12:00:00Z"&#10;}</pre> | 200 OK |
| POST /api/v1/sensors | <pre>{&#10;  "name": "Nombre de ejemplo",&#10;  "code": "TEMP-001",&#10;  "description": null,&#10;  "type": "Temperature",&#10;  "unit": "°C",&#10;  "communityId": "11111111-1111-4111-8111-111111111111",&#10;  "latitude": 0,&#10;  "longitude": 0&#10;}</pre> | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "name": "Nombre de ejemplo",&#10;  "code": "TEMP-001",&#10;  "description": null,&#10;  "type": "Temperature",&#10;  "unit": "°C",&#10;  "communityId": "11111111-1111-4111-8111-111111111111",&#10;  "communityName": "ejemplo",&#10;  "latitude": 0,&#10;  "longitude": 0,&#10;  "isActive": true,&#10;  "createdAt": "2026-09-09T12:00:00Z",&#10;  "updatedAt": "2026-09-09T12:00:00Z"&#10;}</pre> | 201 Created |
| PUT /api/v1/sensors/{id} | <pre>{&#10;  "name": "Nombre de ejemplo",&#10;  "code": "TEMP-001",&#10;  "description": null,&#10;  "type": "Temperature",&#10;  "unit": "°C",&#10;  "communityId": "11111111-1111-4111-8111-111111111111",&#10;  "latitude": 0,&#10;  "longitude": 0&#10;}</pre> | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "name": "Nombre de ejemplo",&#10;  "code": "TEMP-001",&#10;  "description": null,&#10;  "type": "Temperature",&#10;  "unit": "°C",&#10;  "communityId": "11111111-1111-4111-8111-111111111111",&#10;  "communityName": "ejemplo",&#10;  "latitude": 0,&#10;  "longitude": 0,&#10;  "isActive": true,&#10;  "createdAt": "2026-09-09T12:00:00Z",&#10;  "updatedAt": "2026-09-09T12:00:00Z"&#10;}</pre> | 200 OK |
| PATCH /api/v1/sensors/{id}/activate | Sin body. | Sin body. | 204 No Content |
| PATCH /api/v1/sensors/{id}/deactivate | Sin body. | Sin body. | 204 No Content |
| DELETE /api/v1/sensors/{id} | Sin body. | Sin body. | 204 No Content |

### MonitoringController

**Categoría:** Controller

**Descripción:** Recibe consultas de mediciones y operaciones de simulación.

**Ruta del archivo:**

```text
src/Services/MonitoringService/Climate.Monitoring.Api/Controllers/MonitoringController.cs
```

| Verbo | Ruta del microservicio | Ruta del gateway | Acceso | Descripción |
|---|---|---|---|---|
| GET | /api/v1/monitoring/current | /api/monitoring/current | JWT | Obtiene la última lectura disponible de cada sensor. |
| GET | /api/v1/monitoring/sensors/{sensorId}/latest | /api/monitoring/sensors/{sensorId}/latest | JWT | Obtiene la última lectura del sensor. |
| GET | /api/v1/monitoring/sensors/{sensorId}/history | /api/monitoring/sensors/{sensorId}/history | JWT | Consulta lecturas anteriores. |
| GET | /api/v1/monitoring/sensors/{sensorId}/chart | /api/monitoring/sensors/{sensorId}/chart | JWT | Calcula puntos de una gráfica agrupando lecturas. |
| POST | /api/v1/monitoring/readings | /api/monitoring/readings | Administrator / Operator | Registra una lectura de un sensor activo. |
| GET | /api/v1/monitoring/simulation/status | /api/monitoring/simulation/status | JWT | Consulta si la simulación está activa. |
| POST | /api/v1/monitoring/simulation/start | /api/monitoring/simulation/start | Administrator / Operator | Inicia la simulación. |
| POST | /api/v1/monitoring/simulation/stop | /api/monitoring/simulation/stop | Administrator / Operator | Detiene la simulación. |
| POST | /api/v1/monitoring/simulation/reset | /api/monitoring/simulation/reset | Administrator | Detiene la simulación y borra sus lecturas. |
| POST | /api/v1/monitoring/system/reset | /api/monitoring/system/reset | Administrator | Ejecuta el mismo reinicio de simulación y lecturas. |

**Filtros de consulta:** history: communityId, from y to. chart: from, to e interval (1m, 5m, 15m, 1h). Se envían en la URL, no en el body de GET.

| Operación | Body request (JSON) | Response (JSON) | HTTP de éxito |
|---|---|---|---|
| GET /api/v1/monitoring/current | Sin body. | <pre>[&#10;  {&#10;    "id": "11111111-1111-4111-8111-111111111111",&#10;    "sensorId": "11111111-1111-4111-8111-111111111111",&#10;    "communityId": "11111111-1111-4111-8111-111111111111",&#10;    "sensorType": "Temperature",&#10;    "value": 24.5,&#10;    "unit": "°C",&#10;    "recordedAt": "2026-09-09T12:00:00Z"&#10;  }&#10;]</pre> | 200 OK |
| GET /api/v1/monitoring/sensors/{sensorId}/latest | Sin body. | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "sensorId": "11111111-1111-4111-8111-111111111111",&#10;  "communityId": "11111111-1111-4111-8111-111111111111",&#10;  "sensorType": "Temperature",&#10;  "value": 24.5,&#10;  "unit": "°C",&#10;  "recordedAt": "2026-09-09T12:00:00Z"&#10;}</pre> | 200 OK |
| GET /api/v1/monitoring/sensors/{sensorId}/history | Sin body. | <pre>[&#10;  {&#10;    "id": "11111111-1111-4111-8111-111111111111",&#10;    "sensorId": "11111111-1111-4111-8111-111111111111",&#10;    "communityId": "11111111-1111-4111-8111-111111111111",&#10;    "sensorType": "Temperature",&#10;    "value": 24.5,&#10;    "unit": "°C",&#10;    "recordedAt": "2026-09-09T12:00:00Z"&#10;  }&#10;]</pre> | 200 OK |
| GET /api/v1/monitoring/sensors/{sensorId}/chart | Sin body. | <pre>{&#10;  "sensorId": "11111111-1111-4111-8111-111111111111",&#10;  "unit": "°C",&#10;  "data": [&#10;    {&#10;      "timestamp": "2026-09-09T12:00:00Z",&#10;      "value": 24.5&#10;    }&#10;  ]&#10;}</pre> | 200 OK |
| POST /api/v1/monitoring/readings | <pre>{&#10;  "sensorId": "11111111-1111-4111-8111-111111111111",&#10;  "value": 24.5,&#10;  "recordedAt": null&#10;}</pre> | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "sensorId": "11111111-1111-4111-8111-111111111111",&#10;  "communityId": "11111111-1111-4111-8111-111111111111",&#10;  "sensorType": "Temperature",&#10;  "value": 24.5,&#10;  "unit": "°C",&#10;  "recordedAt": "2026-09-09T12:00:00Z"&#10;}</pre> | 201 Created |
| GET /api/v1/monitoring/simulation/status | Sin body. | <pre>{&#10;  "isRunning": true&#10;}</pre> | 200 OK |
| POST /api/v1/monitoring/simulation/start | Sin body. | <pre>{&#10;  "isRunning": true&#10;}</pre> | 200 OK |
| POST /api/v1/monitoring/simulation/stop | Sin body. | <pre>{&#10;  "isRunning": false&#10;}</pre> | 200 OK |
| POST /api/v1/monitoring/simulation/reset | Sin body. | <pre>{&#10;  "isRunning": false&#10;}</pre> | 200 OK |
| POST /api/v1/monitoring/system/reset | Sin body. | <pre>{&#10;  "isRunning": false&#10;}</pre> | 200 OK |

### AlertsController

**Categoría:** Controller

**Descripción:** Recibe consultas de alertas y solicitudes de resolución.

**Ruta del archivo:**

```text
src/Services/AlertService/Climate.Alerts.Api/Controllers/AlertsController.cs
```

| Verbo | Ruta del microservicio | Ruta del gateway | Acceso | Descripción |
|---|---|---|---|---|
| GET | /api/v1/alerts | /api/alerts | JWT | Busca alertas. |
| GET | /api/v1/alerts/{id} | /api/alerts/{id} | JWT | Consulta una alerta. |
| PATCH | /api/v1/alerts/{id}/resolve | /api/alerts/{id}/resolve | Administrator / Operator | Marca una alerta como resuelta. |

**Filtros de consulta:** riskType, alertLevel, sensorId, communityId e isActive. Se envían en la URL, no en el body de GET.

| Operación | Body request (JSON) | Response (JSON) | HTTP de éxito |
|---|---|---|---|
| GET /api/v1/alerts | Sin body. | <pre>[&#10;  {&#10;    "id": "11111111-1111-4111-8111-111111111111",&#10;    "sensorId": "11111111-1111-4111-8111-111111111111",&#10;    "communityId": "11111111-1111-4111-8111-111111111111",&#10;    "alertType": "Drought",&#10;    "level": "Yellow",&#10;    "title": "Alerta de ejemplo",&#10;    "description": "Descripción de ejemplo",&#10;    "sensorValue": 24.5,&#10;    "thresholdValue": 30,&#10;    "generatedAt": "2026-09-09T12:00:00Z",&#10;    "isActive": true,&#10;    "resolvedAt": null&#10;  }&#10;]</pre> | 200 OK |
| GET /api/v1/alerts/{id} | Sin body. | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "sensorId": "11111111-1111-4111-8111-111111111111",&#10;  "communityId": "11111111-1111-4111-8111-111111111111",&#10;  "alertType": "Drought",&#10;  "level": "Yellow",&#10;  "title": "Alerta de ejemplo",&#10;  "description": "Descripción de ejemplo",&#10;  "sensorValue": 24.5,&#10;  "thresholdValue": 30,&#10;  "generatedAt": "2026-09-09T12:00:00Z",&#10;  "isActive": true,&#10;  "resolvedAt": null&#10;}</pre> | 200 OK |
| PATCH /api/v1/alerts/{id}/resolve | Sin body. | Sin body. | 204 No Content |

### EventsController

**Categoría:** Controller

**Descripción:** Recibe consultas del historial de eventos.

**Ruta del archivo:**

```text
src/Services/EventService/Climate.Events.Api/Controllers/EventsController.cs
```

| Verbo | Ruta del microservicio | Ruta del gateway | Acceso | Descripción |
|---|---|---|---|---|
| GET | /api/v1/events | /api/events | JWT | Busca eventos climáticos. |
| GET | /api/v1/events/{id} | /api/events/{id} | JWT | Consulta un evento. |

**Filtros de consulta:** riskType, alertLevel, sensorId, communityId, from y to. Se envían en la URL, no en el body de GET.

| Operación | Body request (JSON) | Response (JSON) | HTTP de éxito |
|---|---|---|---|
| GET /api/v1/events | Sin body. | <pre>[&#10;  {&#10;    "id": "11111111-1111-4111-8111-111111111111",&#10;    "alertId": "11111111-1111-4111-8111-111111111111",&#10;    "sensorId": "11111111-1111-4111-8111-111111111111",&#10;    "communityId": "11111111-1111-4111-8111-111111111111",&#10;    "riskType": "Drought",&#10;    "alertLevel": "Yellow",&#10;    "description": "Descripción de ejemplo",&#10;    "occurredAt": "2026-09-09T12:00:00Z",&#10;    "resolvedAt": null&#10;  }&#10;]</pre> | 200 OK |
| GET /api/v1/events/{id} | Sin body. | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "alertId": "11111111-1111-4111-8111-111111111111",&#10;  "sensorId": "11111111-1111-4111-8111-111111111111",&#10;  "communityId": "11111111-1111-4111-8111-111111111111",&#10;  "riskType": "Drought",&#10;  "alertLevel": "Yellow",&#10;  "description": "Descripción de ejemplo",&#10;  "occurredAt": "2026-09-09T12:00:00Z",&#10;  "resolvedAt": null&#10;}</pre> | 200 OK |

### AuditController

**Categoría:** Controller

**Descripción:** Recibe consultas administrativas de la bitácora.

**Ruta del archivo:**

```text
src/Services/AuditService/Climate.Audit.Api/Controllers/AuditController.cs
```

| Verbo | Ruta del microservicio | Ruta del gateway | Acceso | Descripción |
|---|---|---|---|---|
| GET | /api/v1/audit | /api/audit | Administrator | Busca acciones registradas. |
| GET | /api/v1/audit/{id} | /api/audit/{id} | Administrator | Consulta una acción de la bitácora. |

**Filtros de consulta:** userId, action, resource, from y to. Se envían en la URL, no en el body de GET.

| Operación | Body request (JSON) | Response (JSON) | HTTP de éxito |
|---|---|---|---|
| GET /api/v1/audit | Sin body. | <pre>[&#10;  {&#10;    "id": "11111111-1111-4111-8111-111111111111",&#10;    "userId": "11111111-1111-4111-8111-111111111111",&#10;    "userName": "usuario_ejemplo",&#10;    "action": "UpdateSensor",&#10;    "resource": "Sensor",&#10;    "resourceId": null,&#10;    "description": "Descripción de ejemplo",&#10;    "ipAddress": null,&#10;    "timestamp": "2026-09-09T12:00:00Z"&#10;  }&#10;]</pre> | 200 OK |
| GET /api/v1/audit/{id} | Sin body. | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "userId": "11111111-1111-4111-8111-111111111111",&#10;  "userName": "usuario_ejemplo",&#10;  "action": "UpdateSensor",&#10;  "resource": "Sensor",&#10;  "resourceId": null,&#10;  "description": "Descripción de ejemplo",&#10;  "ipAddress": null,&#10;  "timestamp": "2026-09-09T12:00:00Z"&#10;}</pre> | 200 OK |

### InternalSensorsController

**Categoría:** Controller

**Descripción:** Entrega a otros servicios el catálogo de sensores activos.

**Ruta del archivo:**

```text
src/Services/SensorService/Climate.Sensors.Api/Controllers/InternalSensorsController.cs
```

| Verbo | Ruta del microservicio | Ruta del gateway | Acceso | Descripción |
|---|---|---|---|---|
| GET | /api/v1/internal/sensors/active | No expuesta por el gateway. | Clave interna | Entrega el catálogo de sensores activos a otros servicios. |

| Operación | Body request (JSON) | Response (JSON) | HTTP de éxito |
|---|---|---|---|
| GET /api/v1/internal/sensors/active | Sin body. | <pre>[&#10;  {&#10;    "id": "11111111-1111-4111-8111-111111111111",&#10;    "communityId": "11111111-1111-4111-8111-111111111111",&#10;    "name": "Nombre de ejemplo",&#10;    "code": "TEMP-001",&#10;    "type": "Temperature",&#10;    "unit": "°C",&#10;    "isActive": true&#10;  }&#10;]</pre> | 200 OK |

### InternalEvaluationController

**Categoría:** Controller

**Descripción:** Recibe lecturas de otro servicio para evaluar riesgos.

**Ruta del archivo:**

```text
src/Services/AlertService/Climate.Alerts.Api/Controllers/InternalEvaluationController.cs
```

| Verbo | Ruta del microservicio | Ruta del gateway | Acceso | Descripción |
|---|---|---|---|---|
| POST | /api/v1/internal/risk-evaluations | No expuesta por el gateway. | Clave interna | Evalúa los riesgos de una lectura. |

| Operación | Body request (JSON) | Response (JSON) | HTTP de éxito |
|---|---|---|---|
| POST /api/v1/internal/risk-evaluations | <pre>{&#10;  "eventId": "11111111-1111-4111-8111-111111111111",&#10;  "occurredAt": "2026-09-09T12:00:00Z",&#10;  "correlationId": "ejemplo",&#10;  "readingId": "11111111-1111-4111-8111-111111111111",&#10;  "sensorId": "11111111-1111-4111-8111-111111111111",&#10;  "communityId": "11111111-1111-4111-8111-111111111111",&#10;  "sensorType": "Temperature",&#10;  "value": 24.5,&#10;  "unit": "°C",&#10;  "recordedAt": "2026-09-09T12:00:00Z"&#10;}</pre> | <pre>[&#10;  {&#10;    "id": "11111111-1111-4111-8111-111111111111",&#10;    "sensorId": "11111111-1111-4111-8111-111111111111",&#10;    "communityId": "11111111-1111-4111-8111-111111111111",&#10;    "alertType": "Drought",&#10;    "level": "Yellow",&#10;    "title": "Alerta de ejemplo",&#10;    "description": "Descripción de ejemplo",&#10;    "sensorValue": 24.5,&#10;    "thresholdValue": 30,&#10;    "generatedAt": "2026-09-09T12:00:00Z",&#10;    "isActive": true,&#10;    "resolvedAt": null&#10;  }&#10;]</pre> | 200 OK |

### InternalEventsController

**Categoría:** Controller

**Descripción:** Recibe el estado de alertas para conservar sus eventos.

**Ruta del archivo:**

```text
src/Services/EventService/Climate.Events.Api/Controllers/InternalEventsController.cs
```

| Verbo | Ruta del microservicio | Ruta del gateway | Acceso | Descripción |
|---|---|---|---|---|
| POST | /api/v1/internal/events | No expuesta por el gateway. | Clave interna | Registra o actualiza un evento de alerta. |

| Operación | Body request (JSON) | Response (JSON) | HTTP de éxito |
|---|---|---|---|
| POST /api/v1/internal/events | <pre>{&#10;  "eventId": "11111111-1111-4111-8111-111111111111",&#10;  "alertId": "11111111-1111-4111-8111-111111111111",&#10;  "sensorId": "11111111-1111-4111-8111-111111111111",&#10;  "communityId": "11111111-1111-4111-8111-111111111111",&#10;  "riskType": "Drought",&#10;  "alertLevel": "Yellow",&#10;  "description": "Descripción de ejemplo",&#10;  "occurredAt": "2026-09-09T12:00:00Z",&#10;  "resolvedAt": null&#10;}</pre> | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "alertId": "11111111-1111-4111-8111-111111111111",&#10;  "sensorId": "11111111-1111-4111-8111-111111111111",&#10;  "communityId": "11111111-1111-4111-8111-111111111111",&#10;  "riskType": "Drought",&#10;  "alertLevel": "Yellow",&#10;  "description": "Descripción de ejemplo",&#10;  "occurredAt": "2026-09-09T12:00:00Z",&#10;  "resolvedAt": null&#10;}</pre> | 200 OK |

### InternalAuditController

**Categoría:** Controller

**Descripción:** Recibe acciones para guardarlas en la bitácora.

**Ruta del archivo:**

```text
src/Services/AuditService/Climate.Audit.Api/Controllers/InternalAuditController.cs
```

| Verbo | Ruta del microservicio | Ruta del gateway | Acceso | Descripción |
|---|---|---|---|---|
| POST | /api/v1/internal/audit | No expuesta por el gateway. | Clave interna | Registra una acción de usuario. |

| Operación | Body request (JSON) | Response (JSON) | HTTP de éxito |
|---|---|---|---|
| POST /api/v1/internal/audit | <pre>{&#10;  "eventId": "11111111-1111-4111-8111-111111111111",&#10;  "userId": "11111111-1111-4111-8111-111111111111",&#10;  "userName": "usuario_ejemplo",&#10;  "action": "UpdateSensor",&#10;  "resource": "Sensor",&#10;  "resourceId": null,&#10;  "description": "Descripción de ejemplo",&#10;  "ipAddress": null,&#10;  "timestamp": "2026-09-09T12:00:00Z"&#10;}</pre> | <pre>{&#10;  "id": "11111111-1111-4111-8111-111111111111",&#10;  "userId": "11111111-1111-4111-8111-111111111111",&#10;  "userName": "usuario_ejemplo",&#10;  "action": "UpdateSensor",&#10;  "resource": "Sensor",&#10;  "resourceId": null,&#10;  "description": "Descripción de ejemplo",&#10;  "ipAddress": null,&#10;  "timestamp": "2026-09-09T12:00:00Z"&#10;}</pre> | 200 OK |

### InternalRealtimeController

**Categoría:** Controller

**Descripción:** Recibe avisos de otros servicios y los distribuye a los clientes conectados.

**Ruta del archivo:**

```text
src/Services/MonitoringService/Climate.Monitoring.Api/Controllers/InternalRealtimeController.cs
```

| Verbo | Ruta del microservicio | Ruta del gateway | Acceso | Descripción |
|---|---|---|---|---|
| POST | /api/v1/internal/realtime | No expuesta por el gateway. | Clave interna | Publica un aviso de nombre permitido a las conexiones en vivo. |

| Operación | Body request (JSON) | Response (JSON) | HTTP de éxito |
|---|---|---|---|
| POST /api/v1/internal/realtime | <pre>{&#10;  "eventName": "SystemReset",&#10;  "payload": {&#10;    "isRunning": false&#10;  }&#10;}</pre> | Sin body. | 202 Accepted |

**Otros puntos de acceso:** cada API publica /health para revisar su conexión de base de datos. El gateway publica /health mediante DownstreamHealthCheck y HealthResponseWriter en `src/Gateway/Climate.Gateway/Health/`; consulta los servicios configurados y devuelve un resumen. Swagger permite consultar la descripción de las APIs: las APIs individuales lo habilitan en desarrollo; el gateway publica su interfaz en /swagger. La conexión en vivo se abre en /hubs/monitoring y está protegida por JWT.
