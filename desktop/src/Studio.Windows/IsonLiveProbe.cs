using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using NAudio.Wave;
using Studio.Audio;
using Studio.Core;
namespace Studio.Windows;
internal static class IsonLiveProbe
{
    private sealed class Player:IWavePlayer
    {
        public event EventHandler<StoppedEventArgs>? PlaybackStopped{add{}remove{}}
        public PlaybackState PlaybackState{get;private set;}public float Volume{get;set;}=1;
        public WaveFormat OutputWaveFormat=>WaveFormat.CreateIeeeFloatWaveFormat(44100,2);public IWaveProvider? Source;
        public void Init(IWaveProvider source)=>Source=source;
        public void Play()=>PlaybackState=PlaybackState.Playing;public void Pause()=>PlaybackState=PlaybackState.Paused;public void Stop()=>PlaybackState=PlaybackState.Stopped;public void Dispose(){}
    }
    internal static int Run(string report)
    {
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||!Environment.GetCommandLineArgs().Contains("--uia-test-mode"))throw new InvalidOperationException("CI-only probe");
        var root=Path.Combine(Path.GetTempPath(),"ison-live-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var opens=0;string? error=null;object? metrics=null;var player=new Player();
        try{
            var wave=Path.Combine(root,"wave.wav");using(var writer=new WaveFileWriter(wave,new WaveFormat(44100,16,2))){for(var i=0;i<44100*3;i++){var value=(short)(Math.Sin(2*Math.PI*110*i/44100)*10000);writer.Write(BitConverter.GetBytes(value),0,2);writer.Write(BitConverter.GetBytes(value),0,2);}}
            var sha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(wave))).ToLowerInvariant();File.Move(wave,Path.Combine(root,sha+".wav"));var manifest=Path.Combine(root,"isokratis-bank.json");File.WriteAllText(manifest,JsonSerializer.Serialize(new IsonBank(1,[new("a","Φωνή πρώτη","test.mp3",sha+".wav",110,sha,sha),new("b","Φωνή δεύτερη","test.mp3",sha+".wav",110,sha,sha)])));
            using var output=new AudioOutput((_,_)=>{opens++;return new(player);});using var form=new MainForm(true,output,true);
            form.Shown+=async(_,_)=>{try{metrics=await form.ProbeLiveIson(manifest,()=>opens,()=>{var bytes=new byte[8192];return player.Source!.Read(bytes,0,bytes.Length)==bytes.Length&&bytes.Any(b=>b!=0);});}catch(Exception ex){error=ex.ToString();}finally{form.Close();}};
            Application.Run(form);
        }catch(Exception ex){error=ex.ToString();}finally{Directory.Delete(root,true);}
        File.WriteAllText(report,JsonSerializer.Serialize(new{Version=typeof(MainForm).Assembly.GetName().Version!.ToString(3),SourceCommit=Environment.GetEnvironmentVariable("GITHUB_SHA"),Passed=error is null,Error=error,DeviceOpens=opens,Metrics=metrics,PhysicalHardwareTested=false,HumanNvdaTested=false,InputSimulation="Native command dispatch with simulated audio backend"},new JsonSerializerOptions{WriteIndented=true}));return error is null?0:1;
    }
}
public sealed partial class MainForm
{
    internal async Task<object> ProbeLiveIson(string manifest,Func<int> opens,Func<bool> pump)
    {
        void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        bool Arrow(Keys key){var message=new Message();return ProcessCmdKey(ref message,key);}
        async Task Settled(){for(var i=0;i<500&&isonWork is not null;i++)await Task.Delay(10);Check(isonWork is null&&isonArmed&&isonPlaying,"Live request did not settle: "+isonStatus.Text);}
        isonLoaded=false;isonManifest=manifest;isonBank=IsonBank.Load(manifest);PopulateIson();isonBase.Value=100;isonNote.SelectedIndex=0;isonLoaded=true;
        devices.Items.Clear();devices.Items.Add(new OutputDevice("probe","Δοκιμαστική έξοδος"));devices.SelectedIndex=0;tabs.SelectedTab=isonPage;
        await PlayIson();Check(isonKeyboard.Focused&&opens()==1&&pump(),"Initial live start/focus/PCM failed");
        var directory=Path.GetDirectoryName(manifest)!;for(var note=0;note<8;note++){var hz=ByzantineTuning.Frequency(100,note,0,ByzantineTuning.Diatonic);var sample=IsonRenderer.Select(isonBank,((IsonVoice)isonVoice.SelectedItem!).Id,hz);await Task.Run(()=>CachedIson(directory,sample,hz,0.45,CancellationToken.None));}
        var maximum=0.0;foreach(var key in new[]{Keys.Up,Keys.Down,Keys.Right,Keys.Left}){var watch=Stopwatch.StartNew();Check(Arrow(key),"Arrow was not handled");await Settled();maximum=Math.Max(maximum,watch.Elapsed.TotalMilliseconds);Check(pump(),"Live PCM vanished");}
        Check(isonNote.SelectedIndex==0&&isonVoice.SelectedIndex==0&&opens()==1,"Arrows reopened device or did not change both dimensions");
        for(var i=0;i<20;i++)Arrow(i%2==0?Keys.Up:Keys.Down);await Settled();Check(isonNote.SelectedIndex==0,"Stale fast navigation won");
        foreach(var item in ByzantineModes.All.Skip(1).Concat(ByzantineModes.All.Take(1))){isonMode.SelectedItem=item;await Settled();Check(isonNote.SelectedIndex==item.Degree,"Mode basis did not apply");}
        ToggleIsonPause();Check(audio.IsPaused&&tabs.SelectedTab==isonPage,"Pause changed tab");isonKeyboard.Focus();Arrow(Keys.Up);await Settled();Check(audio.IsPaused,"Changing a paused ison resumed it");ToggleIsonPause();Check(audio.IsPlaying,"Resume failed");Check(opens()==1,"Device reopened");
        Stop();Check(!IsonArrow(Keys.Up)&&!audio.IsPlaying,"Stopped arrows restarted audio");
        return new{ManualStarts=1,ArrowDirections=4,RapidChanges=20,ModeCount=8,DeviceReopenCount=opens()-1,CachedSwitchMaxMilliseconds=maximum,PausePreserved=true,StopPreserved=true};
    }
}
