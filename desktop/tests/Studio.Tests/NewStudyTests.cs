using System.Text.Json;
using Studio.Core;
using Studio.Audio;
namespace Studio.Tests;
public static class NewStudyTests
{
    private static void Check(bool ok,string message="New study regression"){if(!ok)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Invalid exam accepted");}
    public static void Register(Action<string,Action> test,string[] args)
    {
        test("Study features: notation builds all 28 categories and six levels without changing audio data",()=>{
            var g=new ExerciseGenerator(580);
            foreach(var c in Enum.GetValues<Category>())foreach(var level in Enum.GetValues<Level>()){
                var s=new Settings(c,level,MinNote:21,MaxNote:108);var e=g.Next(s);var original=JsonSerializer.Serialize(e);var notation=ScoreNotation.Build(e);
                Check(notation.Symbols.Length>0&&notation.Symbols.All(n=>n.Duration>0&&n.Beat>=0&&n.Staff>=0&&n.Staff<notation.Staves&&n.Beat+n.Duration<=notation.Length+0.000001),c+" "+level);
                Check(notation.Symbols.Where(n=>n.Midi.HasValue).All(n=>e.Notes.Contains(n.Midi!.Value)));Check(original==JsonSerializer.Serialize(e));
            }
        });
        test("Study features: written rhythm values, rests, dots and 32nd/64th durations are exact",()=>{
            var score=TheoryV2.RhythmScore("π1/4 1/8. 1/16 1/2","4/4",1,60);
            var e=new Exercise("notation-rhythm",Category.RhythmDictation,score.Events.Select(n=>n.Midi).ToArray(),"dictation","ρυθμός",[],Direction.Ascending,score,AnswerKind.Text);
            var n=ScoreNotation.Build(e);Check(n.Symbols.Where(n=>n.Staff==0).Sum(n=>n.Duration)==4&&n.Symbols.First(n=>n.Staff==0).Midi is null&&n.Symbols.Any(n=>n.Staff==0&&n.Denominator==8&&n.Dots==1));
            var melody=TheoryV3.MelodyScore("Ντο4:1/4 Ρε4:1/8 Μι4:1/16 Φα4:1/32 Σολ4:1/64",60,TonalityMode.Major,"melody",true);
            n=ScoreNotation.Build(e with{Category=Category.MelodyDictation,Score=melody,Notes=melody.Events.Select(n=>n.Midi).ToArray(),CorrectId="melody"});
            Check(n.Symbols.Where(n=>n.Midi.HasValue).Select(n=>n.Denominator).SequenceEqual(new[]{4,8,16,32,64}));Check(n.Symbols.Where(n=>n.Midi.HasValue).Select(n=>n.Midi).SequenceEqual(new int?[]{60,62,64,65,67}));
        });
        test("Study features: sustained notes split across bars with ties instead of repeated attacks",()=>{
            var score=TheoryV2.RhythmScore("1/1 | 1/1","4/4",2,60);var e=new Exercise("ties",Category.RhythmDictation,score.Events.Select(n=>n.Midi).ToArray(),"dictation","ρυθμός",[],Direction.Ascending,score,AnswerKind.Text);
            var n=ScoreNotation.Build(e);Check(n.Symbols.Count(n=>n.Midi.HasValue)==2&&n.Symbols.All(n=>!n.TieTo&&!n.TieFrom));
            var melody=TheoryV3.MelodyScore("Ντο4:1/1+1/1",60,TonalityMode.Major,"melody",true);
            n=ScoreNotation.Build(e with{Category=Category.MelodyDictation,Score=melody,Notes=melody.Events.Select(n=>n.Midi).ToArray(),CorrectId="melody"});
            var notes=n.Symbols.Where(n=>n.Midi.HasValue).ToArray();Check(notes.Length==2&&notes[0].TieTo&&notes[1].TieFrom&&notes.Sum(n=>n.Duration)==8);
        });
        test("Study features: notation privacy blocks unanswered, active and expired-but-unfinished exams",()=>{
            var now=DateTimeOffset.UtcNow;var engine=ExamEngine.Create(ExamCatalog.Profiles[0] with{QuestionCount=1},581,1,()=>now);
            Check(!ScoreNotation.CanReveal(false,null)&&ScoreNotation.CanReveal(true,null));Check(!ScoreNotation.CanReveal(true,engine.State));
            now=now.AddMinutes(2);Check(engine.Expired&&!ScoreNotation.CanReveal(true,engine.State));engine.Finish();Check(ScoreNotation.CanReveal(false,engine.State));
        });
        test("Study features: surprise exams randomize selected categories and preserve balanced levels",()=>{
            Category[] categories=[Category.Intervals,Category.Cadences,Category.MelodyDictation];
            var a=SurpriseExams.Create(categories,Level.Expert,12,2,0,582);var b=SurpriseExams.Create(categories,Level.Expert,12,2,0,582);var c=SurpriseExams.Create(categories,Level.Expert,12,2,0,583);
            Check(categories.SequenceEqual(new[]{Category.Intervals,Category.Cadences,Category.MelodyDictation}));Check(a.State.Questions.All(q=>categories.Contains(q.Exercise.Category)&&q.Settings.Level==Level.Expert));
            Check(a.State.Questions.GroupBy(q=>q.Exercise.Category).All(g=>g.Count()==4));
            string Shape(ExamEngine e)=>string.Join(';',e.State.Questions.Select(q=>q.Exercise.Category+":"+string.Join(',',q.Exercise.Notes)));
            Check(Shape(a)==Shape(b)&&Shape(a)!=Shape(c));Reject(()=>SurpriseExams.Create([],Level.Beginner,10,3,0,1));Reject(()=>SurpriseExams.Create(categories,Level.Beginner,101,3,0,1));Reject(()=>SurpriseExams.Create(categories,Level.Beginner,10,0,0,1));
        });
        test("Study features: surprise exam resume, hidden solutions, scoring and timeout use real exam engine",()=>{
            var exam=SurpriseExams.Create([Category.Intervals,Category.Cadences],Level.Intermediate,4,1,0,584);
            Check(!exam.Report(true).Contains("Σωστό:",StringComparison.Ordinal));exam.RegisterSuccessfulPlay();exam.Submit(exam.Current.Exercise.CorrectId);exam.Advance();
            exam=ExamEngine.Restore(JsonSerializer.Deserialize<ExamState>(JsonSerializer.Serialize(exam.State))!);
            while(exam.Running){exam.RegisterSuccessfulPlay();exam.Submit(exam.Current.Exercise.CorrectId);exam.Advance();}
            Check(exam.Summary().Percentage==100&&exam.Report(true).Contains("Σωστό:",StringComparison.Ordinal)&&ScoreNotation.CanReveal(false,exam.State));
        });
        test("Study features: coach separates levels, recognizes partial rhythm scores and avoids claims from tiny samples",()=>{
            var s=new Settings(Category.Intervals);var e=new ExerciseGenerator(585).Next(s);var session=new Session();session.Begin(e);var a=session.Answer(e.CorrectId,s.Level,s);
            Check(StudyCoach.Recommend([a,a]).Length==0);Check(StudyCoach.Recommend([a,a,a]).Length==0);
            var partial=a with{Category=Category.RhythmReproduction,Level=Level.Expert,Correct=true,Percentage=82,ExpectedName="παύσεις"};
            var rows=new[]{partial,partial,partial,a,a,a};var r=StudyCoach.Recommend(rows).Single();Check(r.Category==Category.RhythmReproduction&&r.Level==Level.Expert&&r.Percentage==82&&r.Focus=="παύσεις");
            Check(StudyCoach.Recommend([a with{Correct=false,Level=Level.Advanced},a with{Correct=false,Level=Level.Expert},a with{Correct=false,Level=Level.Professional}]).Length==0);
            var target=StudyCoach.Target(rows,s);Check(target.Category==Category.RhythmReproduction&&target.Level==Level.Expert&&target.SoloVoice==-1&&target.SegmentEnd is null);
        });
        test("Study features: coach uses recent chronological evidence and improves after new correct answers",()=>{
            var s=new Settings();var e=new ExerciseGenerator(586).Next(s);var session=new Session();session.Begin(e);var a=session.Answer(e.CorrectId,s.Level,s);var start=DateTimeOffset.UtcNow.AddDays(-2);
            var rows=Enumerable.Range(0,103).Select(i=>a with{At=start.AddMinutes(i),Correct=i>=3,Percentage=i>=3?100:0}).Reverse().ToArray();
            Check(StudyCoach.Recommend(rows).Length==0);
        });
        test("Study features: notation covers all 88 keys and polyphonic voices retain their exact pitches",()=>{
            for(var midi=21;midi<=108;midi++){
                var e=new Exercise("key",Category.Notes,[midi],"note:"+midi,Theory.NoteName(midi),[new("note:"+midi,Theory.NoteName(midi))],Direction.Ascending);
                Check(ScoreNotation.Build(e).Symbols.Single(n=>n.Midi.HasValue).Midi==midi);
            }
            var poly=new ExerciseGenerator(587).Next(new(Category.Polyphony4,Level.Professional,MinNote:21,MaxNote:108));var n=ScoreNotation.Build(poly);Check(n.Staves==4&&n.Symbols.Select(s=>s.Staff).Distinct().Count()==4);
        });
        var index=Array.IndexOf(args,"--maestro");if(index>=0)test("Study features: real surprise questions and score conversion preserve finite nonzero Maestro playback",()=>{
            var renderer=new PianoRenderer(args[index+1]);var exam=SurpriseExams.Create([Category.Cadences,Category.RhythmDictation,Category.MelodyDictation],Level.Professional,3,2,0,588);
            foreach(var q in exam.State.Questions){var before=JsonSerializer.Serialize(q.Exercise);_ = ScoreNotation.Build(q.Exercise);Check(before==JsonSerializer.Serialize(q.Exercise));var pcm=renderer.Render(q.Exercise,q.Settings,renderer.Presets[0]);Check(pcm.All(float.IsFinite)&&pcm.Any(n=>Math.Abs(n)>0.001f));}
        });
    }
}
