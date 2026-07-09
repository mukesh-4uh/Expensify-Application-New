using Android.App;
using Android.Content.PM;
using Android.OS;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

namespace ExpensifyApp
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            // 👇 ADD THIS LINE
            CrossFingerprint.SetCurrentActivityResolver(() => this);

            base.OnCreate(savedInstanceState);
        }
    }
}
