#!/usr/bin/env python3
"""Reproduce metadata-only PCM fingerprints from the verified original archive.
Never bundles or writes any piano sample. Requires an independently installed ffmpeg.
"""
import argparse,hashlib,json,subprocess,zipfile
from pathlib import Path
COMMIT='717078b450eccc4a7733755f4b5973426ebc23e9'
ARCHIVE_SHA='2507c213538b850889bea4fbd90ca2a54abcc32d6a42febae53b81df1f677264'
p=argparse.ArgumentParser();p.add_argument('archive',type=Path);p.add_argument('--check',type=Path);p.add_argument('--output',type=Path);args=p.parse_args()
if not args.check and not args.output:p.error('Choose --check or --output')
with args.archive.open('rb')as stream:
    if hashlib.file_digest(stream,'sha256').hexdigest()!=ARCHIVE_SHA:raise SystemExit('Original archive SHA-256 mismatch')
names=[f'mcg_{layer}_{key:03}'for layer in ['p','mp','mf','f','ff']for key in range(21,109)]
names +=[f'mcr_{layer}_{key:03}'for layer in ['mp','mf','f','ff']for key in range(21,109)]
names +=['mcg_e_sp_mf','mcg_e_spr_mf']
reference=json.loads(args.check.read_text())if args.check else None
samples={}
with zipfile.ZipFile(args.archive)as archive:
    for number,name in enumerate(names,1):
        folder='SampleFix'if name=='mcg_ff_083'else'Samples'
        source=archive.read(f'MatsHelgesson.MaestroConcertGrandPiano-{COMMIT}/{folder}/{name}.flac')
        raw=subprocess.run(['ffmpeg','-v','error','-i','pipe:0','-f','s16le','-acodec','pcm_s16le','-ar','44100','-ac','2','pipe:1'],input=source,stdout=subprocess.PIPE,check=True).stdout
        packed=int.from_bytes(source[18:26],'big');frames=packed&0xfffffffff
        if len(raw)!=frames*4:raise SystemExit('Reference frame count mismatch: '+name)
        item={'Frames':frames,'Sha256':hashlib.sha256(raw).hexdigest()};samples[name+'.wav']=item
        if reference and reference['Samples'].get(name+'.wav')!=item:raise SystemExit('PCM reference mismatch: '+name)
        if number%100==0:print('Verified original-source PCM references:',number,'/794',flush=True)
result={'SourceCommit':COMMIT,'ArchiveSha256':ARCHIVE_SHA,'ReferenceDecoder':subprocess.check_output(['ffmpeg','-version'],text=True).splitlines()[0],'Format':'PCM16 little-endian, stereo 44100 Hz; hashes cover only WAV data chunks','Samples':dict(sorted(samples.items()))}
if args.output:args.output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('All 794 PCM fingerprints reproduced from the pinned archive; no samples redistributed.')
