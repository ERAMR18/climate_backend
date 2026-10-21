"""Create a traceable requirement inventory without claiming unverified implementation."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
prompt = root.parent / 'phase_3.md'
output = root / 'docs' / 'FASE_3_MATRIZ_REQUISITOS.md'
lines = prompt.read_text(encoding='utf-8-sig').splitlines()
rows = []
section = 0
sequence = 0
current_rf = None
for number, raw in enumerate(lines, 1):
    text = raw.strip()
    heading = re.match(r'### (\d+)\.', text)
    if heading:
        section = int(heading[1]); sequence = 0; current_rf = None
    requirement = re.match(r'\*\*((?:RF|RNF)-REP-\d+)', text)
    if requirement:
        current_rf = requirement[1]
    if not section or not text or text.startswith(('#', '```', '|', 'https://')):
        continue
    if text.startswith('`') and text.endswith('`'):
        continue
    sequence += 1
    identifier = current_rf if requirement else f'P3-S{section:02d}-{sequence:03d}'
    initial = 'PENDIENTE'
    implementation = 'Pendiente; conservar alcance del prompt.'
    evidence = f'phase_3.md:{number}'
    test = 'No ejecutada para este requisito'
    final = 'PENDIENTE'
    if section in {3, 4, 5, 7, 8, 23, 24, 25, 27, 28}:
        initial = 'PARCIAL' if section in {3, 4, 7, 23, 27} else 'PENDIENTE'
        final = 'PARCIAL'
        implementation = 'Ver diagnóstico y progreso por etapa; este requisito requiere cierre específico.'
        evidence += '; FASE_3_ARQUITECTURA.md; FASE_3_PRUEBAS.md'
    if section == 8:
        implementation = 'Auth NestJS/PostgreSQL funcional; migración y Outbox probados en entorno aislado. Rollout real pendiente.'
        evidence += '; src/Services/AuthService/'
        test = 'npm test; scripts/Test-Phase3Auth.ps1'
    if section == 7:
        implementation = 'Gateway compila con JWT, CorrelationId y rutas protegidas. Nuevos destinos se incorporan por etapa.'
        evidence += '; src/Gateway/Climate.Gateway/'
        test = 'dotnet build Gateway; validación HTTP pendiente'
    if section == 6 and text.startswith('**Auth:'):
        final = 'PARCIAL'
        implementation = 'Script de migración implementado y probado dos veces con SQL Server y PostgreSQL aislados; datos reales sin tocar.'
        evidence += '; src/Services/AuthService/src/migrate.ts'
        test = 'scripts/Test-Phase3Auth.ps1'
    safe = lambda value: value.replace('|', '\\|').replace('\n', ' ')
    rows.append('| ' + ' | '.join(map(safe, [identifier, text, initial, implementation, evidence, test, final])) + ' |')

header = '''# Fase 3 — Matriz de requisitos

Estado: EN CURSO. Ningún requisito se certifica por la sola existencia de un archivo.
Cada fila conserva una instrucción, párrafo o elemento del prompt local con su línea de origen.
Los subrequisitos de un RF/RNF mantienen el alcance bajo la sección correspondiente.
Los criterios finales de la sección 26 siguen pendientes hasta validar la arquitectura completa.

Las fases 1/2 tienen evidencia previa en `phase-2-compliance.md`, `phase-2-validation.md`
y `validation/phase3-inventory.json`; su regresión tras todas las migraciones sigue pendiente.
La fuente oficial Fase 1 indicada devolvió HTTP 404; no se inventó su contenido.

Este archivo se generó inicialmente con `scripts/phase3_matrix.py`. A partir de aquí se actualiza
manualmente conforme se implementa y valida cada fila. NO regenerarlo para continuar porque
el generador inicial no contiene los estados futuros.

| ID | Requisito | Estado inicial | Implementación | Evidencia | Prueba | Estado final |
| --- | --- | --- | --- | --- | --- | --- |
'''
if output.exists():
    raise SystemExit('La matriz ya existe; actualizar sus estados sin regenerar ni perder evidencia.')
output.write_text(header + '\n'.join(rows) + '\n', encoding='utf-8')
print(f'{len(rows)} requisitos/subrequisitos inventariados en {output}')
