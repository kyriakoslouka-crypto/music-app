using System.Globalization;
using System.Text.Json;
using Studio.Core;
using Studio.Audio;
namespace Studio.Windows;
public sealed partial class MainForm
{
    private readonly AccessibleTabPage isonPage=new("Βυζαντινός ισοκράτης");
    private readonly ComboBox isonVoice=Combo("Φωνή βυζαντινού ισοκράτη",[]);
    private readonly ComboBox isonNote=Combo("Φθόγγος ισοκρατήματος",ByzantineTuning.Notes);
    private readonly NumericUpDown isonBase=Number("Βάση Νη σε Hz",35,800,131);
    private readonly NumericUpDown isonMoria=Number("Μεταφορά σε μόρια, 72 ανά οκτάβα",-72,72,0);
    private readonly NumericUpDown isonVolume=Number("Ένταση ισοκράτη σε ποσοστό",1,100,45);
    private readonly TextBox isonIntervals=new(){Text="12 10 8 12 12 10 8",AccessibleName="Επτά διαστήματα σε μόρια, άθροισμα 72",Width=310};
    private readonly TextBox isonStatus=new(){ReadOnly=true,Multiline=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,AccessibleName="Κατάσταση βυζαντινού ισοκράτη"};
    private readonly Button isonImport=new(){Text="Εισαγωγή ZIP φωνών",AccessibleName="Εισαγωγή όλων των ZIP φωνών ισοκράτη",AutoSize=true};
    private readonly Button isonCancelImport=new(){Text="Ακύρωση εισαγωγής",AutoSize=true,Enabled=false};
    private IsonBank? isonBank;private string? isonManifest;private bool isonPlaying,isonLoaded,isonImporting;
    private CancellationTokenSource? isonWork,isonImportWork;private long isonRequest;
    private sealed record IsonPreferences(string VoiceId,int Degree,decimal Base,decimal Moria,decimal Volume,string Intervals);
    private sealed record IsonVoice(string Id,string Name,double Low,double High){public override string ToString()=>$"{Name} — {Low:F1} έως {High:F1} Hz";}
    private static string IsonPointer=>Path.Combine(DataDirectory,"isokratis-current.txt");
    private void BuildIsokratis()
    {
        isonBase.DecimalPlaces=3;isonBase.Value=130.813m;isonBase.Increment=0.1m;isonMoria.DecimalPlaces=2;isonMoria.Increment=0.5m;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new(12)};
        layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.Percent,100));
        layout.Controls.Add(new Label{AutoSize=true,MaximumSize=new(900,0),Text="Ισοκράτημα από τις δικές σου ηχογραφημένες φωνές. Εισάγαγε μαζί όλα τα ZIP. Η αρχική κλίμακα είναι διατονικό πρότυπο 72 μορίων. Μπορείς να αλλάξεις τα επτά διαστήματα. Δεν είναι ακόμη ασκήσεις αναγνώρισης ήχων, φθορών ή χροών. Ctrl+L: έναρξη, Esc: διακοπή. Χρησιμοποιείται η έξοδος ήχου των Ρυθμίσεων."});
        var fields=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
        fields.Controls.AddRange([new Label{Text="Φωνή",AutoSize=true},isonVoice,new Label{Text="Φθόγγος",AutoSize=true},isonNote,new Label{Text="Νη Hz",AutoSize=true},isonBase,new Label{Text="Μόρια ±",AutoSize=true},isonMoria,new Label{Text="Ένταση",AutoSize=true},isonVolume,new Label{Text="Διαστήματα",AutoSize=true},isonIntervals]);layout.Controls.Add(fields);
        var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};var play=new Button{Text="Έναρξη ισοκρατήματος (Ctrl+L)",AutoSize=true};var stop=new Button{Text="Διακοπή ισοκρατήματος (Esc)",AutoSize=true};
        buttons.Controls.AddRange([isonImport,isonCancelImport,play,stop]);layout.Controls.Add(buttons);layout.Controls.Add(isonStatus);isonPage.Controls.Add(layout);tabs.TabPages.Add(isonPage);
        isonCancelImport.Click+=(_,_)=>isonImportWork?.Cancel();
        isonImport.Click+=async(_,_)=>await ImportIson();play.Click+=async(_,_)=>await PlayIson();stop.Click+=(_,_)=>Stop();
        foreach(var n in new[]{isonBase,isonMoria,isonVolume})n.ValueChanged+=(_,_)=>IsonChanged();isonNote.SelectedIndexChanged+=(_,_)=>IsonChanged();isonVoice.SelectedIndexChanged+=(_,_)=>IsonChanged();isonIntervals.TextChanged+=(_,_)=>IsonChanged();
        isonStatus.Text="Δεν έχει φορτωθεί βιβλιοθήκη. Επίλεξε Εισαγωγή ZIP φωνών και διάλεξε μαζί part001.zip, part002.zip κ.λπ. Οι φωνές εγκαθίστανται μόνο τοπικά.";
    }
    private double IsonFrequency()=>ByzantineTuning.Frequency((double)isonBase.Value,isonNote.SelectedIndex,(double)isonMoria.Value,ByzantineTuning.Parse(isonIntervals.Text));
    private void IsonChanged()
    {
        if(!isonLoaded)return;if(isonPlaying||isonWork is not null)Stop();
        try{var hz=IsonFrequency();isonStatus.Text=$"Έτοιμο: {ByzantineTuning.Notes[isonNote.SelectedIndex]}, {hz:F3} Hz. Ctrl+L για ισοκράτημα. Αλλαγή ρυθμίσεων σταματά την προηγούμενη βάση.";SaveIsonPreferences();}
        catch(Exception e)when(e is ArgumentException or FormatException or OverflowException){isonStatus.Text=e.Message;}
    }
    private void SaveIsonPreferences(){if(auditOnly||uiTestMode||!isonLoaded)return;try{File.WriteAllText(Path.Combine(DataDirectory,"isokratis-settings.json"),JsonSerializer.Serialize(new IsonPreferences((isonVoice.SelectedItem as IsonVoice)?.Id??"",isonNote.SelectedIndex,isonBase.Value,isonMoria.Value,isonVolume.Value,isonIntervals.Text)));}catch(Exception e)when(e is IOException or UnauthorizedAccessException){isonStatus.Text+=" Δεν αποθηκεύτηκαν ρυθμίσεις: "+e.Message;}}
    private void PopulateIson()
    {
        isonVoice.Items.Clear();if(isonBank is null)return;
        isonVoice.Items.AddRange(isonBank.Samples.GroupBy(s=>s.VoiceId).Select(g=>(object)new IsonVoice(g.Key,g.First().VoiceName,g.Min(s=>s.DeclaredHz),g.Max(s=>s.DeclaredHz))).OrderBy(v=>v.ToString()).ToArray());isonVoice.SelectedIndex=0;
    }
    private void InitializeIson()
    {
        if(auditOnly||uiTestMode)return;
        try{
            if(File.Exists(IsonPointer)){isonManifest=File.ReadAllText(IsonPointer).Trim();isonBank=IsonBank.Load(isonManifest);PopulateIson();isonStatus.Text=$"Φορτώθηκαν {isonBank.Samples.Length} δείγματα. Ctrl+L για έναρξη.";}
            var path=Path.Combine(DataDirectory,"isokratis-settings.json");if(File.Exists(path)){
                var p=JsonSerializer.Deserialize<IsonPreferences>(File.ReadAllText(path));if(p is not null){isonBase.Value=Math.Clamp(p.Base,35,800);isonMoria.Value=Math.Clamp(p.Moria,-72,72);isonVolume.Value=Math.Clamp(p.Volume,1,100);isonNote.SelectedIndex=Math.Clamp(p.Degree,0,7);ByzantineTuning.Parse(p.Intervals);isonIntervals.Text=p.Intervals;var item=isonVoice.Items.Cast<IsonVoice>().FirstOrDefault(v=>v.Id==p.VoiceId);if(item is not null)isonVoice.SelectedItem=item;}
            }
        }catch(Exception e){isonStatus.Text="Δεν φορτώθηκε η αποθηκευμένη βιβλιοθήκη/ρύθμιση: "+e.Message+". Τα αρχεία διατηρήθηκαν. Μπορείς να εισάγεις ξανά τα ZIP.";}
        finally{isonLoaded=true;}
    }
    private async Task ImportIson()
    {
        if(isonImporting)return;
        try{
            if(exam is not null&&exam.State.Finished is null)throw new InvalidOperationException("Ολοκλήρωσε πρώτα την εξέταση.");
            using var dialog=new OpenFileDialog{Filter="ZIP φωνών (*.zip)|*.zip",Multiselect=true,Title="Επίλεξε όλα τα ZIP φωνών, part001 και part002 μαζί"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            Stop();isonImporting=true;isonImport.Enabled=false;isonCancelImport.Enabled=true;using var source=new CancellationTokenSource();isonImportWork=source;
            var destination=Path.Combine(DataDirectory,"isokratis-banks",Guid.NewGuid().ToString("N"));
            var progress=new Progress<string>(s=>{if(!IsDisposed)isonStatus.Text=s;});
            var manifest=await Task.Run(()=>IsonBank.Import(dialog.FileNames,destination,IsonDecoder.Decode,progress,source.Token),source.Token);
            if(IsDisposed)return;source.Token.ThrowIfCancellationRequested();var bank=IsonBank.Load(manifest);
            var temporary=IsonPointer+".tmp";File.WriteAllText(temporary,manifest);File.Move(temporary,IsonPointer,true);
            isonLoaded=false;isonManifest=manifest;isonBank=bank;PopulateIson();isonLoaded=true;SaveIsonPreferences();isonStatus.Text=$"Η εισαγωγή ολοκληρώθηκε: {bank.Samples.Length} δείγματα, {isonVoice.Items.Count} ομάδες φωνών. Επίλεξε φωνή και βάση, Ctrl+L για συνεχή ακρόαση.";
        }catch(OperationCanceledException){if(!IsDisposed)isonStatus.Text="Η εισαγωγή ακυρώθηκε. Η προηγούμενη βιβλιοθήκη διατηρείται.";}
        catch(Exception e){if(!IsDisposed)isonStatus.Text="Η εισαγωγή δεν ολοκληρώθηκε: "+e.Message;}
        finally{isonImporting=false;isonImportWork=null;if(!IsDisposed){isonImport.Enabled=true;isonCancelImport.Enabled=false;if(tabs.SelectedTab==isonPage){isonStatus.Focus();isonStatus.SelectAll();}}}
    }
    private async Task PlayIson()
    {
        CancellationTokenSource? source=null;
        try{
            if(exam is not null)throw new InvalidOperationException("Επίστρεψε σε εξάσκηση από την καρτέλα Εξετάσεις πριν χρησιμοποιήσεις ισοκράτη.");
            if(isonImporting)throw new InvalidOperationException("Περίμενε να ολοκληρωθεί η εισαγωγή.");
            if(isonBank is null||isonManifest is null||isonVoice.SelectedItem is not IsonVoice voice)throw new InvalidOperationException("Εισήγαγε πρώτα τα ZIP φωνών.");
            if(devices.SelectedItem is not OutputDevice device)throw new InvalidOperationException("Επίλεξε έξοδο ήχου στις Ρυθμίσεις.");
            var frequency=IsonFrequency();var sample=IsonRenderer.Select(isonBank,voice.Id,frequency);var directory=Path.GetDirectoryName(isonManifest)!;var volume=(double)isonVolume.Value/100;
            Stop();heard=false;submit.Enabled=false;var request=++isonRequest;source=new CancellationTokenSource();isonWork=source;isonStatus.Text="Προετοιμασία συνεχούς ισοκρατήματος…";
            var loop=await Task.Run(()=>IsonRenderer.Render(IsonBank.ReadSample(directory,sample),sample.DeclaredHz,frequency,volume,sample.SourcePath,source.Token),source.Token);
            source.Token.ThrowIfCancellationRequested();if(IsDisposed||request!=isonRequest||tabs.SelectedTab!=isonPage)return;
            if(cancelSpeech.Checked)NvdaSpeech.Cancel();heard=false;submit.Enabled=false;var generation=++playbackGeneration;
            audio.Play(loop.Samples,device,error=>{if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke((Action)(()=>{if(generation!=playbackGeneration)return;isonPlaying=false;playing=false;if(error is not null)ShowAudioFailure(error);}));}catch(InvalidOperationException)when(IsDisposed||Disposing){}},true);
            isonPlaying=true;playing=true;SaveIsonPreferences();isonStatus.Text=$"Παίζει {voice.Name} στα {frequency:F3} Hz. Εκτιμώμενη βάση δείγματος: {loop.EstimatedRootHz:F3} Hz, αξιοπιστία {loop.Confidence:P0}. Esc για διακοπή. Η εκτίμηση κουρδίσματος και η ακουστική ποιότητα χρειάζονται προσωπικό έλεγχο.";
        }catch(OperationCanceledException){if(!IsDisposed&&ReferenceEquals(isonWork,source))isonStatus.Text="Η προετοιμασία ακυρώθηκε. Ctrl+L για έναρξη.";}
        catch(Exception e){if(!IsDisposed&&(source is null||ReferenceEquals(isonWork,source))){Stop();isonStatus.Text="Δεν ξεκίνησε ο ισοκράτης: "+e.Message;}}
        finally{if(ReferenceEquals(isonWork,source))isonWork=null;source?.Dispose();}
    }
    private void StopIsonWork(){isonRequest++;isonWork?.Cancel();if(isonPlaying&&!IsDisposed)isonStatus.Text="Το ισοκράτημα σταμάτησε. Ctrl+L για νέα έναρξη.";isonPlaying=false;}
    internal object AuditIson(){var controls=AccessibilityContract.Audit(isonPage);if(isonVoice.AccessibilityObject.Name!="Φωνή βυζαντινού ισοκράτη"||isonStatus.ReadOnly!=true)throw new InvalidOperationException("Missing ison accessibility");return new{Passed=true,TabCount=tabs.TabCount,Controls=controls,NoSamplesBundled=true,IntervalCount=ByzantineTuning.Diatonic.Length};}
}
