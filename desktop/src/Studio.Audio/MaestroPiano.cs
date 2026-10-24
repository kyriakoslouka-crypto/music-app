using System.Security.Cryptography;
using System.Text.Json;
using Studio.Core;
namespace Studio.Audio;
public sealed class MaestroPiano
{
    private readonly MaestroManifest manifest;
    private readonly string directory;
    private readonly Dictionary<string,PcmWave> cache = [];
    public MaestroPiano(string path)
    {
        directory=Path.GetDirectoryName(Path.GetFullPath(path))!;
        manifest=JsonSerializer.Deserialize<MaestroManifest>(File.ReadAllText(path)) ?? throw new InvalidDataException("Κενή βιβλιοθήκη.");
        if(manifest.Format!=1 || manifest.SourceCommit!=MaestroLibrary.Commit || manifest.SourceSha256!=MaestroLibrary.ArchiveSha256 || manifest.Regions.Length!=792) throw new InvalidDataException("Μη έγκυρο προφίλ Maestro.");
        foreach(var key in Enumerable.Range(21,88))
        {
            foreach(var velocity in Enumerable.Range(1,127)) foreach(var release in new[]{false,true})
                if(manifest.Regions.Count(r=>r.Key==key && r.Release==release && r.LowVelocity<=velocity && r.HighVelocity>=velocity)!=1) throw new InvalidDataException("Ελλιπής ή αμφίσημη κάλυψη δυναμικών.");
        }
        foreach(var r in manifest.Regions)
            if(r.File!=Path.GetFileName(r.File) || r.Sha256.Length!=64 || r.ReleaseSeconds is < 0 or > 20 || !File.Exists(Path.Combine(directory,r.File))) throw new InvalidDataException("Λείπει ή είναι μη έγκυρο δείγμα πιάνου.");
        foreach(var file in new[]{manifest.PedalDown,manifest.PedalUp})
            if(file!=Path.GetFileName(file) || !File.Exists(Path.Combine(directory,file))) throw new InvalidDataException("Λείπει δείγμα πεντάλ.");
    }
    public MaestroRegion Select(int key,int velocity,bool release=false) => manifest.Regions.Single(r=>r.Key==key&&r.Release==release&&velocity>=r.LowVelocity&&velocity<=r.HighVelocity);
    private PcmWave Load(MaestroRegion region)
    {
        lock(cache)
        {
            if(cache.TryGetValue(region.File,out var value)) return value;
            var path=Path.Combine(directory,region.File);
            using(var stream=File.OpenRead(path)) if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(region.Sha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Αλλοιωμένο δείγμα: "+region.File);
            var wave=PcmWave.Read(path); if(cache.Count>=24) cache.Remove(cache.Keys.First()); cache[region.File]=wave; return wave;
        }
    }
    private sealed class Voice(int key,int velocity,int start,int channel)
    {
        public int Key=key,Velocity=velocity,Start=start,Channel=channel;
        public int Release=-1;
        public bool Held=true;
    }
    public float[] Render(Exercise exercise,Settings settings,CancellationToken token=default)
    {
        return RenderScheduled(Schedule.Create(exercise,settings),settings,token);
    }
    public float[] RenderScheduled(ScheduledEvent[] schedule,Settings settings,CancellationToken token=default)
    {
        const int rate=PianoRenderer.SampleRate;
        var voices=new List<Voice>(); var pedal=false;
        foreach(var e in schedule)
        {
            if(e.Action==MidiAction.NoteOn)
            {
                foreach(var v in voices.Where(v=>v.Key==e.Key&&v.Channel==e.Channel&&v.Release<0)) v.Release=e.Frame;
                voices.Add(new(e.Key,e.Value,e.Frame,e.Channel));
            }
            else if(e.Action==MidiAction.NoteOff)
            {
                foreach(var v in voices.Where(v=>v.Key==e.Key&&v.Channel==e.Channel&&v.Held)) {v.Held=false;if(!pedal)v.Release=e.Frame;}
            }
            else { pedal=e.Value>=64; if(!pedal)foreach(var v in voices.Where(v=>!v.Held&&v.Release<0))v.Release=e.Frame; }
        }
        if(voices.Count>2048)throw new ArgumentException("Υπέρβαση 2048 συνολικών συμβάντων.");
        var buffer=new float[(schedule[^1].Frame+rate*14)*2]; var end=0;
        foreach(var voice in voices)
        {
            token.ThrowIfCancellationRequested();
            var region=Select(voice.Key,voice.Velocity); var sample=Load(region);
            var release=voice.Release<0?schedule[^1].Frame:voice.Release;
            var releaseFrames=Math.Max(1,(int)(region.ReleaseSeconds*rate));
            var frames=Math.Min((int)((long)sample.Frames*rate/sample.SampleRate),release-voice.Start+releaseFrames);
            Mix(sample,voice.Start,frames,(voice.Velocity/127f)*0.32f,release,releaseFrames);
            var noise=Load(Select(voice.Key,voice.Velocity,true));
            var decay=(float)Math.Pow(10,-3.0*(release-voice.Start)/rate/20);
            Mix(noise,release,(int)((long)noise.Frames*rate/noise.SampleRate),0.10f*decay);
        }
        var previousPedal=false;
        foreach(var e in schedule.Where(e=>e.Action==MidiAction.Pedal))
        {
            if(!settings.Pedal || (e.Value>=64)==previousPedal)continue;
            previousPedal=e.Value>=64;
            var sample=PcmWave.Read(Path.Combine(directory,e.Value>=64?manifest.PedalDown:manifest.PedalUp));
            Mix(sample,e.Frame,(int)((long)sample.Frames*rate/sample.SampleRate),0.025f);
        }
        var length=Math.Min(buffer.Length, (end+rate/4)*2); Array.Resize(ref buffer,length);
        var peak=buffer.Max(v=>Math.Abs(v)); var gain=peak>0.9f?0.9f/peak:1f;
        for(var i=0;i<buffer.Length;i++) buffer[i]*=gain*Math.Min(1f,(buffer.Length-1-i)/(rate*0.04f));
        return buffer;
        void Mix(PcmWave source,int start,int frames,float gain,int release=int.MaxValue,int releaseFrames=1)
        {
            frames=Math.Min(frames,buffer.Length/2-start); end=Math.Max(end,start+frames);
            for(var f=0;f<frames;f++)
            {
                if((f&8191)==0)token.ThrowIfCancellationRequested();
                var sourcePosition=(double)f*source.SampleRate/rate; var a=(int)sourcePosition; var b=Math.Min(a+1,source.Frames-1); var fraction=(float)(sourcePosition-a);
                var envelope=start+f<release?1f:(float)Math.Pow(Math.Max(0,1.0-(start+f-release)/(double)releaseFrames),3);
                for(var c=0;c<2;c++)buffer[(start+f)*2+c]+=(source.Samples[a*2+c]+(source.Samples[b*2+c]-source.Samples[a*2+c])*fraction)*gain*envelope;
            }
        }
    }
}
