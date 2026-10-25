using Studio.Core;
using Studio.Audio;
namespace Studio.Tests;
public static class Version5Tests
{
    private static void Check(bool c,string message="Assertion failed"){if(!c)throw new Exception(message);}
    private static void Throws<T>(Action action)where T:Exception{try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
    private static string CorrectResponse(ExamQuestion q)=>q.Exercise.AnswerKind==AnswerKind.Choice?q.Exercise.CorrectId:q.Exercise.Score!.ExpectedText!;
    public static void Register(Action<string,Action> test,string[] args)
    {
        test("0.5: all ten independent exam profiles generate balanced valid question sets",()=>{
            Check(ExamCatalog.Profiles.Length==10);Check(ExamCatalog.Profiles.Select(p=>p.Id).Distinct().Count()==10);
            foreach(var profile in ExamCatalog.Profiles)
            {
                var exam=ExamEngine.Create(profile,500);Check(exam.State.Questions.Length==profile.QuestionCount);Check(exam.State.Questions.Select(q=>q.Exercise.Id).Distinct().Count()==profile.QuestionCount);
                foreach(var q in exam.State.Questions){Theory.VerifyExercise(q.Exercise);Theory.Validate(q.Settings);Check(profile.Categories.Contains(q.Exercise.Category));}
                foreach(var category in profile.Categories)Check(exam.State.Questions.Any(q=>q.Exercise.Category==category));
                if(profile.Id.StartsWith("abrsm"))Check(profile.Disclosure.Contains("Δεν είναι επίσημη"));
            }
        });
        test("0.5: exam play limits, failed-output refund, duplicate grading and completion",()=>{
            var exam=ExamEngine.Create(ExamCatalog.Profiles[0],2);
            Throws<InvalidOperationException>(()=>exam.Submit(CorrectResponse(exam.Current)));Throws<InvalidOperationException>(()=>exam.Advance());
            exam.RegisterSuccessfulPlay();exam.RefundFailedPlay();Check(exam.PlaysRemaining==3);
            for(var i=0;i<3;i++)exam.RegisterSuccessfulPlay();Throws<InvalidOperationException>(()=>exam.RegisterSuccessfulPlay());
            Check(exam.Submit(CorrectResponse(exam.Current)).Correct);Throws<InvalidOperationException>(()=>exam.Submit(CorrectResponse(exam.Current)));exam.Advance();
            while(exam.Running){exam.RegisterSuccessfulPlay();exam.Submit(CorrectResponse(exam.Current));exam.Advance();}
            Check(exam.Summary().Percentage==100&&exam.Summary().Passed&&exam.State.Finished is not null);Throws<InvalidOperationException>(()=>exam.RegisterSuccessfulPlay());
        });
        test("0.5: unanswered/skipped questions remain zero and cannot inflate the percentage",()=>{
            var exam=ExamEngine.Create(ExamCatalog.Profiles[0],1);exam.RegisterSuccessfulPlay();exam.Submit(CorrectResponse(exam.Current));exam.Advance();exam.Skip();exam.Finish();
            var s=exam.Summary();Check(s.Answered==1&&s.Skipped==1&&s.Unanswered==10);Check(Math.Abs(s.Percentage-100.0/12)<0.0001&&!s.Passed);
        });
        test("0.5: simulated time limit survives resume and rejects late submissions",()=>{
            var time=new DateTimeOffset(2026,10,24,0,0,0,TimeSpan.Zero);var exam=ExamEngine.Create(ExamCatalog.Profiles[0],3,1,()=>time);exam.RegisterSuccessfulPlay();time=time.AddSeconds(61);
            Check(exam.Expired&&!exam.Running);Throws<InvalidOperationException>(()=>exam.Submit(CorrectResponse(exam.Current)));var resumed=ExamEngine.Restore(exam.State,()=>time);Check(resumed.Expired);resumed.Finish();Check(resumed.State.FinishReason=="time-limit");
        });
        test("0.5: exact exam progress/play counters/current index and completed history restore",()=>{
            var dir=Path.Combine(Path.GetTempPath(),"studio-exam-"+Guid.NewGuid());try{
                var exam=ExamEngine.Create(ExamCatalog.Profiles[4],4);exam.RegisterSuccessfulPlay();exam.Submit(CorrectResponse(exam.Current));exam.Advance();exam.RegisterSuccessfulPlay();var store=new ExamStore(dir);store.Save(exam.State);
                var restored=ExamEngine.Restore(store.LoadCurrent()!);Check(restored.State.CurrentIndex==1&&restored.PlaysRemaining==2&&restored.State.Answers.Length==1);Check(restored.Current.Exercise.Id==exam.Current.Exercise.Id);
                restored.Finish();store.Save(restored.State);Check(store.History().Length==1);Check(File.Exists(Path.Combine(dir,"exam-current.json.bak")));
                var global=new List<Attempt>();LearningAnalytics.MergeExamAnswers(global,restored.State);LearningAnalytics.MergeExamAnswers(global,restored.State);Check(global.Count==1);
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        });
        test("0.5: forged scores, invalid replay counters and corrupt exam files are rejected",()=>{
            var exam=ExamEngine.Create(ExamCatalog.Profiles[0],7);exam.RegisterSuccessfulPlay();exam.Submit(CorrectResponse(exam.Current));
            var answer=exam.State.Answers[0];Throws<InvalidDataException>(()=>ExamEngine.Restore(exam.State with{Answers=[answer with{Attempt=answer.Attempt with{Percentage=3}}]}));
            var plays=exam.State.Plays.ToArray();plays[0]=100;Throws<InvalidDataException>(()=>ExamEngine.Restore(exam.State with{Plays=plays}));
            var dir=Path.Combine(Path.GetTempPath(),"studio-exam-bad-"+Guid.NewGuid());Directory.CreateDirectory(dir);try{var file=Path.Combine(dir,"exam-current.json");File.WriteAllText(file,"{bad");Throws<InvalidDataException>(()=>new ExamStore(dir).LoadCurrent());Check(File.ReadAllText(file)=="{bad");}finally{Directory.Delete(dir,true);}
        });
        test("0.5: null exam entries and incomplete persisted questions fail as data errors without overwriting files",()=>{
            var engine=ExamEngine.Create(ExamCatalog.Profiles[0],31);var state=engine.State;
            Throws<InvalidDataException>(()=>ExamEngine.Restore(state with{Answers=[null!]}));
            Throws<InvalidDataException>(()=>ExamEngine.Restore(state with{Answers=[new(0,null!)]}));
            var q=state.Questions[0];var questions=state.Questions.ToArray();questions[0]=q with{Exercise=q.Exercise with{Notes=null!}};
            var broken=state with{Questions=questions};
            var dir=Path.Combine(Path.GetTempPath(),"studio-null-exam-"+Guid.NewGuid());Directory.CreateDirectory(dir);
            try{
                var file=Path.Combine(dir,"exam-current.json");var json=System.Text.Json.JsonSerializer.Serialize(broken);File.WriteAllText(file,json);
                Throws<InvalidDataException>(()=>new ExamStore(dir).LoadCurrent());Check(File.ReadAllText(file)==json);Check(!File.Exists(file+".bak"));
            }finally{Directory.Delete(dir,true);}
        });
        test("0.5: missing prior questions and altered answer metadata are rejected on restore",()=>{
            var engine=ExamEngine.Create(ExamCatalog.Profiles[0],32);engine.RegisterSuccessfulPlay();engine.Submit(CorrectResponse(engine.Current));engine.Advance();
            Throws<InvalidDataException>(()=>ExamEngine.Restore(engine.State with{Answers=[]}));
            var answer=engine.State.Answers[0];
            Throws<InvalidDataException>(()=>ExamEngine.Restore(engine.State with{Answers=[answer with{Attempt=answer.Attempt with{ExpectedName="Αλλοιωμένη απάντηση"}}]}));
            Throws<InvalidDataException>(()=>ExamEngine.Restore(engine.State with{Answers=[answer with{Attempt=answer.Attempt with{Notes=[21]}}]}));
            Check(ExamEngine.Restore(engine.State).State.CurrentIndex==1);
        });
        test("0.5: adaptive practice targets weak concepts while retaining contrast examples",()=>{
            var settings=new Settings(Category.Intervals,Level.Elementary);var g=new ExerciseGenerator(50);var history=new List<Attempt>();
            while(history.Count<4){var e=g.Next(settings);if(e.CorrectId!="interval:3")continue;var session=new Session();session.Begin(e);history.Add(session.Answer(e.Choices.First(c=>c.Id!=e.CorrectId).Id,settings.Level,settings));}
            Check(LearningAnalytics.Weaknesses(history).First().Percentage==0);Check(LearningAnalytics.ReviewableMistakes(history).Length==4);
            var target=0;for(var seed=0;seed<100;seed++){var task=LearningAnalytics.FreshAdaptive(history,settings,seed);Theory.VerifyExercise(task.Exercise);if(task.Exercise.CorrectId=="interval:3")target++;Check(task.Settings.Level==Level.Elementary);}
            Check(target>50&&target<100,"Adaptive practice lacks target weighting or contrast");
        });
        test("0.5: legacy progress and partial scores have correct learning analytics",()=>{
            var e=new ExerciseGenerator(1).Next(new());var a=new Attempt(DateTimeOffset.Now,e.Category,Level.Beginner,e.Id,e.Notes,e.CorrectId,e.CorrectName,e.CorrectId,e.CorrectName,true);
            var partial=a with{Correct=false,Percentage=50};Check(LearningAnalytics.Weaknesses([a,partial]).Single().Percentage==75);
            Check(LearningAnalytics.ReviewableMistakes([partial]).Length==0);
            Check(Enum.GetValues<Category>().All(c=>!string.IsNullOrWhiteSpace(CategoryLabels.Name(c))));
        });
        test("0.5: passing rhythm partial scores remain partial in analytics and legacy correct scores migrate",()=>{
            var e=new ExerciseGenerator(1).Next(new());
            var legacy=new Attempt(DateTimeOffset.UtcNow,e.Category,Level.Beginner,e.Id,e.Notes,e.CorrectId,e.CorrectName,e.CorrectId,e.CorrectName,true);
            var passing=legacy with{Percentage=80};var failing=legacy with{Correct=false,Percentage=40};
            Check(LearningAnalytics.EffectivePercentage(legacy)==100);
            Check(LearningAnalytics.EffectivePercentage(passing)==80);
            Check(Math.Abs(LearningAnalytics.Weaknesses([legacy,passing,failing]).Single().Percentage-220.0/3)<0.00001);
        });
        test("0.5: playback cache cannot return the previous question or stale tempo/voice/segment audio",()=>{
            var cache=new PlaybackCache();var settings=new Settings();float[] samples=[1,2,3,4];cache.Store("first",settings,samples);Check(ReferenceEquals(cache.Get("first",settings),samples));
            Check(cache.Get("second",settings)is null);Check(cache.Get("first",settings with{Tempo=120})is null);Check(cache.Get("first",settings with{SoloVoice=1})is null);Check(cache.Get("first",settings with{SegmentStart=1})is null);cache.Clear();Check(cache.Get("first",settings)is null);
        });
        var index=Array.IndexOf(args,"--maestro");if(index>=0)test("0.5: exam question uses actual Maestro audio and stores its scored snapshot",()=>{
            var engine=ExamEngine.Create(ExamCatalog.Profiles[^1],13);var renderer=new PianoRenderer(args[index+1]);var q=engine.Current;var pcm=renderer.Render(q.Exercise,q.Settings,renderer.Presets[0]);Check(pcm.All(float.IsFinite)&&pcm.Any(v=>Math.Abs(v)>0.001f));engine.RegisterSuccessfulPlay();var attempt=engine.Submit(CorrectResponse(q));Check(attempt.Correct&&attempt.ExamId==engine.State.Id&&attempt.Snapshot?.Id==q.Exercise.Id);
        });
    }
}
