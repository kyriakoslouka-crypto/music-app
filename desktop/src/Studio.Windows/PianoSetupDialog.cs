using NAudio.Wave;
using Studio.Audio;
using System.Diagnostics;
namespace Studio.Windows;
public sealed class PianoSetupDialog : Form
{
    private readonly string installation;
    private readonly TextBox description = new() { ReadOnly=true, Multiline=true, Dock=DockStyle.Fill, ScrollBars=ScrollBars.Vertical, AccessibleName="Πληροφορίες και άδεια Maestro Concert Grand", Text=
        "Maestro Concert Grand — Yamaha CF-3\r\nMats Helgesson, 2003. Δημοσιευμένη μετατροπή SFZ από kinwie με άδεια του δημιουργού.\r\n\r\nΠραγματικά stereo δείγματα, πέντε velocity layers, 88 νότες, δείγματα απελευθέρωσης και πεντάλ.\r\n\r\nΗ αρχική άδεια δεν είναι ανοικτή: απαγορεύει πώληση και αναδημοσίευση τροποποιημένων δειγμάτων χωρίς άδεια. Η εφαρμογή κατεβάζει το πρωτότυπο από τη δημοσιευμένη πηγή και προετοιμάζει ιδιωτικό τοπικό αντίγραφο. Μην αναδιανείμεις τον φάκελο πιάνου.\r\n\r\nΑπαιτούνται περίπου 263 MB λήψης, 3 GB ελεύθερου χώρου και σύνδεση μόνο για την πρώτη εγκατάσταση. Μετά η χρήση είναι offline. Καμία αγορά, λογαριασμός ή αναζήτηση αρχείων δεν απαιτείται.\r\n\r\nTab: μετακίνηση. Alt+Ε: εγκατάσταση. Esc: ακύρωση. Η ακύρωση δεν καταστρέφει προηγούμενη εγκατάσταση. Η λήψη επαληθεύεται με συγκεκριμένο SHA-256." };
    private readonly TextBox state = new() { ReadOnly=true, Dock=DockStyle.Fill, AccessibleName="Κατάσταση εγκατάστασης", Text="Έτοιμο για εγκατάσταση." };
    private readonly ProgressBar progress = new() { Dock=DockStyle.Fill, AccessibleName="Πρόοδος εγκατάστασης πιάνου", Minimum=0, Maximum=100 };
    private readonly Button install = new() { Text="&Εγκατάσταση Maestro Concert Grand",AutoSize=true };
    private readonly Button close = new() {Text="Ακύρωση / &Κλείσιμο",AutoSize=true};
    private CancellationTokenSource? cancellation;
    private bool closing;
    public string? InstalledManifest {get;private set;}
    public PianoSetupDialog(string installation)
    {
        this.installation=installation; Text="Εγκατάσταση πραγματικού πιάνου"; AccessibleName=Text;
        Size=new(800,650); MinimumSize=new(620,480); StartPosition=FormStartPosition.CenterParent;
        Font=new("Segoe UI",11);
        var table=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new(16) };
        table.RowStyles.Add(new(SizeType.Percent,100)); table.RowStyles.Add(new(SizeType.AutoSize)); table.RowStyles.Add(new(SizeType.Absolute,30)); table.RowStyles.Add(new(SizeType.Absolute,40));table.RowStyles.Add(new(SizeType.AutoSize));
        table.Controls.Add(description); var source=new LinkLabel { Text="Πρωτότυπη πηγή και όροι χρήσης",AutoSize=true,AccessibleName="Άνοιγμα αρχικής πηγής Maestro και άδειας" };
        source.LinkClicked+=(_,_)=>{try{Process.Start(new ProcessStartInfo(MaestroLibrary.SourceUrl){UseShellExecute=true});}catch(Exception e){MessageBox.Show(this,e.Message,"Αδυναμία ανοίγματος πηγής");}};
        table.Controls.Add(source);table.Controls.Add(progress);table.Controls.Add(state);
        var buttons=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill}; buttons.Controls.AddRange([install,close]);table.Controls.Add(buttons);Controls.Add(table);
        install.Click+=async(_,_)=>await Install(); close.Click+=(_,_)=>CancelOrClose();
        FormClosing+=(_,e)=>{if(cancellation is not null){e.Cancel=true;closing=true;cancellation.Cancel();state.Text="Ακύρωση σε εξέλιξη…";}};
        AccessibilityContract.Apply(this);
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {if(keyData==Keys.Escape){CancelOrClose();return true;}return base.ProcessCmdKey(ref msg,keyData);}
    private void CancelOrClose(){if(cancellation is not null){closing=true;cancellation.Cancel();state.Text="Ακύρωση σε εξέλιξη…";}else Close();}
    private async Task Install()
    {
        if(cancellation is not null)return;
        using var source=new CancellationTokenSource();cancellation=source;install.Enabled=false;
        var temporary=installation+".install-"+Guid.NewGuid().ToString("N"); var previous=installation+".previous-"+Guid.NewGuid().ToString("N");
        var lastAnnounced=-1;
        var updates=new Progress<PianoInstallProgress>(p=>{
            if(IsDisposed)return;progress.Value=Math.Clamp(p.Percent,0,100);state.Text=p.Message;
            if(p.Percent/10!=lastAnnounced){lastAnnounced=p.Percent/10;state.AccessibilityObject.RaiseAutomationNotification(System.Windows.Forms.Automation.AutomationNotificationKind.Other,System.Windows.Forms.Automation.AutomationNotificationProcessing.MostRecent,p.Message);}
        });
        try
        {
            Directory.CreateDirectory(temporary);
            var archive=Path.Combine(temporary,"original.zip");
            using(var http=new HttpClient {Timeout=TimeSpan.FromMinutes(30)})await MaestroLibrary.DownloadAsync(http,archive,updates,source.Token);
            var extracted=Path.Combine(temporary,"source");var prepared=Path.Combine(temporary,"prepared");
            await Task.Run(()=>{
                MaestroLibrary.VerifyAndExtract(archive,extracted,source.Token);
                MaestroLibrary.Prepare(extracted,prepared,Decode,updates,source.Token);
            },source.Token);
            source.Token.ThrowIfCancellationRequested();
            if(Directory.Exists(installation))Directory.Move(installation,previous);
            try{Directory.Move(prepared,installation);}catch{if(Directory.Exists(previous))Directory.Move(previous,installation);throw;}
            InstalledManifest=Path.Combine(installation,MaestroLibrary.ManifestName);
            progress.Value=100;state.Text="Το πιάνο εγκαταστάθηκε. Πάτησε Κλείσιμο και Ctrl+N για άσκηση.";close.Text="&Κλείσιμο";
            state.AccessibilityObject.RaiseAutomationNotification(System.Windows.Forms.Automation.AutomationNotificationKind.ActionCompleted,System.Windows.Forms.Automation.AutomationNotificationProcessing.ImportantMostRecent,state.Text);
            if(Directory.Exists(previous))Directory.Delete(previous,true);
        }
        catch(OperationCanceledException){state.Text="Η εγκατάσταση ακυρώθηκε. Το προηγούμενο πιάνο διατηρήθηκε.";}
        catch(Exception ex){state.Text="Αποτυχία εγκατάστασης: "+ex.Message;MessageBox.Show(this,state.Text,"Εγκατάσταση πιάνου",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        finally
        {
            cancellation=null;install.Enabled=true;
            try{if(Directory.Exists(temporary))Directory.Delete(temporary,true);}catch(IOException){}catch(UnauthorizedAccessException){}
            if(closing)Close();
        }
    }
    private static void Decode(string input,string output,CancellationToken token)
    {
        // Windows 11's Media Foundation decodes FLAC locally. No codec download, shell, or lossy re-encoding.
        using var reader=new MediaFoundationReader(input);
        if(reader.WaveFormat.Channels!=2||reader.WaveFormat.BitsPerSample!=16||reader.WaveFormat.SampleRate!=44100)throw new InvalidDataException("Μη αναμενόμενη μορφή πηγαίου δείγματος. Απαιτείται stereo 44.1 kHz PCM16.");
        using var writer=new WaveFileWriter(output,reader.WaveFormat);
        var buffer=new byte[65536];int read;
        while((read=reader.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();writer.Write(buffer,0,read);}
    }
}
