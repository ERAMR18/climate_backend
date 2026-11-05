from pathlib import Path

root = Path(__file__).resolve().parent.parent
rows = []
def row(identifier, requirement, initial, scope, implementation, validation, final='Implementado local; ver pruebas', external='—'):
    rows.append([identifier, requirement, initial, scope, implementation, 'Código y docs/FASE_4_PRUEBAS.md', validation, final, external])
for identifier, requirement, implementation in [
    ('RF-OAUTH-01','OAuth 2.0','Auth OAuthService/openid-client'),
    ('RF-OAUTH-02','OpenID Connect','Google discovery, ID token y nonce'),
    ('RF-OAUTH-03','Google','Google OIDC + botón Angular'),
    ('RF-OAUTH-04','GitHub','OAuth + /user y /user/emails'),
    ('RF-OAUTH-05','Extensibilidad','Registro de metadata revisada y adaptador OIDC'),
]:
    row(identifier,requirement,'Sin flujo externo','Sí',implementation,'Pruebas Auth con proveedor simulado','Código integrado; login real pendiente' if identifier in ('RF-OAUTH-03','RF-OAUTH-04') else 'Implementado en código','Credenciales reales' if identifier in ('RF-OAUTH-03','RF-OAUTH-04') else '—')
for identifier, requirement, implementation in [
    ('RNF-OAUTH-01','Secretos fuera del código','Variables de entorno y archivos privados ignorados'),
    ('RNF-OAUTH-03','Administración por roles','AuthGuard/guards/interceptor y policies existentes'),
    ('RNF-OAUTH-04','Rechazar JWT expirado','Tokens.verify y validadores por servicio'),
    ('RNF-OAUTH-05','Validación de APIs internas','JWT + claves internas restringidas existentes'),
]: row(identifier,requirement,'Parcial/existente','Sí',implementation,'Regresión Auth/JWT y stack local')
row('RNF-OAUTH-02','Secrets Kubernetes reales','Manifiestos de ejemplo','No ejecutar','Inyección por configuración preparada','Validación productiva no realizada','Pendiente externo','Secret store y responsable de infraestructura')
for identifier, requirement in [
    ('K8S-10-CLUSTER','Clúster y K3s'),('K8S-11-PODS','Pods'),('K8S-11-DEPLOY','Deployments'),
    ('K8S-12-SERVICE','Services internos'),('K8S-12-INGRESS','Ingress HTTPS'),('K8S-13-CONFIG','ConfigMaps'),
    ('K8S-13-SECRET','Secrets productivos'),('K8S-14-PV','Persistent Volumes/PVC'),('K8S-14-HEALTH','Probes liveness/readiness'),
    ('K8S-OBJ7-SCALE','Escalabilidad horizontal'),('K8S-OBJ8-DNS','Dominio DNS'),('K8S-OBJ8-TLS','TLS público'),
]: row(identifier,requirement,'Manifiestos/documentación Fase3','No ejecutar','backend/k8s reutilizable','No aplicado ni validado en VPS','Excluido; no cumplido','Ver FASE_4_PENDIENTES_FUERA_SCOPE.md')
row('F3-CONTINUIDAD','Servicios heterogéneos, bases, reglas, reportes, auditoría y simulador','Fase3 implementada','Sí preservar','No reemplazar servicios propietarios','Regresión + stack Docker local')
requirements = [
    ('ARD-08','USB serial sin asumir conectividad','ISerialDataSource/System.IO.Ports'),
    ('ARD-09','Arquitectura PC → Gateway → Monitoring existente','Sin microservicio ni Hub adicional'),
    ('ARD-10','Firmware LM35 y generador simulado','Sketch configurable, JSON v1, millis y errores'),
    ('ARD-11-PORT','Puerto/baud/detección/reconexión','SerialDataSource + AcquisitionWorker'),
    ('ARD-11-DTO','DTO tipado/validación/asociación','Protocol.Parse + DeviceOptions'),
    ('ARD-11-HTTP','HttpClient/DI/Worker/cancelación','TransmissionWorker y configuración validada'),
    ('ARD-12','Opciones, secretos y HTTPS','appsettings.example y token externo; loopback explícito'),
    ('ARD-13-BUFFER','Cola persistente acotada','SQLite WAL, MaxReadings, FIFO y retención'),
    ('ARD-13-RETRY','Backoff y errores permanentes','429/5xx reintentan; 400 elimina con diagnóstico; 401/403 detiene'),
    ('ARD-13-ID','Idempotencia estable','UUID en payload persistido antes de HTTP'),
    ('ARD-14','Ingestión limitada por dispositivo','TelemetryController + hash/token + catálogo'),
    ('ARD-15','Dispositivo/sensor/comunidad/tipo/estado/fuente','Configuración por sensor; comunidad del catálogo existente'),
    ('ARD-16','Reglas existentes y ausencia de alertas duplicadas','Dispatch persistente + readingId procesado por Java'),
    ('ARD-17','SignalR y recuperación REST','Hub existente, eventos y sincronización REST'),
    ('ARD-18','Gráfico actual/histórico y selección de sensor','TemperaturePanel usa SensorChart existente sin gráfica agregada duplicada'),
    ('ARD-19','Simulación convive y fuentes distinguibles','Exclusión del sensor de dispositivo; Source en SQL/DTO/chart'),
    ('ARD-20','Pruebas sin hardware','Pruebas .NET, Auth, Angular e integración Docker'),
    ('ARD-21','UX: accesibilidad, loading/error/empty y datos obsoletos','Panel responsive, etiquetas, estado por antigüedad y foco visible'),
    ('ARD-24','Documentación y matriz','Siete documentos de Fase4 y bitácora recuperable'),
]
for identifier, requirement, implementation in requirements:
    row(identifier,requirement,'No existía integración Arduino','Sí',implementation,'Ver FASE_4_PRUEBAS.md', 'Código de referencia; compilación/subida física pendiente' if identifier == 'ARD-10' else 'Implementado; evidencia en informe', 'Placa/IDE/sensor' if identifier == 'ARD-10' else '—')
# Estado posterior al cierre local: confirmaciones aportadas por el usuario en VPS.
for values in rows:
    if values[0] == 'RF-OAUTH-04':
        values[5] = 'Código, tests issuer y confirmación del usuario en VPS'
        values[6] = 'Login real confirmado por usuario; correcciones Gateway/issuer/configuración'
        values[7] = 'Implementado; operación real reportada'
        values[8] = 'Mantener credenciales vigentes; no se incluyen valores'
    if values[0] == 'ARD-10':
        values[4] = 'Sketch simulado/LM35 y adaptador DHT11; compilados para Uno'
        values[5] = 'Arduino CLI y salida upload aportada por usuario'
        values[6] = 'Simulado: flash3920/RAM204; DHT11: flash5626/RAM243'
        values[7] = 'Compilados; carga del simulador confirmada; medición DHT11 pendiente'
        values[8] = 'Confirmar carga/medición/calibración DHT11'
    if values[0] in ('ARD-08','ARD-11-HTTP','ARD-14'):
        values[5] = 'Tests locales y HTTP200 aportado por usuario del VPS'
        values[7] = 'Implementado; aceptación de telemetría en VPS reportada'
    if values[0] == 'ARD-24':
        values[4] = 'Markdown, guías UUID/nuevaPC, PDF Fase4 y PDF general actualizado'
        values[5] = 'docs/FASE_4_DOCUMENTACION_COMPLETA.md y PDFs generados'
out = '# Matriz Fase 4\n\nLos IDs K8S/ARD son identificadores locales ligados a las secciones oficiales o del prompt; RF/RNF conservan sus IDs oficiales. No equiparar código integrado con validación de proveedores reales o hardware.\n\n'
out += 'Estado actualizado al 4 de noviembre de 2026: VPS Docker/GitHub/HTTP200 reportados por el usuario; Kubernetes sigue sin ejecutarse. DHT11 compilado no equivale a medición física validada.\n\n'
out += '| ID | Requisito | Estado inicial | En alcance | Implementación | Evidencia | Validación | Estado final | Pendiente externo |\n'
out += '|---|---|---|---|---|---|---|---|---|\n'
out += ''.join('| ' + ' | '.join(values) + ' |\n' for values in rows)
(root / 'docs/FASE_4_MATRIZ_REQUISITOS.md').write_text(out, encoding='utf-8')
print(f'{len(rows)} requirement rows saved')
