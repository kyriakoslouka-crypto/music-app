using Studio.Core;
using Studio.Audio;
using System.Text.Json;
namespace Studio.Tests;
public static class Version4Tests
{
    private static void Check(bool condition,string message="Assertion failed"){if(!condition)throw new Exception(message);}
    private static void Throws<T>(Action action)where T:Exception{try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
    public static void Register(Action<string,Action> test,string[] args)
    {
        test("0.4: eleven categories, six levels, twelve tonics generate and score valid advanced exercises",()=>{
            var g=new ExerciseGenerator(400);
            foreach(var c in Enum.GetValues<Category>().Where(c=>c>=Category.Polyphony2))foreach(var level in Enum.GetValues<Level>())for(var i=0;i<12;i++)
            {
                var settings=new Settings(c,level,MinNote:21,MaxNote:108,KeyTonic:i,KeyMode:i%2==0?TonalityMode.Major:TonalityMode.NaturalMinor);
                var e=g.Next(settings);Theory.VerifyExercise(e);Check(e.Notes.All(n=>n>=21&&n<=108));var session=new Session();session.Begin(e);
                if(e.AnswerKind==AnswerKind.Choice)Check(session.Answer(e.CorrectId,level,settings).Correct);else Check(session.AnswerResponse(e.Score!.ExpectedText!,settings).Correct);
                Check(Schedule.Create(e,settings).All(n=>n.Channel!=9));
            }
        });
        test("0.4: independent polyphonic rhythms, register order, target voice and solo channels",()=>{
            var g=new ExerciseGenerator(82);
            foreach(var c in new[]{Category.Polyphony2,Category.Polyphony3,Category.Polyphony4,Category.InnerVoice})
            {
                var s=new Settings(c,Level.Advanced,MinNote:36,MaxNote:96);var e=g.Next(s);var count=c==Category.Polyphony2?2:c==Category.Polyphony3?3:4;
                Check(e.Score!.Events.Where(n=>n.Voice>=0).Select(n=>n.Voice).Distinct().Count()==count);
                var fingerprints=Enumerable.Range(0,count).Select(v=>string.Join(',',e.Score.Events.Where(n=>n.Voice==v).Select(n=>n.Beat))).ToArray();Check(fingerprints.Distinct().Count()==count);
                if(c==Category.InnerVoice)Check(e.Score.TargetVoice is 1 or 2);
                var events=Schedule.Create(e,s with{SoloVoice=e.Score.TargetVoice});Check(events.Where(n=>n.Action==MidiAction.NoteOn).All(n=>n.Channel==15||n.Channel==e.Score.TargetVoice));
                Throws<ArgumentException>(()=>Schedule.Create(e,s with{SoloVoice=6}));
            }
        });
        test("0.4: twelve-tone P/I/R/RI algebra, all transpositions and unique pitch classes",()=>{
            int[] row=[0,1,4,2,5,8,6,7,10,9,11,3];
            Check(TheoryV4.Transform(TheoryV4.Transform(row,"I"),"I").SequenceEqual(row));Check(TheoryV4.Transform(TheoryV4.Transform(row,"R"),"R").SequenceEqual(row));
            Check(TheoryV4.Transform(row,"RI").SequenceEqual(TheoryV4.Transform(TheoryV4.Transform(row,"I"),"R")));
            foreach(var form in TheoryV4.RowForms)for(var t=0;t<12;t++)Check(TheoryV4.Transform(row,form.Id,t).Order().SequenceEqual(Enumerable.Range(0,12)));
            Throws<ArgumentException>(()=>TheoryV4.Transform(new int[12],"P"));
        });
        test("0.4: secondary dominants, Neapolitan and augmented-sixth pitch references",()=>{
            Check(Harmony.Resolve("V/V",TonalityMode.Major).Steps.SequenceEqual(new[]{14,18,21}));
            Check(Harmony.Resolve("V7/ii",TonalityMode.Major).Steps.SequenceEqual(new[]{9,13,16,19}));
            Check(Harmony.Resolve("bII6",TonalityMode.Major).Steps.SequenceEqual(new[]{5,8,13}));
            Check(Harmony.Resolve("It+6",TonalityMode.Major).Steps.SequenceEqual(new[]{-4,0,6}));
            Check(Harmony.Resolve("Fr+6",TonalityMode.Major).Steps.SequenceEqual(new[]{-4,0,2,6}));
            Check(Harmony.Resolve("Ger+6",TonalityMode.Major).Steps.SequenceEqual(new[]{-4,0,3,6}));
            foreach(var mode in Enum.GetValues<TonalityMode>())foreach(var tonic in Enumerable.Range(48,12))foreach(var form in new[]{"It+6","Fr+6","Ger+6"})
            {
                var i=mode==TonalityMode.Major?"I":"i";var text=i+" "+form+" "+(form=="Ger+6"?i+"64 ":"")+"V "+i;
                var score=TheoryV4.ChromaticScore(text,tonic,mode,21,108,"chromatic");var aug=score.Events.Where(n=>n.Beat==4).OrderBy(n=>n.Voice).ToArray();var next=score.Events.Where(n=>n.Beat==6).OrderBy(n=>n.Voice).ToArray();
                Check(next[0].Midi==aug[0].Midi-1&&next[^1].Midi==aug[^1].Midi+1);
            }
        });
        test("0.4: ninth/eleventh/thirteenth chords and 36-semitone intervals retain actual pitches",()=>{
            Check(TheoryV4.ExtendedChords.Single(c=>c.Id=="dominant13").Steps.SequenceEqual(new[]{0,4,7,10,14,17,21}));
            Check(TheoryV4.ExtendedChords.Single(c=>c.Id=="major9").Steps.SequenceEqual(new[]{0,4,7,11,14}));
            var g=new ExerciseGenerator(5);var seen=new HashSet<int>();
            for(var i=0;i<300;i++){var e=g.Next(new(Category.ExtendedIntervals,Level.Professional,MinNote:21,MaxNote:108));var d=Math.Abs(e.Notes[1]-e.Notes[0]);seen.Add(d);Check(e.CorrectId=="interval:"+d);}
            Check(seen.Count==24&&seen.Contains(36));
        });
        test("0.4: advanced snapshots, row metadata and target voices survive progress restore",()=>{
            var dir=Path.Combine(Path.GetTempPath(),"studio-v4-"+Guid.NewGuid());try{
                var s=new Settings(Category.RowTransform,Level.Professional);var e=new ExerciseGenerator(4).Next(s);var session=new Session();session.Begin(e);var a=session.Answer(e.CorrectId,s.Level,s);var store=new ProgressStore(dir);store.Save([a]);var restored=store.Load().Single();Theory.VerifyExercise(restored.Snapshot!);
                var recipe=JsonSerializer.Deserialize<RowRecipe>(restored.Snapshot!.Score!.Metadata!)!;Check(recipe.Prime.Distinct().Count()==12);Check(restored.Correct);
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        });
        test("0.4: corrupt polyphonic target and serial transformations are rejected",()=>{
            var g=new ExerciseGenerator(8);var e=g.Next(new(Category.Polyphony4));Throws<ArgumentException>(()=>Theory.VerifyExercise(e with{Score=e.Score!with{TargetVoice=10}}));
            var row=g.Next(new(Category.RowTransform));Throws<ArgumentException>(()=>Theory.VerifyExercise(row with{CorrectId=row.CorrectId=="P"?"I":"P"}));
        });
        var index=Array.IndexOf(args,"--maestro");if(index>=0)test("0.4: real Maestro renders all advanced categories including four-voice solo and seven-note chords",()=>{
            var renderer=new PianoRenderer(args[index+1]);var g=new ExerciseGenerator(8);
            foreach(var c in Enum.GetValues<Category>().Where(c=>c>=Category.Polyphony2)){
                var s=new Settings(c,Level.Professional,Tempo:120,MinNote:36,MaxNote:96);var e=g.Next(s);var pcm=renderer.Render(e,s,renderer.Presets[0]);Check(pcm.All(float.IsFinite)&&pcm.Any(v=>Math.Abs(v)>0.001f));Check(pcm.Max(v=>Math.Abs(v))<=0.901f);
                if(c==Category.Polyphony4){var solo=renderer.Render(e,s with{SoloVoice=e.Score!.TargetVoice},renderer.Presets[0]);Check(!solo.SequenceEqual(pcm));}
            }
        });
    }
}
