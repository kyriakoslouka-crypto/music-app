using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace Studio.Core;
public sealed record RhythmInputGroup(string[] Pitches,int Count,double UnitTicks,bool Rest);
// Keep the historic tick unit (one sixteenth). Dyadic doubles represent 32nds/64ths exactly.
public static class RhythmInput
{
    public static readonly IReadOnlyDictionary<string,double> Durations=new Dictionary<string,double>{
        ["1/16"]=1,["1/8"]=2,["1/8."]=3,["1/4"]=4,["1/4."]=6,["1/2"]=8,["1/2."]=12,["1/1"]=16,
        ["1/32"]=0.5,["1/64"]=0.25,["1/16."]=1.5,["1/32."]=0.75,["1/64."]=0.375};
    private static readonly Regex Group=new(@"^(?<pitches>(?:(?<pitch>ντο|σολ|ρε|μι|φα|λα|σι)[#b♯♭]?)*)(?<value>[0-9].*)$",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    private static readonly Regex Fraction=new(@"^(?<n>[0-9]{1,3})/(?<d>1|2|4|8|16|32|64)(?<dot>\.)?$",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    public static string Normalize(string text)=>string.Concat(text.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC).ToLowerInvariant();
    public static string[] Tokens(string text)
    {
        if(string.IsNullOrWhiteSpace(text)||text.Length>16384)throw new ArgumentException("Γράψε ρυθμικές αξίες, π.χ. 2/4 8/32 16/64.");
        var tokens=text.Replace(',',' ').Replace(';',' ').Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
        if(tokens.Length is <1 or >2048)throw new ArgumentException("Γράψε από 1 έως 2048 ομάδες αξιών.");
        return tokens;
    }
    private static (int Count,double Unit) FractionValue(string value)
    {
        var m=Fraction.Match(value);
        if(!m.Success||!int.TryParse(m.Groups["n"].Value,NumberStyles.None,CultureInfo.InvariantCulture,out var n)||n is <1 or >128)
            throw new ArgumentException("Άγνωστη αξία. Χρησιμοποίησε πλήθος/αξία, π.χ. 8/32 ή 16/64 (έως 128 αξίες ανά ομάδα).");
        var denominator=int.Parse(m.Groups["d"].Value,CultureInfo.InvariantCulture);
        return(n,16.0/denominator*(m.Groups["dot"].Success?1.5:1));
    }
    public static double SingleDuration(string value)
    {
        var parts=Normalize(value).Split('+');if(parts.Length>128)throw new ArgumentException("Υπερβολικά πολλές δεμένες αξίες.");
        return parts.Sum(part=>{var (count,unit)=FractionValue(part);return count*unit;});
    }
    public static RhythmInputGroup ParseGroup(string token)
    {
        var normalized=Normalize(token);var rest=false;
        if(normalized.StartsWith("παυση",StringComparison.Ordinal)){rest=true;normalized=normalized[5..];}
        else if(normalized.StartsWith('r')||normalized.StartsWith('π')){rest=true;normalized=normalized[1..];}
        var match=Group.Match(normalized);
        if(!match.Success)throw new ArgumentException("Γράψε αξίες όπως 2/4, μιφα2/8, φασολλασι4/16, 8/32, 16/64. Παύση: π1/8.");
        var names=Regex.Matches(match.Groups["pitches"].Value,@"(?:ντο|σολ|ρε|μι|φα|λα|σι)[#b♯♭]?").Select(m=>m.Value).ToArray();
        if(rest&&names.Length!=0)throw new ArgumentException("Η παύση γράφεται χωρίς όνομα νότας, π.χ. π1/8.");
        var value=match.Groups["value"].Value;
        if(value.Contains('+')){
            if(names.Length>1)throw new ArgumentException("Η σύζευξη + ενώνει αξίες μίας νότας. Γράψε τις υπόλοιπες νότες χωριστά.");
            return new(names,1,SingleDuration(value),rest);
        }
        var (count,unit)=FractionValue(value);
        if(names.Length>1&&names.Length!=count)throw new ArgumentException($"Δόθηκαν {names.Length} ονόματα για {count} αξίες. Το πλήθος πρέπει να συμφωνεί.");
        return new(names,count,unit,rest);
    }
    public static string FormatDuration(double ticks)
    {
        if(!double.IsFinite(ticks)||ticks<=0||ticks>2048)throw new ArgumentException("Μη έγκυρη διάρκεια.");
        foreach(var pair in Durations)if(pair.Value==ticks)return pair.Key;
        // Keep a sustained value as a tie, never as n/d (which means n separate attacks).
        var remaining=ticks;var values=new List<string>();
        foreach(var pair in Durations.OrderByDescending(p=>p.Value))while(remaining>=pair.Value){values.Add(pair.Key);remaining-=pair.Value;if(values.Count>128)throw new ArgumentException("Υπερβολική διάρκεια.");}
        if(remaining!=0)throw new ArgumentException("Η διάρκεια δεν γράφεται με αξίες έως εξηκοστά τέταρτα.");
        return string.Join('+',values);
    }
}
