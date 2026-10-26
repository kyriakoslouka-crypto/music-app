using System.Text.Json;
namespace Studio.Audio;
/// <summary>Non-destructive adoption of existing user recordings; never moves or removes legacy data.</summary>
public static class IsonMigration
{
    public static void Adopt(string legacy,string destination)
    {
        Directory.CreateDirectory(destination);
        var current=new IsonLibraryStore(destination);
        if(current.Load() is null&&new IsonLibraryStore(legacy).Load() is { } old)current.Activate(old.Manifest);
        var settings=Path.Combine(destination,"isokratis-settings.json");var previous=Path.Combine(legacy,"isokratis-settings.json");
        if(!File.Exists(settings)&&File.Exists(previous)){
            if(new FileInfo(previous).Length>1024*1024)throw new InvalidDataException("Υπερβολικά μεγάλο αρχείο παλιών ρυθμίσεων.");
            var text=File.ReadAllText(previous);using var json=JsonDocument.Parse(text);
            if(json.RootElement.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Μη έγκυρο αρχείο παλιών ρυθμίσεων.");
            var temporary=settings+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temporary,text);File.Move(temporary,settings,false);}finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
