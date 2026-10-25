using Studio.Core;
using Studio.Audio;
namespace Studio.Tests;
public static class CadenceEvidenceTests
{
    private static void Check(bool ok){if(!ok)throw new Exception("Cadence evidence regression");}
    private static void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Misleading cadence accepted");}
    public static void Register(Action<string,Action> test,string[] args)
    {
        test("Cadence evidence: independent explicit F-major to C-major is plagal, G-major to C-major is perfect",()=>{
            Check(CadenceAnalysis.Infer(60,TonalityMode.Major,[53,60,69,77],[60,64,67,72]).Id=="plagal");
            Check(CadenceAnalysis.Infer(60,TonalityMode.Major,[55,62,67,71],[60,64,67,72]).Id=="pac");
            Check(CadenceAnalysis.Infer(60,TonalityMode.Major,[55,62,71,79],[60,64,72,79]).Id=="iac");
            Check(CadenceAnalysis.Infer(60,TonalityMode.NaturalMinor,[53,60,68,77],[60,63,67,72]).Id=="plagal");
            Reject(()=>CadenceAnalysis.Infer(60,TonalityMode.Major,[53],[60]));
        });
        test("Cadence evidence: all twelve keys, six levels and both modes agree with an independent classifier",()=>{
            foreach(var level in Enum.GetValues<Level>())foreach(var mode in Enum.GetValues<TonalityMode>())for(var tonic=48;tonic<60;tonic++)foreach(var id in new[]{"pac","iac","half","plagal","deceptive"}){
                var score=TheoryV2.CadenceScore(id,tonic,mode,difficulty:(int)level);Check(CadenceAnalysis.FromScore(score).Id==id);
            }
        });
        test("Cadence evidence: plagal answers grade correctly and actual chord analysis is attached",()=>{
            foreach(var level in Enum.GetValues<Level>()){
                var score=TheoryV2.CadenceScore("plagal",60,TonalityMode.Major,difficulty:(int)level);
                var e=new Exercise("plagal-proof",Category.Cadences,score.Events.Select(n=>n.Midi).ToArray(),"plagal","Πλάγια πτώση",TheoryV2.Cadences,Direction.Harmonic,score);
                var session=new Session();session.Begin(e);var correct=session.Answer("plagal",level);Check(correct.Correct&&correct.ExpectedId=="plagal"&&correct.Analysis!.Contains("IV–I",StringComparison.Ordinal));
                session.Begin(e);var wrong=session.Answer("pac",level);Check(!wrong.Correct&&wrong.ExpectedId=="plagal");
                Reject(()=>new Session().Begin(e with{CorrectId="pac",CorrectName="Αυθεντική τέλεια"}));
            }
        });
        test("Cadence evidence: final scheduled piano keys independently match labels across rates and repeats",()=>{
            var g=new ExerciseGenerator(570);
            foreach(var level in Enum.GetValues<Level>())foreach(var rate in new[]{22050,44100,48000})foreach(var pedal in new[]{false,true}){
                var s=new Settings(Category.Cadences,level,MinNote:21,MaxNote:108,Repetitions:2,Pedal:pedal);var e=g.Next(s);
                var chords=Schedule.Create(e,s,rate).Where(n=>n.Action==MidiAction.NoteOn).GroupBy(n=>n.Frame).OrderBy(g=>g.Key).TakeLast(2).ToArray();
                Check(CadenceAnalysis.Infer(e.Score!.Tonic,e.Score.Mode,chords[0].Select(n=>n.Key),chords[1].Select(n=>n.Key)).Id==e.CorrectId);
            }
        });
        test("Cadence evidence: incomplete introduction or solo-voice playback cannot be graded as a complete cadence",()=>{
            var s=new Settings(Category.Cadences);var e=new ExerciseGenerator(571).Next(s);
            Reject(()=>Schedule.Create(e,s with{SegmentEnd=3}));Reject(()=>Schedule.Create(e,s with{SoloVoice=3}));
            Check(Schedule.Create(e,s with{SegmentStart=4,SegmentEnd=8}).Length>0);
        });
        test("Cadence evidence: actual chord explanation survives progress restore",()=>{
            var dir=Path.Combine(Path.GetTempPath(),"pets-cadence-"+Guid.NewGuid().ToString("N"));
            try{
                var s=new Settings(Category.Cadences);var e=new ExerciseGenerator(572).Next(s);var session=new Session();session.Begin(e);var a=session.Answer(e.CorrectId,s.Level,s);
                var store=new ProgressStore(dir);store.Save([a]);var restored=store.Load().Single();Check(restored.Analysis==a.Analysis&&restored.Analysis!.Contains("Μπάσο",StringComparison.Ordinal));
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        });
        var index=Array.IndexOf(args,"--maestro");if(index>=0)test("Cadence evidence: actual Maestro PCM exactly matches independently written authentic and plagal references",()=>{
            var renderer=new PianoRenderer(args[index+1]);var s=new Settings(Category.Cadences,Tempo:120);
            foreach(var id in new[]{"pac","plagal"}){
                int[] first=id=="pac"?[55,62,67,71]:[53,60,69,77];int[] last=[60,64,67,72];
                var notes=new List<ScoreNote>();
                foreach(var (keys,beat,duration) in new[]{(last,0.0,1.6),(first,4.0,1.8),(last,6.0,1.8)})for(var v=0;v<4;v++)notes.Add(new(keys[v],beat,duration,v));
                var reference=new MusicalScore(notes.ToArray(),8,"independent-reference",60,TonalityMode.Major,"Reference");
                var score=TheoryV2.CadenceScore(id,60,TonalityMode.Major);var e=new Exercise("audio-proof",Category.Cadences,score.Events.Select(n=>n.Midi).ToArray(),id,id,TheoryV2.Cadences,Direction.Harmonic,score);
                var actual=renderer.Render(e,s,renderer.Presets[0]);var expected=renderer.RenderPreview(reference,s,renderer.Presets[0]);Check(actual.Any(n=>Math.Abs(n)>0.001f)&&actual.SequenceEqual(expected));
            }
        });
    }
}
