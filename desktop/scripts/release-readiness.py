#!/usr/bin/env python3
"""Separate reproducible automated evidence from manual Windows 11 acceptance."""
import argparse,json,os
from pathlib import Path
import xml.etree.ElementTree as ET
parser=argparse.ArgumentParser()
parser.add_argument('--output',required=True)
parser.add_argument('--evidence')
parser.add_argument('--artifacts')
parser.add_argument('--expected-commit')
parser.add_argument('--require-final',action='store_true')
args=parser.parse_args()
base=Path(__file__).resolve().parent.parent
version=ET.parse(base/'Directory.Build.props').findtext('./PropertyGroup/Version')
artifacts=Path(args.artifacts)if args.artifacts else base/'artifacts'
def read(name):
    path=artifacts/name
    try:
        data=json.loads(path.read_text(encoding='utf-8-sig'))if path.exists()else{}
        return data if isinstance(data,dict)else{}
    except (OSError,ValueError):return {}
core=read(f'tests-{version}.json')
windows=read('WINDOWS-CHECKS.json');typed=read('typed-uia-audit.json');uia=read('accessibility-audit.json')
package=read('windows-package-smoke.json');portable=read('portable-integrity.json');piano=read('windows-piano-check.json')
recovery=read('audio-output-check.json')
maestro=read('windows-maestro-tests.json');source=read('source-build-smoke.json');nvda=read('nvda-smoke.json')
commit=args.expected_commit or os.environ.get('GITHUB_SHA')or piano.get('SourceCommit')
manualPath=Path(args.evidence)if args.evidence else artifacts/'MANUAL-ACCEPTANCE.json'
try:
    manual=json.loads(manualPath.read_text(encoding='utf-8-sig'))if manualPath.exists()else{}
    if not isinstance(manual,dict):manual={}
except (OSError,ValueError):manual={}
def success(data,minimum):return data.get('Failed')==0 and isinstance(data.get('Executed'),int)and data['Executed']>=minimum
def flags(data,names):return all(data.get(n)is True for n in names)
def records(data,name):return data.get(name,[])if isinstance(data.get(name,[]),list)else[]
boundaries=records(uia,'ExamBoundaryRegression');announcements=records(nvda,'Checks')
automated={
 'CoreTestsPassed':core.get('Version')==version and success(core,60),
 'NativeWindowsBuildPassed':windows.get('NativeWindowsBuildSucceeded')is True,
 'EvidenceMatchesSourceCommit':bool(commit)and all(r.get('SourceCommit')==commit for r in [core,piano,maestro,nvda,recovery]),
 'WindowsTypedUIAPassed':typed.get('Passed')is True and success(typed,20),
 'WindowsKeyboardAndExamBoundariesPassed':uia.get('UIAutomationPassed')is True and len(records(uia,'KeyboardChecks'))>=20 and len(boundaries)==6 and all(b.get('Rejected')is True and b.get('HistoryUnchanged')is True for b in boundaries),
 'AutomaticPracticeSafetyPassed':isinstance(uia.get('AutomaticPracticeRegression'),dict)and uia['AutomaticPracticeRegression'].get('Passed')is True and uia['AutomaticPracticeRegression'].get('Executed',0)>=6,
 'InstalledAndStandalonePassed':package.get('Version')==version and flags(package,['Passed','InstallerProcessPassed','InstalledFilesMatch','InstalledManagedAuditPassed','StandaloneManagedAuditPassed','UninstallPreservesUserFile'])and package.get('InstalledFileCount',0)>0,
 'PortableIntegrityPassed':portable.get('Passed')is True and portable.get('PrivatePianoSamplesBundled')is False,
 'WindowsPianoFidelityPassed':piano.get('Passed')is True and piano.get('DecodedSamples')==794 and piano.get('ReferencePcmSha256Matches')==794 and piano.get('CategoriesRendered')==28,
 'WindowsMaestroTestsPassed':maestro.get('Version')==version and success(maestro,67),
 'AudioOutputRecoveryChecksPassed':recovery.get('Version')==version and recovery.get('Passed')is True and success(recovery,8),
 'DeliveredSourceRebuildPassed':source.get('Version')==version and flags(source,['Passed','FrozenRestorePassed','SourceBuildPassed','TypedUiAProjectBuildPassed']),
 'ActualNvdaSpeechPipelinePassed':flags(nvda,['Passed','SignatureValid','ActualNvdaProcessTested','SpeechAnnouncementsPassed'])and len(announcements)==5 and all(c.get('Passed')is True for c in announcements)and {c.get('Name')for c in announcements}=={'Ρυθμίσεις','Εξάσκηση','Πρόοδος','Εξετάσεις και προσαρμογή','Οδηγίες'},
}
manualCurrent=bool(commit)and manual.get('Version')==version and manual.get('SourceCommit')==commit
checks={
 'AutomatedChecksPassed':all(automated.values()),
 'ManualEvidenceMatchesRelease':manualCurrent,
 'NvdaActuallyTestedAndPassed':manualCurrent and manual.get('NvdaPassed')is True,
 'PhysicalAudioHardwarePassed':manualCurrent and manual.get('AudioHardwarePassed')is True,
 'ProfessionalPianoListeningPassed':manualCurrent and manual.get('ProfessionalListeningPassed')is True,
 'DownloadOnUserComputerPassed':manualCurrent and manual.get('UserDownloadPassed')is True,
 'RemainingCurriculumScopeResolvedOrAccepted':manualCurrent and manual.get('CurriculumScopeAccepted')is True,
}
report={'Version':version,'SourceCommit':commit,'TargetFinalVersion':'1.0.0','ReadyForWindows11Trial':all(automated.values()),'ReadyForProfessionalFinal':all(checks.values()),'AutomatedChecks':automated,'Checks':checks,'MissingAutomated':[k for k,v in automated.items()if not v],'Missing':[k for k,v in checks.items()if not v]}
Path(args.output).write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Automated trial readiness:',report['ReadyForWindows11Trial'])
print('Professional final readiness:',report['ReadyForProfessionalFinal'])
print('Outstanding:',', '.join(report['Missing']+report['MissingAutomated']))
if(args.require_final or version=='1.0.0')and not report['ReadyForProfessionalFinal']:raise SystemExit(1)
