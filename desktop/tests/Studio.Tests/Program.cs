using Studio.Core;
using Studio.Audio;
using System.Text.Json;
using System.Diagnostics;
if(args.Length==2 && args[0]=="--download-maestro")
{
    using var client=new HttpClient{Timeout=TimeSpan.FromMinutes(10)};
    var last=-1;
    var progress=new Progress<PianoInstallProgress>(p=>{if(p.Percent/10!=last){last=p.Percent/10;Console.WriteLine(p.Message);}});
    await MaestroLibrary.DownloadAsync(client,args[1],progress,CancellationToken.None);
    using var file=File.OpenRead(args[1]);
    var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file));
    if(!hash.Equals(MaestroLibrary.ArchiveSha256,StringComparison.OrdinalIgnoreCase))throw new Exception("Download checksum mismatch");
    Console.WriteLine("DOWNLOAD VERIFIED "+hash);return 0;
}
if(args.Length==3 && args[0]=="--prepare-maestro")
{
    var extracted=args[2]+".original";
    MaestroLibrary.VerifyAndExtract(args[1],extracted,CancellationToken.None);
    var last=-1;
    var progress=new Progress<PianoInstallProgress>(p=>{if(p.Percent/10!=last){last=p.Percent/10;Console.WriteLine(p.Message);}});
    var result=MaestroLibrary.Prepare(extracted,args[2],(input,output,token)=>{
        token.ThrowIfCancellationRequested();
        var start=new ProcessStartInfo("ffmpeg"){UseShellExecute=false};
        foreach(var a in new[]{"-nostdin","-hide_banner","-loglevel","error","-i",input,"-c:a","pcm_s16le",output})start.ArgumentList.Add(a);
        using var process=Process.Start(start)??throw new Exception("Cannot start ffmpeg");
        process.WaitForExit();if(process.ExitCode!=0)throw new Exception("FLAC decoding failed");
    },progress,CancellationToken.None);
    Console.WriteLine("PREPARED "+result);return 0;
}
var tests = new List<(string Name, Action Run)>();
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
void Test(string name, Action run) => tests.Add((name, run));
Test("All levels/categories/directions: 21,600 exercises in range with correct answer", () => {
    var generator = new ExerciseGenerator(1234);
    foreach (var level in Enum.GetValues<Level>()) foreach (var category in Enum.GetValues<Category>().Where(c=>c<=Category.Notes)) foreach (var direction in Enum.GetValues<Direction>())
    for (var i = 0; i < 300; i++) {
        var e = generator.Next(new(category, level, direction, 21, 108));
        Check(e.Notes.All(n => n >= 21 && n <= 108)); Check(e.Choices.Count(c => c.Id == e.CorrectId) == 1);
        Check(e.Choices.Select(c => c.Id).Distinct().Count() == e.Choices.Length);
        if (category == Category.Intervals) {
            var distance = Math.Abs(e.Notes[1] - e.Notes[0]); Check(e.CorrectId == $"interval:{distance}");
            Check(e.CorrectName == Theory.IntervalNames[distance]);
            Check(direction == Direction.Descending ? e.Notes[1] <= e.Notes[0] : e.Notes[1] >= e.Notes[0]);
        }
    }
});
Test("Known music theory formulas and inversions", () => {
    Check(Theory.Chords.Single(c => c.Id == "dominant7").Steps.SequenceEqual(new[] {0,4,7,10}));
    Check(Theory.Chords.Single(c => c.Id == "minor-major7").Steps.SequenceEqual(new[] {0,3,7,11}));
    Check(Theory.Chords.Single(c => c.Id == "half-diminished7").Steps.SequenceEqual(new[] {0,3,6,10}));
    Check(Theory.Invert([0,4,7], 1).SequenceEqual(new[] {4,7,12}));
    Check(Theory.Invert([0,3,7,10], 3).SequenceEqual(new[] {10,12,15,19}));
    Throws<ArgumentOutOfRangeException>(() => Theory.Invert([0,4,7], 3));
});
Test("Chord answer matches independently reconstructed pitch-class formula", () => {
    var gen = new ExerciseGenerator(21);
    foreach (var category in new[] { Category.Chords, Category.Inversions }) for (var i = 0; i < 1500; i++) {
        var e = gen.Next(new(category, Level.Professional, MinNote: 21, MaxNote: 108, RootPitchClass: 0));
        var split = e.CorrectId.Split(':'); var pattern = Theory.Chords.Single(c => c.Id == split[0]);
        Check(e.Notes.Select(n => n % 12).Order().SequenceEqual(pattern.Steps.Select(n => n % 12).Order()));
        if (split.Length == 2) { var inversion = int.Parse(split[1]); Check(e.Notes[0] % 12 == pattern.Steps[inversion]); }
    }
});
Test("Symmetric chords are never given ambiguous inversion labels", () => {
    var e = new ExerciseGenerator(1).Next(new(Category.Inversions, Level.Professional));
    Check(!e.Choices.Any(c => c.Id.StartsWith("augmented:") || c.Id.StartsWith("diminished7:")));
    Check(e.Choices.Any(c => c.Id == "dominant7:3"));
});
Test("Root filter, descending first-note semantics, keyboard extremes", () => {
    var gen = new ExerciseGenerator(52);
    for (var i = 0; i < 100; i++) {
        var e = gen.Next(new(Level: Level.Professional, Direction: Direction.Descending, MinNote:21, MaxNote:108, RootPitchClass:9));
        Check(e.Notes[0] % 12 == 9); Check(e.Notes[1] <= e.Notes[0]);
    }
    foreach (var note in new[] {21,108}) {
        var e = gen.Next(new(Category.Notes, MinNote:note, MaxNote:note));
        Check(e.Notes.SequenceEqual(new[] {note})); Check(e.CorrectId == $"note:{note}");
    }
});
Test("Invalid and impossible settings rejected", () => {
    var gen = new ExerciseGenerator();
    foreach (var s in new[] {new Settings(MinNote:20), new Settings(MaxNote:109), new Settings(Tempo:0), new Settings(Repetitions:-1), new Settings(Velocity:128), new Settings(RootPitchClass:12), new Settings(Category:(Category)100), new Settings(Category:Category.Chords,MinNote:60,MaxNote:61), new Settings(Category:Category.Notes,MinNote:60,MaxNote:60,RootPitchClass:1)})
        Throws<ArgumentException>(() => gen.Next(s));
});
Test("Seeded generation is reproducible, not a fixed canned list", () => {
    var a = new ExerciseGenerator(42); var b = new ExerciseGenerator(42); var seen = new HashSet<string>();
    for (var i=0; i<1000; i++) { var ea=a.Next(new(Level:Level.Professional,MinNote:21,MaxNote:108)); var eb=b.Next(new(Level:Level.Professional,MinNote:21,MaxNote:108)); Check(ea.Notes.SequenceEqual(eb.Notes)); seen.Add(string.Join(',',ea.Notes)); }
    Check(seen.Count > 600);
});
Test("Session scores exactly once and rejects arbitrary answer IDs", () => {
    var session = new Session(); var e = new ExerciseGenerator(2).Next(new()); session.Begin(e);
    Throws<ArgumentException>(() => session.Answer("not-a-choice",Level.Beginner));
    Check(session.Answer(e.CorrectId,Level.Beginner).Correct);
    Throws<InvalidOperationException>(() => session.Answer(e.CorrectId,Level.Beginner));
    session.Begin(e); var wrong=e.Choices.First(c => c.Id!=e.CorrectId); Check(!session.Answer(wrong.Id,Level.Beginner).Correct);
    session.Clear(); Throws<InvalidOperationException>(() => session.Answer(e.CorrectId,Level.Beginner));
});
Test("Progress/preferences roundtrip, backup, corrupt history preserved", () => {
    var dir=Path.Combine(Path.GetTempPath(),"studio-tests-"+Guid.NewGuid());
    try {
        var store=new ProgressStore(dir); Check(store.Load().Count==0);
        var session=new Session(); var e=new ExerciseGenerator(3).Next(new()); session.Begin(e); var a=session.Answer(e.CorrectId,Level.Beginner);
        store.Save(new[] {a}); store.Save(new[] {a,a}); Check(store.Load().Count==2);
        Check(File.Exists(Path.Combine(dir,"history.json.bak")));
        var pref=new Preferences(new(Level:Level.Professional),"grand.sf2",0,1,"test-device"); store.SavePreferences(pref); Check(store.LoadPreferences()==pref);
        File.WriteAllText(Path.Combine(dir,"history.json"),"{broken"); Throws<InvalidDataException>(()=>store.Load());
        Check(File.ReadAllText(Path.Combine(dir,"history.json"))=="{broken");
    } finally { Directory.Delete(dir,true); }
});
Test("Audio timeline: exact tempo, ordering, sustain release, repeat counts", () => {
    var e = new Exercise("test", Category.Intervals, [60,67], "interval:7", "Καθαρή πέμπτη", [new("interval:7", "Καθαρή πέμπτη")], Direction.Ascending);
    var settings=new Settings(Tempo:60,Repetitions:3,Pedal:true,Velocity:42);
    var events=Schedule.Create(e,settings);
    Check(events.Count(e=>e.Action==MidiAction.NoteOn)==6);
    Check(events.Where(e=>e.Action==MidiAction.NoteOn).All(e=>e.Value==42));
    Check(events.Where(e=>e.Action==MidiAction.Pedal && e.Value==0).Count()==3);
    var starts=events.Where(e=>e.Action==MidiAction.NoteOn).ToArray(); Check(starts[1].Frame-starts[0].Frame==44100);
    var harmonic=Schedule.Create(e,settings with {Direction=Direction.Harmonic,Repetitions=1});
    Check(harmonic.Where(e=>e.Action==MidiAction.NoteOn).Select(e=>e.Frame).Distinct().Count()==1);
    Check(harmonic.Last().Action==MidiAction.Pedal && harmonic.Last().Value==0);
});
Test("Theory verifier rejects corrupted exercises and wrong inversions", () => {
    var gen = new ExerciseGenerator(77); var e = gen.Next(new());
    Throws<ArgumentException>(() => Theory.VerifyExercise(e with { Notes = [1,200] }));
    Throws<ArgumentException>(() => Theory.VerifyExercise(e with { CorrectId = "interval:25" }));
    var chord = gen.Next(new(Category.Inversions, Level.Professional));
    Throws<ArgumentException>(() => Theory.VerifyExercise(chord with { Notes = chord.Notes.Select((n,i) => n + (i==1 ? 1 : 0)).ToArray() }));
});
Test("Missing/invalid SoundFont fails explicitly, without synthetic fallback", () => {
    Throws<FileNotFoundException>(() => new PianoRenderer("/missing/piano.sf2"));
    var path=Path.GetTempFileName(); try { File.WriteAllText(path,"invalid SF2"); Throws<Exception>(()=>new PianoRenderer(path)); } finally { File.Delete(path); }
});
Test("Independent reference covers all ten chord qualities and their inversion formulas",()=>{
    var reference=new Dictionary<string,int[]>{
        ["major"]=[0,4,7],["minor"]=[0,3,7],["diminished"]=[0,3,6],["augmented"]=[0,4,8],
        ["dominant7"]=[0,4,7,10],["major7"]=[0,4,7,11],["minor7"]=[0,3,7,10],
        ["half-diminished7"]=[0,3,6,10],["diminished7"]=[0,3,6,9],["minor-major7"]=[0,3,7,11]};
    foreach(var (id,pitches) in reference){
        var actual=Theory.Chords.Single(c=>c.Id==id);Check(actual.Steps.SequenceEqual(pitches));
        for(var inversion=0;inversion<pitches.Length;inversion++){
            var expected=pitches.Skip(inversion).Concat(pitches.Take(inversion).Select(n=>n+12));
            Check(Theory.Invert(actual.Steps,inversion).SequenceEqual(expected));
        }
    }
});
Test("Major and natural minor pitch sets agree with independent tonal references",()=>{
    Check(Enumerable.Range(0,12).Where(n=>Theory.InKey(n,new(KeyTonic:0))).SequenceEqual(new[]{0,2,4,5,7,9,11}));
    Check(Enumerable.Range(0,12).Where(n=>Theory.InKey(n,new(KeyTonic:9,KeyMode:TonalityMode.NaturalMinor))).SequenceEqual(new[]{0,2,4,5,7,9,11}));
    Check(Enumerable.Range(0,12).Where(n=>Theory.InKey(n,new(KeyTonic:6))).SequenceEqual(new[]{1,3,5,6,8,10,11}));
});
Test("All 24 tonalities constrain actual notes, not only answer labels",()=>{
    var g=new ExerciseGenerator(18);
    foreach(var mode in Enum.GetValues<TonalityMode>())foreach(var tonic in Enumerable.Range(0,12))foreach(var category in Enum.GetValues<Category>().Where(c=>c<=Category.Notes))
    {
        var settings=new Settings(category,Level.Professional,MinNote:21,MaxNote:108,KeyTonic:tonic,KeyMode:mode);
        for(var i=0;i<12;i++){var e=g.Next(settings);Check(e.Notes.All(n=>Theory.InKey(n,settings)));Theory.VerifyExercise(e);}
    }
    Throws<ArgumentException>(()=>g.Next(new(KeyTonic:12)));
});
Test("Inversions retain valid voicings whose unplayed root is below the selected range",()=>{
    var g=new ExerciseGenerator(6); var found=false;
    for(var i=0;i<100;i++) {var e=g.Next(new(Category.Inversions,MinNote:60,MaxNote:68,RootPitchClass:8));Check(e.Notes.All(n=>n>=60&&n<=68));found|=e.CorrectId=="major:1"&&e.Notes.SequenceEqual(new[]{60,63,68});}
    Check(found);
});
Test("Continuous repeat wraps indefinitely; finite cursor and stop boundary are exact",()=>{
    var output=new float[10003];var cursor=new LoopingPcmCursor([1,2,3,4],true);
    Check(cursor.Read(output,0,output.Length)==output.Length);
    Check(output.Select((v,i)=>v==(i%4)+1).All(v=>v));
    var finite=new LoopingPcmCursor([1,2,3,4],false);Check(finite.Read(output,2,100)==4);Check(finite.Read(output,0,100)==0);
    var empty=new LoopingPcmCursor([],true);Check(empty.Read(output,0,10)==0);
    var e=new ExerciseGenerator(1).Next(new(Repetitions:0));Check(Schedule.Create(e,new(Repetitions:0)).Count(e=>e.Action==MidiAction.NoteOn)==2);
});
Test("BPM changes only event times and preserves pitches and velocities",()=>{
    var e=new ExerciseGenerator(30).Next(new(Level:Level.Professional));
    var slow=Schedule.Create(e,new(Tempo:60));var fast=Schedule.Create(e,new(Tempo:120));
    Check(slow.Select(v=>(v.Action,v.Key,v.Value)).SequenceEqual(fast.Select(v=>(v.Action,v.Key,v.Value))));
    Check(slow.Select(v=>v.Frame).SequenceEqual(fast.Select(v=>v.Frame*2)));
});
Test("Harmonic unison triggers one piano key, not two overlapping sample voices",()=>{
    var e=new ExerciseGenerator(1).Next(new(MinNote:60,MaxNote:60,Direction:Direction.Harmonic));
    Check(Schedule.Create(e,new(Direction:Direction.Harmonic)).Count(v=>v.Action==MidiAction.NoteOn)==1);
});
Test("Semantically corrupt history is preserved, not accepted as progress",()=>{
    var dir=Path.Combine(Path.GetTempPath(),"studio-null-"+Guid.NewGuid());Directory.CreateDirectory(dir);
    try{var file=Path.Combine(dir,"history.json");File.WriteAllText(file,"[null]");Throws<InvalidDataException>(()=>new ProgressStore(dir).Load());Check(File.ReadAllText(file)=="[null]");}
    finally{Directory.Delete(dir,true);}
});
Test("Unverified piano archives are rejected before extraction",()=>{
    var file=Path.GetTempFileName();try{File.WriteAllText(file,"not the official piano");Throws<InvalidDataException>(()=>MaestroLibrary.VerifyAndExtract(file,file+"-out",CancellationToken.None));Check(!Directory.Exists(file+"-out"));}finally{File.Delete(file);}
});
var maestroIndex=Array.IndexOf(args,"--maestro");
if(maestroIndex>=0&&maestroIndex+1<args.Length)
{
    Test("Maestro real sample coverage: all 88 keys, five attacks and four release layers",()=>{
        var m=new MaestroPiano(args[maestroIndex+1]);
        foreach(var key in Enumerable.Range(21,88)){
            var files=new HashSet<string>();
            foreach(var velocity in new[]{1,29,30,59,60,89,90,108,109,127}){var a=m.Select(key,velocity);Check(a.LowVelocity<=velocity&&a.HighVelocity>=velocity);files.Add(a.File);var r=m.Select(key,velocity,true);Check(r.Release);}
            Check(files.Count==5);
        }
    });
    Test("Maestro renders real stereo samples, release layers and pedal for every category",()=>{
        var m=new MaestroPiano(args[maestroIndex+1]);var g=new ExerciseGenerator(82);
        foreach(var category in Enum.GetValues<Category>().Where(c=>c<=Category.Notes)){
            var settings=new Settings(category,Level.Professional,Pedal:true);var pcm=m.Render(g.Next(settings),settings);
            Check(pcm.Any(v=>Math.Abs(v)>0.001f));Check(pcm.All(float.IsFinite));Check(pcm.Max(v=>Math.Abs(v))<=0.901f);Check(pcm.Length%2==0);
        }
    });
    Test("Maestro different velocities use different recordings; BPM preserves attack waveform pitch",()=>{
        var m=new MaestroPiano(args[maestroIndex+1]);var e=new ExerciseGenerator(1).Next(new(Category.Notes,MinNote:60,MaxNote:60));
        var slow=m.Render(e,new(Category.Notes,Tempo:60,Velocity:85));var fast=m.Render(e,new(Category.Notes,Tempo:120,Velocity:85));
        var soft=m.Render(e,new(Category.Notes,Tempo:60,Velocity:15));Check(!slow.SequenceEqual(soft));
        var a=(int)(44100*0.4);var b=(int)(44100*0.2);double dot=0,aa=0,bb=0;
        for(var i=0;i<8192;i++){var x=slow[2*a+i];var y=fast[2*b+i];dot+=x*y;aa+=x*x;bb+=y*y;}
        Check(aa>0&&bb>0&&dot/Math.Sqrt(aa*bb)>0.99999,"Tempo modified the attack waveform");
        using var cancel=new CancellationTokenSource();cancel.Cancel();Throws<OperationCanceledException>(()=>m.Render(e,new(Category.Notes),cancel.Token));
    });
}else Console.WriteLine("NOT RUN: Maestro integration (provide --maestro installed-manifest). Windows codec/UIA/NVDA/hardware remain untested here.");
var fontIndex=Array.IndexOf(args,"--soundfont");
if (fontIndex >=0 && fontIndex+1<args.Length) {
    Test("Real SF2 decoding and stereo PCM rendering at two velocities with sustain", () => {
        var renderer=new PianoRenderer(args[fontIndex+1]); Check(renderer.Presets.Length>0);
        var e=new ExerciseGenerator(7).Next(new(Category.Chords));
        var soft=renderer.Render(e,new(Category.Chords,Velocity:35,Pedal:true),renderer.Presets[0]);
        var loud=renderer.Render(e,new(Category.Chords,Velocity:110,Pedal:true),renderer.Presets[0]);
        Check(soft.Length==loud.Length && soft.Length>44100*4);
        Check(soft.All(float.IsFinite) && loud.All(float.IsFinite)); Check(loud.Max(v=>Math.Abs(v))<=0.901f);
        Check(loud.Any(v=>Math.Abs(v)>0.001f)); Check(!soft.SequenceEqual(loud)); Check(loud[^1]==0);
        using var cancel=new CancellationTokenSource(); cancel.Cancel();
        Throws<OperationCanceledException>(()=>renderer.Render(e,new(),renderer.Presets[0],cancel.Token));
    });
} else Console.WriteLine("NOT RUN: real SF2 render (provide --soundfont path). No Windows/NVDA/hardware tests run by this runner.");
Studio.Tests.FreeTuningTests.Register(Test);
Studio.Tests.IsonMigrationTests.Register(Test);
Studio.Tests.TrainingCatalogTests.Register(Test);
Studio.Tests.IntonationTests.Register(Test);
Studio.Tests.LiveIsonTests.Register(Test);
Studio.Tests.ResponseOptionTests.Register(Test);
Studio.Tests.IsonTests.Register(Test,args);
Studio.Tests.RhythmInputTests.Register(Test,args);
Studio.Tests.LevelCurriculumTests.Register(Test,args);
Studio.Tests.CadenceEvidenceTests.Register(Test,args);
Studio.Tests.NewStudyTests.Register(Test,args);
Studio.Tests.FlacIntegrityTests.Register(Test);
Studio.Tests.Version2Tests.Register(Test,args);
Studio.Tests.Version3Tests.Register(Test,args);
Studio.Tests.Version4Tests.Register(Test,args);
Studio.Tests.Version5Tests.Register(Test,args);
var failures=0;
var outcomes=new List<object>();
foreach (var (name,run) in tests) { try { run(); Console.WriteLine("PASS "+name);outcomes.Add(new{Name=name,Passed=true}); } catch(Exception e) { failures++; Console.Error.WriteLine("FAIL "+name+": "+e);outcomes.Add(new{Name=name,Passed=false,Error=e.ToString()}); } }
Console.WriteLine($"{tests.Count-failures} passed; {failures} failed; {tests.Count} executed.");
var reportIndex=Array.IndexOf(args,"--report");
if(reportIndex>=0&&reportIndex+1<args.Length)File.WriteAllText(args[reportIndex+1],JsonSerializer.Serialize(new{Version=typeof(Exercise).Assembly.GetName().Version!.ToString(3),At=DateTimeOffset.UtcNow,Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,OperatingSystem=System.Runtime.InteropServices.RuntimeInformation.OSDescription,SourceCommit=Environment.GetEnvironmentVariable("GITHUB_SHA"),Executed=tests.Count,Failed=failures,Outcomes=outcomes,WindowsRuntimeTested=OperatingSystem.IsWindows(),WindowsGuiTested=false,NvdaTested=false,ListeningTested=false},new JsonSerializerOptions{WriteIndented=true}));
return failures==0 ? 0 : 1;
