#!/usr/bin/env python3
"""Record evidence, never infer NVDA/hardware success from a cross-build."""
import argparse,json
from pathlib import Path
import xml.etree.ElementTree as ET
parser=argparse.ArgumentParser()
parser.add_argument('--output',required=True)
parser.add_argument('--evidence')
parser.add_argument('--require-final',action='store_true')
args=parser.parse_args()
base=Path(__file__).resolve().parent.parent
version=ET.parse(base/'Directory.Build.props').findtext('./PropertyGroup/Version')
def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))if path.exists()else{}
core=read(base/'artifacts'/f'tests-{version}.json')
windows=read(base/'artifacts'/'WINDOWS-CHECKS.json')
manual=read(Path(args.evidence)if args.evidence else base/'artifacts'/'MANUAL-ACCEPTANCE.json')
checks={
    'CoreTestsPassed':core.get('Version')==version and core.get('Executed',0)>0 and core.get('Failed')==0,
    'WindowsProgrammaticUIAPassed':windows.get('ProgrammaticUIASucceeded')is True,
    'NvdaActuallyTestedAndPassed':manual.get('NvdaPassed')is True,
    'PhysicalAudioHardwarePassed':manual.get('AudioHardwarePassed')is True,
    'ProfessionalPianoListeningPassed':manual.get('ProfessionalListeningPassed')is True,
    'DownloadOnUserComputerPassed':manual.get('UserDownloadPassed')is True,
    'RemainingCurriculumScopeResolvedOrAccepted':manual.get('CurriculumScopeAccepted')is True,
}
report={'Version':version,'TargetFinalVersion':'1.0.0','ReadyForProfessionalFinal':all(checks.values()),'Checks':checks,'Missing':[k for k,v in checks.items()if not v]}
Path(args.output).write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Professional final readiness:',report['ReadyForProfessionalFinal'])
print('Outstanding:',', '.join(report['Missing']))
if(args.require_final or version=='1.0.0')and not report['ReadyForProfessionalFinal']:raise SystemExit(1)
