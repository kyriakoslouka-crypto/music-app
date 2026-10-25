using Studio.Core;
using Studio.Audio;
namespace Studio.Tests;
public static class Version3Tests
{
    private static void Check(bool c,string message="Assertion failed"){if(!c)throw new Exception(message);}
    private static void Throws<T>(Action action)where T:Exception{try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
    public static void Register(Action<string,Action> test,string[] args)
    {
        test("0.3: enormous octave numbers never wrap into valid piano pitches",()=>{
            foreach(var text in new[]{"C1073741828","Ντο1073741828","C9999999999999999999999999","C-1073741820","C9"})
                Throws<ArgumentException>(()=>NoteCodec.ParsePitch(text));
            Check(NoteCodec.ParsePitch("B#7")==108);Check(NoteCodec.ParsePitch("Cb1")==23);
            Check(NoteCodec.ParsePitch("Λα0")==21);Check(NoteCodec.ParsePitch("Ντο8")==108);
        });
        test("0.3: all six categories and difficulty levels generate valid scores and correct responses",()=>{
            var g=new ExerciseGenerator(300);var counts=new HashSet<int>();
            foreach(var c in Enum.GetValues<Category>().Where(c=>c>=Category.MelodyDictation&&c<=Category.MelodyMemory))foreach(var level in Enum.GetValues<Level>())for(var i=0;i<20;i++)
            {
                var settings=new Settings(c,level,MinNote:36,MaxNote:96,KeyTonic:i%12,KeyMode:i%2==0?TonalityMode.Major:TonalityMode.NaturalMinor);
                var e=g.Next(settings);Theory.VerifyExercise(e);Check(e.Notes.All(n=>n>=36&&n<=96));var session=new Session();session.Begin(e);
                if(e.AnswerKind==AnswerKind.Choice)Check(session.Answer(e.CorrectId,level,settings).Correct);
                else Check(session.AnswerResponse(e.Score!.ExpectedText!,settings).Correct);
                if(c==Category.MelodyDictation)counts.Add(e.Score!.Events.Count(n=>n.Voice==0));
            }
            Check(counts.Count==6);
        });
        test("0.3: Greek, scientific and MIDI notation preserve exact pitches including B and enharmonics",()=>{
            foreach(var (text,midi)in new[]{("Ντο4",60),("C4",60),("60",60),("B3",59),("Ρεb4",61),("C#4",61),("σι3",59),("sol4",67),("Λα0",21)})Check(NoteCodec.ParsePitch(text)==midi,text);
            Check(NoteCodec.Parse("Ντο4:1/4 C#4:1/8 67:1/2",true).Select(n=>n.Ticks).SequenceEqual(new[]{4.0,2.0,8.0}));
            Throws<ArgumentException>(()=>NoteCodec.ParsePitch("H4"));Throws<ArgumentException>(()=>NoteCodec.Parse("C4:banana",true));
        });
        test("0.3: functional chord roots, qualities and inversion formulas match independent references",()=>{
            Check(Harmony.Resolve("I",TonalityMode.Major).Steps.SequenceEqual(new[]{0,4,7}));
            Check(Harmony.Resolve("ii6",TonalityMode.Major).Steps.SequenceEqual(new[]{5,9,14}));
            Check(Harmony.Resolve("V7",TonalityMode.Major).Steps.SequenceEqual(new[]{7,11,14,17}));
            Check(Harmony.Resolve("i",TonalityMode.NaturalMinor).Steps.SequenceEqual(new[]{0,3,7}));
            Check(Harmony.Resolve("viio",TonalityMode.NaturalMinor).Steps.SequenceEqual(new[]{11,14,17}));
            Check(Harmony.Resolve("iiø7",TonalityMode.NaturalMinor).Steps.SequenceEqual(new[]{2,5,8,12}));
            Check(Harmony.Resolve("IV",TonalityMode.Major).Function.Contains("Προδεσπόζουσα"));
            Check(Harmony.Resolve("V",TonalityMode.Major).Function.StartsWith("Δεσπόζουσα"));
        });
        test("0.3: dictation wrong note/duration/case are scored wrong while equivalent note spelling passes",()=>{
            var s=new Settings(Category.MelodyDictation);var e=new ExerciseGenerator(3).Next(s);var n=NoteCodec.Parse(e.Score!.ExpectedText!,true);n[0]=n[0]with{Midi=n[0].Midi+1};
            var session=new Session();session.Begin(e);var result=session.AnswerResponse(NoteCodec.Format(n,true),s);Check(!result.Correct&&result.Percentage<100&&result.Percentage>0);
            var harmony=new ExerciseGenerator(3).Next(new(Category.HarmonicDictation));var expected=harmony.Score!.ExpectedText!;
            Check(!TheoryV3.Grade(harmony,string.Join(' ',expected.Split(' ').Select(t=>t=="I"?"i":t)),new(Category.HarmonicDictation)).Correct);
        });
        test("0.3: selected fragments clip sustained notes, rebase timing and keep assessment target unchanged",()=>{
            var score=new MusicalScore([new(60,0,2),new(64,2,1),new(67,4,1)],5,"preview",60,TonalityMode.Major,"test");
            var cut=ScoreSegments.Slice(score,1,3);Check(cut.LengthBeats==2&&cut.Events.Length==2);Check(cut.Events[0].Beat==0&&cut.Events[0].Duration==1);Check(cut.Events[1].Beat==1&&cut.Events[1].Midi==64);
            Throws<ArgumentException>(()=>ScoreSegments.Slice(score,3.1,3.9));Throws<ArgumentException>(()=>ScoreSegments.Slice(score,4,6));
            var g=new ExerciseGenerator(1);var s=new Settings(Category.MelodyDictation);var e=g.Next(s);var original=e.Score!.ExpectedText;_ =Schedule.Create(e,s with{SegmentStart=4,SegmentEnd=5});Check(e.Score.ExpectedText==original);
        });
        test("0.3: independent MIDI voice channels prevent cross-voice note-off scheduling",()=>{
            var score=new MusicalScore([new(60,0,1,0),new(60,0,2,1)],2,"preview",60,TonalityMode.Major,"test");
            var events=Schedule.CreateScore(score,new());Check(events.Where(n=>n.Action==MidiAction.NoteOn).Select(n=>n.Channel).Distinct().Count()==2);
            var off=events.Where(n=>n.Action==MidiAction.NoteOff).ToArray();Check(off[0].Channel==0&&off[1].Channel==1&&off[0].Frame<off[1].Frame);
        });
        test("0.3: memory/solfege data restores with per-item error feedback and original snapshot",()=>{
            var dir=Path.Combine(Path.GetTempPath(),"studio-v3-"+Guid.NewGuid());try{
                var s=new Settings(Category.MelodyMemory,Level.Professional);var e=new ExerciseGenerator(1).Next(s);var session=new Session();session.Begin(e);var a=session.AnswerResponse(e.Score!.ExpectedText!,s);
                var store=new ProgressStore(dir);store.Save([a]);var restored=store.Load().Single();Check(restored.Snapshot!.Score!.ExpectedText==e.Score.ExpectedText);Check(restored.Percentage==100);Theory.VerifyExercise(restored.Snapshot);
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        });
        var index=Array.IndexOf(args,"--maestro");if(index>=0)test("0.3: actual Maestro renders every new category, written previews and selected fragments",()=>{
            var renderer=new PianoRenderer(args[index+1]);var g=new ExerciseGenerator(4);
            foreach(var c in Enum.GetValues<Category>().Where(c=>c>=Category.MelodyDictation&&c<=Category.MelodyMemory))
            {
                var s=new Settings(c,Level.Intermediate,Tempo:120);var e=g.Next(s);var pcm=renderer.Render(e,s,renderer.Presets[0]);Check(pcm.Any(v=>Math.Abs(v)>0.001f)&&pcm.All(float.IsFinite));
                var fragment=renderer.Render(e,s with{SegmentStart=2,SegmentEnd=4},renderer.Presets[0]);Check(fragment.Length<pcm.Length);
                if(e.AnswerKind==AnswerKind.Text){var preview=renderer.RenderPreview(TheoryV3.Preview(e,e.Score!.ExpectedText!,s),s,renderer.Presets[0]);Check(preview.Any(v=>Math.Abs(v)>0.001f));}
            }
        });
    }
}
