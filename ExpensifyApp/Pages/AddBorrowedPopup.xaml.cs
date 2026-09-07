using CommunityToolkit.Maui.Views;
using System;

namespace ExpensifyApp.Pages;

// ─── Result DTO ─────────────────────────────────────────────────────────────
public class AddBorrowedResult
{
    public string PersonName   { get; set; } = "";
    public string MobileNumber { get; set; } = "";
    public string Relationship { get; set; } = "";
    public int    Amount       { get; set; }
    public string Purpose      { get; set; } = "";
    public DateTime DateBorrowed { get; set; }
    public DateTime DueDate    { get; set; }
    public string Notes        { get; set; } = "";
}

// ─── Popup ──────────────────────────────────────────────────────────────────
public partial class AddBorrowedPopup : Popup
{
    public AddBorrowedPopup()
    {
        InitializeComponent();
        dateBorrowedPicker.Date = DateTime.Today;
        dueDatePicker.Date      = DateTime.Today.AddDays(30);
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        string name = personNameEntry.Text?.Trim() ?? "";
        string amtStr = amountEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(name))
        {
            await Application.Current!.MainPage!.DisplayAlert("Validation", "Please enter the person's name.", "OK");
            return;
        }

        if (!int.TryParse(amtStr, out int amount) || amount <= 0)
        {
            await Application.Current!.MainPage!.DisplayAlert("Validation", "Please enter a valid amount.", "OK");
            return;
        }

        if (dueDatePicker.Date < dateBorrowedPicker.Date)
        {
            await Application.Current!.MainPage!.DisplayAlert("Validation", "Repay date must be after borrowed date.", "OK");
            return;
        }

        Close(new AddBorrowedResult
        {
            PersonName   = name,
            MobileNumber = mobileEntry.Text?.Trim() ?? "",
            Relationship = relationshipPicker.SelectedItem?.ToString() ?? "Friend",
            Amount       = amount,
            Purpose      = purposeEntry.Text?.Trim() ?? "",
            DateBorrowed = dateBorrowedPicker.Date,
            DueDate      = dueDatePicker.Date,
            Notes        = notesEntry.Text?.Trim() ?? ""
        });
    }

    private void OnCancelClicked(object sender, EventArgs e) => Close(null);
}
