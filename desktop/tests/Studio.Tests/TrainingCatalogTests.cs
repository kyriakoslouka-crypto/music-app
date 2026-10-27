using Studio.Core;
using System.Text.Json;
namespace Studio.Tests;
public static class TrainingCatalogTests
{
    static void Check(bool value){if(!value)throw new Exception("Active curriculum assertion failed");}
    public static void Register(Action<string,Action> test)
    {
        test("Active curriculum: exactly seven archived categories are excluded with stable saved IDs",()=>{
            Category[] removed=[Category.RhythmPatterns,Category.Meters,Category.RhythmDictation,Category.RhythmReproduction,Category.MelodyDictation,Category.TonalSolfege,Category.AtonalSolfege];
            Check(TrainingCatalog.Categories.Length==21&&removed.All(c=>!TrainingCatalog.IsActive(c))&&TrainingCatalog.IsActive(Category.HarmonicDictation)&&TrainingCatalog.IsActive(Category.MelodyMemory));
            Check((int)Category.HarmonicDictation==12&&(int)Category.MelodyMemory==16);
            foreach(var c in removed){var old=new Settings(c,Level.Expert,Tempo:123);var current=TrainingCatalog.Normalize(old);Check(current.Category==Category.Intervals&&current.Tempo==123&&old.Category==c);}
        });
        test("Active curriculum: all new exam profiles generate only retained categories and restore",()=>{
            Check(TrainingCatalog.ExamProfiles.Length==10);
            foreach(var profile in TrainingCatalog.ExamProfiles)for(var seed=0;seed<5;seed++){
                Check(profile.Categories.Length>0&&profile.Categories.All(TrainingCatalog.IsActive));var engine=ExamEngine.Create(profile,seed);
                Check(engine.State.Questions.All(q=>TrainingCatalog.IsActive(q.Exercise.Category))&&TrainingCatalog.CanResume(engine.State));
                var restored=ExamEngine.Restore(JsonSerializer.Deserialize<ExamState>(JsonSerializer.Serialize(engine.State))!);Check(restored.State.Questions.Length==profile.QuestionCount);
            }
            var archived=ExamEngine.Create(ExamCatalog.Profiles[0] with{Categories=[Category.RhythmPatterns]},1).State;
            Check(!TrainingCatalog.CanResume(archived)&&ExamEngine.Restore(archived).State.Questions.Length==archived.Questions.Length);
        });
        test("Active curriculum: recommendations filter archived history without deleting it",()=>{
            var history=Enumerable.Range(0,6).Select(i=>new Attempt(DateTimeOffset.UtcNow,i<3?Category.RhythmPatterns:Category.Chords,Level.Beginner,"old"+i,[],"expected","απάντηση","wrong","λάθος",false)).ToList();
            var active=TrainingCatalog.ActiveHistory(history);Check(history.Count==6&&active.Length==3&&StudyCoach.Target(active,new()).Category==Category.Chords&&LearningAnalytics.AdaptiveSettings(active,new()).Category==Category.Chords);
        });
        test("Automatic exams: default and disabled preferences survive legacy and new JSON",()=>{
            var legacy=JsonSerializer.Deserialize<Preferences>("{\"Settings\":{\"Category\":0}}")!;Check(legacy.AutomaticExams);
            var disabled=new Preferences(new(),AutomaticExams:false);Check(!JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(disabled))!.AutomaticExams);
        });
    }
}
