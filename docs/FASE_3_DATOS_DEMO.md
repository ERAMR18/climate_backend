# Datos de demostración y simulación local

El arranque local carga tres comunidades de Guatemala, 15 sensores (cinco tipos por comunidad), 27 reglas de riesgo y una primera lectura por sensor que aún no tenga datos. Activa después el simulador existente de Monitoring, que genera lecturas periódicas y utiliza los servicios actuales de alertas, eventos y dashboard.

Desde `backend`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start-Phase3.ps1 -NoBuild
```

Para cargar los datos con los servicios ya iniciados:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Seed-Phase3Demo.ps1
```

La carga usa las APIs públicas y las credenciales privadas de `.env.phase3.local`. Reutiliza comunidades por nombre, sensores por código y reglas por nombre. No sobrescribe registros existentes ni reactiva sensores o comunidades desactivados. Si se interrumpe, se puede ejecutar otra vez: agrega únicamente entradas faltantes y lecturas iniciales para sensores sin datos. Las lecturas posteriores del simulador forman parte del historial normal.

El catálogo `seed/phase3-demo.json` recupera las definiciones originales de SensorService y AlertService. Se puede regenerar desde la raíz del proyecto con `backend/.venv-phase3/Scripts/python.exe tools/build-phase3-demo.py`.

Para omitir la carga automática, establecer `PHASE3_DEMO_ENABLED=false` en `.env.phase3.local`. Esto no elimina datos ni detiene un simulador que ya esté activo; se puede detener desde la interfaz. `DEMO_SEED_ENABLED` corresponde al inicializador antiguo, que no se utiliza con Community remoto.

Verificación funcional:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Phase3Demo.ps1
```

Comprueba que repetir el seed conserva los conteos, que crecen las lecturas y que el dashboard muestra sensores, lecturas y estadísticas de los cinco tipos. Evidencia: `docs/validation/phase3-demo.json`.
