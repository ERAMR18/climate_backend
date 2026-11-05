# Fase 4 — avance recuperable

## Documentación completa y PDF entregados

Generado Documentacion_Climate_Monitoring_Fase_4.pdf (54 páginas). Actualizado Documentacion_Climate_Monitoring_System.pdf (62 páginas): Fase4 vigente primero y ocho páginas anteriores de Fase3 conservadas al final como historial. Copia original intacta en docs/archive/Documentacion_Climate_Monitoring_System_antes_Fase_4.pdf; regenerar no duplica el anexo.

Fuente principal nueva: docs/FASE_4_DOCUMENTACION_COMPLETA.md. Incluye arquitectura vectorial, OAuth/JWT, VPS Docker y sus correcciones, DHT11/simulación, protocolo, puente/SQLite, API/idempotencia/alertas/SignalR/dashboard, seguridad, estados reales y pendientes individuales. Anexos: guías completas UUID y nuevaPC, matriz42requisitos actualizada y los dos .ino. Fuente de estado distingue GitHub real/HTTP200/upload reportados por usuario, pruebas automatizadas históricas registradas y DHT11 compilado sin medición confirmada.

Validación aprobada: contenido requerido e índice/marcadores, sin credenciales conocidas, sin texto fuera de página ni páginas vacías; texto previo preservado. Revisión visual de portada, índice, diagrama, tablas, guía y firmware. Evidencias docs/validation/phase4-pdf.json y phase4-pdf-layout.json; previsualizaciones en docs/validation/pdf-preview. Generar documentación no ejecutó despliegues ni cargas de placa.

Para regenerar desde raíz Windows:
```powershell
python tools/phase4-doc-matrix.py
python tools/generate-phase4-pdf.py
python tools/validate-phase4-pdf.py
```
Dependencias locales ya instaladas en tools/pdf-libs; receta portable en docs/FASE_4_PDF_ENTREGA.md y tools/requirements-phase4-pdf.txt. No repetir instalaciones/compilaciones del sistema para modificar solo documentación. Objetivo de documentación completado; conservar los pendientes externos técnicos que se indican en el PDF.

## Dos guías operativas entregadas

Creado docs/GUIA_ARDUINO_CAMBIAR_SENSOR_UUID.md: sensor del catálogo VPS, UUID/deviceId/token/source, asociación Windows, cola por UUID sin borrar anteriores, copiar solo ARDUINO_*, overlay y recreación Monitoring conservando Compose existentes, verificación y recuperación.

Creado docs/GUIA_ARDUINO_NUEVA_PC.md: copia mínima de código y archivos privados del puente (sin trasladar .env de bases/OAuth), SDK.NET10, COM, compilación/ejecución, instalación ArduinoIDE opcional y carga DHT11/simulación, cuándo cambiar VPS y cómo retomar. Las dos guías distinguen PowerShell Windows/Bash VPS, contienen comandos completos y no dependen de variables de terminal creadas en mensajes anteriores. Se comprobaron delimitadores Markdown, enlaces relativos y existencia de scripts citados; no se ejecutaron instalaciones ni despliegues para crear los documentos.

Para consultar: abrir los dos Markdown; no volver a configurar el sistema solo por leer la guía. Estado y dependencias externas previas conservados.

## DHT11 físico adaptado — 2026-11-04

Antes de este cambio el usuario confirmó200 del puente al dominio: telemetría simulada aceptada en VPS. Nuevo archivo del usuario backend/lecturas-sensor/lecturas-sensor.ino lee DHT11 en pin2, pero emitía status/temperature/humidity incompatibles con Protocol.Parse. Se adaptó a JSON v1 con deviceId arduino-temp-001, sensorType TEMPERATURE, value, unit C, source ARDUINO y sequence. Se conserva humidity como campo auxiliar serial; puente/backend actuales no la persisten como lectura de humedad.

Bibliotecas DHT sensor library1.4.7 y Adafruit Unified Sensor1.1.15 ya estaban instaladas, verificadas por CLI. Compilación real Uno aprobada: 5626 bytes flash/243 bytes RAM. No se cargó este firmware nuevo ni se verificó medición DHT11. Script tools/Arduino-Uno.ps1 agrega -SketchPath conservando el simulador como default. No se modificó .env ni se aplicaron cambios en VPS.

Para usar DHT11: detener puente/cerrar Monitor Serial; compilar/cargar `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Arduino-Uno.ps1 -SketchPath backend/lecturas-sensor -Upload -PortName COM3`; cambiar únicamente ARDUINO_SOURCE=ARDUINO en .env VPS y recrear Monitoring con su misma lista Compose desde labels; ejecutar puente serial Release COM3. No reconstruir backend/frontend ni cambiar token/UUID por sustituir simulación por DHT11. Sensor data pin2 y alimentación/tierra conforme a etiquetas del módulo; no afirmar cableado universal para sensores sueltos.

Simulador anterior: hardware/arduino/temperature-sensor/temperature-sensor.ino (SIMULATED). Firmware DHT11 nuevo: backend/lecturas-sensor/lecturas-sensor.ino (ARDUINO). Ruta de recepción dashboard es siempre USB → puenteC# → POST dominio/api/monitoring/telemetry → Monitoring/SignalR. Retomar con `Get-Content .\FASE_4_PROGRESO.md`.

## Telemetría VPS responde 404 — siguiente diagnóstico

Usuario configuró UUID VPS 772683f4-fb65-4d25-8c6e-64d110b3ea5f y copió ARDUINO_* al .env VPS. Puente envía POST HTTPS al endpoint correcto /api/monitoring/telemetry pero recibe404 y descarta esas lecturas según política permanente. Indicar Ctrl+C antes de continuar. No repetir configuración del firmware/token/UUID.

Diagnóstico pendiente: POST vacío sin credenciales directamente desde contenedor frontend a http://monitoring-service:8080/api/v1/monitoring/telemetry; 404 indica imagen Monitoring sin endpoint, 400/401 indica que ruta existe. Las recreaciones previas con --no-build no actualizan la imagen. Recuperar Compose de Monitoring desde labels, incluir overlay Arduino, validar, build monitoring-service y up no-deps. Comprobar UUID por printenv individual y repetir POST interno/público antes de reiniciar puente Release COM3. Si interno400/401 y público404, revisar ruta telemetry-device en Gateway y Nginx, no secretos. No se declaró causa definitiva sin resultado interno.

## Sensor local obsoleto / sensor nuevo VPS simulado por servidor

Comprobación local: puente apunta al dominio HTTPS, Device.SensorId sigue siendo UUID local 3391662b-e60b-44e5-8473-c27b3c3523a5, COM3, Simulate=false; no proceso Climate.ArduinoBridge detectado. No se cambió UUID sin conocer el nuevo del VPS. UI marca obsoleto después de 30s sin lectura. Monitoring excluye de simulación únicamente UUID presentes en Telemetry.Devices: crear sensor web no lo registra como Arduino. Si el nuevo sensor VPS genera valores solo, verificar asociación del UUID real en overlay/entorno y reinicio de Monitoring antes de transmitir.

Siguiente paso: copiar UUID nuevo VPS → Configure-Phase4Arduino.ps1 local → copiar cinco ARDUINO_* al .env VPS → aplicar overlay docker-compose.arduino.vps.yml a Monitoring → ejecutar puente serial COM3 Release. El sensor local puede permanecer obsoleto porque ahora el puente apunta al VPS, no a localhost. Verificar Configured SensorId por printenv de campo individual en contenedor, sin imprimir secretos.

## Publicación de telemetría VPS — instrucciones completas pendientes de ejecutar por usuario

Última comprobación Git backend: único archivo pendiente de publicar, docker-compose.arduino.vps.yml. Cambios del puente/appsettings son locales a Windows; frontend no requiere otro pull/build por este ajuste. Dar siempre comandos completos sin depender de COMPOSE definido en turnos anteriores.

Orden: comprobar/crear sensor Temperature activo C en catálogo VPS; configurar su UUID en PC con Configure-Phase4Arduino.ps1 (conserva token y BaseUrl HTTPS); copiar únicamente ARDUINO_* al .env del VPS; publicar solo el nuevo overlay mediante git add específico/commit/push; git pull VPS; recuperar Compose desde labels del contenedor monitoring-service y agregar overlay Arduino; config quiet/build Monitoring/up no-deps; comprobar salud y asociación no secreta; compilar Release y ejecutar puente COM3 en PC. No recrear Auth/frontend ni MongoDB con la lista Compose de Monitoring. Confirmar envío con Confirmed reading y dashboard.

## Puente cambiado al dominio y compilado — 2026-11-04

Usuario confirmó carga exitosa del firmware en Uno COM3; falta verificar telemetría completa en VPS. Cambio aplicado en tools/Climate.ArduinoBridge/appsettings.json: BaseUrl HTTPS dominio, AllowLocalHttp=false, TelemetryPath existente y puerto COM3; conserva token/deviceId/sensorId. Buffer nuevo arduino-vps-buffer.db para no reenviar cola de pruebas locales al VPS; arduino-buffer.db se conserva.

Compilación Release aprobada (0 advertencias/0 errores) y sintaxis PowerShell del lanzador validada. Lanzador agrega -Configuration y -NoBuild. No se inició envío al VPS desde esta sesión. Nuevo overlay backend/docker-compose.arduino.vps.yml preparado, aún debe publicarse/aplicarse al VPS junto con ARDUINO_*; no asumimos que el UUID local exista allí. Guía completa: docs/ARDUINO_VPS_PUESTA_EN_MARCHA.md.

Desde raíz Windows:
```powershell
dotnet build tools/Climate.ArduinoBridge/Climate.ArduinoBridge.csproj --configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Climate.ArduinoBridge/Start-ArduinoBridge.ps1 -PortName COM3 -Configuration Release -NoBuild
```
Cerrar Monitor Serial antes. Esperar Confirmed reading para dar por aceptada la transmisión; 401/403 requieren corregir autorización/overlay, no regenerar todo. Retomar con `Get-Content .\FASE_4_PROGRESO.md`. Compilación normal falló en sandbox por TLS NuGet; la compilación con confianza del usuario restauró y terminó correctamente sin desactivar certificados.

## Arduino IDE y Uno preparados — 2026-11-04

Usuario confirmó que GitHub ya funciona en VPS. No continuar el diagnóstico OAuth salvo nuevo error.

Instalado Arduino IDE 2.3.10 mediante winget (ArduinoSA.IDE.stable), Arduino CLI 1.5.1 incluido y core Arduino AVR Boards 1.8.8, con avr-gcc/avrdude. IDE: C:/Users/casa/AppData/Local/Programs/arduino-ide. Windows y Arduino CLI detectan Arduino UNO en COM3 (arduino:avr:uno). Driver USB serial ya funciona; no se instalaron drivers adicionales.

Compilado realmente hardware/arduino/temperature-sensor/temperature-sensor.ino con SIMULATE_SENSOR=1: flash 3920/32256 bytes, RAM 204/2048. No se cargó firmware a la placa ni se validó su salida serial en esta sesión. Se preservó el programa que tuviera el Uno. La limitación antigua de compilador Arduino pendiente queda superada; carga/medición física aún pendientes.

Comandos desde la raíz Windows, sin repetir instalación:
```powershell
# Verificar compilación
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Arduino-Uno.ps1
# Cargar en la placa (reemplaza su sketch actual)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Arduino-Uno.ps1 -Upload -PortName COM3
```
Cerrar Monitor Serial y ArduinoBridge antes de cargar. El sketch simula temperatura sin sensor LM35. Para ver JSON, Monitor Serial a 9600 baud; para enviar backend usar ArduinoBridge con -PortName COM3, tras cerrar Monitor Serial. La configuración del puente debe apuntar a la API/sensor/credencial del entorno deseado (VPS o local); no se cambió automáticamente en esta instalación.

Retomar: `Get-Content .\FASE_4_PROGRESO.md`. Instalación/compilación completas, siguiente paso cargar cuando el usuario quiera reemplazar el programa actual del Uno.

## Frontend VPS corregido; faltan variables en Auth — 2026-11-04

Usuario confirmó runtime-config público con apiUrl y hubUrl HTTPS del dominio. Nuevo error de Auth: PROVIDER_NOT_CONFIGURED, referencia 7ea98a84-fd96-42cb-9a82-0fec6984b54b. En settings() significa que falta al menos una variable de OAUTH_GITHUB_CLIENT_ID, OAUTH_GITHUB_CLIENT_SECRET, OAUTH_GITHUB_CALLBACK_URL, OAUTH_FRONTEND_URL. No atribuirlo aún a credenciales inválidas. .env no inyecta variables automáticamente: overlay identity-service debe mapearlas.

Siguiente paso en VPS: docker exec node para imprimir únicamente OK/FALTA de las cuatro variables. Recuperar lista Compose del contenedor identity-service, incluir explícitamente docker-compose.phase4.vps.local.yml con mapping OAuth y HTTPS obligatorio, recrear solo identity-service sin rebuild. Mantener overrides de frontend al actualizarlo; evitar recrear Auth con lista Compose que omita su overlay. Reintentar login desde /login y consultar etiqueta segura de error si persiste. No mostrar ni copiar secretos.

## Gateway reparado; comprobar cookie OAuth — 2026-11-04

El usuario reconstruyó Gateway. Curl directo ahora llega a Express y devuelve X-OAuth-Error:SESSION_MISSING con mensaje, esperado al probar sin flujo/cookie. Logs siguientes: SESSION_MISSING a 20:50:45 (curl conocido) y 20:51:32 (origen aún por confirmar). La URL del callback anterior incluía state. Si el segundo corresponde al login real, comprobar cookie climate_oauth: emisión Set-Cookie, host de inicio vs callback, y envío por navegador/proxy. No desactivar Secure, SameSite o state sin evidencia.

Siguiente diagnóstico sin rebuild: obtener runtime-config.json público (solo URLs) y cabeceras HTTP/Set-Cookie de GET /api/auth/oauth/github/start con challenge válido. Ocultar valor de cookie y no imprimir Location (incluye state). Luego inspeccionar en navegador si climate_oauth existe para el dominio y si se envía al callback; no compartir su valor. Herramienta web no pudo acceder al runtime-config público desde esta sesión.

## Evidencia Gateway bloquea callback — 2026-11-04

El usuario probó curl público sin code/state: HTTP401, Content-Length:0, WWW-Authenticate:Bearer y X-Correlation-ID; sin logs OAuth en Auth. Esto apunta al challenge JWT del Gateway antes del controlador OAuth. Auth devolvería un mensaje de sesión ausente y, con diagnósticos, X-OAuth-Error. El código local Gateway ya tiene ruta oauth-public con prioridad -20 y AuthorizationPolicy Anonymous; la imagen VPS puede estar desactualizada porque se reconstruyeron Auth/frontend sin confirmar Gateway.

Próximo paso en VPS: comparar `curl -i http://127.0.0.1:18080/api/auth/oauth/github/callback` con URL pública; reconstruir/recrear solamente gateway usando sus archivos Compose registrados en labels, después de git pull. Repetir curl: debe llegar a Auth y responder mensaje de sesión ausente (401 esperado sin sesión), no challenge Bearer vacío. Confirmar que /api/sensors sigue protegido. Si el acceso directo sí llega a Auth pero el público no, revisar proxy Nginx efectivo. No abrir todo /api/auth ni desactivar JWT globalmente.

## Segundo 401 en VPS — diagnóstico pendiente (2026-11-04)

Nueva captura mantiene 401. El usuario confirmó con docker exec que /app/dist/oauth.js en el VPS contiene issuer=https://github.com/login/oauth: el arreglo anterior sí está desplegado. El motivo real del nuevo rechazo sigue sin conocerse. No concluir que es otra vez issuer ni desactivar validación.

Se agregó oauth-diagnostics.ts y logging seguro en OAuthController.callback: solo provider revisado, código fijo y referencia UUID; nunca error crudo, código OAuth, cookie ni token. Respuesta incluye X-OAuth-Error y X-OAuth-Reference. Distingue sesión ausente/expirada, correo verificado, cuenta que exige vinculación, credenciales del proveedor y errores TLS conocidos. TypeScript y 14 pruebas Auth aprobadas, incluidas pruebas de no filtración de secretos. Cambios locales aún sin publicar/desplegar.

Próximo paso: publicar los tres archivos Auth pendientes (oauth.ts, oauth-diagnostics.ts, tests/oauth-diagnostics.test.cjs), git pull en VPS, recuperar lista Compose desde labels del contenedor identity-service, construir/recrear solo Auth y repetir desde /login. Consultar únicamente líneas `OAuth callback rejected` para identificar causa. No abrir/reutilizar URL de callback compartida: contiene código temporal y requiere cookie del navegador.

Retomar local: `Get-Content .\FASE_4_PROGRESO.md`. No reconstruir frontend, Gateway ni bases para este diagnóstico.

## Callback GitHub 401 — 2026-11-04

La captura de producción muestra `iss=https://github.com/login/oauth`. Auth tenía `issuer=https://github.com`; openid-client rechazaba la respuesta antes del intercambio de código. Corregido en backend/src/Services/AuthService/src/oauth.ts conforme al issuer oficial de GitHub. Nueva prueba github-callback.test.cjs reproduce el error anterior y comprueba aceptación del issuer oficial/rechazo de otro issuer sin canjear el código.

Validación local: TypeScript build y 12 pruebas Auth aprobadas. No se probó login real en VPS desde esta sesión. Cambios aún sin commit/push: publicar únicamente oauth.ts y tests/github-callback.test.cjs en el repo backend, luego git pull y reconstruir/recrear identity-service en VPS conservando los archivos Compose del contenedor desplegado. No basta --no-build sobre la imagen anterior. Reintentar desde /login, no recargar el callback ya consumido.

Para retomar: `Get-Content .\FASE_4_PROGRESO.md`. Prueba específica desde backend/src/Services/AuthService: `node --test tests/github-callback.test.cjs` después de compilar TypeScript.

## Corrección de arranque local — 2026-11-04

Síntoma: contenedores Healthy pero POST /api/auth/login cerraba la conexión desde Windows. Verificación: Gateway interno HTTP 200 y puerto publicado con respuesta vacía. Se recrearon únicamente gateway y frontend, sin rebuild ni borrar volúmenes. Ambos endpoints publicados volvieron a HTTP 200. Seed reanudado correctamente: 0 comunidades, 0 sensores, 0 reglas y 0 lecturas iniciales creadas; existentes preservados, simulación activada.

Start-Phase4.ps1 ahora verifica también los endpoints publicados desde Windows antes del seed; si fallan recrea solo el contenedor afectado una vez y vuelve a verificar. Respeta GATEWAY_PORT/FRONTEND_PORT y pasa la URL local al seed. Para retomar sin duplicar datos:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File backend/scripts/Start-Phase4.ps1 -NoBuild
```

Estado: IMPLEMENTACIÓN LOCAL COMPLETADA Y VALIDADA. No reconstruir servicios ni reiniciar el trabajo desde cero. Los pendientes externos no están cumplidos.

Solicitud: implementar phase_4.md, incluyendo firmware que simula sensores.
Diagnóstico: Auth NestJS/PostgreSQL; Monitoring .NET con SQL Server, evaluación de alertas y SignalR existentes; Angular con gráficos e historial. Docker Desktop iniciado y 17 contenedores locales saludables. No se ha modificado producción.
Referencias oficiales Fases 2/3/4 guardadas en docs/references; URL de Fase 1 devuelve 404. Informe completo: docs/FASE_4_INFORME_FINAL.md. Matriz de 42 requisitos: docs/FASE_4_MATRIZ_REQUISITOS.md.

## Retomar sin duplicar

Desde PowerShell:
```powershell
Set-Location C:\Users\casa\Documents\umg\desarrolloweb\webdev_proyect
Get-Content .\FASE_4_PROGRESO.md
```
Indicar al agente: «Continúa Fase 4 desde FASE_4_PROGRESO.md; inspecciona los archivos existentes y completa pendientes sin recrear lo terminado».

## Entregado y comprobado
- Implementados: OAuth/OIDC Google y GitHub con openid-client 6.8.8, state/PKCE/nonce/cookie HttpOnly/handoff con PKCE; login Angular; firmware simulado/LM35; puente SQLite; endpoint y migración; despacho persistente de alertas y SignalR; panel de temperatura y separación de fuentes; scripts de arranque/asociación.
- Verificados: 99 pruebas automatizadas (Monitoring 18; Auth 11; puente 13; Angular 57), build y lint. Puente sin advertencias; SQLite nativo actualizado. OIDC prueba firma, nonce, issuer, audience y expiración inválidos.
- Imágenes de Auth, Monitoring, Gateway y Angular construidas y activadas. Compilar todos los servicios falló por CA Maven de un servicio sin cambios; se reutilizaron sus imágenes Fase3 existentes. Auth admite CA por secreto BuildKit, sin desactivar TLS.
- Integración real: 15 comprobaciones HTTP/SQL/alertas/SignalR/puente aprobadas. Evidencia docs/validation/phase4-integration.json.
- Edge: botones externos, login local, temperatura actualizada, histórico y responsive aprobados. Tres auditorías WCAG sin violaciones detectadas y sin errores JavaScript; docs/validation/phase4-browser.json y capturas.
- Documentos completos en docs/FASE_4_*.md. Se corrigieron transacción anidada de auditoría, URL pública duplicada (overlay fuerza localhost), ancho móvil de gráficas y contraste de fecha en alerta amarilla.
- No quedó puente oculto ejecutándose: las pruebas lo detienen. El sensor ARDUINO-PHASE4-DEMO y su credencial local se conservan para volver a transmitir sin duplicar registros.

## Comandos del punto actual
```powershell
# Desde la raíz; reutilizar imágenes ya comprobadas
powershell -NoProfile -ExecutionPolicy Bypass -File backend/scripts/Start-Phase4.ps1 -NoBuild
# En otra terminal, generar datos sin hardware
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Climate.ArduinoBridge/Start-ArduinoBridge.ps1 -Simulate
```
Los archivos privados .env.phase3.local, appsettings.json y device-token.local se conservan. Configure-Phase4Arduino.ps1 reutiliza la credencial y actualiza la asociación sin duplicar claves.

Frontend http://localhost:4200; Gateway http://localhost:8080. Seleccionar **Arduino temperatura demo** en la gráfica de temperatura. Ctrl+C detiene el puente. El lanzador impide otra instancia simultánea. Para Arduino con firmware simulado usar `-PortName COM3` en lugar de `-Simulate`, cambiando COM por el puerto real; instrucciones en docs/FASE_4_ARDUINO.md.

## Pendientes externos, no cumplidos

- Google/GitHub reales: responsable debe aportar Client IDs/Secrets y registrar callbacks; el proveedor de pruebas OIDC es simulado.
- Arduino: no se ejecutó compilador/core Arduino ni se subió sketch ni se probó placa USB/sensor real; validar placa, cableado, driver y medición.
- VPS/K3s, Pods, Ingress, DNS/TLS públicos, escala/backplane SignalR, almacenamiento y Secrets productivos están excluidos. Detalle individual en docs/FASE_4_PENDIENTES_FUERA_SCOPE.md.
- Referencia oficial Fase 1 no disponible (404).

Para retomar, leer este archivo y el informe, abordar únicamente el pendiente concreto y conservar archivos/configuración. Node compatible para Angular: tools/node/node.exe (24.21.0); Node 22.13 del PATH es insuficiente. No reinstalar Angular ni recrear el demo. Omitir -NoBuild solo al cambiar código; -BuildAll reconstruye también servicios sin cambios y requiere resolver la CA Maven del entorno.

No imprimir archivos privados .env ni credenciales. No aplicar Kubernetes ni desplegar VPS.
