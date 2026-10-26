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
live=read('ison-live-check.json');flow=read('automatic-practice-check.json');ison=read('windows-ison-check.json')
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
 'CoreTestsPassed':core.get('Version')==version and success(core,96),
 'NativeIsonDecodeRenderPassed':ison.get('Version')==version and ison.get('Passed')is True and ison.get('DecodedFormats')==4 and ison.get('PrivateSamplesBundled')is False,
 'IsonNativeAccessibilityPassed':isinstance(uia.get('Isokratis'),dict)and flags(uia['Isokratis'],['Passed','NoSamplesBundled'])and uia['Isokratis'].get('TabCount')==7 and uia['Isokratis'].get('ModeCount')==8,
 'ResponseOptionTestsPassed':len([o for o in records(maestro,'Outcomes')if o.get('Name','').startswith('Response options:')and o.get('Passed')is True])>=4,
 'ResponseOptionNativeUiPassed':len([p for p in records(uia,'PresentationRegression')if p.get('AnswerKind')=='Text'and p.get('NoTypingRequired')is True and p.get('NoPreselectedAnswer')is True and p.get('ChoiceCount',0)>=2])==12,
 'LiveIsonNavigationPassed':live.get('Version')==version and live.get('Passed')is True and live.get('DeviceOpens')==1 and live.get('Metrics',{}).get('ArrowDirections')==4 and live.get('Metrics',{}).get('ModeCount')==8,
 'FreeIsonTuningPassed':len([o for o in records(maestro,'Outcomes')if o.get('Name','').startswith('Free tuning:')and o.get('Passed')is True])>=3 and live.get('Metrics',{}).get('FreeTuningPassed')is True and live.get('Metrics',{}).get('PerModeRestorePassed')is True and live.get('Metrics',{}).get('NativeTuningEditorPassed')is True and uia.get('Isokratis',{}).get('CustomAccidentalMinimum')==-72 and uia.get('Isokratis',{}).get('CustomAccidentalMaximum')==72 and uia.get('Isokratis',{}).get('TuningEditor',{}).get('Passed')is True and uia.get('Isokratis',{}).get('TuningEditor',{}).get('IntervalControls')==7 and uia.get('Isokratis',{}).get('TuningEditor',{}).get('AlterationControls')==7,
 'ByzantineIntonationPassed':len([o for o in records(maestro,'Outcomes')if o.get('Name','').startswith('Intonation:')and o.get('Passed')is True])>=5 and live.get('Metrics',{}).get('GenusCount')==5 and live.get('Metrics',{}).get('SharpFlatPassed')is True and live.get('Metrics',{}).get('IntonationRestorePassed')is True and live.get('Metrics',{}).get('UnsupportedPitchPreservesLive')is True and uia.get('Isokratis',{}).get('GenusCount')==5 and uia.get('Isokratis',{}).get('NoteCount')==15 and uia.get('Isokratis',{}).get('AccidentalCount')==9,
 'LiveIsonStorageTestsPassed':len([o for o in records(maestro,'Outcomes')if o.get('Name','').startswith('Live ison:')and o.get('Passed')is True])>=4,
 'IsonUnitTestsPassed':len([o for o in records(maestro,'Outcomes')if o.get('Name','').startswith('Ison:')and o.get('Passed')is True])>=4,
 'NativeWindowsBuildPassed':windows.get('NativeWindowsBuildSucceeded')is True,
 'EvidenceMatchesSourceCommit':bool(commit)and all(r.get('SourceCommit')==commit for r in [core,piano,maestro,nvda,recovery,flow,ison,live]),
 'WindowsTypedUIAPassed':typed.get('Passed')is True and success(typed,24),
 'WindowsKeyboardAndExamBoundariesPassed':uia.get('UIAutomationPassed')is True and len(records(uia,'KeyboardChecks'))>=26 and len(boundaries)==6 and all(b.get('Rejected')is True and b.get('HistoryUnchanged')is True for b in boundaries),
 'AutomaticPracticeSafetyPassed':isinstance(uia.get('AutomaticPracticeRegression'),dict)and uia['AutomaticPracticeRegression'].get('Passed')is True and uia['AutomaticPracticeRegression'].get('Executed',0)>=6,
 'AutomaticPracticeEndToEndPassed':flow.get('Version')==version and flow.get('Passed')is True and flow.get('AutomaticExamples')==2 and flow.get('NonzeroMaestroExamples')==2 and flow.get('ManualReplayCalls')==0 and flow.get('RealDelayCancellationPassed')is True,
 'InstalledAndStandalonePassed':package.get('Version')==version and flags(package,['Passed','InstallerProcessPassed','InstalledFilesMatch','InstalledManagedAuditPassed','StandaloneManagedAuditPassed','UninstallPreservesUserFile'])and package.get('InstalledFileCount',0)>0,
 'PortableIntegrityPassed':portable.get('Passed')is True and portable.get('PrivatePianoSamplesBundled')is False and portable.get('PrivateIsokratisSamplesBundled')is False,
 'WindowsPianoFidelityPassed':piano.get('Passed')is True and piano.get('DecodedSamples')==794 and piano.get('ReferencePcmSha256Matches')==794 and piano.get('CategoriesRendered')==28,
 'WindowsMaestroTestsPassed':maestro.get('Version')==version and success(maestro,107),
 'DifficultyCurriculumChecksPassed':len([o for o in records(maestro,'Outcomes')if o.get('Name','').startswith('Difficulty:')and o.get('Passed')is True])>=9,
 'IndependentCadenceEvidencePassed':len([o for o in records(maestro,'Outcomes')if o.get('Name','').startswith('Cadence evidence:')and o.get('Passed')is True])>=7,
 'NotationCoachSurpriseTestsPassed':len([o for o in records(maestro,'Outcomes')if o.get('Name','').startswith('Study features:')and o.get('Passed')is True])>=10,
 'NotationAndSurpriseUiPassed':isinstance(uia.get('NewStudyFeatures'),dict)and flags(uia['NewStudyFeatures'],['Passed','PrivacyChecksPassed'])and uia['NewStudyFeatures'].get('TabCount')==7 and uia['NewStudyFeatures'].get('MusicFont')=='Bravura',
 'CadenceContextChecksPassed':any(o.get('Name')=='Cadences: no authentic context before plagal and independent final-chord classification'and o.get('Passed')is True for o in records(maestro,'Outcomes')),
 'RhythmicNotationChecksPassed':len([o for o in records(maestro,'Outcomes')if o.get('Name','').startswith('Rhythm input:')and o.get('Passed')is True])>=12,
 'AudioOutputRecoveryChecksPassed':recovery.get('Version')==version and recovery.get('Passed')is True and success(recovery,9),
 'DeliveredSourceRebuildPassed':source.get('Version')==version and flags(source,['Passed','FrozenRestorePassed','SourceBuildPassed','TypedUiAProjectBuildPassed']),
 'ActualNvdaSpeechPipelinePassed':flags(nvda,['Passed','SignatureValid','ActualNvdaProcessTested','SpeechAnnouncementsPassed'])and len(announcements)==7 and all(c.get('Passed')is True for c in announcements)and {c.get('Name')for c in announcements}=={'Ρυθμίσεις','Εξάσκηση','Πρόοδος','Εξετάσεις και προσαρμογή','Οδηγίες','Απρόοπτα διαγωνίσματα','Βυζαντινός ισοκράτης'},
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
