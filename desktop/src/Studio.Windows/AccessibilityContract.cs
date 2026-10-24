using System.Text.Json;
namespace Studio.Windows;
public static class AccessibilityContract
{
    public static void Apply(Control root)
    {
        if(string.IsNullOrWhiteSpace(root.Name))root.Name=root.GetType().Name;
        if(string.IsNullOrWhiteSpace(root.AccessibleName)) root.AccessibleName=root.Text.Replace("&","").Trim();
        if(root is Form || root is TabControl)root.AccessibleDescription="Χειρισμός με Tab, Shift+Tab, βέλη και πλήκτρα συντόμευσης. F1 για οδηγίες.";
        var index=0;
        foreach(Control child in root.Controls)
        {
            child.TabIndex=index++;
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
        result["PresentationRegression"]=form.AuditPresentations();
        using var installer=new PianoSetupDialog(Path.Combine(Path.GetTempPath(),"studio-audit-unused"));
        result["PianoSetupDialog"]=Audit(installer);
        File.WriteAllText(path,JsonSerializer.Serialize(result,new JsonSerializerOptions {WriteIndented=true}));
    }
}
