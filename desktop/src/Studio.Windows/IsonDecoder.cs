using NAudio.Wave;
namespace Studio.Windows;
public static class IsonDecoder
{
    public static void Decode(string input,string output,CancellationToken token)
    {
        using var reader=new MediaFoundationReader(input);
        if(reader.TotalTime>TotalLimit)throw new InvalidDataException("Το δείγμα ξεπερνά το ένα λεπτό.");
        using var resampler=new MediaFoundationResampler(reader,new WaveFormat(44100,16,2)){ResamplerQuality=60};
        using var writer=new WaveFileWriter(output,new WaveFormat(44100,16,2));var buffer=new byte[65536];int count;long total=0;
        while((count=resampler.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();total+=count;if(total>44100L*4*60)throw new InvalidDataException("Πολύ μεγάλο αποκωδικοποιημένο δείγμα.");writer.Write(buffer,0,count);}
    }
    private static readonly TimeSpan TotalLimit=TimeSpan.FromMinutes(1);
}
