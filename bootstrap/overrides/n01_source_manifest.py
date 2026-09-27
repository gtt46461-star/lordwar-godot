#!/usr/bin/env python3
"""Deterministic identity for a complete SOURCE tree, excluding generated outputs."""
import hashlib
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
ignore = {'.git', '.godot', 'Builds', 'Evidence', '__pycache__'}
files = sorted(p for p in root.rglob('*') if p.is_file()
               and not any(part in ignore for part in p.relative_to(root).parts)
               and p.name != 'build_identity.json')
source = hashlib.sha256()
content = hashlib.sha256()
for file in files:
    rel = file.relative_to(root).as_posix()
    digest = hashlib.sha256(file.read_bytes()).hexdigest()
    line = f'{rel}\0{digest}\n'.encode('utf-8')
    source.update(line)
    if rel.startswith('Art/LordWarArt/'):
        content.update(line)
text = (root / 'Scripts/LordWar/Core/BuildInfo.cs').read_text()
def const(pattern):
    hit = re.search(pattern, text)
    if not hit:
        raise SystemExit(f'BuildInfo lacks {pattern}')
    return hit.group(1)
identity = {
    'batch': 'N01',
    'buildId': const(r'BuildId="([^"]+)"'),
    'versionName': const(r'VersionName="([^"]+)"'),
    'versionCode': int(const(r'VersionCode=(\d+)')),
    'packageName': const(r'PackageName="([^"]+)"'),
    'sourceTreeHash': source.hexdigest(),
    'contentHash': content.hexdigest(),
    'fileCount': len(files),
    'artCount': len(list((root / 'Art/LordWarArt').rglob('*.png'))),
}
path = root / 'build_identity.json'
if path.exists():
    old = json.loads(path.read_text())
    if old != identity:
        raise SystemExit(f'source identity mismatch: {old} != {identity}')
else:
    path.write_text(json.dumps(identity, indent=2, ensure_ascii=False) + '\n')
print(json.dumps(identity, ensure_ascii=False))
