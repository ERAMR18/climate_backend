# Fase 2 — secciones 41–50

## Cambios y alcance

| Sección | Implementación |
|---|---|
| 41 | ConfigMaps compartido, Gateway y frontend; configuración no sensible separada. |
| 42 | Secret de ejemplo con placeholders, referencias por clave y archivos locales excluidos de Git. |
| 43 | RabbitMQ StatefulSet, Services internos y NetworkPolicy que permite AMQP únicamente desde las APIs del namespace. |
| 44 | PVC mediante `volumeClaimTemplates`, retención explícita al eliminar/escalar, colas durables y mensajes persistentes con confirms. |
| 45 | Seis conexiones SQL en Secret; DNS del host Docker configurable y guía de red/firewall. |
| 46 | Readiness con SQL, liveness independiente y startupProbe en cada aplicación. |
| 47 | Inicialización SQL con reintentos acotados en las seis APIs; outbox y consumidor reconectan sin orden de arranque impuesto por Kubernetes. |
| 48 | Consumidor dentro de Audit; prueba de recuperación tras detenerlo y reemplazar el broker conservando su volumen. |
| 49 | Réplicas configurables; prueba con dos consumidores y replay de EventIds. Restricciones de Monitoring/RabbitMQ documentadas, sin HPA. |
| 50 | Dockerfiles construyen; Deployments consumen imágenes. Ningún manifiesto compila código. |

## Resiliencia de SQL

`DatabaseStartup.RunAsync` ejecuta la inicialización con un scope nuevo por
intento. Reintenta `DbException` hasta 12 veces, con cinco segundos entre intentos;
respeta cancelación y propaga el último error. No reintenta errores de
configuración ni reproduce solicitudes HTTP de escritura. Las cuatro pruebas
nuevas cubren recuperación, agotamiento, errores no recuperables y cancelación.

## Validación

- Build .NET correcto, sin errores ni advertencias en la compilación local.
- **123 pruebas backend aprobadas**, ninguna omitida, con RabbitMQ real temporal.
- **24 recursos Kubernetes y Secret de ejemplo** validados contra el esquema
  oficial 1.34.1 y las restricciones del proyecto.
- La prueba ampliada se reproduce con `scripts/Test-Images.ps1 -TestRecovery`.
  Utiliza contenedores y volúmenes temporales propios y SQL Server real.
- **Prueba de recuperación aprobada:** 12 mensajes pendientes sobrevivieron a
  reemplazar el contenedor RabbitMQ; dos consumidores dejaron 13 eventos únicos
  en AuditDb. El replay no agregó duplicados ni envió mensajes válidos a DLQ.
  También pasaron la caída/reconexión del broker, outbox durante esa caída,
  arranque con SQL tardío y separación de readiness/liveness.
- Siete imágenes backend reconstruidas; las ocho aplicaciones verificadas
  saludables, sin root y con archivos de aplicación de solo lectura. Login,
  OpenAPI y configuración runtime frontend correctos. Todos los recursos Docker
  temporales de la prueba se eliminaron al finalizar.
- `git diff --check` correcto.

## Límites

El contexto Minikube no responde y su ejecutable no está en PATH. No se aplicaron
recursos Kubernetes. La retención de PVC, aplicación efectiva de NetworkPolicy
por el CNI y conectividad del SQL externo deben verificarse en el clúster activo.
La prueba Docker acredita el comportamiento de las aplicaciones y del broker;
no acredita por sí sola el funcionamiento del almacenamiento Kubernetes.

La consulta del catálogo de vulnerabilidades NuGet falló durante la construcción
Docker (`NU1900`). Se mantiene como advertencia; los hallazgos de vulnerabilidades
siguen siendo errores. No se desactivaron TLS ni la auditoría de NuGet. Es necesario
repetir esa consulta cuando la conectividad esté disponible.

No se modificaron endpoints, DTOs ni frontend en estas secciones. La auditoría
visual pendiente de la entrega anterior permanece fuera de esta validación.

Consulta [la guía de configuración y operación](../k8s/README.md).
