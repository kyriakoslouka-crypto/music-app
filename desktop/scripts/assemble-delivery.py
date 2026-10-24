#!/usr/bin/env python3
"""Assemble versioned, source-complete local deliverables without claiming public availability."""
from pathlib import Path
import hashlib,json,shutil,zipfile,subprocess,sys,xml.etree.ElementTree as ET
base=Path(__file__).resolve().parent.parent
version=ET.parse(base/'Directory.Build.props').findtext('./PropertyGroup/Version')
folder=base/'deliverables'/'releases'/version
folder.mkdir(parents=True,exist_ok=True)
items={
 f'ProfessionalEarTrainingStudio-{version}-Setup.exe':base/'artifacts'/f'ProfessionalEarTrainingStudio-{version}-Setup.exe',
 f'ProfessionalEarTrainingStudio-{version}-Portable.zip':base/'artifacts'/f'ProfessionalEarTrainingStudio-{version}-win-x64.zip',
 f'ProfessionalEarTrainingStudio-{version}-Standalone.exe':base/'artifacts/standalone-win-x64/ProfessionalEarTrainingStudio.exe'}
for name,source in items.items():shutil.copy2(source,folder/name)
source=folder/f'ProfessionalEarTrainingStudio-{version}-Source.zip'
with zipfile.ZipFile(source,'w',compression=zipfile.ZIP_DEFLATED)as archive:
 for f in sorted(base.rglob('*')):
  if f.is_file()and not any(p in ('artifacts','deliverables','bin','obj','__pycache__')for p in f.relative_to(base).parts):archive.write(f,Path('desktop')/f.relative_to(base))
 for f in sorted((base.parent/'.github').rglob('*'))if(base.parent/'.github').exists()else[]:
  if f.is_file():archive.write(f,Path('.github')/f.relative_to(base.parent/'.github'))
shutil.copytree(base/'docs',folder/'Οδηγίες',dirs_exist_ok=True)
shutil.copy2(base/'README.md',folder/'README.md')
report=base/'artifacts'/f'tests-{version}.json'
if report.exists():shutil.copy2(report,folder/'TEST-RESULTS.json')
for name in ['WINDOWS-CHECKS.json','accessibility-audit.json','typed-uia-audit.json','windows-package-smoke.json','portable-integrity.json','windows-piano-check.json','windows-maestro-tests.json']:
 if(base/'artifacts'/name).exists():shutil.copy2(base/'artifacts'/name,folder/name)
subprocess.run([sys.executable,str(base/'scripts/release-readiness.py'),'--output',str(folder/'READINESS.json')],check=True)
lines=[]
for f in sorted(folder.rglob('*')):
 if f.is_file()and f.name!='SHA256SUMS.txt':
  with f.open('rb')as stream:digest=hashlib.file_digest(stream,'sha256').hexdigest()
  lines.append(digest+'  '+str(f.relative_to(folder)))
(folder/'SHA256SUMS.txt').write_text('\n'.join(lines)+'\n',encoding='utf-8')
(base/'deliverables'/'LATEST.json').write_text(json.dumps({'Version':version,'Folder':str(folder.relative_to(base)),'PublicUrl':None,'DownloadFromUserDeviceVerified':False},indent=2),encoding='utf-8')
print('Complete local delivery folder:',folder)
print('Assembly alone does not verify external delivery or user-side download; see downloads/0.5.0/DELIVERY-VERIFICATION.json for separate evidence.')
