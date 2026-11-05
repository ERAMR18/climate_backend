# Guía 1: configurar otro UUID de sensor Arduino en el VPS

Esta guía cambia la asociación del dispositivo con un sensor existente en la base de datos del VPS. No mueve datos de la base local, no cambia GitHub y no requiere volver a cargar el Arduino solo por cambiar el UUID.

Flujo: sensor → Arduino → USB → puente C# en Windows → HTTPS del VPS → Monitoring → dashboard.

## Qué identificador cambia y dónde

| Dato | Dónde se configura | Debe coincidir con |
|---|---|---|
| UUID del sensor | Windows: `tools/Climate.ArduinoBridge/appsettings.json`, `Device.SensorId` | VPS: `ARDUINO_SENSOR_ID` y sensor activo del catálogo |
| Nombre del dispositivo | Firmware: `DEVICE_ID`; Windows: `Device.DeviceId` | VPS: `ARDUINO_DEVICE_ID` |
| Credencial privada | Windows: `device-token.local` | SHA256 del token en `ARDUINO_TOKEN_SHA256` del VPS |
| Origen | JSON del firmware o generador del puente | VPS: `ARDUINO_SOURCE` |
| Destino | Windows: `Backend.BaseUrl` | `https://analisissistemas2026proyecto.xyz` |

El UUID lo genera el backend al crear el sensor. No se escribe en el sketch: el puente hace la asociación. `device-token.local` contiene el token original; el hash de `.env` no se puede usar en su lugar.

## Paso 1 — Crear o elegir el sensor en la web

1. Entra a https://analisissistemas2026proyecto.xyz con permiso para administrar sensores.
2. Abre Sensores y selecciona uno o crea otro.
3. Debe ser de tipo Temperatura, unidad `C` o `°C`, estar activo y tener comunidad asignada.
4. Guarda y abre el detalle; copia el UUID de su URL.

No confundas el sensor del VPS con un sensor de localhost. Poner un nombre como «Arduino» no basta para excluirlo del simulador del servidor: también hay que aplicar esta asociación a Monitoring.

## Paso 2 — Detener el puente actual, en Windows

En su terminal pulsa `Ctrl+C`. Cierra el Monitor Serial del IDE si está abierto. No dejes dos puentes usando la misma identidad.

Si hay lecturas pendientes del UUID anterior, consérvalas. Idealmente termina de enviarlas antes de cambiar la asociación. El paso siguiente usará una cola distinta para el nuevo UUID, sin borrar la antigua ni cambiar los UUID de sus lecturas.

## Paso 3 — Actualizar la asociación local, en PowerShell

Sitúate en la raíz de tu proyecto; ajusta esta ruta si lo copiaste a otra carpeta:

```powershell
Set-Location 'C:\Users\casa\Documents\umg\desarrolloweb\webdev_proyect'
```

Elige el origen según lo que ejecutará la placa:

| Modalidad | Sketch / ejecución | Origen del VPS |
|---|---|---|
| DHT11 físico | `backend/lecturas-sensor/lecturas-sensor.ino` | `ARDUINO` |
| Temperatura simulada por la placa | `hardware/arduino/temperature-sensor/temperature-sensor.ino` con SIMULATE_SENSOR=1 | `SIMULATED` |
| Sin placa, simulación del puente | Lanzador con `-Simulate` | `SIMULATED` |

Ejecuta el bloque. Cuando aparezca la pregunta, pega el UUID y pulsa Enter. El UUID no se coloca dentro del texto de `Read-Host`.

```powershell
$newSensorId = Read-Host 'Pega el UUID del sensor del VPS y pulsa Enter'
$readingSource = Read-Host 'Escribe ARDUINO para DHT11 fisico o SIMULATED para datos de prueba'
powershell -NoProfile -ExecutionPolicy Bypass -File backend/scripts/Configure-Phase4Arduino.ps1 -SensorId $newSensorId -Source $readingSource
if ($LASTEXITCODE -ne 0) { throw 'Configuracion incompleta. Corrige el error antes de continuar.' }
```

El script conserva la credencial existente; si falta, genera una nueva y habrá que registrar su hash en el VPS. No lo vuelvas a ejecutar borrando tokens para solucionar errores de red.

Confirma destino HTTPS y usa una cola por UUID:

```powershell
$configPath = Join-Path (Get-Location) 'tools/Climate.ArduinoBridge/appsettings.json'
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$config.Backend.BaseUrl = 'https://analisissistemas2026proyecto.xyz'
$config.Backend.TelemetryPath = '/api/monitoring/telemetry'
$config.Backend.AllowLocalHttp = $false
$config.Buffer.Path = 'arduino-vps-' + $config.Device.SensorId + '.db'
[IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json -Depth 8))
```

Si cierras PowerShell, los archivos ya guardados permanecen: no necesitas recuperar `$newSensorId` ni `$readingSource` para los pasos siguientes.

## Paso 4 — Copiar solo los cinco parámetros Arduino

En PowerShell de Windows:

```powershell
Get-Content backend/.env.phase3.local |
  Where-Object { $_ -match '^ARDUINO_(DEVICE_ID|ENABLED|SENSOR_ID|SOURCE|TOKEN_SHA256)=' }
```

Copia únicamente esas cinco líneas. No copies contraseñas de bases, JWT ni credenciales GitHub. El hash se puede registrar en el servidor; el token original debe permanecer privado en la PC.

## Paso 5 — Guardar los parámetros en Bash del VPS

```bash
cd /opt/climate/climate_backend
nano .env.phase3.local
```

Actualiza las cinco variables con la salida de Windows. Deja una definición por variable, con `ARDUINO_ENABLED=true` y el origen elegido. Conserva el resto del archivo. Guarda con Ctrl+O, Enter; sal con Ctrl+X.

El `.env` no configura un contenedor por sí solo. Hay que inyectar las variables con el overlay y recrear Monitoring.

## Paso 6 — Comprobar el overlay en el VPS

```bash
cd /opt/climate/climate_backend
test -f docker-compose.arduino.vps.yml && echo 'Overlay Arduino presente'
```

Si no existe, y ya se publicó en GitHub, ejecuta `git pull --ff-only`. Si aún falta, créalo con este bloque; no contiene secretos:

```bash
if [ ! -f docker-compose.arduino.vps.yml ]; then
cat > docker-compose.arduino.vps.yml <<'YAML'
services:
  monitoring-service:
    environment:
      Telemetry__Devices__0__DeviceId: ${ARDUINO_DEVICE_ID:?Falta ARDUINO_DEVICE_ID}
      Telemetry__Devices__0__SensorId: ${ARDUINO_SENSOR_ID:?Falta ARDUINO_SENSOR_ID}
      Telemetry__Devices__0__TokenSha256: ${ARDUINO_TOKEN_SHA256:?Falta ARDUINO_TOKEN_SHA256}
      Telemetry__Devices__0__Source: ${ARDUINO_SOURCE:-SIMULATED}
      Telemetry__Devices__0__IsActive: ${ARDUINO_ENABLED:-false}
YAML
fi
```

No uses `docker-compose.phase4.yml` local en producción: fija localhost. Tampoco ejecutes seeds para cambiar un UUID.

## Paso 7 — Aplicar el cambio a Monitoring, en Bash del VPS

Este bloque recupera los archivos Compose que ya usa el contenedor y agrega el overlay solo si falta. No depende de tener la variable COMPOSE de otra terminal.

```bash
cd /opt/climate/climate_backend
CONFIG_FILES=$(docker inspect --format '{{ index .Config.Labels "com.docker.compose.project.config_files" }}' climate-phase3-vps-monitoring-service-1)
if [ -z "$CONFIG_FILES" ]; then echo 'No se encontro la configuracion de Monitoring'; else
  IFS=',' read -r -a FILES <<< "$CONFIG_FILES"
  COMPOSE=(docker compose --project-name climate-phase3-vps --env-file .env.phase3.local)
  HAS_ARDUINO=0
  for FILE in "${FILES[@]}"; do
    COMPOSE+=(-f "$FILE")
    if [ "$(basename "$FILE")" = 'docker-compose.arduino.vps.yml' ]; then HAS_ARDUINO=1; fi
  done
  if [ "$HAS_ARDUINO" -eq 0 ]; then COMPOSE+=(-f /opt/climate/climate_backend/docker-compose.arduino.vps.yml); fi
  "${COMPOSE[@]}" config --quiet &&
  "${COMPOSE[@]}" up -d --no-deps --no-build --force-recreate --wait --wait-timeout 180 monitoring-service
fi
```

Para cambiar solo UUID/token/origen no hay que reconstruir código. Si la imagen del VPS todavía es de antes de Fase4, primero ejecutar `"${COMPOSE[@]}" build monitoring-service` y repetir el `up` solo si la construcción termina correctamente. La instalación que ya confirmó HTTP200 no necesita esa reconstrucción por cambiar el UUID.

No recrees Auth/frontend con una lista que omita sus propios overlays de dominio/OAuth. No ejecutes `down -v`.

## Paso 8 — Verificar lo recibido por el contenedor

En el VPS, sin imprimir secretos:

```bash
docker exec climate-phase3-vps-monitoring-service-1 printenv Telemetry__Devices__0__SensorId
docker exec climate-phase3-vps-monitoring-service-1 printenv Telemetry__Devices__0__Source
docker exec climate-phase3-vps-monitoring-service-1 printenv Telemetry__Devices__0__IsActive
```

Las salidas deben coincidir con el nuevo UUID, el origen seleccionado y `true`. Desde ese momento el simulador servidor excluye ese sensor; sus históricos anteriores no se borran.

## Paso 9 — Iniciar el puente en Windows

Si es la primera compilación en esa PC o cambió el código:

```powershell
dotnet build tools/Climate.ArduinoBridge/Climate.ArduinoBridge.csproj --configuration Release
```

Después, con Monitor Serial cerrado:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Climate.ArduinoBridge/Start-ArduinoBridge.ps1 -PortName COM3 -Configuration Release -NoBuild
```

Cambia COM3 por el puerto real. Para leer la placa no agregues `-Simulate`; si quieres probar sin hardware, reemplaza `-PortName COM3` por `-Simulate` y usa origen SIMULATED en el VPS.

## Paso 10 — Comprobar el dashboard

1. Espera `Confirmed reading` en la consola del puente: HTTP200 significa aceptación del backend.
2. En el dominio, selecciona el sensor cuyo UUID acabas de configurar.
3. Verifica temperatura, fecha, fuente e histórico.
4. Ctrl+C detiene el puente. Sin nuevas lecturas, la interfaz marca datos obsoletos al superar su umbral (30s por defecto).

Una lectura ARDUINO es física; SIMULATED puede venir del firmware de prueba o del generador .NET. El adaptador DHT11 actual persiste temperatura; humidity es solo un campo auxiliar serial.

## Errores y cómo retomar

| Resultado | Qué revisar |
|---|---|
| 401 | Token/hash/asociación activa; si hay challenge Bearer vacío, comprobar imagen/ruta pública del Gateway |
| 403 | Coincidencia exacta de UUID y source con el registro del dispositivo |
| 400 | Sensor existente y activo, tipo/unidad, valor/fecha; un POST de prueba con `{}` produce400 esperado |
| 404 | Imagen Monitoring/ruta Gateway antigua; recrear con --no-build no actualiza una imagen vieja |
| Datos cambian con puente detenido | UUID aún no asociado correctamente o existe otro emisor; comprobar paso8 |
| COM ocupado | Cerrar Monitor Serial/otra instancia del puente |
| Cola de UUID anterior bloquea | No borrar ni cambiar payloads: conservar archivo antiguo y configurar cola distinta para el UUID nuevo como en paso3 |

Si cierras Bash, repite paso7 para recuperar COMPOSE. Si cierras Windows, vuelve a la raíz e inicia con paso9. Los archivos privados conservan configuración; no hay que repetir el seed ni regenerar credenciales. Un cambio de UUID no reasigna históricos previos.

Para revisar el punto general del proyecto:

```powershell
Get-Content .\FASE_4_PROGRESO.md
```
