using System.Windows.Forms.Automation;
using System.Globalization;
using System.Text.Json;
using Studio.Core;
using Studio.Audio;
namespace Studio.Windows;
public sealed partial class MainForm
{
    private readonly AccessibleTabPage isonPage=new("Βυζαντινός ισοκράτης");
    private readonly ComboBox isonVoice=Combo("Φωνή βυζαντινού ισοκράτη",[]);
    private readonly ComboBox isonGenus=Combo("Διαστηματικό γένος ισοκρατήματος",[]);
    private readonly ComboBox isonAccidental=Combo("Πρόσθετη ύφεση ή δίεση επιλεγμένου φθόγγου",["Χωρίς πρόσθετη αλλοίωση","Ύφεση 2 μορίων","Ύφεση 4 μορίων","Ύφεση 6 μορίων","Ύφεση 8 μορίων","Δίεση 2 μορίων","Δίεση 4 μορίων","Δίεση 6 μορίων","Δίεση 8 μορίων"]);
    private static readonly double[] IsonAccidentalValues=[0,-2,-4,-6,-8,2,4,6,8];
    private readonly NumericUpDown isonCustomAccidental=Number("Ελεύθερη πρόσθετη αλλοίωση επιλεγμένου φθόγγου σε μόρια",-ByzantineIntonation.MaximumAlteration,ByzantineIntonation.MaximumAlteration,0);
    private readonly Button isonEditTuning=new(){Text="Προσωπικό κούρδισμα ανά φθόγγο",AutoSize=true};
    private readonly Button isonResetTuning=new(){Text="Επαναφορά προτύπου επιλεγμένου ήχου",AutoSize=true};
    private Dictionary<string,IsonTuning> isonModeTunings=[];private string isonTuningModeId="first";
    private bool isonAnchorReference=true;
    private double[] isonAlterations=new double[7];private bool isonIntonationChanging;
    private readonly ComboBox isonMode=Combo("Ήχος βυζαντινού ισοκρατήματος",[]);
    private readonly Button isonKeyboard=new(){Text="Ζωντανός χειρισμός με βέλη",AccessibleName="Ζωντανός χειρισμός ισοκράτη με τα τέσσερα βέλη",AccessibleDescription="Shift και πάνω ή κάτω: οκτάβα. Πάνω και κάτω: φθόγγος. Δεξιά και αριστερά: φωνή. Esc: διακοπή. Tab: κανονική πλοήγηση στις ρυθμίσεις.",AutoSize=true};
    private readonly System.Windows.Forms.Timer isonPreferenceSave=new(){Interval=350};
    private readonly IsonLoopCache isonCache=new();private bool isonArmed;private CancellationTokenSource? isonWarmWork;private string? isonWarmKey;
    private readonly ComboBox isonNote=Combo("Φθόγγος ισοκρατήματος",Enumerable.Range(0,15).Select(ByzantineIntonation.PitchLabel).ToArray());
    private readonly NumericUpDown isonBase=Number("Διατονική αναφορά Νη σε Hz",35,800,131);
    private readonly NumericUpDown isonMoria=Number("Μεταφορά σε μόρια, 72 ανά οκτάβα",-72,72,0);
    private readonly NumericUpDown isonVolume=Number("Ένταση ισοκράτη σε ποσοστό",1,100,45);
    private readonly TextBox isonIntervals=new(){Text="12 10 8 12 12 10 8",AccessibleName="Επτά διαστήματα από Νη σε μόρια, άθροισμα 72",Width=310};
    private readonly TextBox isonStatus=new(){ReadOnly=true,Multiline=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,AccessibleName="Κατάσταση βυζαντινού ισοκράτη"};
    private readonly Button isonImport=new(){Text="Εισαγωγή ZIP φωνών",AccessibleName="Εισαγωγή όλων των ZIP φωνών ισοκράτη",AutoSize=true};
    private readonly Button isonCancelImport=new(){Text="Ακύρωση εισαγωγής",AutoSize=true,Enabled=false};
    private IsonBank? isonBank;private string? isonManifest;private bool isonPlaying,isonLoaded,isonImporting;
    private CancellationTokenSource? isonWork,isonImportWork;private long isonRequest;
    private sealed record IsonPreferences(string VoiceId,int Degree,decimal Base,decimal Moria,decimal Volume,string Intervals,string ModeId="first",string? GenusId=null,double[]? Alterations=null,bool? Anchored=null,Dictionary<string,IsonTuning>? ModeTunings=null);
    private IsonPreferences? isonLastGood;
    private IsonPreferences CaptureIsonPreferences(){SaveCurrentIsonTuning(isonTuningModeId);return new((isonVoice.SelectedItem as IsonVoice)?.Id??"",isonNote.SelectedIndex,isonBase.Value,isonMoria.Value,isonVolume.Value,isonIntervals.Text,(isonMode.SelectedItem as ByzantineMode)?.Id??"first",(isonGenus.SelectedItem as ByzantineGenus)?.Id,isonAlterations.ToArray(),isonAnchorReference,IsonTuning.CopyMap(isonModeTunings));}
    private sealed record IsonVoice(string Id,string Name,double Low,double High){public override string ToString()=>$"{Name} — {Low:F1} έως {High:F1} Hz";}
    private IsonLibraryStore IsonStore=>new(IsonDataDirectory);
    private void BuildIsokratis()
    {
        isonCustomAccidental.DecimalPlaces=2;isonCustomAccidental.Increment=0.1m;
        isonPreferenceSave.Tick+=(_,_)=>{isonPreferenceSave.Stop();WriteIsonPreferences();};
        isonGenus.Items.AddRange(ByzantineIntonation.All.Cast<object>().ToArray());isonGenus.SelectedIndex=0;
        isonMode.Items.AddRange(ByzantineModes.All.Cast<object>().ToArray());isonMode.SelectedIndex=0;isonNote.SelectedIndex=1;isonIntervals.Text=string.Join(' ',ByzantineIntonation.ModeIntervals("first"));
        isonBase.DecimalPlaces=3;isonBase.Value=130.813m;isonBase.Increment=0.1m;isonMoria.DecimalPlaces=2;isonMoria.Increment=0.5m;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new(12)};
        layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.Percent,100));
        layout.Controls.Add(new Label{AutoSize=true,MaximumSize=new(900,0),Text="Εγκατάσταση φωνών μία φορά. Ctrl+L: έναρξη και ζωντανός χειρισμός. Πάνω/κάτω: φθόγγοι, Shift+πάνω/κάτω: οκτάβα, δεξιά/αριστερά: φωνές. Tab: ρυθμίσεις, Esc: διακοπή. Οι ήχοι επιλέγουν βάση και διαστηματικό γένος. Οι σειρές αρχίζουν από Νη και έχουν 72 μόρια ανά οκτάβα. Η βάση του ήχου παραμένει σταθερή κατά την αλλαγή γένους. Δεν εφαρμόζονται αυτόματες έλξεις."});
        var fields=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
        fields.Controls.AddRange([new Label{Text="Ήχος",AutoSize=true},isonMode,new Label{Text="Γένος",AutoSize=true},isonGenus,new Label{Text="Αλλοίωση φθόγγου",AutoSize=true},isonAccidental,new Label{Text="Ελεύθερη αλλοίωση ±",AutoSize=true},isonCustomAccidental,new Label{Text="Φωνή",AutoSize=true},isonVoice,new Label{Text="Φθόγγος",AutoSize=true},isonNote,new Label{Text="Νη Hz",AutoSize=true},isonBase,new Label{Text="Μόρια ±",AutoSize=true},isonMoria,new Label{Text="Ένταση",AutoSize=true},isonVolume,new Label{Text="Διαστήματα",AutoSize=true},isonIntervals]);layout.Controls.Add(fields);
        var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};var play=new Button{Text="Έναρξη ισοκρατήματος (Ctrl+L)",AutoSize=true};var stop=new Button{Text="Διακοπή ισοκρατήματος (Esc)",AutoSize=true};
        buttons.Controls.AddRange([isonImport,isonCancelImport,play,isonKeyboard,stop,isonEditTuning,isonResetTuning]);layout.Controls.Add(buttons);layout.Controls.Add(isonStatus);isonPage.Controls.Add(layout);tabs.TabPages.Add(isonPage);
        isonKeyboard.Click+=async(_,_)=>{if(!isonArmed)await PlayIson();};
        isonMode.SelectedIndexChanged+=(_,_)=>{if(!isonLoaded)return;SaveCurrentIsonTuning(isonTuningModeId);isonLoaded=false;var mode=(ByzantineMode)isonMode.SelectedItem!;isonNote.SelectedIndex=mode.Degree;isonTuningModeId=mode.Id;SetIsonTuning(isonModeTunings.TryGetValue(mode.Id,out var own)?own:DefaultIsonTuning(mode.Id));isonLoaded=true;RefreshIsonNotes();IsonChanged();};
        isonGenus.SelectedIndexChanged+=(_,_)=>{if(!isonLoaded||isonIntonationChanging)return;isonLoaded=false;if(isonGenus.SelectedItem is ByzantineGenus{Intervals.Length:7} genus){isonAnchorReference=true;var mode=(ByzantineMode)isonMode.SelectedItem!;isonIntervals.Text=string.Join(' ',genus.Id==ByzantineIntonation.DefaultGenre(mode.Id)?ByzantineIntonation.ModeIntervals(mode.Id):genus.Intervals);}isonLoaded=true;RefreshIsonNotes();IsonChanged();};
        isonAccidental.SelectedIndexChanged+=(_,_)=>{if(!isonLoaded||isonIntonationChanging||isonAccidental.SelectedIndex<0)return;isonAlterations[isonNote.SelectedIndex%7]=IsonAccidentalValues[isonAccidental.SelectedIndex];RefreshIsonNotes();IsonChanged();};
        isonCustomAccidental.ValueChanged+=(_,_)=>{if(!isonLoaded||isonIntonationChanging)return;isonAlterations[isonNote.SelectedIndex%7]=(double)isonCustomAccidental.Value;RefreshIsonNotes();IsonChanged();};
        isonEditTuning.Click+=(_,_)=>{try{using var dialog=new IsonTuningDialog(CurrentIsonTuning());if(dialog.ShowDialog(this)==DialogResult.OK){isonLoaded=false;SetIsonTuning(dialog.Result!);isonLoaded=true;RefreshIsonNotes();IsonChanged();}}catch(Exception e)when(e is ArgumentException or FormatException or OverflowException){isonStatus.Text=e.Message;isonStatus.Focus();}};
        isonResetTuning.Click+=(_,_)=>{isonLoaded=false;SetIsonTuning(DefaultIsonTuning(isonTuningModeId));isonLoaded=true;RefreshIsonNotes();IsonChanged();};
        isonCancelImport.Click+=(_,_)=>isonImportWork?.Cancel();
        isonImport.Click+=async(_,_)=>await ImportIson();play.Click+=async(_,_)=>await PlayIson();stop.Click+=(_,_)=>Stop();
        foreach(var n in new[]{isonBase,isonMoria,isonVolume})n.ValueChanged+=(_,_)=>IsonChanged();isonNote.SelectedIndexChanged+=(_,_)=>{if(isonIntonationChanging)return;RefreshIsonNotes();IsonChanged();};isonVoice.SelectedIndexChanged+=(_,_)=>IsonChanged();isonIntervals.TextChanged+=(_,_)=>{if(!isonLoaded)return;isonIntonationChanging=true;isonGenus.SelectedItem=ByzantineIntonation.All.Single(g=>g.Id=="custom");isonIntonationChanging=false;RefreshIsonNotes();IsonChanged();};
        isonStatus.Text="Δεν έχει φορτωθεί βιβλιοθήκη. Επίλεξε Εισαγωγή ZIP φωνών και διάλεξε μαζί part001.zip, part002.zip κ.λπ. Οι φωνές εγκαθίστανται μόνο τοπικά.";
    }
    private IsonTuning CurrentIsonTuning()=>new(ByzantineTuning.Parse(isonIntervals.Text),isonAlterations.ToArray(),(isonGenus.SelectedItem as ByzantineGenus)?.Id??"custom",isonAnchorReference);
    private static IsonTuning DefaultIsonTuning(string id)=>new(ByzantineIntonation.ModeIntervals(id),new double[7],ByzantineIntonation.DefaultGenre(id),true);
    private void SaveCurrentIsonTuning(string id){try{var tuning=CurrentIsonTuning();tuning.Validate();isonModeTunings[id]=tuning;}catch(ArgumentException){}catch(FormatException){}}
    private void SetIsonTuning(IsonTuning tuning){tuning.Validate();isonAnchorReference=tuning.Anchored;isonAlterations=tuning.Alterations.ToArray();isonGenus.SelectedItem=ByzantineIntonation.All.Single(g=>g.Id==tuning.GenusId);isonIntervals.Text=string.Join(' ',tuning.Intervals.Select(n=>n.ToString("R",CultureInfo.InvariantCulture)));}
    private int IsonAnchor=>((ByzantineMode)isonMode.SelectedItem!).Degree;
    private bool IsonAnchored=>isonAnchorReference;
    private double IsonFrequency(int? degree=null)=>ByzantineIntonation.Frequency((double)isonBase.Value,degree??isonNote.SelectedIndex,IsonAnchor,ByzantineTuning.Parse(isonIntervals.Text),IsonAnchored,(double)isonMoria.Value,isonAlterations);
    private string IsonIntonationSummary()
    {
        var steps=ByzantineTuning.Parse(isonIntervals.Text);var anchor=IsonAnchor;var pairs=Enumerable.Range(0,7).Select(i=>$"{ByzantineTuning.Notes[(anchor+i)%7]} προς {ByzantineTuning.Notes[(anchor+i+1)%7]}: {steps[(anchor+i)%7]:0.##}");
        return $"{isonMode.SelectedItem}. {isonGenus.SelectedItem}. Διαστήματα από τη βάση {ByzantineTuning.Notes[anchor]}: "+string.Join(" · ",pairs)+". Η λίστα φθόγγων αναφέρει τις συνολικές αλλοιώσεις σε σχέση με τη διατονική αναφορά Νη.";
    }
    private void RefreshIsonNotes()
    {
        if(!isonLoaded||isonIntonationChanging)return;isonIntonationChanging=true;
        try{var intervals=ByzantineTuning.Parse(isonIntervals.Text);var degree=Math.Clamp(isonNote.SelectedIndex,0,14);isonNote.Items.Clear();isonNote.Items.AddRange(Enumerable.Range(0,15).Select(n=>(object)ByzantineIntonation.NoteName(n,IsonAnchor,intervals,IsonAnchored,isonAlterations)).ToArray());isonNote.SelectedIndex=degree;isonAccidental.SelectedIndex=Array.IndexOf(IsonAccidentalValues,isonAlterations[degree%7]);isonCustomAccidental.Value=(decimal)isonAlterations[degree%7];}
        catch(Exception e)when(e is ArgumentException or FormatException or OverflowException){isonStatus.Text=e.Message;}
        finally{isonIntonationChanging=false;}
    }
    private void IsonChanged()
    {
        if(!isonLoaded)return;
        try{var hz=IsonFrequency();isonStatus.Text=$"Έτοιμο: {isonNote.SelectedItem}, {hz:F3} Hz. Ctrl+L για ισοκράτημα. Με ενεργό ισοκράτημα η αλλαγή ακούγεται αυτόματα.";isonStatus.Text+="\r\n"+IsonIntonationSummary();SaveIsonPreferences();if(isonArmed)_=PlayIson(false);}
        catch(Exception e)when(e is ArgumentException or FormatException or OverflowException){Stop();isonStatus.Text=e.Message;}
    }
    private void SaveIsonPreferences(){if(auditOnly||uiTestMode||!isonLoaded)return;isonPreferenceSave.Stop();isonPreferenceSave.Start();}
    private void WriteIsonPreferences(){if(auditOnly||uiTestMode||!isonLoaded)return;try{CurrentIsonTuning().Validate();Directory.CreateDirectory(IsonDataDirectory);var path=Path.Combine(IsonDataDirectory,"isokratis-settings.json");var temporary=path+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(CaptureIsonPreferences()));File.Move(temporary,path,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or FormatException or OverflowException){isonStatus.Text+=" Δεν αποθηκεύτηκαν ρυθμίσεις: "+e.Message;}}
    private void PopulateIson()
    {
        isonVoice.Items.Clear();if(isonBank is null)return;
        isonVoice.Items.AddRange(isonBank.Samples.GroupBy(s=>s.VoiceId).Select(g=>(object)new IsonVoice(g.Key,g.First().VoiceName,g.Min(s=>s.DeclaredHz),g.Max(s=>s.DeclaredHz))).OrderBy(v=>v.ToString()).ToArray());isonVoice.SelectedIndex=0;
    }
    private void InitializeIson()
    {
        if(auditOnly||uiTestMode)return;
        try{
            var installed=IsonStore.Load();if(installed is not null){isonManifest=installed.Manifest;isonBank=installed.Bank;PopulateIson();isonImport.Text="Αντικατάσταση εγκατεστημένων φωνών";isonImport.AccessibleName="Αντικατάσταση ήδη εγκατεστημένων φωνών από ZIP";isonStatus.Text=$"Οι {isonBank.Samples.Length} φωνές φορτώθηκαν αυτόματα από την εγκατεστημένη βιβλιοθήκη. Δεν χρειάζονται ξανά ZIP. Ctrl+L για έναρξη.";}
            var path=Path.Combine(IsonDataDirectory,"isokratis-settings.json");if(File.Exists(path)){
                var p=JsonSerializer.Deserialize<IsonPreferences>(File.ReadAllText(path));if(p is not null)ApplyIsonPreferences(p);
            }
        }catch(Exception e){isonStatus.Text="Πρόβλημα φόρτωσης ρύθμισης: "+e.Message+". Η εγκατεστημένη βιβλιοθήκη διατηρείται. Ελέγξτε τα πεδία ρυθμίσεων.";}
        finally{isonLoaded=true;RefreshIsonNotes();}
    }
    private void ApplyIsonPreferences(IsonPreferences p)
    {
        isonModeTunings=IsonTuning.CopyMap(p.ModeTunings);isonTuningModeId=p.ModeId;
        isonBase.Value=Math.Clamp(p.Base,35,800);isonMoria.Value=Math.Clamp(p.Moria,-72,72);isonVolume.Value=Math.Clamp(p.Volume,1,100);isonNote.SelectedIndex=Math.Clamp(p.Degree,0,14);var selectedMode=isonMode.Items.Cast<ByzantineMode>().FirstOrDefault(m=>m.Id==p.ModeId);if(selectedMode is not null)isonMode.SelectedItem=selectedMode;ByzantineTuning.Parse(p.Intervals);isonIntervals.Text=p.Intervals;var genusId=p.GenusId??(ByzantineTuning.Parse(p.Intervals).SequenceEqual(ByzantineTuning.Diatonic)?ByzantineIntonation.DefaultGenre(p.ModeId):"custom");if(p.GenusId is null&&p.ModeId=="grave"&&p.Degree==6)genusId="soft-diatonic";var genus=ByzantineIntonation.All.SingleOrDefault(g=>g.Id==genusId)??ByzantineIntonation.All.Last();isonGenus.SelectedItem=genus;isonAnchorReference=p.Anchored??genus.Id!="custom";if(p.GenusId is null&&genus.Intervals.Length==7)isonIntervals.Text=string.Join(' ',genus.Id==ByzantineIntonation.DefaultGenre(p.ModeId)?ByzantineIntonation.ModeIntervals(p.ModeId):genus.Intervals);isonAlterations=p.Alterations?.ToArray()??new double[7];ByzantineIntonation.ValidateAlterations(isonAlterations);var item=isonVoice.Items.Cast<IsonVoice>().FirstOrDefault(v=>v.Id==p.VoiceId);if(item is not null)isonVoice.SelectedItem=item;
    }
    private async Task ImportIson()
    {
        if(isonImporting)return;
        try{
            if(exam is not null&&exam.State.Finished is null)throw new InvalidOperationException("Ολοκλήρωσε πρώτα την εξέταση.");
            using var dialog=new OpenFileDialog{Filter="ZIP φωνών (*.zip)|*.zip",Multiselect=true,Title="Επίλεξε όλα τα ZIP φωνών, part001 και part002 μαζί"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            Stop();isonImporting=true;isonImport.Enabled=false;isonCancelImport.Enabled=true;using var source=new CancellationTokenSource();isonImportWork=source;
            var destination=Path.Combine(IsonDataDirectory,"isokratis-banks",Guid.NewGuid().ToString("N"));
            var progress=new Progress<string>(s=>{if(!IsDisposed)isonStatus.Text=s;});
            var manifest=await Task.Run(()=>IsonBank.Import(dialog.FileNames,destination,IsonDecoder.Decode,progress,source.Token),source.Token);
            if(IsDisposed)return;source.Token.ThrowIfCancellationRequested();var bank=IsonBank.Load(manifest);
            IsonStore.Activate(manifest);isonCache.Clear();isonWarmKey=null;
            isonLoaded=false;isonManifest=manifest;isonBank=bank;PopulateIson();isonImport.Text="Αντικατάσταση εγκατεστημένων φωνών";isonImport.AccessibleName="Αντικατάσταση ήδη εγκατεστημένων φωνών από ZIP";isonLoaded=true;SaveIsonPreferences();isonStatus.Text=$"Η εισαγωγή ολοκληρώθηκε: {bank.Samples.Length} δείγματα, {isonVoice.Items.Count} ομάδες φωνών. Επίλεξε φωνή και βάση, Ctrl+L για συνεχή ακρόαση.";
        }catch(OperationCanceledException){if(!IsDisposed)isonStatus.Text="Η εισαγωγή ακυρώθηκε. Η προηγούμενη βιβλιοθήκη διατηρείται.";}
        catch(Exception e){if(!IsDisposed)isonStatus.Text="Η εισαγωγή δεν ολοκληρώθηκε: "+e.Message;}
        finally{isonImporting=false;isonImportWork=null;if(!IsDisposed){isonImport.Enabled=true;isonCancelImport.Enabled=false;if(tabs.SelectedTab==isonPage){isonStatus.Focus();isonStatus.SelectAll();}}}
    }
    private async Task PlayIson(bool focusKeyboard=true)
    {
        CancellationTokenSource? source=null;
        try{
            if(exam is not null)throw new InvalidOperationException("Επίστρεψε σε εξάσκηση από την καρτέλα Εξετάσεις πριν χρησιμοποιήσεις ισοκράτη.");
            if(isonImporting)throw new InvalidOperationException("Περίμενε να ολοκληρωθεί η εισαγωγή.");
            if(isonBank is null||isonManifest is null||isonVoice.SelectedItem is not IsonVoice voice)throw new InvalidOperationException("Εισήγαγε πρώτα τα ZIP φωνών.");
            if(devices.SelectedItem is not OutputDevice device)throw new InvalidOperationException("Επίλεξε έξοδο ήχου στις Ρυθμίσεις.");
            var frequency=IsonFrequency();var sample=IsonRenderer.Select(isonBank,voice.Id,frequency);var directory=Path.GetDirectoryName(isonManifest)!;var volume=(double)isonVolume.Value/100;
            if(!isonArmed){Stop();isonArmed=true;}isonWork?.Cancel();heard=false;submit.Enabled=false;if(focusKeyboard)isonKeyboard.Focus();var request=++isonRequest;source=new CancellationTokenSource();isonWork=source;isonStatus.Text="Προετοιμασία συνεχούς ισοκρατήματος…";
            var loop=await Task.Run(()=>CachedIson(directory,sample,frequency,volume,source.Token),source.Token);
            source.Token.ThrowIfCancellationRequested();if(IsDisposed||request!=isonRequest||tabs.SelectedTab!=isonPage)return;
            if(cancelSpeech.Checked&&focusKeyboard)NvdaSpeech.Cancel();heard=false;submit.Enabled=false;if(!isonPlaying||!audio.TrySwitchLoop(loop.Samples)){var generation=++playbackGeneration;
            audio.Play(loop.Samples,device,error=>{if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke((Action)(()=>{if(generation!=playbackGeneration)return;isonPlaying=false;playing=false;if(error is not null)ShowAudioFailure(error);}));}catch(InvalidOperationException)when(IsDisposed||Disposing){}},true);}
            isonPlaying=true;playing=audio.IsPlaying;isonLastGood=CaptureIsonPreferences();SaveIsonPreferences();isonStatus.Text=$"Παίζει {voice.Name} στα {frequency:F3} Hz. Εκτιμώμενη βάση δείγματος: {loop.EstimatedRootHz:F3} Hz, αξιοπιστία {loop.Confidence:P0}. Esc για διακοπή. Η εκτίμηση κουρδίσματος και η ακουστική ποιότητα χρειάζονται προσωπικό έλεγχο.\r\n"+IsonIntonationSummary();WarmIson(voice.Id,directory,volume);
        }catch(OperationCanceledException){if(!IsDisposed&&ReferenceEquals(isonWork,source))isonStatus.Text="Η προετοιμασία ακυρώθηκε. Ctrl+L για έναρξη.";}
        catch(Exception e){if(!IsDisposed&&(source is null||ReferenceEquals(isonWork,source))){if(isonPlaying&&(audio.IsPlaying||audio.IsPaused)&&isonLastGood is not null){isonLoaded=false;ApplyIsonPreferences(isonLastGood);isonLoaded=true;RefreshIsonNotes();isonArmed=true;SaveIsonPreferences();isonStatus.Text="Η νέα επιλογή δεν υποστηρίζεται: "+e.Message+" Διατηρήθηκε το προηγούμενο έγκυρο ισοκράτημα. Τα βέλη παραμένουν ενεργά.";}else{Stop();isonStatus.Text="Δεν ξεκίνησε ο ισοκράτης: "+e.Message;}}}
        finally{if(ReferenceEquals(isonWork,source))isonWork=null;source?.Dispose();}
    }
    private void ToggleIsonPause()
    {
        if(!isonPlaying){isonStatus.Text="Δεν υπάρχει ενεργό ισοκράτημα. Ctrl+L για έναρξη.";return;}
        try{if(audio.IsPaused&&cancelSpeech.Checked)NvdaSpeech.Cancel();audio.PauseOrResume();playing=audio.IsPlaying;isonStatus.Text=playing?"Συνέχεια ισοκρατήματος. Esc για διακοπή.":"Παύση ισοκρατήματος. Ctrl+P για συνέχεια.";
            if(!playing)isonStatus.AccessibilityObject.RaiseAutomationNotification(AutomationNotificationKind.ActionCompleted,AutomationNotificationProcessing.ImportantMostRecent,isonStatus.Text);
        }catch(Exception e){Stop();isonStatus.Text="Αποτυχία παύσης ή συνέχειας: "+e.Message;}
    }
    private void StopIsonWork(){isonArmed=false;CancelIsonWarm();isonWarmKey=null;isonRequest++;isonWork?.Cancel();if(isonPlaying&&!IsDisposed)isonStatus.Text="Το ισοκράτημα σταμάτησε. Ctrl+L για νέα έναρξη.";isonPlaying=false;}
    private IsonLoop CachedIson(string directory,IsonSample sample,double frequency,double volume,CancellationToken token)
    {
        var key=sample.WaveSha256+":"+sample.DeclaredHz.ToString("R",CultureInfo.InvariantCulture)+":"+frequency.ToString("R",CultureInfo.InvariantCulture)+":"+volume.ToString("R",CultureInfo.InvariantCulture);
        return isonCache.Get(key,()=>IsonRenderer.Render(IsonBank.ReadSample(directory,sample),sample.DeclaredHz,frequency,volume,sample.SourcePath,token));
    }
    private void CancelIsonWarm(){try{isonWarmWork?.Cancel();}catch(ObjectDisposedException){}}
    private void WarmIson(string voice,string directory,double volume)
    {
        var intervals=ByzantineTuning.Parse(isonIntervals.Text);var baseHz=(double)isonBase.Value;var moria=(double)isonMoria.Value;var anchor=IsonAnchor;var anchored=IsonAnchored;var alterations=isonAlterations.ToArray();var key=voice+":"+baseHz+":"+moria+":"+volume+":"+isonIntervals.Text+":"+anchor+":"+anchored+":"+string.Join(',',alterations);
        if(key==isonWarmKey||isonBank is null)return;CancelIsonWarm();isonWarmKey=key;var bank=isonBank;var source=new CancellationTokenSource();isonWarmWork=source;
        _=Task.Run(()=>{try{for(var degree=0;degree<15;degree++){source.Token.ThrowIfCancellationRequested();var hz=ByzantineIntonation.Frequency(baseHz,degree,anchor,intervals,anchored,moria,alterations);var sample=IsonRenderer.Select(bank,voice,hz);_=CachedIson(directory,sample,hz,volume,source.Token);}}catch(Exception e)when(e is OperationCanceledException or ArgumentException or IOException){}finally{source.Dispose();}});
    }
    private bool IsonArrow(Keys key)
    {
        if(!isonArmed||!isonKeyboard.Focused)return false;
        if(key is (Keys.Shift|Keys.Up) or (Keys.Shift|Keys.Down)){var next=ByzantineIntonation.ShiftOctave(isonNote.SelectedIndex,key==(Keys.Shift|Keys.Up)?1:-1);if(next!=isonNote.SelectedIndex)isonNote.SelectedIndex=next;return true;}
        if(key is not (Keys.Up or Keys.Down or Keys.Left or Keys.Right))return false;
        var combo=key is Keys.Up or Keys.Down?isonNote:isonVoice;if(combo.Items.Count==0)return true;
        var delta=key is Keys.Up or Keys.Right?1:-1;combo.SelectedIndex=(combo.SelectedIndex+combo.Items.Count+delta)%combo.Items.Count;return true;
    }
    internal object AuditIson(){var controls=AccessibilityContract.Audit(isonPage);if(isonVoice.AccessibilityObject.Name!="Φωνή βυζαντινού ισοκράτη"||isonStatus.ReadOnly!=true)throw new InvalidOperationException("Missing ison accessibility");return new{Passed=true,TabCount=tabs.TabCount,Controls=controls,NoSamplesBundled=true,IntervalCount=ByzantineTuning.Diatonic.Length,ModeCount=isonMode.Items.Count,GenusCount=isonGenus.Items.Count-1,NoteCount=isonNote.Items.Count,AccidentalCount=isonAccidental.Items.Count,CustomAccidentalMinimum=isonCustomAccidental.Minimum,CustomAccidentalMaximum=isonCustomAccidental.Maximum,TuningEditor=IsonTuningDialog.Audit(),LiveKeyboardName=isonKeyboard.AccessibilityObject.Name};}
}
