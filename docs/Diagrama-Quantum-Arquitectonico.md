# Diagrama de quantums arquitectónicos

Un **quantum arquitectónico** es una unidad desplegable de alta cohesión funcional que incluye el código y los datos necesarios para cumplir una capacidad de negocio. En este sistema, cada microservicio y su base de datos lógica forman un quantum independiente. El Gateway constituye un quantum técnico sin datos propios.

## Vista de quantums y acoplamientos

```mermaid
flowchart LR
    UI["Frontend Angular"]

    subgraph QG["Quantum técnico: API Gateway"]
        GW["YARP Reverse Proxy<br/>JWT · CORS · Rate limiting<br/>Health agregado"]
    end

    subgraph QI["Q1 · Identidad"]
        ID["Identity Service<br/>Usuarios · Roles · JWT"]
        IDDB[("IdentityDb")]
        ID --> IDDB
    end

    subgraph QS["Q2 · Catálogo de sensores"]
        SS["Sensor Service<br/>Comunidades · Sensores"]
        SSDB[("SensorDb")]
        SS --> SSDB
    end

    subgraph QM["Q3 · Monitoreo y tiempo real"]
        MS["Monitoring Service<br/>Lecturas · Simulación · Estadísticas"]
        HUB["SignalR Hub"]
        MSDB[("MonitoringDb")]
        MS --> MSDB
        MS --> HUB
    end

    subgraph QA["Q4 · Alertas"]
        AS["Alert Service<br/>Evaluación de riesgo · Alertas"]
        ASDB[("AlertDb")]
        AS --> ASDB
    end

    subgraph QE["Q5 · Eventos climáticos"]
        ES["Event Service<br/>Historial de eventos"]
        ESDB[("EventDb")]
        ES --> ESDB
    end

    subgraph QU["Q6 · Auditoría"]
        AU["Audit Service<br/>Trazabilidad administrativa"]
        AUDB[("AuditDb")]
        AU --> AUDB
    end

    SQL["SQL Server 2022<br/>infraestructura física compartida"]
    BB["Building Blocks<br/>Contracts + SharedKernel"]

    UI -->|"HTTPS · REST · JWT"| GW
    UI <-->|"WebSocket / SignalR vía Gateway"| GW

    GW -->|"proxy"| ID
    GW -->|"proxy"| SS
    GW -->|"proxy"| MS
    GW -->|"proxy"| AS
    GW -->|"proxy"| ES
    GW -->|"proxy"| AU
    GW <-->|"upgrade WebSocket"| HUB

    MS -->|"HTTP interno: sensores activos"| SS
    MS -->|"HTTP interno: evaluar lectura"| AS
    AS -->|"HTTP interno: registrar evento"| ES
    ID -.->|"HTTP interno: auditar"| AU
    SS -.->|"HTTP interno: auditar"| AU
    MS -.->|"HTTP interno: auditar"| AU
    SS -.->|"HTTP interno: estado de sensor"| HUB
    AS -.->|"HTTP interno: alerta"| HUB

    IDDB --- SQL
    SSDB --- SQL
    MSDB --- SQL
    ASDB --- SQL
    ESDB --- SQL
    AUDB --- SQL

    BB -.->|"acoplamiento de compilación"| ID
    BB -.->|"acoplamiento de compilación"| SS
    BB -.->|"acoplamiento de compilación"| MS
    BB -.->|"acoplamiento de compilación"| AS
    BB -.->|"acoplamiento de compilación"| ES
    BB -.->|"acoplamiento de compilación"| AU

    classDef quantum fill:#e8f1ff,stroke:#2563eb,stroke-width:2px,color:#111827;
    classDef data fill:#fff7d6,stroke:#ca8a04,color:#111827;
    classDef shared fill:#f3e8ff,stroke:#9333ea,color:#111827;
    class GW,ID,SS,MS,HUB,AS,ES,AU quantum;
    class IDDB,SSDB,MSDB,ASDB,ESDB,AUDB data;
    class SQL,BB shared;
```

## Anatomía común de un quantum de negocio

```mermaid
flowchart TB
    subgraph Q["Quantum desplegable de un microservicio"]
        API["API<br/>Controllers · autenticación · endpoints"]
        APP["Application<br/>Casos de uso · puertos · validación"]
        DOM["Domain<br/>Entidades · reglas de negocio"]
        INF["Infrastructure<br/>EF Core · repositorios · clientes HTTP"]
        DB[("Base de datos propietaria")]

        API --> APP
        API --> INF
        APP --> DOM
        INF --> APP
        INF --> DOM
        INF --> DB
    end

    CONTRACTS["Climate.Contracts"] -.-> API
    KERNEL["Climate.SharedKernel"] -.-> APP
    OTHER["Otros quantums"] <-->|"REST interno + API key<br/>X-Correlation-ID"| API
```

## Límites identificados

| Quantum | Artefacto desplegable | Datos propietarios | Acoplamiento dinámico saliente |
|---|---|---|---|
| Gateway | `Climate.Gateway` | Ninguno | Health checks y proxy hacia los seis servicios |
| Identidad | `Climate.Identity.Api` | `IdentityDb` | Audit |
| Sensores | `Climate.Sensors.Api` | `SensorDb` | Audit y Realtime en Monitoring |
| Monitoreo | `Climate.Monitoring.Api` | `MonitoringDb` | Sensors, Alerts y Audit |
| Alertas | `Climate.Alerts.Api` | `AlertDb` | Events y Realtime en Monitoring |
| Eventos | `Climate.Events.Api` | `EventDb` | Ninguno |
| Auditoría | `Climate.Audit.Api` | `AuditDb` | Ninguno |

Aunque las seis bases se alojan en una sola instancia de SQL Server, son límites lógicos separados: ningún servicio comparte tablas o `DbContext`. `Climate.Contracts` y `Climate.SharedKernel` son dependencias de compilación comunes, no unidades desplegables ni propietarios de datos.

El flujo `Monitoring → Alerts → Events`, al ser HTTP síncrono, forma el acoplamiento operativo más importante entre quantums. La auditoría y las notificaciones en tiempo real usan también HTTP interno; una evolución hacia mensajería y Outbox reduciría ese acoplamiento temporal sin cambiar los límites de dominio.
