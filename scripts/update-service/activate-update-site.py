"""Validate a staged static update site, then atomically activate its manifest.

Runs with Python 3 on the VPS; needs write access only to the product site root.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
from urllib.parse import urlparse


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
    return value.hexdigest()


def activate(root, stage, allow_rollback=False):
    root, stage = Path(root).resolve(), Path(stage).resolve()
    if stage.parent != root or not stage.name.startswith('.staging-'):
        raise ValueError('Staging must be a direct .staging-* child of the product root')
    manifest = json.loads((stage / 'stable.json').read_text(encoding='utf-8-sig'))
    version = manifest['version']
    if not re.fullmatch(r'\d+\.\d+\.\d+', version):
        raise ValueError('Invalid version')
    if (manifest['schemaVersion'], manifest['product'], manifest['channel']) != (
            1, 'FourJTooLs.StructuralToolbox', 'stable'):
        raise ValueError('Invalid manifest identity')
    name = 'FourJ_PV_Toolbox_Setup_v' + version + '.exe'
    relative = Path('releases') / version / name
    package = stage / relative
    expected = manifest['sha256'].lower()
    if package.stat().st_size != manifest['sizeBytes'] or digest(package) != expected:
        raise ValueError('Staged installer size/SHA-256 mismatch')
    for url in [manifest['url']] + manifest.get('mirrors', []):
        uri = urlparse(url)
        if uri.scheme != 'https' or not uri.netloc or uri.username or uri.fragment:
            raise ValueError('Invalid HTTPS download URL')
        if not uri.path.endswith('/' + name):
            raise ValueError('URL does not reference this installer')
    for path in ('index.html', 'assets/site.css'):
        if not (stage / path).is_file():
            raise ValueError('Missing site file: ' + path)
    lock = root / '.publish-lock'
    lock.mkdir()  # Fail closed if another deployment is active.
    try:
        current = root / 'stable.json'
        if current.exists():
            old = json.loads(current.read_text(encoding='utf-8-sig'))
            if tuple(map(int, old['version'].split('.'))) > tuple(map(int, version.split('.'))) and not allow_rollback:
                raise ValueError('Downgrade requires --allow-rollback')
        destination = root / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        if destination.exists():
            if digest(destination) != expected:
                raise ValueError('Refusing to replace an immutable version with different bytes')
        else:
            os.replace(str(package), str(destination))
        archived = destination.parent / 'stable.json'
        if archived.exists() and archived.read_bytes() != (stage / 'stable.json').read_bytes():
            raise ValueError('Refusing to change an existing version manifest')
        shutil.copyfile(str(stage / 'stable.json'), str(archived))
        (root / 'assets').mkdir(exist_ok=True)
        for path in ('assets/site.css', 'index.html'):
            os.replace(str(stage / path), str(root / path))
        if current.exists():
            shutil.copyfile(str(current), str(stage / 'previous.json'))
            os.replace(str(stage / 'previous.json'), str(root / 'previous-stable.json'))
        os.replace(str(stage / 'stable.json'), str(current))  # Publication point, always last.
    finally:
        lock.rmdir()
    return version


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root')
    parser.add_argument('stage')
    parser.add_argument('--allow-rollback', action='store_true')
    args = parser.parse_args()
    print('ACTIVATED=' + activate(args.root, args.stage, args.allow_rollback))
