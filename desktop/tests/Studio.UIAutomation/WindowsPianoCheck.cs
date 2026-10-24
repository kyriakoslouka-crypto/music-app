using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Runtime.InteropServices;
using Studio.Audio;
using Studio.Core;
using Studio.Windows;

internal static class WindowsPianoCheck
{
    internal static async Task<int> Run(string directory,string report)
    {
        var decoded=0;var verified=0;var embeddedMd5=0;var rendered=0;string? error=null;string? manifest=null;
        try{
            if(Directory.Exists(directory))throw new IOException("Use a new private piano test directory.");
            Directory.CreateDirectory(directory);
            var archive=Path.Combine(directory,"original.zip");
            using(var client=new HttpClient{Timeout=TimeSpan.FromMinutes(15)})await MaestroLibrary.DownloadAsync(client,archive,null,CancellationToken.None);
            var source=MaestroLibrary.VerifyAndExtract(archive,Path.Combine(directory,"source"),CancellationToken.None);
            manifest=MaestroLibrary.Prepare(source,Path.Combine(directory,"piano"),(input,output,token)=>{
                var result=WindowsPianoDecoder.Decode(input,output,token);decoded++;
                if(result.ReferenceFingerprintMatched)verified++;if(result.FingerprintPresent&&result.FingerprintMatched)embeddedMd5++;
                if(decoded%100==0)Console.WriteLine($"Windows Media Foundation: {decoded}/794 samples decoded and verified");
            },null,CancellationToken.None);
            if(decoded!=794||verified!=794)throw new Exception("Not all 794 source samples matched the independent PCM SHA-256 references.");
            var piano=new MaestroPiano(manifest);var generator=new ExerciseGenerator(422);
            foreach(var category in Enum.GetValues<Category>()){
                var settings=new Settings(category,Level.Intermediate,MinNote:36,MaxNote:96,Tempo:120,Pedal:true);
                var pcm=piano.Render(generator.Next(settings),settings);
                if(pcm.Length==0||pcm.Length%2!=0||pcm.Any(p=>!float.IsFinite(p))||!pcm.Any(p=>Math.Abs(p)>0.001f)||pcm.Max(p=>Math.Abs(p))>0.901f)throw new Exception("Invalid actual Maestro PCM: "+category);
                rendered++;
            }
        }catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(report,JsonSerializer.Serialize(new{
            Passed=error is null,Error=error,Runtime=RuntimeInformation.FrameworkDescription,OperatingSystem=RuntimeInformation.OSDescription,
            SourceCommit=Environment.GetEnvironmentVariable("GITHUB_SHA"),PianoSourceCommit=MaestroLibrary.Commit,PianoArchiveSha256=MaestroLibrary.ArchiveSha256,
            DecodedSamples=decoded,ReferencePcmSha256Matches=verified,EmbeddedFlacMd5Matches=embeddedMd5,CategoriesRendered=rendered,PrivateManifest=manifest,
            SamplesRedistributed=false,NvdaTested=false,PhysicalAudioTested=false,ListeningQualityTested=false
        },new JsonSerializerOptions{WriteIndented=true}));
        return error is null?0:1;
    }
}
