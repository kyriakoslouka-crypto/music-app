using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
namespace Studio.Audio;

public sealed record DecodedPcmEvidence(long Frames,bool FingerprintPresent,bool FingerprintMatched,string PcmSha256,bool ReferenceFingerprintMatched);
public static class FlacPcmIntegrity
{
    // FLAC's STREAMINFO MD5 is an audio-fidelity check, not an authenticity check.
    // Authenticity is separately enforced by the pinned archive SHA-256.
    public static DecodedPcmEvidence Verify(string flac,string wave,string? expectedPcmSha256=null)
    {
        Span<byte> header=stackalloc byte[42];
        using(var input=File.OpenRead(flac))input.ReadExactly(header);
        if(!header[..4].SequenceEqual("fLaC"u8)||(header[4]&127)!=0||header[5]!=0||header[6]!=0||header[7]!=34)throw new InvalidDataException("Μη έγκυρο FLAC STREAMINFO.");
        var packed=BinaryPrimitives.ReadUInt64BigEndian(header[18..26]);
        var rate=(int)(packed>>44);var channels=(int)((packed>>41)&7)+1;var bits=(int)((packed>>36)&31)+1;var frames=(long)(packed&0xFFFFFFFFFUL);
        if(rate!=44100||channels!=2||bits!=16||frames<=0)throw new InvalidDataException("Απαιτείται πηγαίο stereo 44.1 kHz FLAC16.");
        using var reader=new BinaryReader(File.OpenRead(wave));
        string Tag()=>Encoding.ASCII.GetString(reader.ReadBytes(4));
        if(Tag()!="RIFF")throw new InvalidDataException("Μη έγκυρο RIFF.");
        _=reader.ReadUInt32();if(Tag()!="WAVE")throw new InvalidDataException("Μη έγκυρο WAVE.");
        var formatValid=false;
        while(reader.BaseStream.Position+8<=reader.BaseStream.Length){
            var tag=Tag();var length=reader.ReadUInt32();var next=checked(reader.BaseStream.Position+length+(length&1));
            if(next>reader.BaseStream.Length+1)throw new InvalidDataException("Ελλιπές WAV chunk.");
            if(tag=="fmt "){
                if(length<16)throw new InvalidDataException("Ελλιπές WAV fmt.");
                var format=reader.ReadUInt16();var count=reader.ReadUInt16();var sampleRate=reader.ReadInt32();
                _=reader.ReadInt32();var align=reader.ReadUInt16();var depth=reader.ReadUInt16();
                formatValid=format==1&&count==2&&sampleRate==44100&&depth==16&&align==4;
            }
            if(tag=="data"){
                if(!formatValid||length!=frames*4)throw new InvalidDataException("Η αποκωδικοποίηση άλλαξε μορφή ή πλήθος frames.");
                using var hash=IncrementalHash.CreateHash(HashAlgorithmName.MD5);
                using var strongHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer=new byte[65536];long remaining=length;
                while(remaining>0){var read=reader.Read(buffer,0,(int)Math.Min(buffer.Length,remaining));if(read==0)throw new EndOfStreamException();hash.AppendData(buffer,0,read);strongHash.AppendData(buffer,0,read);remaining-=read;}
                var expected=header[26..42];var present=false;foreach(var b in expected)present|=b!=0;
                var matches=present&&hash.GetHashAndReset().AsSpan().SequenceEqual(expected);
                if(present&&!matches)throw new InvalidDataException("Τα PCM δεδομένα δεν συμφωνούν με το πηγαίο FLAC. Η προετοιμασία σταμάτησε.");
                var sha256=Convert.ToHexString(strongHash.GetHashAndReset());
                var referenceMatches=expectedPcmSha256 is not null&&sha256.Equals(expectedPcmSha256,StringComparison.OrdinalIgnoreCase);
                if(expectedPcmSha256 is not null&&!referenceMatches)throw new InvalidDataException("Το αποκωδικοποιημένο PCM διαφέρει από την ανεξάρτητη αναφορά SHA-256.");
                return new(frames,present,matches,sha256,referenceMatches);
            }
            reader.BaseStream.Position=next;
        }
        throw new InvalidDataException("Λείπουν PCM δεδομένα.");
    }
}
