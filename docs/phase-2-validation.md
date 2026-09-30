# Validación final — secciones 51–64

Fecha de ejecución local: 28 de septiembre de 2026. Se utilizaron SQL Server y
RabbitMQ reales en proyectos Docker temporales, sin modificar datos desplegados.

## Resultado y cobertura

| Comprobación | Resultado |
|---|---|
| Backend unitario/integración | 123 pruebas aprobadas; ninguna omitida al ejecutar con RabbitMQ temporal |
| Angular | 56 pruebas aprobadas, lint correcto y build de producción correcto |
| Navegador | Recorrido aprobado y 14 estados axe sin hallazgos; foco/teclado y móvil verificados |
| OpenAPI/EF | Contrato exportado desde seis APIs; seis modelos sin cambios pendientes de migración |
| Funcional F1 | JWT, expiración, roles, catálogos, filtros, reglas, workflow, lecturas, eventos, dashboard y SignalR aprobados |
| Desacoplamiento F2 | Audit detenido; UpdateSensor responde éxito y SensorDb contiene el cambio |
| Recuperación F2 | 13 mensajes sobreviven al reemplazo de RabbitMQ; dos consumidores dejan 14 eventos únicos; replay no duplica filas |
| Resiliencia | Broker caído deja outbox pendiente; reconexión automática, SQL tardío y probes aprobados |
| Kubernetes | 24 recursos y Secret de ejemplo validados contra esquema 1.34.1 y restricciones del proyecto |
| Dependencias | npm: cero vulnerabilidades reportadas; consulta NuGet de 34 proyectos: ninguna vulnerabilidad reportada |

F1 es `Test-Phase2Functional.ps1`. Además de las funciones iniciales, comprueba
CRUD/estado/filtros de comunidades y usuarios, último acceso, hash almacenado en
SQL, JWT expirado firmado con la clave temporal, filtros combinados de alertas y
eventos, historial por fecha y conteos activos/inactivos del dashboard. RabbitMQ
permanece inaccesible durante ese escenario; seis acciones de reglas/workflow
quedan guardadas en outbox. Se reciben los seis nombres de eventos por WebSocket.

F2 es `Test-Images.ps1 -TestRecovery`. Valida directamente el cambio de sensor en
SQL con Audit detenido, espera la publicación, reemplaza el broker conservando
el volumen, recupera consumidores y consulta el evento mediante los filtros
userId/action/resource/from/to. Verifica metadata del detalle y DLQ vacía para
entregas válidas. Las pruebas de mensajes inválidos y retry agotado están en la
suite backend con broker aislado.

## Navegador y accesibilidad

`scripts/test-browser.mjs` del frontend usa Edge headless, Playwright y axe-core.
El recorrido verifica redirección de una ruta protegida, login real, retorno a
la ruta original, navegación de pantallas, creación/desactivación de una regla,
confirmación, Tab/Shift+Tab, Escape, restauración de foco, navegación móvil y logout.
Comprueba que REST/SignalR utilicen Gateway y que no haya errores JavaScript.

Se revisaron y aprobaron 14 estados: login, dashboard, comunidades, sensores, reglas, alertas,
eventos, usuarios, auditoría, lecturas, formulario de regla, confirmación, reglas
en móvil y menú móvil. El [reporte final](validation/axe-final.json) no contiene
hallazgos; se conserva una [captura móvil](validation/mobile-rules.png).
Los hallazgos iniciales de contraste del sidebar y referencia ARIA inexistente
fueron corregidos. La prueba incluye comprobación de desbordamiento a 375 px.
Una pasada automática de axe no equivale a certificación completa WCAG.

## Repetir

Desde `backend`, con Docker activo y puertos libres:

```powershell
dotnet build ClimateMonitoringSystem.sln
./scripts/Test-AuditMessaging.ps1
./scripts/Test-Phase2Functional.ps1
./scripts/Build-Images.ps1 -IncludeFrontend
./scripts/Test-Images.ps1 -TestRecovery
./scripts/Test-Images.ps1 -BrowserScript ../frontend/climate-monitoring-web/scripts/test-browser.mjs
python scripts/validate-manifests.py
```

Para navegador, ejecutar antes `npm ci` desde el frontend y disponer de Edge, o
instalar Chromium con `npx playwright install chromium` y adaptar `BROWSER_CHANNEL`.
`Test-Images` proporciona la contraseña temporal mediante el entorno; no debe
guardarse en archivos. `UI_ARTIFACTS` permite elegir la carpeta de reportes.
Los puertos predeterminados de imágenes son 14349, 18090 y 14200. La prueba
funcional usa 14339, 18080 y el intervalo de APIs que comienza en 15100.

Desde el frontend:

```powershell
npm run lint
npm run test:ci
npm run build
npm audit
```

La CLI npm instalada inicialmente falló al ejecutar `audit fix`; se usó una CLI
npm 11 aislada para actualizar el lockfile dentro de los rangos compatibles y
después se ejecutó `npm ci`. No se aplicó `--force` ni una migración mayor Angular.
Para la CA usada por npm dentro del build, ver [Kubernetes/imágenes](kubernetes.md).

## Límites y pendientes explícitos

- Minikube continúa rechazando la conexión. No se aplicaron manifiestos ni se
  acredita funcionamiento del PVC/CNI/SQL externo desde Pods. Docker prueba las
  aplicaciones, no esos componentes del clúster.
- Se conserva Angular 21 del proyecto. La referencia tecnológica indica 22+;
  esa diferencia está marcada `PARTIAL` en la matriz, separada de los RF/RNF.
- VPS, dominio, Certbot y HTTPS de producción: fuera de alcance.
- No se publicaron imágenes en un registro, no se hicieron commits ni despliegues.
- La auditoría de dependencias informa el catálogo consultado en esta fecha;
  no sustituye pruebas de seguridad ni garantiza ausencia de vulnerabilidades.

## Cierre de secciones

51: prefijo/tag configurables y registro documentado. 52: frontend estático en
Kubernetes existente, verificado. 53–55: suites, UpdateSensor y recuperación
ejecutados. 56–58: README, guías y Mermaid actualizados. 59: matriz individual de
68 RF y 8 RNF. 60–64: arquitectura y exclusiones preservadas, análisis previo,
implementación sobre componentes existentes y validación final documentada.

La [matriz](phase-2-compliance.md) distingue cumplimiento funcional local,
limitaciones de infraestructura y elementos excluidos.
