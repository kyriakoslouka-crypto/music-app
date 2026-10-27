using Studio.Core;
namespace Studio.Windows;
public sealed partial class MainForm
{
    private readonly CheckBox automaticExam=new(){Text="Αυτόματη επόμενη ερώτηση και ακρόαση εξέτασης",AccessibleName="Αυτόματη συνέχεια εξετάσεων",AutoSize=true,Checked=true};
    private async Task QueueAutomaticExam(Exercise expected,bool expectedAnswered,Func<Task> action)
    {
        var currentExam=exam;var index=currentExam?.State.CurrentIndex;
        if(!automaticExam.Checked||currentExam?.Running!=true||tabs.SelectedIndex!=1||IsDisposed||Disposing)return;
        CancelAutomaticPractice();using var source=new CancellationTokenSource();automaticPending=source;
        try{
            await automaticWait((int)automaticDelay.Value,source.Token);source.Token.ThrowIfCancellationRequested();
            if(IsDisposed||Disposing||!automaticExam.Checked||exam!=currentExam||!currentExam.Running||currentExam.State.CurrentIndex!=index||currentExam.CurrentAnswered!=expectedAnswered||session.Current!=expected||answered!=expectedAnswered||tabs.SelectedIndex!=1||playing)return;
            if(automaticPending==source)automaticPending=null;
            await action();
        }catch(OperationCanceledException){}
        catch(Exception e){Error(e.Message);}
        finally{if(automaticPending==source)automaticPending=null;}
    }
    internal object AuditAutomaticExam()
    {
        var originalWait=automaticWait;var enabled=automaticExam.Checked;var records=new List<object>();var calls=0;
        Task Action(){calls++;return Task.CompletedTask;}
        Exercise Prepare(bool answer=true,Func<DateTimeOffset>? clock=null){
            CancelAutomaticPractice();exam=ExamEngine.Create(TrainingCatalog.ExamProfiles[0] with{QuestionCount=2},17,clock is null?0:1,clock);automaticExam.Checked=true;tabs.SelectedIndex=1;
            var e=exam.Current.Exercise;session.Begin(e);if(answer)exam.Skip();answered=answer;playing=false;automaticWait=(_,_)=>Task.CompletedTask;calls=0;return e;
        }
        void Check(string name,int wanted){if(calls!=wanted||automaticPending is not null)throw new InvalidOperationException("Automatic exam regression: "+name);records.Add(new{Name=name,Passed=true});}
        try{
            var e=Prepare();QueueAutomaticExam(e,true,Action).GetAwaiter().GetResult();Check("Answered question advances",1);
            e=Prepare(false);QueueAutomaticExam(e,false,Action).GetAwaiter().GetResult();Check("New question plays automatically",1);
            e=Prepare();automaticExam.Checked=false;QueueAutomaticExam(e,true,Action).GetAwaiter().GetResult();Check("Disabled setting stays manual",0);
            e=Prepare();automaticWait=(_,token)=>{Stop();return Task.FromCanceled(token);};QueueAutomaticExam(e,true,Action).GetAwaiter().GetResult();Check("Esc cancels",0);
            e=Prepare();automaticWait=(_,_)=>{tabs.SelectedIndex=3;return Task.CompletedTask;};QueueAutomaticExam(e,true,Action).GetAwaiter().GetResult();Check("Leaving question cancels",0);
            e=Prepare();automaticWait=(_,_)=>{exam!.Advance();return Task.CompletedTask;};QueueAutomaticExam(e,true,Action).GetAwaiter().GetResult();Check("Manual advance rejects stale wait",0);
            e=Prepare();automaticWait=(_,_)=>{exam=ExamEngine.Create(TrainingCatalog.ExamProfiles[0],18);return Task.CompletedTask;};QueueAutomaticExam(e,true,Action).GetAwaiter().GetResult();Check("Replaced examination rejects stale wait",0);
            e=Prepare();automaticWait=(_,_)=>{exam!.Finish();return Task.CompletedTask;};QueueAutomaticExam(e,true,Action).GetAwaiter().GetResult();Check("Finished exam cannot continue",0);
            var now=DateTimeOffset.UtcNow;e=Prepare(clock:()=>now);automaticWait=(_,_)=>{now=now.AddMinutes(2);return Task.CompletedTask;};QueueAutomaticExam(e,true,Action).GetAwaiter().GetResult();Check("Expiry prevents automatic continuation",0);
        }finally{CancelAutomaticPractice();exam=null;session.Clear();answered=false;automaticWait=originalWait;automaticExam.Checked=enabled;}
        return new{Passed=true,Executed=records.Count,Checks=records,SimulatedWait=true,PhysicalAudioTested=false};
    }
    internal object AuditCurriculum()
    {
        var expected=TrainingCatalog.Categories.Select(CategoryLabels.Name).ToArray();var actual=category.Items.Cast<string>().ToArray();
        if(!actual.SequenceEqual(expected)||actual.Length!=21||examProfile.Items.Cast<ExamProfile>().Any(p=>p.Categories.Any(c=>!TrainingCatalog.IsActive(c))))throw new InvalidOperationException("Removed categories still exposed");
        var before=category.SelectedIndex;try{for(var i=0;i<actual.Length;i++){category.SelectedIndex=i;if(ReadSettings().Category!=TrainingCatalog.Categories[i])throw new InvalidOperationException("Category index remapping failed");}}finally{category.SelectedIndex=before;}
        return new{Passed=true,ActiveCategories=actual,Count=actual.Length,RemovedCount=7,StableEnumIds=true};
    }
    internal async Task ProbeAutomaticExam(Func<int> plays)
    {
        automaticPractice.Checked=false;automaticExam.Checked=true;automaticDelay.Value=3;
        exam=ExamEngine.Create(TrainingCatalog.ExamProfiles[0] with{Categories=[Category.Intervals],QuestionCount=2},2716);SetExamLocks(true);
        async Task Wait(Func<bool> condition){var end=DateTime.UtcNow.AddSeconds(25);while(!condition()){if(DateTime.UtcNow>end)throw new TimeoutException("Automatic exam flow timed out");await Task.Delay(50);}}
        await ShowExamQuestion();await Wait(()=>heard&&!playing&&plays()==3);
        var first=exam.Current.Exercise;answers.SelectedIndex=Array.FindIndex(first.Choices,c=>c.Id==first.CorrectId);Submit();
        if(exam.State.Answers.Length!=1||!exam.CurrentAnswered)throw new InvalidOperationException("Exam answer not graded before continuation");
        var restored=ExamEngine.Restore(System.Text.Json.JsonSerializer.Deserialize<ExamState>(System.Text.Json.JsonSerializer.Serialize(exam.State))!);
        if(!restored.CurrentAnswered||restored.State.Answers.Length!=1)throw new InvalidOperationException("Answered exam state cannot restore");
        await Wait(()=>exam.State.CurrentIndex==1&&heard&&!playing&&plays()==4);
        var second=exam.Current.Exercise;answers.SelectedIndex=Array.FindIndex(second.Choices,c=>c.Id==second.CorrectId);Submit();
        await Wait(()=>exam.State.Finished is not null);await Task.Delay(3500);
        if(plays()!=4||exam.State.Answers.Length!=2||exam.Summary().Percentage!=100||automaticPending is not null)throw new InvalidOperationException("Exam did not finish once with two correct answers");
    }

}
