using Studio.Core;
namespace Studio.Audio;
public sealed class PlaybackCache
{
    private string? id;private Settings? settings;private float[]? samples;
    public void Store(string exerciseId,Settings configuration,float[] pcm){id=exerciseId;settings=configuration;samples=pcm;}
    public float[]? Get(string? exerciseId,Settings configuration)=>exerciseId==id&&configuration==settings?samples:null;
    public void Clear(){id=null;settings=null;samples=null;}
}
