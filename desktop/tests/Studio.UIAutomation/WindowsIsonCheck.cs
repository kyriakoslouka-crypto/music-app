using System.Net.Http;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Studio.Audio;
using Studio.Windows;
internal static class WindowsIsonCheck
{
    public static async Task<int> Run(string directory,string report)
    {
        Directory.CreateDirectory(directory);var checks=new List<object>();string? failure=null;var rendered=0;
        try{
            using var client=new HttpClient{Timeout=TimeSpan.FromMinutes(2)};
            var specs=new[]{
                (Path:"voices_mmm/a3-hum.mp3",Sha:"cb149b2c4722b0c70f37dbb43c02e728ebc88b90187135c2badc38f91d88077d",Hz:110.0),
                (Path:"voices_new/karampasis_sf_file/sample-00-midi-43.mp3",Sha:"4b8d2698f45a9b5acd3a5af771a9191f339dd483b46ecffb19a14a28606b77c0",Hz:97.99885899543733),
                (Path:"voices_pa3/a3_ison3.mp3",Sha:"79ede9803cd1a0d13111884d464560ece7b8e5b49fcc0904c0e13d2408d0c23a",Hz:110.0),
                (Path:"voices_pa3/a4_ison2.mp3",Sha:"8e9d1041d14a7cb985337bcc0d85e3c593de136e39456ef622523f1719c1aa7b",Hz:220.0)};
            var zipPath=Path.Combine(directory,"private-samples.zip");
            using(var zip=ZipFile.Open(zipPath,ZipArchiveMode.Create)){
                foreach(var spec in specs){var bytes=await client.GetByteArrayAsync("https://isokratis-nek.netlify.app/"+spec.Path);if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(spec.Sha,StringComparison.OrdinalIgnoreCase))throw new Exception("Source sample hash mismatch: "+spec.Path);using var entry=zip.CreateEntry(spec.Path).Open();entry.Write(bytes);}
                using(var writer=new StreamWriter(zip.CreateEntry("DOWNLOAD-REPORT.json").Open()))writer.Write(JsonSerializer.Serialize(new{Files=specs.Select(s=>new{Status="Downloaded",Path=s.Path,SHA256=s.Sha})}));
                using(var writer=new StreamWriter(zip.CreateEntry("metadata/source.txt").Open()))writer.Write("const sets = "+JsonSerializer.Serialize(new[]{new{voices=new[]{new{id="native-test",name="Δοκιμή φωνής",samples=new[]{new{url=specs[1].Path,root=specs[1].Hz}}}}}})+";");
            }
            var manifest=IsonBank.Import([zipPath],Path.Combine(directory,"bank"),IsonDecoder.Decode);var bank=IsonBank.Load(manifest);
            if(bank.Samples.Length!=4)throw new Exception("Missing imported samples");
            foreach(var sample in bank.Samples){
                var wave=IsonBank.ReadSample(Path.GetDirectoryName(manifest)!,sample);if(wave.SampleRate!=44100||wave.Frames<44100)throw new Exception("Native MP3 decode/resample failed");
                var loop=IsonRenderer.Render(wave,sample.DeclaredHz,sample.DeclaredHz*Math.Pow(2,2.0/72),0.45,sample.SourcePath);
                if(loop.Samples.Length%2!=0||loop.Samples.Length<44100||loop.Samples.Any(x=>!float.IsFinite(x))||loop.Samples.Max(Math.Abs)>0.383||!loop.Samples.Any(x=>Math.Abs(x)>0.001))throw new Exception("Invalid continuous ison PCM");
                if(Math.Abs(loop.Samples[0]-loop.Samples[^2])>0.15||Math.Abs(loop.Samples[1]-loop.Samples[^1])>0.15)throw new Exception("Excessive loop seam jump");
                rendered++;checks.Add(new{sample.SourcePath,NativeDecodePassed=true,Frames=wave.Frames,loop.EstimatedRootHz,loop.Confidence,LoopFrames=loop.Samples.Length/2});
            }
        }catch(Exception ex){failure=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(report,JsonSerializer.Serialize(new{Version=typeof(IsonBank).Assembly.GetName().Version!.ToString(3),SourceCommit=Environment.GetEnvironmentVariable("GITHUB_SHA"),Passed=failure is null,Error=failure,DecodedFormats=rendered,Checks=checks,HumanListeningTested=false,PhysicalAudioTested=false,PrivateSamplesBundled=false},new JsonSerializerOptions{WriteIndented=true}));
        return failure is null?0:1;
    }
}
