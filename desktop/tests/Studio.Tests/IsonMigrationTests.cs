using Studio.Audio;
using System.Text.Json;
namespace Studio.Tests;
public static class IsonMigrationTests
{
    public static void Register(Action<string,Action> test)
    {
        test("Application split: migration adopts voices and copies tuning without changing legacy files",()=>{
            var root=Path.Combine(Path.GetTempPath(),"ison-migration-"+Guid.NewGuid().ToString("N"));var old=Path.Combine(root,"old");var current=Path.Combine(root,"new");
            try{
                var bank=Path.Combine(old,"isokratis-banks","existing");Directory.CreateDirectory(bank);var sha=new string('a',64);var wave=Path.Combine(bank,sha+".wav");File.WriteAllBytes(wave,[1,2,3,4]);
                var manifest=Path.Combine(bank,"isokratis-bank.json");File.WriteAllText(manifest,JsonSerializer.Serialize(new IsonBank(1,[new("voice","Φωνή","source.mp3",sha+".wav",130,sha,sha)])));new IsonLibraryStore(old).Activate(manifest);
                var settings=Path.Combine(old,"isokratis-settings.json");const string original="{\"Alterations\":[0,90.5,0,0,0,0,0]}";File.WriteAllText(settings,original);
                IsonMigration.Adopt(old,current);var installed=new IsonLibraryStore(current).Load();
                if(installed?.Manifest!=manifest||File.ReadAllText(Path.Combine(current,"isokratis-settings.json"))!=original||File.ReadAllText(settings)!=original||!File.ReadAllBytes(wave).SequenceEqual(new byte[]{1,2,3,4}))throw new Exception("Migration changed or lost original data");
                File.WriteAllText(Path.Combine(current,"isokratis-settings.json"),"{\"personal\":true}");IsonMigration.Adopt(old,current);
                if(File.ReadAllText(Path.Combine(current,"isokratis-settings.json"))!="{\"personal\":true}")throw new Exception("Repeated migration overwrote personal settings");
            }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
        });
        test("Application split: invalid legacy settings are retained and never installed",()=>{
            var root=Path.Combine(Path.GetTempPath(),"ison-invalid-migration-"+Guid.NewGuid().ToString("N"));var old=Path.Combine(root,"old");var current=Path.Combine(root,"new");
            try{Directory.CreateDirectory(old);File.WriteAllText(Path.Combine(old,"isokratis-settings.json"),"[]");try{IsonMigration.Adopt(old,current);throw new Exception("Invalid tuning accepted");}catch(InvalidDataException){}if(File.Exists(Path.Combine(current,"isokratis-settings.json"))||File.ReadAllText(Path.Combine(old,"isokratis-settings.json"))!="[]")throw new Exception("Invalid migration changed files");}
            finally{if(Directory.Exists(root))Directory.Delete(root,true);}
        });
    }
}
