using System.IO;
using System.Text.Json;
using NAudio.Wave;
using Studio.Windows;
internal static class AudioOutputChecks
{
    private sealed class FakePlayer : IWavePlayer
    {
        public event EventHandler<StoppedEventArgs>? PlaybackStopped;
        public PlaybackState PlaybackState{get;private set;}
        public float Volume{get;set;}=1;
        public WaveFormat OutputWaveFormat{get;private set;}=WaveFormat.CreateIeeeFloatWaveFormat(44100,2);
        public Exception? InitFailure;public bool ThrowOnStop,ThrowOnDispose;public int Plays,Stops,Disposes;public byte[] FirstBytes=[];public IWaveProvider? Source;
        public void Init(IWaveProvider source){Source=source;OutputWaveFormat=source.WaveFormat;if(InitFailure is not null)throw InitFailure;FirstBytes=new byte[16];if(source.Read(FirstBytes,0,16)!=16)throw new Exception("Expected original piano PCM bytes.");}
        public void Play(){Plays++;PlaybackState=PlaybackState.Playing;}
        public void Pause(){PlaybackState=PlaybackState.Paused;}
        public void Stop(){Stops++;PlaybackState=PlaybackState.Stopped;if(ThrowOnStop)throw new IOException("stop failed");}
        public void Dispose(){Disposes++;if(ThrowOnDispose)throw new IOException("dispose failed");}
        public void End(Exception? ex=null){PlaybackState=PlaybackState.Stopped;PlaybackStopped?.Invoke(this,new StoppedEventArgs(ex));}
    }
    private sealed class Resource : IDisposable{public int Disposals;public void Dispose(){Disposals++;}}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Wait(Func<bool> done){if(!SpinWait.SpinUntil(done,TimeSpan.FromSeconds(5)))throw new Exception("Bounded audio test timed out");}
    internal static int Run(string report)
    {
        var results=new List<object>();var failed=0;float[] pcm=[0.1f,0.2f,0.3f,0.4f];var device=new OutputDevice("same-device","Ίδια συσκευή");
        void Test(string name,Action action){try{action();results.Add(new{Name=name,Passed=true});}catch(Exception ex){failed++;results.Add(new{Name=name,Passed=false,Error=ex.ToString()});}}
        Test("Actual NAudio float-to-byte adapter preserves exact PCM at nonzero offsets and loop boundaries",()=>{
            var player=new FakePlayer();using var output=new AudioOutput((_,_)=>new(player));output.Play(pcm,device,_=>{},true);
            const string expected="CDCCCC3DCDCC4C3E9A99993ECDCCCC3E";
            Check(Convert.ToHexString(player.FirstBytes)==expected,"Original PCM bits changed");
            var buffer=Enumerable.Repeat((byte)0xA5,48).ToArray();Check(player.Source!.Read(buffer,8,32)==32,"Loop read length changed");
            Check(Convert.ToHexString(buffer.AsSpan(8,32))==expected+expected,"Adapter offset or loop boundary corrupted PCM");
            Check(buffer.Take(8).Concat(buffer.Skip(40)).All(b=>b==0xA5),"Bytes outside requested buffer region changed");
        });
        Test("Asynchronous event-driven failure retries once on same device with fresh identical PCM",()=>{
            var first=new FakePlayer();var second=new FakePlayer();var calls=0;Exception? final=null;var completions=0;
            using var output=new AudioOutput((d,events)=>{Check(d==device,"Device changed");var n=Interlocked.Increment(ref calls);Check(events==(n==1),"Wrong WASAPI retry mode");return new(n==1?first:second);});
            output.Play(pcm,device,e=>{final=e;Interlocked.Increment(ref completions);});first.End(new IOException("driver event error"));Wait(()=>second.Plays==1);
            Check(first.FirstBytes.SequenceEqual(second.FirstBytes),"PCM changed or cursor not restarted");Check(completions==0,"Transient failure surfaced as terminal");second.End();Wait(()=>completions==1);Check(final is null&&calls==2,"Retry did not finish normally");
        });
        Test("Synchronous initialization failure uses compatible mode without losing original PCM",()=>{
            var first=new FakePlayer{InitFailure=new IOException("init")};var second=new FakePlayer();var calls=0;
            using var output=new AudioOutput((_,events)=>{calls++;Check(events==(calls==1),"Mode mismatch");return new(calls==1?first:second);});
            output.Play(pcm,device,_=>{});Check(second.Plays==1&&calls==2&&first.Disposes==1,"Init failure not recovered");
        });
        Test("Two driver failures produce one terminal callback retaining both original errors",()=>{
            var first=new FakePlayer();var second=new FakePlayer();var calls=0;var done=0;Exception? final=null;
            using var output=new AudioOutput((_,_)=>new(Interlocked.Increment(ref calls)==1?first:second));
            output.Play(pcm,device,e=>{final=e;Interlocked.Increment(ref done);});first.End(new IOException("event-error"));Wait(()=>second.Plays==1);second.End(new IOException("polling-error"));second.End(new IOException("duplicate"));Wait(()=>done==1);
            Check(calls==2&&final is AudioPlaybackException e&&e.DiagnosticText.Contains("event-error")&&e.DiagnosticText.Contains("polling-error")&&e.Device==device,"Original diagnostics lost or duplicate completion");
        });
        Test("ASIO failure never silently switches to WASAPI or another output",()=>{
            var player=new FakePlayer();var calls=0;Exception? final=null;
            using var output=new AudioOutput((d,events)=>{calls++;Check(d.Asio&&!events,"Unexpected ASIO mode");return new(player);});
            output.Play(pcm,device with{Asio=true},e=>final=e);player.End(new IOException("ASIO failed"));Check(calls==1&&final is AudioPlaybackException,"ASIO incorrectly retried");
        });
        Test("Stop cancels queued retry even when backend initialization was already entered",()=>{
            var first=new FakePlayer();var second=new FakePlayer();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();using var returned=new ManualResetEventSlim();var calls=0;var done=0;
            using var output=new AudioOutput((_,_)=>{if(Interlocked.Increment(ref calls)==1)return new(first);entered.Set();if(!release.Wait(5000))throw new Exception("test release timeout");returned.Set();return new(second);});
            output.Play(pcm,device,_=>Interlocked.Increment(ref done));first.End(new IOException("retry"));Check(entered.Wait(5000),"Recovery not queued");output.Stop();release.Set();Check(returned.Wait(5000),"Factory did not return");Wait(()=>second.Disposes==1);Check(second.Plays==0&&done==0,"Audio resumed after Stop");
        });
        Test("Previous device callbacks cannot complete or interrupt a new example",()=>{
            var first=new FakePlayer();var second=new FakePlayer();var calls=0;var old=0;var fresh=0;
            using var output=new AudioOutput((_,_)=>new(++calls==1?first:second));output.Play(pcm,device,_=>old++);output.Play(pcm,device,_=>fresh++);first.End(new IOException("stale"));Check(old==0&&fresh==0&&second.Plays==1,"Stale callback escaped");second.End();Check(fresh==1,"New request lost");
        });
        Test("Driver cleanup exceptions do not prevent owned resource disposal or closing",()=>{
            var player=new FakePlayer{ThrowOnStop=true,ThrowOnDispose=true};var resource=new Resource();using var output=new AudioOutput((_,_)=>new(player,resource));
            output.Play(pcm,device,_=>{});output.Stop();output.Stop();Check(player.Disposes==1&&resource.Disposals==1,"Cleanup resource leaked or disposed twice");
        });
        File.WriteAllText(report,JsonSerializer.Serialize(new{Version="0.5.0",SourceCommit=Environment.GetEnvironmentVariable("GITHUB_SHA"),Passed=failed==0,Executed=results.Count,Failed=failed,PhysicalAudioTested=false,SimulatedDriverFailures=true,Checks=results},new JsonSerializerOptions{WriteIndented=true}));
        return failed==0?0:1;
    }
}
