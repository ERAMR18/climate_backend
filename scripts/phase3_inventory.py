"""Read-only inventory of tracked application sources; never reads environment secrets."""
from pathlib import Path
import json
import re

root = Path(__file__).resolve().parents[1]
front = root.parent / 'frontend' / 'climate-monitoring-web'
excluded = {'node_modules', 'bin', 'obj', '.git', '.angular', 'dist', '.vs', 'target', '__pycache__'}

def sources(base):
    for path in base.rglob('*'):
        if excluded.intersection(path.parts) or not path.is_file():
            continue
        if path.suffix in {'.cs', '.csproj', '.ts', '.json', '.yml', '.yaml', '.html', '.java', '.py', '.sql', '.xml'} and not path.name.endswith('lock.json'):
            yield path

def inspect(base):
    result = []
    for path in sources(base):
        try:
            value = path.read_text(encoding='utf-8-sig')
        except (OSError, UnicodeError):
            continue
        result.append({'file': str(path.relative_to(base)).replace('\\', '/'),
                       'lines': len(value.splitlines()),
                       'contracts': [line.strip() for line in value.splitlines() if re.search(
                           r'\[Route|\[Http|DbSet<|HasIndex|HasOne|HasMany|path:|loadComponent:|PackageReference|TargetFramework|AddHttpClient|AddHostedService', line)]})
    return result

output = root / 'docs' / 'validation' / 'phase3-inventory.json'
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps({'backend': inspect(root / 'src'), 'tests': inspect(root / 'tests'),
                             'frontend': inspect(front / 'src')}, indent=2, ensure_ascii=False), encoding='utf-8')
print(output)
