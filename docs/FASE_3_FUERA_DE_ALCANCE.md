# Fase 3 — Fuera del alcance acordado

Este archivo aplica las últimas instrucciones del usuario sobre el prompt original. Estas exclusiones no se presentan como funcionalidades implementadas ni como pendientes que bloqueen el funcionamiento local.

| Elemento | Decisión y motivo |
| --- | --- |
| Desplegar en VPS, modificar servidores remotos, DNS, certificados y comprobar HTTPS público | Excluido expresamente por el usuario. Se conservan las plantillas existentes como referencia; no se ejecutan despliegues remotos. |
| Conservar o transferir datos históricos del usuario | No es requisito para esta entrega: se permite una instalación nueva. No se borraron bases ni volúmenes existentes. Los migradores ya implementados y probados permanecen disponibles, pero no se exige ejecutarlos sobre datos anteriores. |
| Grabar videos de demostración, narraciones, presentaciones o evidencias audiovisuales | No se producen ni publican en este trabajo. No aparecen como requisito funcional en phase_3.md; se registran aquí por la aclaración del usuario. Se entregan pruebas ejecutables y evidencia técnica en Markdown/JSON. |
| Publicar repositorios, crear entregas en una plataforma educativa o subir imágenes a un registro externo | No se solicitaron destinos ni credenciales para publicar. Los archivos quedan guardados localmente y las imágenes construidas en Docker local. |
| Lectura de la fuente externa de Fase 1 mediante el enlace original | El enlace consultado devuelve HTTP404. No se inventa su contenido. Esto limita la certificación documental contra ese enunciado externo, no impide ejecutar los módulos existentes ni sus regresiones. |
| Despliegue Kubernetes/K3s y proveedores OAuth/OIDC externos | Corresponden a Fase4 según el propio prompt. En Fase3 se entrega preparación: manifiestos, healthchecks, configuración, almacenamiento y contrato de identidad externa. |
| Certificar rendimiento para cualquier hardware o cualquier cantidad de datos | Se documentan las mediciones locales y los límites configurables. El procesamiento por lotes, los trabajos asíncronos y el rechazo explícito al exceder límites sí forman parte de la funcionalidad y se prueban; no se promete capacidad ilimitada. |

Sí permanecen dentro del alcance y no se excluyen por conveniencia: funcionamiento local completo, los ocho tipos de PDF, historial/descarga, permisos/JWT, integración de servicios, dashboard, RabbitMQ/Outbox, pruebas de volumen, respaldo/restauración local, configuración Docker y documentación verificable.
