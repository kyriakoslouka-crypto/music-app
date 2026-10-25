using System.Globalization;
namespace Studio.Core;

public sealed record ScaleDefinition(string Id,string Name,int[] Up,int[]? Down=null);
public sealed record MeterDefinition(string Id,string Name,int Numerator,int Denominator,int[] Groups)
{
    public double QuarterBeats=>Numerator*4.0/Denominator;
    public int Ticks=>Numerator*16/Denominator;
    public override string ToString()=>Name;
}
public sealed record RhythmCell(double Ticks,bool Rest=false);
public static class RhythmCodec
{
    public static RhythmCell[][] Parse(string text,MeterDefinition meter,int bars)
    {
        if(string.IsNullOrWhiteSpace(text)||text.Length>16384||bars is <1 or >8)throw new ArgumentException("Γράψε αξίες και χώρισε τα μέτρα με |.");
        var parts=text.Trim().Replace('｜','|').Split('|',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length!=bars)throw new ArgumentException($"Απαιτούνται {bars} μέτρα χωρισμένα με |.");
        return parts.Select(part=>{
            var cells=new List<RhythmCell>();
            foreach(var token in RhythmInput.Tokens(part)){
                var group=RhythmInput.ParseGroup(token);
                if(cells.Count+group.Count>2048)throw new ArgumentException("Υπερβολικά πολλές αξίες.");
                for(var i=0;i<group.Count;i++)cells.Add(new(group.UnitTicks,group.Rest));
            }
            if(cells.Count==0||cells.Sum(c=>c.Ticks)!=meter.Ticks)throw new ArgumentException("Οι αξίες δεν συμπληρώνουν το μέτρο "+meter.Id+". Το 8/32 σημαίνει οκτώ ξεχωριστά τριακοστά δεύτερα.");
            return cells.ToArray();
        }).ToArray();
    }
    public static string Format(IEnumerable<IEnumerable<RhythmCell>> bars)=>string.Join(" | ",bars.Select(bar=>{
        var cells=bar.ToArray();var tokens=new List<string>();
        for(var i=0;i<cells.Length;){var count=1;var value=RhythmInput.FormatDuration(cells[i].Ticks);
            while(i+count<cells.Length&&count<128&&cells[i+count]==cells[i])count++;
            if(count>1&&value.StartsWith("1/",StringComparison.Ordinal)&&!value.Contains('+'))tokens.Add((cells[i].Rest?"r":"")+count+value[1..]);
            else for(var n=0;n<count;n++)tokens.Add((cells[i].Rest?"r":"")+value);
            i+=count;
        }
        return string.Join(' ',tokens);
    }));
    public static string Canonical(RhythmCell[][] bars)=>string.Join('|',bars.Select(b=>string.Join(',',b.Select(c=>(c.Rest?"r":"n")+c.Ticks.ToString("G",CultureInfo.InvariantCulture)))));

}
public static class TheoryV2
{
    public static readonly ScaleDefinition[] Scales=[
        new("major","Μείζων",[0,2,4,5,7,9,11,12]),
        new("natural-minor","Φυσική ελάσσων",[0,2,3,5,7,8,10,12]),
        new("harmonic-minor","Αρμονική ελάσσων",[0,2,3,5,7,8,11,12]),
        new("melodic-minor","Μελωδική ελάσσων (κλασική)",[0,2,3,5,7,9,11,12],[12,10,8,7,5,3,2,0]),
        new("chromatic","Χρωματική",Enumerable.Range(0,13).ToArray()),
        new("whole-tone","Ολοτονική",[0,2,4,6,8,10,12]),
        new("major-pentatonic","Μείζων πεντατονική",[0,2,4,7,9,12]),
        new("minor-pentatonic","Ελάσσων πεντατονική",[0,3,5,7,10,12]),
        new("diminished-wh","Ελαττωμένη τόνος–ημιτόνιο",[0,2,3,5,6,8,9,11,12]),
        new("diminished-hw","Ελαττωμένη ημιτόνιο–τόνος",[0,1,3,4,6,7,9,10,12]),
        new("augmented","Αυξημένη εξατονική",[0,3,4,7,8,11,12])];
    public static readonly ScaleDefinition[] Modes=[
        new("ionian","Ιωνικός",[0,2,4,5,7,9,11,12]),new("dorian","Δώριος",[0,2,3,5,7,9,10,12]),
        new("phrygian","Φρύγιος",[0,1,3,5,7,8,10,12]),new("lydian","Λύδιος",[0,2,4,6,7,9,11,12]),
        new("mixolydian","Μιξολύδιος",[0,2,4,5,7,9,10,12]),new("aeolian","Αιολικός",[0,2,3,5,7,8,10,12]),new("locrian","Λόκριος",[0,1,3,5,6,8,10,12])];
    public static readonly Option[] Cadences=[new("pac","Αυθεντική τέλεια"),new("iac","Αυθεντική ατελής"),new("half","Ημίπτωση"),new("plagal","Πλάγια πτώση"),new("deceptive","Απατηλή πτώση")];
    public static readonly MeterDefinition[] Meters=[
        new("2/4","Απλό 2/4",2,4,[1,1]),new("3/4","Απλό 3/4",3,4,[1,1,1]),new("4/4","Απλό 4/4",4,4,[1,1,1,1]),
        new("6/8","Σύνθετο 6/8 — 3+3",6,8,[3,3]),new("9/8","Σύνθετο 9/8 — 3+3+3",9,8,[3,3,3]),new("12/8","Σύνθετο 12/8 — 3+3+3+3",12,8,[3,3,3,3]),
        new("5/8:2+3","Μικτό 5/8 — 2+3",5,8,[2,3]),new("5/8:3+2","Μικτό 5/8 — 3+2",5,8,[3,2]),
        new("7/8:2+2+3","Μικτό 7/8 — 2+2+3",7,8,[2,2,3]),new("7/8:2+3+2","Μικτό 7/8 — 2+3+2",7,8,[2,3,2]),new("7/8:3+2+2","Μικτό 7/8 — 3+2+2",7,8,[3,2,2]),new("8/8:3+3+2","Μικτό 8/8 — 3+3+2",8,8,[3,3,2])];
    public static readonly (string Id,string Name,string Tokens)[] Rhythms=[
        ("quarters","Τέσσερα τέταρτα","1/4 1/4 1/4 1/4"),("halves","Δύο μισά","1/2 1/2"),
        ("eighths","Οκτώ όγδοα","1/8 1/8 1/8 1/8 1/8 1/8 1/8 1/8"),("mixed","Τέταρτο, δύο όγδοα, μισό","1/4 1/8 1/8 1/2"),
        ("dotted","Παρεστιγμένο τέταρτο, όγδοο, δύο τέταρτα","1/4. 1/8 1/4 1/4"),
        ("syncopation","Συγκοπή: όγδοο, τέταρτο, όγδοο, μισό","1/8 1/4 1/8 1/2"),
        ("rests","Παύση τετάρτου, δύο τέταρτα, δύο όγδοα","r1/4 1/4 1/4 1/8 1/8"),
        ("sixteenths","Τέσσερα δέκατα έκτα και τρία τέταρτα","1/16 1/16 1/16 1/16 1/4 1/4 1/4"),
        ("thirty-seconds","Οκτώ τριακοστά δεύτερα και τρία τέταρτα","8/32 3/4"),
        ("sixty-fourths","Δεκαέξι εξηκοστά τέταρτα και τρία τέταρτα","16/64 3/4"),
        ("mixed-fine","Εναλλαγές 64ων, 32ων, 16ων, ογδόων και τετάρτου","8/64 4/32 4/16 2/8 1/4"),
        ("dotted-fine","64ο, παρεστιγμένο 32ο και διευρυνόμενες αξίες","1/64 1/32. 1/16 1/8 1/4 1/2")];
    public static Exercise Generate(Settings s,Random random)
    {
        if(s.Category is Category.RhythmDictation or Category.RhythmReproduction)return Dictation(s,random);
        var candidates=new List<Exercise>();var constrained=new List<Exercise>();
        if(s.Category==Category.Cadences)
        {
            foreach(var tonic in Enumerable.Range(s.MinNote,s.MaxNote-s.MinNote+1).Where(n=>(!s.KeyTonic.HasValue||n%12==s.KeyTonic)&&(!s.RootPitchClass.HasValue||n%12==s.RootPitchClass)))
                foreach(var c in s.Level switch{Level.Beginner=>Cadences.Where(c=>c.Id is "pac" or "plagal"),Level.Elementary=>Cadences.Where(c=>c.Id is "pac" or "plagal" or "half"),Level.Intermediate=>Cadences.Where(c=>c.Id!="iac"),_=>Cadences.AsEnumerable()}){var score=CadenceScore(c.Id,tonic,s.KeyMode,difficulty:(int)s.Level);if(Fits(score,s))candidates.Add(Create(s,c.Id,c.Name,score,[]));
                    else{var limited=CadenceScore(c.Id,tonic,s.KeyMode);if(Fits(limited,s))constrained.Add(Create(s,c.Id,c.Name,limited with{Prompt=limited.Prompt+" Η επιλεγμένη έκταση περιορίζει την ανοιχτή διάταξη αυτού του επιπέδου."},[]));}}
        }
        else if(s.Category is Category.Scales or Category.Modes)
        {
            var definitions=s.Category==Category.Scales?Scales.Take(s.Level switch{Level.Beginner=>2,Level.Elementary=>4,Level.Intermediate=>6,Level.Advanced=>8,Level.Expert=>10,_=>11}):Modes.Take((int)s.Level+2);
            foreach(var tonic in Enumerable.Range(s.MinNote,s.MaxNote-s.MinNote+1).Where(n=>(!s.KeyTonic.HasValue||n%12==s.KeyTonic)&&(!s.RootPitchClass.HasValue||n%12==s.RootPitchClass)))
                foreach(var d in definitions){var score=ScaleScore(d,tonic,s.Direction);if(Fits(score,s))candidates.Add(Create(s,d.Id,d.Name,score,[]));}
        }
        else if(s.Category==Category.Meters)
        {
            foreach(var m in Meters.Take(s.Level switch{Level.Beginner=>3,Level.Elementary=>4,Level.Intermediate=>6,Level.Advanced=>8,Level.Expert=>10,_=>12}))
                candidates.Add(Create(s,m.Id,m.Name,MeterScore(m,Math.Max(2,s.Bars),Pitch(s)),[]));
        }
        else if(s.Category==Category.RhythmPatterns)
        {
            foreach(var r in Rhythms.Take(s.Level switch{Level.Beginner=>2,Level.Elementary=>4,Level.Intermediate=>6,Level.Advanced=>8,Level.Expert=>9,_=>Rhythms.Length}))
                candidates.Add(Create(s,r.Id,r.Name,RhythmScore(r.Tokens,"4/4",1,Pitch(s),r.Id),[]));
        }
        if(s.Category==Category.Cadences){
            var available=candidates.Select(e=>e.CorrectId).ToHashSet();
            candidates.AddRange(constrained.Where(e=>!available.Contains(e.CorrectId)));
        }
        if(candidates.Count==0&&constrained.Count>0)candidates=constrained;
        if(candidates.Count==0)throw new ArgumentException("Η έκταση ή η θεμέλιος δεν χωρά το παράδειγμα. Διεύρυνε την έκταση ή επίλεξε άλλη τονικότητα.");
        var choices=candidates.Select(c=>new Option(c.CorrectId,c.CorrectName)).DistinctBy(c=>c.Id).ToArray();
        var focus=s.Level==Level.Professional?s.Category switch{
            Category.Scales=>candidates.Where(e=>e.CorrectId is "diminished-wh" or "diminished-hw" or "augmented").ToArray(),
            Category.Modes=>candidates.Where(e=>e.CorrectId is "phrygian" or "lydian" or "locrian").ToArray(),
            Category.RhythmPatterns=>candidates.Where(e=>e.CorrectId is "sixty-fourths" or "mixed-fine" or "dotted-fine").ToArray(),
            Category.Meters=>candidates.Where(e=>e.Score!.MeterId.StartsWith("7/8",StringComparison.Ordinal)||e.Score.MeterId.StartsWith("8/8",StringComparison.Ordinal)).ToArray(),
            _=>candidates.ToArray()}:candidates.ToArray();
        if(focus.Length==0)focus=candidates.ToArray();
        var picked=focus[random.Next(focus.Length)] with {Choices=choices};Theory.VerifyExercise(picked);return picked;
    }
    private static int Pitch(Settings s)=>Math.Clamp(60,s.MinNote,s.MaxNote);
    private static bool Fits(MusicalScore score,Settings s)=>score.Events.All(n=>n.Midi>=s.MinNote&&n.Midi<=s.MaxNote);
    private static Exercise Create(Settings s,string id,string name,MusicalScore score,Option[] choices,AnswerKind kind=AnswerKind.Choice)=>new(Guid.NewGuid().ToString("N"),s.Category,score.Events.Select(n=>n.Midi).ToArray(),id,name,choices,s.Direction,score,kind);
    public static MusicalScore ScaleScore(ScaleDefinition d,int tonic,Direction direction)
    {
        var down=d.Down??d.Up.Reverse().ToArray();
        var steps=direction==Direction.Descending?down.Concat(d.Up.Skip(1)):d.Up.Concat(down.Skip(1));
        var notes=steps.Select((n,i)=>new ScoreNote(tonic+n,i*0.5,0.44)).ToArray();
        return new(notes,notes.Length*0.5,d.Id,tonic,TonalityMode.Major,"Άκουσε άνοδο και κάθοδο. Η αρχική και τελική νότα είναι το τονικό κέντρο.");
    }
    public static MusicalScore CadenceScore(string id,int tonic,TonalityMode mode,bool legacyContext=false,int difficulty=0)
    {
        var minor=mode==TonalityMode.NaturalMinor;var third=minor?3:4;
        int[] i=[0,third,7,12],v=[-5,2,7,11];
        var first=id switch{"pac"=>v,"iac"=>new[]{-5,2,11,19},"half"=>i,"plagal"=>new[]{-7,0,minor?8:9,17},"deceptive"=>v,_=>throw new ArgumentException("Άγνωστη πτώση")};
        var last=id switch{"pac"=>i,"iac"=>new[]{0,third,12,19},"half"=>v,"plagal"=>i,"deceptive"=>new[]{minor?-4:-3,0,third,12},_=>throw new ArgumentException("Άγνωστη πτώση")};
        if(difficulty==5&&id=="iac")first=[-1,2,7,11];
        if(difficulty is <0 or >5)throw new ArgumentException("Μη έγκυρο επίπεδο πτώσης.");
        int[][] shifts=[[0,0,0,0],[-12,0,0,0],[-12,-12,0,0],[-12,-12,-12,0],[0,0,0,12],[-12,0,0,0]];
        int[] Voice(int[] chord)=>chord.Select((n,index)=>n+shifts[difficulty][index]).ToArray();
        var notes=new List<ScoreNote>();
        void Chord(int[] chord,double beat,double duration){notes.AddRange(chord.Select((n,voice)=>new ScoreNote(tonic+n,beat,duration,voice)));}
        if(legacyContext){Chord(i,0,0.8);Chord(v,1,0.8);Chord(i,2,0.8);}
        else Chord(i,0,1.6);
        Chord(Voice(first),4,1.8);Chord(Voice(last),6,1.8);
        return new(notes.ToArray(),8,id,tonic,mode,legacyContext?"Ακούγεται παλιό τονικό πλαίσιο Ι–V–Ι. Αναγνώρισε μόνο τις δύο τελευταίες συγχορδίες.":"Ακούγεται μία συγχορδία τονικής για αναφορά, καθαρή παύση και έπειτα οι δύο συγχορδίες της ζητούμενης πτώσης. Βαθμολόγησε αυτές τις δύο συγχορδίες. Στην ελάσσονα η δεσπόζουσα έχει προσαγωγέα.",Metadata:legacyContext?null:difficulty==0?"cadence-tonic-context-v2":"cadence-tonic-context-v2:L"+difficulty);
    }
    public static MusicalScore MeterScore(MeterDefinition meter,int bars,int pitch)
    {
        var notes=new List<ScoreNote>();var unit=4.0/meter.Denominator;
        for(var bar=0;bar<bars;bar++)
        {
            var starts=new HashSet<int>();var total=0;foreach(var group in meter.Groups){starts.Add(total);total+=group;}
            for(var n=0;n<meter.Numerator;n++)notes.Add(new(pitch,bar*meter.QuarterBeats+n*unit,unit*0.4,0,n==0?120:starts.Contains(n)?95:50));
        }
        return new(notes.ToArray(),bars*meter.QuarterBeats,meter.Id,pitch,TonalityMode.Major,"Άκουσε τον ισχυρό πρώτο παλμό και τους δευτερεύοντες τονισμούς. Οι ομαδοποιήσεις είναι ακουστές μέσω δυναμικής.",MeterId:meter.Id,Bars:bars);
    }
    public static MusicalScore RhythmScore(string tokens,string meterId,int bars,int pitch,string definitionId="dictation")
    {
        var meter=Meters.Single(m=>m.Id==meterId);var cells=RhythmCodec.Parse(tokens,meter,bars);var notes=new List<ScoreNote>();
        // One complete count-in bar; voice -1 is context, voice 0 is the assessed pattern.
        var context=MeterScore(meter,1,pitch);notes.AddRange(context.Events.Select(n=>n with{Voice=-1}));
        double beat=meter.QuarterBeats;
        foreach(var bar in cells)foreach(var cell in bar){if(!cell.Rest)notes.Add(new(pitch,beat,cell.Ticks/4.0*0.8));beat+=cell.Ticks/4.0;}
        return new(notes.ToArray(),beat,definitionId,pitch,TonalityMode.Major,
            "Μία μπάρα εισαγωγής ορίζει τον παλμό. Ακολουθεί το ρυθμικό παράδειγμα. Γραφή πλήθος/αξία: 2/4 = δύο τέταρτα, 8/32 = οκτώ τριακοστά δεύτερα, 16/64 = δεκαέξι εξηκοστά τέταρτα. Προαιρετικά ονόματα: μιφα2/8 ή φασολλασι4/16. Τα ύψη δεν βαθμολογούνται στη ρυθμική υπαγόρευση. Τελεία για παράστιξη, r ή π για παύση, | ανά μέτρο. Στην αναπαραγωγή F8 σε κάθε έναρξη και επιπλέον στο τέλος της τελευταίας διάρκειας.",tokens,meterId,bars);
    }
    private static Exercise Dictation(Settings s,Random random)
    {
        var meter=Meters.Single(m=>m.Id==s.MeterId);var bars=new List<List<RhythmCell>>();
        for(var bar=0;bar<s.Bars;bar++)
        {
            var cells=new List<RhythmCell>();double remaining=meter.Ticks;
            while(remaining>0)
            {
                double[] allowed=s.Level switch{Level.Beginner=>meter.Denominator==4?[4,8]:[2,6],Level.Elementary=>[2,4,8],Level.Intermediate=>[1,2,3,4,6,8],Level.Advanced=>[0.5,1,2,3,4,6],Level.Expert=>[0.5,1,2,3,4],_=>[0.25,0.5,1,2]};
                var available=allowed.Where(t=>t<=remaining).ToArray();if(available.Length==0)available=[remaining];
                var ticks=cells.Count==0&&s.Level>=Level.Advanced?available.Min():available[random.Next(available.Length)];
                cells.Add(new(ticks,s.Level>=Level.Intermediate&&cells.Count>0&&random.Next(5)==0));remaining-=ticks;
            }
            bars.Add(cells);
        }
        var tokens=RhythmCodec.Format(bars);var score=RhythmScore(tokens,meter.Id,s.Bars,Pitch(s));
        var kind=s.Category==Category.RhythmReproduction?AnswerKind.Taps:AnswerKind.Text;
        var e=Create(s,"dictation",tokens,score,[],kind);Theory.VerifyExercise(e);return e;
    }
    public static void Verify(Exercise e)
    {
        if(e.Category>Category.RhythmReproduction){TheoryV3.Verify(e);return;}
        var score=e.Score??throw new ArgumentException("Λείπει η μουσική ακολουθία.");
        if(score.LengthBeats<=0||score.LengthBeats>256||!double.IsFinite(score.LengthBeats)||score.Events.Length==0||score.Events.Length>2048)throw new ArgumentException("Μη έγκυρη διάρκεια ή πλήθος συμβάντων.");
        foreach(var n in score.Events)if(n.Midi is <21 or >108||!double.IsFinite(n.Beat)||!double.IsFinite(n.Duration)||n.Beat<0||n.Duration<=0||n.Beat+n.Duration>score.LengthBeats+0.001||n.Velocity is <1 or >127)throw new ArgumentException("Μη έγκυρο μουσικό συμβάν.");
        if(!e.Notes.SequenceEqual(score.Events.Select(n=>n.Midi)))throw new ArgumentException("Ασυμφωνία νοτών και ακολουθίας.");
        MusicalScore expected=e.Category switch{
            Category.Cadences=>CadenceScore(e.CorrectId,score.Tonic,score.Mode,score.Metadata is null,score.Metadata?.StartsWith("cadence-tonic-context-v2:L",StringComparison.Ordinal)==true?int.Parse(score.Metadata[26..],CultureInfo.InvariantCulture):0),
            Category.Scales=>ScaleScore(Scales.Single(d=>d.Id==e.CorrectId),score.Tonic,e.Direction),
            Category.Modes=>ScaleScore(Modes.Single(d=>d.Id==e.CorrectId),score.Tonic,e.Direction),
            Category.Meters=>MeterScore(Meters.Single(m=>m.Id==e.CorrectId),score.Bars,score.Tonic),
            Category.RhythmPatterns=>RhythmScore(Rhythms.Single(r=>r.Id==e.CorrectId).Tokens,"4/4",1,score.Tonic,e.CorrectId),
            Category.RhythmDictation or Category.RhythmReproduction=>RhythmScore(score.ExpectedText??throw new ArgumentException("Λείπει ο ρυθμός."),score.MeterId,score.Bars,score.Tonic),
            _=>throw new ArgumentException("Άγνωστη κατηγορία ακολουθίας")};
        if(e.Category==Category.Cadences&&CadenceAnalysis.FromScore(score).Id!=e.CorrectId)throw new ArgumentException("Η ετικέτα πτώσης δεν συμφωνεί με ανεξάρτητη ανάλυση των πραγματικών συγχορδιών.");
        if(!score.Events.SequenceEqual(expected.Events)||Math.Abs(score.LengthBeats-expected.LengthBeats)>0.00001)throw new ArgumentException("Η ακολουθία δεν συμφωνεί με τη μουσικοθεωρητική απάντηση.");
    }
}
public sealed record GradeResult(bool Correct,double Percentage,string Feedback,string Canonical);
public static class WrittenGrader
{
    public static GradeResult Grade(Exercise exercise,string response,Settings settings)
    {
        Theory.VerifyExercise(exercise);
        if(exercise.Category>Category.RhythmReproduction)return TheoryV3.Grade(exercise,response,settings);
        var score=exercise.Score??throw new ArgumentException("Η άσκηση δεν έχει γραπτή απάντηση.");
        if(exercise.AnswerKind==AnswerKind.Text)
        {
            var meter=TheoryV2.Meters.Single(m=>m.Id==score.MeterId);
            var answer=RhythmCodec.Canonical(RhythmCodec.Parse(response,meter,score.Bars));
            var expected=RhythmCodec.Canonical(RhythmCodec.Parse(score.ExpectedText!,meter,score.Bars));
            var correct=answer==expected;return new(correct,correct?100:0,correct?"Σωστή ρυθμική υπαγόρευση.":"Οι διάρκειες ή οι παύσεις διαφέρουν.",answer);
        }
        if(exercise.AnswerKind!=AnswerKind.Taps)throw new ArgumentException("Η άσκηση απαιτεί επιλογή.");
        var taps=response.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(t=>double.Parse(t,CultureInfo.InvariantCulture)).ToArray();
        if(taps.Length<2||taps.Any(t=>!double.IsFinite(t)||t<0)||taps.Zip(taps.Skip(1)).Any(p=>p.Second<=p.First))throw new ArgumentException("Χρειάζονται τουλάχιστον δύο διαδοχικά κτυπήματα.");
        var meterBeats=TheoryV2.Meters.Single(m=>m.Id==score.MeterId).QuarterBeats;
        var expectedBeats=score.Events.Where(n=>n.Voice==0).Select(n=>n.Beat-meterBeats).Append(score.LengthBeats-meterBeats).ToArray();
        if(taps.Length!=expectedBeats.Length)return new(false,0,$"Δόθηκαν {taps.Length} κτυπήματα αντί {expectedBeats.Length}.",response);
        var tolerance=settings.Level switch{Level.Beginner=>0.3,Level.Elementary=>0.24,Level.Intermediate=>0.18,Level.Advanced=>0.13,Level.Expert=>0.1,_=>0.08};
        var correctIntervals=0;
        for(var i=1;i<taps.Length;i++){
            var expectedInterval=expectedBeats[i]-expectedBeats[i-1];
            var permitted=expectedInterval<0.25?Math.Min(tolerance,expectedInterval*0.3):tolerance;
            if(Math.Abs((taps[i]-taps[i-1])*settings.Tempo/60-expectedInterval)<=permitted)correctIntervals++;
        }
        var percentage=100.0*correctIntervals/(taps.Length-1);return new(percentage>=80,percentage,$"Ακρίβεια διαδοχών κτυπημάτων: {percentage:F1}%. Δεν βαθμολογείται φωνητική ή διάρκεια κρατήματος πλήκτρου.",response);
    }
}
