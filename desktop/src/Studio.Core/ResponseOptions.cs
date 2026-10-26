using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace Studio.Core;
// Presentation adapter: original scores, response graders and saved exams remain unchanged.
public static class ResponseOptions
{
    public static Option[] Create(Exercise exercise,Settings settings)
    {
        if(exercise.AnswerKind!=AnswerKind.Text)return exercise.Choices;
        var expected=exercise.Score!.ExpectedText!;var choices=new List<Option>{new(expected,Describe(exercise,expected))};
        var seed=BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(exercise.Id)));
        var random=new Random(seed);var generator=new ExerciseGenerator(seed);
        var target=exercise.Category is Category.HarmonicDictation or Category.ChromaticHarmony?2:4;
        for(var attempt=0;attempt<48&&choices.Count<target;attempt++){
            var other=generator.Next(settings);var text=other.Score!.ExpectedText!;
            var label=Describe(exercise,text);
            if(choices.Any(c=>c.Name==label)||WrittenGrader.Grade(exercise,text,settings).Correct)continue;
            choices.Add(new(text,label));
        }
        if(choices.Count<2&&exercise.Category==Category.RhythmDictation){
            var meter=TheoryV2.Meters.Single(m=>m.Id==exercise.Score!.MeterId);var bars=RhythmCodec.Parse(expected,meter,exercise.Score!.Bars);
            var first=bars[0][0];bars[0]=first.Ticks>=0.5?[new(first.Ticks/2,first.Rest),new(first.Ticks/2,first.Rest),..bars[0].Skip(1)]:[first with{Rest=!first.Rest},..bars[0].Skip(1)];
            var alternate=RhythmCodec.Format(bars);choices.Add(new(alternate,Describe(exercise,alternate)));
        }
        if(choices.Count<2&&exercise.Category is Category.HarmonicDictation or Category.ChromaticHarmony){
            var tokens=expected.Split(' ');tokens[1]=tokens[1]=="V"?(exercise.Score!.Mode==TonalityMode.Major?"IV":"iv"):"V";
            var alternate=string.Join(' ',tokens);choices.Add(new(alternate,Describe(exercise,alternate)));
        }
        if(choices.Count<2)throw new InvalidOperationException("Δεν βρέθηκαν δύο διακριτές απαντήσεις: "+exercise.Category+" / "+settings.Level+".");
        for(var i=choices.Count-1;i>0;i--){var j=random.Next(i+1);(choices[i],choices[j])=(choices[j],choices[i]);}
        return choices.Select((c,i)=>c with{Name=$"Επιλογή {i+1}. {c.Name}"}).ToArray();
    }
    public static string Instruction(Exercise exercise)=>exercise.Category switch{
        Category.RhythmDictation=>$"Μία μπάρα εισαγωγής ορίζει τον παλμό. Έπειτα ακολουθούν {exercise.Score!.Bars} μέτρα {exercise.Score.MeterId}. Η εισαγωγή δεν περιλαμβάνεται στις επιλογές.",
        Category.HarmonicDictation or Category.ChromaticHarmony=>"Η πρώτη τονική συγχορδία είναι πλαίσιο. Επίλεξε μόνο τη διαδοχή που ακολουθεί μετά από αυτήν.",
        Category.Polyphony2 or Category.Polyphony3 or Category.Polyphony4 or Category.InnerVoice=>$"Τέσσερις παλμοί εισαγωγής και μετά πολυφωνία. Ζητείται η φωνή {exercise.Score!.TargetVoice+1}. Η πρώτη φωνή είναι η ψηλότερη.",
        Category.TwelveToneRows=>"Τέσσερις παλμοί εισαγωγής και μετά δωδεκαφθογγική σειρά, χωρίς τονική συγχορδία.",
        Category.AtonalSolfege=>"Μία νότα αναφοράς, τέσσερις παλμοί εισαγωγής και μετά η μελωδία. Το τελικό ασθενές σήμα δεν ανήκει στην απάντηση.",
        _=>"Τονικό πλαίσιο, τέσσερις παλμοί εισαγωγής και μετά η μελωδία. Το πλαίσιο και το τελικό ασθενές σήμα δεν ανήκουν στην απάντηση."
    };
    public static string Describe(Exercise exercise,string text)
    {
        if(exercise.Category==Category.RhythmDictation){
            var meter=TheoryV2.Meters.Single(m=>m.Id==exercise.Score!.MeterId);
            return string.Join(". ",RhythmCodec.Parse(text,meter,exercise.Score!.Bars).Select((bar,i)=>$"Μέτρο {i+1}: "+string.Join(" · ",bar.Select(c=>(c.Rest?"παύση ":"")+Duration(c.Ticks)))));
        }
        if(exercise.Category is Category.HarmonicDictation or Category.ChromaticHarmony)
            return string.Join(" → ",text.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(t=>Chord(t,exercise.Score!.Mode)));
        var durations=exercise.Category is Category.MelodyDictation or Category.Polyphony2 or Category.Polyphony3 or Category.Polyphony4 or Category.InnerVoice;
        return string.Join(" · ",NoteCodec.Parse(text,durations).Select((n,i)=>$"{i+1}: {Theory.PitchNames[n.Midi%12].Split('/')[0].Trim()}"+(durations?", "+Duration(n.Ticks):"")));
    }
    public static string Duration(double ticks)
    {
        var fraction=RhythmInput.FormatDuration(ticks);
        if(fraction.Contains('+'))return string.Join(" δεμένο με ",fraction.Split('+').Select(p=>Duration(RhythmInput.SingleDuration(p))));
        var dotted=fraction.EndsWith('.');var unit=fraction.TrimEnd('.') switch{"1/1"=>"ολόκληρο","1/2"=>"μισό","1/4"=>"τέταρτο","1/8"=>"όγδοο","1/16"=>"δέκατο έκτο","1/32"=>"τριακοστό δεύτερο","1/64"=>"εξηκοστό τέταρτο",_=>throw new ArgumentException("Άγνωστη διάρκεια.")};
        return unit+(dotted?" παρεστιγμένο":"");
    }
    private static string Chord(string token,TonalityMode mode)
    {
        var chord=Harmony.Resolve(token,mode);
        if(token.Contains('/')){var parts=token.Split('/');return Chord(parts[0],TonalityMode.Major)+" της "+Chord(parts[1],mode);}
        if(token is "It+6" or "Fr+6" or "Ger+6")return token switch{"It+6"=>"Ιταλική αυξημένη έκτη","Fr+6"=>"Γαλλική αυξημένη έκτη",_=>"Γερμανική αυξημένη έκτη"};
        var normalized=token=="N6"?"bII6":Harmony.Normalize(token);
        var match=Regex.Match(normalized,@"^(b|#)?(VII|III|VI|IV|II|V|I|vii|iii|vi|iv|ii|v|i)");var roman=match.Groups[2].Value;
        var degree=roman.ToUpperInvariant() switch{"I"=>"πρώτη","II"=>"δεύτερη","III"=>"τρίτη","IV"=>"τέταρτη","V"=>"πέμπτη","VI"=>"έκτη",_=>"έβδομη"};
        var quality=normalized.Contains('h')?"ημιελαττωμένη":normalized.Contains('o')?"ελαττωμένη":roman==roman.ToUpperInvariant()?"μείζονα":"ελάσσονα";
        var seventh=chord.Steps.Length==4?(normalized.Contains("maj7",StringComparison.Ordinal)?", με μείζονα έβδομη":", με έβδομη"):"";
        return (match.Groups[1].Value switch{"b"=>"χαμηλωμένη ","#"=>"υψωμένη ",_=>""})+degree+" βαθμίδα, "+quality+seventh+", "+Theory.InversionName(chord.Inversion);
    }
}
