using Studio.Audio;
using Studio.Core;
using System.Security.Cryptography;
using System.Text.Json;
namespace Studio.Tests;
public static class LiveIsonTests
{
    static void Check(bool value,string message="Live ison assertion failed"){if(!value)throw new Exception(message);}
    public static void Register(Action<string,Action> test)
    {
        test("Live ison: eight distinct echos labels and explicit indicative drone bases",()=>{
            Check(ByzantineModes.All.Length==8&&ByzantineModes.All.Select(m=>m.Id).Distinct().Count()==8);Check(ByzantineModes.All.Select(m=>m.Degree).SequenceEqual(new[]{1,4,3,4,1,1,6,0}));Check(ByzantineModes.All.All(m=>m.Degree is >=0 and <=7));
        });
        test("Live ison: smooth latest-request loop replacements preserve stereo and buffer offsets",()=>{
            var cursor=new LiveLoopCursor([0.2f,-0.2f]);cursor.Replace([0.6f,-0.6f]);cursor.Replace([0.8f,-0.8f]);var data=Enumerable.Repeat(99f,6000).ToArray();Check(cursor.Read(data,8,5000)==5000);
            Check(data.Take(8).All(x=>x==99)&&data.Skip(5008).All(x=>x==99));Check(data[8]==0.2f&&data[9]==-0.2f);Check(Math.Abs(data[5006]-0.8f)<1e-6);Check(Enumerable.Range(0,2500).All(i=>Math.Abs(data[8+i*2]+data[9+i*2])<1e-6));
            Check(Enumerable.Range(1,2499).Max(i=>Math.Abs(data[8+i*2]-data[6+i*2]))<0.001);cursor.Replace([0.1f,-0.1f]);cursor.Read(data,0,100);cursor.Replace([0.3f,-0.3f]);cursor.Read(data,0,5000);Check(Math.Abs(data[4998]-0.3f)<1e-6);
        });
        test("Live ison: bounded rendered-loop cache avoids repeated generation and clears on request",()=>{
            var cache=new IsonLoopCache(2);var renders=0;IsonLoop Render(){renders++;return new([0.1f,-0.1f],110,110,1,"test");}var first=cache.Get("a",Render);Check(ReferenceEquals(first,cache.Get("a",Render))&&renders==1);cache.Get("b",Render);cache.Get("c",Render);cache.Get("a",Render);Check(renders==4);cache.Clear();cache.Get("a",Render);Check(renders==5);
        });
        test("Live ison: installed library restores offline and recovers a missing or broken pointer",()=>{
            var root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var bank=Path.Combine(root,"isokratis-banks","one");Directory.CreateDirectory(bank);
            try{var temporary=Path.Combine(bank,"tone.wav");IsonTests.WriteTone(temporary);var sha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(temporary))).ToLowerInvariant();File.Move(temporary,Path.Combine(bank,sha+".wav"));var manifest=Path.Combine(bank,"isokratis-bank.json");File.WriteAllText(manifest,JsonSerializer.Serialize(new IsonBank(1,[new("one","Φωνή","test.mp3",sha+".wav",110,sha,sha)])));
                var store=new IsonLibraryStore(root);store.Activate(manifest);Check(new IsonLibraryStore(root).Load()is{Recovered:false,Bank.Samples.Length:1});File.Delete(Path.Combine(root,"isokratis-current.txt"));Check(store.Load()is{Recovered:true});File.WriteAllText(Path.Combine(root,"isokratis-current.txt"),"missing.json");Check(store.Load()is{Recovered:true});Check(IsonBank.ReadSample(bank,store.Load()!.Bank.Samples[0]).Frames>44100);
                var pointer=File.ReadAllText(Path.Combine(root,"isokratis-current.txt"));Check(!Path.IsPathRooted(pointer),"Library pointer must survive a folder move");
            }finally{Directory.Delete(root,true);}
        });
    }
}
