using System.Text;
namespace Studio.Audio;
public sealed record PcmWave(int SampleRate, float[] Samples)
{
    public int Frames => Samples.Length / 2;
    public static PcmWave Read(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        string Tag() => Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (Tag()!="RIFF") throw new InvalidDataException("Αναμένεται RIFF WAV.");
        _=reader.ReadUInt32(); if(Tag()!="WAVE") throw new InvalidDataException("Μη έγκυρο WAV.");
        ushort format=0,channels=0,bits=0; int rate=0; byte[]? data=null;
        while(reader.BaseStream.Position+8<=reader.BaseStream.Length)
        {
            var tag=Tag(); var size=reader.ReadUInt32(); var next=reader.BaseStream.Position+size+(size&1);
            if(size>128_000_000 || next>reader.BaseStream.Length+1) throw new InvalidDataException("Μη έγκυρο μέγεθος WAV.");
            if(tag=="fmt ") { if(size<16) throw new InvalidDataException("Μη έγκυρο fmt."); format=reader.ReadUInt16();channels=reader.ReadUInt16();rate=reader.ReadInt32();_=reader.ReadInt32();_=reader.ReadUInt16();bits=reader.ReadUInt16(); }
            if(tag=="data") data=reader.ReadBytes((int)size);
            reader.BaseStream.Position=next;
        }
        if(channels!=2 || rate is < 22050 or > 192000 || data is null || !((format==1&&bits==16)||(format==3&&bits==32))) throw new InvalidDataException("Απαιτείται stereo PCM16 ή float32 WAV.");
        if(data.Length==0 || data.Length % (2*bits/8)!=0) throw new InvalidDataException("Ελλιπή stereo frames.");
        var samples=new float[data.Length/(bits/8)];
        for(var i=0;i<samples.Length;i++) samples[i]=bits==16 ? BitConverter.ToInt16(data,i*2)/32768f : BitConverter.ToSingle(data,i*4);
        if(samples.Any(v=>!float.IsFinite(v))) throw new InvalidDataException("Μη πεπερασμένο PCM.");
        return new(rate,samples);
    }
}
