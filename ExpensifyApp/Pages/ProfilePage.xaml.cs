using ExpensifyApp.DataBase;
using ExpensifyApp.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using CommunityToolkit.Maui.Views;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using Microsoft.Maui.Graphics;
using ExpensifyApp.Services;

namespace ExpensifyApp.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly ExpenseContext _db;

    public ProfilePage()
    {
        InitializeComponent();
        _db = new ExpenseContext();
        BindingContext = this;

        Appearing += async (s, e) => await LoadProfileData();
    }

    private async Task LoadProfileData()
    {
        try
        {
            profileNameLabel.Text = Preferences.Get("ProfileName", "Premium Member");
            
            var all = await _db.ExpenseTable.ToListAsync();
            totalTransactionsLabel.Text = all.Count.ToString();
            totalSpentLabel.Text = $"₹{all.Sum(e => e.Expenses):N0}";

            var profile = await _db.UserFinancialProfile.FirstOrDefaultAsync();
            if (profile != null)
            {
                dashboardModeSubtitle.Text = $"Current: {profile.DashboardMode}";
                incomeSubtitle.Text = $"Current: ₹{profile.MonthlyIncome:N0}";
            }

            UpdateGoogleSyncUI();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnEditNameTapped(object sender, EventArgs e)
    {
        string currentName = Preferences.Get("ProfileName", "Premium Member");
        string newName = await DisplayPromptAsync("Edit Profile", "Enter your name:", initialValue: currentName);
        
        if (!string.IsNullOrWhiteSpace(newName))
        {
            Preferences.Set("ProfileName", newName);
            profileNameLabel.Text = newName;
            await UIHelper.ShowToastMessage("Profile name updated.");
        }
    }

    private async void OnSetBudgetTapped(object sender, EventArgs e)
    {
        string result = await DisplayPromptAsync("Set Budget", "Enter your monthly target budget (₹):", keyboard: Keyboard.Numeric);
        if (!string.IsNullOrWhiteSpace(result) && int.TryParse(result, out int newBudget) && newBudget > 0)
        {
            var budgetRecord = await _db.BudgetTable.FirstOrDefaultAsync();
            if (budgetRecord != null)
            {
                budgetRecord.Amount = newBudget;
                _db.BudgetTable.Update(budgetRecord);
            }
            else
            {
                _db.BudgetTable.Add(new BudgetTable { Amount = newBudget });
            }
            await _db.SaveChangesAsync();
            await DisplayAlert("Success", $"Monthly budget set to ₹{newBudget:N0}", "OK");
        }
    }

    private async void OnManageRecurringTapped(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new ManageRecurringPage());
        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
        }
    }

    private async void OnExportTapped(object sender, EventArgs e)
    {
        try
        {
            string action = await DisplayActionSheet("Select Export Period", "Cancel", null, "This Month", "This Year", "All Time");
            if (string.IsNullOrEmpty(action) || action == "Cancel") return;

            var all = await _db.ExpenseTable.ToListAsync();
            
            var filtered = action switch
            {
                "This Month" => all.Where(x => x.Date.Month == DateTime.Today.Month && x.Date.Year == DateTime.Today.Year).ToList(),
                "This Year" => all.Where(x => x.Date.Year == DateTime.Today.Year).ToList(),
                _ => all
            };

            if (!filtered.Any())
            {
                await DisplayAlert("Export", $"No transaction records found for {action}.", "OK");
                return;
            }

            await UIHelper.ShowToastMessage("Generating PDF report...");
            
            // Generate PDF
            using PdfDocument document = new PdfDocument();
            PdfPage page = document.Pages.Add();
            PdfGraphics graphics = page.Graphics;

            PdfFont titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 20, PdfFontStyle.Bold);
            PdfFont textFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12);

            graphics.DrawString($"Expense Report ({action})", titleFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(0, 0));
            graphics.DrawString($"Generated: {DateTime.Now:MMM dd, yyyy HH:mm}", textFont, PdfBrushes.Gray, new Syncfusion.Drawing.PointF(0, 30));

            PdfGrid pdfGrid = new PdfGrid();
            pdfGrid.Columns.Add(4);
            pdfGrid.Headers.Add(1);
            
            PdfGridRow header = pdfGrid.Headers[0];
            header.Cells[0].Value = "Date";
            header.Cells[1].Value = "Category";
            header.Cells[2].Value = "Payment Mode";
            header.Cells[3].Value = "Amount (₹)";

            foreach (var exp in filtered.OrderByDescending(x => x.Date))
            {
                PdfGridRow row = pdfGrid.Rows.Add();
                row.Cells[0].Value = exp.Date.ToString("MMM dd, yyyy HH:mm");
                row.Cells[1].Value = exp.Category ?? "";
                row.Cells[2].Value = exp.PayMode ?? "";
                row.Cells[3].Value = exp.Expenses.ToString("N0");
            }

            pdfGrid.Draw(page, new Syncfusion.Drawing.PointF(0, 60));

            string fileName = $"ExpenseReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                document.Save(fileStream);
            }

            document.Close(true);

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Share Expense Report",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export Error", ex.Message, "OK");
        }
    }

    private async void OnClearTapped(object sender, EventArgs e)
    {
        try
        {
            bool confirm = await DisplayAlert("Reset Database", "Are you sure you want to clear ALL expense logs? This cannot be undone.", "Delete All", "Cancel");
            if (!confirm) return;

            var authRequest = new AuthenticationRequestConfiguration("Reset Database", "Authenticate to wipe all expense logs");
            var authResult = await CrossFingerprint.Current.AuthenticateAsync(authRequest);

            if (authResult.Authenticated)
            {
                var all = await _db.ExpenseTable.ToListAsync();
                if (all.Any())
                {
                    _db.ExpenseTable.RemoveRange(all);
                    await _db.SaveChangesAsync();
                }

                await UIHelper.ShowToastMessage("Database successfully reset.");
                await LoadProfileData();
            }
            else
            {
                await UIHelper.ShowToastMessage("Authentication failed. Database not reset.");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnChangeDashboardModeTapped(object sender, EventArgs e)
    {
        try
        {
            string mode = await DisplayActionSheet("Select Dashboard Mode", "Cancel", null, "Monthly budget", "Monthly commitments");
            if (string.IsNullOrEmpty(mode) || mode == "Cancel") return;

            var profile = await _db.UserFinancialProfile.FirstOrDefaultAsync();
            if (profile != null)
            {
                profile.DashboardMode = mode;
                _db.UserFinancialProfile.Update(profile);
                await _db.SaveChangesAsync();

                await UIHelper.ShowToastMessage("Dashboard mode updated!");
                await LoadProfileData();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnEditIncomeTapped(object sender, EventArgs e)
    {
        try
        {
            var profile = await _db.UserFinancialProfile.FirstOrDefaultAsync();
            if (profile == null) return;

            string currentVal = profile.MonthlyIncome.ToString();
            string result = await DisplayPromptAsync("Edit Income", "Enter your monthly salary/allowance (₹):", initialValue: currentVal, keyboard: Keyboard.Numeric);
            if (!string.IsNullOrWhiteSpace(result) && int.TryParse(result, out int income) && income > 0)
            {
                profile.MonthlyIncome = income;
                _db.UserFinancialProfile.Update(profile);

                // Update default budget limit in BudgetTable matching new income percentage
                int expensePct = profile.ExpensePercentage;
                int newBudget = (int)(income * (expensePct / 100.0));
                
                var budgetRecord = await _db.BudgetTable.FirstOrDefaultAsync();
                if (budgetRecord != null)
                {
                    budgetRecord.Amount = newBudget;
                    _db.BudgetTable.Update(budgetRecord);
                }
                else
                {
                    _db.BudgetTable.Add(new BudgetTable { Amount = newBudget });
                }

                await _db.SaveChangesAsync();

                await UIHelper.ShowToastMessage("Income updated successfully!");
                await LoadProfileData();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnSavingsPlannerTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SavingsPlannerPage());
    }

    private async void OnLendingTrackerTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new LendingRecoveryPage());
    }

    private async void OnBorrowedFromFriendsTapped(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new BorrowedFromFriendsPage());
        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
        }
    }


    private async void OnLogoutTapped(object sender, EventArgs e)
    {
        try
        {
            bool confirm = await DisplayAlert("Logout Confirmation", "Do you want to log out?", "Logout", "Cancel");
            if (confirm)
            {
                var login = new LoginPage();
                var root = Navigation.NavigationStack[0];
                Navigation.InsertPageBefore(login, root);
                NavigationPage.SetHasNavigationBar(login, false);
                await Navigation.PopToRootAsync(true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error during logout: " + ex.Message);
        }
    }

    private async void OnDashboardTapped(object sender, EventArgs e)
        => await AppNavigation.NavigateToTabAsync(Navigation, new DashboardPage());

    private async void OnHistoryTapped(object sender, EventArgs e)
        => await AppNavigation.NavigateToTabAsync(Navigation, new HistoryPage());

    private async void OnStatsTapped(object sender, EventArgs e)
        => await AppNavigation.NavigateToTabAsync(Navigation, new StatsPage());

    private async void addButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new MenuPage());
        }
        catch(Exception ex)
        {
            await UIHelper.HandleException(ex);
        }
    }

    private void UpdateGoogleSyncUI()
    {
        bool isConnected = GoogleAuthAndBackupService.IsSignedIn;

        if (isConnected)
        {
            string email = GoogleAuthAndBackupService.UserEmail;
            string name = GoogleAuthAndBackupService.UserName;
            string photoUrl = GoogleAuthAndBackupService.UserPhotoUrl;

            profileNameLabel.Text = name;
            profileEmailLabel.Text = email;

            // Show Google Account Initial & Photo with Google Badge
            string initial = !string.IsNullOrWhiteSpace(name) ? name.Substring(0, 1).ToUpper() : "G";
            avatarInitialLabel.Text = initial;
            avatarInitialLabel.IsVisible = true;
            defaultAvatarLabel.IsVisible = false;
            googleBadgeBorder.IsVisible = true;

            if (!string.IsNullOrWhiteSpace(photoUrl))
            {
                profileAvatarImage.Source = new UriImageSource
                {
                    Uri = new Uri(photoUrl),
                    CachingEnabled = true,
                    CacheValidity = TimeSpan.FromDays(7)
                };
                profileAvatarImage.IsVisible = true;
            }
            else
            {
                profileAvatarImage.IsVisible = false;
            }

            googleStatusTitleLabel.Text = "Google Sheets Connected 🟢";
            googleStatusSubtitleLabel.Text = $"{email}\nLast Backup: {GoogleAuthAndBackupService.LastBackupDisplay}";
            googleStatusBadge.BackgroundColor = Color.FromArgb("#E8F5E9");
            googleStatusBadgeLabel.Text = "Connected";
            googleStatusBadgeLabel.TextColor = Color.FromArgb("#2E7D32");

            googleSignInButton.IsVisible = false;
            googleBackupNowButton.IsVisible = true;
            googleLocateSheetButton.IsVisible = true;
            googleDisconnectButton.IsVisible = true;
        }
        else
        {
            profileNameLabel.Text = Preferences.Get("ProfileName", "Premium Member");
            profileEmailLabel.Text = "Offline Profile";
            profileAvatarImage.IsVisible = false;
            avatarInitialLabel.IsVisible = false;
            defaultAvatarLabel.IsVisible = true;
            googleBadgeBorder.IsVisible = false;

            googleStatusTitleLabel.Text = "Google Sheets Backup";
            googleStatusSubtitleLabel.Text = "Sign in to back up data to your Google Sheet";
            googleStatusBadge.BackgroundColor = Color.FromArgb("#F0F2F5");
            googleStatusBadgeLabel.Text = "Not Connected";
            googleStatusBadgeLabel.TextColor = Color.FromArgb("#8A94A6");

            googleSignInButton.IsVisible = true;
            googleBackupNowButton.IsVisible = false;
            googleLocateSheetButton.IsVisible = false;
            googleDisconnectButton.IsVisible = false;
        }
    }

    private async void OnGoogleLocateSheetClicked(object sender, EventArgs e)
    {
        try
        {
            // If we have a live Google Spreadsheet, offer to open it directly
            string spreadsheetId = GoogleAuthAndBackupService.StoredSpreadsheetId;
            if (!string.IsNullOrWhiteSpace(spreadsheetId))
            {
                string sheetsUrl = GoogleDirectSheetsApiService.GetSpreadsheetWebUrl(spreadsheetId);
                string choice = await DisplayActionSheet(
                    $"📊 Your Expensify Master Workbook\nAccount: {GoogleAuthAndBackupService.UserEmail}",
                    "Cancel",
                    null,
                    "🌐 Open in Google Sheets (Online)",
                    "📋 Copy Spreadsheet Link",
                    "📤 Share Spreadsheet Link",
                    "📄 Open Local Backup File");

                if (choice == "🌐 Open in Google Sheets (Online)")
                {
                    await Browser.Default.OpenAsync(sheetsUrl, BrowserLaunchMode.SystemPreferred);
                }
                else if (choice == "📋 Copy Spreadsheet Link")
                {
                    await Clipboard.Default.SetTextAsync(sheetsUrl);
                    await UIHelper.ShowToastMessage("Spreadsheet link copied to clipboard!");
                }
                else if (choice == "📤 Share Spreadsheet Link")
                {
                    await Share.Default.RequestAsync(new ShareTextRequest
                    {
                        Title = "Share Expensify Google Sheet",
                        Text = sheetsUrl
                    });
                }
                else if (choice == "📄 Open Local Backup File")
                {
                    await OpenLocalBackupFileAsync();
                }
                return;
            }

            // No live sheet yet — show local backup options
            await OpenLocalBackupFileAsync();
        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
        }
    }

    private async Task OpenLocalBackupFileAsync()
    {
        string path = GoogleAuthAndBackupService.GetMasterBackupFilePath();
        if (!File.Exists(path))
        {
            var backupRes = await GoogleAuthAndBackupService.BackupNowAsync(_db);
            path = backupRes.LocalBackupPath;
        }

        var fileInfo = new FileInfo(path);
        string fileSize = fileInfo.Exists ? $"{fileInfo.Length / 1024.0:F1} KB" : "0 KB";
        string modified = fileInfo.Exists ? fileInfo.LastWriteTime.ToString("dd MMM yyyy, hh:mm tt") : "Not yet generated";

        string choice = await DisplayActionSheet(
            $"📄 Local Backup: {Path.GetFileName(path)} ({fileSize})\nLast Synced: {modified}",
            "Cancel",
            null,
            "📊 Open in Google Sheets / Excel",
            "📋 Copy File Path",
            "📤 Share Workbook to Drive / Apps");

        if (choice == "📊 Open in Google Sheets / Excel" || choice == "📤 Share Workbook to Drive / Apps")
        {
            await GoogleAuthAndBackupService.ShareToGoogleDriveOrSheetsAsync(path);
        }
        else if (choice == "📋 Copy File Path")
        {
            await Clipboard.Default.SetTextAsync(path);
            await UIHelper.ShowToastMessage("File path copied to clipboard!");
        }
    }


    private async void OnGoogleBackupNowClicked(object sender, EventArgs e)
    {
        try
        {
            if (!GoogleAuthAndBackupService.IsSignedIn)
            {
                await DisplayAlert("Google Backup", "Please connect your Google account first.", "OK");
                return;
            }

            // If no access token yet, prompt user to grant Google Sheets permission first
            if (string.IsNullOrWhiteSpace(GoogleAuthAndBackupService.StoredAccessToken))
            {
                bool grantAccess = await DisplayAlert(
                    "Grant Google Sheets Access",
                    $"To sync directly to your Google Sheet, Expensify needs permission to create and write to a spreadsheet in your Google Drive.\n\nThis will open a Google sign-in page. Please sign in as {GoogleAuthAndBackupService.UserEmail} and allow access.",
                    "Grant Access",
                    "Use Local Backup Only");

                if (grantAccess)
                {
                    string? token = await GoogleAuthAndBackupService.ObtainAccessTokenAsync();
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        await UIHelper.ShowToastMessage("⚠️ Access not granted. Using local backup.");
                    }
                }
            }

            googleBackupNowButton.IsEnabled = false;
            googleBackupNowButton.Text = "⏳ Backing up to Google Sheets...";

            var result = await GoogleAuthAndBackupService.BackupNowAsync(_db);

            UpdateGoogleSyncUI();

            googleBackupNowButton.IsEnabled = true;
            googleBackupNowButton.Text = "📊 Backup to Google Sheets Now";

            if (result.Success)
            {
                // If synced to a live sheet, offer to open it directly
                string spreadsheetId = GoogleAuthAndBackupService.StoredSpreadsheetId;
                if (!string.IsNullOrWhiteSpace(spreadsheetId))
                {
                    string sheetsUrl = GoogleDirectSheetsApiService.GetSpreadsheetWebUrl(spreadsheetId);
                    bool openSheet = await DisplayAlert(
                        "✅ Synced to Google Sheets",
                        $"{result.Message}\n\nYour data is live in your Google Sheet.\nWould you like to open it now?",
                        "Open Google Sheet",
                        "Done");

                    if (openSheet)
                        await Browser.Default.OpenAsync(sheetsUrl, BrowserLaunchMode.SystemPreferred);
                }
                else
                {
                    bool openShare = await DisplayAlert(
                        "Google Sheets Backup",
                        $"{result.Message}\n\nWould you like to open or save this workbook in Google Drive / Google Sheets now?",
                        "Open in Google Sheets/Drive",
                        "Done");

                    if (openShare)
                        await GoogleAuthAndBackupService.ShareToGoogleDriveOrSheetsAsync(result.LocalBackupPath);
                }
            }
            else
            {
                await DisplayAlert("Backup Notice", result.Message, "OK");
            }
        }
        catch (Exception ex)
        {
            googleBackupNowButton.IsEnabled = true;
            googleBackupNowButton.Text = "📊 Backup to Google Sheets Now";
            await UIHelper.HandleException(ex);
        }
    }

    private async void OnGoogleSignInClicked(object sender, EventArgs e)
    {
        try
        {
            string? selectedEmail = await GoogleAccountPickerService.PickGoogleAccountAsync();
            if (!string.IsNullOrWhiteSpace(selectedEmail))
            {
                string username = selectedEmail.Split('@')[0];
                string name = char.ToUpper(username[0]) + (username.Length > 1 ? username.Substring(1).ToLower() : "");
                string encodedEmail = Uri.EscapeDataString(selectedEmail.ToLowerInvariant());
                string encodedName = Uri.EscapeDataString(name);
                string avatarUrl = $"https://unavatar.io/{encodedEmail}?fallback=https://ui-avatars.com/api/?name={encodedName}%26background=0F9F99%26color=fff%26size=128";

                await GoogleAuthAndBackupService.SignInWithGoogleAsync(selectedEmail, name, avatarUrl);
                UpdateGoogleSyncUI();
                await UIHelper.ShowToastMessage($"Connected as {selectedEmail}");

                // Now request Google OAuth access so we can sync to their real Google Sheet
                bool grantAccess = await DisplayAlert(
                    "Grant Google Sheets Access",
                    $"To automatically back up your data to your personal Google Sheet,\n\nExpensify needs permission to create and write to a spreadsheet in your Google Drive.\n\nThis will open a Google sign-in page. Please sign in as {selectedEmail} and allow access.",
                    "Grant Access",
                    "Skip for Now");

                if (grantAccess)
                {
                    string? token = await GoogleAuthAndBackupService.ObtainAccessTokenAsync();
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        await UIHelper.ShowToastMessage("✅ Google Sheets access granted! Syncing now...");
                        GoogleAuthAndBackupService.TriggerDataReplication(_db);
                    }
                    else
                    {
                        await UIHelper.ShowToastMessage("⚠️ Access not granted. Local backup will still work.");
                    }
                }
                return;
            }

            var popup = new GoogleSignInPopup();
            await this.ShowPopupAsync(popup);
            UpdateGoogleSyncUI();
        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
        }
    }

    private async void OnGoogleDisconnectClicked(object sender, EventArgs e)
    {
        try
        {
            bool confirm = await DisplayAlert("Disconnect Google", "Are you sure you want to disconnect your Google account? Automatic Google Sheets backups will be paused.", "Disconnect", "Cancel");
            if (confirm)
            {
                await GoogleAuthAndBackupService.SignOutAsync();
                UpdateGoogleSyncUI();
                await UIHelper.ShowToastMessage("Google account disconnected.");
            }
        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
        }
    }
}
