using Studio.Core;
using System.Text.Json;
namespace Studio.Tests;
public static class ResponseOptionTests
{
    static void Check(bool condition,string message="Response option assertion failed"){if(!condition)throw new Exception(message);}
    public static void Register(Action<string,Action> test)
    {
        test("Response options: every written category and six levels has unambiguous graded choices",()=>{
            var covered=new HashSet<Category>();
            foreach(var mode in Enum.GetValues<TonalityMode>())foreach(var level in Enum.GetValues<Level>())foreach(var category in Enum.GetValues<Category>()){
                var settings=new Settings(category,level,MinNote:36,MaxNote:96,KeyMode:mode);var exercise=new ExerciseGenerator(582+(int)level).Next(settings);
                if(exercise.AnswerKind!=AnswerKind.Text)continue;covered.Add(category);var options=ResponseOptions.Create(exercise,settings);
                Check(options.Length is >=2 and <=4,category.ToString());Check(options.Select(o=>o.Name).Distinct().Count()==options.Length);Check(options.Select(o=>o.Id).Distinct().Count()==options.Length);
                Check(options.Count(o=>WrittenGrader.Grade(exercise,o.Id,settings).Correct)==1,category+" has multiple correct choices");
                foreach(var option in options){Check(option.Name.StartsWith("Επιλογή "));var session=new Session();session.Begin(exercise);var answer=session.AnswerResponse(option.Id,settings);Check(answer.Correct==(option.Id==exercise.Score!.ExpectedText));}
                Check(options.SequenceEqual(ResponseOptions.Create(exercise,settings)),"Resume changed option order");
                var restored=JsonSerializer.Deserialize<Exercise>(JsonSerializer.Serialize(exercise))!;Check(options.SequenceEqual(ResponseOptions.Create(restored,settings)));
            }
            var narrow=new Settings(Category.RhythmDictation,MeterId:"2/4",Bars:1);var single=new ExerciseGenerator(7).Next(narrow);Check(ResponseOptions.Create(single,narrow).Length>=2);
            Check(covered.Count==12,"Missing written category coverage: "+covered.Count);
        });
        test("Response options: Greek names distinguish all values, rests, ties and note order",()=>{
            var settings=new Settings(Category.RhythmDictation,Level.Professional,Bars:1);var exercise=new ExerciseGenerator(2).Next(settings);
            var label=ResponseOptions.Describe(exercise,"1/4 1/8 1/16 1/32 1/64 r1/64 1/32 1/16 1/8 1/4+1/32");
            Check(label.Contains("τέταρτο")&&label.Contains("όγδοο")&&label.Contains("δέκατο έκτο")&&label.Contains("τριακοστό δεύτερο")&&label.Contains("εξηκοστό τέταρτο")&&label.Contains("παύση εξηκοστό τέταρτο"));
            Check(ResponseOptions.Duration(5).Contains("δεμένο με"));Check(ResponseOptions.Duration(6)=="τέταρτο παρεστιγμένο");
            var melody=new ExerciseGenerator(3).Next(new(Category.MelodyDictation));var a=ResponseOptions.Describe(melody,"60:1/4 62:1/8");var b=ResponseOptions.Describe(melody,"62:1/4 60:1/8");Check(a!=b&&a.Contains("Ντο, τέταρτο")&&!a.Contains("οκτάβα"));
        });
        test("Response options: spoken harmonic choices distinguish quality, inversions and sevenths",()=>{
            var e=new ExerciseGenerator(2).Next(new(Category.HarmonicDictation));
            var label=ResponseOptions.Describe(e,"I ii6 V7 I64");Check(label.Contains("δεύτερη βαθμίδα, ελάσσονα")&&label.Contains("1η αναστροφή")&&label.Contains("με έβδομη")&&label.Contains("2η αναστροφή"));
            Check(ResponseOptions.Describe(e,"ii")!=ResponseOptions.Describe(e,"II"));Check(ResponseOptions.Describe(e,"V7")!=ResponseOptions.Describe(e,"Vmaj7"));
        });
        test("Response options: exam response selection survives save and restore with original graders",()=>{
            var profile=new ExamProfile("response-test","Δοκιμή επιλογών",Level.Intermediate,[Category.RhythmDictation,Category.MelodyDictation,Category.HarmonicDictation],3,3,60,"Ανεπίσημη δοκιμή");
            var exam=ExamEngine.Create(profile,91);var current=exam.Current;var options=ResponseOptions.Create(current.Exercise,current.Settings);var selected=options.Single(o=>WrittenGrader.Grade(current.Exercise,o.Id,current.Settings).Correct);
            exam.RegisterSuccessfulPlay();var attempt=exam.Submit(selected.Id);Check(attempt.Correct);var restored=ExamEngine.Restore(JsonSerializer.Deserialize<ExamState>(JsonSerializer.Serialize(exam.State))!);Check(restored.CurrentAnswered);Check(restored.State.Answers.Single().Attempt.Percentage==100);
        });
    }
}
