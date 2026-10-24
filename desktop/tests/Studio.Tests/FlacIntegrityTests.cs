using Studio.Audio;
namespace Studio.Tests;
internal static class FlacIntegrityTests
{
    private static readonly byte[] Reference=Convert.FromHexString("664c614380000022001000100000000000000ac442f0000000040a23c9ea75d5db77a963e22c19bbf115");
    private static readonly byte[] Pcm=Convert.FromHexString("00000000ff7f0080d2042efb000800f8");
    public static void Register(Action<string,Action> test)
    {
        void WithFiles(Action<string,string> check){
            var root=Path.Combine(Path.GetTempPath(),"pets-flac-metadata-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try{
                var flac=Path.Combine(root,"streaminfo.flac");File.WriteAllBytes(flac,Reference);
                var wave=Path.Combine(root,"decoded.wav");
                using(var writer=new BinaryWriter(File.Create(wave))){
                    writer.Write("RIFF"u8);writer.Write(36+Pcm.Length);writer.Write("WAVEfmt "u8);writer.Write(16);writer.Write((ushort)1);writer.Write((ushort)2);
                    writer.Write(44100);writer.Write(176400);writer.Write((ushort)4);writer.Write((ushort)16);writer.Write("data"u8);writer.Write(Pcm.Length);writer.Write(Pcm);
                }
                check(flac,wave);
            }finally{Directory.Delete(root,true);}
        }
        test("FLAC integrity: independent STREAMINFO/PCM vector verifies lossless frames and fingerprint",()=>WithFiles((flac,wave)=>{
            var result=FlacPcmIntegrity.Verify(flac,wave,"50f2fa3349998974f1e60687d3d2c53abe3315a0adbfcfbbfe5a226896e8acd5");
            if(result.Frames!=4||!result.FingerprintPresent||!result.FingerprintMatched||!result.ReferenceFingerprintMatched)throw new Exception("Reference fingerprint failed");
            var without=Reference.ToArray();Array.Clear(without,26,16);File.WriteAllBytes(flac,without);
            var unknown=FlacPcmIntegrity.Verify(flac,wave,"50f2fa3349998974f1e60687d3d2c53abe3315a0adbfcfbbfe5a226896e8acd5");
            if(unknown.FingerprintPresent||unknown.FingerprintMatched||unknown.Frames!=4||!unknown.ReferenceFingerprintMatched)throw new Exception("Missing fingerprint incorrectly reported as verified");
        }));
        test("FLAC integrity: altered PCM, sample rate and incomplete frames are rejected",()=>WithFiles((flac,wave)=>{
            var original=File.ReadAllBytes(wave);
            void Reject(byte[] bytes){File.WriteAllBytes(wave,bytes);try{FlacPcmIntegrity.Verify(flac,wave);}catch(Exception error)when(error is InvalidDataException or EndOfStreamException){return;}throw new Exception("Corrupt PCM accepted");}
            var tampered=original.ToArray();tampered[^1]^=1;Reject(tampered);
            var resampled=original.ToArray();BitConverter.GetBytes(48000).CopyTo(resampled,24);Reject(resampled);
            Reject(original[..^4]);
        }));
    }
}
