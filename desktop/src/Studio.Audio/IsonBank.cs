using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Studio.Audio;
public sealed record IsonSample(string VoiceId,string VoiceName,string SourcePath,string WavePath,double DeclaredHz,string SourceSha256,string WaveSha256);
public sealed record IsonBank(int Schema,IsonSample[] Samples)
{
    public static IsonBank Load(string path){var bank=JsonSerializer.Deserialize<IsonBank>(File.ReadAllText(path))??throw new InvalidDataException("Κενή βιβλιοθήκη.");Validate(bank,Path.GetDirectoryName(path)!);return bank;}
    public static PcmWave ReadSample(string directory,IsonSample sample){var path=Path.Combine(directory,sample.WavePath);using(var stream=File.OpenRead(path))if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(sample.WaveSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Αλλοιωμένο αρχείο φωνής. Εισήγαγε ξανά τα ZIP.");return PcmWave.Read(path);}
    private static void Validate(IsonBank bank,string directory){
        if(bank.Schema!=1||bank.Samples is null||bank.Samples.Length is <1 or >1000)throw new InvalidDataException("Μη έγκυρη βιβλιοθήκη φωνών.");
        foreach(var s in bank.Samples){if(s is null||string.IsNullOrWhiteSpace(s.VoiceId)||string.IsNullOrWhiteSpace(s.VoiceName)||s.VoiceName.Length>160||!double.IsFinite(s.DeclaredHz)||s.DeclaredHz is <20 or >4000||!Regex.IsMatch(s.SourceSha256??"",@"^[0-9a-f]{64}$")||!Regex.IsMatch(s.WaveSha256??"",@"^[0-9a-f]{64}$")||s.WavePath!=s.SourceSha256+".wav"||!File.Exists(Path.Combine(directory,s.WavePath)))throw new InvalidDataException("Ελλιπές ή μη ασφαλές δείγμα φωνής.");}
    }
    public static string Import(string[] archives,string destination,Action<string,string,CancellationToken> decode,IProgress<string>? progress=null,CancellationToken token=default)
    {
        if(Directory.Exists(destination))throw new IOException("Απαιτείται νέος φάκελος βιβλιοθήκης.");
        var zips=new List<ZipArchive>();var entries=new Dictionary<string,ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);long total=0;var committed=false;
        try{
            foreach(var file in archives.Distinct(StringComparer.OrdinalIgnoreCase)){
                var zip=ZipFile.OpenRead(file);zips.Add(zip);
                foreach(var entry in zip.Entries){
                    token.ThrowIfCancellationRequested();var name=entry.FullName;
                    if(name.EndsWith('/'))continue;
                    if(name.StartsWith('/')||name.Contains('\\')||name.Contains(':')||name.Split('/').Any(s=>s is ".." or "." or "")||!entries.TryAdd(name,entry))throw new InvalidDataException("Μη ασφαλής ή διπλή διαδρομή ZIP.");
                    total+=entry.Length;if(entry.Length>40_000_000||total>536_870_912||entries.Count>2000)throw new InvalidDataException("Υπέρβαση ορίων ZIP.");
                }
            }
            string Text(string name){if(!entries.TryGetValue(name,out var entry)||entry.Length>8_000_000)throw new InvalidDataException("Λείπουν μεταδεδομένα: "+name);using var reader=new StreamReader(entry.Open(),Encoding.UTF8,true);return reader.ReadToEnd();}
            using var report=JsonDocument.Parse(Text("DOWNLOAD-REPORT.json"));
            var records=report.RootElement.GetProperty("Files").EnumerateArray().Where(r=>r.GetProperty("Status").GetString()=="Downloaded").ToArray();
            if(records.Length is <1 or >1000)throw new InvalidDataException("Δεν περιέχονται επιτυχώς ληφθείσες φωνές.");
            var roots=new Dictionary<string,double>{{"g3",98},{"a3",110},{"b3",123.470825},{"c4",130.812783},{"d4",146.832384},{"e4",164.813778},{"f4",174.614116},{"g4",195.997718},{"a4",220}};
            var extra=new Dictionary<string,(string Id,string Name,double Hz)>(StringComparer.OrdinalIgnoreCase);
            foreach(var name in entries.Keys.Where(n=>n.StartsWith("metadata/",StringComparison.Ordinal)&&n.EndsWith(".txt",StringComparison.Ordinal))){
                var text=Text(name);var marker=Regex.Match(text,@"\bconst\s+sets\s*=\s*",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));if(!marker.Success)continue;
                var reader=new Utf8JsonReader(Encoding.UTF8.GetBytes(text[(marker.Index+marker.Length)..]));using var sets=JsonDocument.ParseValue(ref reader);
                foreach(var set in sets.RootElement.EnumerateArray())foreach(var voice in set.GetProperty("voices").EnumerateArray())foreach(var sample in voice.GetProperty("samples").EnumerateArray()){
                    var url=sample.GetProperty("url").GetString()!;extra[url]=(voice.GetProperty("id").GetString()!,voice.GetProperty("name").GetString()!,sample.GetProperty("root").GetDouble());
                }
            }
            var samples=new List<IsonSample>();Directory.CreateDirectory(destination);
            foreach(var record in records){
                token.ThrowIfCancellationRequested();var path=record.GetProperty("Path").GetString()!;var sha=record.GetProperty("SHA256").GetString()!;
                if(!Regex.IsMatch(sha,@"^[0-9a-fA-F]{64}$")||!entries.TryGetValue(path,out var entry))throw new InvalidDataException("Λείπει δείγμα ή υπογραφή. Επίλεξε όλα τα ZIP του πακέτου.");
                string id,name;double hz;
                var pa=Regex.Match(path,@"^voices_pa3/([a-g][34])_ison([12356])\.mp3$");var hum=Regex.Match(path,@"^voices_mmm/([a-g][34])-hum\.mp3$");
                if(pa.Success){id="pa:"+pa.Groups[2].Value;name=pa.Groups[2].Value switch{"1"=>"Μίξη Basic","2"=>"Γεδεών","3"=>"Δανιήλ","5"=>"Ιώβ",_=>"Σαμουήλ"};hz=roots[pa.Groups[1].Value];}
                else if(hum.Success){id="hum";name="Κλειστό στόμα — Bouche fermée";hz=roots[hum.Groups[1].Value];}
                else if(path.StartsWith("voices_v63/",StringComparison.Ordinal)){id="core";name="Βασικός χορωδιακός ήχος";hz=record.GetProperty("DeclaredRootHz").GetDouble();}
                else if(extra.TryGetValue(path,out var spec)){id=spec.Id;name=spec.Name;hz=spec.Hz;}
                else throw new InvalidDataException("Άγνωστη βάση ή φωνή: "+path);
                if(!double.IsFinite(hz)||hz is <20 or >4000||name.Length>160||id.Length>160)throw new InvalidDataException("Μη έγκυρα στοιχεία φωνής.");
                sha=sha.ToLowerInvariant();var wave=sha+".wav";var output=Path.Combine(destination,wave);
                progress?.Report($"Προετοιμασία φωνών: {samples.Count+1} από {records.Length}. {name}");
                if(!File.Exists(output)){
                    var input=Path.Combine(destination,sha+".mp3");using(var source=entry.Open())using(var target=File.Create(input))source.CopyTo(target);
                    using(var file=File.OpenRead(input))if(!Convert.ToHexString(SHA256.HashData(file)).Equals(sha,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Αποτυχία SHA-256: "+path);
                    decode(input,output,token);_=PcmWave.Read(output);File.Delete(input);
                }else{using var source=entry.Open();if(!Convert.ToHexString(SHA256.HashData(source)).Equals(sha,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Αποτυχία SHA-256.");}
                using var decoded=File.OpenRead(output);var decodedSha=Convert.ToHexString(SHA256.HashData(decoded)).ToLowerInvariant();
                samples.Add(new(id,name,path,wave,hz,sha,decodedSha));
            }
            File.WriteAllText(Path.Combine(destination,"PROVENANCE.txt"),"User-local imported voice recordings. Source: https://isokratis-nek.netlify.app/ — Nektarios Kakolyris and additional voice-library contributors. Original ZIPs remain unchanged. No redistribution license is inferred. Original and decoded SHA-256 values are recorded in isokratis-bank.json.\n");
            var bank=new IsonBank(1,samples.ToArray());Validate(bank,destination);var manifest=Path.Combine(destination,"isokratis-bank.json");File.WriteAllText(manifest,JsonSerializer.Serialize(bank,new JsonSerializerOptions{WriteIndented=true}));token.ThrowIfCancellationRequested();committed=true;return manifest;
        }finally{foreach(var zip in zips)zip.Dispose();if(!committed&&Directory.Exists(destination))Directory.Delete(destination,true);}
    }
}
