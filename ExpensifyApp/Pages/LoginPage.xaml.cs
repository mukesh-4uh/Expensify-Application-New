using CommunityToolkit.Maui.Converters;
using ExpensifyApp.DataBase;
using ExpensifyApp.Helpers;
using Microsoft.EntityFrameworkCore;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;


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
            await Task.Delay(1000);
            var result = await CheckAndRequestFolderPermission();

            if (result != PermissionStatus.Granted)
            {
                await UIHelper.ShowErrorMessage(
                    "Storage permission denied. Please enable 'Manage All Files' permission for the app to work properly.");

                await AppSettingsService.OpenManageAllFilesSettingsAsync();
                return;
            }
            await AppLoadActivityHelper.DoAppInitWork();
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
    private async void loginButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(phoneEntry.Text) || string.IsNullOrEmpty(passwordEntry.Text))
            {
                await UIHelper.ShowMessage("Enter Phone Number and Password");
                return;
            }

            await AuthenticateUserAsync();
        }
        catch(Exception ex)
        {
            await UIHelper.HandleException(ex);
        }
       
    }

    private async void fingerPrintButton_Clicked(object sender, EventArgs e)
    {
        //await AuthenticateUserAsync();
        bool hasTodayData = await _dbContext.HasAnyExpenseTodayAsync();

        await Task.Delay(200);

        if (hasTodayData)
        {
            await Navigation.PushAsync(new TodayPage());
        }
        else
        {
            await Navigation.PushAsync(new MenuPage());
        }
    }

    private async Task AuthenticateUserAsync()
    {
        try
        {
            bool available = await CrossFingerprint.Current.IsAvailableAsync(true);

            if (!available)
            {
                await UIHelper.ShowMessage("Security authentication (biometrics or phone lock credentials) is not available or configured on this device.");
                return;
            }

            var request = new AuthenticationRequestConfiguration(
                "Security Verification",
                "Confirm your fingerprint or device PIN/password to log in")
            {
                AllowAlternativeAuthentication = true
            };

            var result = await CrossFingerprint.Current.AuthenticateAsync(request);

            if (result.Authenticated)
            {
                bool hasTodayData = await _dbContext.HasAnyExpenseTodayAsync();

                await Task.Delay(200);

                if (hasTodayData)
                {
                    await Navigation.PushAsync(new TodayPage());
                }
                else
                {
                    await Navigation.PushAsync(new MenuPage());
                }
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
}
