namespace Studio.Audio;
// One stereo stream stays open. Replacements crossfade for 20 ms on the audio thread.
public sealed class LiveLoopCursor(float[] pcm)
{
    private readonly object gate=new();
    private LoopingPcmCursor current=new(pcm,true);private LoopingPcmCursor? next;private float[]? pending;private int blended;
    private const int FadeSamples=1764;
    private readonly float[] oldBuffer=new float[FadeSamples],newBuffer=new float[FadeSamples],mix=new float[FadeSamples];
    public void Replace(float[] samples){if(samples.Length==0||samples.Length%2!=0)throw new ArgumentException("Απαιτείται stereo βρόχος.");lock(gate)pending=samples;}
    public int Read(float[] buffer,int offset,int count)
    {
        if(offset<0||count<0||offset>Buffer.ByteLength(buffer)/4||count>Buffer.ByteLength(buffer)/4-offset)throw new ArgumentException("Buffer too small");
        lock(gate){var written=0;while(written<count){
            if(next is null&&pending is not null){next=new(pending,true);pending=null;blended=0;}
            if(next is null){written+=current.Read(buffer,offset+written,count-written);break;}
            var n=Math.Min(count-written,FadeSamples-blended);current.Read(oldBuffer,0,n);next.Read(newBuffer,0,n);
            for(var i=0;i<n;i++){var t=(double)((blended+i)/2)/(FadeSamples/2-1);mix[i]=(float)(oldBuffer[i]*(1-t)+newBuffer[i]*t);}
            Buffer.BlockCopy(mix,0,buffer,(offset+written)*4,n*4);written+=n;blended+=n;
            if(blended==FadeSamples){current=next;next=null;}
        }return written;}
    }
}
public sealed class IsonLoopCache(int capacity=32)
{
    private readonly object gate=new();private readonly Dictionary<string,IsonLoop> entries=[];private readonly Queue<string> order=[];
    public IsonLoop Get(string key,Func<IsonLoop> render){lock(gate)if(entries.TryGetValue(key,out var existing))return existing;var result=render();lock(gate){if(entries.TryGetValue(key,out var existing))return existing;while(entries.Count>=Math.Max(1,capacity))entries.Remove(order.Dequeue());entries[key]=result;order.Enqueue(key);return result;}}
    public void Clear(){lock(gate){entries.Clear();order.Clear();}}
}
