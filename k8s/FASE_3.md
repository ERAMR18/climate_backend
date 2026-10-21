# Preparación Kubernetes de Fase 3

`kustomization.yaml` conserva los nombres DNS existentes y agrega Community y Report. Se actualizaron Auth NestJS, Alert Java y Audit Python. Diez Deployments, Services ClusterIP, ConfigMaps, RabbitMQ persistente y PVC privado de PDF. Las imágenes coinciden con `scripts/Build-Images.ps1 -Phase3 -IncludeFrontend`.

PostgreSQL, MongoDB y SQL Server se configuran como servicios externos privados mediante las conexiones de `secrets.example.yaml`. Provisionar bases separadas por propietario y permisos separados antes de migrar. Los valores son placeholders; el Secret no se incluye en Kustomize. No se ha aplicado este conjunto a un clúster real.

El PVC report-files pide 5Gi ReadWriteOnce y el Deployment reports usa Recreate y una réplica. Para escalar, usar almacenamiento compartido RWX u objetos e implementar el adaptador correspondiente antes de subir réplicas; Mongo coordina trabajos, pero no sustituye el acceso compartido al PDF. Montaje `/data/reports` con fsGroup 10001. La API conserva disco privado y no contiene rutas del cliente.

Monitoring conserva una réplica y simulación deshabilitada por defecto. Escalar SignalR requiere backplane/servicio SignalR y afinidad para conexiones; escalar simulación requiere elección de líder o particionado, antes de habilitarla en varios pods. Los demás servicios no dependen de sesiones en memoria para persistir sus datos; Auth conserva revocación en PostgreSQL y Outbox se procesa desde persistencia.

Healthchecks liveness/readiness, recursos, usuarios sin privilegios, raíz de solo lectura y volúmenes temporales forman parte de los manifiestos. Audit no bloquea los procesos de negocio. Validación ejecutada: 29 recursos más Secret de ejemplo contra esquema oficial Kubernetes 1.34.1, selectores/DNS, secretos y probes. Comando:

```powershell
python scripts/validate-manifests.py --schema ../tools/kubernetes-1.34.1-openapi.json
```

OAuth/OIDC queda preparado mediante contrato de proveedor externo en Auth y tabla external_identities; no se implementó login externo ni se sustituyó HS256 por OIDC. La transición requiere configurar proveedor, validar state/PKCE y migrar todos los validadores a issuer/audience/JWKS antes de cambiar el emisor. Corresponde a Fase 4.
