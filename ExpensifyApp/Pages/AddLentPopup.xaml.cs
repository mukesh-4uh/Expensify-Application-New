using CommunityToolkit.Maui.Views;
using ExpensifyApp.DataBase;
using System;

namespace ExpensifyApp.Pages;

public class AddLentResult
{
    public string PersonName { get; set; } = "";
    public string MobileNumber { get; set; } = "";
    public string Relationship { get; set; } = "Personal";
    public int Amount { get; set; }
    public string Purpose { get; set; } = "General";
    public DateTime DateGiven { get; set; } = DateTime.Today;
    public DateTime DueDate { get; set; } = DateTime.Today;
    public int ReminderDays { get; set; } = 3;
    public string Status { get; set; } = "Pending";
    public string Notes { get; set; } = "";
}

public partial class AddLentPopup : Popup
{
    private readonly LendingTransaction? _existing = null;

    public AddLentPopup(LendingTransaction? existing = null)
    {
        InitializeComponent();
        _existing = existing;

        dueDatePicker.MinimumDate = DateTime.Today;
        dueDatePicker.Date = DateTime.Today.AddMonths(1);

        if (_existing != null)
        {
            popupTitleLabel.Text = "Edit Loan";
            actionButton.Text = "Update ✓";

            nameEntry.Text = _existing.PersonName;
            amountEntry.Text = _existing.Amount.ToString();
            dueDatePicker.Date = _existing.DueDate;
            notesEntry.Text = _existing.Notes;
        }
    }

    private void OnAddClicked(object sender, EventArgs e)
    {
        errorLabel.IsVisible = false;

        string name = nameEntry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name))
        {
            errorLabel.Text = "❌ Please enter the borrower's name";
            errorLabel.IsVisible = true;
            return;
        }

        string amountText = amountEntry.Text?.Trim() ?? "";
        if (!int.TryParse(amountText, out int amount) || amount <= 0)
        {
            errorLabel.Text = "❌ Please enter a valid amount greater than 0";
            errorLabel.IsVisible = true;
            return;
        }

        Close(new AddLentResult
        {
            PersonName = name,
            MobileNumber = _existing?.MobileNumber ?? "",
            Relationship = _existing?.Relationship ?? "Personal",
            Amount = amount,
            Purpose = _existing?.Purpose ?? "General",
            DateGiven = _existing?.DateGiven ?? DateTime.Today,
            DueDate = dueDatePicker.Date,
            ReminderDays = 3,
            Status = _existing?.Status ?? "Pending",
            Notes = notesEntry.Text?.Trim() ?? ""
        });
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close(null);
    }
}
