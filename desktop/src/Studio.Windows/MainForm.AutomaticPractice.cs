using Studio.Core;
namespace Studio.Windows;
public sealed partial class MainForm
{
    private readonly CheckBox automaticPractice=new(){Text="Αυτόματη επόμενη άσκηση και ακρόαση",AccessibleName="Αυτόματη συνέχεια και αναπαραγωγή εξάσκησης",AutoSize=true};
    private readonly NumericUpDown automaticDelay=Number("Αναμονή αυτόματης συνέχειας σε δευτερόλεπτα",3,120,10);
    private CancellationTokenSource? automaticPending;
    private Func<int,CancellationToken,Task> automaticWait=(seconds,token)=>Task.Delay(TimeSpan.FromSeconds(seconds),token);
    private void CancelAutomaticPractice(){var pending=automaticPending;automaticPending=null;pending?.Cancel();}
    private async Task QueueAutomaticPractice(Exercise expected,bool expectedAnswered,Func<Task> action)
    {
        if(!automaticPractice.Checked||exam is not null||tabs.SelectedIndex!=1||IsDisposed||Disposing)return;
        CancelAutomaticPractice();using var source=new CancellationTokenSource();automaticPending=source;
        try{
            await automaticWait((int)automaticDelay.Value,source.Token);
            source.Token.ThrowIfCancellationRequested();
            if(IsDisposed||Disposing||!automaticPractice.Checked||exam is not null||tabs.SelectedIndex!=1||session.Current!=expected||answered!=expectedAnswered||playing)return;
            // The action can schedule its own next phase. Stop must not cancel this completed wait.
            if(automaticPending==source)automaticPending=null;
            await action();
        }catch(OperationCanceledException){}
        catch(Exception ex){Error(ex.Message);}
        finally{if(automaticPending==source)automaticPending=null;}
    }
    internal object AuditAutomaticPractice()
    {
        var originalWait=automaticWait;var originalEnabled=automaticPractice.Checked;var originalDelay=automaticDelay.Value;
        var records=new List<object>();var calls=0;var generator=new ExerciseGenerator(73);
        var expected=generator.Next(new());
        Task Action(){calls++;return Task.CompletedTask;}
        void Prepare(){CancelAutomaticPractice();exam=null;automaticPractice.Checked=true;tabs.SelectedIndex=1;session.Begin(expected);answered=true;playing=false;automaticWait=(_,_)=>Task.CompletedTask;calls=0;}
        void Check(string name,int wanted){if(calls!=wanted||automaticPending is not null)throw new InvalidOperationException("Automatic practice regression: "+name);records.Add(new{Name=name,Passed=true});}
        try{
            Prepare();automaticPractice.Checked=false;QueueAutomaticPractice(expected,true,Action).GetAwaiter().GetResult();Check("Opt-in only",0);
            Prepare();QueueAutomaticPractice(expected,true,Action).GetAwaiter().GetResult();Check("Same answered question advances after wait",1);
            Prepare();automaticWait=(_,token)=>{Stop();return Task.FromCanceled(token);};QueueAutomaticPractice(expected,true,Action).GetAwaiter().GetResult();Check("Esc/Stop cancels pending action",0);
            Prepare();exam=ExamEngine.Create(ExamCatalog.Profiles[0],73);exam.Finish();QueueAutomaticPractice(expected,true,Action).GetAwaiter().GetResult();Check("Finished exam never falls back to automatic practice",0);
            Prepare();automaticWait=(_,_)=>{tabs.SelectedIndex=0;return Task.CompletedTask;};QueueAutomaticPractice(expected,true,Action).GetAwaiter().GetResult();Check("Leaving practice cancels the action",0);
            Prepare();automaticWait=(_,_)=>{session.Begin(generator.Next(new()));return Task.CompletedTask;};QueueAutomaticPractice(expected,true,Action).GetAwaiter().GetResult();Check("Replaced question rejects stale continuation",0);
        }finally{CancelAutomaticPractice();exam=null;automaticWait=originalWait;automaticPractice.Checked=originalEnabled;automaticDelay.Value=originalDelay;session.Clear();answered=false;}
        return new{Passed=true,Executed=records.Count,Checks=records,SimulatedWait=true,PhysicalAudioTested=false};
    }
}
