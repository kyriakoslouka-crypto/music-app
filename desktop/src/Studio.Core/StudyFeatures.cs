namespace Studio.Core;

public sealed record StudyRecommendation(Category Category,Level Level,int Attempts,double Percentage,string Focus)
{
    public string Describe()=> $"{CategoryLabels.Name(Category)}, επίπεδο {Level}: {Percentage:F0}% σε {Attempts} προσπάθειες. Δούλεψε περισσότερο: {Focus}. Πρόταση: 10 στοχευμένες ερωτήσεις και νέα αξιολόγηση.";
}
public static class StudyCoach
{
    public static StudyRecommendation[] Recommend(IEnumerable<Attempt> history)=>history.GroupBy(a=>(a.Category,a.Level)).Select(g=>{
        var recent=g.OrderBy(a=>a.At).TakeLast(100).ToArray();
        var errors=recent.Where(a=>LearningAnalytics.EffectivePercentage(a)<90).GroupBy(a=>a.ExpectedName).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key,StringComparer.Ordinal).FirstOrDefault();
        return new StudyRecommendation(g.Key.Category,g.Key.Level,recent.Length,recent.Average(LearningAnalytics.EffectivePercentage),errors?.Key??"σταθεροποίηση και σταδιακή μετάβαση στο επόμενο επίπεδο");
    }).Where(r=>r.Attempts>=3&&r.Percentage<90).OrderBy(r=>r.Percentage).ThenByDescending(r=>r.Attempts).Take(5).ToArray();
    public static string Report(IEnumerable<Attempt> history)
    {
        var data=history.ToArray();var recommendations=Recommend(data);
        return recommendations.Length==0?data.Length<3?"Υποδείξεις μελέτης: χρειάζονται τουλάχιστον τρεις απαντήσεις στην ίδια κατηγορία και επίπεδο για πρώτη ένδειξη.":"Υποδείξεις μελέτης: δεν προκύπτει προς το παρόν τεκμηριωμένη αδυναμία από ομάδες τουλάχιστον τριών απαντήσεων. Συνέχισε για περισσότερα στοιχεία.":"Υποδείξεις μελέτης — ενδείξεις από την επίδοσή σου, όχι διάγνωση:\r\n"+string.Join("\r\n",recommendations.Select(r=>r.Describe()));
    }
    public static Settings Target(IReadOnlyList<Attempt> history,Settings fallback)
    {
        var r=Recommend(history).FirstOrDefault();if(r is null)return fallback;
        var last=history.Last(a=>a.Category==r.Category&&a.Level==r.Level);
        return (last.Configuration??fallback)with{Category=r.Category,Level=r.Level,Repetitions=1,SegmentStart=0,SegmentEnd=null,SoloVoice=-1};
    }
}
public static class SurpriseExams
{
    public static ExamEngine Create(Category[] categories,Level level,int count,int plays,int minutes,int seed)
    {
        if(categories is null||categories.Length==0||categories.Distinct().Count()!=categories.Length||categories.Any(c=>!Enum.IsDefined(c))||!Enum.IsDefined(level)||count is <1 or >100||plays is <1 or >10)throw new ArgumentException("Επίλεξε κατηγορίες, επίπεδο, 1–100 ερωτήσεις και 1–10 ακροάσεις.");
        var selected=categories.ToArray();var random=new Random(seed);for(var i=selected.Length-1;i>0;i--){var j=random.Next(i+1);(selected[i],selected[j])=(selected[j],selected[i]);}
        var profile=new ExamProfile("surprise-"+Guid.NewGuid().ToString("N"),"Απρόοπτο διαγώνισμα",level,selected,count,plays,70,"Ανεξάρτητο τυχαίο διαγώνισμα εξάσκησης. Δεν είναι επίσημη εξέταση ή πιστοποίηση. Οι λύσεις και οι παρτιτούρες εμφανίζονται μετά την ολοκλήρωση. Τυχαίες τονικότητες και πλήρης έκταση πιάνου. Αν οι ερωτήσεις είναι λιγότερες από τις κατηγορίες, εξετάζεται τυχαίο υποσύνολο.");
        return ExamEngine.Create(profile,seed,minutes);
    }
}
