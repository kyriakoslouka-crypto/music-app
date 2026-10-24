using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Automation;

// Runs with the supported .NET WindowsDesktop client, independently of PowerShell's assembly resolver.
var expected = new[] { "Ρυθμίσεις", "Εξάσκηση", "Πρόοδος", "Εξετάσεις και προσαρμογή", "Οδηγίες" };
if(args[0]=="--fixture")
{
    var thread=new Thread(()=>{
        System.Windows.Forms.Application.EnableVisualStyles();
        var form=new System.Windows.Forms.Form{Text="UIA fixture",Width=1000,Height=800,Font=new System.Drawing.Font("Segoe UI",11)};
        System.Windows.Forms.TabControl tabs=args[1]=="explicit"?new Studio.Windows.AccessibleTabControl():new System.Windows.Forms.TabControl();tabs.Dock=System.Windows.Forms.DockStyle.Fill;
        foreach(var name in expected){var page=new System.Windows.Forms.TabPage(name);page.Controls.Add(new System.Windows.Forms.TextBox{Text=name,Dock=System.Windows.Forms.DockStyle.Fill,AccessibleName=name});tabs.TabPages.Add(page);}
        form.Controls.Add(tabs);
        if(args[1]!="baseline")Studio.Windows.AccessibilityContract.Apply(form);
        System.Windows.Forms.Application.Run(form);
    });
    thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();return 0;
}
var fixtureRecords=new List<object>();
foreach(var mode in new[]{"baseline","contract","explicit"})
{
    using var fixture=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--fixture "+mode){UseShellExecute=false})!;
    try{
        AutomationElement? fixtureWindow=null;
        for(var i=0;i<100&&fixtureWindow is null;i++){Thread.Sleep(100);fixtureWindow=AutomationElement.RootElement.FindFirst(TreeScope.Children,new PropertyCondition(AutomationElement.ProcessIdProperty,fixture.Id));}
        if(fixtureWindow is null)throw new Exception("Fixture failed to open");
        Thread.Sleep(200);
        var fixtureTabs=fixtureWindow.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.TabItem));
        fixtureRecords.Add(new{Mode=mode,Count=fixtureTabs.Count,Names=Enumerable.Range(0,fixtureTabs.Count).Select(i=>fixtureTabs[i].Current.Name).ToArray()});
    }finally{if(!fixture.HasExited){fixture.CloseMainWindow();if(!fixture.WaitForExit(2000))fixture.Kill();}}
}
using var process = Process.Start(new ProcessStartInfo(args[0], "--uia-test-mode") { UseShellExecute = false })!;
var records = new List<object>(fixtureRecords);
try
{
    var windowCondition = new AndCondition(
        new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id),
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window),
        new PropertyCondition(AutomationElement.IsOffscreenProperty, false));
    AutomationElement? window = null;
    for (var i = 0; i < 100 && window is null; i++)
    {
        Thread.Sleep(100);
        window = AutomationElement.RootElement.FindFirst(TreeScope.Children, windowCondition);
    }
    if (window is null) throw new Exception("No visible application window");
    var tabs = window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));
    var names = Enumerable.Range(0, tabs.Count).Select(i => tabs[i].Current.Name).ToArray();
    records.Add(new { Client = typeof(AutomationElement).Assembly.Location, Count = tabs.Count, Names = names });
    if (!names.SequenceEqual(expected)) throw new Exception("External .NET WindowsDesktop UIA client did not find exactly five correctly ordered tabs");
    foreach (AutomationElement tab in tabs)
    {
        var selection = (SelectionItemPattern)tab.GetCurrentPattern(SelectionItemPattern.Pattern);
        selection.Select();
        if (!selection.Current.IsSelected) throw new Exception("Cannot select " + tab.Current.Name);
        records.Add(new { Selected = tab.Current.Name });
    }
    File.WriteAllText(args[1], JsonSerializer.Serialize(new { Passed = true, Records = records }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception ex)
{
    File.WriteAllText(args[1], JsonSerializer.Serialize(new { Passed = false, Error = ex.Message, Records = records }, new JsonSerializerOptions { WriteIndented = true }));
    Console.Error.WriteLine(ex);
    return 1;
}
finally
{
    if (!process.HasExited) { process.CloseMainWindow(); if (!process.WaitForExit(3000)) process.Kill(); }
}
