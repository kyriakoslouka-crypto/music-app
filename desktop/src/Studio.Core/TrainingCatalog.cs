namespace Studio.Core;
/// <summary>Visible curriculum. Enum IDs and archived graders remain stable for saved history.</summary>
public static class TrainingCatalog
{
    public static bool IsActive(Category category)=>Enum.IsDefined(category)&&category is not (Category.RhythmPatterns or Category.Meters or Category.RhythmDictation or Category.RhythmReproduction or Category.MelodyDictation or Category.TonalSolfege or Category.AtonalSolfege);
    public static Category[] Categories=>Enum.GetValues<Category>().Where(IsActive).ToArray();
    public static Settings Normalize(Settings settings)=>IsActive(settings.Category)?settings:settings with{Category=Category.Intervals};
    public static Attempt[] ActiveHistory(IEnumerable<Attempt> history)=>history.Where(a=>IsActive(a.Category)).ToArray();
    public static ExamProfile[] ExamProfiles=>ExamCatalog.Profiles.Select(p=>p with{Id=p.Id+"-aural-v2",Categories=p.Categories.Where(IsActive).ToArray(),Disclosure=p.Disclosure+" Η παρούσα έκδοση εξαιρεί ρυθμό, μέτρα, μελωδικό ντικτέ και σολφέζ."}).ToArray();
    public static bool CanResume(ExamState state)=>state.Questions.All(q=>IsActive(q.Exercise.Category));
}
