using NAudio.Wave;
using NAudio.MediaFoundation;
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
            // Codec tests must not depend on mutable recordings hosted by a third-party website.
            // Generate known PCM locally and exercise the real Windows MP3 encoder AND decoder.
            var specs=new List<(string Path,string Sha,double Hz)>();
            await Task.Run(()=>{
                MediaFoundationApi.Startup();
                foreach(var fixture in new[]{(Rate:32000,Channels:1,Bitrate:64000,Hz:110.0),(Rate:44100,Channels:2,Bitrate:128000,Hz:97.99885899543733),(Rate:48000,Channels:1,Bitrate:96000,Hz:110.0),(Rate:44100,Channels:1,Bitrate:128000,Hz:220.0)}){
                    var name=$"fixture-{specs.Count}.mp3";var pcm=System.IO.Path.Combine(directory,name+".wav");var mp3=System.IO.Path.Combine(directory,name);
                    using(var writer=new WaveFileWriter(pcm,new WaveFormat(fixture.Rate,16,fixture.Channels))){
                        for(var i=0;i<fixture.Rate*3;i++){var value=(short)(Math.Sin(2*Math.PI*fixture.Hz*i/fixture.Rate)*10000);for(var channel=0;channel<fixture.Channels;channel++)writer.Write(BitConverter.GetBytes(value),0,2);}
                    }
                    using(var reader=new WaveFileReader(pcm))MediaFoundationEncoder.EncodeToMp3(reader,mp3,fixture.Bitrate);
                    var bytes=File.ReadAllBytes(mp3);if(bytes.Length<1000)throw new InvalidDataException("Native MP3 fixture encoding failed");
                    specs.Add(("fixtures/"+name,Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),fixture.Hz));
                }
            });
            var zipPath=Path.Combine(directory,"private-samples.zip");
            using(var zip=ZipFile.Open(zipPath,ZipArchiveMode.Create)){
                foreach(var spec in specs){var bytes=File.ReadAllBytes(System.IO.Path.Combine(directory,System.IO.Path.GetFileName(spec.Path)));if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(spec.Sha,StringComparison.OrdinalIgnoreCase))throw new Exception("Source sample hash mismatch: "+spec.Path);using var entry=zip.CreateEntry(spec.Path).Open();entry.Write(bytes);}
                using(var writer=new StreamWriter(zip.CreateEntry("DOWNLOAD-REPORT.json").Open()))writer.Write(JsonSerializer.Serialize(new{Files=specs.Select(s=>new{Status="Downloaded",Path=s.Path,SHA256=s.Sha})}));
                using(var writer=new StreamWriter(zip.CreateEntry("metadata/source.txt").Open()))writer.Write("const sets = "+JsonSerializer.Serialize(new[]{new{voices=new[]{new{id="native-test",name="Δοκιμή φωνής",samples=specs.Select(s=>new{url=s.Path,root=s.Hz}).ToArray()}}}})+";");
            }
            var manifest=IsonBank.Import([zipPath],Path.Combine(directory,"bank"),IsonDecoder.Decode);var bank=IsonBank.Load(manifest);
            if(bank.Samples.Length!=4)throw new Exception("Missing imported samples");
            foreach(var sample in bank.Samples){
                var wave=IsonBank.ReadSample(Path.GetDirectoryName(manifest)!,sample);if(wave.SampleRate!=44100||wave.Frames<44100)throw new Exception("Native MP3 decode/resample failed");
                var loop=IsonRenderer.Render(wave,sample.DeclaredHz,sample.DeclaredHz*Math.Pow(2,2.0/72),0.45,sample.SourcePath);
                var outputPitch=IsonRenderer.EstimateRoot(new PcmWave(44100,loop.Samples),loop.TargetHz);
                if(loop.Confidence<0.8||outputPitch.Confidence<0.8||Math.Abs(1200*Math.Log2(loop.EstimatedRootHz/sample.DeclaredHz))>5||Math.Abs(1200*Math.Log2(outputPitch.Hz/loop.TargetHz))>5)throw new Exception("Native MP3 pitch roundtrip failed");
                if(loop.Samples.Length%2!=0||loop.Samples.Length<44100||loop.Samples.Any(x=>!float.IsFinite(x))||loop.Samples.Max(Math.Abs)>0.383||!loop.Samples.Any(x=>Math.Abs(x)>0.001))throw new Exception("Invalid continuous ison PCM");
                if(Math.Abs(loop.Samples[0]-loop.Samples[^2])>0.15||Math.Abs(loop.Samples[1]-loop.Samples[^1])>0.15)throw new Exception("Excessive loop seam jump");
                rendered++;checks.Add(new{sample.SourcePath,NativeDecodePassed=true,Frames=wave.Frames,loop.EstimatedRootHz,loop.Confidence,OutputHz=outputPitch.Hz,PitchAccuracyPassed=true,LoopFrames=loop.Samples.Length/2});
            }
        }catch(Exception ex){failure=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(report,JsonSerializer.Serialize(new{Version=typeof(IsonBank).Assembly.GetName().Version!.ToString(3),SourceCommit=Environment.GetEnvironmentVariable("GITHUB_SHA"),Passed=failure is null,Error=failure,DecodedFormats=rendered,FixtureOrigin="Generated PCM encoded by Windows Media Foundation",ExternalVoiceRecordingsTested=false,Checks=checks,HumanListeningTested=false,PhysicalAudioTested=false,PrivateSamplesBundled=false},new JsonSerializerOptions{WriteIndented=true}));
        return failure is null?0:1;
    }
}
