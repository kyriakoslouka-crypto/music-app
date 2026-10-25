using Studio.Core;
using Studio.Audio;
namespace Studio.Tests;
public static class RhythmInputTests
{
    private static void Check(bool condition,string message="Assertion failed"){if(!condition)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Invalid rhythmic input accepted");}
    public static void Register(Action<string,Action> test,string[] args)
    {
        var meter=TheoryV2.Meters.Single(m=>m.Id=="4/4");
        test("Rhythm input: 8/32 and 16/64 retain every attack instead of merging to quarters",()=>{
            var parsed=RhythmCodec.Parse("8/32 16/64 2/4",meter,1)[0];
            Check(parsed.Length==26);Check(parsed.Take(8).All(c=>c.Ticks==0.5));Check(parsed.Skip(8).Take(16).All(c=>c.Ticks==0.25));Check(parsed.Skip(24).All(c=>c.Ticks==4));Check(parsed.Sum(c=>c.Ticks)==16);
            var single=RhythmCodec.Parse("4/4",meter,1)[0];Check(single.Length==4&&single.All(c=>c.Ticks==4));
            Check(RhythmCodec.Parse("1/1",meter,1)[0].Length==1);
        });
        test("Rhythm input: Greek groups, repeated syllable, commas, accents and rests are accepted",()=>{
            var parsed=RhythmCodec.Parse("μίφά2/8, φασολλασι4/16; ντο2/4",meter,1)[0];
            Check(parsed.Select(c=>c.Ticks).SequenceEqual(new[]{2.0,2,1,1,1,1,4,4}));
            Check(RhythmCodec.Parse("π2/8 r2/8 παύση1/2",meter,1)[0].All(c=>c.Rest));
            Reject(()=>RhythmCodec.Parse("μιφασολ2/8 3/4",meter,1));
        });
        test("Rhythm input: tied values differ from repeated attacks and compact formatting round-trips",()=>{
            var grouped=RhythmCodec.Parse("2/4 1/2",meter,1);var tied=RhythmCodec.Parse("1/4+1/4 1/2",meter,1);
            Check(grouped[0].Length==3&&tied[0].Length==2);Check(RhythmCodec.Canonical(grouped)!=RhythmCodec.Canonical(tied));
            var cells=RhythmCodec.Parse("8/32 16/64 2/4",meter,1);Check(RhythmCodec.Format(cells)=="8/32 16/64 2/4");
            Check(RhythmCodec.Canonical(RhythmCodec.Parse(RhythmCodec.Format(tied),meter,1))==RhythmCodec.Canonical(tied));
            var dotted=RhythmCodec.Parse("2/8. 1/8 2/4",meter,1);Check(dotted[0].Take(2).All(c=>c.Ticks==3));
        });
        test("Rhythm input: invalid counts, unsupported values and meter mistakes fail with bounded parsing",()=>{
            foreach(var token in new[]{"0/32","-8/32","999/64","8/0","8/3","8/128","πντο1/4","8/32banana",","})Reject(()=>RhythmCodec.Parse(token,meter,1));
            Reject(()=>RhythmCodec.Parse("8/32",meter,1));Reject(()=>RhythmCodec.Parse("4/4",meter,2));
        });
        test("Rhythm input: compact answers grade correctly and wrong subdivisions stay wrong",()=>{
            var score=TheoryV2.RhythmScore("8/32 16/64 2/4","4/4",1,60);
            var e=new Exercise("compact-rhythm",Category.RhythmDictation,score.Events.Select(n=>n.Midi).ToArray(),"dictation","8/32 16/64 2/4",[],Direction.Ascending,score,AnswerKind.Text);
            Check(WrittenGrader.Grade(e,"8/32 16/64 2/4",new(Category.RhythmDictation)).Correct);
            Check(!WrittenGrader.Grade(e,"8/16 2/4",new(Category.RhythmDictation)).Correct);
            Check(score.Events.Count(n=>n.Voice==0)==26);var events=Schedule.Create(e,new(Category.RhythmDictation,Tempo:60,Pedal:true));
            var onsets=events.Where(n=>n.Action==MidiAction.NoteOn&&n.Channel==0).ToArray();Check(onsets.Length==26);
            Check(Math.Abs(onsets[1].Frame-onsets[0].Frame-44100.0/8)<=1);Check(Math.Abs(onsets[9].Frame-onsets[8].Frame-44100.0/16)<=1);
            Check(!events.Any(n=>n.Action==MidiAction.Pedal&&n.Value>=64));
        });
        test("Rhythm input: advanced generation includes 32nds/64ths and every generated bar remains exact",()=>{
            var generator=new ExerciseGenerator(530);var found32=false;var found64=false;
            for(var i=0;i<150;i++){
                var s=new Settings(Category.RhythmDictation,Level.Professional,MeterId:TheoryV2.Meters[i%TheoryV2.Meters.Length].Id,Bars:2);
                var e=generator.Next(s);Theory.VerifyExercise(e);var bars=RhythmCodec.Parse(e.Score!.ExpectedText!,TheoryV2.Meters.Single(m=>m.Id==s.MeterId),2);
                foreach(var bar in bars){Check(bar.Sum(c=>c.Ticks)==TheoryV2.Meters.Single(m=>m.Id==s.MeterId).Ticks);found32|=bar.Any(c=>c.Ticks==0.5);found64|=bar.Any(c=>c.Ticks==0.25);}
            }
            Check(found32&&found64);
            foreach(var id in new[]{"thirty-seconds","sixty-fourths"}){
                var r=TheoryV2.Rhythms.Single(r=>r.Id==id);var score=TheoryV2.RhythmScore(r.Tokens,"4/4",1,60,id);
                Check(!Schedule.CreateScore(score,new(Pedal:true)).Any(e=>e.Action==MidiAction.Pedal&&e.Value>=64));
            }
        });
        test("Rhythm input: tapping 64ths at double their duration cannot receive a perfect score",()=>{
            var score=TheoryV2.RhythmScore("64/64","4/4",1,60);
            var e=new Exercise("fine-tapping",Category.RhythmReproduction,score.Events.Select(n=>n.Midi).ToArray(),"dictation","64/64",[],Direction.Ascending,score,AnswerKind.Taps);
            var s=new Settings(Category.RhythmReproduction,Level.Professional,Tempo:60);
            var correct=string.Join(' ',Enumerable.Range(0,65).Select(i=>(i/16.0).ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            var doubled=string.Join(' ',Enumerable.Range(0,65).Select(i=>(i/8.0).ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            Check(WrittenGrader.Grade(e,correct,s).Correct);Check(WrittenGrader.Grade(e,doubled,s).Percentage==0);
        });
        test("Rhythm input: compact melodic notes preserve pitches and fine durations without breaking explicit octaves",()=>{
            var notes=NoteCodec.Parse("μιφα2/8 φασολλασι4/16 ντο2/4",true);
            Check(notes.Select(n=>n.Midi).SequenceEqual(new[]{64,65,65,67,69,71,60,60}));Check(notes.Select(n=>n.Ticks).SequenceEqual(new[]{2.0,2,1,1,1,1,4,4}));
            var fine=NoteCodec.Parse("Ντο5:1/32 Ρε5:1/64",true);Check(fine[0]==new NotatedNote(72,0.5)&&fine[1]==new NotatedNote(74,0.25));
            Check(NoteCodec.Parse(NoteCodec.Format(fine,true),true).SequenceEqual(fine));Reject(()=>NoteCodec.Parse("8/32",true));
        });
        test("Rhythm input: quarters and eighths and octave-free melody grade across registers",()=>{
            Check(NoteCodec.Parse("ντορε2/4 μιφα2/8",true).Select(n=>n.Ticks).SequenceEqual(new[]{4.0,4,2,2}));
            var expected="Ντο5:1/4 Ρε3:1/4 Μι6:1/8 Φα2:1/8";
            var score=TheoryV3.MelodyScore(expected,60,TonalityMode.Major,"melody",true);
            var e=new Exercise("octave-free",Category.MelodyDictation,score.Events.Select(n=>n.Midi).ToArray(),"melody",expected,[],Direction.Ascending,score,AnswerKind.Text);
            Check(TheoryV3.Grade(e,"ντορε2/4 μιφα2/8",new(Category.MelodyDictation)).Correct);
            Check(!TheoryV3.Grade(e,"ντομι2/4 μιφα2/8",new(Category.MelodyDictation)).Correct);
            Check(!TheoryV3.Grade(e,"ντορε2/8 μιφα2/8",new(Category.MelodyDictation)).Correct);
            Check(!TheoryV3.Grade(e,"Ντο4:1/4 Ρε3:1/4 Μι6:1/8 Φα2:1/8",new(Category.MelodyDictation)).Correct);
            Check(NoteCodec.ResolveAnswer("ντο ρε μι",false,"Ντο5 Ρε3 Μι6").Select(n=>n.Midi).SequenceEqual(new[]{72,50,88}));
            Check(TheoryV4.Grade(e with{Category=Category.Polyphony2},"ντορε2/4 μιφα2/8",new(Category.Polyphony2)).Correct);
            Check(!TheoryV4.Grade(e with{Category=Category.Polyphony2},"ντομι2/4 μιφα2/8",new(Category.Polyphony2)).Correct);
            Check(NoteCodec.ResolveAnswer("ντο# ρεb",false,"Ντο#5 Ρεb3").Select(n=>n.Midi).SequenceEqual(new[]{73,49}));
            Check(NoteCodec.ResolveAnswer("ντο:1/4 Ρε3:1/8",true,"Ντο5:1/4 Ρε3:1/8").Select(n=>n.Midi).SequenceEqual(new[]{72,50}));
            Check(TheoryV3.Preview(e,"ντορε2/4 μιφα2/8").Events.Where(n=>n.Voice>=0).Select(n=>n.Midi).SequenceEqual(new[]{72,50,88,41}));
        });
        test("Rhythm input: each named subdivision has independently verified attacks, duration and grading",()=>{
            foreach(var denominator in new[]{4,8,16,32,64}){
                var text=$"{denominator}/{denominator}";
                var cells=RhythmCodec.Parse(text,meter,1)[0];
                Check(cells.Length==denominator&&cells.All(c=>c.Ticks==16.0/denominator));
                var score=TheoryV2.RhythmScore(text,"4/4",1,60);
                var e=new Exercise("all-values",Category.RhythmDictation,score.Events.Select(n=>n.Midi).ToArray(),"dictation",text,[],Direction.Ascending,score,AnswerKind.Text);
                Check(WrittenGrader.Grade(e,string.Join(' ',Enumerable.Repeat($"ντο1/{denominator}",denominator)),new(Category.RhythmDictation)).Correct);
                var onsets=Schedule.Create(e,new(Category.RhythmDictation,Tempo:60,Pedal:true)).Where(n=>n.Action==MidiAction.NoteOn&&n.Channel==0).ToArray();
                Check(onsets.Length==denominator);
                for(var i=1;i<onsets.Length;i++)Check(Math.Abs(onsets[i].Frame-onsets[i-1].Frame-44100.0*4/denominator)<=1);
            }
        });
        test("Cadences: no authentic context before plagal and independent final-chord classification",()=>{
            foreach(var mode in Enum.GetValues<TonalityMode>())foreach(var tonic in Enumerable.Range(48,12))foreach(var id in new[]{"pac","iac","half","plagal","deceptive"}){
                var score=TheoryV2.CadenceScore(id,tonic,mode);
                Check(score.Events.Where(n=>n.Beat<4).All(n=>n.Beat==0&&n.Duration==1.6));
                int[] PitchClasses(double beat)=>score.Events.Where(n=>n.Beat==beat).Select(n=>(n.Midi-tonic+120)%12).Distinct().Order().ToArray();
                var minor=mode==TonalityMode.NaturalMinor;
                var first=PitchClasses(4);var last=PitchClasses(6);
                if(id=="plagal"){Check(first.SequenceEqual(minor?new[]{0,5,8}:new[]{0,5,9}));Check(!first.Contains(11));}
                if(id is "pac" or "iac" or "deceptive")Check(first.SequenceEqual(new[]{2,7,11}));
                if(id is "pac" or "iac" or "plagal")Check(last.SequenceEqual(minor?new[]{0,3,7}:new[]{0,4,7}));
                if(id=="half")Check(last.SequenceEqual(new[]{2,7,11}));
                var e=new Exercise("cadence-guard",Category.Cadences,score.Events.Select(n=>n.Midi).ToArray(),id,id,TheoryV2.Cadences,Direction.Harmonic,score);
                Theory.VerifyExercise(e);
                var legacy=TheoryV2.CadenceScore(id,tonic,mode,true);Theory.VerifyExercise(e with{Score=legacy,Notes=legacy.Events.Select(n=>n.Midi).ToArray()});
                if(id=="plagal")Reject(()=>Theory.VerifyExercise(e with{CorrectId="pac"}));
                var scheduled=Schedule.Create(e,new(Category.Cadences,Tempo:60,Pedal:true));
                Check(scheduled.Any(n=>n.Action==MidiAction.Pedal&&n.Value==0&&Math.Abs(n.Frame-2.0*44100)<=1));
                Check(scheduled.Where(n=>n.Action==MidiAction.NoteOn&&n.Frame>2*44100&&n.Frame<4*44100).Count()==0);
            }
        });
        test("Rhythm input: natural spaces and adjacent note values accept all five subdivisions",()=>{
            foreach(var d in new[]{4,8,16,32,64}){
                var simple=$"ντο1/{d} ρε1/{d}";var expected=NoteCodec.Parse(simple,true);
                foreach(var input in new[]{$"ντο 1/{d} ρε 1/{d}",$"ντο1 / {d}ρε1 / {d}",$"ντο1\\{d}ρε1⁄{d}"})
                    Check(NoteCodec.Parse(input,true).SequenceEqual(expected),input);
                var rhythm=string.Join(' ',Enumerable.Repeat($"ντο 1 / {d}",d));
                Check(RhythmCodec.Canonical(RhythmCodec.Parse(rhythm,meter,1))==RhythmCodec.Canonical(RhythmCodec.Parse($"{d}/{d}",meter,1)));
            }
            Check(NoteCodec.Parse("ντο ρε 2 / 4 μι φα 2 / 8",true).Select(n=>n.Midi).SequenceEqual(new[]{60,62,64,65}));
            Check(NoteCodec.Parse("ντο¼ρε⅛",true).Select(n=>n.Ticks).SequenceEqual(new[]{4.0,2}));
            Check(NoteCodec.Parse("Ντο5 : 1 / 4 Ρε3 : 1 / 8",true).Select(n=>n.Midi).SequenceEqual(new[]{72,50}));
            Check(NoteCodec.Parse("ντο ρε μι",false).Length==3);
            try{RhythmInput.ParseGroup("ντο1/128");throw new Exception("Invalid value accepted");}catch(ArgumentException e){Check(e.Message.Contains("ντο1/128",StringComparison.Ordinal));}
        });
        var index=Array.IndexOf(args,"--maestro");if(index>=0)test("Rhythm input: actual Maestro renders 32nd/64th dictation and compact melody previews",()=>{
            var renderer=new PianoRenderer(args[index+1]);var score=TheoryV2.RhythmScore("8/32 16/64 2/4","4/4",1,60);
            var e=new Exercise("fine-rhythm-audio",Category.RhythmDictation,score.Events.Select(n=>n.Midi).ToArray(),"dictation","8/32 16/64 2/4",[],Direction.Ascending,score,AnswerKind.Text);
            var pcm=renderer.Render(e,new(Category.RhythmDictation,Tempo:60),renderer.Presets[0]);Check(pcm.All(float.IsFinite)&&pcm.Any(v=>Math.Abs(v)>0.001f));
            var melody=TheoryV3.MelodyScore("ντο8/32 ρε16/64",60,TonalityMode.Major,"melody",true);
            var preview=renderer.RenderPreview(melody,new(Tempo:60),renderer.Presets[0]);Check(preview.All(float.IsFinite)&&preview.Any(v=>Math.Abs(v)>0.001f));
        });
    }
}
