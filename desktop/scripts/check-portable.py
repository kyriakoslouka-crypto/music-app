#!/usr/bin/env python3
"""Validate the real archive before delivery; never permit private piano material."""
import argparse,hashlib,json,zipfile
from pathlib import Path,PurePosixPath
parser=argparse.ArgumentParser()
parser.add_argument('archive',type=Path)
parser.add_argument('--report',type=Path,required=True)
args=parser.parse_args()
result={'Passed':False,'Archive':args.archive.name}
try:
    with zipfile.ZipFile(args.archive)as archive:
        names=archive.namelist()
        for name in names:
            path=PurePosixPath(name.replace('\\','/'))
            if path.is_absolute()or'..'in path.parts or':'in name:raise ValueError('Unsafe ZIP entry: '+name)
            if path.suffix.lower()in{'.wav','.flac','.sf2','.sfz','.mp3','.ogg','.m4a','.aac'}or name.endswith('.studio-piano.json'):raise ValueError('Private piano material in package: '+name)
        if 'ProfessionalEarTrainingStudio.exe'not in names:raise ValueError('Portable root EXE missing')
        bad=archive.testzip()
        if bad:raise ValueError('CRC failure: '+bad)
        result.update(Passed=True,Entries=len(names),PrivatePianoSamplesBundled=False,PrivateIsokratisSamplesBundled=False)
    with args.archive.open('rb')as stream:result['SHA256']=hashlib.file_digest(stream,'sha256').hexdigest()
except Exception as error:
    result['Error']=str(error)
finally:
    args.report.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
if not result['Passed']:raise SystemExit(result['Error'])
print('Portable ZIP CRC, paths and private-sample exclusion passed:',result['Entries'],'entries')
