using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using System;
using System.Threading.Tasks;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

namespace ExpensifyApp
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private static TaskCompletionSource<bool>? _currentAuthTcs;
        public static readonly int ConfirmDeviceCredentialRequestCode = 12345;



        protected override void OnCreate(Bundle savedInstanceState)
        {
            CrossFingerprint.SetCurrentActivityResolver(() => this);
            base.OnCreate(savedInstanceState);
        }

        public static Task<bool> StartConfirmDeviceCredentialAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            _currentAuthTcs = tcs;

            try
            {
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (activity == null)
                {
                    tcs.SetResult(false);
                    return tcs.Task;
                }

                var keyguardManager = (Android.App.KeyguardManager?)activity.GetSystemService(Context.KeyguardService);
                if (keyguardManager == null || !keyguardManager.IsKeyguardSecure)
                {
                    tcs.SetResult(false);
                    return tcs.Task;
                }

                var intent = keyguardManager.CreateConfirmDeviceCredentialIntent(
                    "Security Verification", 
                    "Confirm your PIN, pattern, or password to log in");

                if (intent != null)
                {
                    activity.StartActivityForResult(intent, ConfirmDeviceCredentialRequestCode);
                }
                else
                {
                    tcs.SetResult(false);
                }
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }

            return tcs.Task;
        }


        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == ConfirmDeviceCredentialRequestCode)
            {
                if (resultCode == Result.Ok)
                {
                    _currentAuthTcs?.TrySetResult(true);
                }
                else
                {
                    _currentAuthTcs?.TrySetResult(false);
                }
            }
        }

    }
}
