namespace Studio.Core;

public static class LevelCurriculum
{
    public static bool PreferNote(int midi,Settings s)
    {
        var natural=new[]{0,2,4,5,7,9,11}.Contains(midi%12);
        var centre=(s.MinNote+s.MaxNote)/2.0;var span=s.MaxNote-s.MinNote;
        return s.Level switch{
            Level.Beginner=>natural&&PreferRegister(midi,s),
            Level.Elementary=>natural&&PreferRegister(midi,s),
            Level.Intermediate=>PreferRegister(midi,s),
            Level.Advanced=>PreferRegister(midi,s),
            Level.Expert=>!natural&&PreferRegister(midi,s),
            _=>!natural&&Math.Abs(midi-centre)>=span/3.0};
    }
    public static bool PreferRegister(int midi,Settings s)
    {
        var centre=(s.MinNote+s.MaxNote)/2.0;var radius=(s.MaxNote-s.MinNote)/2.0;
        return s.Level==Level.Professional?Math.Abs(midi-centre)>=radius*2/3:Math.Abs(midi-centre)<=Math.Max(1,radius*((int)s.Level+1)/6.0);
    }
    public static string[] Degrees(Level level,TonalityMode mode)
    {
        var baseDegrees=mode==TonalityMode.Major?Harmony.MajorDegrees:Harmony.MinorDegrees;
        if(level==Level.Beginner)return mode==TonalityMode.Major?["I","IV","V"]:["i","iv","V"];
        if(level==Level.Elementary)return baseDegrees.Take(5).ToArray();
        if(level==Level.Intermediate)return baseDegrees.ToArray();
        string[] figures=level switch{Level.Advanced=>["","6"],Level.Expert=>["","6","64"],_=>["","6","64","7","65","43","42"]};
        var tokens=new List<string>();
        foreach(var d in baseDegrees)foreach(var f in figures){
            var seventh=f is "7" or "65" or "43" or "42";
            if(d=="viio"&&mode==TonalityMode.NaturalMinor&&seventh&&f!="7")continue; // symmetric diminished sevenths have no audible inversion
            if(seventh&&((mode==TonalityMode.Major&&d is "I" or "IV")||(mode==TonalityMode.NaturalMinor&&d is "III" or "VI"))){if(f=="7")tokens.Add(d+"maj7");continue;}
            var stem=seventh&&d=="iio"?"iih":seventh&&d=="viio"&&mode==TonalityMode.Major?"viih":d;
            tokens.Add(stem+f);
        }
        return tokens.ToArray();
    }
    public static string Describe(Settings s)=>$"Επίπεδο {(int)s.Level+1} από 6. "+(s.Category switch{
        Category.Intervals=>$"Διαστήματα έως {Theory.Distances(s.Level).Max()} ημιτόνια.",
        Category.Chords or Category.Inversions=>s.Level>=Level.Expert?"Έμφαση σε τετράφωνες συγχορδίες και δυσκολότερες αναστροφές.":"Προοδευτική εισαγωγή τύπων συγχορδιών και αναστροφών.",
        Category.Notes=>"Από φυσικές νότες της κεντρικής περιοχής σε αλλοιωμένες νότες και ακραία register της επιλεγμένης έκτασης.",
        Category.MelodyDictation or Category.TonalSolfege or Category.AtonalSolfege or Category.MelodyMemory=>$"Μήκος {new[]{4,6,8,12,16,24}[(int)s.Level]} νοτών και αυξανόμενα άλματα.",
        Category.RhythmDictation or Category.RhythmReproduction=>"Προοδευτικά πυκνότερες αξίες. Στο Pro εξηκοστά τέταρτα σε κάθε μέτρο.",
        _=>"Προοδευτικό ρεπερτόριο, πυκνότητα και ακουστική πολυπλοκότητα."})+" Η τονικότητα, η έκταση, το BPM και οι επαναλήψεις που επέλεξες διατηρούνται.";
}
