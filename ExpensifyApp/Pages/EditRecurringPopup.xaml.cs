using CommunityToolkit.Maui.Views;

namespace ExpensifyApp.Pages;

public class EditRecurringResult
{
    public int Amount { get; set; }
    public int DayOfMonth { get; set; }
}

public partial class EditRecurringPopup : Popup
{
    public EditRecurringPopup(string category, string subCategory, int currentAmount, int currentDayOfMonth)
    {
        InitializeComponent();

        string sub = string.IsNullOrWhiteSpace(subCategory) ? "" : $" • {subCategory}";
        categoryLabel.Text = $"{category}{sub}";

        // Pre-fill amount
        amountEntry.Text = currentAmount.ToString();
        dayEntry.Text = currentDayOfMonth.ToString();
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

        // Validate day of month Entry
        string dayText = dayEntry.Text?.Trim() ?? "";
        if (!int.TryParse(dayText, out int day) || day < 1 || day > 31)
        {
            errorLabel.Text = "❌ Please enter a valid day of month (1 to 31)";
            errorLabel.IsVisible = true;
            return;
        }

        Close(new EditRecurringResult
        {
            Amount = amount,
            DayOfMonth = day
        });
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close(null);
    }
}
