using CommunityToolkit.Maui.Views;
using System;

namespace ExpensifyApp.Pages;

public class AddCommitmentResult
{
    public string Category { get; set; } = "";
    public string SubCategory { get; set; } = "";
    public int Amount { get; set; }
    public int DayOfMonth { get; set; }
}

public partial class AddCommitmentPopup : Popup
{
    public AddCommitmentPopup()
    {
        InitializeComponent();

        // Prepopulate categories
        string[] categories = {
            "Food", "Groceries", "Travel", "Shopping", "Education",
            "Medicine", "Entertainment", "Rent", "Savings", "Loan",
            "Lending", "Other"
        };
        foreach (var cat in categories)
            categoryPicker.Items.Add(cat);
    }

    private void OnAddClicked(object sender, EventArgs e)
    {
        errorLabel.IsVisible = false;

        // Validate Category
        if (categoryPicker.SelectedIndex < 0)
        {
            errorLabel.Text = "❌ Please select a category";
            errorLabel.IsVisible = true;
            return;
        }

        // Validate Amount
        string amountText = amountEntry.Text?.Trim() ?? "";
        if (!int.TryParse(amountText, out int amount) || amount <= 0)
        {
            errorLabel.Text = "❌ Please enter a valid amount greater than 0";
            errorLabel.IsVisible = true;
            return;
        }

        // Validate Day
        string dayText = dayEntry.Text?.Trim() ?? "";
        if (!int.TryParse(dayText, out int day) || day < 1 || day > 31)
        {
            errorLabel.Text = "❌ Please enter a valid day of month (1 to 31)";
            errorLabel.IsVisible = true;
            return;
        }

        Close(new AddCommitmentResult
        {
            Category = categoryPicker.Items[categoryPicker.SelectedIndex],
            SubCategory = subCategoryEntry.Text?.Trim() ?? "",
            Amount = amount,
            DayOfMonth = day
        });
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close(null);
    }
}
