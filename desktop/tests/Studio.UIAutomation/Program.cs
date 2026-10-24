using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Automation;

// Runs with the supported .NET WindowsDesktop client, independently of PowerShell's assembly resolver.
var expected = new[] { "Ρυθμίσεις", "Εξάσκηση", "Πρόοδος", "Εξετάσεις και προσαρμογή", "Οδηγίες" };
using var process = Process.Start(new ProcessStartInfo(args[0], "--uia-test-mode") { UseShellExecute = false })!;
var records = new List<object>();
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
