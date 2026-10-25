using System.Globalization;
namespace Studio.Core;
public sealed record WrittenSymbol(int? Midi,double Beat,double Duration,int Voice,int Staff,int Denominator,int Dots,bool TieFrom,bool TieTo,bool Accent=false);
public sealed record NotationDocument(string Title,WrittenSymbol[] Symbols,int Staves,int Numerator,int Denominator,double Length,string Description,bool SeparateVoices=false);
public static class ScoreNotation
{
    public static bool CanReveal(bool answered,ExamState? exam)=>exam is null?answered:exam.Finished is not null;
    public static void RequireReveal(bool answered,ExamState? exam){if(!CanReveal(answered,exam))throw new InvalidOperationException("Η παρτιτούρα αποκαλύπτεται μετά την απάντηση, ή μετά την ολοκλήρωση ολόκληρης της εξέτασης.");}
    public static NotationDocument Build(Exercise e)
    {
        Theory.VerifyExercise(e);var raw=new List<(int? Midi,double Beat,double Duration,int Voice)>();
        var score=e.Score;var numerator=0;var denominator=4;double length=0;
        void Sequence(NotatedNote[] notes,int voice=0){double beat=0;foreach(var n in notes){raw.Add((n.Midi,beat,n.Ticks/4,voice));beat+=n.Ticks/4;}length=Math.Max(length,beat);}
        if(e.Category is Category.RhythmPatterns or Category.RhythmDictation or Category.RhythmReproduction){
            var meter=TheoryV2.Meters.Single(m=>m.Id==score!.MeterId);numerator=meter.Numerator;denominator=meter.Denominator;double beat=0;
            foreach(var bar in RhythmCodec.Parse(score!.ExpectedText!,meter,score.Bars))foreach(var c in bar){raw.Add((c.Rest?null:score.Tonic,beat,c.Ticks/4,0));beat+=c.Ticks/4;}length=beat;
        }else if(e.Category is Category.MelodyDictation or Category.TonalSolfege or Category.AtonalSolfege or Category.MelodyMemory)Sequence(NoteCodec.Parse(score!.ExpectedText!,e.Category==Category.MelodyDictation));
        else if(score is null){
            var together=e.Category is Category.Chords or Category.Inversions||e.Direction==Direction.Harmonic;
            for(var n=0;n<e.Notes.Length;n++)raw.Add((e.Notes[n],together?0:n,together?2:1,0));length=together?2:e.Notes.Length;
        }else{
            var events=e.Category==Category.Cadences?score.Events.Where(n=>n.Beat>=4).ToArray():score.Events.Where(n=>n.Voice>=0).ToArray();
            var start=events.Min(n=>n.Beat);var poly=e.Category is Category.Polyphony2 or Category.Polyphony3 or Category.Polyphony4 or Category.InnerVoice;
            if(poly){numerator=4;denominator=4;}
            if(e.Category==Category.Meters){var meter=TheoryV2.Meters.Single(m=>m.Id==score.MeterId);numerator=meter.Numerator;denominator=meter.Denominator;}
            foreach(var n in events){
                var duration=e.Category switch{
                    Category.Cadences or Category.HarmonicDictation or Category.Degrees or Category.ChromaticHarmony or Category.AdvancedAnalysis=>2,
                    Category.Scales or Category.Modes or Category.ModalColour=>0.5,
                    Category.Polyphony2 or Category.Polyphony3 or Category.Polyphony4 or Category.InnerVoice or Category.TwelveToneRows or Category.RowTransform=>n.Duration/0.85,
                    Category.ExtendedIntervals=>1,
                    Category.Meters=>4.0/denominator,
                    _=>n.Duration};
                raw.Add((n.Midi,n.Beat-start,duration,n.Voice));length=Math.Max(length,n.Beat-start+duration);
            }
        }
        var isPoly=e.Category is Category.Polyphony2 or Category.Polyphony3 or Category.Polyphony4 or Category.InnerVoice;
        var staves=isPoly?raw.Max(n=>n.Voice)+1:2;
        var symbols=new List<WrittenSymbol>();var measure=numerator==0?double.PositiveInfinity:numerator*4.0/denominator;
        (int Denom,int Dots,double Beats)[] values=[(1,0,4),(2,1,3),(2,0,2),(4,1,1.5),(4,0,1),(8,1,0.75),(8,0,0.5),(16,1,0.375),(16,0,0.25),(32,1,0.1875),(32,0,0.125),(64,1,0.09375),(64,0,0.0625)];
        foreach(var note in raw){
            var beat=note.Beat;var remaining=Math.Round(note.Duration*16)/16;
            if(remaining<=0||Math.Abs(remaining-note.Duration)>0.000001)throw new ArgumentException("Η διάρκεια δεν γράφεται με αξίες έως 64α.");
            var staff=isPoly?note.Voice:note.Midi is <60?1:0;var first=true;
            while(remaining>0){
                var room=double.IsPositiveInfinity(measure)?remaining:measure-beat%measure;if(room<0.000001)room=measure;
                var value=values.First(v=>v.Beats<=Math.Min(remaining,room)+0.000001);remaining-=value.Beats;
                symbols.Add(new(note.Midi,beat,value.Beats,note.Voice,staff,value.Denom,value.Dots,!first&&note.Midi.HasValue,remaining>0&&note.Midi.HasValue,e.Category==Category.Meters&&score!.Events.Any(n=>n.Beat==note.Beat&&n.Velocity>=90)));
                first=false;beat+=value.Beats;
            }
        }
        var description=CategoryLabels.Name(e.Category)+". Ζητούμενο μουσικό μέρος χωρίς εισαγωγή. "+(numerator>0?$"Μέτρο {numerator}/{denominator}. ":"Χωρίς υποχρεωτική ένδειξη μέτρου. ")+string.Join("; ",symbols.Select(n=>$"χρόνος {n.Beat.ToString("0.###",CultureInfo.InvariantCulture)}, φωνή {n.Voice+1}: {(n.Midi.HasValue?Theory.NoteName(n.Midi.Value):"παύση")} αξία 1/{n.Denominator}{(n.Dots>0?" παρεστιγμένη":"")}{(n.TieTo?" με σύζευξη":"")}{(n.Accent?" τονισμένο":"")}"));
        return new(CategoryLabels.Name(e.Category),symbols.ToArray(),staves,numerator,denominator,length,description,isPoly);
    }
}
