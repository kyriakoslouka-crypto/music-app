using System.Text.Json;
namespace Studio.Windows;
public static class AccessibilityContract
{
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Auto)]
    private static extern nint SendMessage(nint window,uint message,nint wParam,nint lParam);
    public static object TabSnapshot(TabControl tabs)
    {
        var accessible=tabs.AccessibilityObject;
        return new{Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,WinForms=typeof(TabControl).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false).Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion,ManagedCount=tabs.TabPages.Count,NativeCount=(int)SendMessage(tabs.Handle,0x1304,0,0),AccessibleCount=accessible.GetChildCount(),
            Pages=tabs.TabPages.Cast<TabPage>().Select(p=>new{p.Text,p.Name,p.Visible,p.TabIndex}).ToArray(),
            Children=Enumerable.Range(0,Math.Max(0,accessible.GetChildCount())).Select(i=>new{Index=i,Name=accessible.GetChild(i)?.Name,Role=accessible.GetChild(i)?.Role.ToString(),State=accessible.GetChild(i)?.State.ToString(),Bounds=accessible.GetChild(i)?.Bounds.ToString(),Hash=accessible.GetChild(i)?.GetHashCode()}).ToArray()};
    }

    private static void DescribeControl(object? sender,QueryAccessibilityHelpEventArgs e)
    {if(sender is Control control)e.HelpString=control.AccessibleDescription;}
    public static void Apply(Control root)
    {
        if(string.IsNullOrWhiteSpace(root.Name))root.Name=root.GetType().Name;
        if(string.IsNullOrWhiteSpace(root.AccessibleName)) root.AccessibleName=root.Text.Replace("&","").Trim();
        if(root is Form || root is TabControl)root.AccessibleDescription="Χειρισμός με Tab, Shift+Tab, βέλη και πλήκτρα συντόμευσης. F1 για οδηγίες.";
        root.QueryAccessibilityHelp-=DescribeControl;
        if(!string.IsNullOrWhiteSpace(root.AccessibleDescription))root.QueryAccessibilityHelp+=DescribeControl;
        var index=0;
        foreach(Control child in root.Controls)
        {
            child.TabIndex=index++;
            if(root is NumericUpDown&&string.IsNullOrWhiteSpace(child.AccessibleDescription))child.AccessibleDescription=root.AccessibleDescription;
            if((child is Label && child is not LinkLabel)||child is Panel||child is TabPage)child.TabStop=false;
            if(child is LinkLabel)child.TabStop=true;
            if(string.IsNullOrWhiteSpace(child.Name))child.Name=(root.Name.Length==0?"studio":root.Name)+"_"+child.TabIndex;
            if(string.IsNullOrWhiteSpace(child.AccessibleName))child.AccessibleName=child.Text.Replace("&","").Trim();
            if(string.IsNullOrWhiteSpace(child.AccessibleName))child.AccessibleName=root.AccessibleName+" — στοιχείο "+(child.TabIndex+1);
            Apply(child);
        }
    }
    public static object[] Audit(Control root)
    {
        var entries=new List<object>();
        void Walk(Control c,string path)
        {
            c.CreateControl();var accessible=c.AccessibilityObject;
            if(c.TabStop&&c is not ContainerControl&&string.IsNullOrWhiteSpace(accessible.Name))throw new InvalidOperationException("Ανώνυμο προσβάσιμο στοιχείο: "+path);
            if(c.TabStop&&c is not ContainerControl&&!System.Text.RegularExpressions.Regex.IsMatch(accessible.Name??"",@"[\p{IsGreek}]"))throw new InvalidOperationException("Λείπει ελληνική προσβάσιμη ονομασία: "+path);
            if(c.Controls.Cast<Control>().GroupBy(child=>child.TabIndex).Any(g=>g.Count()>1))throw new InvalidOperationException("Μη μοναδική σειρά Tab: "+path);
            entries.Add(new{Path=path,Type=c.GetType().Name,Name=accessible.Name,Role=accessible.Role.ToString(),c.TabIndex,c.TabStop,c.Enabled});
            foreach(Control child in c.Controls)Walk(child,path+"/"+child.Name);
        }
        Walk(root,root.Name);return entries.ToArray();
    }
    public static void WriteAudit(string path,MainForm form)
    {
        var result=new Dictionary<string,object>();
        result["MainForm"]=Audit(form);
        if(form.IsIsokratisOnly){result["Isokratis"]=form.AuditIson();result["ApplicationSplit"]=form.AuditApplicationSplit();File.WriteAllText(path,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));return;}
        result["ApplicationSplit"]=form.AuditApplicationSplit();
        result["PresentationRegression"]=form.AuditPresentations();
        result["ExamBoundaryRegression"]=form.AuditExamBoundaries();
        result["AudioDiagnosticRegression"]=form.AuditAudioDiagnostic();
        result["AutomaticPracticeRegression"]=form.AuditAutomaticPractice();
        result["NewStudyFeatures"]=form.AuditNewStudy();
        using(var isokratis=new MainForm(true,true)){result["Isokratis"]=isokratis.AuditIson();}
        using var installer=new PianoSetupDialog(Path.Combine(Path.GetTempPath(),"studio-audit-unused"));
        result["PianoSetupDialog"]=Audit(installer);
        File.WriteAllText(path,JsonSerializer.Serialize(result,new JsonSerializerOptions {WriteIndented=true}));
    }
}
