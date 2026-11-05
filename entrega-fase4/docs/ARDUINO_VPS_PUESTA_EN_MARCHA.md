# Arduino Uno → PC Windows → VPS

## Estado preparado

El Uno ya recibió el firmware simulado según la salida de carga del usuario. El puente está configurado para `https://analisissistemas2026proyecto.xyz/api/monitoring/telemetry`, COM3, 9600 baud y lectura serial real (`Simulate=false`). El firmware de la placa genera temperaturas simuladas: su source sigue siendo SIMULATED, aunque el transporte sea USB físico.

Compilación Release comprobada sin advertencias ni errores. No se inició transmisión al VPS durante este cambio. Se conserva sensorId `3391662b-e60b-44e5-8473-c27b3c3523a5` y token local; no se verificó que ese UUID exista en el VPS. La cola VPS utiliza arduino-vps-buffer.db, conservando arduino-buffer.db de las pruebas locales sin reenviarlo automáticamente al dominio.

## Windows: compilar e iniciar

Desde la raíz:
```powershell
dotnet build tools/Climate.ArduinoBridge/Climate.ArduinoBridge.csproj --configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Climate.ArduinoBridge/Start-ArduinoBridge.ps1 -PortName COM3 -Configuration Release -NoBuild
```
Cierra Arduino IDE Monitor Serial y otras instancias del puente antes de iniciar. No usar -Simulate: se quiere leer el JSON de la placa. Ctrl+C detiene el puente, conservando pendientes. No es necesario volver a subir el sketch cada vez.

El mensaje `Confirmed reading` confirma aceptación por el backend. 401/403 detienen envíos y conservan la cola: corregir asociación/token/fuente y reiniciar el mismo comando. HTTP400 puede indicar sensor inactivo/inexistente, unidad/fecha/valor inválido. Un intento de publicar a través de un Gateway sin ruta pública de telemetría también puede producir 401 Bearer vacío; el Gateway actualizado contiene telemetry-device y exige la credencial en Monitoring, no un JWT humano.

## VPS: asociación necesaria

En el catálogo web, comprobar que el UUID configurado en tools/Climate.ArduinoBridge/appsettings.json corresponda a un sensor Temperature activo y unidad C/°C en el VPS. Si el sensor tiene otro UUID, ajustar Device.SensorId en Windows y ARDUINO_SENSOR_ID en el VPS para que coincidan. El script Configure-Phase4Arduino.ps1 permite actualizar la asociación local conservando el token y las opciones HTTPS existentes.

En el archivo privado del VPS deben existir, sin duplicados, ARDUINO_DEVICE_ID, ARDUINO_SENSOR_ID, ARDUINO_TOKEN_SHA256, ARDUINO_SOURCE=SIMULATED y ARDUINO_ENABLED=true. El hash debe corresponder a device-token.local de la PC; no usar el hash como token ni copiar el Client Secret de GitHub. Para calcular únicamente el hash en Windows:
```powershell
$credential=[IO.File]::ReadAllText((Join-Path (Get-Location) 'tools/Climate.ArduinoBridge/device-token.local')).Trim()
$sha=[Security.Cryptography.SHA256]::Create()
try { ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($credential)))).Replace('-','').ToLowerInvariant() }
finally { $sha.Dispose(); Remove-Variable credential }
```
Guardar ese hash en ARDUINO_TOKEN_SHA256 del VPS, nunca el token original en Git.

El archivo `backend/docker-compose.arduino.vps.yml` mapea estas variables a Monitoring y no modifica puertos/volúmenes ni introduce localhost. Para publicarlo desde la PC:
```powershell
git -C backend add docker-compose.arduino.vps.yml
git -C backend commit -m "Add VPS Arduino device configuration overlay"
git -C backend push origin master
```
Si ya se publicó, no repetir el commit.

En Bash del VPS:
```bash
cd /opt/climate/climate_backend
git pull --ff-only
```
Si el pull termina correctamente, recuperar los Compose actuales de Monitoring y agregar el overlay:
```bash
CONFIG_FILES=$(docker inspect --format '{{ index .Config.Labels "com.docker.compose.project.config_files" }}' climate-phase3-vps-monitoring-service-1)
IFS=',' read -r -a FILES <<< "$CONFIG_FILES"
COMPOSE=(docker compose --project-name climate-phase3-vps --env-file .env.phase3.local)
for FILE in "${FILES[@]}"; do COMPOSE+=(-f "$FILE"); done
COMPOSE+=(-f /opt/climate/climate_backend/docker-compose.arduino.vps.yml)
"${COMPOSE[@]}" config --quiet &&
"${COMPOSE[@]}" up -d --no-deps --no-build --force-recreate --wait --wait-timeout 120 monitoring-service
```
Esto presupone imagen Monitoring actualizada a Fase4. Si aún usa una imagen previa, ejecutar `"${COMPOSE[@]}" build monitoring-service` antes del up, y no continuar si falla. Mantener este overlay en posteriores actualizaciones de Monitoring. No recrear Auth/frontend con una lista que omita sus propios overlays OAuth/dominio.

No se ejecutaron estos comandos contra el VPS desde esta sesión. Una vez confirmada la aceptación, seleccionar el sensor asociado en la gráfica de temperatura del dashboard del dominio.

## Retomar

Leer `FASE_4_PROGRESO.md`. Compilación y cambio de destino ya completados; pendientes verificar sensor/credencial/overlay del VPS y ejecutar el puente. No regenerar tokens, borrar colas ni repetir instalaciones de Arduino IDE.
