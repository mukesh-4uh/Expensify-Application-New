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
}
