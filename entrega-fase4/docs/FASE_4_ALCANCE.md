# Fase 4: alcance y operación local

Fecha: 2026-10-29. Alcance autorizado: `phase_4.md` de la raíz y solicitud del usuario, incluyendo datos de sensores simulados por Arduino. El documento adjunto define los requisitos técnicos; no autoriza despliegues ni cambios productivos. El requisito oficial se conserva en `references/phase_4.official.md`. La URL oficial de Fase 1 responde 404; Fases 2 y 3 sí se conservaron.

Se extiende Auth NestJS/PostgreSQL y Monitoring .NET/SQL Server. Se conservan Sensor .NET, Community .NET/PostgreSQL, Alert Java/PostgreSQL, Report y Audit Python/MongoDB, Event .NET, Gateway y Angular. No se agrega un microservicio IoT. El puente se ejecuta en la PC conectada al Arduino.

Incluye Google OIDC, GitHub OAuth, JWT y roles existentes, identidad externa sin fusión automática por email, firmware de referencia/simulación, puente .NET 10, buffer SQLite, ingestión autenticada y limitada por sensor, migración SQL aditiva, evaluación de alertas con reintentos, SignalR existente y gráfica de temperatura con selección de sensor y fuentes diferenciadas.

Quedan fuera VPS, K3s, DNS, TLS público, Secrets productivos, registros privados y pruebas de hardware sin placa. El login con proveedores reales requiere credenciales del responsable. Ver `FASE_4_PENDIENTES_FUERA_SCOPE.md`; estos puntos no se declaran cumplidos.

Desde la raíz en PowerShell:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File backend/scripts/Start-Phase4.ps1
```
Este comando construye los cuatro servicios modificados y reutiliza las imágenes de Fase 3 para los demás. En una instalación sin esas imágenes usar `-BuildAll`. Si ya están construidas usar `-NoBuild`. Se reutiliza el proyecto `climate-phase3-local` y sus volúmenes; no ejecutar `down -v`.

Frontend: http://localhost:4200. API: http://localhost:8080. Usuario inicial según `ADMIN_SEED_USERNAME` de `backend/.env.phase3.local`; contraseña en `AUTH_ADMIN_SEED_PASSWORD` del mismo archivo privado. No publicar su contenido. La demostración se completa por APIs sin duplicar catálogos.

Si un proxy requiere la CA existente del proyecto para construir Auth:
```powershell
docker build --secret id=npm_ca,src=tools/trusted-roots.pem -t climate/auth:phase3 backend/src/Services/AuthService
powershell -NoProfile -ExecutionPolicy Bypass -File backend/scripts/Start-Phase4.ps1
```
No desactivar validación TLS. Las imágenes ajenas al cambio se reutilizan; Maven de Alert requiere una cadena de certificados válida para `-BuildAll`.

Para retomar sin repetir trabajo: `Get-Content .\FASE_4_PROGRESO.md`. Esa bitácora mantiene el último punto comprobado y comandos concretos.
