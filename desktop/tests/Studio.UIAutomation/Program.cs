using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Automation;

var expected = new[] { "Ρυθμίσεις", "Εξάσκηση", "Πρόοδος", "Εξετάσεις και προσαρμογή", "Οδηγίες" };
var content = new[] { "Εγκατάσταση ή επισκευή πραγματικού πιάνου Maestro Concert Grand", "Οδηγία τρέχουσας άσκησης", "Στατιστικά και ιστορικό", "Εκπαιδευτικό εξεταστικό προφίλ", "Οδηγίες και περιορισμοί έκδοσης" };
if (args[0] == "--fixture")
{
    var thread = new Thread(() => {
        System.Windows.Forms.Application.EnableVisualStyles();
        var form = new System.Windows.Forms.Form { Text = "UIA fixture", Width = 1000, Height = 800, Font = new System.Drawing.Font("Segoe UI", 11) };
        System.Windows.Forms.TabControl tabs = args[1] == "explicit" ? new Studio.Windows.AccessibleTabControl() : new System.Windows.Forms.TabControl();
        tabs.Dock = System.Windows.Forms.DockStyle.Fill;
        foreach (var name in expected) {
            System.Windows.Forms.TabPage page=args[1]=="explicit"?new Studio.Windows.AccessibleTabPage(name):new System.Windows.Forms.TabPage(name);
            page.Controls.Add(new System.Windows.Forms.TextBox { Text = name, Dock = System.Windows.Forms.DockStyle.Fill, AccessibleName = name });
            tabs.TabPages.Add(page);
        }
        form.Controls.Add(tabs);
        if (args[1] != "baseline") Studio.Windows.AccessibilityContract.Apply(form);
        System.Windows.Forms.Application.Run(form);
    });
    thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); return 0;
}
var references = new List<object>();
var checks = new List<(string Name, bool Passed)>();
Process? application = null;
string? error = null;
void Check(bool passed, string name) { checks.Add((name, passed)); if (!passed) throw new Exception(name); }
AutomationElement FindWindow(Process process) {
    var condition = new AndCondition(new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id),
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window), new PropertyCondition(AutomationElement.IsOffscreenProperty, false));
    for (var i = 0; i < 100; i++) {
        var result = AutomationElement.RootElement.FindFirst(TreeScope.Children, condition);
        if (result is not null) return result;
        if (process.HasExited) break;
        Thread.Sleep(100);
    }
    throw new Exception("No visible application window");
}
AutomationElementCollection Tabs(AutomationElement window) => window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));
string[] Names(AutomationElementCollection tabs) => Enumerable.Range(0, tabs.Count).Select(i => tabs[i].Current.Name).ToArray();
void Close(Process? process) { if (process is not null && !process.HasExited) { process.CloseMainWindow(); if (!process.WaitForExit(3000)) process.Kill(); } }
try
{
    foreach (var mode in new[] { "baseline", "contract", "explicit" }) {
        using var fixture = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--fixture " + mode) { UseShellExecute = false })!;
        try {
            var window = FindWindow(fixture); Thread.Sleep(200);
            var tabs = Tabs(window); var names = Names(tabs);
            var parents=Enumerable.Range(0,tabs.Count).Select(i=>{
                var chain=new List<string>();var node=tabs[i];
                for(var depth=0;depth<5&&node is not null;depth++){chain.Add(node.Current.ControlType.ProgrammaticName+":"+node.Current.Name+":"+string.Join(".",node.GetRuntimeId()));node=TreeWalker.RawViewWalker.GetParent(node);}
                return chain.ToArray();
            }).ToArray();
            references.Add(new { Mode = mode, ReferenceOnly = mode != "explicit", Count = tabs.Count, Names = names,Parents=parents });
            if (mode == "explicit") Check(names.SequenceEqual(expected), "Explicit provider exposes exactly five named fixture headers");
        } finally { Close(fixture); }
    }
    application = Process.Start(new ProcessStartInfo(args[0], "--uia-test-mode") { UseShellExecute = false })!;
    var appWindow = FindWindow(application);
    var appTabs = Tabs(appWindow);
    Check(Names(appTabs).SequenceEqual(expected), "Application exposes exactly five correctly ordered named tabs");
    Check(Enumerable.Range(0, appTabs.Count).Select(i => appTabs[i].Current.AutomationId).Distinct().Count() == 5, "All five headers have unique automation identifiers");
    for (var i = 0; i < appTabs.Count; i++) {
        var selection = (SelectionItemPattern)appTabs[i].GetCurrentPattern(SelectionItemPattern.Pattern);
        selection.Select();
        Check(selection.Current.IsSelected, "SelectionItem selects " + expected[i]);
        var sentinel = appWindow.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, content[i]));
        Check(sentinel is not null, "Native page content remains exposed: " + expected[i]);
        if(i is 1 or 2 or 4)Check(sentinel!.TryGetCurrentPattern(TextPattern.Pattern,out _),"Native TextPattern retained: "+expected[i]);
        if(i==3)Check(sentinel!.TryGetCurrentPattern(SelectionPattern.Pattern,out _),"Native ComboBox SelectionPattern retained");
    }
}
catch (Exception ex) {
    error = ex.ToString();
    if (checks.All(c => c.Passed)) checks.Add(("Harness completed without unexpected errors", false));
    Console.Error.WriteLine(ex);
}
finally { Close(application); application?.Dispose(); }
File.WriteAllText(args[1], JsonSerializer.Serialize(new {
    Passed = error is null, Executed = checks.Count, Failed = checks.Count(c => !c.Passed), Error = error,
    Client = typeof(AutomationElement).Assembly.Location, References = references,
    Checks = checks.Select(c => new { c.Name, c.Passed }).ToArray()
}, new JsonSerializerOptions { WriteIndented = true }));
return error is null ? 0 : 1;
