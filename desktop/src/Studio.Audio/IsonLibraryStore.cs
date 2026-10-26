namespace Studio.Audio;
public sealed record InstalledIson(string Manifest,IsonBank Bank,bool Recovered);
public sealed class IsonLibraryStore(string directory)
{
    private string Pointer=>Path.Combine(directory,"isokratis-current.txt");
    private string Banks=>Path.Combine(directory,"isokratis-banks");
    public void Activate(string manifest){_=IsonBank.Load(manifest);Directory.CreateDirectory(directory);var temporary=Pointer+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temporary,Path.GetRelativePath(directory,manifest));File.Move(temporary,Pointer,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
    public InstalledIson? Load()
    {
        if(File.Exists(Pointer))try{var path=File.ReadAllText(Pointer).Trim();var manifest=Path.GetFullPath(Path.IsPathRooted(path)?path:Path.Combine(directory,path));return new(manifest,IsonBank.Load(manifest),false);}catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException){}
        if(!Directory.Exists(Banks))return null;
        foreach(var folder in new DirectoryInfo(Banks).EnumerateDirectories().OrderByDescending(d=>d.LastWriteTimeUtc).Take(128))try{var manifest=Path.Combine(folder.FullName,"isokratis-bank.json");var bank=IsonBank.Load(manifest);Activate(manifest);return new(manifest,bank,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException){}
        return null;
    }
}
