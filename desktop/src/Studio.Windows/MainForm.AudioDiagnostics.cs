namespace Studio.Windows;
public sealed partial class MainForm
{
    private string? lastAudioFailure;
    private static string AudioFailurePath=>Path.Combine(DataDirectory,"last-audio-error.txt");
    private void ShowAudioFailure(Exception error)
    {
        Stop();heard=false;submit.Enabled=false;
        lastAudioFailure="Αναφορά σφάλματος εξόδου ήχου\r\n"+DateTimeOffset.Now.ToString("g")+"\r\n"+
            $"Έκδοση {typeof(MainForm).Assembly.GetName().Version!.ToString(3)}\r\n"+
            (error is AudioPlaybackException playback?playback.DiagnosticText:error.ToString());
        if(!auditOnly&&!uiTestMode){
            try{File.WriteAllText(AudioFailurePath,lastAudioFailure);}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){lastAudioFailure+="\r\nΗ αναφορά δεν αποθηκεύτηκε στον δίσκο: "+ex.Message;}
        }
        status.ScrollBars=ScrollBars.Vertical;
        // Keep the full error in a real, keyboard-readable field, not only a modal dialog.
        status.Text=lastAudioFailure;tabs.SelectedIndex=1;status.Focus();status.Select(0,0);
        if(!auditOnly)status.AccessibilityObject.RaiseAutomationNotification(System.Windows.Forms.Automation.AutomationNotificationKind.Other,System.Windows.Forms.Automation.AutomationNotificationProcessing.ImportantMostRecent,
            "Η έξοδος ήχου απέτυχε. Η άσκηση διατηρήθηκε. Πάτησε F6 για ολόκληρη την αναφορά, χωρίς αναδυόμενο παράθυρο.");
    }
    private void ReadAudioFailure()
    {
        Stop();
        if(lastAudioFailure is null&&!auditOnly&&!uiTestMode){
            try{if(File.Exists(AudioFailurePath))lastAudioFailure=File.ReadAllText(AudioFailurePath);}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){lastAudioFailure="Δεν διαβάστηκε η αποθηκευμένη αναφορά: "+ex.Message;}
        }
        status.ScrollBars=ScrollBars.Vertical;status.Text=lastAudioFailure??"Δεν έχει καταγραφεί σφάλμα εξόδου ήχου. Ctrl+R για ακρόαση της τρέχουσας άσκησης.";
        tabs.SelectedIndex=1;status.Focus();status.Select(0,0);status.ScrollToCaret();
    }
    internal object AuditAudioDiagnostic()
    {
        var device=new OutputDevice("audit-device","Συσκευή δοκιμής WASAPI");
        var error=new AudioPlaybackException(device,new System.Runtime.InteropServices.COMException("Initial driver failure",unchecked((int)0x88890004)),new IOException("Compatibility attempt failure"));
        ShowAudioFailure(error);ReadAudioFailure();
        if(!status.ReadOnly||!status.Focused||(status.SelectionStart!=0||status.SelectionLength!=0)||!status.Text.Contains("0x88890004")||!status.Text.Contains(device.Name)||!status.Text.Contains("Ctrl+R")||playing||heard||submit.Enabled)
            throw new InvalidOperationException("Audio error is not fully keyboard-readable or playback state was not cleared.");
        return new{Passed=true,Shortcut="F6",ReadOnly=true,ReadingStartsAtFirstLine=true,DeviceAndOriginalErrorPreserved=true,ModalCopyRequired=false,PhysicalAudioTested=false};
    }
}
