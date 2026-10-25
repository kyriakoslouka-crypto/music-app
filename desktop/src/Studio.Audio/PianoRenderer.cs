using MeltySynth;
using Studio.Core;
namespace Studio.Audio;
public sealed record PianoPreset(int Bank, int Program, string Name) { public override string ToString() => $"{Name} ({Bank}:{Program})"; }
public enum MidiAction { NoteOff, Pedal, NoteOn }
public sealed record ScheduledEvent(int Frame, MidiAction Action, int Key, int Value, int Channel=0);
public static class Schedule
{
    public static ScheduledEvent[] Create(Exercise exercise, Settings settings, int sampleRate = 44100)
    {
        Theory.Validate(settings);
        Theory.VerifyExercise(exercise);
        if (exercise.Notes.Length == 0 || exercise.Notes.Any(n => n < 21 || n > 108)) throw new ArgumentException("Μη έγκυρες νότες.");
        if(exercise.Score is not null)return CreateScore(exercise.Score,settings,sampleRate);
        var events = new List<ScheduledEvent>();
        var beat = sampleRate * 60.0 / settings.Tempo;
        var harmonic = exercise.Category is Category.Chords or Category.Inversions || settings.Direction == Direction.Harmonic;
        var notes=harmonic?exercise.Notes.Distinct().ToArray():exercise.Notes;
        var length = harmonic ? beat * 2 : beat * notes.Length;
        for (var repetition = 0; repetition < Math.Max(1, settings.Repetitions); repetition++)
        {
            var start = (int)(beat * 0.4 + repetition * (length + beat * 1.5));
            if (settings.Pedal) events.Add(new(start, MidiAction.Pedal, 64, 127));
            for (var i = 0; i < notes.Length; i++)
            {
                var on = start + (harmonic ? 0 : (int)(beat * i));
                events.Add(new(on, MidiAction.NoteOn, notes[i], settings.Velocity));
                events.Add(new(on + (int)(beat * (harmonic ? 1.8 : 0.8)), MidiAction.NoteOff, notes[i], 0));
            }
            events.Add(new(start + (int)(length + beat * 0.3), MidiAction.Pedal, 64, 0));
        }
        return events.OrderBy(e => e.Frame).ThenBy(e => e.Action).ToArray();
    }
    public static ScheduledEvent[] CreateScore(MusicalScore original,Settings settings,int rate=44100)
    {
        Theory.Validate(settings);
        if(settings.SoloVoice>=0&&!original.Events.Any(n=>n.Voice==settings.SoloVoice))throw new ArgumentException("Η επιλεγμένη φωνή δεν υπάρχει σε αυτό το παράδειγμα.");
        var selected=settings.SoloVoice<0?original:original with{Events=original.Events.Where(n=>n.Voice<0||n.Voice==settings.SoloVoice).ToArray()};
        var score=ScoreSegments.Slice(selected,settings.SegmentStart,settings.SegmentEnd);
        if(score.Events.Any(n=>n.Midi is <21 or >108||n.Duration<=0||n.Beat<0||!double.IsFinite(n.Duration)||!double.IsFinite(n.Beat)))throw new ArgumentException("Μη έγκυρες νότες προεπισκόπησης.");
        var channels=score.Events.Select(n=>n.Voice<0?15:n.Voice).Distinct().ToArray();
        if(channels.Any(c=>c is <0 or >15 or 9))throw new ArgumentException("Μη έγκυρη φωνή.");
        var events=new List<ScheduledEvent>();var secondsPerBeat=60.0/settings.Tempo;
        var count=Math.Max(1,settings.Repetitions);
        var totalSeconds=(0.4+count*(score.LengthBeats+1.5))*secondsPerBeat;
        if(totalSeconds>160)throw new ArgumentException("Το παράδειγμα με αυτές τις επαναλήψεις υπερβαίνει τα 160 δευτερόλεπτα. Μείωσε επαναλήψεις/μέτρα ή αύξησε BPM.");
        var usePedal=settings.Pedal&&original.DefinitionId!="dictation"&&!TheoryV2.Rhythms.Any(r=>r.Id==original.DefinitionId)&&!TheoryV2.Meters.Any(m=>m.Id==original.DefinitionId);
        for(var repetition=0;repetition<count;repetition++)
        {
            var offset=0.4+repetition*(score.LengthBeats+1.5);
            int Frame(double beat)=>(int)Math.Round((offset+beat)*secondsPerBeat*rate);
            foreach(var n in score.Events)
            {
                var velocity=Math.Clamp((int)Math.Round(n.Velocity*settings.Velocity/85.0),1,127);
                events.Add(new(Frame(n.Beat),MidiAction.NoteOn,n.Midi,velocity,n.Voice<0?15:n.Voice));
                events.Add(new(Frame(n.Beat+n.Duration),MidiAction.NoteOff,n.Midi,0,n.Voice<0?15:n.Voice));
            }
            if(usePedal)foreach(var onset in score.Events.Select(n=>n.Beat).Distinct().Order())
            {foreach(var channel in channels)events.Add(new(Frame(onset),MidiAction.Pedal,64,0,channel));foreach(var channel in channels)events.Add(new(Frame(onset),MidiAction.Pedal,64,127,channel));}
            // Cadence chords must release before the reference pause and before each resolution.
            if(usePedal&&TheoryV2.Cadences.Any(c=>c.Id==original.DefinitionId))
                foreach(var end in score.Events.Select(n=>n.Beat+n.Duration).Distinct())
                    foreach(var channel in channels)events.Add(new(Frame(end),MidiAction.Pedal,64,0,channel));
            foreach(var channel in channels)events.Add(new(Frame(score.LengthBeats),MidiAction.Pedal,64,0,channel));
        }
        return events.OrderBy(e=>e.Frame).ThenBy(e=>e.Action).ToArray();
    }
}
public sealed class PianoRenderer
{
    public const int SampleRate = 44100;
    private readonly SoundFont? soundFont;
    private readonly MaestroPiano? maestro;
    public PianoPreset[] Presets { get; }
    public PianoRenderer(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Δεν βρέθηκε η βιβλιοθήκη πιάνου SF2.", path);
        if (path.EndsWith(".studio-piano.json", StringComparison.OrdinalIgnoreCase))
        {
            maestro = new MaestroPiano(path); Presets = [new(0,0,"Maestro Concert Grand — Yamaha CF-3")]; return;
        }
        soundFont = new SoundFont(path);
        Presets = soundFont.Presets.Select(p => new PianoPreset(p.BankNumber, p.PatchNumber, p.Name)).ToArray();
        if (Presets.Length == 0) throw new InvalidDataException("Η βιβλιοθήκη δεν έχει όργανα.");
    }
    public float[] Render(Exercise exercise, Settings settings, PianoPreset preset, CancellationToken cancellation = default)
    {
        if (!Presets.Contains(preset)) throw new ArgumentException("Το όργανο δεν ανήκει στη βιβλιοθήκη.");
        var events = Schedule.Create(exercise, settings);
        return RenderScheduled(events,settings,preset,cancellation);
    }
    public float[] RenderPreview(MusicalScore score,Settings settings,PianoPreset preset,CancellationToken token=default)=>RenderScheduled(Schedule.CreateScore(score,settings with{SegmentStart=0,SegmentEnd=null,SoloVoice=-1}),settings,preset,token);
    private float[] RenderScheduled(ScheduledEvent[] events,Settings settings,PianoPreset preset,CancellationToken cancellation)
    {
        if(maestro is not null)return maestro.RenderScheduled(events,settings,cancellation);
        var frames = events[^1].Frame + SampleRate * 4;
        if (frames > SampleRate * 180) throw new ArgumentException("Υπερβολική διάρκεια.");
        var synth = new Synthesizer(soundFont!, new SynthesizerSettings(SampleRate) { MaximumPolyphony = 256, EnableReverbAndChorus = false });
        foreach(var channel in events.Select(e=>e.Channel).Distinct()){synth.ProcessMidiMessage(channel,0xB0,0,preset.Bank);synth.ProcessMidiMessage(channel,0xC0,preset.Program,0);}
        var result = new float[frames * 2];
        var left = new float[1024]; var right = new float[1024];
        var position = 0; var nextEvent = 0;
        while (position < frames)
        {
            cancellation.ThrowIfCancellationRequested();
            while (nextEvent < events.Length && events[nextEvent].Frame == position)
            {
                var e = events[nextEvent++];
                switch (e.Action)
                {
                    case MidiAction.NoteOn: synth.NoteOn(e.Channel, e.Key, e.Value); break;
                    case MidiAction.NoteOff: synth.NoteOff(e.Channel, e.Key); break;
                    case MidiAction.Pedal: synth.ProcessMidiMessage(e.Channel, 0xB0, 64, e.Value); break;
                }
            }
            var until = nextEvent < events.Length ? events[nextEvent].Frame : frames;
            var count = Math.Min(left.Length, Math.Min(until - position, frames - position));
            synth.Render(left.AsSpan(0, count), right.AsSpan(0, count));
            for (var i = 0; i < count; i++) { result[2 * (position + i)] = left[i]; result[2 * (position + i) + 1] = right[i]; }
            position += count;
        }
        var peak = result.Max(v => Math.Abs(v));
        var gain = peak > 0.9f ? 0.9f / peak : 1f;
        for (var i = 0; i < result.Length; i++)
        {
            var tail = Math.Min(1f, (result.Length - 1 - i) / (SampleRate * 0.2f));
            result[i] *= gain * tail;
        }
        return result;
    }
}
