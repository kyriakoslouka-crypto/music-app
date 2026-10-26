using System.Text.Json;
using Studio.Audio;
namespace Studio.Windows;
public sealed partial class MainForm
{
    private sealed record IsonOutputPreferences(string? DeviceId,bool CancelSpeech);
    private string IsonOutputPath=>Path.Combine(IsonDataDirectory,"audio-output.json");
    private void BuildIsonSettings()
    {
        var page=new AccessibleTabPage("Ρυθμίσεις ήχου");var table=SettingsLayout();
        Row(table,"Έξοδος WASAPI ή ASIO",devices);Row(table,"Ομιλία",cancelSpeech);
        var refresh=new Button{Text="Ανανέωση συσκευών ήχου",AutoSize=true};refresh.Click+=(_,_)=>RefreshDevices((devices.SelectedItem as OutputDevice)?.Id);Row(table,"Συσκευές",refresh);
        page.Controls.Add(table);tabs.TabPages.Add(page);
        devices.SelectedIndexChanged+=(_,_)=>SaveIsonOutput();cancelSpeech.CheckedChanged+=(_,_)=>SaveIsonOutput();
    }
    private void BuildIsonHelp()
    {
        helpPage=new AccessibleTabPage("Οδηγίες");helpControl=new(){ReadOnly=true,Multiline=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,AccessibleName="Οδηγίες ανεξάρτητου ισοκράτη",Text="Professional Isokratis — VERSION "+typeof(MainForm).Assembly.GetName().Version!.ToString(3)+"\r\n\r\nCtrl+L: έναρξη και ζωντανός χειρισμός. Πάνω/κάτω: φθόγγοι. Shift+πάνω/κάτω: οκτάβα. Δεξιά/αριστερά: φωνές. Ctrl+P: παύση/συνέχεια. Esc: διακοπή. Tab/Shift+Tab: πεδία. Ctrl+Tab: καρτέλες. Ctrl+Home: ρυθμίσεις ήχου. F1: οδηγίες. F6: πλήρης αναφορά τελευταίου σφάλματος ήχου.\r\n\r\nΟι οκτώ ήχοι έχουν χωριστά προσωπικά κουρδίσματα. Ελεύθερη πρόσθετη αλλοίωση ±7200 μορίων: μετακινεί μόνο τον επιλεγμένο ομώνυμο φθόγγο σε όλες τις οκτάβες. Η γενική μεταφορά και η διατονική αναφορά Νη επηρεάζουν όλους τους φθόγγους. Το Προσωπικό κούρδισμα περιλαμβάνει Εφαρμογή/Ακύρωση και Επαναφορά προτύπου. Τα βασικά επτά διαστήματα αθροίζουν 72 μόρια. Παραμένουν τα όρια 35–2000 Hz και η έκταση της φωνής.\r\n\r\nΟι παλιές φωνές και τα κουρδίσματα υιοθετούνται αυτόματα στην πρώτη εκκίνηση. Δεν μετακινούνται και δεν διαγράφονται τα αρχικά δεδομένα. Οι νέες ρυθμίσεις/εισαγωγές αποθηκεύονται στο LocalAppData\\ProfessionalIsokratis. Αν δεν υπάρχουν φωνές, Εισαγωγή ZIP φωνών μία φορά, με όλα τα μέρη μαζί. Δεν απαιτείται πιάνο Maestro. Οι ηχογραφήσεις δεν περιλαμβάνονται στον εγκαταστάτη.\r\n\r\nΔεν έχουν επιβεβαιωθεί από άνθρωπο η νέα διεπαφή NVDA και η ακουστική ποιότητα στον υπολογιστή σας."};helpPage.Controls.Add(helpControl);tabs.TabPages.Add(helpPage);
    }
    private void InitializeStandaloneIson()
    {
        loading=true;
        try{
            if(!uiTestMode){
                IsonMigration.Adopt(DataDirectory,IsonDataDirectory);
                var pref=File.Exists(IsonOutputPath)?JsonSerializer.Deserialize<IsonOutputPreferences>(File.ReadAllText(IsonOutputPath)):null;
                RefreshDevices(pref?.DeviceId);cancelSpeech.Checked=pref?.CancelSpeech??false;
            }
            InitializeIson();
        }catch(Exception e){isonStatus.Text="Δεν ολοκληρώθηκε η φόρτωση: "+e.Message+". Τα προηγούμενα δεδομένα δεν διαγράφηκαν.";isonLoaded=true;}
        finally{loading=false;}
    }
    private void SaveIsonOutput()
    {
        if(loading||auditOnly||uiTestMode||!IsIsokratisOnly)return;
        try{Directory.CreateDirectory(IsonDataDirectory);var temporary=IsonOutputPath+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(new IsonOutputPreferences((devices.SelectedItem as OutputDevice)?.Id,cancelSpeech.Checked)));File.Move(temporary,IsonOutputPath,true);}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){isonStatus.Text="Δεν αποθηκεύτηκε η έξοδος ήχου: "+e.Message;}
    }
    internal object AuditApplicationSplit()
    {
        var names=tabs.TabPages.Cast<TabPage>().Select(p=>p.Text).ToArray();
        string[] expected=IsIsokratisOnly?["Βυζαντινός ισοκράτης","Ρυθμίσεις ήχου","Οδηγίες"]:["Ρυθμίσεις","Εξάσκηση","Πρόοδος","Εξετάσεις και προσαρμογή","Οδηγίες"];
        if(!names.SequenceEqual(expected))throw new InvalidOperationException("Unexpected application sections");
        return new{Passed=true,IsokratisOnly=IsIsokratisOnly,TabNames=names,NoSurpriseTab=true,NoPianoSetup=IsIsokratisOnly};
    }
}
