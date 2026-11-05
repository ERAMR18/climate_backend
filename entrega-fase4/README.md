# Entrega de Fase 4

PDF actualizado, documentación editable y recursos para Arduino/puente C#.

- `Documentacion_Climate_Monitoring_Fase_4.pdf`: 54 páginas de la fase.
- `Documentacion_Climate_Monitoring_System.pdf`: 62 páginas; edición actual y Fase3 histórica.
- `docs/GUIA_ARDUINO_CAMBIAR_SENSOR_UUID.md`: asociación de otro sensor al VPS.
- `docs/GUIA_ARDUINO_NUEVA_PC.md`: instalar y ejecutar desde otro Windows.
- `backend/lecturas-sensor/lecturas-sensor.ino`: DHT11 físico.
- `hardware/arduino/temperature-sensor/temperature-sensor.ino`: simulación y referencia LM35.
- `tools/Climate.ArduinoBridge`: código del puente y ejemplo de configuración.

La estructura interna conserva las rutas del workspace: para usar estos recursos,
considera esta carpeta como raíz del paquete. Los comandos de las guías se ejecutan
desde aquí después de adaptar su ruta de ejemplo de Windows.

No se incluyen .env, appsettings.json privado, device-token.local, bases SQLite,
credenciales, binarios de aplicaciones ni dependencias descargadas. Transferir
la configuración y el token del puente por un medio privado según la guía.
Para usar Configure-Phase4Arduino.ps1 en este paquete, preparar primero un archivo
privado backend/.env.phase3.local (puede estar vacío inicialmente); no copiar
contraseñas productivas de bases de datos solo para configurar Arduino.

El backend que se despliega es la raíz del repositorio, fuera de esta carpeta.
No ejecutar Docker Compose desde el backend reducido de este paquete: contiene
solo los recursos de configuración/firmware necesarios para el puente.

Para regenerar PDF en Windows desde esta carpeta:

```powershell
python -m pip install --target tools/pdf-libs -r tools/requirements-phase4-pdf.txt
python tools/phase4-doc-matrix.py
python tools/generate-phase4-pdf.py
python tools/validate-phase4-pdf.py
```

Historial y estado: FASE_4_PROGRESO.md. Los resultados de VPS aportados por usuario
y las validaciones locales se distinguen en el PDF; no se declara Kubernetes desplegado.
