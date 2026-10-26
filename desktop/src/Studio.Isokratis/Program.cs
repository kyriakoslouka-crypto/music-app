using System.Security.Cryptography;
using System.Text;
using Studio.Windows;
namespace Studio.Isokratis;
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();System.Globalization.CultureInfo.CurrentUICulture=new("el-GR");
        var identity=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserDomainName+"\\"+Environment.UserName)));
        using var instance=new Mutex(true,"Local\\ProfessionalIsokratis-"+identity,out var first);
        if(!first){if(!args.Contains("--uia-test-mode"))MessageBox.Show("Ο ισοκράτης είναι ήδη ανοικτός. Alt+Tab για επιστροφή.","Professional Isokratis");Environment.ExitCode=1;return;}
        try{
            if(args.Length>=2&&args[0]=="--ison-live-check")Environment.ExitCode=IsokratisDiagnostics.RunLiveProbe(args[1]);
            else Application.Run(new MainForm(args.Contains("--accessibility-audit"),true));
        }finally{instance.ReleaseMutex();}
    }
}
