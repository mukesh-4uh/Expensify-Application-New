using CommunityToolkit.Maui.Converters;
using CommunityToolkit.Maui.Views;
using ExpensifyApp.DataBase;
using ExpensifyApp.Helpers;
using ExpensifyApp.Services;
using Microsoft.EntityFrameworkCore;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;
using Microsoft.Maui.Graphics;

namespace ExpensifyApp.Pages;


public partial class LoginPage : ACSBasePage
{
    private readonly ExpenseContext _dbContext;
  
    public LoginPage()
	{
		InitializeComponent();
        _dbContext = new ExpenseContext();

    }

    protected async override void OnPageAppearing()
    {
        try
        {
            base.OnPageAppearing();
            
            // Request both folder and camera permissions immediately
            var result = await CheckAndRequestFolderPermission();
            try
            {
                await PermissionHelper.CheckAndRequestCameraPermission();
            }
            catch { }

            if (result != PermissionStatus.Granted)
            {
                await UIHelper.ShowErrorMessage(
                    "Storage permission denied. Please enable 'Manage All Files' permission for the app to work properly.");

                await AppSettingsService.OpenManageAllFilesSettingsAsync();
                return;
            }
            await AppLoadActivityHelper.DoAppInitWork();

           
            bool biometricAvailable = await CrossFingerprint.Current.IsAvailableAsync(false);
            if (!biometricAvailable)
            {               
                fingerPrintButton.IsVisible = false;
                dividerGrid.IsVisible = false;
        
                screenLockButton.BackgroundColor = Color.FromArgb("#10CFC9");
                screenLockButton.TextColor = Colors.White;
                screenLockButton.BorderWidth = 0;
                screenLockButton.Text = "Login with PIN / Pattern / Password";
            }
            else
            {
               
                fingerPrintButton.IsVisible = true;
                dividerGrid.IsVisible = true;
                
                screenLockButton.BackgroundColor = Colors.Transparent;
                screenLockButton.TextColor = Color.FromArgb("#10CFC9");
                screenLockButton.BorderColor = Color.FromArgb("#10CFC9");
                screenLockButton.BorderWidth = 2;
                screenLockButton.Text = "Use Screen Lock (PIN/Pattern)";
            }
        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
        }
    }


    public async Task<PermissionStatus> CheckAndRequestFolderPermission()
    {
        return await PermissionHelper.CheckAndRequestFileWritePermission();
    }


    public static class AppSettingsService
    {
        public static Task OpenManageAllFilesSettingsAsync()
        {
#if ANDROID
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        // Optional: Small delay to ensure dialog UI finishes
                        await Task.Delay(500);

                        var uri = Android.Net.Uri.Parse("package:" + AppInfo.PackageName);
                        var intent = new Android.Content.Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                        intent.SetData(uri);
                        intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                        Android.App.Application.Context.StartActivity(intent);
                    }
                    catch (Exception)
                    {
                        await UIHelper.ShowErrorMessage("Could not open settings. Please enable 'Manage All Files' manually.");
                    }
                });
#endif
            return Task.CompletedTask;
        }
    }
    private async void fingerPrintButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            var request = new AuthenticationRequestConfiguration(
                "Security Verification",
                "Place your fingerprint to log in")
            {
                AllowAlternativeAuthentication = false
            };

            var result = await CrossFingerprint.Current.AuthenticateAsync(request);

            if (result.Authenticated)
            {
                await NavigateToNextPageAsync();
            }
            else
            {
                await UIHelper.ShowMessage("Fingerprint authentication failed or was cancelled.");
            }
        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
        }
    }

    private async void screenLockButton_Clicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            var keyguardManager = activity?.GetSystemService(Android.Content.Context.KeyguardService) as Android.App.KeyguardManager;
            if (keyguardManager != null)
            {
                if (!keyguardManager.IsKeyguardSecure)
                {
                    await UIHelper.ShowMessage("No secure screen lock (PIN, Pattern, or Password) is configured on this device.");
                    return;
                }

                bool nativeAuthSuccess = await MainActivity.StartConfirmDeviceCredentialAsync();
                if (nativeAuthSuccess)
                {
                    await NavigateToNextPageAsync();
                    return;
                }
                else
                {
                    await UIHelper.ShowMessage("Authentication failed or cancelled.");
                    return;
                }
            }
#endif
          
            var request = new AuthenticationRequestConfiguration(
                "Security Verification",
                "Confirm your device PIN/Password/Pattern to log in")
            {
                AllowAlternativeAuthentication = true
            };

            var result = await CrossFingerprint.Current.AuthenticateAsync(request);

            if (result.Authenticated)
            {
                await NavigateToNextPageAsync();
            }
            else
            {
                await UIHelper.ShowMessage("Authentication failed or cancelled.");
            }
        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
        }
    }

    private async Task NavigateToNextPageAsync()
    {
        try
        {
            if (GoogleAuthAndBackupService.ShouldShowPostLoginPrompt())
            {
                var popup = new GoogleSignInPopup();
                await this.ShowPopupAsync(popup);
            }

            var profile = await _dbContext.UserFinancialProfile.FirstOrDefaultAsync();
            if (profile != null)
            {
                await Navigation.PushAsync(new DashboardPage());
            }
            else
            {
                await Navigation.PushAsync(new FinancialSetupPage());
            }
        }
        catch
        {
            await Navigation.PushAsync(new DashboardPage());
        }
    }
}
