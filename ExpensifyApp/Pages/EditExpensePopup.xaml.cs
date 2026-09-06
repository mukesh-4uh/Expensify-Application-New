using CommunityToolkit.Maui.Views;

namespace ExpensifyApp.Pages;

/// <summary>
/// Result returned from the EditExpensePopup when the user saves.
/// </summary>
public class EditExpenseResult
{
    public int Amount { get; set; }
    public DateTime Date { get; set; }
}

public partial class EditExpensePopup : Popup
{
    private readonly DateTime _originalDate;

    public EditExpensePopup(string category, string subCategory, int currentAmount, DateTime currentDate)
    {
        InitializeComponent();

        _originalDate = currentDate;

        // Display info
        string sub = string.IsNullOrWhiteSpace(subCategory) ? "" : $" • {subCategory}";
        categoryLabel.Text = $"{category}{sub}";

        // Pre-fill fields
        amountEntry.Text = currentAmount.ToString();
        datePicker.Date = currentDate.Date;
        datePicker.MaximumDate = DateTime.Today;
    }

    private void OnSaveClicked(object sender, EventArgs e)
    {
        errorLabel.IsVisible = false;

        // Validate amount
        string amountText = amountEntry.Text?.Trim() ?? "";
        if (!int.TryParse(amountText, out int amount) || amount <= 0)
        {
            errorLabel.Text = "❌ Please enter a valid amount greater than 0";
            errorLabel.IsVisible = true;
            return;
        }

        // Validate date — no future dates
        if (datePicker.Date > DateTime.Today)
        {
            errorLabel.Text = "❌ Future dates are not allowed";
            errorLabel.IsVisible = true;
            return;
        }

        // Preserve the original time component
        var newDate = datePicker.Date.Date.Add(_originalDate.TimeOfDay);

        Close(new EditExpenseResult
        {
            Amount = amount,
            Date = newDate
        });
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close(null);
    }
}
