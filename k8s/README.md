# Kubernetes: secciones 31–50

La [guía final](../docs/kubernetes.md) amplía selección de registro, comandos,
logs y diagnóstico. La [validación final](../docs/phase-2-validation.md) distingue
las pruebas ejecutadas de la comprobación pendiente en un clúster real.

La base contiene 24 recursos en `climate-monitoring`: ocho Deployments, sus
Services ClusterIP, ConfigMaps y RabbitMQ con StatefulSet, Service headless y
persistencia. `volumeClaimTemplates` crea el PVC de RabbitMQ; requiere una
StorageClass predeterminada que aprovisione 2 GiB ReadWriteOnce.
SQL Server permanece en Docker, fuera del clúster.

## Configuración y secretos

`configmap.yaml` contiene issuer/audience JWT, DNS y puerto RabbitMQ, URLs de
servicios y opciones de simulación. Gateway y frontend tienen ConfigMaps propios.
Las claves JWT/API, conexiones SQL y credenciales RabbitMQ se obtienen mediante
`secretKeyRef`. El ejemplo de Secret contiene únicamente placeholders.
Los cambios en variables de entorno requieren reiniciar los Deployments afectados.
No subir `secrets.yaml` ni salidas de `kubectl get secret` al repositorio.

## Preparación

Desde `backend`:

```powershell
./scripts/Build-Images.ps1 -IncludeFrontend
kubectl kustomize k8s
python -m pip install -r scripts/requirements-manifests.txt
python scripts/validate-manifests.py
```

Las imágenes `climate/{gateway,identity,sensors,monitoring,alerts,events,audit,frontend}:phase2`
deben cargarse en todos los nodos del clúster local (por ejemplo, `minikube image load`)
o publicarse en un registro y actualizar `image` en los Deployments. Para releases,
usar etiquetas únicas o digests. Las bases están fijadas por versión y digest;
NuGet usa lockfiles y npm utiliza `npm ci`.

## SQL Server externo y configuración

1. Mantener SQL en Docker: `docker compose --env-file .env up -d sqlserver`.
2. Configurar `SQLSERVER_BIND_ADDRESS` con la interfaz del host alcanzable desde
   los nodos y `SQLSERVER_PORT`. El valor local predeterminado es `127.0.0.1` y no
   permite acceso desde un clúster remoto. Permitir el puerto solo desde la red
   del clúster mediante el firewall del host.
3. Copiar `secrets.example.yaml` a `secrets.yaml` (ignorado por Git), sustituir
   todos los placeholders y las seis conexiones por el DNS/puerto de ese host.
   Usar credenciales con permisos para las migraciones iniciales. Ajustar
   `TrustServerCertificate` según el certificado instalado en SQL.
4. En `frontend/configmap.yaml`, poner el origen público del Gateway en
   `GATEWAY_PUBLIC_URL`. Debe ser accesible desde el navegador.
5. En `gateway/configmap.yaml`, poner el origen exacto del frontend en
   `Cors__AllowedOrigins__0`. REST y SignalR usan el Gateway; el servidor de
   archivos frontend no actúa como proxy.

Los dominios `example.invalid` son placeholders obligatorios. No aplicar la
plantilla de secretos sin completarla. Kustomize la excluye deliberadamente.
Las URLs entre servicios usan exclusivamente los nombres de los Services.

## Aplicación cuando el clúster esté disponible

```powershell
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -k k8s
kubectl -n climate-monitoring get pods,services,pvc
```

Para una comprobación local, configurar previamente los orígenes como
`http://localhost:4200` y `http://localhost:8080`, y ejecutar en dos terminales:

```powershell
kubectl -n climate-monitoring port-forward service/frontend 4200:8080
kubectl -n climate-monitoring port-forward service/gateway 8080:8080
```

No hay Services públicos de microservicios ni manifiestos SQL. La exposición
definitiva mediante dominio/TLS queda para las secciones posteriores.

## Operación y límites

- `/health/live` comprueba el proceso; `/health` incluye dependencias y determina
  readiness. La sonda de arranque permite hasta cinco minutos de inicialización.
- Los ocho contenedores de aplicación usan usuario sin privilegios, sistema de
  archivos de solo lectura y `/tmp` efímero; tienen requests y límites de recursos.
- Monitoring usa una réplica y estrategia Recreate: el simulador y SignalR aún
  no tienen coordinación distribuida ni backplane para múltiples réplicas.
- RabbitMQ tiene una réplica persistente. No escalar ese StatefulSet sin diseñar
  primero el clustering y las colas para alta disponibilidad.
- El PVC se conserva al eliminar o reducir el StatefulSet (`Retain`). El directorio
  `/var/lib/rabbitmq` contiene los datos. Conservar también el nombre del nodo,
  las credenciales y la cookie Erlang al reemplazar el Pod.
- La NetworkPolicy permite AMQP 5672 solo desde las seis APIs del mismo namespace.
  Requiere un plugin de red que aplique NetworkPolicy. La interfaz Management
  no está publicada y su acceso de red queda bloqueado por esa política. Para
  administración puntual se puede usar un port-forward autorizado y credenciales.
- La validación de esquema es local y no demuestra conectividad SQL, permisos
  del volumen ni planificación en un clúster real. Verificar esos puntos al aplicar.

Referencias: [Deployments](https://kubernetes.io/docs/concepts/workloads/controllers/deployment/),
[sondas](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
y [construcción Docker](https://docs.docker.com/build/building/best-practices/).

La retención se basa en [StatefulSet y PVC](https://kubernetes.io/docs/concepts/workloads/controllers/statefulset/)
y el aislamiento en [NetworkPolicy](https://kubernetes.io/docs/concepts/services-networking/network-policies/).

## Dependencias y recuperación

Kubernetes inicia las aplicaciones sin un orden equivalente a `depends_on`.
Las seis APIs reintentan la inicialización SQL hasta 12 veces, con cinco segundos
entre fallos de base de datos; cada intento crea su propio scope. Si se agotan,
el proceso falla y Kubernetes puede reiniciarlo. Errores de configuración fallan
directamente. La sonda de arranque contempla el tiempo de las conexiones SQL.

Outbox conserva eventos no confirmados en SQL y reintenta cada cinco segundos.
RabbitMQ utiliza exchanges/colas durables y mensajes persistentes con publisher
confirms. Audit vuelve a conectar cada cinco segundos y confirma mensajes solo
después de persistirlos. Si falla la escritura, usa contextos nuevos en tres
intentos; tras agotarlos envía el mensaje a la DLQ durable
`climate.audit.events.dlq`, que necesita revisión y reenvío operativo.

La indisponibilidad de RabbitMQ no impide servir operaciones de negocio con
outbox. `/health` comprueba SQL y `/health/live` solo el proceso; que Audit esté
ready no garantiza que esté consumiendo en ese instante. Revisar también logs,
mensajes pendientes y DLQ. Monitoring conserva su ciclo tras un error de un
servicio; no se reintentan automáticamente todas las escrituras HTTP, para evitar
repetir efectos cuando se pierde una respuesta.

## Escalado de Audit

```powershell
kubectl -n climate-monitoring scale deployment/audit --replicas=2
kubectl -n climate-monitoring rollout status deployment/audit
```

También puede editarse `spec.replicas`. Los consumidores compiten por la misma
cola; `EventId` es la clave primaria de `AuditLogs`. Si dos entregas simultáneas
compiten por insertarlo, un intento posterior consulta el registro existente
con un contexto nuevo. La entrega es al menos una vez y AuditDb conserva una fila
por evento. Aumentar réplicas no garantiza orden global de procesamiento.
Las réplicas adicionales incrementan conexiones SQL/RabbitMQ: dimensionar recursos.
Monitoring y RabbitMQ conservan las restricciones indicadas arriba. No hay HPA.

## Prueba reproducible sin clúster

```powershell
./scripts/Build-Images.ps1 -IncludeFrontend
./scripts/Test-Images.ps1 -TestRecovery
```

La prueba usa un proyecto Docker temporal: detiene Audit, produce eventos,
recrea RabbitMQ conservando su volumen, inicia dos instancias Audit, verifica SQL,
republica EventIds y prueba caídas del broker/SQL y las sondas. Dockerfile construye
las imágenes; los Deployments solo ejecutan esas imágenes, sin compilación ni
initContainers para ordenar dependencias. Esta prueba no sustituye verificar el
PVC, CNI y conectividad del SQL externo en un clúster real.
