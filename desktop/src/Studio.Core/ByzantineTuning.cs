using System.Globalization;
namespace Studio.Core;
public static class ByzantineTuning
{
    public static readonly string[] Notes=["Νη","Πα","Βου","Γα","Δι","Κε","Ζω","Νη άνω"];
    public static readonly double[] Diatonic=[12,10,8,12,12,10,8];
    public static double[] Parse(string text)
    {
        var values=text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(s=>double.Parse(s.Replace(',','.'),CultureInfo.InvariantCulture)).ToArray();Validate(values);return values;
    }
    public static void Validate(IReadOnlyList<double> intervals){if(intervals.Count!=7||intervals.Any(v=>!double.IsFinite(v)||v<=0||v>=72)||Math.Abs(intervals.Sum()-72)>0.000001)throw new ArgumentException("Γράψε επτά θετικά διαστήματα, με άθροισμα 72 μόρια, π.χ. 12 10 8 12 12 10 8.");}
    public static double Frequency(double baseHz,int degree,double adjustment,IReadOnlyList<double> intervals)
    {
        Validate(intervals);if(!double.IsFinite(baseHz)||baseHz is <35 or >800||degree is <0 or >7||!double.IsFinite(adjustment)||adjustment is < -72 or >72)throw new ArgumentException("Μη έγκυρη βάση, βαθμίδα ή μεταφορά μορίων.");
        var hz=baseHz*Math.Pow(2,(intervals.Take(degree).Sum()+adjustment)/72);if(hz is <35 or >2000)throw new ArgumentException("Επίλεξε συχνότητα μεταξύ 35 και 2000 Hz.");return hz;
    }
}
