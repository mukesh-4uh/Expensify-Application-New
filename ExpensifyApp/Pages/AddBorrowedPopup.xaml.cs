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

        Close(new AddBorrowedResult
        {
            PersonName   = name,
            MobileNumber = "",
            Relationship = "Friend",
            Amount       = amount,
            Purpose      = "",
            DateBorrowed = dateBorrowedPicker.Date,
            DueDate      = dateBorrowedPicker.Date.AddDays(30),
            Notes        = ""
        });
    }

    private void OnCancelClicked(object sender, EventArgs e) => Close(null);
}
