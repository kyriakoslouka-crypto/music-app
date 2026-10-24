using Studio.Core;
using Studio.Audio;
using System.Text.Json;
namespace Studio.Tests;
public static class Version2Tests
{
    private static void Check(bool valid,string message="Assertion failed"){if(!valid)throw new Exception(message);}
    private static void Throws<T>(Action action)where T:Exception{try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
    public static void Register(Action<string,Action> test,string[] args)
    {
        test("0.2: all seven new categories, six levels, 2,520 generated/scored performances",()=>{
            var g=new ExerciseGenerator(200);
            foreach(var category in Enum.GetValues<Category>().Where(c=>c>Category.Notes&&c<=Category.RhythmReproduction))foreach(var level in Enum.GetValues<Level>())for(var i=0;i<60;i++)
            {
                var settings=new Settings(category,level,MinNote:21,MaxNote:108,KeyTonic:i%12,MeterId:TheoryV2.Meters[i%TheoryV2.Meters.Length].Id,Bars:1+i%3);
                var e=g.Next(settings);Theory.VerifyExercise(e);Check(e.Score is not null);Check(e.Notes.All(n=>n>=21&&n<=108));
                var events=Schedule.Create(e,settings);Check(events.Count(n=>n.Action==MidiAction.NoteOn)==e.Score!.Events.Length);
                var session=new Session();session.Begin(e);
                if(e.AnswerKind==AnswerKind.Choice)Check(session.Answer(e.CorrectId,level,settings).Correct);
                else if(e.AnswerKind==AnswerKind.Text)Check(session.AnswerResponse(e.Score.ExpectedText!,settings).Correct);
            }
        });
        test("0.2: perfect/imperfect authentic, half, plagal and deceptive harmonic endpoints",()=>{
            foreach(var mode in Enum.GetValues<TonalityMode>())foreach(var tonic in Enumerable.Range(48,12))foreach(var cadence in TheoryV2.Cadences)
            {
                var s=TheoryV2.CadenceScore(cadence.Id,tonic,mode);
                var first=s.Events.Where(n=>n.Beat==4).Select(n=>n.Midi-tonic).ToArray();var last=s.Events.Where(n=>n.Beat==6).Select(n=>n.Midi-tonic).ToArray();
                var minor=mode==TonalityMode.NaturalMinor;
                switch(cadence.Id){
                    case "pac":Check(first[0]%12 is -5 or 7);Check(last[0]==0&&last[^1]%12==0);Check(first[^1]==11&&last[^1]==12);break;
                    case "iac":Check(first[0]==-5&&last[0]==0&&last[^1]==19);Check(first[2]==11&&last[2]==12);break;
                    case "half":Check(first[0]==0&&last[0]==-5);Check(last.Contains(11));break;
                    case "plagal":Check(first[0]==-7&&last[0]==0);Check(first.Contains(minor?8:9));break;
                    case "deceptive":Check(first[0]==-5&&last[0]==(minor?-4:-3));Check(last[^1]==12);break;
                }
                // No parallel fifths/octaves in the two assessed SATB chords.
                for(var a=0;a<4;a++)for(var b=a+1;b<4;b++)
                {
                    var p=(first[b]-first[a])%12;var q=(last[b]-last[a])%12;
                    var sameDirection=Math.Sign(last[a]-first[a])==Math.Sign(last[b]-first[b])&&last[a]!=first[a]&&last[b]!=first[b];
                    Check(!(sameDirection&&p==q&&p is 0 or 7),"Parallel perfect interval in "+cadence.Id);
                }
            }
        });
        test("0.2: independent scale/mode reference steps and classical melodic-minor descent",()=>{
            var references=new Dictionary<string,int[]>{["dorian"]=[0,2,3,5,7,9,10,12],["phrygian"]=[0,1,3,5,7,8,10,12],["lydian"]=[0,2,4,6,7,9,11,12],["mixolydian"]=[0,2,4,5,7,9,10,12],["locrian"]=[0,1,3,5,6,8,10,12]};
            foreach(var (id,steps)in references)Check(TheoryV2.Modes.Single(m=>m.Id==id).Up.SequenceEqual(steps));
            var melodic=TheoryV2.Scales.Single(m=>m.Id=="melodic-minor");Check(melodic.Up.SequenceEqual(new[]{0,2,3,5,7,9,11,12}));Check(melodic.Down!.SequenceEqual(new[]{12,10,8,7,5,3,2,0}));
            Check(TheoryV2.Scales.Single(s=>s.Id=="chromatic").Up.Zip(Enumerable.Range(0,13)).All(p=>p.First==p.Second));
            foreach(var d in TheoryV2.Scales.Concat(TheoryV2.Modes))foreach(var direction in new[]{Direction.Ascending,Direction.Descending})
            {var score=TheoryV2.ScaleScore(d,60,direction);Check(score.Events.First().Midi==score.Events.Last().Midi);Check(score.Events.Max(n=>n.Midi)-score.Events.Min(n=>n.Midi)==12);}
        });
        test("0.2: meter grouping, compound/accent grids and exact fractional event timing",()=>{
            foreach(var meter in TheoryV2.Meters)
            {
                Check(meter.Groups.Sum()==meter.Numerator);var score=TheoryV2.MeterScore(meter,3,60);Check(score.Events.Length==3*meter.Numerator);
                for(var bar=0;bar<3;bar++)Check(score.Events[bar*meter.Numerator].Beat==bar*meter.QuarterBeats&&score.Events[bar*meter.Numerator].Velocity==120);
            }
            var six=TheoryV2.MeterScore(TheoryV2.Meters.Single(m=>m.Id=="6/8"),2,60);Check(six.Events[3].Beat==1.5&&six.Events[3].Velocity==95);Check(six.Events[1].Velocity==50);
        });
        test("0.2: rhythm notation parses ties/rests/dots and validates every bar",()=>{
            var m=TheoryV2.Meters.Single(m=>m.Id=="4/4");
            var a=RhythmCodec.Parse("1/4. 1/8 1/2",m,1);var b=RhythmCodec.Parse("1/4+1/8 1/8 1/2",m,1);Check(RhythmCodec.Canonical(a)==RhythmCodec.Canonical(b));
            Check(RhythmCodec.Parse("r1/4 1/4 1/2",m,1)[0][0].Rest);
            Throws<ArgumentException>(()=>RhythmCodec.Parse("1/4 1/4",m,1));Throws<ArgumentException>(()=>RhythmCodec.Parse("unknown",m,1));
            foreach(var r in TheoryV2.Rhythms)Check(RhythmCodec.Parse(r.Tokens,m,1)[0].Sum(c=>c.Ticks)==16);
        });
        test("0.2: written dictation grading preserves retry on invalid syntax and scores wrong rhythms",()=>{
            var settings=new Settings(Category.RhythmDictation,Level.Advanced,Bars:1);var e=new ExerciseGenerator(20).Next(settings);var session=new Session();session.Begin(e);
            Throws<ArgumentException>(()=>session.AnswerResponse("bad",settings));Check(session.AnswerResponse(e.Score!.ExpectedText!,settings).Correct);
            Throws<InvalidOperationException>(()=>session.AnswerResponse(e.Score!.ExpectedText!,settings));
            var wrong=new[]{"1/1","1/2 1/2","1/4 1/4 1/4 1/4"}.First(s=>RhythmCodec.Canonical(RhythmCodec.Parse(s,TheoryV2.Meters[2],1))!=RhythmCodec.Canonical(RhythmCodec.Parse(e.Score!.ExpectedText!,TheoryV2.Meters[2],1)));
            session.Begin(e);Check(!session.AnswerResponse(wrong,settings).Correct);
        });
        test("0.2: keyboard taps score exact tempo, bad timing/count and final boundary",()=>{
            var settings=new Settings(Category.RhythmReproduction,Level.Professional,Tempo:120,Bars:1);var e=new ExerciseGenerator(3).Next(settings);var score=e.Score!;var bar=TheoryV2.Meters.Single(m=>m.Id==score.MeterId).QuarterBeats;
            var times=score.Events.Where(n=>n.Voice==0).Select(n=>(n.Beat-bar)*0.5).Append((score.LengthBeats-bar)*0.5).ToArray();
            string Text(IEnumerable<double> values)=>string.Join(' ',values.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            Check(WrittenGrader.Grade(e,Text(times),settings).Percentage==100);
            Check(!WrittenGrader.Grade(e,Text(times.Select(t=>t*2)),settings).Correct);
            Check(!WrittenGrader.Grade(e,Text(times.SkipLast(1)),settings).Correct);
        });
        test("0.2: new score/response/history snapshots survive atomic save and restore",()=>{
            var dir=Path.Combine(Path.GetTempPath(),"studio-v2-"+Guid.NewGuid());try{
                var s=new Settings(Category.RhythmDictation,Level.Expert,MeterId:"7/8:2+2+3",Bars:2);var e=new ExerciseGenerator(2).Next(s);var session=new Session();session.Begin(e);var a=session.AnswerResponse(e.Score!.ExpectedText!,s);
                var store=new ProgressStore(dir);store.Save([a]);var loaded=store.Load().Single();Check(loaded.Correct&&loaded.Snapshot?.Score?.MeterId==s.MeterId);Theory.VerifyExercise(loaded.Snapshot!);Check(loaded.Configuration==s);Check(loaded.Percentage==100);
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        });
        test("0.2: corrupt timings and resource-overlong examples are rejected",()=>{
            var s=new Settings(Category.Scales,Level.Expert);var e=new ExerciseGenerator(8).Next(s);var bad=e.Score!.Events.ToArray();bad[0]=bad[0]with{Duration=-1};Throws<ArgumentException>(()=>Theory.VerifyExercise(e with{Score=e.Score with{Events=bad}}));
            var longSettings=new Settings(Category.Meters,Level.Professional,Tempo:30,Repetitions:5,Bars:8);var longExercise=TheoryV2.Generate(longSettings,new Random(9));
            if(longExercise.Score!.LengthBeats*5*2>160)Throws<ArgumentException>(()=>Schedule.Create(longExercise,longSettings));
        });
        var index=Array.IndexOf(args,"--maestro");
        if(index>=0)test("0.2: real Maestro rendering of every new category and written-answer roundtrip",()=>{
            var renderer=new PianoRenderer(args[index+1]);var g=new ExerciseGenerator(260);
            foreach(var c in Enum.GetValues<Category>().Where(c=>c>Category.Notes&&c<=Category.RhythmReproduction)){
                var s=new Settings(c,Level.Advanced,Tempo:120,Bars:1);var e=g.Next(s);var pcm=renderer.Render(e,s,renderer.Presets[0]);Check(pcm.Length>44100);Check(pcm.Any(v=>Math.Abs(v)>0.001f));Check(pcm.All(float.IsFinite));Check(pcm.Max(v=>Math.Abs(v))<=0.901f);
            }
        });
    }
}
