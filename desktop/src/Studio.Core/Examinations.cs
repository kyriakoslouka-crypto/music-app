using System.Text;
using System.Text.Json;
namespace Studio.Core;

public sealed record ExamProfile(string Id,string Name,Level Level,Category[] Categories,int QuestionCount,int MaxPlays,double PassPercentage,string Disclosure,int TimeLimitMinutes=0)
{ public override string ToString()=>Name; }
public sealed record ExamQuestion(Exercise Exercise,Settings Settings);
public sealed record ExamAnswer(int Index,Attempt Attempt,bool Skipped=false);
public sealed record ExamState(int Schema,string Id,ExamProfile Profile,DateTimeOffset Started,ExamQuestion[] Questions,int CurrentIndex,int[] Plays,ExamAnswer[] Answers,DateTimeOffset? Finished=null,string? FinishReason=null);
public sealed record ExamSummary(int Answered,int Skipped,int Unanswered,double Percentage,bool Passed);
public static class ExamCatalog
{
    public const string Unofficial="Ανεξάρτητη εκπαιδευτική προσομοίωση δυσκολίας. Δεν είναι επίσημη εξέταση ABRSM, δεν χρησιμοποιεί επίσημα θέματα, δεν πιστοποιεί Grade και δεν καλύπτει όλη την επίσημη προφορική/φωνητική διαδικασία ή κάθε στοιχείο του syllabus. Η βαθμολογία είναι δική μας κλίμακα 0–100.";
    public static readonly ExamProfile[] Profiles=Build();
    private static ExamProfile[] Build()
    {
        var profiles=new List<ExamProfile>();
        Category[][] categories=[
            [Category.Intervals,Category.RhythmPatterns,Category.Scales],
            [Category.Intervals,Category.RhythmPatterns,Category.Meters,Category.Scales],
            [Category.Intervals,Category.Chords,Category.Cadences,Category.RhythmDictation],
            [Category.Chords,Category.Inversions,Category.Cadences,Category.MelodyDictation,Category.Meters],
            [Category.Inversions,Category.Cadences,Category.MelodyDictation,Category.HarmonicDictation,Category.Degrees],
            [Category.Cadences,Category.Modes,Category.MelodyDictation,Category.HarmonicDictation,Category.RhythmDictation],
            [Category.MelodyDictation,Category.HarmonicDictation,Category.Degrees,Category.Polyphony2,Category.Meters],
            [Category.MelodyDictation,Category.HarmonicDictation,Category.ChromaticHarmony,Category.Polyphony2,Category.AdvancedAnalysis]];
        Level[] levels=[Level.Beginner,Level.Beginner,Level.Elementary,Level.Elementary,Level.Intermediate,Level.Advanced,Level.Advanced,Level.Expert];
        for(var grade=1;grade<=8;grade++)profiles.Add(new("abrsm-inspired-"+grade,$"ABRSM-inspired Grade {grade} — ανεπίσημο",levels[grade-1],categories[grade-1],grade<=3?12:grade<=6?16:20,3,60,Unofficial));
        profiles.Add(new("university","Πανεπιστημιακή ακουστική αντίληψη — ανεξάρτητο",Level.Expert,[Category.MelodyDictation,Category.HarmonicDictation,Category.ChromaticHarmony,Category.Polyphony3,Category.InnerVoice,Category.RowTransform,Category.ExtendedChords,Category.AdvancedAnalysis],24,4,65,"Ανεξάρτητη πανεπιστημιακή εκπαιδευτική προσομοίωση, όχι διαπιστευμένη ή συγκεκριμένου ιδρύματος."));
        profiles.Add(new("pro","Professional / Pro — ανώτερη δυσκολία",Level.Professional,[Category.Polyphony4,Category.InnerVoice,Category.ChromaticHarmony,Category.TwelveToneRows,Category.RowTransform,Category.ExtendedIntervals,Category.ExtendedChords,Category.AdvancedAnalysis,Category.MelodyDictation,Category.HarmonicDictation],30,3,80,"Ανεξάρτητη προσομοίωση υψηλής δυσκολίας. Δεν αποτελεί επαγγελματική πιστοποίηση, επιτήρηση ή αξιολόγηση φωνής."));
        return profiles.ToArray();
    }
}
public sealed class ExamEngine
{
    private readonly Func<DateTimeOffset> now;
    public ExamState State{get;private set;}
    public bool Expired=>State.Finished is null&&State.Profile.TimeLimitMinutes>0&&now()>=State.Started.AddMinutes(State.Profile.TimeLimitMinutes);
    public bool Running=>State.Finished is null&&!Expired;
    public bool CurrentAnswered=>State.Answers.Any(a=>a.Index==State.CurrentIndex);
    public ExamQuestion Current=>State.Questions[State.CurrentIndex];
    public int PlaysRemaining=>Math.Max(0,State.Profile.MaxPlays-State.Plays[State.CurrentIndex]);
    private ExamEngine(ExamState state,Func<DateTimeOffset>? clock=null){Validate(state);State=state;now=clock??(()=>DateTimeOffset.UtcNow);}
    public static ExamEngine Create(ExamProfile profile,int seed,int minutes=0,Func<DateTimeOffset>? clock=null)
    {
        if(profile.QuestionCount is <1 or >200||profile.Categories.Length==0||!Enum.IsDefined(profile.Level))throw new ArgumentException("Μη έγκυρο προφίλ.");
        if(minutes is <0 or >180)throw new ArgumentException("Χρονικό όριο 0–180 λεπτά.");
        var generator=new ExerciseGenerator(seed);var random=new Random(seed);var questions=new List<ExamQuestion>();
        for(var i=0;i<profile.QuestionCount;i++)
        {
            var category=profile.Categories[i%profile.Categories.Length];
            var settings=new Settings(category,profile.Level,Direction:(Direction)(i%3),MinNote:21,MaxNote:108,KeyTonic:random.Next(12),KeyMode:i%2==0?TonalityMode.Major:TonalityMode.NaturalMinor,Tempo:profile.Level>=Level.Advanced?100:80,Bars:profile.Level>=Level.Intermediate?2:1);
            questions.Add(new(generator.Next(settings),settings));
        }
        for(var i=questions.Count-1;i>0;i--){var j=random.Next(i+1);(questions[i],questions[j])=(questions[j],questions[i]);}
        return new(new(1,Guid.NewGuid().ToString("N"),profile with{TimeLimitMinutes=minutes},(clock??(()=>DateTimeOffset.UtcNow))(),questions.ToArray(),0,new int[questions.Count],[]),clock);
    }
    public static ExamEngine Restore(ExamState state,Func<DateTimeOffset>? clock=null)=>new(state,clock);
    private void Active()
    {
        if(State.Finished is not null)throw new InvalidOperationException("Η εξέταση έχει ολοκληρωθεί.");
        if(Expired)throw new InvalidOperationException("Το χρονικό όριο έληξε. Τερμάτισε την εξέταση για αναφορά.");
    }
    public void CheckPlayAllowed(){Active();if(CurrentAnswered)throw new InvalidOperationException("Η ερώτηση έχει ήδη υποβληθεί.");if(PlaysRemaining==0)throw new InvalidOperationException("Εξαντλήθηκαν οι ακροάσεις αυτής της ερώτησης.");}
    public void RegisterSuccessfulPlay()
    {
        CheckPlayAllowed();var plays=State.Plays.ToArray();plays[State.CurrentIndex]++;State=State with{Plays=plays};
    }
    public void RefundFailedPlay()
    {
        if(State.Finished is not null||CurrentAnswered||State.Plays[State.CurrentIndex]==0)return;
        var plays=State.Plays.ToArray();plays[State.CurrentIndex]--;State=State with{Plays=plays};
    }
    public Attempt Submit(string response)
    {
        Active();if(CurrentAnswered)throw new InvalidOperationException("Η ερώτηση βαθμολογήθηκε ήδη.");if(State.Plays[State.CurrentIndex]==0)throw new InvalidOperationException("Άκουσε πρώτα το παράδειγμα.");
        var session=new Session();session.Begin(Current.Exercise);
        var attempt=(Current.Exercise.AnswerKind==AnswerKind.Choice?session.Answer(response,Current.Settings.Level,Current.Settings):session.AnswerResponse(response,Current.Settings))with{ExamId=State.Id};
        State=State with{Answers=State.Answers.Append(new(State.CurrentIndex,attempt)).ToArray()};return attempt;
    }
    public Attempt Skip()
    {
        Active();if(CurrentAnswered)throw new InvalidOperationException("Η ερώτηση απαντήθηκε ήδη.");var e=Current.Exercise;
        var attempt=new Attempt(now(),e.Category,Current.Settings.Level,e.Id,e.Notes,e.CorrectId,e.CorrectName,"__skip__","Παράλειψη",false,Current.Settings,e,0,"Η ερώτηση παραλείφθηκε.",State.Id);
        State=State with{Answers=State.Answers.Append(new(State.CurrentIndex,attempt,true)).ToArray()};return attempt;
    }
    public void Advance()
    {
        Active();if(!CurrentAnswered)throw new InvalidOperationException("Υπόβαλε ή παράλειψε την τρέχουσα ερώτηση.");
        if(State.CurrentIndex==State.Questions.Length-1)Finish("completed");else State=State with{CurrentIndex=State.CurrentIndex+1};
    }
    public void Finish(string reason="user-ended")
    {if(State.Finished is null)State=State with{Finished=now(),FinishReason=Expired?"time-limit":reason};}
    public ExamSummary Summary()
    {
        var percentage=State.Answers.Sum(a=>a.Attempt.Percentage)/State.Questions.Length;
        return new(State.Answers.Count(a=>!a.Skipped),State.Answers.Count(a=>a.Skipped),State.Questions.Length-State.Answers.Length,percentage,percentage>=State.Profile.PassPercentage);
    }
    public string Report(bool reveal)
    {
        var summary=Summary();var text=new StringBuilder();text.AppendLine(State.Profile.Name).AppendLine(State.Profile.Disclosure);
        text.AppendLine($"Ερώτηση {State.CurrentIndex+1} από {State.Questions.Length}. Απαντημένες: {summary.Answered}. Παραλείψεις: {summary.Skipped}.");
        if(State.Finished is null){text.AppendLine($"Υπόλοιπο ακροάσεων τρέχουσας: {PlaysRemaining}.");if(State.Profile.TimeLimitMinutes>0)text.AppendLine($"Χρονικό όριο έως {State.Started.AddMinutes(State.Profile.TimeLimitMinutes).ToLocalTime():g}.");return text.ToString();}
        text.AppendLine($"Τελικό ποσοστό: {summary.Percentage:F1}%. Δικό μας όριο επιτυχίας: {State.Profile.PassPercentage:F0}%. {(summary.Passed?"Επιτυχία στο ανεξάρτητο προφίλ.":"Χρειάζεται επανάληψη.")} Αναπάντητες: {summary.Unanswered}.");
        foreach(var category in State.Questions.Select(q=>q.Exercise.Category).Distinct())
        {
            var indices=State.Questions.Select((q,i)=>(q,i)).Where(p=>p.q.Exercise.Category==category).Select(p=>p.i).ToArray();
            var score=State.Answers.Where(a=>indices.Contains(a.Index)).Sum(a=>a.Attempt.Percentage)/indices.Length;
            text.AppendLine($"{CategoryLabels.Name(category)}: {score:F1}%.");
        }
        if(reveal)for(var i=0;i<State.Questions.Length;i++)
        {
            var answer=State.Answers.SingleOrDefault(a=>a.Index==i);text.AppendLine($"Ερώτηση {i+1}: {answer?.Attempt.Percentage??0:F1}%. Απάντηση: {answer?.Attempt.AnswerName??"Δεν δόθηκε"}. Σωστό: {State.Questions[i].Exercise.CorrectName}. {answer?.Attempt.Analysis}");
        }
        return text.ToString();
    }
    public static void Validate(ExamState state)
    {
        if(state is null||state.Profile is null||state.Questions is null||state.Plays is null||state.Answers is null||state.Answers.Any(a=>a is null||a.Attempt is null)||string.IsNullOrEmpty(state.Id)||state.Profile.Categories is null||state.Questions.Any(q=>q is null||q.Exercise is null||q.Settings is null)||!double.IsFinite(state.Profile.PassPercentage)||!Enum.IsDefined(state.Profile.Level))throw new InvalidDataException("Ελλιπής εξέταση.");
        if(state.Questions.Select(q=>q.Exercise.Id).Distinct().Count()!=state.Questions.Length)throw new InvalidDataException("Διπλή ταυτότητα ερώτησης.");
        if(state.Schema!=1||state.Id.Length!=32||state.Id.Any(c=>!Uri.IsHexDigit(c))||state.Profile.QuestionCount is <1 or >200||state.Questions.Length!=state.Profile.QuestionCount||state.CurrentIndex<0||state.CurrentIndex>=state.Questions.Length||state.Plays.Length!=state.Questions.Length||state.Profile.MaxPlays is <1 or >20||state.Profile.PassPercentage is <0 or >100||state.Profile.TimeLimitMinutes is <0 or >180||state.Answers.Select(a=>a.Index).Distinct().Count()!=state.Answers.Length)throw new InvalidDataException("Μη έγκυρη εξέταση.");
        if(state.Profile.Categories.Length==0||state.Profile.Categories.Any(c=>!Enum.IsDefined(c))||state.Finished<state.Started)
            throw new InvalidDataException("Μη έγκυρο προφίλ ή χρόνος εξέτασης.");
        if(Enumerable.Range(0,state.CurrentIndex).Any(i=>!state.Answers.Any(a=>a.Index==i)))
            throw new InvalidDataException("Λείπουν προηγούμενες απαντήσεις της εξέτασης.");
        foreach(var question in state.Questions)
        {
            try{Theory.Validate(question.Settings);Theory.VerifyExercise(question.Exercise);}
            catch(ArgumentException ex){throw new InvalidDataException("Μη έγκυρη αποθηκευμένη ερώτηση.",ex);}
            if(question.Settings.Category!=question.Exercise.Category||question.Settings.Level!=state.Profile.Level||!state.Profile.Categories.Contains(question.Exercise.Category))
                throw new InvalidDataException("Κατηγορία ή επίπεδο ερώτησης.");
        }
        if(state.Plays.Any(n=>n<0||n>state.Profile.MaxPlays))throw new InvalidDataException("Μετρητής ακροάσεων.");
        foreach(var answer in state.Answers)
        {
            if(!double.IsFinite(answer.Attempt.Percentage)||answer.Index<0||answer.Index>state.CurrentIndex||answer.Index>=state.Questions.Length||answer.Attempt.ExerciseId!=state.Questions[answer.Index].Exercise.Id||answer.Attempt.ExamId!=state.Id)throw new InvalidDataException("Απάντηση ξένης εξέτασης.");
            var question=state.Questions[answer.Index];var attempt=answer.Attempt;
            if(attempt.Category!=question.Exercise.Category||attempt.Level!=question.Settings.Level||attempt.ExpectedId!=question.Exercise.CorrectId||attempt.ExpectedName!=question.Exercise.CorrectName||attempt.Notes is null||!attempt.Notes.SequenceEqual(question.Exercise.Notes)||attempt.Configuration!=question.Settings)
                throw new InvalidDataException("Τα δεδομένα απάντησης δεν συμφωνούν με την ερώτηση.");
            if(answer.Skipped){if(answer.Attempt.AnswerId!="__skip__"||answer.Attempt.Percentage!=0||answer.Attempt.Correct)throw new InvalidDataException("Παράλειψη βαθμολογήθηκε.");continue;}
            if(state.Plays[answer.Index]==0)throw new InvalidDataException("Απάντηση χωρίς ακρόαση.");
            var q=state.Questions[answer.Index];var session=new Session();session.Begin(q.Exercise);
            Attempt checkedAnswer;
            try{checkedAnswer=q.Exercise.AnswerKind==AnswerKind.Choice?session.Answer(answer.Attempt.AnswerId,q.Settings.Level,q.Settings):session.AnswerResponse(answer.Attempt.AnswerName,q.Settings);}
            catch(Exception ex)when(ex is ArgumentException or FormatException or OverflowException){throw new InvalidDataException("Μη έγκυρη αποθηκευμένη απάντηση.",ex);}
            if(checkedAnswer.Correct!=answer.Attempt.Correct||Math.Abs(checkedAnswer.Percentage-answer.Attempt.Percentage)>0.0001)throw new InvalidDataException("Η αποθηκευμένη βαθμολογία δεν επαληθεύεται.");
        }
    }
}
public static class AtomicState
{
    public static void Write<T>(string path,T state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(file,state);file.Flush(true);}if(File.Exists(path))File.Copy(path,path+".bak",true);File.Move(temp,path,true);}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
public sealed class ExamStore(string directory)
{
    public ExamState? LoadCurrent()=>Read(Path.Combine(directory,"exam-current.json"));
    public void Save(ExamState state)
    {
        ExamEngine.Validate(state);AtomicState.Write(Path.Combine(directory,"exam-current.json"),state);
        if(state.Finished is not null)AtomicState.Write(Path.Combine(directory,"exams",state.Id+".json"),state);
    }
    public ExamState[] History()=>Directory.Exists(Path.Combine(directory,"exams"))?Directory.GetFiles(Path.Combine(directory,"exams"),"*.json").Select(p=>Read(p)!).OrderByDescending(e=>e.Started).ToArray():[];
    private static ExamState? Read(string path)
    {
        if(!File.Exists(path))return null;
        try{var state=JsonSerializer.Deserialize<ExamState>(File.ReadAllText(path))??throw new InvalidDataException("Κενή εξέταση.");ExamEngine.Validate(state);return state;}
        catch(JsonException e){throw new InvalidDataException("Κατεστραμμένη εξέταση· το αρχείο διατηρήθηκε: "+path,e);}
    }
}
public sealed record CategoryPerformance(Category Category,int Attempts,double Percentage);
public static class LearningAnalytics
{
    // Older history has no Percentage field: a correct legacy attempt deserializes as zero.
    // A modern passing rhythm attempt can legitimately score 80–99%; retain that score.
    public static double EffectivePercentage(Attempt attempt)=>attempt.Correct&&attempt.Percentage==0?100:Math.Clamp(attempt.Percentage,0,100);
    public static CategoryPerformance[] Weaknesses(IEnumerable<Attempt> history)=>history.GroupBy(a=>a.Category).Select(g=>{
        var recent=g.TakeLast(100).ToArray();return new CategoryPerformance(g.Key,recent.Length,recent.Average(EffectivePercentage));
    }).OrderBy(p=>p.Percentage).ThenByDescending(p=>p.Attempts).ToArray();
    public static Settings AdaptiveSettings(IReadOnlyList<Attempt> history,Settings fallback)
    {
        var weakest=Weaknesses(history).FirstOrDefault(p=>p.Attempts>=3);if(weakest is null)return fallback;
        var last=history.Last(a=>a.Category==weakest.Category);var level=last.Level;
        if(weakest.Percentage<50&&level>Level.Beginner)level--;else if(weakest.Percentage>85&&level<Level.Professional)level++;
        return (last.Configuration??fallback)with{Category=weakest.Category,Level=level,MinNote=21,MaxNote=108,Repetitions=1,SegmentStart=0,SegmentEnd=null,SoloVoice=-1};
    }
    public static ExamQuestion FreshAdaptive(IReadOnlyList<Attempt> history,Settings fallback,int seed)
    {
        var settings=AdaptiveSettings(history,fallback);var generator=new ExerciseGenerator(seed);
        var target=history.Where(a=>a.Category==settings.Category&&!a.Correct).GroupBy(a=>a.ExpectedId).OrderByDescending(g=>g.Count()).FirstOrDefault()?.Key;
        var candidate=generator.Next(settings);
        if(target is not null&&candidate.AnswerKind==AnswerKind.Choice&&!candidate.Choices.Any(c=>c.Id==target))
        {var original=history.Last(a=>a.Category==settings.Category);settings=settings with{Level=original.Level,Tempo=Math.Max(30,(original.Configuration?.Tempo??80)-10)};candidate=generator.Next(settings);}
        // Keep contrast questions so the learner cannot assume every answer is the weak concept.
        if(target is null||new Random(seed^7931).Next(4)==0)return new(candidate,settings);
        for(var i=0;i<128;i++){if(candidate.CorrectId==target)return new(candidate,settings);candidate=generator.Next(settings);}
        return new(candidate,settings);
    }
    public static void MergeExamAnswers(IList<Attempt> history,ExamState state)
    {
        foreach(var answer in state.Answers)
        {
            var found=-1;for(var i=0;i<history.Count;i++)if(history[i].ExamId==state.Id&&history[i].ExerciseId==answer.Attempt.ExerciseId){found=i;break;}
            if(found<0)history.Add(answer.Attempt);else history[found]=answer.Attempt;
        }
    }
    public static Attempt[] ReviewableMistakes(IEnumerable<Attempt> history)=>history.Where(a=>!a.Correct&&a.Snapshot is not null).Reverse().ToArray();
}
