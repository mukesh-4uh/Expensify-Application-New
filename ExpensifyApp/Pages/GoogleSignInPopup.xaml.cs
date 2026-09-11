using CommunityToolkit.Maui.Views;
using ExpensifyApp.Helpers;
using ExpensifyApp.Services;
using System;
using System.Threading.Tasks;

namespace ExpensifyApp.Pages;

public partial class GoogleSignInPopup : Popup
{
    public GoogleSignInPopup()
    {
        InitializeComponent();
    }

    private void OnDontAskAgainTapped(object sender, EventArgs e)
    {
        dontAskAgainCheckBox.IsChecked = !dontAskAgainCheckBox.IsChecked;
    }

    private async void OnGoogleSignInTapped(object sender, EventArgs e)
    {
        try
        {
            // Animate card touch
            await googleSignInCard.ScaleTo(0.96, 70);
            await googleSignInCard.ScaleTo(1.0, 70);

            // Opens the native Android Google Account Chooser dialog
            string? selectedEmail = await GoogleAccountPickerService.PickGoogleAccountAsync();

            if (!string.IsNullOrWhiteSpace(selectedEmail))
            {
                await CompleteSignInWithEmail(selectedEmail);
            }
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("Google Sign-In", ex.Message, "OK");
        }
    }

    private async Task CompleteSignInWithEmail(string email)
    {
        string username = email.Split('@')[0];
        string name = FormatDisplayName(username);

        // Resolve actual Google account profile photo from the entered email address
        string encodedEmail = Uri.EscapeDataString(email.ToLowerInvariant());
        string encodedName = Uri.EscapeDataString(name);
        string avatarUrl = $"https://unavatar.io/{encodedEmail}?fallback=https://ui-avatars.com/api/?name={encodedName}%26background=0F9F99%26color=fff%26size=128";

        await GoogleAuthAndBackupService.SignInWithGoogleAsync(email, name, avatarUrl);

        await UIHelper.ShowToastMessage($"Connected with {email}!");

        Close(true);
    }

    private static string FormatDisplayName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return "Google User";

        string clean = rawName.Replace('.', ' ').Replace('_', ' ').Trim();
        var parts = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var capitalized = parts.Select(p => char.ToUpper(p[0]) + (p.Length > 1 ? p.Substring(1).ToLower() : ""));
        return string.Join(" ", capitalized);
    }

    private void OnMaybeLaterClicked(object sender, EventArgs e)
    {
        if (dontAskAgainCheckBox.IsChecked)
        {
            GoogleAuthAndBackupService.DismissPrompt(neverAskAgain: true);
        }
        else
        {
            GoogleAuthAndBackupService.DismissPrompt(neverAskAgain: false);
        }

        Close(false);
    }
}
