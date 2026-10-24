#!/usr/bin/env python3
"""Package an already cross-published win-x64 tree; never includes private samples."""
import argparse
import hashlib
import xml.etree.ElementTree as ET
from pathlib import Path
import subprocess
import zipfile

parser = argparse.ArgumentParser()
parser.add_argument('--nsis', required=True, help='Path to NSIS 3 makensis (set NSISDIR if extracted locally)')
args = parser.parse_args()
base = Path(__file__).resolve().parent.parent
version=ET.parse(base/'Directory.Build.props').findtext('./PropertyGroup/Version')
artifacts = base / 'artifacts'
publish = artifacts / 'win-x64'
if not (publish / 'ProfessionalEarTrainingStudio.exe').is_file():
    raise SystemExit('Cross-publish the Windows project first.')
files = sorted(p for p in publish.rglob('*') if p.is_file())
for path in files:
    if path.suffix.lower() in {'.wav', '.flac', '.sf2', '.sfz', '.zip'} or path.name.endswith('.studio-piano.json'):
        raise SystemExit(f'Private piano material must not be redistributed: {path}')

def nsis_path(path):
    return str(path.relative_to(publish)).replace('/', '\\').replace('$', '$$').replace('"', '$\\"')

lines = [f'Delete "$INSTDIR\\{nsis_path(p)}"' for p in files]
for directory in sorted((p for p in publish.rglob('*') if p.is_dir()), key=lambda p: len(p.parts), reverse=True):
    lines.append(f'RMDir "$INSTDIR\\{nsis_path(directory)}"')
manifest = artifacts / 'uninstall-files.nsh'
manifest.write_text('\n'.join(lines) + '\n', encoding='utf-8')
setup = artifacts / f'ProfessionalEarTrainingStudio-{version}-Setup.exe'
subprocess.run([args.nsis, '-V2', f'-DAPP_VERSION={version}', f'-DPUBLISH_DIR={publish}', f'-DOUTPUT_FILE={setup}',
                f'-DUNINSTALL_INCLUDE={manifest}', str(base / 'installer/Studio.nsi')], check=True)
portable = artifacts / f'ProfessionalEarTrainingStudio-{version}-win-x64.zip'
with zipfile.ZipFile(portable, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for path in files:
        archive.write(path, Path(f'ProfessionalEarTrainingStudio-{version}') / path.relative_to(publish))
with zipfile.ZipFile(portable) as archive:
    if archive.testzip() is not None:
        raise SystemExit('Portable ZIP CRC failure')
    print(f'Portable ZIP CRC passed: {len(archive.namelist())} files.')
outputs = [setup, portable, publish / 'ProfessionalEarTrainingStudio.exe',
           artifacts / 'standalone-win-x64/ProfessionalEarTrainingStudio.exe']
checksums = []
for path in outputs:
    with path.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    checksums.append(f'{digest}  {path.relative_to(artifacts)}')
    print(path.name, path.stat().st_size, 'bytes', digest)
(artifacts / 'SHA256SUMS.txt').write_text('\n'.join(checksums) + '\n', encoding='ascii')
