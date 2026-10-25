using System.Text.Json;
using NAudio.Wave;
namespace Studio.Windows;
// Hidden CI-only integration entry point. Never opens a physical device or overwrites user progress.
internal static class AutomaticPracticeProbe
{
    private sealed class ProbePlayer(Action<bool> played) : IWavePlayer
    {
        private IWaveProvider? source;private System.Windows.Forms.Timer? timer;
        public event EventHandler<StoppedEventArgs>? PlaybackStopped;
        public PlaybackState PlaybackState{get;private set;}
        public float Volume{get;set;}=1;
        public WaveFormat OutputWaveFormat{get;private set;}=WaveFormat.CreateIeeeFloatWaveFormat(44100,2);
        public void Init(IWaveProvider provider){source=provider;OutputWaveFormat=provider.WaveFormat;}
        public void Play(){
            PlaybackState=PlaybackState.Playing;var buffer=new byte[65536];var nonzero=false;int read;
            while((read=source!.Read(buffer,0,buffer.Length))>0)for(var i=0;i<read;i++)if(buffer[i]!=0)nonzero=true;
            played(nonzero);timer=new(){Interval=50};timer.Tick+=(_,_)=>{timer.Stop();PlaybackState=PlaybackState.Stopped;PlaybackStopped?.Invoke(this,new StoppedEventArgs());};timer.Start();
        }
        public void Pause(){timer?.Stop();PlaybackState=PlaybackState.Paused;}
        public void Stop(){timer?.Stop();PlaybackState=PlaybackState.Stopped;}
        public void Dispose(){timer?.Dispose();}
    }
    internal static int Run(string manifest,string report)
    {
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||!Environment.GetCommandLineArgs().Contains("--uia-test-mode"))throw new InvalidOperationException("Automatic flow probe is restricted to isolated CI.");
        var plays=0;var nonzeroPlays=0;string? error=null;
        using var output=new AudioOutput((_,_)=>new(new ProbePlayer(nonzero=>{plays++;if(nonzero)nonzeroPlays++;})));
        using var form=new MainForm(true,output,true);
        form.Shown+=async(_,_)=>{
            try{await form.ProbeAutomaticPractice(manifest,()=>plays);if(plays!=2||nonzeroPlays!=2)throw new InvalidOperationException("Two real Maestro PCM examples did not reach the byte output adapter.");}
            catch(Exception ex){error=ex.ToString();}
            finally{form.Close();}
        };
        Application.Run(form);
        File.WriteAllText(report,JsonSerializer.Serialize(new{Version=typeof(MainForm).Assembly.GetName().Version!.ToString(3),SourceCommit=Environment.GetEnvironmentVariable("GITHUB_SHA"),Passed=error is null,Error=error,AutomaticExamples=plays,NonzeroMaestroExamples=nonzeroPlays,ManualReplayCalls=0,RealDelayCancellationPassed=error is null,PhysicalAudioTested=false,SimulatedOutputDevice=true},new JsonSerializerOptions{WriteIndented=true}));
        return error is null?0:1;
    }
}
public sealed partial class MainForm
{
    internal async Task ProbeAutomaticPractice(string manifest,Func<int> plays)
    {
        if(File.Exists(Path.Combine(DataDirectory,"history.json"))||File.Exists(Path.Combine(DataDirectory,"settings.json")))throw new InvalidOperationException("Refusing to overwrite existing application data in a flow probe.");
        await LoadFont(manifest);devices.Items.Add(new OutputDevice("ci-simulated-output","Δοκιμαστική έξοδος CI"));devices.SelectedIndex=0;
        automaticPractice.Checked=true;automaticDelay.Value=3;
        async Task Wait(Func<bool> condition){var deadline=DateTime.UtcNow.AddSeconds(25);while(!condition()){if(DateTime.UtcNow>deadline)throw new TimeoutException("Automatic exercise flow did not complete.");await Task.Delay(50);}}
        await NewExercise();var first=session.Current!;await Wait(()=>heard&&!playing);
        if(plays()!=1)throw new InvalidOperationException("First automatic replay missing.");
        answers.SelectedIndex=Array.FindIndex(first.Choices,c=>c.Id==first.CorrectId);Submit();
        await Wait(()=>session.Current?.Id!=first.Id&&heard&&!playing);
        if(plays()!=2||history.Count!=1||!history[0].Correct)throw new InvalidOperationException("Automatic next exercise or scored history is incorrect.");
        var second=session.Current!;answers.SelectedIndex=Array.FindIndex(second.Choices,c=>c.Id==second.CorrectId);Submit();Stop();
        await Task.Delay(3500);
        if(session.Current?.Id!=second.Id||plays()!=2||automaticPending is not null||store.Load().Count!=2)throw new InvalidOperationException("Stopped automatic continuation ran late or progress was not saved.");
    }
}
