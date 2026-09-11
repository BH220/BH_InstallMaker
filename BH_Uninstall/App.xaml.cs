using System.Windows;

namespace BH_Uninstall
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DiConfig.Configure();
            base.OnStartup(e);
        }
    }
}
