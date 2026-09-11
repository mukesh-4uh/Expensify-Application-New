using ExpensifyApp.Pages;
using ExpensifyApp.Services;
using Microsoft.Maui.Networking;

namespace ExpensifyApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage = new NavigationPage(new LoginPage());

            // When network is back on, automatically run background Google Sheets replication
            Connectivity.Current.ConnectivityChanged += (s, e) =>
            {
                if (e.NetworkAccess == NetworkAccess.Internet && GoogleAuthAndBackupService.IsSignedIn)
                {
                    GoogleAuthAndBackupService.TriggerDataReplication();
                }
            };
        }

        //protected override Window CreateWindow(IActivationState? activationState)
        //{
        //    return new Window(new AppShell());
        //}
    }
}