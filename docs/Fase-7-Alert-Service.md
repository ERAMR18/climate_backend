# Fase 7 — Alert Service

## 1. Objetivo

Alert Service recibe lecturas ya persistidas por Monitoring, evalúa reglas climáticas configurables, mantiene el ciclo de vida de alertas y expone consultas filtradas. Es propietario de `AlertDb` y no consulta directamente `MonitoringDb` ni `SensorDb`.

Los valores iniciales son exclusivamente de demostración. No representan criterios oficiales de protección civil, meteorología o gestión de riesgos.

## 2. Flujo implementado

```text
Monitoring guarda SensorReading
             │
             ▼
SensorReadingRecorded
             │ HTTP + X-Internal-Api-Key
             ▼
Alert Service
             │
             ▼
IRiskEvaluationService
             │
             ├── Green  → resolver alerta activa, si existe
             └── Yellow/Orange/Red
                         │
                         ├── crear alerta si no existe
                         └── actualizar alerta activa existente
```

Esto evita crear una alerta nueva en cada ciclo de cinco segundos para el mismo sensor y fenómeno.

## 3. Estructura

```text
Climate.Alerts.Api/
├── Configuration/SecurityOptions.cs
├── Controllers/
│   ├── AlertsController.cs
│   └── InternalEvaluationController.cs
├── Errors/
└── Program.cs

Climate.Alerts.Application/
├── Abstractions/IAlertRepository.cs
├── Alerts/
│   ├── AlertModels.cs
│   ├── AlertService.cs
│   └── IAlertService.cs
├── Risk/
│   ├── Rules.cs
│   └── RiskEvaluationService.cs
└── DependencyInjection.cs

Climate.Alerts.Domain/
└── Alerts/
    ├── ClimateAlert.cs
    ├── AlertLevel.cs
    └── RiskType.cs

Climate.Alerts.Infrastructure/
├── Persistence/
│   ├── AlertsDbContext.cs
│   ├── AlertConfiguration.cs
│   ├── AlertRepository.cs
│   └── Migrations/InitialAlerts
└── Risk/
    ├── RiskRulesOptions.cs
    └── ConfiguredRiskRuleProvider.cs
```

## 4. Niveles y fenómenos

Niveles:

```text
Green  → Normal
Yellow → Precaución
Orange → Alerta
Red    → Emergencia
```

Fenómenos:

```text
Flood
Drought
Storm
Frost
ForestFire
```

Una evaluación Green no se persiste como alerta nueva. Se utiliza para cerrar una alerta activa cuando la condición vuelve al rango normal.

## 5. Motor de reglas

`IRiskEvaluationService` no conoce JSON ni EF Core. Obtiene reglas desde `IRiskRuleProvider`. La implementación actual carga reglas mediante Options Pattern, pero puede reemplazarse por un repositorio de base de datos sin modificar controllers o casos de uso.

Cada regla declara:

- tipo de sensor;
- fenómeno;
- dirección `Increasing` o `Decreasing`;
- umbrales Yellow, Orange y Red;
- título y descripción.

Una lectura puede activar varias estrategias. Por ejemplo, temperatura participa en `Frost`, `Drought` y `ForestFire`; lluvia participa en `Flood` y `Drought`.

## 6. Reglas demostrativas iniciales

| Sensor | Riesgo | Dirección | Yellow | Orange | Red |
|---|---|---:|---:|---:|---:|
| WaterLevel | Flood | Aumenta | 4.0 | 5.5 | 7.0 |
| Rainfall | Flood | Aumenta | 30 | 50 | 70 |
| Rainfall | Drought | Disminuye | 10 | 5 | 1 |
| WindSpeed | Storm | Aumenta | 40 | 70 | 100 |
| Temperature | Frost | Disminuye | 5 | 2 | 0 |
| Temperature | ForestFire | Aumenta | 32 | 38 | 43 |
| Humidity | ForestFire | Disminuye | 35 | 25 | 15 |
| Temperature | Drought | Aumenta | 30 | 36 | 42 |
| Humidity | Drought | Disminuye | 40 | 30 | 20 |

Estas reglas se encuentran en `Climate.Alerts.Api/appsettings.json`. La configuración se valida al inicio para impedir umbrales desordenados según su dirección.

Las evaluaciones son por lectura individual. Una evolución futura podrá introducir ventanas temporales y reglas compuestas por comunidad manteniendo las mismas abstracciones.

## 7. Persistencia

`ClimateAlert` almacena:

- sensor y comunidad externos;
- fenómeno y nivel;
- título y descripción;
- valor observado y umbral aplicado;
- fecha de generación;
- estado activo y fecha de resolución.

Existen índices por sensor/riesgo/estado, comunidad/estado, nivel/estado y fecha. La migración es `InitialAlerts`.

## 8. Endpoints

| Método | Ruta | Acceso |
|---|---|---|
| GET | `/api/v1/alerts` | Autenticado |
| GET | `/api/v1/alerts/{id}` | Autenticado |
| PATCH | `/api/v1/alerts/{id}/resolve` | Administrator u Operator |
| POST | `/api/v1/internal/risk-evaluations` | Clave interna |
| GET | `/health` | Público |

`GET /api/v1/alerts` admite:

```text
riskType
alertLevel
sensorId
communityId
isActive
```

La ruta interna no se publica en Swagger y exige `X-Internal-Api-Key`.

## 9. Integración con Monitoring

Monitoring construye `SensorReadingRecorded` después de confirmar la escritura en `MonitoringDb`. Luego llama Alert Service con un cliente HTTP tipado.

Configuración de Monitoring:

```text
AlertService__BaseUrl
AlertService__ApiKey
```

Configuración de Alert Service:

```text
InternalApi__ApiKey
```

Ambas claves deben coincidir y tener al menos 32 caracteres. Si la evaluación falla, el worker registra el error y reintenta su siguiente ciclo; la lectura ya guardada no se elimina mediante una transacción distribuida.

## 10. Seguridad

- JWT validado nuevamente en Alert Service.
- Consulta disponible para roles autenticados.
- Resolución manual limitada a Administrator y Operator.
- Endpoint interno protegido con una clave independiente del JWT de usuarios.
- Secretos ausentes de `appsettings.json`.
- Problem Details sin stack trace en producción.

## 11. Ejecución y migración

```powershell
$env:ConnectionStrings__AlertDb='Server=localhost,1433;Database=AlertDb;User Id=sa;Password=REPLACE_ME;TrustServerCertificate=True'
$env:Jwt__SigningKey='THE_SAME_KEY_USED_BY_IDENTITY'
$env:InternalApi__ApiKey='A_RANDOM_INTERNAL_KEY_WITH_AT_LEAST_32_CHARACTERS'

dotnet run --project src/Services/AlertService/Climate.Alerts.Api
```

```powershell
dotnet tool run dotnet-ef database update `
  --project src/Services/AlertService/Climate.Alerts.Infrastructure `
  --startup-project src/Services/AlertService/Climate.Alerts.Api
```

## 12. Pruebas

Las pruebas verifican:

- clasificación ascendente y descendente;
- límites Green, Yellow, Orange y Red;
- múltiples riesgos para un tipo de sensor;
- creación de alertas;
- actualización sin duplicados activos;
- resolución automática al volver a Green;
- resolución manual de alertas inexistentes.

## 13. Límite de la fase

Event Service todavía no está implementado, por lo que la creación y resolución no intentan escribir directamente en `EventDb`. Esa integración explícita se añadirá en la Fase 8. `AlertGenerated` se emitirá por SignalR en la Fase 10.

La siguiente etapa es la **Fase 8 — Event Service** y debe comenzar sólo cuando se solicite.
