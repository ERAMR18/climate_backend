# Pruebas Fase 4

Fecha local: 2026-10-29 (America/Mexico_City). Los artefactos JSON usan UTC. No confundir esa diferencia con otra ejecución.

| Verificación | Resultado observado |
|---|---|
| Auth TypeScript build | Aprobado, openid-client carga también en Node 22.13 |
| Auth tests | 11 aprobadas; incluye cinco casos inválidos de ID token en prueba OIDC: nonce, issuer, audience, expiración y firma |
| Monitoring .NET build | Aprobado sin advertencias locales |
| Monitoring tests | 18 aprobadas: regresión, dispositivo/sensor desconocido o inactivo, exclusión de simulación manual/batch |
| ArduinoBridge .NET build | Aprobado sin advertencias; SQLite nativo actualizado a bundle 3.0.2 |
| ArduinoBridge tests | 13 aprobadas: JSON, campos obligatorios, dispositivo/unidad/rango, TLS, persistencia/reinicio, UUID, límite, reconexión serial simulada, 401 y reintentos 503→200 |
| Angular | Build y lint aprobados; 57 pruebas aprobadas, incluida actualización del panel/historial con SignalR y estado obsoleto |
| Docker | Cuatro imágenes modificadas construidas; 17 contenedores saludables en proyecto climate-phase3-local |
| Integración HTTP/SQL/SignalR | 15 verificaciones aprobadas en docs/validation/phase4-integration.json |
| Navegador Edge | Aprobado: login, dashboard escritorio/móvil, valor actualizado, canvas histórico y responsive; tres auditorías WCAG sin violaciones detectadas, sin errores JavaScript. docs/validation/phase4-browser.json |

La integración utiliza un sensor demo `ARDUINO-PHASE4-DEMO`, creado una sola vez mediante API y reutilizado al repetir. Genera credencial local solo si falta y guarda su hash en configuración. No borra catálogos ni volúmenes. Comprueba que un JWT humano no autoriza telemetría, desconocidos fallan, credencial queda limitada al sensor/fuente, rango inválido falla, SQL conserva la lectura, replay no duplica, replay alterado falla, alertas existentes se disparan, SignalR publica y el puente .NET simulado transmite por HTTP autenticado. La simulación del servidor sigue activa y excluye ese sensor.

Durante la primera integración se detectó una transacción anidada con el filtro de auditoría. Se corrigió reutilizando esa transacción y tomando bloqueos UPDLOCK/HOLDLOCK sobre el UUID. El resultado final registrado sustituye ese intento fallido. No se suprimió el filtro ni se abandonó auditoría.

En navegador se detectaron valores duplicados de URL pública en el archivo privado de Fase3 y un desbordamiento del gráfico al pasar de escritorio a móvil. El overlay fija localhost y el contenedor de gráficas ahora permite reducir su ancho. Las pruebas de navegador se repiten con la imagen final.

Comandos desde la raíz:
```powershell
dotnet test backend/tests/Climate.Monitoring.Tests/Climate.Monitoring.Tests.csproj --no-restore
dotnet test tools/Climate.ArduinoBridge.Tests/Climate.ArduinoBridge.Tests.csproj
node backend/src/Services/AuthService/node_modules/typescript/bin/tsc -p backend/src/Services/AuthService/tsconfig.json
node --test backend/src/Services/AuthService/tests/*.test.cjs
tools/node/node.exe tools/phase4-integration.mjs
tools/node/node.exe tools/phase4-browser.mjs
```
Para Angular desde frontend/climate-monitoring-web:
```powershell
../../tools/node/node.exe node_modules/@angular/cli/bin/ng.js test --watch=false
../../tools/node/node.exe node_modules/@angular/cli/bin/ng.js build
../../tools/node/node.exe node_modules/@angular/cli/bin/ng.js lint
```
Los scripts de integración requieren stack local activo y SDK .NET 10. Browser requiere Playwright existente en tools/ui-validation y Edge. No imprimen contraseñas/tokens. El backend persiste las lecturas demo y auditoría de la prueba; repetir no duplica sensores, aunque cada ejecución produce nuevas lecturas deliberadamente.

Limitaciones: Google/GitHub reales no comprobados sin credenciales; Google usa proveedor simulado con JWT firmado y JWKS de prueba. El test del puente usa ISerialDataSource simulado, no hardware. No se ejecutó compilador Arduino/Arduino IDE ni carga al dispositivo; validar placa, core, puerto y ADC con el responsable. No se probaron desconexiones físicas USB ni ruido térmico. Kubernetes, DNS, TLS público, réplicas/backplane y volúmenes productivos no se ejecutaron.

La compilación masiva inicial encontró un error de CA Maven al reconstruir Alert, no modificado en esta fase. Se reutilizaron sus imágenes existentes. Auth se construyó pasando el certificado existente como secreto temporal, sin desactivar TLS. Docker .NET emitió NU1900 al consultar el feed de vulnerabilidades por CA, aunque restauró los paquetes cacheados y publicó correctamente. Eso no constituye una auditoría de vulnerabilidades completa de dependencias heredadas.
