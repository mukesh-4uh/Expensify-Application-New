using CommunityToolkit.Maui.Views;

namespace ExpensifyApp.Pages;

public class DuplicateExpenseResult
{
    public DateTime Date { get; set; }
}

public partial class DuplicateExpensePopup : Popup
{
    public DuplicateExpensePopup(string category, string subCategory, int amount)
    {
        InitializeComponent();

        string sub = string.IsNullOrWhiteSpace(subCategory) ? "" : $" • {subCategory}";
        infoLabel.Text = $"{category}{sub}";
        amountLabel.Text = $"₹{amount:N0}";

        datePicker.Date = DateTime.Today;
        datePicker.MaximumDate = DateTime.Today;
    }

    private void OnDuplicateClicked(object sender, EventArgs e)
    {
        errorLabel.IsVisible = false;

        if (datePicker.Date > DateTime.Today)
        {
            errorLabel.Text = "❌ Future dates are not allowed";
            errorLabel.IsVisible = true;
            return;
        }

        Close(new DuplicateExpenseResult { Date = datePicker.Date });
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close(null);
    }
}
