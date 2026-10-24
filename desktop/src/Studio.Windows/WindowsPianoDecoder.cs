using NAudio.Wave;
using Studio.Audio;
namespace Studio.Windows;
public static class WindowsPianoDecoder
{
    private sealed record Fingerprint(long Frames,string Sha256);
    private sealed record References(string SourceCommit,string ArchiveSha256,Dictionary<string,Fingerprint> Samples);
    private static readonly Lazy<References> Reference=new(()=>{
        using var stream=typeof(WindowsPianoDecoder).Assembly.GetManifestResourceStream("Studio.Windows.MaestroPcmFingerprints.json")??throw new InvalidDataException("Λείπουν τα αποτυπώματα PCM.");
        var data=System.Text.Json.JsonSerializer.Deserialize<References>(stream)??throw new InvalidDataException("Μη έγκυρα αποτυπώματα PCM.");
        if(data.SourceCommit!=MaestroLibrary.Commit||data.ArchiveSha256!=MaestroLibrary.ArchiveSha256||data.Samples.Count!=794)throw new InvalidDataException("Μη αναμενόμενη πηγή αποτυπωμάτων PCM.");
        return data;
    });
    public static DecodedPcmEvidence Decode(string input,string output,CancellationToken token)
    {
        using(var reader=new MediaFoundationReader(input)){
            if(reader.WaveFormat.Channels!=2||reader.WaveFormat.BitsPerSample!=16||reader.WaveFormat.SampleRate!=44100)throw new InvalidDataException("Απαιτείται stereo 44.1 kHz PCM16.");
            // Canonical PCM16 header: don't carry an OS-specific WAVEFORMATEXTENSIBLE
            // wrapper into the sample library. The actual audio bytes are unchanged.
            using var writer=new WaveFileWriter(output,new WaveFormat(44100,16,2));
            var buffer=new byte[65536];int read;
            while((read=reader.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();writer.Write(buffer,0,read);}
        }
        if(!Reference.Value.Samples.TryGetValue(Path.GetFileName(output),out var expected))throw new InvalidDataException("Μη αναμενόμενο όνομα δείγματος Maestro.");
        var evidence=FlacPcmIntegrity.Verify(input,output,expected.Sha256);
        if(evidence.Frames!=expected.Frames)throw new InvalidDataException("Διαφορετικό πλήθος PCM frames από την ανεξάρτητη αναφορά.");
        return evidence;
    }
}
