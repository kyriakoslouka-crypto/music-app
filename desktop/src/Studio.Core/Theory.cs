namespace Studio.Core;

public enum Category { Intervals, Chords, Inversions, Notes, Cadences, Scales, Modes, RhythmPatterns, Meters, RhythmDictation, RhythmReproduction, MelodyDictation, HarmonicDictation, Degrees, TonalSolfege, AtonalSolfege, MelodyMemory, Polyphony2, Polyphony3, Polyphony4, InnerVoice, ChromaticHarmony, TwelveToneRows, RowTransform, ModalColour, AdvancedAnalysis, ExtendedIntervals, ExtendedChords }
public enum Direction { Ascending, Descending, Harmonic }
public enum TonalityMode { Major, NaturalMinor }
public enum Level { Beginner, Elementary, Intermediate, Advanced, Expert, Professional }
public sealed record NamedPattern(string Id, string Name, int[] Steps);
public sealed record Option(string Id, string Name) { public override string ToString() => Name; }
public enum AnswerKind { Choice, Text, Taps }
public sealed record ScoreNote(int Midi, double Beat, double Duration, int Voice=0, int Velocity=85);
public sealed record MusicalScore(ScoreNote[] Events, double LengthBeats, string DefinitionId, int Tonic, TonalityMode Mode,
    string Prompt, string? ExpectedText=null, string MeterId="4/4", int Bars=1, int TargetVoice=0, string? Metadata=null);
public sealed record Exercise(string Id, Category Category, int[] Notes, string CorrectId, string CorrectName, Option[] Choices, Direction Direction,
    MusicalScore? Score=null, AnswerKind AnswerKind=AnswerKind.Choice);
public sealed record Settings(Category Category = Category.Intervals, Level Level = Level.Beginner,
    Direction Direction = Direction.Ascending, int MinNote = 48, int MaxNote = 84,
    int? RootPitchClass = null, int Tempo = 80, int Repetitions = 1, int Velocity = 85, bool Pedal = false,
    int? KeyTonic = null, TonalityMode KeyMode = TonalityMode.Major, string MeterId="4/4", int Bars=2, double SegmentStart=0, double? SegmentEnd=null, int SoloVoice=-1);

public static class Theory
{
    public static readonly string[] PitchNames = ["Ντο", "Ντο δίεση / Ρε ύφεση", "Ρε", "Ρε δίεση / Μι ύφεση", "Μι", "Φα", "Φα δίεση / Σολ ύφεση", "Σολ", "Σολ δίεση / Λα ύφεση", "Λα", "Λα δίεση / Σι ύφεση", "Σι"];
    public static readonly string[] IntervalNames = ["Ταυτοφωνία", "Μικρή δεύτερη", "Μεγάλη δεύτερη", "Μικρή τρίτη", "Μεγάλη τρίτη", "Καθαρή τέταρτη", "Τρίτονο", "Καθαρή πέμπτη", "Μικρή έκτη", "Μεγάλη έκτη", "Μικρή έβδομη", "Μεγάλη έβδομη", "Καθαρή οκτάβα", "Μικρή ένατη", "Μεγάλη ένατη", "Μικρή δέκατη", "Μεγάλη δέκατη", "Καθαρή ενδέκατη", "Αυξημένη ενδέκατη / ελαττωμένη δωδέκατη", "Καθαρή δωδέκατη", "Μικρή δέκατη τρίτη", "Μεγάλη δέκατη τρίτη", "Μικρή δέκατη τέταρτη", "Μεγάλη δέκατη τέταρτη", "Διπλή οκτάβα"];
    public static readonly NamedPattern[] Chords = [
        new("major", "Μείζων", [0,4,7]), new("minor", "Ελάσσων", [0,3,7]),
        new("diminished", "Ελαττωμένη", [0,3,6]), new("augmented", "Αυξημένη", [0,4,8]),
        new("dominant7", "Δεσπόζουσα με έβδομη", [0,4,7,10]),
        new("major7", "Μείζων με μείζονα έβδομη", [0,4,7,11]),
        new("minor7", "Ελάσσων με μικρή έβδομη", [0,3,7,10]),
        new("half-diminished7", "Ημιελαττωμένη με έβδομη", [0,3,6,10]),
        new("diminished7", "Πλήρως ελαττωμένη με έβδομη", [0,3,6,9]),
        new("minor-major7", "Ελάσσων με μείζονα έβδομη", [0,3,7,11])];
    public static bool InKey(int midi, Settings settings)
    {
        if (settings.KeyTonic is null) return true;
        int[] scale = settings.KeyMode == TonalityMode.Major ? [0,2,4,5,7,9,11] : [0,2,3,5,7,8,10];
        return scale.Contains((midi - settings.KeyTonic.Value + 120) % 12);
    }
    public static string NoteName(int midi) => $"{PitchNames[midi % 12]}, οκτάβα {midi / 12 - 1}";
    public static string InversionName(int inversion) => inversion == 0 ? "Βασική θέση" : $"{inversion}η αναστροφή";
    public static int[] Invert(int[] steps, int inversion)
    {
        if (steps.Length < 3 || inversion < 0 || inversion >= steps.Length) throw new ArgumentOutOfRangeException(nameof(inversion));
        return steps.Select((n, i) => i < inversion ? n + 12 : n).Order().ToArray();
    }
    public static IEnumerable<int> Distances(Level level) => level switch {
        Level.Beginner => [0,2,4,5,7,12], Level.Elementary => Enumerable.Range(0,13),
        Level.Intermediate => Enumerable.Range(0,17), _ => Enumerable.Range(0,25) };
    public static IEnumerable<NamedPattern> Patterns(Level level) => Chords.Take(level switch {
        Level.Beginner => 2, Level.Elementary => 4, Level.Intermediate => 7, _ => 10 });
    public static bool HasAudibleInversion(NamedPattern pattern) => pattern.Id is not ("augmented" or "diminished7");
    public static void VerifyExercise(Exercise e)
    {
        if(e is null||string.IsNullOrWhiteSpace(e.Id)||e.Notes is null||e.Choices is null||e.Choices.Any(c=>c is null||string.IsNullOrWhiteSpace(c.Id)||string.IsNullOrWhiteSpace(c.Name))||string.IsNullOrWhiteSpace(e.CorrectId)||string.IsNullOrWhiteSpace(e.CorrectName))
            throw new ArgumentException("Ελλιπή δεδομένα άσκησης.");
        if (!Enum.IsDefined(e.AnswerKind) || !Enum.IsDefined(e.Category) || !Enum.IsDefined(e.Direction) || e.Notes.Length == 0 || e.Notes.Any(n => n < 21 || n > 108)) throw new ArgumentException("Μη έγκυρη άσκηση.");
        if (e.Choices.Select(c => c.Id).Distinct().Count() != e.Choices.Length || (e.AnswerKind==AnswerKind.Choice && e.Choices.Count(c => c.Id == e.CorrectId) != 1)) throw new ArgumentException("Μη έγκυρες απαντήσεις.");
        if(e.Category>Category.Notes){TheoryV2.Verify(e);return;}
        if (e.Category == Category.Notes)
        {
            if (e.Notes.Length != 1 || e.CorrectId != $"note:{e.Notes[0]}") throw new ArgumentException("Η απάντηση δεν αντιστοιχεί στη νότα.");
            return;
        }
        if (e.Category == Category.Intervals)
        {
            if (e.Notes.Length != 2) throw new ArgumentException("Το διάστημα απαιτεί δύο νότες.");
            var distance = Math.Abs(e.Notes[1] - e.Notes[0]);
            if (distance > 24 || e.CorrectId != $"interval:{distance}" || (e.Direction == Direction.Descending ? e.Notes[1] > e.Notes[0] : e.Notes[1] < e.Notes[0])) throw new ArgumentException("Λανθασμένο διάστημα ή φορά.");
            return;
        }
        var parts = e.CorrectId.Split(':');
        var pattern = Chords.SingleOrDefault(c => c.Id == parts[0]) ?? throw new ArgumentException("Άγνωστη συγχορδία.");
        var inversion = 0;
        if (e.Category == Category.Inversions)
        {
            if (parts.Length != 2 || !int.TryParse(parts[1], out inversion) || !HasAudibleInversion(pattern)) throw new ArgumentException("Μη έγκυρη αναστροφή.");
        }
        else if (parts.Length != 1) throw new ArgumentException("Μη έγκυρη απάντηση συγχορδίας.");
        var formula = Invert(pattern.Steps, inversion);
        var root = e.Notes[0] - formula[0];
        if (!e.Notes.SequenceEqual(formula.Select(n => root + n))) throw new ArgumentException("Η συγχορδία δεν αντιστοιχεί στη θεωρητική δομή.");
    }
    public static void Validate(Settings s)
    {
        if (!Enum.IsDefined(s.Category) || !Enum.IsDefined(s.Level) || !Enum.IsDefined(s.Direction)) throw new ArgumentException("Μη έγκυρη κατηγορία ή επίπεδο.");
        if(s.SoloVoice is < -1 or > 6)throw new ArgumentException("Μη έγκυρη επιλογή φωνής.");
        if(!double.IsFinite(s.SegmentStart)||s.SegmentStart<0||s.SegmentStart>256||s.SegmentEnd.HasValue&&(!double.IsFinite(s.SegmentEnd.Value)||s.SegmentEnd<=s.SegmentStart||s.SegmentEnd>256))throw new ArgumentException("Μη έγκυρο τμήμα ακρόασης.");
        if(s.Bars is < 1 or > 8 || !TheoryV2.Meters.Any(m=>m.Id==s.MeterId)) throw new ArgumentException("Μη έγκυρο μέτρο ή αριθμός μέτρων.");
        if (s.MinNote < 21 || s.MaxNote > 108 || s.MinNote > s.MaxNote) throw new ArgumentException("Η έκταση πρέπει να είναι μεταξύ MIDI 21 και 108.");
        if (s.KeyTonic is < 0 or > 11 || !Enum.IsDefined(s.KeyMode)) throw new ArgumentException("Μη έγκυρη τονικότητα.");
        if (s.RootPitchClass is < 0 or > 11 || s.Tempo is < 30 or > 240 || s.Repetitions is < 0 or > 5 || s.Velocity is < 1 or > 127) throw new ArgumentException("Μη έγκυρες μουσικές ρυθμίσεις.");
    }
}

public sealed class ExerciseGenerator(int? seed = null)
{
    private readonly Random random = seed.HasValue ? new(seed.Value) : new();
    public Exercise Next(Settings settings)
    {
        Theory.Validate(settings);
        if(settings.Category>Category.MelodyMemory){var advanced=TheoryV4.Generate(settings,random);Theory.VerifyExercise(advanced);return advanced;}
        if(settings.Category>Category.RhythmReproduction)return TheoryV3.Generate(settings,random);
        if(settings.Category>Category.Notes)return TheoryV2.Generate(settings,random);
        var candidates = BuildCandidates(settings).Where(c => c.Notes.All(n => Theory.InKey(n, settings))).ToArray();
        if (candidates.Length == 0) throw new ArgumentException("Η έκταση ή η θεμέλιος δεν επιτρέπει ασκήσεις. Διεύρυνε την έκταση.");
        var picked = candidates[random.Next(candidates.Length)];
        var choices = candidates.Select(c => new Option(c.Answer, c.Label)).DistinctBy(c => c.Id).ToArray();
        var exercise = new Exercise(Guid.NewGuid().ToString("N"), settings.Category, picked.Notes, picked.Answer, picked.Label, choices, settings.Direction);
        Theory.VerifyExercise(exercise);
        return exercise;
    }
    private sealed record Candidate(int[] Notes, string Answer, string Label);
    private static IEnumerable<Candidate> BuildCandidates(Settings s)
    {
        for (var root = s.Category==Category.Inversions ? Math.Max(0,s.MinNote-11) : s.MinNote; root <= s.MaxNote; root++)
        {
            if (s.RootPitchClass.HasValue && root % 12 != s.RootPitchClass) continue;
            if (s.Category == Category.Notes) { yield return new([root], $"note:{root}", Theory.NoteName(root)); continue; }
            if (s.Category == Category.Intervals)
            {
                foreach (var d in Theory.Distances(s.Level))
                {
                    // The selected pitch class is the FIRST heard note for melodic intervals.
                    var second = s.Direction == Direction.Descending ? root - d : root + d;
                    if (second >= s.MinNote && second <= s.MaxNote)
                        yield return new([root, second], $"interval:{d}", Theory.IntervalNames[d]);
                }
                continue;
            }
            foreach (var chord in Theory.Patterns(s.Level))
            {
                // Symmetric chords have no uniquely audible inversion without a named root.
                if (s.Category == Category.Inversions && !Theory.HasAudibleInversion(chord)) continue;
                var positions = s.Category == Category.Inversions ? chord.Steps.Length : 1;
                for (var inversion = 0; inversion < positions; inversion++)
                {
                    var notes = Theory.Invert(chord.Steps, inversion).Select(n => root + n).ToArray();
                    if (notes[0]<s.MinNote || notes[^1] > s.MaxNote) continue;
                    var id = s.Category == Category.Inversions ? $"{chord.Id}:{inversion}" : chord.Id;
                    var label = s.Category == Category.Inversions ? $"{chord.Name}, {Theory.InversionName(inversion)}" : chord.Name;
                    yield return new(notes, id, label);
                }
            }
        }
    }
}
