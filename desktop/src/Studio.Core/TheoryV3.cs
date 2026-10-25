using System.Globalization;
using System.Text.RegularExpressions;
namespace Studio.Core;
public sealed record NotatedNote(int Midi,double Ticks);
public static class NoteCodec
{
    private static readonly Dictionary<string,int> Names=new(StringComparer.OrdinalIgnoreCase){["c"]=0,["d"]=2,["e"]=4,["f"]=5,["g"]=7,["a"]=9,["b"]=11,["do"]=0,["re"]=2,["mi"]=4,["fa"]=5,["sol"]=7,["la"]=9,["si"]=11,["ti"]=11,["ντο"]=0,["ρε"]=2,["μι"]=4,["φα"]=5,["σολ"]=7,["λα"]=9,["σι"]=11};
    public static int ParsePitch(string text)
    {
        if(int.TryParse(text,out var midi)){if(midi is <21 or >108)throw new ArgumentException("Η νότα πρέπει να είναι MIDI 21–108.");return midi;}
        var match=Regex.Match(text.Trim(),@"^(ντο|σολ|ρε|μι|φα|λα|σι|sol|do|re|mi|fa|la|si|ti|[a-g])([#b♯♭]?)(-?\d+)$",RegexOptions.IgnoreCase);
        if(!match.Success||!Names.TryGetValue(match.Groups[1].Value,out var pitch))throw new ArgumentException("Νότα όπως 60, Ντο4, C4 ή Ρεb4 απαιτείται.");
        var alteration=match.Groups[2].Value switch{"#" or "♯"=>1,"b" or "♭"=>-1,_=>0};
        if(!int.TryParse(match.Groups[3].Value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var octave)||octave is <0 or >8)
            throw new ArgumentException("Η οκτάβα πρέπει να βρίσκεται στην έκταση του πιάνου, 0–8.");
        midi=(octave+1)*12+pitch+alteration;
        if(midi is <21 or >108)throw new ArgumentException("Η νότα είναι εκτός πιάνου.");return midi;
    }
    public static NotatedNote[] Parse(string text,bool durations)
    {
        var tokens=RhythmInput.Tokens(text);var notes=new List<NotatedNote>();
        foreach(var token in tokens){
            if(durations&&!token.Contains(':')){
                var group=RhythmInput.ParseGroup(token);
                if(group.Rest||group.Pitches.Length==0)throw new ArgumentException("Στη μελωδική απάντηση γράψε και νότες, π.χ. μιφα2/8. Χωρίς οκτάβα χρησιμοποιείται η οκτάβα 4.");
                for(var i=0;i<group.Count;i++)notes.Add(new(ParsePitch(group.Pitches[group.Pitches.Length==1?0:i]+"4"),group.UnitTicks));
            }else{
                var parts=token.Split(':');if(parts.Length!=(durations?2:1))throw new ArgumentException("Γράψε νότα:διάρκεια, π.χ. Ντο4:1/4, ή μιφα2/8 χωρίς οκτάβα.");
                notes.Add(new(ParsePitch(parts[0]),durations?RhythmInput.SingleDuration(parts[1]):4));
            }
            if(notes.Count>128)throw new ArgumentException("Απαιτούνται έως 128 νότες.");
        }
        return notes.ToArray();
    }
    public static string Format(IEnumerable<NotatedNote> notes,bool durations)=>string.Join(' ',notes.Select(n=>n.Midi+(durations?":"+RhythmInput.FormatDuration(n.Ticks):"")));

}
public sealed record FunctionalChord(string Token,int[] Steps,int Inversion,string Function);
public static class Harmony
{
    public static readonly string[] MajorDegrees=["I","ii","iii","IV","V","vi","viio"];
    public static readonly string[] MinorDegrees=["i","iio","III","iv","V","VI","viio"];
    public static string Normalize(string token)=>token.Replace("°","o").Replace("ø","h").Replace("♭","b").Replace("♯","#");
    public static FunctionalChord Resolve(string input,TonalityMode mode)
    {
        var token=Normalize(input);
        if(token.Contains('/'))
        {
            var parts=token.Split('/');if(parts.Length!=2||parts[1].Any(char.IsDigit))throw new ArgumentException("Μη έγκυρη δευτερεύουσα βαθμίδα.");
            var target=Resolve(parts[1],mode);var dominant=Resolve(parts[0],TonalityMode.Major);
            return new(token,dominant.Steps.Select(n=>n+target.Steps[0]).ToArray(),dominant.Inversion,"Δευτερεύουσα δεσπόζουσα λειτουργία");
        }
        if(token is "It+6" or "Fr+6" or "Ger+6")return new(token,token switch{"It+6"=>[-4,0,6],"Fr+6"=>[-4,0,2,6],_=>[-4,0,3,6]},0,"Προδεσπόζουσα αυξημένη έκτη");
        if(token=="N6")token=mode==TonalityMode.Major?"bII6":"bII6";
        var match=Regex.Match(token,@"^(b|#)?(VII|III|VI|IV|II|V|I|vii|iii|vi|iv|ii|v|i)(o|h)?(maj7|64|65|43|42|7|6)?$");
        if(!match.Success)throw new ArgumentException("Άγνωστη βαθμίδα: "+input);
        var roman=match.Groups[2].Value;var degree=roman.ToUpperInvariant()switch{"I"=>0,"II"=>1,"III"=>2,"IV"=>3,"V"=>4,"VI"=>5,_=>6};
        int[] scale=mode==TonalityMode.Major?[0,2,4,5,7,9,11]:[0,2,3,5,7,8,10];
        var quality=match.Groups[3].Value;var figure=match.Groups[4].Value;
        var root=scale[degree]+(match.Groups[1].Value switch{"b"=>-1,"#"=>1,_=>0});
        if(mode==TonalityMode.NaturalMinor&&degree==6&&quality is "o" or "h"&&match.Groups[1].Length==0)root=11;
        var seventh=figure is "7" or "65" or "43" or "42" or "maj7";
        int[] chord=quality switch{
            "o"=>seventh?[0,3,6,9]:[0,3,6],"h"=>[0,3,6,10],
            _=>char.IsUpper(roman[0])?(seventh?[0,4,7,figure=="maj7"?11:10]:[0,4,7]):(seventh?[0,3,7,10]:[0,3,7])};
        var inversion=figure switch{"6" or "65"=>1,"64" or "43"=>2,"42"=>3,_=>0};
        var steps=Theory.Invert(chord,inversion).Select(n=>n+root).ToArray();
        var function=degree switch{0 or 2 or 5=>"Τονική λειτουργία",1 or 3=>"Προδεσπόζουσα λειτουργία",_=>"Δεσπόζουσα λειτουργία"};
        return new(token,steps,inversion,function);
    }
}
public static class TheoryV3
{
    public static Exercise Generate(Settings s,Random random)
    {
        Theory.Validate(s);
        var tonicClass=s.KeyTonic??s.RootPitchClass??0;
        if(s.KeyTonic.HasValue&&s.RootPitchClass.HasValue&&s.KeyTonic!=s.RootPitchClass)throw new ArgumentException("Το τονικό κέντρο και η θεμέλιος πρέπει να συμφωνούν.");
        var roots=Enumerable.Range(s.MinNote,Math.Max(0,s.MaxNote-s.MinNote-11)).Where(n=>n%12==tonicClass).ToArray();
        if(roots.Length==0)throw new ArgumentException("Απαιτείται έκταση τουλάχιστον οκτάβας για την επιλεγμένη τονική.");
        var tonic=roots.OrderBy(n=>Math.Abs(n-(s.MinNote+s.MaxNote)/2)).First();
        if(s.Category is Category.HarmonicDictation or Category.Degrees)
        {
            var major=s.KeyMode==TonalityMode.Major;
            if(s.Category==Category.Degrees)
            {
                var degrees=(major?Harmony.MajorDegrees:Harmony.MinorDegrees).Take(s.Level==Level.Beginner?5:7).ToArray();
                var token=degrees[random.Next(degrees.Length)];var score=HarmonyScore(token,tonic,s.KeyMode,s.MinNote,s.MaxNote,"degree");
                var choices=degrees.Select(d=>new Option(d,d+" — "+Harmony.Resolve(d,s.KeyMode).Function)).ToArray();
                return Create(s,token,choices.Single(c=>c.Id==token).Name,score,choices,AnswerKind.Choice);
            }
            string[] patterns=s.Level switch{
                Level.Beginner=>["I IV V I","I V I"],Level.Elementary=>["I ii V I","I vi IV V I"],
                Level.Intermediate=>["I ii6 V7 I","I vi ii V7 I"],Level.Advanced=>["I IV ii6 V7 I","I vi IV V vi ii6 V7 I"],
                Level.Expert=>["I iii vi ii6 I64 V7 I","I vi ii7 V7 I IV V7 I"],_=>["I iii vi ii7 V7 vi IV ii6 I64 V7 I","I IV viio I vi ii7 V7 I"]};
            var progression=patterns[random.Next(patterns.Length)];
            if(!major)progression=string.Join(' ',progression.Split(' ').Select(token=>token switch{"I"=>"i","I64"=>"i64","ii"=>"iio","ii6"=>"iio6","ii7"=>"iih7","iii"=>"III","IV"=>"iv","vi"=>"VI",_=>token}));
            var harmony=HarmonyScore(progression,tonic,s.KeyMode,s.MinNote,s.MaxNote,"harmony");
            return Create(s,"harmony",progression,harmony,[],AnswerKind.Text);
        }
        var count=s.Level switch{Level.Beginner=>4,Level.Elementary=>6,Level.Intermediate=>8,Level.Advanced=>12,Level.Expert=>16,_=>24};
        var atonal=s.Category==Category.AtonalSolfege;
        var available=Enumerable.Range(s.MinNote,s.MaxNote-s.MinNote+1).Where(n=>atonal||Theory.InKey(n,s with{KeyTonic=tonicClass})).ToArray();
        var position=Array.IndexOf(available,tonic);var notes=new List<NotatedNote>();
        var maxStep=s.Level switch{Level.Beginner=>1,Level.Elementary=>2,Level.Intermediate=>3,Level.Advanced=>4,Level.Expert=>6,_=>12};
        for(var i=0;i<count;i++)
        {
            if(i==0||i==count-1)position=Array.IndexOf(available,tonic);
            else position=Math.Clamp(position+random.Next(-maxStep,maxStep+1),0,available.Length-1);
            var durations=s.Level<=Level.Elementary?new[]{4}:s.Level==Level.Intermediate?new[]{2,4}:new[]{1,2,3,4,6,8};
            notes.Add(new(available[position],durations[random.Next(durations.Length)]));
        }
        var hasDurations=s.Category==Category.MelodyDictation;
        if(!hasDurations)notes=notes.Select(n=>n with{Ticks=4}).ToList();
        var text=NoteCodec.Format(notes,hasDurations);var id=s.Category switch{Category.MelodyDictation=>"melody",Category.MelodyMemory=>"memory",Category.TonalSolfege=>"solfege-tonal",Category.AtonalSolfege=>"solfege-atonal",_=>throw new ArgumentException("Κατηγορία")};
        var melody=MelodyScore(text,tonic,s.KeyMode,id,hasDurations);
        return Create(s,id,text,melody,[],AnswerKind.Text);
    }
    private static Exercise Create(Settings settings,string id,string name,MusicalScore score,Option[] choices,AnswerKind kind)
    {
        var exercise=new Exercise(Guid.NewGuid().ToString("N"),settings.Category,score.Events.Select(n=>n.Midi).ToArray(),id,name,choices,settings.Direction,score,kind);Theory.VerifyExercise(exercise);return exercise;
    }
    private static List<ScoreNote> Context(int tonic,TonalityMode mode)=>new(){new(tonic,0,1,-1),new(tonic+(mode==TonalityMode.Major?4:3),0,1,-1),new(tonic+7,0,1,-1)};
    public static MusicalScore MelodyScore(string text,int tonic,TonalityMode mode,string id,bool durations)
    {
        var parsed=NoteCodec.Parse(text,durations);var notes=id=="solfege-atonal"?new List<ScoreNote>{new(tonic,0,0.8,-1)}:Context(tonic,mode);
        for(var tick=1;tick<4;tick++)notes.Add(new(tonic,tick,0.2,-1,60));
        double beat=4;
        foreach(var n in parsed){notes.Add(new(n.Midi,beat,n.Ticks/4.0*0.85));beat+=n.Ticks/4.0;}
        var prompt=durations?"Ακούγεται τονική συγχορδία, παύση, έπειτα μελωδία. Γράψε Ντο4:1/4 Ρε4:1/32 ή μιφα2/8. Η σύντομη γραφή χωρίς οκτάβα χρησιμοποιεί οκτάβα 4· για άλλες οκτάβες γράψε π.χ. Ντο5:1/64.":"Ακούγεται τονικό πλαίσιο, έπειτα μελωδία. Γράψε νότες με οκτάβα, π.χ. Ντο4 Ρε4 ή 60 62. Στο σολφέζ μπορείς να τραγουδήσεις και να συγκρίνεις· δεν βαθμολογείται μικρόφωνο ή η φωνή σου. Η γραπτή απάντηση χρησιμοποιεί σταθερό ντο.";
        notes.Add(new(tonic,beat,0.2,-1,45));
        return new(notes.ToArray(),beat+0.5,id,tonic,mode,$"Αναφορά: {Theory.NoteName(tonic)}. "+(id=="solfege-atonal"?"Μία νότα αναφοράς αντί τονικής συγχορδίας. ":"")+"Μία μπάρα τεσσάρων παλμών εισάγει τη μελωδία. Το τελικό ασθενές σήμα ορίζει το τέλος της τελευταίας διάρκειας. "+prompt+" Ctrl+Shift+R παίζει τη γραπτή απάντησή σου. Τα πεδία τμήματος ορίζουν ακρόαση μέρους σε χρόνους.",text);
    }
    public static MusicalScore HarmonyScore(string text,int tonic,TonalityMode mode,int minimum,int maximum,string id)
    {
        var tokens=text.Split(' ',StringSplitOptions.RemoveEmptyEntries);if(tokens.Length is <1 or >32)throw new ArgumentException("Μη έγκυρο πλήθος συγχορδιών.");
        var notes=Context(tonic,mode);double beat=2;
        foreach(var token in tokens)
        {
            var chord=Harmony.Resolve(token,mode);var root=tonic;
            while(root+chord.Steps[^1]>maximum)root-=12;
            while(root+chord.Steps[0]<minimum)root+=12;
            if(root+chord.Steps[^1]>maximum)throw new ArgumentException("Η έκταση δεν χωρά τη συγχορδία "+token);
            notes.AddRange(chord.Steps.Select((n,voice)=>new ScoreNote(root+n,beat,1.75,voice)));beat+=2;
        }
        return new(notes.ToArray(),beat,id,tonic,mode,"Ακούγεται τονική συγχορδία ως πλαίσιο και μετά η ακολουθία. Γράψε βαθμίδες χωρισμένες με κενά, π.χ. I ii6 V7 I. Το o σημαίνει ελαττωμένη και h ημιελαττωμένη. Τα πεζά/κεφαλαία δηλώνουν ποιότητα. Δεν πρόκειται για αξιολόγηση αυστηρής τετράφωνης σύνδεσης.",string.Join(' ',tokens.Select(Harmony.Normalize)));
    }
    public static void Verify(Exercise e)
    {
        if(e.Category>Category.MelodyMemory){TheoryV4.Verify(e);return;}
        var score=e.Score??throw new ArgumentException("Λείπει η ακολουθία.");
        if(score.Events.Length==0||score.Events.Length>2048||!double.IsFinite(score.LengthBeats)||score.LengthBeats<=0||score.LengthBeats>256)throw new ArgumentException("Μη έγκυρη διάρκεια.");
        if(!score.Events.Select(n=>n.Midi).SequenceEqual(e.Notes)||score.Events.Any(n=>n.Midi is <21 or >108||!double.IsFinite(n.Beat)||!double.IsFinite(n.Duration)||n.Beat<0||n.Duration<=0||n.Beat+n.Duration>score.LengthBeats||n.Velocity is <1 or >127))throw new ArgumentException("Μη έγκυρη ακολουθία.");
        var text=score.ExpectedText??throw new ArgumentException("Λείπει απάντηση.");
        var expected=e.Category is Category.HarmonicDictation or Category.Degrees?
            HarmonyScore(text,score.Tonic,score.Mode,e.Notes.Min(),e.Notes.Max(),score.DefinitionId):MelodyScore(text,score.Tonic,score.Mode,score.DefinitionId,e.Category==Category.MelodyDictation);
        // Harmony root octave is part of voicing; verify pitch classes, inversion, onset and duration against tokens separately.
        if(e.Category is Category.HarmonicDictation or Category.Degrees)
        {
            var tokens=text.Split(' ');if(e.Category==Category.Degrees&&tokens.Single()!=e.CorrectId)throw new ArgumentException("Λανθασμένη βαθμίδα.");
            for(var i=0;i<tokens.Length;i++)
            {
                var actual=score.Events.Where(n=>n.Beat==2+i*2).OrderBy(n=>n.Voice).ToArray();var chord=Harmony.Resolve(tokens[i],score.Mode);
                if(actual.Length!=chord.Steps.Length)throw new ArgumentException("Πλήθος φωνών συγχορδίας.");
                var offset=actual[0].Midi-chord.Steps[0];if((offset-score.Tonic)%12!=0||!actual.Select(n=>n.Midi).SequenceEqual(chord.Steps.Select(n=>offset+n))||actual.Any(n=>n.Duration!=1.75))throw new ArgumentException("Λανθασμένη βαθμίδα ή αναστροφή.");
            }
            if(score.Events.Count(n=>n.Voice>=0)!=tokens.Sum(t=>Harmony.Resolve(t,score.Mode).Steps.Length)||score.LengthBeats!=2+tokens.Length*2)throw new ArgumentException("Επιπλέον/ελλιπή αρμονικά συμβάντα.");
        }
        else if(!expected.Events.SequenceEqual(score.Events)||expected.LengthBeats!=score.LengthBeats)throw new ArgumentException("Η μελωδία δεν συμφωνεί με την απάντηση.");
        if(e.Category is Category.HarmonicDictation or Category.Degrees && !Context(score.Tonic,score.Mode).SequenceEqual(score.Events.Where(n=>n.Voice<0)))throw new ArgumentException("Λανθασμένο τονικό πλαίσιο.");
    }
    public static GradeResult Grade(Exercise e,string response,Settings settings)
    {
        if(e.Category>Category.MelodyMemory)return TheoryV4.Grade(e,response,settings);
        var score=e.Score!;var expectedText=score.ExpectedText!;string[] expected,actual;
        if(e.Category==Category.HarmonicDictation)
        {
            actual=response.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(Harmony.Normalize).ToArray();if(actual.Length is <1 or >128)throw new ArgumentException("Γράψε τουλάχιστον μία και έως 128 βαθμίδες.");foreach(var token in actual)_=Harmony.Resolve(token,score.Mode);
            expected=expectedText.Split(' ');
        }
        else
        {
            var durations=e.Category==Category.MelodyDictation;
            actual=NoteCodec.Parse(response,durations).Select(n=>$"{n.Midi}:{n.Ticks}").ToArray();expected=NoteCodec.Parse(expectedText,durations).Select(n=>$"{n.Midi}:{n.Ticks}").ToArray();
        }
        var matched=actual.Zip(expected).Count(p=>p.First==p.Second);var count=Math.Max(actual.Length,expected.Length);var percentage=100.0*matched/count;var correct=actual.SequenceEqual(expected);
        var errors=Enumerable.Range(0,expected.Length).Where(i=>i>=actual.Length||actual[i]!=expected[i]).Select(i=>(i+1).ToString());
        return new(correct,percentage,correct?"Ακριβής ακολουθία.":$"Σωστά στοιχεία: {matched} από {count}. Θέσεις προς διόρθωση: {string.Join(", ",errors)}.",string.Join(' ',actual));
    }
    public static MusicalScore Preview(Exercise e,string response,Settings? settings=null)
    {
        if(e.Category>Category.MelodyMemory)return TheoryV4.Preview(e,response,settings);
        if(e.Category==Category.RhythmDictation)return TheoryV2.RhythmScore(response,e.Score!.MeterId,e.Score.Bars,e.Score.Tonic);
        if(e.Category==Category.HarmonicDictation)return HarmonyScore(response,e.Score!.Tonic,e.Score.Mode,settings?.MinNote??21,settings?.MaxNote??108,"preview");
        return MelodyScore(response,e.Score!.Tonic,e.Score.Mode,e.Score.DefinitionId,e.Category==Category.MelodyDictation);
    }
}
public static class ScoreSegments
{
    public static MusicalScore Slice(MusicalScore score,double start,double? end)
    {
        var finish=end??score.LengthBeats;
        if(start<0||!double.IsFinite(start)||!double.IsFinite(finish)||finish>score.LengthBeats||finish<=start)throw new ArgumentException("Το τμήμα πρέπει να βρίσκεται μέσα στη διάρκεια του παραδείγματος.");
        var events=score.Events.Where(n=>n.Beat<finish&&n.Beat+n.Duration>start).Select(n=>n with{Beat=Math.Max(0,n.Beat-start),Duration=Math.Min(n.Beat+n.Duration,finish)-Math.Max(n.Beat,start)}).ToArray();
        if(events.Length==0)throw new ArgumentException("Το επιλεγμένο τμήμα περιέχει μόνο σιωπή.");
        return score with{Events=events,LengthBeats=finish-start};
    }
}
