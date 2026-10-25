"""Synthetic gate tests; fixtures never count as actual NVDA/hardware acceptance."""
import json,subprocess,sys,tempfile,unittest
from pathlib import Path
import xml.etree.ElementTree as ET
SCRIPT=Path(__file__).with_name('release-readiness.py')
VERSION=ET.parse(SCRIPT.parent.parent/'Directory.Build.props').findtext('./PropertyGroup/Version')
COMMIT='a'*40
NAMES=['Ρυθμίσεις','Εξάσκηση','Πρόοδος','Εξετάσεις και προσαρμογή','Οδηγίες']
class GateTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
  self.data={
   f'tests-{VERSION}.json':dict(Version=VERSION,Executed=81,Failed=0,SourceCommit=COMMIT),
   'WINDOWS-CHECKS.json':dict(NativeWindowsBuildSucceeded=True),
   'typed-uia-audit.json':dict(Passed=True,Executed=20,Failed=0),
   'accessibility-audit.json':dict(UIAutomationPassed=True,AutomaticPracticeRegression=dict(Passed=True,Executed=6),KeyboardChecks=list(range(20)),ExamBoundaryRegression=[dict(Rejected=True,HistoryUnchanged=True)for _ in range(6)]),
   'windows-package-smoke.json':dict(Version=VERSION,InstalledFileCount=505,**dict.fromkeys(['Passed','InstallerProcessPassed','InstalledFilesMatch','InstalledManagedAuditPassed','StandaloneManagedAuditPassed','UninstallPreservesUserFile'],True)),
   'portable-integrity.json':dict(Passed=True,PrivatePianoSamplesBundled=False),
   'windows-piano-check.json':dict(Passed=True,DecodedSamples=794,ReferencePcmSha256Matches=794,CategoriesRendered=28,SourceCommit=COMMIT),
   'windows-maestro-tests.json':dict(Version=VERSION,Executed=90,Failed=0,SourceCommit=COMMIT,Outcomes=[dict(Name=f"Rhythm input: synthetic fixture {i}",Passed=True)for i in range(12)]+[dict(Name=f"Difficulty: synthetic fixture {i}",Passed=True)for i in range(9)]+[dict(Name="Cadences: no authentic context before plagal and independent final-chord classification",Passed=True)]),
   'automatic-practice-check.json':dict(Version=VERSION,SourceCommit=COMMIT,Passed=True,AutomaticExamples=2,NonzeroMaestroExamples=2,ManualReplayCalls=0,RealDelayCancellationPassed=True),
   'audio-output-check.json':dict(Version=VERSION,Passed=True,Executed=8,Failed=0,SourceCommit=COMMIT),
   'source-build-smoke.json':dict(Version=VERSION,**dict.fromkeys(['Passed','FrozenRestorePassed','SourceBuildPassed','TypedUiAProjectBuildPassed'],True)),
   'nvda-smoke.json':dict(SourceCommit=COMMIT,Checks=[dict(Name=n,Passed=True)for n in NAMES],**dict.fromkeys(['Passed','SignatureValid','ActualNvdaProcessTested','SpeechAnnouncementsPassed'],True)),
  }
 def tearDown(self):self.temp.cleanup()
 def runGate(self,final=False):
  for name,data in self.data.items():(self.root/name).write_text(json.dumps(data),encoding='utf-8')
  output=self.root/'result.json'
  result=subprocess.run([sys.executable,str(SCRIPT),'--artifacts',str(self.root),'--expected-commit',COMMIT,'--output',str(output)]+(['--require-final']if final else[]),capture_output=True,text=True)
  return result.returncode,json.loads(output.read_text())
 def test_auto_success_does_not_certify_manual_acceptance(self):
  code,r=self.runGate(True);self.assertEqual(code,1);self.assertTrue(r['ReadyForWindows11Trial']);self.assertFalse(r['ReadyForProfessionalFinal'])
 def test_each_failed_report_blocks_trial(self):
  for name in list(self.data):
   with self.subTest(report=name):
    old=self.data[name];self.data[name]={};_,r=self.runGate();self.assertFalse(r['ReadyForWindows11Trial']);self.data[name]=old
 def test_stale_commit_cannot_pass(self):
  self.data['nvda-smoke.json']['SourceCommit']='b'*40
  _,r=self.runGate();self.assertFalse(r['AutomatedChecks']['EvidenceMatchesSourceCommit'])
 def test_manual_flags_need_current_version_and_commit(self):
  evidence=dict.fromkeys(['NvdaPassed','AudioHardwarePassed','ProfessionalListeningPassed','UserDownloadPassed','CurriculumScopeAccepted'],True)
  self.data['MANUAL-ACCEPTANCE.json']=dict(evidence,Version=VERSION,SourceCommit='b'*40)
  _,r=self.runGate();self.assertFalse(r['ReadyForProfessionalFinal'])
  self.data['MANUAL-ACCEPTANCE.json']['SourceCommit']=COMMIT
  code,r=self.runGate(True);self.assertEqual(code,0);self.assertTrue(r['ReadyForProfessionalFinal'])
 def test_duplicate_nvda_names_cannot_replace_fifth_tab(self):
  self.data['nvda-smoke.json']['Checks'][4]['Name']='Ρυθμίσεις'
  _,r=self.runGate();self.assertFalse(r['AutomatedChecks']['ActualNvdaSpeechPipelinePassed'])
 def test_wrong_version_cannot_reuse_old_tests(self):
  self.data[f'tests-{VERSION}.json']['Version']='0.4.0'
  _,r=self.runGate();self.assertFalse(r['AutomatedChecks']['CoreTestsPassed'])
if __name__=='__main__':unittest.main(verbosity=2)
