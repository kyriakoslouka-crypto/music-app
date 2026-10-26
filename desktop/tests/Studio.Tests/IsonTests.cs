using Studio.Audio;
using Studio.Core;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;
namespace Studio.Tests;
public static class IsonTests
{
    static void Check(bool value,string text="Ison assertion failed"){if(!value)throw new Exception(text);}
    static void Reject<T>(Action action)where T:Exception{try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
    public static PcmWave Tone(double hz,int rate=44100){var samples=new float[rate*6];for(var n=0;n<samples.Length/2;n++)samples[n*2]=samples[n*2+1]=(float)(0.3*Math.Sin(2*Math.PI*hz*n/rate)+0.05*Math.Sin(4*Math.PI*hz*n/rate));return new(rate,samples);}
    public static void WriteTone(string path){var wave=Tone(109.2);using var writer=new BinaryWriter(File.Create(path));writer.Write("RIFF"u8);writer.Write(36+wave.Samples.Length*2);writer.Write("WAVEfmt "u8);writer.Write(16);writer.Write((short)1);writer.Write((short)2);writer.Write(44100);writer.Write(44100*4);writer.Write((short)4);writer.Write((short)16);writer.Write("data"u8);writer.Write(wave.Samples.Length*2);foreach(var x in wave.Samples)writer.Write((short)(x*32767));}
    public static void Register(Action<string,Action> test,string[] args)
    {
        test("Ison: independent 72-moria octave and diatonic frequencies",()=>{
            Check(Math.Abs(ByzantineTuning.Frequency(130,7,0,ByzantineTuning.Diatonic)-260)<1e-9);
            Check(Math.Abs(ByzantineTuning.Frequency(130,1,0,ByzantineTuning.Diatonic)-130*Math.Pow(2,1.0/6))<1e-9);
            Check(Math.Abs(1200*Math.Log2(ByzantineTuning.Frequency(130,0,1,ByzantineTuning.Diatonic)/130)-1200.0/72)<1e-9);
            Reject<ArgumentException>(()=>ByzantineTuning.Parse("12 10 8 12 12 10 7"));Reject<ArgumentException>(()=>ByzantineTuning.Parse("NaN 10 8 12 12 10 8"));
        });
        test("Ison: pitch estimate and microtonal resampling at 44.1/48 kHz",()=>{
            foreach(var rate in new[]{44100,48000}){
                var wave=Tone(109.2,rate);var root=IsonRenderer.EstimateRoot(wave,110);Check(Math.Abs(root.Hz-109.2)<0.1&&root.Confidence>0.99);
                var target=110*Math.Pow(2,4.0/72);var loop=IsonRenderer.Render(wave,110,target,0.5,"synthetic");
                var estimate=IsonRenderer.EstimateRoot(new(44100,loop.Samples),target);Check(Math.Abs(estimate.Hz-target)<0.25);
                Check(loop.Samples.All(float.IsFinite)&&loop.Samples.Max(Math.Abs)<0.426&&loop.Samples.Length%2==0);
                Check(Math.Abs(loop.Samples[0]-loop.Samples[^2])<0.02,"Loop seam discontinuity");
            }
        });
        test("Ison: volume, cancellation, range and silent sample guards",()=>{
            var wave=Tone(110);var a=IsonRenderer.Render(wave,110,110,1,"synthetic");var b=IsonRenderer.Render(wave,110,110,0.25,"synthetic");Check(a.Samples.Zip(b.Samples).All(x=>Math.Abs(x.First*0.25-x.Second)<1e-7));
            Reject<ArgumentException>(()=>IsonRenderer.Render(wave,110,440,1,"synthetic"));Reject<InvalidDataException>(()=>IsonRenderer.Render(new(44100,new float[44100*4]),110,110,1,"silent"));
            Reject<OperationCanceledException>(()=>IsonRenderer.Render(wave,110,110,1,"cancel",new CancellationToken(true)));
        });
        test("Ison: ZIP import hashes, save/reload, decoded integrity and rollback",()=>{
            var root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try{
                var bytes=new byte[]{1,2,3,4};var sha=Convert.ToHexString(SHA256.HashData(bytes));var zip=Path.Combine(root,"bank.zip");
                void Make(string hash,string path="voices_pa3/a3_ison1.mp3") {using var archive=ZipFile.Open(zip,ZipArchiveMode.Create);using(var stream=archive.CreateEntry(path).Open())stream.Write(bytes);using var writer=new StreamWriter(archive.CreateEntry("DOWNLOAD-REPORT.json").Open());writer.Write(JsonSerializer.Serialize(new{Files=new[]{new{Status="Downloaded",Path="voices_pa3/a3_ison1.mp3",SHA256=hash}}}));}
                Make(sha);var dest=Path.Combine(root,"valid");var manifest=IsonBank.Import([zip],dest,(_,output,_)=>WriteTone(output));var bank=IsonBank.Load(manifest);Check(bank.Samples.Length==1&&bank.Samples[0].DeclaredHz==110);Check(IsonBank.ReadSample(dest,bank.Samples[0]).Frames>44100);
                Reject<IOException>(()=>IsonBank.Import([zip],dest,(_,_,_)=>{}));Check(File.Exists(manifest));
                File.AppendAllText(Path.Combine(dest,bank.Samples[0].WavePath),"corrupt");Reject<InvalidDataException>(()=>IsonBank.ReadSample(dest,bank.Samples[0]));
                File.Delete(zip);Make(new string('0',64));var failed=Path.Combine(root,"bad");Reject<InvalidDataException>(()=>IsonBank.Import([zip],failed,(_,_,_)=>{}));Check(!Directory.Exists(failed));
                File.Delete(zip);Make(sha,"../escape.mp3");Reject<InvalidDataException>(()=>IsonBank.Import([zip],failed,(_,_,_)=>{}));Check(!File.Exists(Path.Combine(root,"escape.mp3")));
            }finally{Directory.Delete(root,true);}
        });
        var index=Array.IndexOf(args,"--ison-archives");
        if(index>=0&&index+2<args.Length)test("Ison: all 198 private MP3 files import, decode, reload and 12 voice loops",()=>{
            var root=Path.Combine(Path.GetTempPath(),"ison-private-"+Guid.NewGuid().ToString("N"));
            try{
                var manifest=IsonBank.Import([args[index+1],args[index+2]],root,(input,output,token)=>{
                    token.ThrowIfCancellationRequested();var start=new ProcessStartInfo("ffmpeg"){UseShellExecute=false};foreach(var a in new[]{"-nostdin","-hide_banner","-loglevel","error","-i",input,"-ar","44100","-ac","2","-c:a","pcm_s16le",output})start.ArgumentList.Add(a);using var process=Process.Start(start)!;process.WaitForExit();Check(process.ExitCode==0);
                });
                var bank=IsonBank.Load(manifest);Check(bank.Samples.Length==198);Check(bank.Samples.GroupBy(s=>s.VoiceId).Count()==12);
                foreach(var group in bank.Samples.GroupBy(s=>s.VoiceId)) {var sample=IsonRenderer.Select(bank,group.Key,130.813);var wave=IsonBank.ReadSample(root,sample);var loop=IsonRenderer.Render(wave,sample.DeclaredHz,130.813,0.45,sample.SourcePath);Check(loop.Samples.All(float.IsFinite)&&loop.Samples.Max(Math.Abs)<=0.383);}
            }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
        });
    }
}
