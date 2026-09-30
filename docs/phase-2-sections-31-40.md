# Fase 2 — secciones 31–40

## Implementación

| Sección | Resultado |
|---|---|
| 31 | Diálogos con Tab/Shift+Tab, Escape, foco inicial/restaurado, fondo inerte y bloqueo durante envío; errores accesibles, skeletons y feedback en reglas. |
| 32 | OpenAPI regenerado desde las seis APIs y combinado para Gateway. Contratos Angular y configuración runtime verificados. |
| 33 | Ocho imágenes multi-stage construidas, bases fijadas por digest, lockfiles NuGet/npm, usuarios sin privilegios y exclusión de secretos. |
| 34 | Compose conserva el desarrollo local; Kubernetes tiene manifiestos independientes. |
| 35 | SQL permanece en Docker. Conexiones externas configurables mediante Secret; ningún workload SQL en Kubernetes. |
| 36 | `k8s/` con aplicaciones, RabbitMQ persistente, configuración y ejemplo de secretos. |
| 37 | Namespace `climate-monitoring` en todos los recursos correspondientes. |
| 38 | Ocho Deployments con réplicas, puertos, configuración, probes, recursos y contexto de seguridad. |
| 39 | Services ClusterIP internos con selectores verificados. |
| 40 | Gateway conserva la entrada backend y utiliza DNS de Services Kubernetes. |

El frontend utiliza un servidor estático Node.js y configuración runtime para
conectar el navegador al Gateway. La URL pública debe coincidir con la exposición
elegida y el origen frontend debe estar permitido por CORS.

## Verificación realizada

- Build .NET: cero errores y advertencias.
- OpenAPI exportado desde seis servicios; seis comprobaciones EF sin cambios de
  modelo pendientes respecto de las migraciones.
- Angular: lint correcto y **56 pruebas aprobadas** en 13 archivos.
- Build de las **ocho imágenes** `climate/*:phase2` completado.
- `scripts/Test-Images.ps1`: SQL y RabbitMQ reales, ocho imágenes saludables,
  usuarios no root, archivos de aplicación de solo lectura, login JWT por
  Gateway, descarga JSON de OpenAPI, runtime config y rutas Angular directas.
  Los contenedores y volúmenes temporales se eliminaron al finalizar.
- `kubectl kustomize`: **23 recursos**, más ejemplo Secret validado por separado
  contra el OpenAPI oficial de Kubernetes 1.34.1. También se comprobaron DNS,
  selectores, referencias de configuración/secretos, probes y ausencia de SQL.
- `git diff --check`: sin errores en ambos repositorios.

## Límites comprobados

Minikube estaba detenido; no se aplicaron recursos. Los manifiestos incluyen
dominios de ejemplo que deben sustituirse, secretos locales y requisitos de
almacenamiento/red detallados en [la guía Kubernetes](../k8s/README.md).
La planificación, persistencia y conectividad SQL desde Kubernetes quedan por
verificar en el clúster activo.

Las pruebas de foco y teclado se ejecutaron con Vitest/JSDOM. No se ejecutó una
auditoría visual automatizada en navegador: la instalación de Playwright/axe
falló por `UNABLE_TO_VERIFY_LEAF_SIGNATURE` en npm. No se desactivó TLS.

## Repetir las comprobaciones

Desde `backend`, con Docker disponible:

```powershell
./scripts/Build-Images.ps1 -IncludeFrontend
./scripts/Test-Images.ps1
python -m pip install -r scripts/requirements-manifests.txt
python scripts/validate-manifests.py
```

El script de imágenes usa puertos locales 14349, 18090 y 14200 y un proyecto
Compose temporal con credenciales aleatorias. Requiere que esos puertos estén
libres. El parámetro opcional `-BrowserScript` permite incorporar una prueba de
navegador cuando sus herramientas estén disponibles.
