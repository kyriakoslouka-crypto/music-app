using System.Text.Json;
using Studio.Core;
using Studio.Audio;
namespace Studio.Tests;
public static class LevelCurriculumTests
{
    private static void Check(bool ok,string message="Difficulty regression"){if(!ok)throw new Exception(message);}
    private static string Fingerprint(Exercise e)=>e.CorrectId+":"+string.Join(',',e.Notes)+":"+(e.Score is null?"":JsonSerializer.Serialize(e.Score.Events));
    public static void Register(Action<string,Action> test,string[] args)
    {
        test("Difficulty: every category has six distinct actual generation streams, not renamed labels",()=>{
            foreach(var category in Enum.GetValues<Category>())foreach(var mode in Enum.GetValues<TonalityMode>()){
                var streams=new List<string>();
                foreach(var level in Enum.GetValues<Level>()){
                    var generator=new ExerciseGenerator(560+(int)category);var samples=new List<string>();
                    for(var i=0;i<16;i++){
                        var s=new Settings(category,level,MinNote:21,MaxNote:108,KeyTonic:0,KeyMode:mode);var e=generator.Next(s);Theory.VerifyExercise(e);
                        Check(e.Notes.All(n=>n>=s.MinNote&&n<=s.MaxNote));
                        var session=new Session();session.Begin(e);
                        Check(e.AnswerKind==AnswerKind.Choice?session.Answer(e.CorrectId,level,s).Correct:e.AnswerKind==AnswerKind.Taps||session.AnswerResponse(e.Score!.ExpectedText!,s).Correct);
                        samples.Add(Fingerprint(e));
                    }
                    streams.Add(string.Join(";",samples));
                }
                Check(streams.Distinct().Count()==6,category+" "+mode+": two levels have identical musical streams");
            }
        });
        test("Difficulty: Expert and Pro differ in every category with default and minor-key constraints",()=>{
            foreach(var category in Enum.GetValues<Category>())foreach(var mode in Enum.GetValues<TonalityMode>()){
                string Stream(Level level){var g=new ExerciseGenerator(566);return string.Join(';',Enumerable.Range(0,12).Select(_=>Fingerprint(g.Next(new(category,level,KeyTonic:0,KeyMode:mode)))));}
                Check(Stream(Level.Expert)!=Stream(Level.Professional),category+" "+mode+": Pro aliases Expert");
            }
        });
        test("Difficulty: independent interval limits, advanced chord targets and non-root seventh inversions",()=>{
            Check(Enum.GetValues<Level>().Select(l=>Theory.Distances(l).Max()).SequenceEqual(new[]{12,12,16,19,22,24}));
            var g=new ExerciseGenerator(561);
            for(var i=0;i<100;i++){
                var e=g.Next(new(Category.Chords,Level.Professional));Check(new[]{"half-diminished7","diminished7","minor-major7"}.Contains(e.CorrectId));
                e=g.Next(new(Category.Inversions,Level.Professional));Check(e.Notes.Length==4&&int.Parse(e.CorrectId.Split(':')[1])>=2);Check(!e.CorrectId.StartsWith("diminished7:",StringComparison.Ordinal));
                e=g.Next(new(Category.Notes,Level.Professional,MinNote:21,MaxNote:108));Check(new[]{1,3,6,8,10}.Contains(e.Notes[0]%12));Check(e.Notes[0]<=35||e.Notes[0]>=94);
            }
        });
        test("Difficulty: professional rhythm guarantees 64ths per bar and Expert uses 32nds",()=>{
            var g=new ExerciseGenerator(562);
            foreach(var level in new[]{Level.Expert,Level.Professional})foreach(var meter in TheoryV2.Meters){
                var s=new Settings(Category.RhythmDictation,level,MeterId:meter.Id,Bars:3);var e=g.Next(s);
                foreach(var bar in RhythmCodec.Parse(e.Score!.ExpectedText!,meter,3)){
                    Check(bar.Sum(c=>c.Ticks)==meter.Ticks);Check(bar[0].Ticks==(level==Level.Professional?0.25:0.5));
                    Check(bar.All(c=>c.Ticks>=(level==Level.Professional?0.25:0.5)));
                }
                Check(Schedule.Create(e,s).Where(n=>n.Action==MidiAction.NoteOn).All(n=>n.Key==60));
            }
        });
        test("Difficulty: harmonic degrees grow to seventh inversions without symmetric inversion ambiguity",()=>{
            foreach(var mode in Enum.GetValues<TonalityMode>()){
                var counts=Enum.GetValues<Level>().Select(l=>LevelCurriculum.Degrees(l,mode).Length).ToArray();Check(counts.Zip(counts.Skip(1)).All(p=>p.First<p.Second));
                foreach(var token in LevelCurriculum.Degrees(Level.Professional,mode)){
                    var chord=Harmony.Resolve(token,mode);Check(chord.Steps.Length is 3 or 4);
                    Check(!(chord.Steps.Length==4&&chord.Steps.Select(n=>(n+120)%12).Order().SequenceEqual(new[]{2,5,8,11})&&chord.Inversion!=0));
                }
            }
        });
        test("Difficulty: serial subdivision, register and polyphonic density are preserved in actual scores",()=>{
            var g=new ExerciseGenerator(563);
            foreach(var level in Enum.GetValues<Level>()){
                var s=new Settings(Category.RowTransform,level,MinNote:21,MaxNote:108);var e=g.Next(s);var recipe=JsonSerializer.Deserialize<RowRecipe>(e.Score!.Metadata!)!;
                var legacy=JsonSerializer.Deserialize<RowRecipe>("{\"Prime\":[0,1,4,2,5,8,6,7,10,9,11,3],\"Form\":\"P\",\"Transposition\":0}")!;Check(legacy.Step==1&&legacy.OctaveDisplacement==0);
                Check(recipe.Step==new[]{1.5,1.25,1.0,0.75,0.5,0.25}[(int)level]);
                Check(e.Score.Events.Where(n=>n.Voice==0).Select(n=>n.Midi%12).Distinct().Count()==12);
                Check(TheoryV4.Transform(recipe.Prime,recipe.Form,recipe.Transposition).SequenceEqual(e.Score.Events.Where(n=>n.Voice==0).Select(n=>(n.Midi-e.Score.Tonic+120)%12)));
                e=g.Next(s with{Category=Category.Polyphony4});
                for(var voice=0;voice<4;voice++){
                    var events=e.Score!.Events.Where(n=>n.Voice==voice).ToArray();Check(Math.Abs(events.Sum(n=>n.Duration/0.85)-16)<0.000001);
                    if(level>=Level.Expert)Check(Math.Abs(events[0].Duration/0.85-(level==Level.Professional?0.0625:0.125))<0.000001);
                }
            }
        });
        test("Difficulty: cadence voicings retain independent V-I versus IV-I identities at all levels",()=>{
            foreach(var level in Enum.GetValues<Level>())foreach(var mode in Enum.GetValues<TonalityMode>())foreach(var id in new[]{"pac","iac","half","plagal","deceptive"}){
                var score=TheoryV2.CadenceScore(id,60,mode,difficulty:(int)level);
                int[] Classes(double beat)=>score.Events.Where(n=>n.Beat==beat).Select(n=>(n.Midi-60+120)%12).Distinct().Order().ToArray();
                if(id is "pac" or "iac" or "deceptive")Check(Classes(4).SequenceEqual(new[]{2,7,11}));
                if(id=="plagal")Check(Classes(4).SequenceEqual(mode==TonalityMode.Major?new[]{0,5,9}:new[]{0,5,8}));
                if(id is "pac" or "iac" or "plagal")Check(Classes(6).SequenceEqual(mode==TonalityMode.Major?new[]{0,4,7}:new[]{0,3,7}));
                var last=score.Events.Where(n=>n.Beat==6).OrderBy(n=>n.Midi).ToArray();
                if(id=="pac")Check((last[^1].Midi-60)%12==0);
                if(id=="iac")Check((last[^1].Midi-60)%12==7);
            }
        });
        test("Difficulty: new voicings and serial timing survive progress and exact grading",()=>{
            var directory=Path.Combine(Path.GetTempPath(),"pets-levels-"+Guid.NewGuid().ToString("N"));
            try{
                var store=new ProgressStore(directory);var g=new ExerciseGenerator(564);var attempts=new List<Attempt>();
                foreach(var c in new[]{Category.Cadences,Category.RowTransform,Category.Polyphony4,Category.Degrees}){
                    var s=new Settings(c,Level.Professional,MinNote:21,MaxNote:108);var e=g.Next(s);var session=new Session();session.Begin(e);
                    var a=e.AnswerKind==AnswerKind.Choice?session.Answer(e.CorrectId,s.Level,s):session.AnswerResponse(e.Score!.ExpectedText!,s);Check(a.Correct);attempts.Add(a);
                }
                store.Save(attempts);var restored=store.Load();Check(restored.Count==4);foreach(var a in restored){Theory.VerifyExercise(a.Snapshot!);Check(a.Correct);}
            }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        });
        var index=Array.IndexOf(args,"--maestro");if(index>=0)test("Difficulty: actual Maestro renders six levels of rhythm, melody, polyphony, cadence and serial timing",()=>{
            var renderer=new PianoRenderer(args[index+1]);var g=new ExerciseGenerator(565);
            foreach(var level in Enum.GetValues<Level>())foreach(var c in new[]{Category.Polyphony4,Category.RowTransform,Category.Cadences,Category.MelodyDictation,Category.RhythmPatterns}){
                var s=new Settings(c,level,MinNote:21,MaxNote:108,Tempo:120,SegmentEnd:c==Category.Cadences?null:6);var e=g.Next(s);var pcm=renderer.Render(e,s,renderer.Presets[0]);Check(pcm.All(float.IsFinite)&&pcm.Any(n=>Math.Abs(n)>0.001f));
            }
        });
    }
}
