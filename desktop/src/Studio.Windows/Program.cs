using System.Security.Cryptography;
using System.Text;
namespace Studio.Windows;
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        System.Globalization.CultureInfo.CurrentUICulture=new("el-GR");
        var audit=args.Contains("--accessibility-audit");
        var identity=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserDomainName+"\\"+Environment.UserName)));
        using var singleInstance=new Mutex(true,"Local\\ProfessionalEarTrainingStudio-"+identity,out var first);
        if(!first&&(audit||args.Contains("--uia-test-mode"))){Environment.ExitCode=1;return;}
        if(!first){MessageBox.Show("Η εφαρμογή είναι ήδη ανοικτή. Πήγαινε στο υπάρχον παράθυρο με Alt+Tab.","Professional Ear Training Studio");return;}
        try{if(args.Length>=3&&args[0]=="--automatic-practice-check")Environment.ExitCode=AutomaticPracticeProbe.Run(args[1],args[2]);else Application.Run(new MainForm(audit));}finally{singleInstance.ReleaseMutex();}
    }
}
