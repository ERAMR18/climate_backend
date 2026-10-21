# Fase 3 — Cierre del alcance local

Fecha: 2026-10-21. **Implementación funcional terminada y validada localmente.**

**Instalación local iniciada y verificada:** proyecto climate-phase3-local,17 contenedores saludables, frontend http://localhost:4200 y Gateway http://localhost:8080. Login admin verificado sin imprimir contraseña; ocho tipos de reporte y dashboard vacío de instalación nueva comprobados. Los ocho volúmenes de datos pertenecen al proyecto nuevo. Evidencia: backend/docs/validation/phase3-local-runtime.json. La aplicación queda ejecutándose; los proyectos de prueba quedaron detenidos.

## Alcance vigente

El usuario excluyó VPS y no exige conservar ni transferir datos anteriores. La instalación nueva funciona sin importar históricos. No se borraron bases existentes. Videos, publicaciones externas, fuente documental no disponible y preparación Fase4 se registran en [FASE_3_FUERA_DE_ALCANCE.md](backend/docs/FASE_3_FUERA_DE_ALCANCE.md).

## Resultado

- Auth NestJS/PostgreSQL, Community .NET/PostgreSQL, Alert Spring Boot/PostgreSQL, Audit FastAPI/Mongo y Report FastAPI/Mongo integrados con Sensor, Monitoring, Event y Gateway existentes.
- Angular22.2.3, ocho PDF, historial, descarga Administrator, permisos/JWT, reportes por APIs propietarias y auditoría asíncrona RabbitMQ/Outbox.
- Dashboard completo: comunidades/sensores/alertas/eventos, distribución por nivel y fenómeno, evolución, estadísticas SQL de24h por tipo/unidad, filtro de comunidad y SignalR/recuperación REST.
- Compose predeterminado ahora ejecuta Fase3 completa:17 contenedores. Referencia anterior en docker-compose.legacy.yml. Fuente de configuración: overlays phase3; regenerar con scripts/update-phase3-compose.ps1. No se ejecutan proveedores duplicados.
- Ocho volúmenes respaldados con servicios detenidos, SHA-256 verificado y restaurados en otro proyecto. Login, referencias, lecturas, reglas, alertas, eventos, auditoría, historial y PDF recuperados.
- PDF real de5 000 lecturas/20 páginas de API: 38.312s y 1061586 bytes. Sobrepasar el límite produce fallo explícito sin entregar documento truncado.
- Cuota atómica persistente:20 solicitudes simultáneas,3 aceptadas y17 rechazadas429; los trabajos liberan cupos al terminar/fallar. Filtros reales por sensor/comunidad/tipo/fechas y recuperación de trabajo al reiniciar comprobados.
- UI: filtros pertinentes, validación de fechas, loading/vacío/error/éxito, recuperación y permisos. Seis axe de UI aislada y16 sobre Compose real incluyen WCAG2.2 AA, sin violaciones detectadas.
- Regresión: .NET122 aprobadas+1 omitida; Java7; Auth5; Python11; Angular56/build/lint. Diez imágenes finales construidas. K8s:29 recursos+Secret validados, sin desplegar.

## Ejecutar una instalación nueva

Desde backend: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start-Phase3.ps1 -NoBuild`.
Omitir -NoBuild si aún no están construidas las imágenes. El script genera .env.phase3.local solo si falta y usa proyecto climate-phase3-local, separado de volúmenes anteriores. Usuario admin; contraseña en AUTH_ADMIN_SEED_PASSWORD del archivo privado. No publicar ese archivo.

Frontend http://localhost:4200; Gateway http://localhost:8080.

## Evidencia y continuación

Informe: backend/docs/FASE_3_INFORME_FINAL.md. Matriz: backend/docs/FASE_3_MATRIZ_REQUISITOS.md. Resultados: backend/docs/FASE_3_PRUEBAS.md. Evidencia JSON: backend/docs/validation/phase3-large-report.json, phase3-report-integration.json y phase3-recovery.json.

**No hay pendientes funcionales identificados dentro del alcance acordado.** Las exclusiones están en su Markdown propio; no se necesitan migraciones reales ni VPS para cerrar. Para un cambio futuro, revisar este archivo y git diff, reutilizar los servicios y ejecutar solo las pruebas afectadas. No regenerar servicios ni matriz inicial. Histórico anterior en backend/docs/validation/phase3-progress-history.md.

## Corrección: datos de demostración y simulador

Se restauraron las tres comunidades, los 15 sensores y las 27 reglas iniciales mediante las APIs actuales, sin duplicar servicios. El dashboard ya tiene 15 lecturas actuales, cinco grupos estadísticos y alertas. Se verificó que la simulación aumenta las lecturas de 45 a 60 y que repetir el seed no duplica los catálogos. El estado vacío mencionado arriba corresponde a la comprobación anterior y queda superado por esta corrección.

`Start-Phase3.ps1` ahora carga automáticamente la demostración y activa el simulador. Se puede omitir con `PHASE3_DEMO_ENABLED=false`. Para reanudar una carga interrumpida, ejecutar `scripts/Seed-Phase3Demo.ps1`: conserva lo existente y completa lo faltante. Guía: `backend/docs/FASE_3_DATOS_DEMO.md`. Evidencia: `backend/docs/validation/phase3-demo.json`.
