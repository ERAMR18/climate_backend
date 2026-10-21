# Fase 3 — Reportería

Implementación: `src/Services/ReportService`, FastAPI, MongoDB privado, Jinja2 y WeasyPrint. Angular solicita trabajos y descarga archivos; todo PDF se genera en backend. El servicio consulta exclusivamente APIs HTTP de dominio. Su MongoDB contiene metadatos, trabajos y descargas, nunca tablas copiadas de los otros servicios.

## Tipos y fuentes

| Tipo | Fuente | Contenido |
| --- | --- | --- |
| COMMUNITIES | Community | Nombre, municipio, departamento, país, coordenadas, estado, conteo de sensores por API |
| SENSORS | Sensor | Código, nombre, tipo, comunidad, ubicación, unidad, estado e instalación |
| READINGS | Monitoring, catálogos | Lecturas con sensor, comunidad, fecha/hora local, valor/unidad y estado histórico disponible |
| ALERTS | Alert Java, catálogos | Fenómeno, nivel, valor/umbral, mensaje, fecha/hora y estado activa/atendida/cerrada |
| EVENTS | Event, catálogos | Historial de fenómenos y flujo de atención |
| SENSOR_STATUS | Sensor, Monitoring | Actividad administrativa, ausencia de comunicación, última lectura y comunidad |
| CLIMATE_STATISTICS | Monitoring, Alert | Cantidad, promedio/mínimo/máximo por sensor/tipo/unidad y alertas del periodo |
| EXECUTIVE | Todas las APIs | Resumen, cobertura, indicadores, niveles, estadísticas y conclusiones cuantificadas |

El estado sin comunicación usa `SENSOR_COMMUNICATION_TIMEOUT_SECONDS` (900 segundos por defecto) para sensores activos sin lectura reciente. Un sensor desactivado se muestra como inactivo aunque carezca de lecturas. Fechas históricas sin estado conservado se muestran como «Sin registro».

Las agregaciones usan Decimal y separan sensores, tipos y unidades: no mezclan °C/°F ni magnitudes diferentes. La lluvia se suma únicamente con `RAINFALL_MODE=incremental` y unidad `mm`, después de confirmar que cada lectura representa un incremento. El valor por defecto `unknown` muestra «No definido» para acumulación; nunca supone que una medición de nivel o un total acumulativo sea un incremento.

## API y permisos

Gateway público: `/api/reports`. API interna equivalente: `/api/v1/reports`.

| Método y ruta | Resultado |
| --- | --- |
| GET /types | Ocho tipos disponibles |
| POST /generate | 202, identificador y estado PENDING |
| GET / | Historial `{items,total,page,pageSize}`, 25 filas por defecto, máximo 100 |
| GET /statistics | Conteos agregados por estado y usuario permitido |
| GET /{uuid} | Metadatos y progreso |
| GET /{uuid}/download | PDF completo, attachment y cache privado no-store |

Ejemplo de solicitud:

```json
{"type":"READINGS","filters":{"communityId":"11111111-1111-4111-8111-111111111111","from":"2026-10-01T00:00:00Z","to":"2026-10-02T00:00:00Z","timezone":"America/Guatemala"}}
```

Filters admite sensorId/communityId UUID, fechas con zona, sensorType, municipality, department, search, isActive, riskType y status. Las fechas son obligatorias para READINGS y CLIMATE_STATISTICS; EXECUTIVE usa las últimas 24 horas cuando no se especifican. Rango máximo 366 días configurable mediante REPORT_MAX_RANGE_DAYS. Cada API recibe los filtros que corresponden a su dominio; municipio/departamento filtran el catálogo de comunidades, no cambian el significado de una lectura histórica.

Administrator genera, consulta todos y descarga. Operator genera y consulta sus trabajos. Viewer consulta sus trabajos. Todos requieren el permiso explícito correspondiente reports:generate/read/download, además del rol cuando aplica. La descarga se verifica en Report Service. Firma, issuer, audience, expiración y UUID de usuario se validan; usuario y nombre se obtienen del JWT verificado. El navegador no puede elegir autor, archivo ni URL de dependencia.

El worker obtiene de Auth una identidad Viewer fija de servicio con vida de cinco minutos mediante `/api/v1/internal/auth/service-token`, protegida por clave interna y ausente de las rutas Gateway. No persiste el JWT del usuario ni consulta otra base de datos. Las URLs de dominio provienen del entorno del servidor.

## Procesamiento y almacenamiento

Estados PENDING → PROCESSING → COMPLETED/FAILED. Mongo conserva parámetros, usuario, fechas, referencia, estado, error, progreso, tamaño y SHA-256. Reclamo atómico, lease de 60 segundos renovado cada 15 segundos y recuperación de trabajos interrumpidos hasta tres intentos. El PDF y sus temporales usan exclusivamente UUID generados por el servidor.

Consultas internas en páginas de 250, con límite predeterminado de 50 000 filas; al excederlo se falla explícitamente sin entregar un documento truncado. El HTML se escribe por filas. WeasyPrint corre en subproceso con timeout de 180 segundos y límite de PDF de 25 MiB; su cargador bloquea recursos externos y archivos locales. Jinja escapa datos del dominio. Diseño A4 horizontal, logo CM, usuario/fecha/zona/filtros, encabezados repetidos y numeración de páginas.

El volumen `report-files` conserva los PDF, con retención predeterminada de 30 días. Mongo conserva el historial aunque el archivo expire; una descarga expirada responde 410. Una descarga antes de completar responde 409. Cada descarga guarda un registro y actualiza downloadedAt. Respaldar conjuntamente Mongo de reportes y el volumen de PDF; restaurar solamente uno puede dejar referencias sin archivo.

Cada solicitud y descarga guarda el evento de auditoría en su propio documento, atómico con esa escritura. Un publicador RabbitMQ confirmado reintenta independientemente. La caída del broker no impide generar PDF. Los consumidores deduplican EventId. Un error de dependencia termina con DEPENDENCY_UNAVAILABLE; límites excedidos con REPORT_LIMIT_EXCEEDED. No se registran tokens ni cuerpos de documentos en logs.

## Evidencia

`Test-Phase3System.ps1 -Reports`: ocho PDF generados con servicios reales vía YARP, MIME/attachment/hash/tamaño, permisos, historial y datos vacíos; 261 lecturas validan segunda página; expiración, broker detenido/recuperado y dependencia indisponible comprobados. Las pruebas adicionales de JWT y recuperacion tras reinicio tambien pasaron en la ejecucion final de Test-Phase3System.ps1 -Reports.

Cuatro pytest validan contratos, escape/impresión, sensores sin comunicación y agregaciones con unidades distintas y lluvia. La prueba de estadísticas detectó y permitió corregir una variable que confundía el reporte estadístico con el ejecutivo; ambos permanecen separados.

Frontend Angular22: navegación `/reports`, formulario, estado actualizado cada cinco segundos, historial paginado, detalle, descarga reservada a Administrator, acceso y conteos en dashboard. Prueba Playwright con respuestas API controladas verifica tres roles, generación, descarga, diseño móvil y seis escaneos axe sin violaciones. Esta prueba de interfaz complementa la integración HTTP/PDF real; no la sustituye.

## Cierre de volumen, concurrencia y recuperación

Prueba ampliada Test-Phase3System.ps1 -Reports -LargeReports:5 000 lecturas mediante20 páginas y descarga/hash reales, filtros positivos/negativos por sensor/comunidad/tipo/fechas, exceso de límite rechazado explícitamente. Veinte solicitudes concurrentes producen3 trabajos aceptados y17 respuestas429: el cupo es un índice único parcial por userId/activeSlot, no solo un conteo susceptible a carreras. Éxito/fallo/agotamiento de reintentos liberan el cupo.

Test-Phase3Compose.ps1 -Browser -Recovery verificó respaldo offline de Mongo Report y el volumen PDF junto con los demás datos, restauración en otro proyecto y descarga idéntica por SHA-256. La UI muestra solo filtros pertinentes, distingue loading/vacío/error/éxito y recupera el historial después de503. Accesibilidad incluye WCAG2.2 AA. VPS e históricos previos están excluidos, sin bloquear estas funcionalidades.
