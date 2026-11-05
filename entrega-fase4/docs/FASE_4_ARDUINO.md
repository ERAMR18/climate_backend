# Arduino y datos de prueba

## Sensor físico DHT11

`backend/lecturas-sensor/lecturas-sensor.ino` es el adaptador DHT11: DATA en pin digital2, serial9600, muestra cada3s. VCC/GND según etiquetas y especificación del módulo; DHT11 suelto puede requerir resistencia pull-up conforme al fabricante. Bibliotecas DHT sensor library de Adafruit y Adafruit Unified Sensor disponibles. Referencia: https://github.com/adafruit/DHT-sensor-library .

Se adaptó el JSON al mismo contrato v1 del puente y se compiló con Arduino Uno: flash5626 bytes, RAM243 bytes. No se realizó carga del firmware DHT11 durante esta adaptación.
```powershell
# Verificar este sketch particular, sin cargar
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Arduino-Uno.ps1 -SketchPath backend/lecturas-sensor
# Cargar el DHT11, reemplaza el sketch que tenga el Uno
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Arduino-Uno.ps1 -SketchPath backend/lecturas-sensor -Upload -PortName COM3
```
El script sin -SketchPath continúa seleccionando el sketch simulado anterior. El nuevo firmware emite source ARDUINO; cambiar ARDUINO_SOURCE=ARDUINO en el VPS y recrear Monitoring conservando overlays. No requiere cambiar credencial ni sensorUUID. Cerrar Monitor Serial antes de ejecutar el puente. Temperature se publica a dashboard; humidity aparece solo como dato auxiliar del frame y no está integrada a persistencia/histórico del puente actual.

## Herramientas instaladas y verificación (2026-11-04)

Arduino IDE 2.3.10 instalado en Windows con Arduino CLI 1.5.1 incluido y Arduino AVR Boards 1.8.8. Placa detectada por CLI: Arduino UNO, COM3, FQBN arduino:avr:uno. Firmware simulado compilado correctamente: flash 3920 bytes (12%), RAM 204 bytes (9%). No se cargó firmware durante la instalación.

Desde la raíz del proyecto:
```powershell
# Compilar sin cambiar el programa de la placa
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Arduino-Uno.ps1
# Compilar y cargar cuando se quiera reemplazar el sketch del Uno
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Arduino-Uno.ps1 -Upload -PortName COM3
```
Alternativa gráfica: abrir Arduino IDE desde Inicio, abrir el sketch, seleccionar Arduino AVR Boards → Arduino Uno y COM3, pulsar Verificar y luego Subir. Monitor Serial: 9600 baudios, frames JSON cada dos segundos. Cerrar Monitor Serial antes de iniciar ArduinoBridge; solo una aplicación puede ocupar COM3. Puerto puede cambiar al reconectar.

Esta verificación supera la limitación previa de compilación no realizada. Carga física, lectura serial del firmware cargado y medición con LM35 siguen requiriendo su verificación específica.

Referencia: Arduino Uno compatible con Arduino IDE; LM35 en A0, alimentación 5 V, tierra común. Verificar el pinout del LM35 según su encapsulado: no deducir orientación del nombre. Salida del LM35 a A0; conversión de referencia 5 V / ADC de 10 bits / 10 mV por °C. Ajustar ADC_REFERENCE_VOLTS al voltaje real. Este cableado no sirve directamente para DHT11/DHT22/DS18B20; esos sensores necesitan su librería, cableado y adaptador de lectura, conservando el JSON.

Abrir `hardware/arduino/temperature-sensor/temperature-sensor.ino` en Arduino IDE. Elegir placa y puerto reales, compilar y subir. El valor inicial `SIMULATE_SENSOR 1` genera una onda de 20 a 40 °C sin conectar sensor físico, útil para comprobar reglas. Para LM35 cambiar a `0`. Frecuencia `SAMPLE_INTERVAL_MS=2000`, baud `9600`, deviceId `arduino-temp-001`. El loop utiliza millis, no delay. No hay Wi-Fi ni Ethernet. El firmware no inventa timestamps.

Cada línea termina con newline:
```json
{"protocolVersion":1,"deviceId":"arduino-temp-001","sensorType":"TEMPERATURE","value":27.5,"unit":"C","source":"SIMULATED","sequence":125}
```
El modo LM35 emite source ARDUINO. Los errores emiten un frame con `error`, que el puente registra y rechaza como lectura. Sin RTC, el puente asigna UTC al recibir el mensaje. El protocolo no exige características particulares de Arduino: otras placas pueden implementar el mismo contrato.

Arrancar el backend según `FASE_4_ALCANCE.md`. Iniciar sesión y elegir o registrar un sensor Temperature activo con unidad C en una comunidad activa. Puede usarse un sensor demo, pero desde su asociación quedará excluido de simulación del servidor. Copiar su UUID desde el detalle del sensor.

Desde la raíz:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File backend/scripts/Configure-Phase4Arduino.ps1 -SensorId <UUID_REAL> -Source SIMULATED
powershell -NoProfile -ExecutionPolicy Bypass -File backend/scripts/Start-Phase4.ps1 -NoBuild
```
El script conserva el token local si existe, escribe solo su hash en la configuración del servidor, y actualiza la asociación sin duplicar variables. La comunidad se deriva del Sensor existente; no se duplica catálogo. El puente recibe su configuración local sin guardar token en appsettings. `device-token.local` es privado y está excluido de Git. Protegerlo con permisos del usuario de Windows; no subirlo a respaldos públicos.

Sin Arduino, ejecutar la fuente serial simulada de .NET:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Climate.ArduinoBridge/Start-ArduinoBridge.ps1 -Simulate
```
Con Arduino y firmware simulado (cerrar primero el Monitor Serial del IDE):
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Climate.ArduinoBridge/Start-ArduinoBridge.ps1 -PortName COM3
```
Cambiar COM3 por el puerto real. Si se omite puerto en configuración, la detección automática exige exactamente uno. Para el sensor físico configurar `-Source ARDUINO`, subir firmware con SIMULATE_SENSOR=0 y reiniciar el stack con -NoBuild. Detener puente con Ctrl+C antes de cambiar modo; la cola conserva lecturas antiguas, por lo que conviene vaciarla por confirmación antes de cambiar source.

Dashboard → elegir comunidad/sensor en Temperatura. Muestra °C, origen, última lectura, comunicación reciente/historial. El umbral visual es configurable por input `staleAfterSeconds` del componente (30 s por defecto). La existencia de un sensor no implica conexión.

Puente requiere SDK .NET 10 para `dotnet run`, o publicar con `dotnet publish tools/Climate.ArduinoBridge/Climate.ArduinoBridge.csproj -c Release -r win-x64 --self-contained true`. Copiar appsettings.json privado al directorio de ejecución e inyectar Backend__DeviceToken desde un almacén seguro. No usar credenciales de administrador en el puente.

Errores frecuentes: COM ocupado → cerrar Monitor Serial; JSON rechazado → revisar deviceId/protocolo/C/rango; 401/403 → detener y corregir token/asociación/fuente, reiniciar puente; 400 → sensor inactivo, unidad, rango o timestamp; red caída → SQLite conserva y reintenta. Cola llena → advertencia explícita y adquisición pausada/reconectada; lecturas que excedan capacidad pueden perderse y requieren intervención. Retención por defecto siete días y 10 000 pendientes. Nunca se probó una placa física si el informe solo registra simulación.
