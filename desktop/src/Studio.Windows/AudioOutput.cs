using NAudio.CoreAudioApi;
using NAudio.Wave;
using Studio.Audio;
namespace Studio.Windows;
public sealed record OutputDevice(string Id, string Name, bool Asio = false, bool IsDefault=false) { public override string ToString() => Name; }
public sealed class AudioOutput : IDisposable
{
    private IWavePlayer? player;
    private MMDevice? endpoint;
    private bool disposed;
    public bool IsPlaying => player?.PlaybackState == PlaybackState.Playing;
    public bool IsPaused => player?.PlaybackState == PlaybackState.Paused;
    public static List<OutputDevice> Devices()
    {
        var devices = new List<OutputDevice>();
        using var enumerator = new MMDeviceEnumerator();
        string? defaultId=null;
        try{using var current=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);defaultId=current.ID;}
        catch(System.Runtime.InteropServices.COMException){ /* No system default endpoint. */ }
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        { devices.Add(new(device.ID, "WASAPI: " + device.FriendlyName,false,device.ID==defaultId)); device.Dispose(); }
        foreach (var driver in AsioOut.GetDriverNames()) devices.Add(new(driver, "ASIO: " + driver, true));
        return devices;
    }
    public void Play(float[] pcm, OutputDevice device, Action<Exception?> finished, bool loop = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Stop();
        try
        {
            if (device.Asio) player = new AsioOut(device.Id);
            else
            {
                using var enumerator = new MMDeviceEnumerator();
                endpoint = enumerator.GetDevice(device.Id);
                player = new WasapiOut(endpoint, AudioClientShareMode.Shared, true, 80);
            }
            player.Init(new PcmSource(pcm,loop).ToWaveProvider());
            player.PlaybackStopped += (_, e) => finished(e.Exception);
            player.Play();
        }
        catch { Stop(); throw; }
    }
    public void PauseOrResume()
    {
        if (IsPlaying) player!.Pause();
        else if (IsPaused) player!.Play();
    }
    public void Stop()
    {
        var previous = player; player = null;
        if (previous is not null) { previous.Stop(); previous.Dispose(); }
        endpoint?.Dispose(); endpoint = null;
    }
    public void Dispose() { if (disposed) return; disposed = true; Stop(); }
    private sealed class PcmSource(float[] samples,bool loop) : ISampleProvider
    {
        private readonly LoopingPcmCursor cursor=new(samples,loop);
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(PianoRenderer.SampleRate, 2);
        public int Read(float[] buffer,int offset,int count)=>cursor.Read(buffer,offset,count);
    }
}
