using System.Text.Json;
namespace Studio.Core;
public sealed record RowRecipe(int[] Prime,string Form,int Transposition,double Step=1,int OctaveDisplacement=0);
public static class TheoryV4
{
    public static readonly Option[] RowForms=[new("P","Αρχική σειρά"),new("I","Αναστροφή διαστημάτων"),new("R","Καρκινική"),new("RI","Καρκινική αναστροφή")];
    public static readonly NamedPattern[] ExtendedChords=[
        new("major9","Μείζων ένατη",[0,4,7,11,14]),new("minor9","Ελάσσων ένατη",[0,3,7,10,14]),new("dominant9","Δεσπόζουσα ένατη",[0,4,7,10,14]),
        new("dominant11","Δεσπόζουσα ενδέκατη, πλήρης",[0,4,7,10,14,17]),new("dominant13","Δεσπόζουσα δέκατη τρίτη, πλήρης",[0,4,7,10,14,17,21]),
        new("dominant-b9","Δεσπόζουσα με μικρή ένατη",[0,4,7,10,13]),new("dominant-sharp9","Δεσπόζουσα με αυξημένη ένατη",[0,4,7,10,15]),
        new("dominant-sharp11","Δεσπόζουσα με αυξημένη ενδέκατη",[0,4,7,10,14,18]),new("dominant-b13","Δεσπόζουσα με χαμηλή δέκατη τρίτη",[0,4,7,10,14,20]),
        new("quartal","Τεταρτογενής συγχορδία",[0,5,10,15])];
    public static readonly Option[] Colours=[new("ionian","Ιωνικός — μείζων τρίτη και έβδομη"),new("dorian","Δώριος — φυσική έκτη στην ελάσσονα"),new("phrygian","Φρύγιος — χαμηλή δεύτερη"),new("lydian","Λύδιος — αυξημένη τέταρτη"),new("mixolydian","Μιξολύδιος — χαμηλή έβδομη στη μείζονα"),new("aeolian","Αιολικός — φυσική ελάσσων"),new("locrian","Λόκριος — ελαττωμένη πέμπτη και χαμηλή δεύτερη")];
    public static readonly Option[] Analyses=[new("V/V","Δεσπόζουσα της δεσπόζουσας"),new("V7/ii","Δευτερεύουσα δεσπόζουσα της υπερτονικής"),new("bII6","Ναπολιτάνικη έκτη"),new("It+6","Ιταλική αυξημένη έκτη"),new("Fr+6","Γαλλική αυξημένη έκτη"),new("Ger+6","Γερμανική αυξημένη έκτη"),new("iv","Δανεισμένη ελάσσων υποδεσπόζουσα")];
    public static int[] Transform(int[] row,string form,int transposition=0)
    {
        if(row.Length!=12||row.Distinct().Count()!=12||row.Any(n=>n is <0 or >11))throw new ArgumentException("Απαιτείται σειρά και των 12 pitch classes χωρίς επανάληψη.");
        IEnumerable<int> result=form switch{"P"=>row,"I"=>row.Select(n=>(12-n)%12),"R"=>row.Reverse(),"RI"=>row.Select(n=>(12-n)%12).Reverse(),_=>throw new ArgumentException("Μορφή σειράς")};
        if(transposition is <0 or >11)throw new ArgumentException("Μεταφορά σειράς");return result.Select(n=>(n+transposition)%12).ToArray();
    }
    public static string CompoundName(int distance)
    {
        if(distance<=24)return Theory.IntervalNames[distance];
        string[] names=["Μικρή δέκατη έκτη","Μεγάλη δέκατη έκτη","Μικρή δέκατη έβδομη","Μεγάλη δέκατη έβδομη","Καθαρή δέκατη όγδοη","Αυξημένη δέκατη όγδοη / ελαττωμένη δέκατη ένατη","Καθαρή δέκατη ένατη","Μικρή εικοστή","Μεγάλη εικοστή","Μικρή εικοστή πρώτη","Μεγάλη εικοστή πρώτη","Τριπλή οκτάβα"];
        return names[distance-25];
    }
    public static Exercise Generate(Settings s,Random r)
    {
        if(s.Category is Category.Polyphony2 or Category.Polyphony3 or Category.Polyphony4 or Category.InnerVoice)return Polyphony(s,r);
        var tonicClass=s.KeyTonic??s.RootPitchClass??0;
        var roots=Enumerable.Range(s.MinNote,s.MaxNote-s.MinNote+1).Where(n=>n%12==tonicClass).ToArray();
        if(roots.Length==0)throw new ArgumentException("Δεν υπάρχει τονική στην έκταση.");
        if(s.Category==Category.ExtendedIntervals)
        {
            var candidates=new List<Exercise>();
            foreach(var root in Enumerable.Range(s.MinNote,s.MaxNote-s.MinNote+1).Where(n=>(!s.RootPitchClass.HasValue||n%12==s.RootPitchClass)&&(!s.KeyTonic.HasValue||n%12==s.KeyTonic)))for(var d=13;d<=new[]{18,22,26,30,33,36}[(int)s.Level];d++)
            {
                var other=root+(s.Direction==Direction.Descending?-d:d);if(other<s.MinNote||other>s.MaxNote)continue;
                var score=IntervalScore(root,other,s.Direction);candidates.Add(Create(s,"interval:"+d,CompoundName(d),score,[],AnswerKind.Choice));
            }
            if(candidates.Count==0)throw new ArgumentException("Διεύρυνε την έκταση για σύνθετα διαστήματα.");
            return candidates[r.Next(candidates.Count)] with{Choices=candidates.Select(e=>new Option(e.CorrectId,e.CorrectName)).DistinctBy(c=>c.Id).ToArray()};
        }
        if(s.Category==Category.ExtendedChords)
        {
            var candidates=new List<Exercise>();
            foreach(var root in roots)foreach(var chord in ExtendedChords.Take(s.Level switch{Level.Beginner=>3,Level.Elementary=>4,Level.Intermediate=>6,Level.Advanced=>8,Level.Expert=>9,_=>10}).Skip(s.Level switch{Level.Expert=>3,Level.Professional=>5,_=>0}))
            {var score=ExtendedChordScore(chord,root);if(score.Events.All(n=>n.Midi>=s.MinNote&&n.Midi<=s.MaxNote))candidates.Add(Create(s,chord.Id,chord.Name,score,[],AnswerKind.Choice));}
            if(candidates.Count==0)throw new ArgumentException("Η έκταση δεν χωρά την εκτεταμένη συγχορδία.");
            return candidates[r.Next(candidates.Count)] with{Choices=candidates.Select(e=>new Option(e.CorrectId,e.CorrectName)).DistinctBy(c=>c.Id).ToArray()};
        }
        var advancedHarmony=s.Category is Category.ChromaticHarmony or Category.AdvancedAnalysis;
        var tonic=roots.Where(n=>n+(advancedHarmony?19:s.Category is Category.TwelveToneRows or Category.RowTransform&&s.Level==Level.Professional?23:12)<=s.MaxNote&&(!advancedHarmony||n-5>=s.MinNote)).OrderBy(n=>Math.Abs(n-(s.MinNote+s.MaxNote)/2)).FirstOrDefault(-1);
        if(tonic<0)throw new ArgumentException("Διεύρυνε την έκταση κατά τουλάχιστον μία οκτάβα.");
        if(s.Category==Category.ModalColour)
        {
            var mode=TheoryV2.Modes[r.Next((int)s.Level+2)];var score=TheoryV2.ScaleScore(mode,tonic,s.Direction)with{DefinitionId="modal-colour",ExpectedText=mode.Id};
            return Create(s,mode.Id,Colours.Single(c=>c.Id==mode.Id).Name,score,Colours.Take((int)s.Level+2).ToArray(),AnswerKind.Choice);
        }
        if(s.Category is Category.TwelveToneRows or Category.RowTransform)
        {
            int[] row;do{row=Enumerable.Range(0,12).ToArray();for(var j=11;j>0;j--){var k=r.Next(j+1);(row[j],row[k])=(row[k],row[j]);}var first=row[0];row=row.Select(n=>(n-first+12)%12).ToArray();}while(RowForms.Select(f=>string.Join(',',Transform(row,f.Id))).Distinct().Count()!=4);
            var recipe=new RowRecipe(row,RowForms[r.Next(Math.Min(4,(int)s.Level+2))].Id,r.Next(new[]{1,1,1,4,8,12}[(int)s.Level]),new[]{1.5,1.25,1.0,0.75,0.5,0.25}[(int)s.Level],s.Level==Level.Professional?1:0);var score=RowScore(recipe,tonic,s.Category==Category.RowTransform);
            return s.Category==Category.RowTransform?Create(s,recipe.Form,RowForms.Single(f=>f.Id==recipe.Form).Name,score,RowForms.Take(Math.Min(4,(int)s.Level+2)).ToArray(),AnswerKind.Choice):Create(s,"row",score.ExpectedText!,score,[],AnswerKind.Text);
        }
        if(s.Category==Category.AdvancedAnalysis)
        {
            var choices=s.KeyMode==TonalityMode.Major?Analyses:Analyses.Where(c=>c.Id!="V7/ii"&&c.Id!="iv").ToArray();
            choices=choices.Take(Math.Min(choices.Length,(int)s.Level+2)).ToArray();
            var focused=s.Level switch{Level.Professional=>choices.Where(c=>c.Id is "Fr+6" or "Ger+6").ToArray(),Level.Expert=>choices.Where(c=>c.Id is "It+6" or "Fr+6" or "Ger+6").ToArray(),_=>choices};
            if(focused.Length==0)focused=choices;var selected=focused[r.Next(focused.Length)];var approach=selected.Id=="V7/ii"?"ii V ":selected.Id=="Ger+6"?(s.KeyMode==TonalityMode.Major?"I64 V ":"i64 V "):"V ";
            var text=(s.KeyMode==TonalityMode.Major?"I ":"i ")+selected.Id+" "+approach+(s.KeyMode==TonalityMode.Major?"I":"i");
            var score=ChromaticScore(text,tonic,s.KeyMode,s.MinNote,s.MaxNote,"advanced-analysis")with{Prompt="Ακούγεται τονικό πλαίσιο και αρμονική ακολουθία. Ανάλυσε τη ΔΕΥΤΕΡΗ συγχορδία της ακολουθίας, όχι της εισαγωγής."};
            return Create(s,selected.Id,selected.Name,score,choices,AnswerKind.Choice);
        }
        if(s.Category==Category.ChromaticHarmony)
        {
            string[] patterns=s.KeyMode==TonalityMode.Major?["I V/V V I","I V7/ii ii V7 I","I bII6 V I","I It+6 V I","I Fr+6 V I","I Ger+6 I64 V7 I","I bVI iv V I"]:["i V/V V i","i bII6 V i","i It+6 V i","i Fr+6 V i","i Ger+6 i64 V7 i","i VI iv V i"];
            patterns=patterns.Take(Math.Min(patterns.Length,(int)s.Level+1)).ToArray();
            var text=patterns[r.Next(patterns.Length)];if(s.Level>=Level.Expert)text+=" "+patterns[r.Next(patterns.Length)];return Create(s,"chromatic",text,ChromaticScore(text,tonic,s.KeyMode,s.MinNote,s.MaxNote,"chromatic"),[],AnswerKind.Text);
        }
        throw new ArgumentException("Άγνωστη προχωρημένη κατηγορία.");
    }
    private static Exercise Create(Settings s,string id,string label,MusicalScore score,Option[] choices,AnswerKind kind)=>new(Guid.NewGuid().ToString("N"),s.Category,score.Events.Select(n=>n.Midi).ToArray(),id,label,choices,s.Direction,score,kind);
    private static List<ScoreNote> Pulses(int tonic)=>Enumerable.Range(0,4).Select(i=>new ScoreNote(tonic,i,0.2,-1,60)).ToList();
    private static MusicalScore IntervalScore(int first,int second,Direction direction)
    {
        ScoreNote[] notes=[new(first,0,0.85),new(second,direction==Direction.Harmonic?0:1,0.85)];
        return new(notes,direction==Direction.Harmonic?1:2,"extended-interval",first,TonalityMode.Major,"Σύνθετο διάστημα από ένατη μέχρι τριπλή οκτάβα. Αναγνώρισε το πλήρες διάστημα.",Math.Abs(second-first).ToString());
    }
    private static MusicalScore ExtendedChordScore(NamedPattern chord,int tonic)
    {
        var notes=new List<ScoreNote>{new(tonic,0,0.8,-1)};notes.AddRange(chord.Steps.Select((n,v)=>new ScoreNote(tonic+n,2,3,v)));
        return new(notes.ToArray(),5.5,"extended-chord",tonic,TonalityMode.Major,$"Θεμέλιος: {Theory.NoteName(tonic)}. Ακούγεται πρώτα μόνη της και μετά η πλήρης εκτεταμένη συγχορδία.",chord.Id);
    }
    private static Exercise Polyphony(Settings s,Random random)
    {
        var count=s.Category switch{Category.Polyphony2=>2,Category.Polyphony3=>3,_=>4};
        if(s.MaxNote-s.MinNote+1<count*7)throw new ArgumentException("Για ανεξάρτητες φωνές επίλεξε τουλάχιστον επτά ημιτόνια έκτασης ανά φωνή.");
        var tonicClass=s.KeyTonic??s.RootPitchClass??0;
        var tonic=Enumerable.Range(s.MinNote,s.MaxNote-s.MinNote+1).Where(n=>n%12==tonicClass).OrderBy(n=>Math.Abs(n-60)).FirstOrDefault(-1);if(tonic<0)throw new ArgumentException("Τονική εκτός έκτασης.");
        var notes=Pulses(tonic);var patterns=new HashSet<string>();var all=new List<NotatedNote[]>();
        for(var voice=0;voice<count;voice++)
        {
            var band=count-1-voice;var low=s.MinNote+(s.MaxNote-s.MinNote+1)*band/count;var high=s.MinNote+(s.MaxNote-s.MinNote+1)*(band+1)/count-1;
            var pitches=Enumerable.Range(low,high-low+1).Where(n=>Theory.InKey(n,s with{KeyTonic=tonicClass})).ToArray();
            NotatedNote[] line;
            do
            {
                var part=new List<NotatedNote>();double remaining=64;var index=random.Next(pitches.Length);
                while(remaining>0){double[] options=s.Level switch{Level.Beginner=>[4,8],Level.Elementary=>[2,4,8],Level.Intermediate=>[1,2,4,8],Level.Advanced=>[1,2,3,4,6,8],Level.Expert=>[0.5,1,2,3,4],_=>[0.25,0.5,1,2]};var values=options.Where(n=>n<=remaining).ToArray();var ticks=values.Length==0?remaining:part.Count==0&&s.Level>=Level.Expert?values.Min():values[random.Next(values.Length)];var leap=new[]{1,1,2,2,3,6}[(int)s.Level];index=Math.Clamp(index+random.Next(-leap,leap+1),0,pitches.Length-1);part.Add(new(pitches[index],ticks));remaining-=ticks;}
                line=part.ToArray();
            }while(!patterns.Add(string.Join(',',line.Select(n=>n.Ticks))));
            all.Add(line);double beat=4;foreach(var n in line){notes.Add(new(n.Midi,beat,n.Ticks/4.0*0.85,voice));beat+=n.Ticks/4.0;}
        }
        notes.Add(new(tonic,20,0.2,-1,45));var target=s.Category==Category.InnerVoice?random.Next(1,3):random.Next(count);
        var text=NoteCodec.Format(all[target],true);
        var score=new MusicalScore(notes.ToArray(),20.5,"poly:"+count,tonic,s.KeyMode,$"Πολυφωνία {count} ανεξάρτητων γραμμών. Γράψε τη φωνή {target+1} με νότες:διάρκειες. Η φωνή 1 είναι η ψηλότερη. Οι γραμμές έχουν χωριστή ρυθμική κίνηση και register, όχι εγγύηση αυστηρής αντίστιξης. Μπορείς να απομονώσεις φωνή στις Ρυθμίσεις.",text,TargetVoice:target);
        return Create(s,"poly",text,score,[],AnswerKind.Text);
    }
    public static MusicalScore RowScore(RowRecipe recipe,int tonic,bool compare)
    {
        var notes=Pulses(tonic);var reference=Transform(recipe.Prime,"P",recipe.Transposition);var target=Transform(recipe.Prime,recipe.Form,recipe.Transposition);
        if(!double.IsFinite(recipe.Step)||recipe.Step is <0.25 or >1.5||recipe.OctaveDisplacement is <0 or >1)throw new ArgumentException("Μη έγκυρη δυσκολία σειράς.");
        if(compare)for(var i=0;i<12;i++)notes.Add(new(tonic+reference[i],4+i*recipe.Step,0.85*recipe.Step,-1));
        var start=compare?6+12*recipe.Step:4;for(var i=0;i<12;i++)notes.Add(new(tonic+target[i]+(i%2)*12*recipe.OctaveDisplacement,start+i*recipe.Step,0.85*recipe.Step));
        notes.Add(new(tonic,start+12*recipe.Step,0.2,-1,45));
        return new(notes.ToArray(),start+12*recipe.Step+0.5,compare?"row-transform":"twelve-tone",tonic,TonalityMode.Major,
            compare?"Μετά τους παλμούς ακούγεται η αρχική σειρά και μετά παύση μια μετασχηματισμένη σειρά. Αναγνώρισε P, I, R ή RI. Δεν δίνεται τονική συγχορδία.":$"Δωδεκαφθογγική σειρά. Αναφορά register: {Theory.NoteName(tonic)}. Γράψε τις δώδεκα νότες χωρίς οκτάβα, όπως ντο ρε μι. Η οκτάβα είναι προαιρετική. Δεν υπάρχει τονικό πλαίσιο.",string.Join(' ',target.Select(n=>(tonic+n).ToString())),Metadata:JsonSerializer.Serialize(recipe));
    }
    public static MusicalScore ChromaticScore(string text,int tonic,TonalityMode mode,int minimum,int maximum,string id)
    {
        var score=TheoryV3.HarmonyScore(text,tonic,mode,minimum,maximum,id);var notes=score.Events.ToList();var tokens=text.Split(' ');
        for(var i=0;i<tokens.Length-1;i++)
        {
            if(tokens[i] is not ("It+6" or "Fr+6" or "Ger+6"))continue;
            var original=notes.Where(n=>n.Beat==2+i*2).OrderBy(n=>n.Voice).ToArray();var anchor=original[0].Midi+4;
            int[] relative=tokens[i+1] is "I64" or "i64"?[-5,0,mode==TonalityMode.Major?4:3,7]:[-5,-1,2,7];
            var beat=2+(i+1)*2;notes.RemoveAll(n=>n.Beat==beat);notes.AddRange(relative.Select((n,voice)=>new ScoreNote(anchor+n,beat,1.75,voice)));
        }
        if(notes.Any(n=>n.Midi<minimum||n.Midi>maximum))throw new ArgumentException("Η έκταση δεν χωρά τη χρωματική λύση.");
        return score with{Events=notes.OrderBy(n=>n.Beat).ThenBy(n=>n.Voice).ToArray()};
    }
    private static void VerifyHarmony(MusicalScore score)
    {
        var tokens=score.ExpectedText!.Split(' ');
        if(score.LengthBeats!=2+tokens.Length*2)throw new ArgumentException("Διάρκεια αρμονίας.");
        var context=new[]{new ScoreNote(score.Tonic,0,1,-1),new ScoreNote(score.Tonic+(score.Mode==TonalityMode.Major?4:3),0,1,-1),new ScoreNote(score.Tonic+7,0,1,-1)};
        if(!context.SequenceEqual(score.Events.Where(n=>n.Voice<0)))throw new ArgumentException("Πλαίσιο αρμονίας.");
        for(var i=0;i<tokens.Length;i++)
        {
            var chord=Harmony.Resolve(tokens[i],score.Mode);var actual=score.Events.Where(n=>n.Beat==2+i*2&&n.Voice>=0).OrderBy(n=>n.Voice).ToArray();
            if(actual.Length<chord.Steps.Length||actual.Length>7||actual.Any(n=>n.Duration!=1.75))throw new ArgumentException("Φωνές αρμονίας.");
            var expected=chord.Steps.Select(n=>(n+score.Tonic+120)%12).Distinct().Order();
            if(!actual.Select(n=>n.Midi%12).Distinct().Order().SequenceEqual(expected)||actual[0].Midi%12!=(score.Tonic+chord.Steps[0]+120)%12)throw new ArgumentException("Λανθασμένη αρμονική ποιότητα/αναστροφή.");
            if(i>0&&tokens[i-1] is "It+6" or "Fr+6" or "Ger+6")
            {
                var previous=score.Events.Where(n=>n.Beat==2+(i-1)*2&&n.Voice>=0).OrderBy(n=>n.Voice).ToArray();
                if(actual[0].Midi!=previous[0].Midi-1||actual[^1].Midi!=previous[^1].Midi+1)throw new ArgumentException("Η αυξημένη έκτη δεν λύνεται αντίθετα κατά ημιτόνιο.");
            }
        }
        if(score.Events.Where(n=>n.Voice>=0).Any(n=>n.Beat<2||(n.Beat-2)%2!=0))throw new ArgumentException("Πρόσθετα αρμονικά συμβάντα.");
    }
    public static void Verify(Exercise e)
    {
        var s=e.Score??throw new ArgumentException("Λείπει score.");
        if(s.Events.Length==0||s.Events.Length>2048||!double.IsFinite(s.LengthBeats)||s.LengthBeats<=0||s.LengthBeats>256||!s.Events.Select(n=>n.Midi).SequenceEqual(e.Notes)||s.Events.Any(n=>n.Midi is <21 or >108||n.Beat<0||n.Duration<=0||!double.IsFinite(n.Beat)||!double.IsFinite(n.Duration)||n.Beat+n.Duration>s.LengthBeats||n.Velocity is <1 or >127))throw new ArgumentException("Μη έγκυρα προχωρημένα συμβάντα.");
        MusicalScore? expected=null;
        switch(e.Category)
        {
            case Category.ExtendedIntervals:
                var distance=Math.Abs(e.Notes[1]-e.Notes[0]);if(distance is <13 or >36||e.CorrectId!="interval:"+distance||e.Direction==Direction.Descending&&e.Notes[1]>e.Notes[0]||e.Direction!=Direction.Descending&&e.Notes[1]<e.Notes[0])throw new ArgumentException("Σύνθετο διάστημα.");
                expected=IntervalScore(e.Notes[0],e.Notes[1],e.Direction);break;
            case Category.ExtendedChords:expected=ExtendedChordScore(ExtendedChords.Single(c=>c.Id==e.CorrectId),s.Tonic);break;
            case Category.ModalColour:expected=TheoryV2.ScaleScore(TheoryV2.Modes.Single(m=>m.Id==e.CorrectId),s.Tonic,e.Direction);break;
            case Category.TwelveToneRows:case Category.RowTransform:
                var recipe=JsonSerializer.Deserialize<RowRecipe>(s.Metadata!)??throw new ArgumentException("Σειρά");expected=RowScore(recipe,s.Tonic,e.Category==Category.RowTransform);
                if(e.Category==Category.RowTransform&&(e.CorrectId!=recipe.Form||RowForms.Select(f=>string.Join(',',Transform(recipe.Prime,f.Id,recipe.Transposition))).Distinct().Count()!=4))throw new ArgumentException("Αμφίσημος/λανθασμένος μετασχηματισμός.");break;
            case Category.ChromaticHarmony:case Category.AdvancedAnalysis:
                VerifyHarmony(s);
                if(e.Category==Category.AdvancedAnalysis&&s.ExpectedText!.Split(' ')[1]!=e.CorrectId)throw new ArgumentException("Λανθασμένη ανάλυση.");return;
            default:
                var count=e.Category switch{Category.Polyphony2=>2,Category.Polyphony3=>3,Category.Polyphony4 or Category.InnerVoice=>4,_=>throw new ArgumentException("Κατηγορία")};
                if(s.TargetVoice<0||s.TargetVoice>=count||e.Category==Category.InnerVoice&&s.TargetVoice is not (1 or 2))throw new ArgumentException("Στόχος φωνής.");
                var written=NoteCodec.Parse(s.ExpectedText!,true);var patterns=new HashSet<string>();
                for(var voice=0;voice<count;voice++)
                {
                    var line=s.Events.Where(n=>n.Voice==voice).OrderBy(n=>n.Beat).ToArray();if(line.Length==0)throw new ArgumentException("Λείπει φωνή.");double beat=4;var ticks=new List<double>();
                    for(var i=0;i<line.Length;i++)
                    {
                        var n=line[i];var value=Math.Round(n.Duration/0.85*16)/4.0;
                        if(Math.Abs(n.Beat-beat)>0.00001||Math.Abs(n.Duration-value/4.0*0.85)>0.00001||!Theory.InKey(n.Midi,new(KeyTonic:s.Tonic%12,KeyMode:s.Mode)))throw new ArgumentException("Λανθασμένη ανεξάρτητη γραμμή.");
                        if(voice==s.TargetVoice&&(i>=written.Length||written[i]!=new NotatedNote(n.Midi,value)))throw new ArgumentException("Στόχος διαφορετικός από ακουστική γραμμή.");
                        ticks.Add(value);beat+=value/4.0;
                    }
                    if(beat!=20||!patterns.Add(string.Join(',',ticks)))throw new ArgumentException("Μη ανεξάρτητος ρυθμός φωνών.");
                    if(voice==s.TargetVoice&&line.Length!=written.Length)throw new ArgumentException("Πλήθος στόχου.");
                    if(voice>0&&line.Max(n=>n.Midi)>=s.Events.Where(n=>n.Voice==voice-1).Min(n=>n.Midi))throw new ArgumentException("Διασταύρωση φωνών.");
                }
                if(!Pulses(s.Tonic).Append(new(s.Tonic,20,0.2,-1,45)).SequenceEqual(s.Events.Where(n=>n.Voice<0)))throw new ArgumentException("Λανθασμένο πλαίσιο πολυφωνίας.");
                if(s.Events.Any(n=>n.Voice>=count)||s.LengthBeats!=20.5)throw new ArgumentException("Επιπλέον φωνές.");return;
        }
        if(expected is null||!expected.Events.SequenceEqual(s.Events)||expected.LengthBeats!=s.LengthBeats)throw new ArgumentException("Η προχωρημένη ακολουθία δεν αντιστοιχεί στην απάντηση.");
    }
    public static GradeResult Grade(Exercise e,string response,Settings settings)
    {
        if(e.Category==Category.ChromaticHarmony)return TheoryV3.Grade(e with{Category=Category.HarmonicDictation},response,settings);
        var durations=e.Category is Category.Polyphony2 or Category.Polyphony3 or Category.Polyphony4 or Category.InnerVoice;
        var actual=NoteCodec.ResolveAnswer(response,durations,e.Score!.ExpectedText!);var expected=NoteCodec.Parse(e.Score!.ExpectedText!,durations);
        var matched=actual.Zip(expected).Count(p=>p.First==p.Second);var total=Math.Max(actual.Length,expected.Length);var exact=actual.SequenceEqual(expected);
        return new(exact,100.0*matched/total,exact?"Ακριβής προχωρημένη ακολουθία.":$"Σωστά στοιχεία: {matched} από {total}.",NoteCodec.Format(actual,durations));
    }
    public static MusicalScore Preview(Exercise e,string response,Settings? settings)
    {
        if(e.Category==Category.ChromaticHarmony)return TheoryV3.HarmonyScore(response,e.Score!.Tonic,e.Score.Mode,settings?.MinNote??21,settings?.MaxNote??108,"preview");
        var durations=e.Category is Category.Polyphony2 or Category.Polyphony3 or Category.Polyphony4 or Category.InnerVoice;
        return TheoryV3.MelodyScore(NoteCodec.Format(NoteCodec.ResolveAnswer(response,durations,e.Score!.ExpectedText!),durations),e.Score.Tonic,e.Score.Mode,"preview",durations);
    }
}
