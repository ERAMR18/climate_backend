# Kubernetes — despliegue y diagnóstico

La [arquitectura](phase-2-architecture.md) mantiene SQL Server fuera del clúster.
Los 24 manifiestos de `k8s/` incluyen namespace, ocho Deployments, Services
ClusterIP, ConfigMaps y RabbitMQ con StatefulSet/PVC/NetworkPolicy.
La [guía detallada](../k8s/README.md) explica secretos, retención, probes y escalado.

## Construir y seleccionar imágenes

Desde `backend`:

```powershell
./scripts/Build-Images.ps1 -IncludeFrontend -Tag phase2
```

Para otro registro, usar el prefijo elegido por el equipo; el script no publica:

```powershell
./scripts/Build-Images.ps1 -IncludeFrontend -ImagePrefix registry.example.invalid/equipo/climate -Tag release-001
```

Publicar esas ocho imágenes cuando el registro esté disponible, o cargarlas en
todos los nodos del clúster local. Sustituir `image:` de cada Deployment por su
nombre correspondiente, o añadir al `kustomization.yaml` entradas como:

```yaml
images:
  - name: climate/gateway
    newName: registry.example.invalid/equipo/climate/gateway
    newTag: release-001
  - name: climate/frontend
    newName: registry.example.invalid/equipo/climate/frontend
    newTag: release-001
```

Repetir para identity, sensors, monitoring, alerts, events y audit. Preferir
digests para releases inmutables. El dominio del ejemplo debe sustituirse;
ningún registry es obligatorio. Si es privado, configurar `imagePullSecrets`.

Si npm necesita una CA corporativa ya confiable, puede pasarse durante el build
con `-NpmCaFile ruta/ca.pem`, o `docker build --secret id=npm_ca,src=ruta/ca.pem ...`
en el frontend. Solo se monta en la etapa de compilación; no queda en el runtime.
La validación TLS permanece activa.

## Configurar y aplicar

1. SQL sigue ejecutándose mediante Docker. En `.env`, definir la interfaz
   `SQLSERVER_BIND_ADDRESS` y puerto alcanzables desde los nodos. El DNS del host
   debe resolverse desde los Pods; `localhost` dentro de un Pod no es ese host.
2. Copiar `k8s/secrets.example.yaml` a `k8s/secrets.yaml` y completar claves,
   credenciales y las seis conexiones SQL. No versionar el archivo real.
3. Configurar el origen público del Gateway en `frontend/configmap.yaml` y el
   origen exacto frontend en `gateway/configmap.yaml` para CORS.
4. Confirmar StorageClass predeterminada para el PVC y CNI que aplique NetworkPolicy.
5. Validar el renderizado y aplicar sobre el contexto seleccionado:

```powershell
kubectl config current-context
kubectl kustomize k8s
python -m pip install -r scripts/requirements-manifests.txt
python scripts/validate-manifests.py
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -k k8s
kubectl -n climate-monitoring get pods,services,pvc
kubectl -n climate-monitoring rollout status deployment/gateway
kubectl -n climate-monitoring rollout status statefulset/rabbitmq
```

Las APIs toleran dependencias tardías; no hay initContainers que compilen o
reproduzcan `depends_on`. RabbitMQ y las seis APIs carecen de servicios públicos.
La consola Management tampoco está publicada por defecto.

## Comprobar y consultar logs

```powershell
kubectl -n climate-monitoring logs deployment/audit --tail=100
kubectl -n climate-monitoring logs statefulset/rabbitmq --tail=100
kubectl -n climate-monitoring describe pod rabbitmq-0
kubectl -n climate-monitoring get events --sort-by=.lastTimestamp
kubectl -n climate-monitoring exec rabbitmq-0 -- rabbitmqctl list_queues name messages_ready messages_unacknowledged
kubectl -n climate-monitoring exec deployment/identity -- dotnet /app/health/Climate.HealthProbe.dll
```

Para acceso local, usar port-forward de Gateway y frontend según la guía; los
orígenes deben coincidir con CORS/runtime config. Las consultas SQL sobre outbox
deben hacerse con credenciales autorizadas en la base del servicio correspondiente.

## Diagnóstico

| Síntoma | Revisar |
|---|---|
| ImagePullBackOff | Nombre/tag/digest, carga en nodos y credenciales del registro |
| PVC Pending | StorageClass, capacidad y modo ReadWriteOnce |
| API no inicia | Conexión SQL externa, DNS, puerto/firewall, credenciales y logs de migración |
| Readiness falla pero liveness responde | SQL o dependencia no disponible; no reiniciar indefinidamente el proceso por ese motivo |
| Outbox crece | Broker/DNS, credenciales AMQP, NetworkPolicy y logs del publisher |
| Cola crece | Audit disponible, SQL AuditDb y logs del consumidor |
| DLQ crece | JSON/validación o errores SQL agotados; revisar antes de reenviar con el mismo EventId |
| Frontend no inicia | `GATEWAY_PUBLIC_URL` válido y `/runtime-config.json` |
| Login/CORS o SignalR falla | Origen frontend exacto, URL pública Gateway, JWT y negociación WebSocket |

La validación local no reemplaza verificar PVC, CNI y conexión SQL desde un
clúster activo. El estado efectivo de esta entrega consta en
[la validación final](phase-2-validation.md). No se configura VPS ni TLS de producción.
