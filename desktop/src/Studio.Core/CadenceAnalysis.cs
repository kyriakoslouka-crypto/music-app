namespace Studio.Core;

public sealed record CadenceEvidence(string Id,string Name,string Formula,string FirstNotes,string LastNotes,string Bass)
{
    public string Describe()=> $"Ανάλυση των δύο τελευταίων συγχορδιών: {Formula}. Νότες: {FirstNotes}, προς {LastNotes}. Μπάσο: {Bass}.";
}
public static class CadenceAnalysis
{
    public static CadenceEvidence FromScore(MusicalScore score)
    {
        var chords=score.Events.GroupBy(n=>n.Beat).OrderBy(g=>g.Key).TakeLast(2).ToArray();
        if(chords.Length!=2)throw new ArgumentException("Η πτώση χρειάζεται δύο πλήρεις συγχορδίες.");
        return Infer(score.Tonic,score.Mode,chords[0].Select(n=>n.Midi),chords[1].Select(n=>n.Midi));
    }
    public static CadenceEvidence Infer(int tonic,TonalityMode mode,IEnumerable<int> first,IEnumerable<int> last)
    {
        var a=first.Order().ToArray();var b=last.Order().ToArray();
        if(a.Length<3||b.Length<3||a.Concat(b).Any(n=>n is <21 or >108))throw new ArgumentException("Απαιτούνται πλήρεις συγχορδίες πιάνου για αναγνώριση πτώσης.");
        int Pc(int n)=>(n-tonic+120)%12;
        var x=a.Select(Pc).Distinct().Order().ToArray();var y=b.Select(Pc).Distinct().Order().ToArray();
        var minor=mode==TonalityMode.NaturalMinor;
        int[] i=minor?[0,3,7]:[0,4,7],iv=minor?[0,5,8]:[0,5,9],vi=minor?[0,3,8]:[0,4,9],v=[2,7,11];
        var id="";var formula="";var root=0;
        if(x.SequenceEqual(iv)&&y.SequenceEqual(i)){id="plagal";formula=minor?"iv–i":"IV–I";root=5;}
        else if(x.SequenceEqual(v)&&y.SequenceEqual(i)){
            id=Pc(a[0])==7&&Pc(b[0])==0&&Pc(b[^1])==0?"pac":"iac";formula=minor?"V–i":"V–I";root=7;
        }
        else if(y.SequenceEqual(v)){id="half";formula=minor?"i–V":"I–V";root=0;}
        else if(x.SequenceEqual(v)&&y.SequenceEqual(vi)){id="deceptive";formula=minor?"V–VI":"V–vi";root=7;}
        if(id.Length==0)throw new ArgumentException("Οι πραγματικές συγχορδίες δεν σχηματίζουν υποστηριζόμενη πτώση.");
        string Names(int[] chord,int chordRoot)=>string.Join("–",chord.Select(Pc).Distinct().OrderBy(n=>(n-chordRoot+12)%12).Select(n=>Theory.PitchNames[(tonic+n)%12]));
        var name=TheoryV2.Cadences.Single(c=>c.Id==id).Name;
        return new(id,name,formula,Names(a,root),Names(b,id=="half"?7:id=="deceptive"?(minor?8:9):0),Theory.PitchNames[a[0]%12]+" προς "+Theory.PitchNames[b[0]%12]);
    }
}
