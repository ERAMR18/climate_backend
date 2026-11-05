# PDF de la documentación completa de Fase 4

Archivos en la raíz del workspace:

- `Documentacion_Climate_Monitoring_Fase_4.pdf`: edición actual de Fase4, OAuth/OIDC, VPS, Arduino simulado/DHT11, puente, seguridad, pruebas y anexos de operación.
- `Documentacion_Climate_Monitoring_System.pdf`: la misma edición actual al inicio y las ocho páginas previas de Fase3 conservadas como anexo histórico al final.
- `docs/archive/Documentacion_Climate_Monitoring_System_antes_Fase_4.pdf`: copia del PDF original antes de actualizarlo. No se reemplaza en regeneraciones posteriores.
- `docs/FASE_4_DOCUMENTACION_COMPLETA.md`: fuente editable del cuerpo principal; las dos guías, matriz y firmwares se incorporan desde sus archivos existentes.

## Regenerar desde PowerShell, en la raíz

Dependencias preparadas en tools/pdf-libs. Si faltan en otra PC:
```powershell
python -m pip install --target tools/pdf-libs -r tools/requirements-phase4-pdf.txt
```

Después:
```powershell
python tools/phase4-doc-matrix.py
python tools/generate-phase4-pdf.py
python tools/validate-phase4-pdf.py
```
Si un comando falla, detenerse y corregirlo antes del siguiente. El generador usa las fuentes Segoe UI y Consolas de Windows. No ejecuta el puente, no carga el Arduino ni modifica el VPS.

El PDF general se reconstruye usando la copia archivada, por lo que no duplica la Fase4 cada vez que se regenera. Los valores privados se usan únicamente para comprobar que no aparecen en el texto de salida; no se imprimen ni se incluyen en el documento. No adjuntar .env ni device-token.local como anexos.

## Verificación

Resultados: `docs/validation/phase4-pdf.json` y `phase4-pdf-layout.json`. Comprobaciones: contenido requerido, navegación/índice, preservación del texto previo, ausencia de credenciales conocidas, páginas no vacías y texto dentro del tamaño de página. Muestras visuales en docs/validation/pdf-preview.

Los resultados de pruebas del sistema en el PDF son las últimas ejecuciones registradas de la fase; generar el PDF no repite automáticamente las pruebas del software. GitHub real y HTTP200 del VPS están atribuidos a las salidas del usuario. La compilación DHT11 no se presenta como medición física/calibración comprobada.

Para retomar el trabajo consultar `Get-Content .\FASE_4_PROGRESO.md`; si solo cambia documentación, no recompilar ni desplegar backend/frontend.
