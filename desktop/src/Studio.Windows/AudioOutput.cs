using NAudio.CoreAudioApi;
using NAudio.Wave;
using Studio.Audio;
namespace Studio.Windows;
public sealed record OutputDevice(string Id, string Name, bool Asio = false, bool IsDefault=false) { public override string ToString() => Name; }
public sealed class AudioBackend(IWavePlayer player,IDisposable? resource=null) : IDisposable
{
    private int disposed;
    public IWavePlayer Player {get;}=player;
    public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;try{Player.Dispose();}finally{resource?.Dispose();}}
}
public sealed class AudioPlaybackException(OutputDevice device,Exception first,Exception? fallback=null) : Exception(
    $"Δεν ήταν δυνατή η αναπαραγωγή από {device.Name}. Κωδικός 0x{(fallback??first).HResult:X8}.",fallback is null?first:new AggregateException(first,fallback))
{
    public OutputDevice Device {get;}=device;
    public Exception FirstFailure {get;}=first;
    public Exception? CompatibilityFailure {get;}=fallback;
    public string DiagnosticText=>$"Συσκευή: {Device.Name}\r\nΜέθοδος: {(Device.Asio?"ASIO":"WASAPI shared, αρχικά event-driven και έπειτα συμβατός χρονισμός")}\r\n"+
        $"Πρώτο σφάλμα: 0x{FirstFailure.HResult:X8} — {FirstFailure.Message}\r\n"+
        (CompatibilityFailure is null?"":$"Δεύτερη προσπάθεια: 0x{CompatibilityFailure.HResult:X8} — {CompatibilityFailure.Message}\r\n")+
        "Η ίδια άσκηση διατηρείται. Δεν έγινε αυτόματη αλλαγή συσκευής.\r\n"+
        "Ctrl+Home: Ρυθμίσεις. Ανανέωση συσκευών και επιλογή ενεργής εξόδου WASAPI. Αν χρησιμοποιείς ASIO, μπορείς να επιλέξεις WASAPI για έλεγχο. Ctrl+R: νέα προσπάθεια.\r\n\r\nΤεχνικά στοιχεία:\r\n"+this;
}
public sealed class AudioOutput : IDisposable
{
    private sealed class Attempt(AudioBackend backend,bool eventSync)
    {
        public readonly AudioBackend Backend=backend;
        public readonly bool EventSync=eventSync;
        public EventHandler<StoppedEventArgs>? Handler;
    }
    private sealed class Request(float[] pcm,OutputDevice device,Action<Exception?> finished,bool loop)
    {
        public readonly float[] Pcm=pcm;public readonly OutputDevice Device=device;public readonly Action<Exception?> Finished=finished;public readonly bool Loop=loop;
        public readonly SynchronizationContext? Context=SynchronizationContext.Current;
        public Attempt? Active;public Exception? FirstFailure;public int Cancelled,Completed,RecoveryQueued;
    }
    private readonly Func<OutputDevice,bool,AudioBackend> factory;
    private Request? current;
    private bool disposed;
    public AudioOutput(Func<OutputDevice,bool,AudioBackend>? backendFactory=null){factory=backendFactory??OpenBackend;}
    public bool IsPlaying=>Volatile.Read(ref current)?.Active?.Backend.Player.PlaybackState==PlaybackState.Playing;
    public bool IsPaused=>Volatile.Read(ref current)?.Active?.Backend.Player.PlaybackState==PlaybackState.Paused;
    public static List<OutputDevice> Devices()
    {
        var devices=new List<OutputDevice>();using var enumerator=new MMDeviceEnumerator();string? defaultId=null;
        try{using var selected=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);defaultId=selected.ID;}
        catch(System.Runtime.InteropServices.COMException){ }
        foreach(var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active))
        {devices.Add(new(device.ID,"WASAPI: "+device.FriendlyName,false,device.ID==defaultId));device.Dispose();}
        foreach(var driver in AsioOut.GetDriverNames())devices.Add(new(driver,"ASIO: "+driver,true));
        return devices;
    }
    private static AudioBackend OpenBackend(OutputDevice device,bool eventSync)
    {
        if(device.Asio)return new(new AsioOut(device.Id));
        using var enumerator=new MMDeviceEnumerator();var endpoint=enumerator.GetDevice(device.Id);
        try{return new(new WasapiOut(endpoint,AudioClientShareMode.Shared,eventSync,eventSync?80:200),endpoint);}
        catch{endpoint.Dispose();throw;}
    }
    public void Play(float[] pcm,OutputDevice device,Action<Exception?> finished,bool loop=false)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(pcm.Length==0||pcm.Length%2!=0)throw new ArgumentException("Απαιτούνται πλήρη stereo δείγματα ήχου.");
        Stop();var request=new Request(pcm,device,finished,loop);Volatile.Write(ref current,request);
        try{Start(request,!device.Asio);}
        catch(Exception first)
        {
            if(!Alive(request))return;request.FirstFailure=first;
            if(device.Asio){Stop();throw new AudioPlaybackException(device,first);}
            try{Start(request,false);}
            catch(Exception second){if(!Alive(request))return;Stop();throw new AudioPlaybackException(device,first,second);}
        }
    }
    private bool Alive(Request request)=>ReferenceEquals(Volatile.Read(ref current),request)&&Volatile.Read(ref request.Cancelled)==0;
    private void Start(Request request,bool eventSync)
    {
        Attempt? attempt=null;
        try
        {
            attempt=new(factory(request.Device,eventSync),eventSync);
            attempt.Handler=(_,e)=>Stopped(request,attempt,e.Exception);
            attempt.Backend.Player.PlaybackStopped+=attempt.Handler;
            // A fresh cursor starts the original piano example from its beginning on retry.
            attempt.Backend.Player.Init(new PcmSource(request.Pcm,request.Loop).ToWaveProvider());
            if(!Alive(request)){Release(attempt);return;}
            Interlocked.Exchange(ref request.Active,attempt);
            if(!Alive(request)){
                if(ReferenceEquals(Interlocked.CompareExchange(ref request.Active,null,attempt),attempt))Release(attempt);
                return;
            }
            attempt.Backend.Player.Play();
        }
        catch
        {
            if(attempt is not null){Interlocked.CompareExchange(ref request.Active,null,attempt);Release(attempt);}
            throw;
        }
    }
    private void Stopped(Request request,Attempt attempt,Exception? error)
    {
        if(!Alive(request)||!ReferenceEquals(Volatile.Read(ref request.Active),attempt)||Volatile.Read(ref request.Completed)!=0)return;
        if(error is not null&&!request.Device.Asio&&attempt.EventSync)
        {
            if(Interlocked.CompareExchange(ref request.RecoveryQueued,1,0)!=0)return;
            request.FirstFailure=error;
            // Never dispose/join a driver from inside its own playback callback.
            void Retry(){
                if(!Alive(request)||!ReferenceEquals(Interlocked.CompareExchange(ref request.Active,null,attempt),attempt))return;
                Release(attempt);
                if(!Alive(request))return;
                try{Start(request,false);}
                catch(Exception second){Complete(request,new AudioPlaybackException(request.Device,error,second));}
            }
            if(request.Context is { } context)context.Post(_=>Retry(),null);
            else ThreadPool.QueueUserWorkItem(_=>Retry());
            return;
        }
        if(attempt.EventSync&&Volatile.Read(ref request.RecoveryQueued)!=0)return;
        Complete(request,error is null?null:new AudioPlaybackException(request.Device,request.FirstFailure??error,request.FirstFailure is null?null:error));
    }
    private void Complete(Request request,Exception? error)
    {
        if(Alive(request)&&Interlocked.CompareExchange(ref request.Completed,1,0)==0)request.Finished(error);
    }
    private static void Release(Attempt attempt)
    {
        if(attempt.Handler is not null)attempt.Backend.Player.PlaybackStopped-=attempt.Handler;
        try{attempt.Backend.Player.Stop();}catch(Exception){/* Preserve the original driver error; disposal must still run. */}
        try{attempt.Backend.Dispose();}catch(Exception){/* Device loss must not prevent closing the application. */}
    }
    public void PauseOrResume()
    {
        var player=Volatile.Read(ref current)?.Active?.Backend.Player;
        if(player?.PlaybackState==PlaybackState.Playing)player.Pause();else if(player?.PlaybackState==PlaybackState.Paused)player.Play();
    }
    public void Stop()
    {
        var request=Interlocked.Exchange(ref current,null);if(request is null)return;
        Interlocked.Exchange(ref request.Cancelled,1);
        var attempt=Interlocked.Exchange(ref request.Active,null);if(attempt is not null)Release(attempt);
    }
    public void Dispose(){if(disposed)return;disposed=true;Stop();}
    private sealed class PcmSource(float[] samples,bool loop) : ISampleProvider
    {
        private readonly LoopingPcmCursor cursor=new(samples,loop);
        public WaveFormat WaveFormat{get;}=WaveFormat.CreateIeeeFloatWaveFormat(PianoRenderer.SampleRate,2);
        public int Read(float[] buffer,int offset,int count)=>cursor.Read(buffer,offset,count);
    }
}
