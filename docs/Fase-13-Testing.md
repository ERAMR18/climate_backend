# Fase 13 — Pruebas automatizadas

## Objetivo

La solución dispone de pruebas unitarias rápidas con xUnit y Moq para las reglas críticas. No necesitan SQL Server, Docker ni servicios externos: repositorios y clientes se sustituyen por mocks o fakes en memoria.

## Estructura

```text
tests/
├── Climate.BuildingBlocks.Tests
├── Climate.Identity.Tests
├── Climate.Sensors.Tests
├── Climate.Monitoring.Tests
├── Climate.Alerts.Tests
├── Climate.Events.Tests
├── Climate.Audit.Tests
└── coverage.runsettings
```

## Matriz requerida

| Requisito | Pruebas principales |
|---|---|
| Login | acceso válido, contraseña inválida y usuario inactivo |
| Creación de sensores | persistencia, comunidad existente y código duplicado |
| Activación/desactivación | borrado lógico y comunidad inactiva |
| Generación de lecturas | metadata del catálogo, sensor inexistente y lote simulado |
| Evaluación de riesgos | reglas ascendentes, descendentes y múltiples estrategias |
| Clasificación de alertas | límites Green, Yellow, Orange y Red |
| Registro de eventos | creación, timestamp, idempotencia y resolución |
| Reinicio del sistema | detiene simulación y elimina solo lecturas |
| Validaciones | contraseñas, roles, coordenadas, tipos, IDs y fechas futuras |

También se cubren:

- normalización y transiciones de entidades de dominio;
- actualización y reactivación de alertas;
- publicación de lecturas en tiempo real;
- auditoría idempotente;
- contratos compartidos, resultados y paginación;
- períodos invertidos y agregación de gráficas.

## Ejecución

Suite completa:

```bash
dotnet test ClimateMonitoringSystem.sln
```

Sin recompilar después de un build exitoso:

```bash
dotnet test ClimateMonitoringSystem.sln --no-build --no-restore
```

Proyecto individual:

```bash
dotnet test tests/Climate.Alerts.Tests
```

Filtro:

```bash
dotnet test ClimateMonitoringSystem.sln --filter "FullyQualifiedName~RiskEvaluationServiceTests"
```

## Cobertura

`tests/coverage.runsettings` genera Cobertura para Application, Domain, SharedKernel y Contracts, excluyendo proyectos de pruebas, migraciones y código generado:

```bash
dotnet test ClimateMonitoringSystem.sln \
  --settings tests/coverage.runsettings \
  --collect:"XPlat Code Coverage" \
  --results-directory TestResults/Coverage
```

Medición de esta fase:

| Proyecto | Líneas | Ramas |
|---|---:|---:|
| Alerts.Application | 80.2% | 85.3% |
| Alerts.Domain | 91.7% | 100% |
| Audit.Application | 89.5% | 93.8% |
| Audit.Domain | 90.0% | 50% |
| Events.Application | 96.2% | 92.8% |
| Events.Domain | 92.8% | 50% |
| Identity.Application | 66.2% | 42.8% |
| Identity.Domain | 93.8% | 100% |
| Monitoring.Application | 76.9% | 55.3% |
| Monitoring.Domain | 83.3% | 100% |
| Sensors.Application | 64.0% | 31.1% |
| Sensors.Domain | 91.8% | 100% |

Los porcentajes son una fotografía, no un sustituto de verificar comportamiento. Infrastructure y API requieren pruebas de integración con host y base reales; no se mezclan con esta métrica unitaria.

## Resultado

```text
BuildingBlocks: 17
Identity:       14
Sensors:        16
Monitoring:     11
Alerts:         15
Events:          5
Audit:           4
Total:          82
```

Todas las pruebas se ejecutan con analizadores estrictos, nullable habilitado y advertencias tratadas como errores.

## Alcance

Esta fase completa la suite unitaria solicitada. Pruebas end-to-end de contenedores y carga sostenida son complementarias y no sustituyen estas pruebas deterministas.
