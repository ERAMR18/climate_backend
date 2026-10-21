"""Offline volume snapshots for the isolated Phase 3 recovery rehearsal only."""
import hashlib
import json
from pathlib import Path
import sys
import tarfile

mode=sys.argv[1]
names=sys.argv[2:]
allowed={'sqlserver-data','rabbitmq-data','auth-postgres-data','community-postgres-data','alert-postgres-data','audit-mongo-data','report-mongo-data','report-files'}
if mode not in {'backup','restore'} or set(names)!=allowed:
    raise SystemExit('Expected the eight isolated Phase 3 volumes')
backup=Path('/backup')
manifest={}
if mode=='restore':
    manifest=json.loads((backup/'manifest.json').read_text())
for name in names:
    root=Path('/volumes')/name
    archive=backup/(name+'.tar.gz')
    if mode=='backup':
        with tarfile.open(archive,'w:gz',compresslevel=1) as output:
            output.add(root,arcname='.')
        with archive.open('rb') as stream:
            digest=hashlib.file_digest(stream,'sha256').hexdigest()
        manifest[name]={'sha256':digest,'bytes':archive.stat().st_size}
    else:
        with archive.open('rb') as stream:
            if hashlib.file_digest(stream,'sha256').hexdigest()!=manifest[name]['sha256']:
                raise SystemExit('Snapshot digest mismatch: '+name)
        with tarfile.open(archive,'r:gz') as source:
            # These archives were generated above from stopped disposable Docker volumes.
            # Restore their original Linux ownership so database processes can read them.
            for member in source:
                target=(root/member.name).resolve()
                if not target.is_relative_to(root.resolve()):
                    raise SystemExit('Unexpected archive path')
            source.extractall(root,filter='fully_trusted')
    print(mode+': '+name,flush=True)
if mode=='backup':
    (backup/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
