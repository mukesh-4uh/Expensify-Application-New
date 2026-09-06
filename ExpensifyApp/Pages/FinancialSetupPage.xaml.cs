using ExpensifyApp.DataBase;
using ExpensifyApp.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ExpensifyApp.Pages;

public partial class FinancialSetupPage : ContentPage
{
    private readonly ExpenseContext _db;
    private string _selectedDashboardMode = "Monthly budget"; // Default mode

    public FinancialSetupPage()
    {
        InitializeComponent();
        _db = new ExpenseContext();

        // Highlight Budget Tracking by default
        HighlightDashboardMode();
    }

    private void HighlightDashboardMode()
    {
        if (_selectedDashboardMode == "Monthly budget")
        {
            budgetModeCard.Stroke = Color.FromArgb("#0D8C87");
            budgetModeCard.StrokeThickness = 2.5;
            commitmentsModeCard.Stroke = Color.FromArgb("#E5E7EB");
            commitmentsModeCard.StrokeThickness = 1.5;

            amountTitleLabel.Text = "Enter Monthly Budget Target (₹)";
            amountEntry.Placeholder = "e.g. 20000";
        }
        else
        {
            commitmentsModeCard.Stroke = Color.FromArgb("#0D8C87");
            commitmentsModeCard.StrokeThickness = 2.5;
            budgetModeCard.Stroke = Color.FromArgb("#E5E7EB");
            budgetModeCard.StrokeThickness = 1.5;

            amountTitleLabel.Text = "Enter Monthly Salary / Income (₹)";
            amountEntry.Placeholder = "e.g. 45000";
        }
    }

    private void OnSelectBudgetMode(object sender, EventArgs e)
    {
        _selectedDashboardMode = "Monthly budget";
        HighlightDashboardMode();
    }

    private void OnSelectCommitmentsMode(object sender, EventArgs e)
    {
        _selectedDashboardMode = "Monthly commitments";
        HighlightDashboardMode();
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        errorLabel.IsVisible = false;

        string amountStr = amountEntry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(amountStr) || !int.TryParse(amountStr, out int amountVal) || amountVal <= 0)
        {
            errorLabel.Text = "❌ Please enter a valid positive amount.";
            errorLabel.IsVisible = true;
            return;
        }

        try
        {
            // Retrieve or create UserFinancialProfile record
            var profile = await _db.UserFinancialProfile.FirstOrDefaultAsync();
            if (profile == null)
            {
                profile = new UserFinancialProfile
                {
                    CreatedDate = DateTime.Now
                };
                _db.UserFinancialProfile.Add(profile);
            }

            profile.DashboardMode = _selectedDashboardMode;
            profile.ProfileType = "Other";
            profile.SavingsMode = "50/30/20"; // Standard baseline strategy
            profile.CommitmentPercentage = 50;
            profile.SavingsPercentage = 30;
            profile.ExpensePercentage = 20;

            if (_selectedDashboardMode == "Monthly budget")
            {
                // Under Monthly budget mode, Income defaults to double budget or 0
                profile.MonthlyIncome = amountVal * 2; 

                // Add or update BudgetTable target
                var budgetRec = await _db.BudgetTable.FirstOrDefaultAsync();
                if (budgetRec == null)
                {
                    budgetRec = new BudgetTable { Amount = amountVal };
                    _db.BudgetTable.Add(budgetRec);
                }
                else
                {
                    budgetRec.Amount = amountVal;
                    _db.BudgetTable.Update(budgetRec);
                }
            }
            else
            {
                // Under Monthly commitments mode, Amount is Monthly Income/Salary
                profile.MonthlyIncome = amountVal;

                // Add or update BudgetTable target to baseline 50% of monthly salary
                int baselineBudget = (int)(amountVal * 0.5);
                var budgetRec = await _db.BudgetTable.FirstOrDefaultAsync();
                if (budgetRec == null)
                {
                    budgetRec = new BudgetTable { Amount = baselineBudget };
                    _db.BudgetTable.Add(budgetRec);
                }
                else
                {
                    budgetRec.Amount = baselineBudget;
                    _db.BudgetTable.Update(budgetRec);
                }
            }

            // Create default Emergency Fund goal if none exist
            var existingGoals = await _db.SavingsGoal.AnyAsync();
            if (!existingGoals)
            {
                int defaultTarget = profile.DashboardMode == "Monthly budget" ? amountVal * 3 : (int)(profile.MonthlyIncome * 0.3 * 6);
                if (defaultTarget <= 0) defaultTarget = 50000;

                _db.SavingsGoal.Add(new SavingsGoal
                {
                    Name = "Emergency Fund",
                    TargetAmount = defaultTarget,
                    CurrentAmount = 0,
                    AllocationPercentage = 100,
                    Deadline = DateTime.Today.AddMonths(12),
                    CategoryIcon = "🚨",
                    IsCompleted = false
                });
            }

            await _db.SaveChangesAsync();

            await UIHelper.ShowToastMessage("Setup completed successfully!");

            // Route to dashboard and prevent navigating back
            Application.Current.MainPage = new NavigationPage(new DashboardPage());
        }
        catch (Exception ex)
        {
            errorLabel.Text = $"❌ Error: {ex.Message}";
            errorLabel.IsVisible = true;
        }
    }
}
