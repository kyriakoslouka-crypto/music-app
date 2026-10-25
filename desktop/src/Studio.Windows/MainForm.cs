using Studio.Core;
using Studio.Audio;
using System.Windows.Forms.Automation;
using System.Diagnostics;
using System.Globalization;
namespace Studio.Windows;

public sealed partial class MainForm : Form
{
    private readonly ExerciseGenerator generator = new();
    private readonly Session session = new();
    private readonly AudioOutput audio = new();
    private static readonly string DataDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProfessionalEarTrainingStudio");
    private static readonly string MaestroDirectory=Path.Combine(DataDirectory,"Piano","Maestro");
    private readonly bool auditOnly;
    private readonly bool uiTestMode=Environment.GetCommandLineArgs().Contains("--uia-test-mode");
    private readonly System.Windows.Forms.Timer preferenceSave=new(){Interval=600};
    private readonly ProgressStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProfessionalEarTrainingStudio"));
    private List<Attempt> history = [];
    private PianoRenderer? piano;
    private string? fontPath;
    private CancellationTokenSource? rendering;
    private readonly PlaybackCache playbackCache=new();
    private Settings activeSettings = new();
    private bool storageHealthy = true, loading, playing, answered, heard;
    private int playbackGeneration;
    private Action<string>? regressionError;
    private readonly AccessibleTabControl tabs = new() { Dock = DockStyle.Fill, Multiline = true, Name="mainSections", AccessibleName="Κύριες ενότητες εφαρμογής" };
    private readonly ComboBox category = Combo("Κατηγορία άσκησης", ["Διαστήματα", "Συγχορδίες", "Αναστροφές συγχορδιών", "Μεμονωμένες νότες", "Πτώσεις", "Κλίμακες", "Εκκλησιαστικοί τρόποι", "Ρυθμικά σχήματα", "Μέτρα", "Ρυθμική υπαγόρευση", "Ρυθμική αναπαραγωγή", "Μελωδικό ντικτέ", "Αρμονικό ντικτέ", "Βαθμίδες και λειτουργίες", "Τονικό σολφέζ", "Ατονικό σολφέζ", "Μελωδική μνήμη", "Δίφωνη πολυφωνία", "Τρίφωνη πολυφωνία", "Τετράφωνη πολυφωνία", "Εσωτερική μελωδική γραμμή", "Χρωματική αρμονία", "Δωδεκαφθογγικές σειρές", "Μετασχηματισμοί σειράς", "Τροπικά χαρακτηριστικά", "Προχωρημένη αρμονική ανάλυση", "Προχωρημένα σύνθετα διαστήματα", "Εκτεταμένες συγχορδίες"]);
    private readonly ComboBox level = Combo("Επίπεδο", ["Beginner — Αρχάριο", "Elementary — Στοιχειώδες", "Intermediate — Μέσο", "Advanced — Προχωρημένο", "Expert — Ειδικός", "Professional / Pro"]);
    private readonly ComboBox direction = Combo("Τρόπος ακρόασης", ["Ανιόν", "Κατιόν", "Αρμονικό"]);
    private readonly ComboBox root = Combo("Αρχική νότα ή θεμέλιος", new[] { "Όλες" }.Concat(Theory.PitchNames).ToArray());
    private readonly ComboBox tonality=Combo("Τονικότητα ασκήσεων",new[]{"Χωρίς τονικό περιορισμό"}.Concat(Theory.PitchNames.Select(n=>n+" μείζονα")).Concat(Theory.PitchNames.Select(n=>n+" φυσική ελάσσονα")).ToArray());
    private readonly ComboBox meter=Combo("Μέτρο ρυθμικής υπαγόρευσης και αναπαραγωγής",TheoryV2.Meters.Select(m=>m.Name).ToArray());
    private readonly NumericUpDown bars=Number("Αριθμός μέτρων ρυθμού",1,8,2);
    private readonly TextBox response=new(){Multiline=true,Height=75,Dock=DockStyle.Fill,AccessibleName="Γραπτή απάντηση χωρίς παρτιτούρα",Visible=false};
    private readonly TextBox prompt=new(){Multiline=true,ReadOnly=true,Height=95,Dock=DockStyle.Fill,AccessibleName="Οδηγία τρέχουσας άσκησης",Text="Επίλεξε άσκηση και πάτησε Ctrl+N."};
    private readonly Button tap=new(){Text="Κτύπημα (F8)",AccessibleName="Κτύπημα ρυθμού με F8",AutoSize=true,Visible=false};
    private readonly Button resetTaps=new(){Text="Καθαρισμός κτυπημάτων",AutoSize=true,Visible=false};
    private readonly ComboBox soloVoice=Combo("Απομόνωση πολυφωνικής φωνής",["Όλες οι φωνές","Φωνή 1 — ψηλότερη","Φωνή 2","Φωνή 3","Φωνή 4"]);
    private readonly NumericUpDown segmentStart=Number("Αρχή τμήματος ακρόασης σε χρόνους",0,256,0);
    private readonly NumericUpDown segmentEnd=Number("Τέλος τμήματος σε χρόνους, 0 για ολόκληρο",0,256,0);
    private readonly Button previewAnswer=new(){Text="Ακρόαση γραπτής απάντησης (Ctrl+Shift+R)",AutoSize=true,Visible=false};
    private bool adjustingSegments;
    private readonly Stopwatch tapClock=new();
    private readonly List<double> taps=[];
    private readonly ComboBox devices = Combo("Έξοδος ήχου", []);
    private readonly ComboBox presets = Combo("Όργανο SF2", []);
    private readonly NumericUpDown minimum = Number("Κατώτερη νότα MIDI, Λα 0 = 21", 21, 108, 48);
    private readonly NumericUpDown maximum = Number("Ανώτερη νότα MIDI, Ντο 8 = 108", 21, 108, 84);
    private readonly NumericUpDown tempo = Number("Ταχύτητα BPM", 30, 240, 80);
    private readonly NumericUpDown repeats = Number("Επαναλήψεις: 0 για απεριόριστη συνεχή ακρόαση", 0, 5, 1);
    private readonly NumericUpDown velocity = Number("Δυναμική MIDI velocity", 1, 127, 85);
    private readonly CheckBox pedal = new() { Text = "Πεντάλ sustain (CC64)", AutoSize = true, AccessibleName = "Πεντάλ sustain" };
    private readonly CheckBox cancelSpeech = new() { Text = "Διακοπή ομιλίας NVDA πριν τον ήχο (προαιρετικό DLL)", AutoSize = true };
    private readonly ListBox answers = new() { Dock = DockStyle.Fill, AccessibleName = "Απαντήσεις άσκησης", IntegralHeight = false };
    private readonly TextBox status = new() { ReadOnly = true, Multiline = true, Height = 85, Dock = DockStyle.Fill, AccessibleName = "Κατάσταση και αποτέλεσμα", Text = "Εγκατάστησε το Maestro Concert Grand με το πρώτο κουμπί ή φόρτωσε δικό σου SF2. Δεν χρησιμοποιείται συνθετικό υποκατάστατο." };
    private readonly TextBox stats = new() { ReadOnly = true, Multiline = true, Dock = DockStyle.Fill, AccessibleName = "Στατιστικά και ιστορικό", ScrollBars = ScrollBars.Vertical };
    private readonly Button submit = new() { Text = "&Υποβολή (Ctrl+Enter)", AutoSize = true, Enabled = false };
    private readonly Button next = new() { Text = "&Νέα άσκηση (Ctrl+N)", AutoSize = true };
    private readonly Button replay = new() { Text = "&Επανάληψη (Ctrl+R)", AutoSize = true, Enabled = false };
    private readonly Button pause = new() { Text = "&Παύση / συνέχεια (Ctrl+P)", AutoSize = true, Enabled = false };
    private readonly Button setup=new(){Text="&Εγκατάσταση / επισκευή Maestro Concert Grand…",AutoSize=true,AccessibleName="Εγκατάσταση ή επισκευή πραγματικού πιάνου Maestro Concert Grand"};
    private readonly Button load = new() { Text = "&Φόρτωση πιάνου SF2…", AutoSize = true };

    public MainForm(bool auditOnly=false)
    {
        this.auditOnly=auditOnly; meter.SelectedIndex=2;
        segmentStart.DecimalPlaces=segmentEnd.DecimalPlaces=2;segmentStart.Increment=segmentEnd.Increment=0.25m;
        Text = "Professional Ear Training Studio — VERSION "+typeof(MainForm).Assembly.GetName().Version!.ToString(3);
        AccessibleName = Text; MinimumSize = new(720, 560); Size = new(1000, 800);
        Font = new("Segoe UI", 11); StartPosition = FormStartPosition.CenterScreen;
        BuildSettings(); BuildPractice(); BuildHistory(); BuildExams(); BuildHelp(); Controls.Add(tabs);
        next.Click += async (_, _) => await NewExercise();
        replay.Click += async (_, _) => await Replay();
        pause.Click += (_, _) => TogglePause();
        submit.Click += (_, _) => Submit();
        load.Click += async (_, _) => await ChooseFont();
        setup.Click+=async(_,_)=>await ShowPianoSetup();
        tap.Click+=(_,_)=>RecordTap();resetTaps.Click+=(_,_)=>ClearTaps();previewAnswer.Click+=async(_,_)=>await PreviewAnswer();
        foreach (var control in new Control[] { category, level, direction, root, tonality, meter, bars, minimum, maximum, presets, devices })
        {
            if (control is ComboBox cb) cb.SelectedIndexChanged += (_, _) => InvalidateExercise();
            else if (control is NumericUpDown number) number.ValueChanged += (_, _) => InvalidateExercise();
            else if (control is CheckBox check) check.CheckedChanged += (_, _) => InvalidateExercise();
        }
        foreach(var number in new[]{tempo,repeats,velocity,segmentStart,segmentEnd})number.ValueChanged+=(_,_)=>ChangePlaybackSettings();
        pedal.CheckedChanged+=(_,_)=>ChangePlaybackSettings();soloVoice.SelectedIndexChanged+=(_,_)=>ChangePlaybackSettings();
        cancelSpeech.CheckedChanged+=(_,_)=>SchedulePreferenceSave();
        automaticPractice.CheckedChanged+=(_,_)=>{CancelAutomaticPractice();SchedulePreferenceSave();};
        automaticDelay.ValueChanged+=(_,_)=>{CancelAutomaticPractice();SchedulePreferenceSave();};
        tabs.SelectedIndexChanged+=(_,_)=>{if(tabs.SelectedIndex!=1)CancelAutomaticPractice();};
        preferenceSave.Tick+=(_,_)=>{preferenceSave.Stop();SavePreferences();};
        AccessibilityContract.Apply(this);
        Shown += async (_, _) => {
            if(auditOnly){
                try{var args=Environment.GetCommandLineArgs();AccessibilityContract.WriteAudit(args[Array.IndexOf(args,"--accessibility-audit")+1],this);}
                catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}
                Close();return;
            }
            await Initialize();
            if(uiTestMode){
                var args=Environment.GetCommandLineArgs();var marker=Array.IndexOf(args,"--uia-state-path");
                if(marker>=0&&marker+1<args.Length)File.WriteAllText(args[marker+1],System.Text.Json.JsonSerializer.Serialize(AccessibilityContract.TabSnapshot(tabs),new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            }
        };
        FormClosing += (_, _) => { examTimer.Stop();examTimer.Dispose();if(exam is not null)SaveExam();preferenceSave.Stop(); if(!auditOnly&&!uiTestMode){SavePreferences();if(storageHealthy)try{store.Save(history);}catch(Exception ex){Error("Η τελευταία αποθήκευση ιστορικού απέτυχε: "+ex.Message);}} Stop(); audio.Dispose(); preferenceSave.Dispose(); };
    }
    private static ComboBox Combo(string name, string[] values)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = name, Width = 430 };
        combo.Items.AddRange(values); if (values.Length > 0) combo.SelectedIndex = 0; return combo;
    }
    private static NumericUpDown Number(string name, int min, int max, int value) => new() { Minimum = min, Maximum = max, Value = value, AccessibleName = name, AccessibleDescription = $"Ελάχιστο {min}, μέγιστο {max}. Χρησιμοποίησε τα βέλη ή πληκτρολόγησε τιμή.", Width = 160 };
    private static TableLayoutPanel SettingsLayout() => new() { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 2, Padding = new(16), AutoSize = false };
    private static void Row(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++; control.TabIndex=row*2+1; table.RowStyles.Add(new(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new(6, 12, 12, 6) }, 0, row);
        control.Margin = new(6, 8, 6, 8); table.Controls.Add(control, 1, row);
    }
    private void BuildSettings()
    {
        var page = new AccessibleTabPage("Ρυθμίσεις"); var table = SettingsLayout();
        Row(table,"Προτεινόμενο concert grand",setup); Row(table, "Δική σου βιβλιοθήκη πιάνου", load); Row(table, "Όργανο", presets);
        Row(table, "Έξοδος", devices);
        var refresh = new Button { Text = "Ανανέωση συσκευών", AutoSize = true }; refresh.Click += (_, _) => RefreshDevices(); Row(table, "Συσκευές", refresh);
        Row(table, "Κατηγορία", category); Row(table, "Επίπεδο", level); Row(table, "Ακρόαση διαστημάτων", direction);
        Row(table, "Αρχική νότα / θεμέλιος", root); Row(table,"Τονικότητα (φθόγγοι κλίμακας)",tonality);
        Row(table, "Χαμηλότερη νότα MIDI (21–108)", minimum); Row(table, "Υψηλότερη νότα MIDI (21–108)", maximum);
        Row(table,"Μέτρο υπαγόρευσης / κτυπημάτων",meter);Row(table,"Πλήθος μέτρων ρυθμού",bars);
        Row(table, "Ταχύτητα BPM", tempo); Row(table, "Επαναλήψεις (0 = απεριόριστα)", repeats); Row(table, "Δυναμική (1–127)", velocity); Row(table, "Πεντάλ", pedal);
        automaticPractice.AccessibleDescription="Προαιρετική αυτόματη συνέχεια μόνο στην εξάσκηση. Ρύθμισε την αναμονή ώστε να προλαβαίνεις το NVDA. Esc ακυρώνει.";
        Row(table, "Ομιλία", cancelSpeech);Row(table,"Συνεχής εξάσκηση",automaticPractice);Row(table,"Αναμονή ανατροφοδότησης και οδηγίας (δευτερόλεπτα)",automaticDelay);Row(table,"Απομόνωση πολυφωνικής φωνής",soloVoice);Row(table,"Αρχή τμήματος (χρόνοι)",segmentStart);Row(table,"Τέλος τμήματος (0 = όλο)",segmentEnd);
        var start = new Button { Text = "Έναρξη εξάσκησης (Ctrl+N)", AutoSize = true }; start.Click += async (_, _) => await NewExercise(); Row(table, "Εξάσκηση", start);
        page.Controls.Add(table); tabs.TabPages.Add(page);
    }
    private void BuildPractice()
    {
        var page=new AccessibleTabPage("Εξάσκηση");
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new(16)};
        layout.RowStyles.Add(new(SizeType.Absolute,110));layout.RowStyles.Add(new(SizeType.Percent,100));
        layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.Absolute,100));
        layout.Controls.Add(prompt);
        var input=new Panel{Dock=DockStyle.Fill};input.Controls.Add(answers);input.Controls.Add(response);layout.Controls.Add(input);
        var tapsPanel=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};tapsPanel.Controls.AddRange([tap,resetTaps]);layout.Controls.Add(tapsPanel);
        var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};var diagnostics=new Button{Text="Στοιχεία ήχου (F6)",AccessibleName="Ανάγνωση τελευταίου σφάλματος ήχου με F6",AutoSize=true};diagnostics.Click+=(_,_)=>ReadAudioFailure();
        buttons.Controls.AddRange([next,replay,pause,previewAnswer,submit,diagnostics]);layout.Controls.Add(buttons);layout.Controls.Add(status);
        page.Controls.Add(layout);tabs.TabPages.Add(page);
    }
    private void BuildHistory()
    {
        var page = new AccessibleTabPage("Πρόοδος"); page.Controls.Add(stats); tabs.TabPages.Add(page);
    }
    private void BuildHelp()
    {
        var help = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, AccessibleName = "Οδηγίες και περιορισμοί έκδοσης", Text =
            "VERSION "+typeof(MainForm).Assembly.GetName().Version!.ToString(3)+" — Ακουστική εκπαίδευση\r\n\r\nΣτην πρώτη εκκίνηση εμφανίζεται οδηγός εγκατάστασης Maestro Concert Grand (Yamaha CF-3, 5 δυναμικές). Alt+Ε κατεβάζει την επιλεγμένη βιβλιοθήκη από την πηγή της και την εγκαθιστά τοπικά. Απαιτούνται περίπου 263 MB λήψης και 3 GB ελεύθερα. Μετά λειτουργεί offline. Η βιβλιοθήκη έχει περιοριστική άδεια και δεν αναδιανέμεται από την εφαρμογή.\r\n\r\nCtrl+N: νέα άσκηση. Ctrl+R: απεριόριστες επαναλήψεις του ίδιου παραδείγματος. Επαναλήψεις 0: συνεχής βρόχος μέχρι Esc ή υποβολή. Ctrl+P: παύση/συνέχεια. Ctrl+Enter: υποβολή. Esc: διακοπή. Ctrl+Home: ρυθμίσεις. Ctrl+Shift+S: διακοπή και ανάγνωση κατάστασης. Ctrl+Tab: καρτέλες. F1: οδηγίες. F6: πλήρης αναφορά τελευταίου σφάλματος ήχου, χωρίς αντιγραφή από παράθυρο. Tab/Shift+Tab, βέλη, Space και Enter: φυσικά controls Windows.\r\n\r\nΑλλαγή BPM, δυναμικής, πεντάλ ή επαναλήψεων διατηρεί την τρέχουσα άσκηση. Πάτησε Ctrl+R για νέο rendering, χωρίς αλλαγή τονικού ύψους. Αλλαγή τονικότητας, έκτασης ή τύπου άσκησης απαιτεί Ctrl+N. Στα βασικά διαστήματα και τις νότες η τονικότητα περιορίζει τους φθόγγους. Στις κλίμακες και τους τρόπους ορίζει το κέντρο. Στις πτώσεις ορίζει μείζονα ή ελάσσονα. Οι νέες ακολουθίες ανακοινώνουν πρώτα την οδηγία και αρχίζουν με Ctrl+R. Ctrl+I διαβάζει την οδηγία ξανά.\r\n\r\nΗ εφαρμογή δεν αποστέλλει ανακοινώσεις όσο παίζει μουσική. Οι απαντήσεις ανακοινώνονται μέσω UI Automation αφού σταματήσει ο ήχος. Η ίδια η πλοήγηση μπορεί να προκαλεί ομιλία NVDA. Προαιρετικό επίσημο nvdaControllerClient64.dll επιτρέπει ακύρωση τρέχουσας ομιλίας πριν την αναπαραγωγή.\r\n\r\nΔιαστήματα 0–24 ημιτονίων, δέκα τύποι συγχορδιών, ακουστικά διακριτές αναστροφές και νότες 21–108. Συμμετρικές συγχορδίες αποκλείονται από τις αναστροφές. Η αναγνώριση μεμονωμένων νοτών είναι άσκηση απόλυτου ύψους. Η τονική ορθογραφία εναρμονίων δεν εξετάζεται.\r\n\r\nΗ πρόοδος και οι έγκυρες ρυθμίσεις αποθηκεύονται αυτόματα στο LocalAppData\\ProfessionalEarTrainingStudio. Δεν υπάρχει τηλεμετρία ή επίσημη σχέση ABRSM. Η πραγματική συμβατότητα NVDA, WASAPI/ASIO και η ακουστική ποιότητα χρειάζονται δοκιμές σε Windows 11. Νέες ασκήσεις: πτώσεις με τονικό πλαίσιο, κλίμακες σε άνοδο/κάθοδο, τρόποι, ρυθμικά σχήματα και μέτρα. Στη ρυθμική υπαγόρευση γράφεις διάρκειες όπως 1/4 1/8 1/8 1/2, r για παύση και | ανά μέτρο. Στα κτυπήματα χρησιμοποιείς F8 σε κάθε έναρξη και μια επιπλέον φορά στο τελικό όριο. Δεν βαθμολογείται μικρόφωνο ή φωνή. Μελωδικό ντικτέ: Ντο4:1/4 ή 60:1/4. Αρμονικό: I ii6 V7 I. Σολφέζ/μνήμη: Ντο4 Ρε4 ή 60 62, σταθερό ντο. Ctrl+Shift+R παίζει τη γραπτή απάντηση. Η φωνή δεν καταγράφεται ή βαθμολογείται. Η αρχή και το τέλος τμήματος επιλέγονται στις Ρυθμίσεις. Δες και τις οδηγίες docs στον φάκελο εγκατάστασης." };
        var page = new AccessibleTabPage("Οδηγίες"); page.Controls.Add(help); tabs.TabPages.Add(page);
    }
    private Settings ReadSettings() => new((Category)category.SelectedIndex, (Level)level.SelectedIndex, (Direction)direction.SelectedIndex,
        (int)minimum.Value, (int)maximum.Value, root.SelectedIndex == 0 ? null : root.SelectedIndex - 1, (int)tempo.Value, (int)repeats.Value, (int)velocity.Value, pedal.Checked, tonality.SelectedIndex==0?null:(tonality.SelectedIndex-1)%12,tonality.SelectedIndex>12?TonalityMode.NaturalMinor:TonalityMode.Major,TheoryV2.Meters[meter.SelectedIndex].Id,(int)bars.Value,(double)segmentStart.Value,segmentEnd.Value==0?null:(double)segmentEnd.Value,soloVoice.SelectedIndex-1);
    private async Task Initialize()
    {
        loading=true;
        try
        {
            try {history=store.Load();}catch(Exception ex){storageHealthy=false;Error("Το ιστορικό δεν αντικαταστάθηκε: "+ex.Message);}
            Preferences? pref=null;
            try {pref=store.LoadPreferences();if(pref is not null)Theory.Validate(pref.Settings);}
            catch(Exception ex){pref=null;storageHealthy=false;Error("Οι ρυθμίσεις δεν αντικαταστάθηκαν: "+ex.Message);}
            if(pref is not null)
            {
                var t=pref.Settings;cancelSpeech.Checked=pref.CancelNvdaSpeech;automaticPractice.Checked=pref.AutomaticPractice;automaticDelay.Value=Math.Clamp(pref.AutomaticDelaySeconds,3,120);
                category.SelectedIndex=(int)t.Category;level.SelectedIndex=(int)t.Level;direction.SelectedIndex=(int)t.Direction;
                minimum.Value=t.MinNote;maximum.Value=t.MaxNote;root.SelectedIndex=t.RootPitchClass+1??0;
                tonality.SelectedIndex=t.KeyTonic.HasValue?t.KeyTonic.Value+1+(t.KeyMode==TonalityMode.NaturalMinor?12:0):0;
                meter.SelectedIndex=Array.FindIndex(TheoryV2.Meters,m=>m.Id==t.MeterId);bars.Value=t.Bars;segmentStart.Value=(decimal)t.SegmentStart;segmentEnd.Value=(decimal)(t.SegmentEnd??0);soloVoice.SelectedIndex=Math.Min(4,t.SoloVoice+1);
                tempo.Value=t.Tempo;repeats.Value=t.Repetitions;velocity.Value=t.Velocity;pedal.Checked=t.Pedal;
            }
            if(!uiTestMode)
            {
                RefreshDevices(pref?.DeviceId);
                var defaultPiano=Path.Combine(MaestroDirectory,MaestroLibrary.ManifestName);
                var path=pref?.SoundFontPath;
                if(path is null||!File.Exists(path))path=File.Exists(defaultPiano)?defaultPiano:null;
                if(path is not null)try{await LoadFont(path,pref?.Bank??0,pref?.Preset??0);}catch(Exception ex){Error("Το πιάνο χρειάζεται επισκευή ή νέα επιλογή: "+ex.Message);}
            }
        }
        finally {loading=false;UpdateHistory();RefreshExamHistory();}
        if(piano is null&&!uiTestMode)await ShowPianoSetup();
    }
    private async Task ShowPianoSetup()
    {
        Stop(); using var dialog=new PianoSetupDialog(MaestroDirectory);
        dialog.ShowDialog(this);
        if(dialog.InstalledManifest is not null)
        {
            try{await LoadFont(dialog.InstalledManifest);SavePreferences();Tell("Το Maestro Concert Grand είναι έτοιμο. Ctrl+N για την πρώτη άσκηση.");}
            catch(Exception ex){Error(ex.Message);}
        }
    }
    private void RefreshDevices(string? preferred = null)
    {
        Stop();
        try
        {
            devices.Items.Clear(); devices.Items.AddRange(AudioOutput.Devices().Cast<object>().ToArray());
            if(devices.Items.Count>0)devices.SelectedItem=devices.Items.Cast<OutputDevice>().FirstOrDefault(d=>d.IsDefault)??devices.Items[0];
            if (preferred is not null) foreach (OutputDevice d in devices.Items) if (d.Id == preferred) devices.SelectedItem = d;
        }
        catch (Exception ex) { Error("Αδυναμία αναζήτησης συσκευών ήχου. " + ex.Message); }
    }
    private async Task ChooseFont()
    {
        Stop(); using var dialog = new OpenFileDialog { Filter = "SoundFont 2 (*.sf2)|*.sf2|Εγκατεστημένο πιάνο Studio (*.studio-piano.json)|*.studio-piano.json", Title = "Επίλεξε νόμιμη βιβλιοθήκη ακουστικού πιάνου SF2" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { await LoadFont(dialog.FileName); SavePreferences(); Tell("Το πιάνο φορτώθηκε. Επίλεξε όργανο και πάτησε Ctrl+N."); }
        catch (Exception ex) { Error("Η βιβλιοθήκη δεν φορτώθηκε. " + ex.Message); }
    }
    private async Task LoadFont(string path, int bank = 0, int program = 0)
    {
        load.Enabled = false;
        try
        {
            var loaded = await Task.Run(() => new PianoRenderer(path));
            if (IsDisposed) return;
            InvalidateExercise(); piano = loaded; fontPath = path;
            presets.Items.Clear(); presets.Items.AddRange(loaded.Presets.Cast<object>().ToArray());
            presets.SelectedItem = loaded.Presets.FirstOrDefault(p => p.Bank == bank && p.Program == program) ?? loaded.Presets[0];
        }
        finally { if (!IsDisposed) load.Enabled = true; }
    }
    private async Task NewExercise()
    {
        if (rendering is not null) return;
        if(exam is not null){await AdvanceExam();return;}
        Stop();
        try
        {
            if (piano is null) throw new InvalidOperationException("Εγκατάστησε το Maestro Concert Grand από το πρώτο κουμπί των Ρυθμίσεων ή φόρτωσε δικό σου SF2.");
            if (devices.SelectedItem is not OutputDevice) throw new InvalidOperationException("Επίλεξε ενεργή έξοδο ήχου.");
            adjustingSegments=true;segmentStart.Value=0;segmentEnd.Value=0;soloVoice.SelectedIndex=0;adjustingSegments=false;
            activeSettings=ReadSettings();await PresentExercise(generator.Next(activeSettings),activeSettings);
            if(session.Current is { } created)_=QueueAutomaticPractice(created,false,Replay);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Error(ex.Message); }
    }
    private Task PresentExercise(Exercise exercise,Settings settings)
    {
        Stop();playbackCache.Clear();activeSettings=settings;
        adjustingSegments=true;segmentStart.Value=(decimal)settings.SegmentStart;segmentEnd.Value=(decimal)(settings.SegmentEnd??0);soloVoice.SelectedIndex=Math.Min(4,settings.SoloVoice+1);adjustingSegments=false;
        session.Begin(exercise); answered = false; heard = false;
            answers.Items.Clear(); answers.Items.AddRange(exercise.Choices.Cast<object>().ToArray()); answers.SelectedIndex = -1;
            response.Clear();taps.Clear();tapClock.Reset();
            answers.Visible=exercise.AnswerKind==AnswerKind.Choice;response.Visible=exercise.AnswerKind==AnswerKind.Text;
            tap.Visible=resetTaps.Visible=exercise.AnswerKind==AnswerKind.Taps;previewAnswer.Visible=exercise.AnswerKind==AnswerKind.Text;
            segmentStart.Enabled=segmentEnd.Enabled=exercise.Score is not null;
            soloVoice.Enabled=exercise.Category is Category.Polyphony2 or Category.Polyphony3 or Category.Polyphony4 or Category.InnerVoice;
            prompt.Text=CategoryName(exercise.Category)+$". Επίπεδο {settings.Level}. "+(exercise.Score is not null?exercise.Score.Prompt+$" Συνολική διάρκεια με πλαίσιο: {exercise.Score.LengthBeats:F2} χρόνοι.":"Άκουσε, επίλεξε απάντηση με τα βέλη και πάτησε Ctrl+Enter. Ctrl+R για επανάληψη.");
            tabs.SelectedIndex = 1; status.Text = "Νέα άσκηση. Προετοιμασία ήχου.";
            submit.Enabled = false; SavePreferences();
            replay.Enabled=true;Tell("Νέα άσκηση. "+prompt.Text+(automaticPractice.Checked&&exam is null?$" Αυτόματη ακρόαση σε {automaticDelay.Value} δευτερόλεπτα. Esc ακυρώνει. Ctrl+R για άμεση ακρόαση.":" Πάτησε Ctrl+R για την πρώτη ακρόαση.")+" Ctrl+I για ανάγνωση οδηγίας.");
        if(exam?.Running==true){previewAnswer.Enabled=false;segmentStart.Enabled=segmentEnd.Enabled=soloVoice.Enabled=false;}
        return Task.CompletedTask;
    }
    private async Task RenderAndPlay()
    {
        if (session.Current is null || piano is null || presets.SelectedItem is not PianoPreset preset) return;
        using var cancellation = new CancellationTokenSource(); rendering = cancellation;
        var current = session.Current;
        try
        {
            var renderer = piano; var snapshot=activeSettings;
            var rendered = await Task.Run(() => renderer.Render(current, snapshot, preset, cancellation.Token));
            cancellation.Token.ThrowIfCancellationRequested();
            if (IsDisposed || session.Current != current || activeSettings!=snapshot) return;
            playbackCache.Store(current.Id,snapshot,rendered);
            // Keep focus stable during music; announce the response control only after playback ends.
            await Task.Delay(150,cancellation.Token);
            StartPlayback();
        }
        finally { if (rendering == cancellation) rendering = null; }
    }
    private void StartPlayback(float[]? previewClip=null,bool preview=false)
    {
        var clip=previewClip??playbackCache.Get(session.Current?.Id,activeSettings);
        if(clip is null)return;
        if(devices.SelectedItem is not OutputDevice device)throw new InvalidOperationException("Επίλεξε ενεργή έξοδο ήχου στις Ρυθμίσεις.");
        if(exam is not null&&!preview)exam.CheckPlayAllowed();
        if (cancelSpeech.Checked) NvdaSpeech.Cancel();
        var generation = ++playbackGeneration;
        if(!status.Focused)status.Text = "Αναπαραγωγή. Ctrl+P: παύση. Esc: διακοπή.";
        playing = true; replay.Enabled = true; pause.Enabled = true; submit.Enabled = !answered&&(!preview||heard);
        audio.Play(clip, device, error =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            try{BeginInvoke((Action)(() => {
                if (generation != playbackGeneration || IsDisposed) return;
                playing = false; pause.Enabled = false;
                if (error is not null) { if(!preview&&exam?.Running==true){exam.RefundFailedPlay();SaveExam();UpdateExamStatus();} heard = false; submit.Enabled = false; ShowAudioFailure(error); }
                else{status.Text=preview?"Η γραπτή απάντηση ακούστηκε χωρίς βαθμολόγηση.":"Η ακρόαση ολοκληρώθηκε. Δώσε απάντηση και πάτησε Ctrl+Enter.";if(!preview&&tabs.SelectedIndex==1)FocusResponse();}
            }));}catch(InvalidOperationException)when(IsDisposed||Disposing){}
        }, activeSettings.Repetitions==0);
        if(!preview){heard=true;if(exam?.Running==true){exam.RegisterSuccessfulPlay();SaveExam();UpdateExamStatus();}}
    }
    private async Task Replay()
    {
        if (rendering is not null || session.Current is null) return;
        try { if(exam is not null)exam.CheckPlayAllowed();Stop(); if (playbackCache.Get(session.Current.Id,activeSettings) is not null) StartPlayback(); else await RenderAndPlay(); }
        catch (OperationCanceledException) { }
        catch (AudioPlaybackException ex) { ShowAudioFailure(ex); }
        catch (Exception ex) { Error(ex.Message); }
    }
    private async Task PreviewAnswer()
    {
        if(exam is not null){Error("Η ακρόαση γραπτής απάντησης δεν διατίθεται μέχρι να επιστρέψεις σε εξάσκηση.");return;}
        if(rendering is not null||session.Current is null||session.Current.AnswerKind!=AnswerKind.Text||piano is null||presets.SelectedItem is not PianoPreset preset)return;
        Stop();using var cancellation=new CancellationTokenSource();rendering=cancellation;
        try
        {
            var original=session.Current;var score=TheoryV3.Preview(original,response.Text,activeSettings);var renderer=piano;var settings=activeSettings;
            var clip=await Task.Run(()=>renderer.RenderPreview(score,settings,preset,cancellation.Token));cancellation.Token.ThrowIfCancellationRequested();
            if(!IsDisposed&&session.Current==original)StartPlayback(clip,true);
        }
        catch(OperationCanceledException){}
        catch(AudioPlaybackException ex){ShowAudioFailure(ex);}
        catch(Exception ex){Error(ex.Message);}
        finally{if(rendering==cancellation)rendering=null;}
    }
    private void TogglePause()
    {
        try
        {
            if(!audio.IsPlaying&&!audio.IsPaused&&automaticPending is not null){CancelAutomaticPractice();Tell("Η αυτόματη αναμονή ακυρώθηκε. Ctrl+R για ακρόαση ή Ctrl+N για νέα άσκηση.");return;}
            if(!audio.IsPlaying&&!audio.IsPaused){Tell("Δεν υπάρχει ενεργή αναπαραγωγή. Ctrl+R για ακρόαση.");return;}
            if(audio.IsPaused&&cancelSpeech.Checked)NvdaSpeech.Cancel();
            audio.PauseOrResume();playing=audio.IsPlaying;
            if(!playing)Tell("Παύση. Ctrl+P για συνέχεια.");else if(!status.Focused)status.Text="Συνέχεια αναπαραγωγής.";
        }
        catch(Exception ex){Error("Αποτυχία παύσης ή συνέχειας: "+ex.Message);}
    }
    internal object[] AuditPresentations()
    {
        var records=new List<object>();var g=new ExerciseGenerator(157);
        foreach(var c in Enum.GetValues<Category>())
        {
            var settings=new Settings(c,Level.Intermediate,MinNote:36,MaxNote:96);var e=g.Next(settings);
            playbackCache.Store("stale",settings,[1,2]);PresentExercise(e,settings).GetAwaiter().GetResult();
            if(heard||answered||submit.Enabled||answers.SelectedIndex!=-1||response.Text.Length!=0||playbackCache.Get("stale",settings)is not null||session.Current?.Id!=e.Id)throw new InvalidOperationException("Λανθασμένη παρουσίαση νέας ερώτησης: "+c);
            if(answers.Visible!=(e.AnswerKind==AnswerKind.Choice)||response.Visible!=(e.AnswerKind==AnswerKind.Text)||tap.Visible!=(e.AnswerKind==AnswerKind.Taps))throw new InvalidOperationException("Λανθασμένος τύπος απάντησης: "+c);
            records.Add(new{Category=CategoryName(c),AnswerKind=e.AnswerKind.ToString(),DeferredPlayback=true,CacheInvalidated=true,NoPreselectedAnswer=true});
        }
        return records.ToArray();
    }
    private void FocusResponse()
    {
        if(session.Current is null||answered)return;
        if(session.Current.AnswerKind==AnswerKind.Choice)answers.Focus();else if(session.Current.AnswerKind==AnswerKind.Text)response.Focus();else tap.Focus();
    }
    private void RecordTap()
    {
        if(session.Current?.AnswerKind!=AnswerKind.Taps||!heard||answered||rendering is not null)return;
        Stop();if(!tapClock.IsRunning)tapClock.Start();
        taps.Add(tapClock.Elapsed.TotalSeconds);status.Text=$"Κτυπήματα: {taps.Count}. Ctrl+Enter για βαθμολόγηση, αφού δώσεις και το τελικό όριο.";
    }
    private void ClearTaps(){taps.Clear();tapClock.Reset();status.Text="Τα κτυπήματα καθαρίστηκαν. F8 για πρώτη έναρξη.";}
    private void Submit()
    {
        if(rendering is not null||answered||!heard||session.Current is null)return;
        Stop();
        try
        {
            Attempt attempt;
            if(session.Current.AnswerKind==AnswerKind.Choice)
            {
                if(answers.SelectedItem is not Option choice){Tell("Επίλεξε απάντηση με τα βέλη.");return;}
                attempt=exam is not null?exam.Submit(choice.Id):session.Answer(choice.Id,activeSettings.Level,activeSettings);
            }
            else
            {
                var text=session.Current.AnswerKind==AnswerKind.Text?response.Text:string.Join(' ',taps.Select(t=>t.ToString("R",CultureInfo.InvariantCulture)));
                attempt=exam is not null?exam.Submit(text):session.AnswerResponse(text,activeSettings);
            }
            if(exam is not null){SaveExam();UpdateExamStatus();}
            history.Add(attempt);answered=true;submit.Enabled=false;tapClock.Stop();
            var saved=storageHealthy;
            var message=$"{(attempt.Correct?"Σωστά.":"Λάθος.")} Σωστή απάντηση: {attempt.ExpectedName}. {attempt.Analysis}";
            try{if(storageHealthy)store.Save(history);else message+=" Η αποθήκευση είναι απενεργοποιημένη λόγω προηγούμενου σφάλματος.";}
            catch(Exception ex){saved=false;message+=" Η απάντηση δεν αποθηκεύτηκε: "+ex.Message;}
            message+=saved&&automaticPractice.Checked&&exam is null?$" Αυτόματη επόμενη άσκηση σε {automaticDelay.Value} δευτερόλεπτα. Esc ακυρώνει.":" Ctrl+N για συνέχεια.";
            UpdateHistory();Tell(exam?.Running==true?"Η απάντηση καταχωρίστηκε. Η αναλυτική βαθμολογία εμφανίζεται στο τέλος. Ctrl+N για επόμενη ερώτηση.":message);
            if(saved&&session.Current is { } completed)_=QueueAutomaticPractice(completed,true,NewExercise);
        }
        catch(Exception ex){Error(ex.Message);}
    }
    private void SavePreferences()
    {
        if (loading || !storageHealthy || auditOnly || uiTestMode) return;
        var preset = presets.SelectedItem as PianoPreset;
        try { var valid=ReadSettings();Theory.Validate(valid);store.SavePreferences(new(valid, fontPath, preset?.Bank ?? 0, preset?.Program ?? 0, (devices.SelectedItem as OutputDevice)?.Id,cancelSpeech.Checked,automaticPractice.Checked,(int)automaticDelay.Value)); }
        catch(ArgumentException){ /* Preserve the previous valid settings while a range is being edited. */ }
        catch (Exception ex) { Error("Αδυναμία αποθήκευσης ρυθμίσεων: " + ex.Message); }
    }
    private void InvalidateExercise()
    {
        if (loading) return;
        if(exam?.Running==true){Stop();playbackCache.Clear();heard=false;replay.Enabled=true;submit.Enabled=false;return;}
        meter.Enabled=category.SelectedIndex is (int)Category.RhythmDictation or (int)Category.RhythmReproduction;
        bars.Enabled=category.SelectedIndex is (int)Category.Meters or (int)Category.RhythmDictation or (int)Category.RhythmReproduction;
        Stop(); session.Clear(); heard = false; playbackCache.Clear(); answers.Items.Clear(); submit.Enabled = false; replay.Enabled = false;
        status.Text = "Οι ρυθμίσεις άλλαξαν. Ctrl+N για νέα άσκηση.";
        SchedulePreferenceSave();
    }
    private void Stop()
    {
        CancelAutomaticPractice();playbackGeneration++; rendering?.Cancel(); audio.Stop(); playing = false; pause.Enabled = false;
    }
    private void SchedulePreferenceSave(){if(loading||auditOnly||uiTestMode)return;preferenceSave.Stop();preferenceSave.Start();}
    private void ChangePlaybackSettings()
    {
        if(loading||adjustingSegments||exam?.Running==true)return;
        Stop();playbackCache.Clear();
        if(session.Current is not null)
        {
            activeSettings=activeSettings with {Tempo=(int)tempo.Value,Repetitions=(int)repeats.Value,Velocity=(int)velocity.Value,Pedal=pedal.Checked,SegmentStart=(double)segmentStart.Value,SegmentEnd=segmentEnd.Value==0?null:(double)segmentEnd.Value,SoloVoice=soloVoice.SelectedIndex-1};
            replay.Enabled=true;submit.Enabled=false;heard=false;
            status.Text="Η ταχύτητα ή η έκφραση άλλαξε. Ctrl+R για το ίδιο παράδειγμα με τις ίδιες νότες.";
        }
        SchedulePreferenceSave();
    }
    private void Tell(string text)
    {
        if(playing||IsDisposed)return;
        status.Text=text;tabs.SelectedIndex=1;
        if(auditOnly)return;
        if(!status.AccessibilityObject.RaiseAutomationNotification(AutomationNotificationKind.ActionCompleted,AutomationNotificationProcessing.ImportantMostRecent,text))
        {status.Focus();status.Select(0,0);}
    }
    private void Error(string text){if(IsDisposed||Disposing)return;Stop();if(regressionError is not null){regressionError(text);return;}MessageBox.Show(this,text,"Professional Ear Training Studio",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    private void UpdateHistory()
    {
        if(exam?.Running==true){stats.Text="Οι αναλυτικές απαντήσεις της τρέχουσας εξέτασης εμφανίζονται μετά τον τερματισμό της.";return;}
        var correct = history.Count(a => a.Correct);
        var percentage = history.Count == 0 ? 0 : 100.0 * correct / history.Count;
        var mean=history.Count==0?0:history.Average(LearningAnalytics.EffectivePercentage);
        var lines = new List<string> { $"Επιτυχείς απαντήσεις: {correct} από {history.Count}. Ποσοστό επιτυχών απαντήσεων: {percentage:F1}%. Μέση βαθμολογία με μερικά σωστά: {mean:F1}%." };
        foreach (var group in history.GroupBy(a => a.Category)) lines.Add($"{CategoryName(group.Key)}: {group.Count(a => a.Correct)} / {group.Count()}");
        lines.Add("Πρόσφατες απαντήσεις (έως 200). Όλο το ιστορικό διατηρείται στο αρχείο JSON.");
        lines.AddRange(history.TakeLast(200).Reverse().Select(a => $"{a.At.LocalDateTime:g} — {CategoryName(a.Category)} — {(a.Correct ? "Σωστό" : "Λάθος")}. Απάντηση: {a.AnswerName}. Αναμενόμενο: {a.ExpectedName}."));
        stats.Lines = lines.ToArray();
    }
    private static string CategoryName(Category value)=>CategoryLabels.Name(value);
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.Tab: tabs.SelectedIndex=(tabs.SelectedIndex+1)%tabs.TabPages.Count;tabs.Focus();return true;
            case Keys.Control | Keys.Shift | Keys.Tab: tabs.SelectedIndex=(tabs.SelectedIndex+tabs.TabPages.Count-1)%tabs.TabPages.Count;tabs.Focus();return true;
            case Keys.Control | Keys.Shift | Keys.R: _=PreviewAnswer();return true;
            case Keys.F8: RecordTap();return true;
            case Keys.F6: ReadAudioFailure();return true;
            case Keys.Control | Keys.I: Stop();tabs.SelectedIndex=1;prompt.Focus();prompt.SelectAll();return true;
            case Keys.Control | Keys.N: _ = NewExercise(); return true;
            case Keys.Control | Keys.R: _ = Replay(); return true;
            case Keys.Control | Keys.P: TogglePause(); return true;
            case Keys.Control | Keys.Enter: Submit(); return true;
            case Keys.Escape: Stop(); status.Text = "Ο ήχος διακόπηκε.";if(heard&&tabs.SelectedIndex==1)FocusResponse();return true;
            case Keys.Control | Keys.Home: Stop(); tabs.SelectedIndex = 0; setup.Focus(); return true;
            case Keys.Control | Keys.Shift | Keys.S: Stop(); tabs.SelectedIndex=1;status.Focus();status.SelectAll(); return true;
            case Keys.F1: Stop(); tabs.SelectedIndex=tabs.TabPages.Count-1;tabs.TabPages[^1].Controls[0].Focus(); return true;
            default: return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
