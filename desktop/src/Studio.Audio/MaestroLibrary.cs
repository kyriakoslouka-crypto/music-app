using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Studio.Audio;

public sealed record MaestroRegion(int Key, int LowVelocity, int HighVelocity, bool Release, string File, string Sha256, double ReleaseSeconds);
public sealed record MaestroManifest(int Format, string SourceCommit, string SourceSha256, MaestroRegion[] Regions, string PedalDown, string PedalUp);
public sealed record PianoInstallProgress(int Percent, string Message);
public static class MaestroLibrary
{
    public const string Commit = "717078b450eccc4a7733755f4b5973426ebc23e9";
    public const string ArchiveSha256 = "2507c213538b850889bea4fbd90ca2a54abcc32d6a42febae53b81df1f677264";
    public const string DownloadUrl = "https://codeload.github.com/sfzinstruments/MatsHelgesson.MaestroConcertGrandPiano/zip/" + Commit;
    public const string SourceUrl = "https://github.com/sfzinstruments/MatsHelgesson.MaestroConcertGrandPiano/tree/" + Commit;
    public const string ManifestName = "maestro.studio-piano.json";
    public static readonly (string Layer, int Low, int High)[] Layers = [("p",1,29),("mp",30,59),("mf",60,89),("f",90,108),("ff",109,127)];
    public static readonly (string Layer, int Low, int High)[] ReleaseLayers = [("mp",1,31),("mf",32,63),("f",64,95),("ff",96,127)];
    public static async Task DownloadAsync(HttpClient client, string path, IProgress<PianoInstallProgress>? progress, CancellationToken token)
    {
        using var response = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[131072]; long total = 0; int count;
        while ((count = await input.ReadAsync(buffer, token)) != 0)
        {
            total += count;
            if (total > 400_000_000) throw new InvalidDataException("Μη αναμενόμενο μέγεθος λήψης.");
            await output.WriteAsync(buffer.AsMemory(0,count), token);
            progress?.Report(new((int)Math.Min(34, total * 35 / 263_000_000), $"Λήψη πιάνου: {total / 1_000_000} MB από περίπου 263 MB."));
        }
        await output.FlushAsync(token);
    }
    public static string VerifyAndExtract(string archive, string target, CancellationToken token)
    {
        using (var stream = File.OpenRead(archive))
            if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(ArchiveSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Το SHA-256 της βιβλιοθήκης δεν συμφωνεί. Η εγκατάσταση σταμάτησε· δεν παρακάμφθηκε ο έλεγχος.");
        using var zip = ZipFile.OpenRead(archive);
        var root = "MatsHelgesson.MaestroConcertGrandPiano-" + Commit + "/";
        long size = 0;
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            if (!entry.FullName.StartsWith(root, StringComparison.Ordinal)) throw new InvalidDataException("Μη αναμενόμενη δομή βιβλιοθήκης.");
            var relative = entry.FullName[root.Length..];
            if (relative.Length == 0) continue;
            var destination = Path.GetFullPath(Path.Combine(target, relative));
            if (!destination.StartsWith(Path.GetFullPath(target) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidDataException("Μη ασφαλής διαδρομή αρχείου.");
            size += entry.Length; if (size > 1_600_000_000) throw new InvalidDataException("Υπερβολικό μέγεθος αποσυμπίεσης.");
            if (entry.FullName.EndsWith('/')) Directory.CreateDirectory(destination);
            else { Directory.CreateDirectory(Path.GetDirectoryName(destination)!); entry.ExtractToFile(destination, false); }
        }
        return target;
    }
    // Decoder is supplied by the host: Windows Media Foundation (FLAC -> lossless PCM16).
    public static string Prepare(string source, string destination, Action<string,string,CancellationToken> decode,
        IProgress<PianoInstallProgress>? progress, CancellationToken token)
    {
        Directory.CreateDirectory(destination);
        var regions = new List<MaestroRegion>(); var completed = 0;
        void ConvertSample(string name)
        {
            token.ThrowIfCancellationRequested();
            var original = Path.Combine(source,"Samples",name+".flac");
            if (name == "mcg_ff_083") original = Path.Combine(source,"SampleFix",name+".flac");
            decode(original, Path.Combine(destination,name+".wav"), token);
            progress?.Report(new(35 + ++completed * 64 / 794, $"Προετοιμασία δείγματος {completed} από 794."));
        }
        foreach (var (layer,low,high) in Layers)
        {
            var releaseText = File.ReadAllText(Path.Combine(source,"Data",$"rel_{layer}.txt"));
            for (var key=21; key<=108; key++)
            {
                var match = Regex.Match(releaseText, @"\$REL" + key.ToString("000") + @"\s+([\d.]+)");
                if (!match.Success) throw new InvalidDataException("Λείπουν οι παράμετροι φυσικής απόσβεσης.");
                var seconds = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                var name=$"mcg_{layer}_{key:000}"; ConvertSample(name);
                regions.Add(Region(key,low,high,false,name,seconds));
            }
        }
        foreach (var (layer,low,high) in ReleaseLayers) for (var key=21; key<=108; key++)
        {
            var name=$"mcr_{layer}_{key:000}"; ConvertSample(name); regions.Add(Region(key,low,high,true,name,0));
        }
        foreach (var name in new[] {"mcg_e_sp_mf","mcg_e_spr_mf"}) ConvertSample(name);
        File.Copy(Path.Combine(source,"mcgreadme.txt"),Path.Combine(destination,"ORIGINAL-LICENSE.txt"));
        File.Copy(Path.Combine(source,"README.md"),Path.Combine(destination,"ORIGINAL-README.md"));
        var manifest = new MaestroManifest(1,Commit,ArchiveSha256,regions.ToArray(),"mcg_e_sp_mf.wav","mcg_e_spr_mf.wav");
        var path=Path.Combine(destination,ManifestName);
        File.WriteAllText(path,JsonSerializer.Serialize(manifest));
        // Full structural verification before publication of the directory.
        _ = new MaestroPiano(path);
        progress?.Report(new(100,"Το Maestro Concert Grand εγκαταστάθηκε."));
        return path;
        MaestroRegion Region(int key,int low,int high,bool release,string name,double seconds)
        {
            using var stream=File.OpenRead(Path.Combine(destination,name+".wav"));
            return new(key,low,high,release,name+".wav",Convert.ToHexString(SHA256.HashData(stream)),seconds);
        }
    }
}
