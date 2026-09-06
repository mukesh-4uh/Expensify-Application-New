using CommunityToolkit.Maui.Views;

namespace ExpensifyApp.Pages;

public partial class DeleteExpensePopup : Popup
{
    public DeleteExpensePopup(string category, string subCategory, int amount, DateTime date)
    {
        InitializeComponent();

        string sub = string.IsNullOrWhiteSpace(subCategory) ? "" : $" • {subCategory}";
        infoLabel.Text = $"{category}{sub}";
        amountLabel.Text = $"₹{amount:N0}";
        dateLabel.Text = $"📅 {date:dd MMM yyyy}";
    }

    private void OnDeleteClicked(object sender, EventArgs e)
    {
        Close(true);
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close(false);
    }
}
