namespace Studio.Core;
public sealed record ByzantineGenus(string Id,string Name,double[] Intervals){public override string ToString()=>Name;}
public static class ByzantineIntonation
{
    // Seven ascending intervals, explicitly ordered Ni -> Pa -> Vou -> Ga -> Di -> Ke -> Zo -> Ni.
    public static readonly ByzantineGenus[] All=[
        new("soft-diatonic","Μαλακό διατονικό",[12,10,8,12,12,10,8]),
        new("hard-diatonic","Σκληρό διατονικό — συγκερασμένο πρότυπο",[12,12,6,12,12,12,6]),
        new("soft-chromatic","Μαλακό χρωματικό — πρότυπο 7–18–5",[12,7,18,5,7,18,5]),
        new("hard-chromatic","Σκληρό χρωματικό",[4,6,20,4,12,6,20]),
        new("enharmonic","Εναρμόνιο — πρότυπο χρήστη από Γα",[12,6,12,12,12,6,12]),
        new("custom","Προσαρμοσμένα διαστήματα",[])];
    public const int MaximumAlteration = 7200;
    public static int ShiftOctave(int degree,int direction)
    {
        if(degree is <0 or >14||direction is not (-1 or 1))throw new ArgumentException("Μη έγκυρη αλλαγή οκτάβας.");
        var target=degree+7*direction;return target is >=0 and <=14?target:degree;
    }
    public static string DefaultGenre(string mode)=>mode switch{"second"=>"soft-chromatic","third"=>"enharmonic","plagal-second"=>"hard-chromatic",_=>"soft-diatonic"};
    public static double[] FromRoot(int degree,IReadOnlyList<double> rootIntervals)
    {
        ByzantineTuning.Validate(rootIntervals);if(degree is <0 or >6)throw new ArgumentException("Μη έγκυρη βάση.");var ni=new double[7];for(var i=0;i<7;i++)ni[(degree+i)%7]=rootIntervals[i];return ni;
    }
    public static double[] ModeIntervals(string id)
    {
        var mode=ByzantineModes.All.Single(m=>m.Id==id);
        double[] root=id switch{
            "first" or "plagal-first"=>[10,8,12,10,8,12,12],"second"=>[7,18,5,12,7,18,5],"third"=>[12,12,6,12,12,6,12],"fourth"=>[8,12,10,8,12,12,10],
            "plagal-second"=>[6,20,4,12,6,20,4],"grave"=>[12,10,8,12,10,8,12],_=>[12,10,8,12,12,10,8]};
        return FromRoot(mode.Degree,root);
    }
    public static double Offset(int degree,int anchor,IReadOnlyList<double> intervals,bool anchored)
    {
        ByzantineTuning.Validate(intervals);if(degree is <0 or >14||anchor is <0 or >6)throw new ArgumentException("Μη έγκυρος φθόγγος αναφοράς.");
        return 72*(degree/7)+intervals.Take(degree%7).Sum()+(anchored?ByzantineTuning.Diatonic.Take(anchor).Sum()-intervals.Take(anchor).Sum():0);
    }
    public static double Frequency(double referenceNi,int degree,int anchor,IReadOnlyList<double> intervals,bool anchored,double globalMoria,IReadOnlyList<double> alterations)
    {
        ValidateAlterations(alterations);if(!double.IsFinite(referenceNi)||referenceNi is <35 or >800||!double.IsFinite(globalMoria)||globalMoria is <-72 or >72)throw new ArgumentException("Μη έγκυρη αναφορά Hz ή μεταφορά.");
        var offset=Offset(degree,anchor,intervals,anchored);var hz=referenceNi*Math.Pow(2,(offset+globalMoria+alterations[degree%7])/72);
        if(hz is <35 or >2000)throw new ArgumentException("Ο φθόγγος είναι εκτός 35–2000 Hz.");return hz;
    }
    public static void ValidateAlterations(IReadOnlyList<double> values){if(values.Count!=7||values.Any(v=>!double.IsFinite(v)||v < -MaximumAlteration || v > MaximumAlteration))throw new ArgumentException("Απαιτούνται επτά αλλοιώσεις από -7200 έως +7200 μόρια.");}
    public static string PitchLabel(int degree)=>ByzantineTuning.Notes[degree%7]+(degree>=14?" δις άνω":degree>=7?" άνω":"");
    public static string NoteName(int degree,int anchor,IReadOnlyList<double> intervals,bool anchored,IReadOnlyList<double> alterations)
    {
        ValidateAlterations(alterations);var shift=Offset(degree,anchor,intervals,anchored)-(72*(degree/7)+ByzantineTuning.Diatonic.Take(degree%7).Sum())+alterations[degree%7];
        return PitchLabel(degree)+(Math.Abs(shift)<1e-6?"":$" — {(shift<0?"ύφεση":"δίεση")} {Math.Abs(shift):0.##} μορίων σε σχέση με τη διατονική αναφορά Νη");
    }
}
