namespace Studio.Core;
public sealed record IsonTuning(double[] Intervals,double[] Alterations,string GenusId,bool Anchored)
{
    public void Validate(){if(Intervals is null||Alterations is null)throw new ArgumentException("Ελλιπές προσωπικό κούρδισμα.");ByzantineTuning.Validate(Intervals);ByzantineIntonation.ValidateAlterations(Alterations);if(!ByzantineIntonation.All.Any(g=>g.Id==GenusId))throw new ArgumentException("Άγνωστο γένος.");}
    public IsonTuning Copy()=>new(Intervals.ToArray(),Alterations.ToArray(),GenusId,Anchored);
    public static Dictionary<string,IsonTuning> CopyMap(IReadOnlyDictionary<string,IsonTuning>? source){var copy=new Dictionary<string,IsonTuning>();if(source is null)return copy;if(source.Count>8)throw new ArgumentException("Απαιτούνται έως οκτώ προσωπικά κουρδίσματα.");foreach(var (id,tuning) in source){if(!ByzantineModes.All.Any(m=>m.Id==id)||tuning is null)throw new ArgumentException("Άγνωστος ήχος.");tuning.Validate();copy[id]=tuning.Copy();}return copy;}
}
