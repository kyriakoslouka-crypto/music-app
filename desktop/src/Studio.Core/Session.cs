using System.Text.Json;
namespace Studio.Core;

public sealed record Attempt(DateTimeOffset At, Category Category, Level Level, string ExerciseId,
    int[] Notes, string ExpectedId, string ExpectedName, string AnswerId, string AnswerName, bool Correct, Settings? Configuration = null, Exercise? Snapshot=null, double Percentage=0, string? Analysis=null, string? ExamId=null);
public sealed class Session
{
    public Exercise? Current { get; private set; }
    private bool answered;
    public void Begin(Exercise exercise) { Theory.VerifyExercise(exercise); Current = exercise; answered = false; }
    public Attempt Answer(string id, Level level, Settings? configuration = null)
    {
        if (Current is null || answered) throw new InvalidOperationException("Απαιτείται νέα άσκηση.");
        var choice = Current.Choices.SingleOrDefault(c => c.Id == id) ?? throw new ArgumentException("Επίλεξε απάντηση.");
        var evidence=Current.Category==Category.Cadences?CadenceAnalysis.FromScore(Current.Score!):null;
        var expected=evidence?.Id??Current.CorrectId;var expectedName=evidence?.Name??Current.CorrectName;
        answered = true;
        return new(DateTimeOffset.Now, Current.Category, level, Current.Id, Current.Notes,
            expected, expectedName, choice.Id, choice.Name, id == expected, configuration,Current,id==expected?100:0,evidence?.Describe());
    }
    public Attempt AnswerResponse(string response,Settings configuration)
    {
        if(Current is null||answered)throw new InvalidOperationException("Απαιτείται νέα άσκηση.");
        var result=WrittenGrader.Grade(Current,response,configuration);answered=true;
        return new(DateTimeOffset.Now,Current.Category,configuration.Level,Current.Id,Current.Notes,Current.CorrectId,Current.CorrectName,
            result.Canonical,response,result.Correct,configuration,Current,result.Percentage,result.Feedback);
    }
    public void Clear() { Current = null; answered = false; }
}
public sealed record Preferences(Settings Settings, string? SoundFontPath = null, int Bank = 0, int Preset = 0, string? DeviceId = null, bool CancelNvdaSpeech = false, bool AutomaticPractice = false, int AutomaticDelaySeconds = 10);
public sealed class ProgressStore
{
    private readonly string directory;
    public ProgressStore(string directory) { this.directory = directory; Directory.CreateDirectory(directory); }
    public List<Attempt> Load()
    {
        var data = Read<List<Attempt>>("history.json") ?? [];
        if (data.Any(a => a is null || a.Notes is null || a.Notes.Length == 0 || !double.IsFinite(a.Percentage) || a.Percentage is <0 or >100 || a.Notes.Any(n => n < 21 || n > 108)
            || !Enum.IsDefined(a.Category) || !Enum.IsDefined(a.Level) || string.IsNullOrWhiteSpace(a.ExpectedId)
            || string.IsNullOrWhiteSpace(a.AnswerId) || string.IsNullOrWhiteSpace(a.ExpectedName) || string.IsNullOrWhiteSpace(a.AnswerName)))
            throw new InvalidDataException("Μη έγκυρα δεδομένα ιστορικού. Το αρχείο δεν αντικαταστάθηκε.");
        return data;
    }
    public Preferences? LoadPreferences() => Read<Preferences>("settings.json");
    public void Save(IReadOnlyList<Attempt> attempts) => Write("history.json", attempts);
    public void SavePreferences(Preferences preferences) => Write("settings.json", preferences);
    private T? Read<T>(string name)
    {
        var path = Path.Combine(directory, name);
        if (!File.Exists(path)) return default;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? throw new InvalidDataException("Κενό αρχείο προόδου."); }
        catch (JsonException ex) { throw new InvalidDataException($"Το {name} είναι κατεστραμμένο. Δεν αντικαταστάθηκε. Ανάκτησέ το από αντίγραφο ασφαλείας.", ex); }
    }
    private void Write<T>(string name, T value)
    {
        var path = Path.Combine(directory, name);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(file, value); file.Flush(true); }
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
