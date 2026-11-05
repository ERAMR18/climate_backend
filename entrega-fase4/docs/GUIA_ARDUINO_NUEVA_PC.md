# Guía 2: Arduino y puente desde una PC Windows nueva

Objetivo: conectar el Arduino por USB a otra PC y enviar datos al VPS. El servidor no necesita otro despliegue si dispositivo, UUID, token y source son los mismos.

El Arduino conserva el sketch cuando se desconecta. Cambiar de PC o de puerto COM no exige volver a cargarlo. Arduino IDE es opcional si solo vas a ejecutar el puente con una placa ya programada.

## Paso 1 — Detener el emisor de la PC anterior

Pulsa Ctrl+C en la consola del puente y cierra Monitor Serial. Evita dos emisores con la misma identidad. Las lecturas pendientes quedan en SQLite de la PC anterior: no copies una cola mientras su proceso está escribiendo.

No necesitas transportar el `.env` completo del backend a otra PC. Ese archivo puede contener credenciales de bases de datos, JWT y OAuth que el puente no utiliza.

## Paso 2 — Copiar los archivos necesarios

Usa como carpeta de ejemplo `C:\ClimateArduino`. Puedes copiar todo el código del workspace por un medio privado, excluyendo credenciales del backend y frontend/node_modules, o copiar solamente:

```text
C:\ClimateArduino\
├── tools\
│   ├── Climate.ArduinoBridge\
│   │   ├── Climate.ArduinoBridge.csproj
│   │   ├── Program.cs y todos los demas archivos .cs del proyecto
│   │   ├── Start-ArduinoBridge.ps1
│   │   ├── appsettings.example.json
│   │   ├── appsettings.json          (privado, copiar aparte)
│   │   └── device-token.local        (privado, copiar aparte)
│   └── Arduino-Uno.ps1              (si quieres compilar/cargar firmware)
├── hardware\arduino\temperature-sensor\temperature-sensor.ino
└── backend\lecturas-sensor\lecturas-sensor.ino
```

Los `.ino` solo son necesarios para recompilar/subir firmware. No copies bin/obj: se generan al compilar. Los archivos privados appsettings.json y device-token.local están ignorados por Git y no aparecen al clonar; hay que transferirlos de forma privada desde la PC que funcionaba. No pegues su contenido en chats ni los publiques en GitHub.

Con esta estructura puedes usar los mismos comandos de la guía. Si copias otra ruta, cambia el `Set-Location` correspondiente.

Si no conservas el token original, el hash del VPS no permite recuperarlo. Será necesario generar una credencial nueva y actualizar su hash en el VPS mediante la [guía de cambio de asociación](GUIA_ARDUINO_CAMBIAR_SENSOR_UUID.md). No recrees credenciales si simplemente cambió el puerto COM.

## Paso 3 — Instalar SDK .NET 10

En PowerShell de la nueva PC:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact --source winget --accept-package-agreements --accept-source-agreements
```

Cierra y vuelve a abrir PowerShell después de instalar. Comprueba:

```powershell
dotnet --list-sdks
```

Debe aparecer un SDK10.x. No basta con instalar únicamente .NET Framework o el runtime si vas a compilar. Si WinGet no existe, usa el instalador del SDK desde la [guía oficial de Microsoft](https://learn.microsoft.com/es-es/dotnet/core/install/windows).

## Paso 4 — Conectar el Uno y encontrar el puerto

Conecta el Arduino con un cable USB de datos. En Administrador de dispositivos de Windows abre Puertos (COM y LPT) y anota el puerto del Arduino. Puede ser COM3, COM4 u otro.

También puedes consultar desde PowerShell:

```powershell
Get-CimInstance Win32_SerialPort | Select-Object DeviceID,Name
```

Si no aparece ningún puerto, revisa cable/USB y el controlador del modelo real de placa. Un Uno compatible puede usar un conversor USB distinto; no instales un driver CH340 para todas las placas indiscriminadamente. La PC anterior reconoció el Uno en COM3, pero la nueva puede asignar otro número.

## Paso 5 — Revisar appsettings.json sin imprimir el token

```powershell
Set-Location 'C:\ClimateArduino'
notepad tools/Climate.ArduinoBridge/appsettings.json
```

Comprueba estos datos, conservando el UUID de tu sensor registrado:

```json
{
  "Arduino": {
    "PortName": "COM3",
    "BaudRate": 9600,
    "ReconnectSeconds": 5,
    "Simulate": false
  },
  "Backend": {
    "BaseUrl": "https://analisissistemas2026proyecto.xyz",
    "TelemetryPath": "/api/monitoring/telemetry",
    "TimeoutSeconds": 10,
    "AllowLocalHttp": false
  },
  "Device": {
    "DeviceId": "arduino-temp-001",
    "SensorId": "CONSERVA_EL_UUID_REAL_DEL_SENSOR_VPS"
  },
  "Buffer": {
    "Path": "arduino-vps-buffer.db",
    "MaxReadings": 10000,
    "RetentionHours": 168
  }
}
```

El texto CONSERVA_EL_UUID_REAL_DEL_SENSOR_VPS es ilustrativo: no lo guardes como valor real. Si transferiste la configuración de la PC anterior, ya debe tener el UUID correcto; no reemplaces el archivo entero por este ejemplo. El token no va dentro de este JSON: el lanzador lo obtiene de device-token.local.

Comprueba únicamente que el archivo de credencial existe:

```powershell
Test-Path tools/Climate.ArduinoBridge/device-token.local
```

Debe responder True. Si vas a recuperar pendientes de otra PC, detén ambos puentes y conserva también los archivos SQLite/WAL/SHM del mismo conjunto antes de moverlos. No mezcles colas de dispositivos distintos. Para comenzar una cola nueva no necesitas copiar las bases de pruebas anteriores.

## Paso 6 — Compilar y ejecutar el puente

Desde `C:\ClimateArduino`, con Monitor Serial cerrado:

```powershell
dotnet build tools/Climate.ArduinoBridge/Climate.ArduinoBridge.csproj --configuration Release
```

Si termina correctamente:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Climate.ArduinoBridge/Start-ArduinoBridge.ps1 -PortName COM3 -Configuration Release -NoBuild
```

Cambia COM3 por el puerto encontrado. Este comando lee el Arduino físico. No agregues -Simulate si quieres leer el puerto USB; esa opción genera datos en .NET sin usar la placa.

Espera mensajes `Confirmed reading`. Abre el dashboard del dominio y selecciona el sensor correspondiente al UUID. Ctrl+C detiene el puente.

Para siguientes ejecuciones basta el comando del lanzador; no recompiles ni reinstales si el código no cambió. Windows debe permanecer encendido y con conexión a Internet para enviar. El Uno sin Wi-Fi no puede enviar HTTPS por sí mismo; si desconectas la PC, el dashboard dejará de recibir datos recientes.

## Paso 7 opcional — Instalar IDE para cambiar o cargar el firmware

Omite esta sección si la placa ya tiene el programa correcto y solo cambiaste de PC.

```powershell
winget install --id ArduinoSA.IDE.stable --exact --source winget --accept-package-agreements --accept-source-agreements
```

Abre Arduino IDE:

1. En Boards Manager instala Arduino AVR Boards.
2. Para DHT11, en Library Manager instala DHT sensor library de Adafruit y acepta Adafruit Unified Sensor como dependencia.
3. Selecciona Arduino Uno y el puerto real.
4. Elige uno de los dos sketches, según la tabla siguiente.

| Modo | Archivo | ARDUINO_SOURCE del VPS |
|---|---|---|
| DHT11 físico en DATA2 | `backend/lecturas-sensor/lecturas-sensor.ino` | ARDUINO |
| Simulación en Uno, sin sensor | `hardware/arduino/temperature-sensor/temperature-sensor.ino`, SIMULATE_SENSOR=1 | SIMULATED |

Cierra el puente antes de subir. Desde PowerShell puedes usar:

```powershell
# Compilar DHT11 sin cargar
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Arduino-Uno.ps1 -SketchPath backend/lecturas-sensor
# Compilar y cargar DHT11 (reemplaza el programa actual de la placa)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Arduino-Uno.ps1 -SketchPath backend/lecturas-sensor -Upload -PortName COM3
```

O para cargar la simulación del Uno:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Arduino-Uno.ps1 -Upload -PortName COM3
```

No subas ambos sketches: el último reemplaza al anterior. Tras cargar, puedes comprobar JSON en Monitor Serial a9600baud y cerrarlo antes de ejecutar el puente. DHT11 físico necesita sensor cableado según las etiquetas del módulo, DATA2 y tierra común; si emite DHT11_READ_FAILED, revisar sensor/cableado, no el dominio. La humedad aparece en JSON pero el puente actual solo persiste temperatura.

Si cambias de SIMULATED a ARDUINO, actualiza la variable ARDUINO_SOURCE del VPS y recrea Monitoring con sus overlays como explica la [guía1](GUIA_ARDUINO_CAMBIAR_SENSOR_UUID.md). No basta con cambiar el sketch.

## ¿Cuándo debo volver a configurar el VPS?

| Cambio | ¿Modificar VPS? |
|---|---|
| Otra PC con mismo Arduino, UUID, deviceId, token y source | No |
| Puerto COM3 cambia a COM5 | No; usar -PortName COM5 |
| Reiniciar Windows o volver a conectar el cable | No; reiniciar el puente |
| Nuevo UUID o token | Sí, actualizar asociación/hash y recrear Monitoring |
| Cambiar source SIMULATED/ARDUINO | Sí |
| Nuevo dispositivo independiente | Sí, registrar identidad y credencial propias; no copiar la misma identidad para dos emisores simultáneos |

El overlay actual configura un dispositivo en posición0. Configurar varios dispositivos simultáneos requiere ampliar la lista Telemetry.Devices, con sensorId/deviceId únicos y credencial propia por dispositivo.

## Diagnóstico y recuperación

| Síntoma | Acción |
|---|---|
| No encuentra SDK | Instalar SDK10 y reabrir PowerShell |
| No existe token | Transferir device-token.local de forma privada; no usar el hash ni secretos OAuth |
| COM ocupado | Cerrar Monitor Serial y otro puente |
| 401/403 | Revisar UUID, source y token/hash; el puente detiene envíos y conserva pendientes; corregir y reiniciar |
| 404 | Endpoint/ruta/imagen del VPS desactualizados; no es un error de sensor |
| Red caída | Mantener SQLite; reintentos con backoff, hasta límites de buffer/retención configurados |
| Datos obsoletos | Revisar si el puente corre, sensor seleccionado y última lectura; el valor histórico no implica conexión actual |

Al retomar, ve al paso6 si ya instalaste/copiado todo. No vuelvas a clonar ni crear otro sensor solo por reiniciar. Registra el UUID, COM y último paso completado en una nota privada, sin incluir el token. En la carpeta original puedes consultar `Get-Content .\FASE_4_PROGRESO.md`.

Referencias oficiales: [.NET en Windows](https://learn.microsoft.com/es-es/dotnet/core/install/windows), [instalación Arduino IDE](https://support.arduino.cc/hc/en-us/articles/360019833020-Download-and-install-Arduino-IDE), [Arduino CLI](https://docs.arduino.cc/arduino-cli/installation), [biblioteca DHT Adafruit](https://github.com/adafruit/DHT-sensor-library).
